using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Neck
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ProcessPowerThrottlingState
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MemoryPriorityInformation
    {
        public uint MemoryPriority;
    }

    internal enum AdaptiveModeState
    {
        Inactive,
        Waiting,
        Optimized,
        Foreground
    }

    internal sealed class EfficiencyModeResult
    {
        public string ProcessName;
        public int ProcessesFound;
        public int ProcessesChanged;
        public int PriorityChanges;
        public int EfficiencyChanges;
        public int MemoryPriorityChanges;
        public int MemoryPriorityEffective;
        public int ProcessesParked;
        public long WorkingSetReleasedBytes;
        public long AvailableMemoryGainBytes;
        public int AccessErrors;

        public bool HasChanges { get { return ProcessesChanged > 0; } }
    }

    internal static class EfficiencyModeManager
    {
        private const uint ProcessSetInformation = 0x0200;
        private const uint ProcessSetQuota = 0x0100;
        private const uint ProcessQueryLimitedInformation = 0x1000;
        private const uint NormalPriorityClass = 0x00000020;
        private const uint BelowNormalPriorityClass = 0x00004000;
        private const uint AboveNormalPriorityClass = 0x00008000;
        private const uint HighPriorityClass = 0x00000080;
        private const uint RealtimePriorityClass = 0x00000100;
        private const int ProcessMemoryPriority = 0;
        private const int ProcessPowerThrottling = 4;
        private const uint MemoryPriorityLow = 2;
        private const uint PowerThrottlingCurrentVersion = 1;
        private const uint PowerThrottlingExecutionSpeed = 0x1;

        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, EfficiencyModeSession> Sessions =
            new Dictionary<string, EfficiencyModeSession>(StringComparer.OrdinalIgnoreCase);

        public static int ActiveCount
        {
            get { lock (SyncRoot) return Sessions.Count; }
        }

        public static bool IsActive(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return false;
            lock (SyncRoot) return Sessions.ContainsKey(processName);
        }

        public static AdaptiveModeState GetState(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return AdaptiveModeState.Inactive;
            lock (SyncRoot)
            {
                EfficiencyModeSession session;
                if (!Sessions.TryGetValue(processName, out session)) return AdaptiveModeState.Inactive;
                if (session.IsThrottled) return AdaptiveModeState.Optimized;
                return session.IsForeground ? AdaptiveModeState.Foreground : AdaptiveModeState.Waiting;
            }
        }

        public static string GetStateLabel(string processName)
        {
            switch (GetState(processName))
            {
                case AdaptiveModeState.Optimized: return "Otimizado";
                case AdaptiveModeState.Foreground: return "Em uso";
                case AdaptiveModeState.Waiting: return "Aguardando";
                default: return "Normal";
            }
        }

        public static bool CanTarget(string processName)
        {
            return !string.IsNullOrWhiteSpace(processName) &&
                   !SosInspector.IsProtectedProcessName(processName) &&
                   !string.Equals(processName, SystemInfo.CurrentProcessName, StringComparison.OrdinalIgnoreCase);
        }

        public static EfficiencyModeResult Apply(string processName)
        {
            EfficiencyModeResult result = NewResult(processName);
            if (!CanTarget(processName)) return result;

            ulong availableBefore = SystemInfo.GetMemoryStatus().AvailableBytes;

            lock (SyncRoot)
            {
                EfficiencyModeSession session;
                if (!Sessions.TryGetValue(processName, out session))
                {
                    session = new EfficiencyModeSession(processName);
                    Sessions.Add(processName, session);
                }
                session.BackgroundSinceUtc = DateTime.UtcNow;
                session.IsForeground = false;
                ApplyToSession(session, result);
                session.IsThrottled = session.Processes.Count > 0;
                if (session.Processes.Count == 0) Sessions.Remove(processName);
            }
            ulong availableAfter = SystemInfo.GetMemoryStatus().AvailableBytes;
            if (availableAfter > availableBefore) result.AvailableMemoryGainBytes = (long)(availableAfter - availableBefore);
            return result;
        }

        public static EfficiencyModeResult Restore(string processName)
        {
            EfficiencyModeResult result = NewResult(processName);
            lock (SyncRoot)
            {
                EfficiencyModeSession session;
                if (!Sessions.TryGetValue(processName, out session)) return result;
                RestoreSession(session, result);
                if (session.Processes.Count == 0) Sessions.Remove(processName);
            }
            return result;
        }

        public static EfficiencyModeResult RestoreAll()
        {
            EfficiencyModeResult total = NewResult(string.Empty);
            lock (SyncRoot)
            {
                List<EfficiencyModeSession> sessions = new List<EfficiencyModeSession>(Sessions.Values);
                foreach (EfficiencyModeSession session in sessions)
                {
                    EfficiencyModeResult current = NewResult(session.ProcessName);
                    RestoreSession(session, current);
                    if (session.Processes.Count == 0) Sessions.Remove(session.ProcessName);
                    AddResult(total, current);
                }
            }
            return total;
        }

        public static void RefreshAdaptiveModes()
        {
            int foregroundProcessId;
            string foregroundProcessName = GetForegroundProcess(out foregroundProcessId);
            RefreshAdaptiveModesCore(foregroundProcessName, foregroundProcessId, DateTime.UtcNow);
        }

        internal static void RefreshAdaptiveModesForTesting(string foregroundProcessName, DateTime utcNow)
        {
            RefreshAdaptiveModesCore(foregroundProcessName, 0, utcNow);
        }

        private static void RefreshAdaptiveModesCore(string foregroundProcessName, int foregroundProcessId, DateTime utcNow)
        {
            lock (SyncRoot)
            {
                foreach (EfficiencyModeSession session in Sessions.Values)
                {
                    bool foreground = string.Equals(session.ProcessName, foregroundProcessName, StringComparison.OrdinalIgnoreCase) ||
                                      (foregroundProcessId > 0 && ProcessFamilyInspector.IsProcessInFamily(session.ProcessName, foregroundProcessId));
                    session.IsForeground = foreground;
                    if (foreground)
                    {
                        session.BackgroundSinceUtc = DateTime.MinValue;
                        if (session.IsThrottled)
                        {
                            RestoreSession(session, NewResult(session.ProcessName));
                            session.IsThrottled = session.Processes.Count > 0;
                        }
                        continue;
                    }

                    if (session.IsThrottled)
                    {
                        ApplyToSession(session, NewResult(session.ProcessName));
                        RemoveExitedProcesses(session);
                        session.IsThrottled = session.Processes.Count > 0;
                        continue;
                    }

                    if (session.BackgroundSinceUtc == DateTime.MinValue)
                    {
                        session.BackgroundSinceUtc = utcNow;
                        continue;
                    }

                    if (utcNow - session.BackgroundSinceUtc >= TimeSpan.FromSeconds(15))
                    {
                        ApplyToSession(session, NewResult(session.ProcessName));
                        session.IsThrottled = session.Processes.Count > 0;
                    }
                }
            }
        }

        internal static ProcessPowerThrottlingState CreateEfficiencyState(bool enabled)
        {
            ProcessPowerThrottlingState state = new ProcessPowerThrottlingState();
            state.Version = PowerThrottlingCurrentVersion;
            state.ControlMask = PowerThrottlingExecutionSpeed;
            state.StateMask = enabled ? PowerThrottlingExecutionSpeed : 0;
            return state;
        }

        private static void ApplyToSession(EfficiencyModeSession session, EfficiencyModeResult result)
        {
            List<Process> processes;
            try { processes = ProcessFamilyInspector.GetProcesses(session.ProcessName); }
            catch
            {
                result.AccessErrors++;
                return;
            }

            foreach (Process process in processes)
            {
                using (process)
                {
                    result.ProcessesFound++;
                    EfficiencyModeProcessState existing;
                    if (session.Processes.TryGetValue(process.Id, out existing))
                    {
                        if (IsSameProcess(process, existing)) continue;
                        session.Processes.Remove(process.Id);
                    }
                    ApplyToProcess(session, process, result);
                }
            }
        }

        private static void ApplyToProcess(EfficiencyModeSession session, Process process, EfficiencyModeResult result)
        {
            IntPtr handle = OpenProcess(ProcessSetInformation | ProcessQueryLimitedInformation, false, process.Id);
            if (handle == IntPtr.Zero)
            {
                result.AccessErrors++;
                return;
            }

            try
            {
                uint priority = GetPriorityClass(handle);
                if (priority == HighPriorityClass || priority == RealtimePriorityClass)
                {
                    result.AccessErrors++;
                    return;
                }

                EfficiencyModeProcessState saved = new EfficiencyModeProcessState();
                saved.ProcessId = process.Id;
                saved.ProcessName = process.ProcessName;
                saved.StartTimeUtcTicks = GetStartTimeUtcTicks(process);
                saved.OriginalPriority = priority;
                saved.PriorityCaptured = priority != 0;

                ProcessPowerThrottlingState originalPower;
                saved.PowerCaptured = TryGetPowerState(handle, out originalPower);
                saved.OriginalPower = originalPower;

                MemoryPriorityInformation originalMemory;
                saved.MemoryPriorityCaptured = TryGetMemoryPriority(handle, out originalMemory);
                saved.OriginalMemoryPriority = originalMemory;

                bool priorityPlanned = priority == NormalPriorityClass || priority == AboveNormalPriorityClass;
                bool powerPlanned = saved.PowerCaptured;
                bool memoryPlanned = saved.MemoryPriorityCaptured && saved.OriginalMemoryPriority.MemoryPriority > MemoryPriorityLow;
                RecoveryRecord recovery = new RecoveryRecord
                {
                    Kind = RecoveryChangeKind.Efficiency,
                    CreatedUtc = DateTime.UtcNow,
                    ProcessId = saved.ProcessId,
                    ProcessName = saved.ProcessName,
                    StartTimeUtcTicks = saved.StartTimeUtcTicks,
                    OriginalPriority = saved.OriginalPriority,
                    PriorityChanged = priorityPlanned,
                    PowerCaptured = saved.PowerCaptured,
                    PowerChanged = powerPlanned,
                    OriginalPower = saved.OriginalPower,
                    MemoryPriorityCaptured = saved.MemoryPriorityCaptured,
                    MemoryPriorityChanged = memoryPlanned,
                    OriginalMemoryPriority = saved.OriginalMemoryPriority
                };
                bool recoveryReady = (!priorityPlanned && !powerPlanned && !memoryPlanned) || RecoveryJournal.Put(recovery);
                if (!recoveryReady) result.AccessErrors++;

                if (recoveryReady && priorityPlanned)
                {
                    saved.PriorityChanged = SetPriorityClass(handle, BelowNormalPriorityClass);
                    if (saved.PriorityChanged) result.PriorityChanges++;
                }

                if (recoveryReady && powerPlanned)
                {
                    ProcessPowerThrottlingState efficient = CreateEfficiencyState(true);
                    saved.PowerChanged = TrySetPowerState(handle, ref efficient);
                    if (saved.PowerChanged) result.EfficiencyChanges++;
                }

                if (saved.MemoryPriorityCaptured && saved.OriginalMemoryPriority.MemoryPriority <= MemoryPriorityLow)
                {
                    result.MemoryPriorityEffective++;
                }
                else if (recoveryReady && memoryPlanned)
                {
                    MemoryPriorityInformation lowMemory = new MemoryPriorityInformation { MemoryPriority = MemoryPriorityLow };
                    saved.MemoryPriorityChanged = TrySetMemoryPriority(handle, ref lowMemory);
                    if (saved.MemoryPriorityChanged)
                    {
                        result.MemoryPriorityChanges++;
                        result.MemoryPriorityEffective++;
                    }
                }

                saved.WorkingSetParked = TryParkWorkingSet(process, result);

                recovery.PriorityChanged = saved.PriorityChanged;
                recovery.PowerChanged = saved.PowerChanged;
                recovery.MemoryPriorityChanged = saved.MemoryPriorityChanged;
                if (saved.PriorityChanged || saved.PowerChanged || saved.MemoryPriorityChanged) RecoveryJournal.Put(recovery);
                else RecoveryJournal.Remove(RecoveryChangeKind.Efficiency, saved.ProcessId, saved.StartTimeUtcTicks);

                if (saved.PriorityChanged || saved.PowerChanged || saved.MemoryPriorityChanged || saved.WorkingSetParked)
                {
                    result.ProcessesChanged++;
                    session.Processes[process.Id] = saved;
                }
                else
                {
                    result.AccessErrors++;
                }
            }
            catch (Exception ex)
            {
                if (ex is Win32Exception || ex is InvalidOperationException || ex is NotSupportedException || ex is EntryPointNotFoundException)
                    result.AccessErrors++;
                else
                    throw;
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        private static void RestoreSession(EfficiencyModeSession session, EfficiencyModeResult result)
        {
            List<int> restored = new List<int>();
            foreach (EfficiencyModeProcessState saved in new List<EfficiencyModeProcessState>(session.Processes.Values))
            {
                Process process = null;
                try
                {
                    process = Process.GetProcessById(saved.ProcessId);
                    if (!string.Equals(process.ProcessName, saved.ProcessName, StringComparison.OrdinalIgnoreCase) || !IsSameProcess(process, saved))
                    {
                        RecoveryJournal.Remove(RecoveryChangeKind.Efficiency, saved.ProcessId, saved.StartTimeUtcTicks);
                        restored.Add(saved.ProcessId);
                        continue;
                    }
                    result.ProcessesFound++;
                    if (RestoreProcess(process, saved, result)) restored.Add(saved.ProcessId);
                }
                catch (ArgumentException)
                {
                    RecoveryJournal.Remove(RecoveryChangeKind.Efficiency, saved.ProcessId, saved.StartTimeUtcTicks);
                    restored.Add(saved.ProcessId);
                }
                catch { result.AccessErrors++; }
                finally { if (process != null) process.Dispose(); }
            }
            foreach (int processId in restored) session.Processes.Remove(processId);
        }

        private static bool RestoreProcess(Process process, EfficiencyModeProcessState saved, EfficiencyModeResult result)
        {
            IntPtr handle = OpenProcess(ProcessSetInformation | ProcessQueryLimitedInformation, false, process.Id);
            if (handle == IntPtr.Zero)
            {
                result.AccessErrors++;
                return false;
            }

            bool changed = false;
            bool restored = true;
            try
            {
                if (saved.PriorityChanged && saved.PriorityCaptured)
                {
                    if (SetPriorityClass(handle, saved.OriginalPriority))
                    {
                        result.PriorityChanges++;
                        changed = true;
                    }
                    else restored = false;
                }

                if (saved.PowerChanged)
                {
                    ProcessPowerThrottlingState state = saved.PowerCaptured
                        ? saved.OriginalPower
                        : new ProcessPowerThrottlingState { Version = PowerThrottlingCurrentVersion, ControlMask = 0, StateMask = 0 };
                    if (TrySetPowerState(handle, ref state))
                    {
                        result.EfficiencyChanges++;
                        changed = true;
                    }
                    else restored = false;
                }

                if (saved.MemoryPriorityChanged && saved.MemoryPriorityCaptured)
                {
                    MemoryPriorityInformation memory = saved.OriginalMemoryPriority;
                    if (TrySetMemoryPriority(handle, ref memory))
                    {
                        result.MemoryPriorityChanges++;
                        changed = true;
                    }
                    else restored = false;
                }

                if (changed || saved.WorkingSetParked) result.ProcessesChanged++;
                if (restored) RecoveryJournal.Remove(RecoveryChangeKind.Efficiency, saved.ProcessId, saved.StartTimeUtcTicks);
                else result.AccessErrors++;
                return restored;
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        private static void RemoveExitedProcesses(EfficiencyModeSession session)
        {
            List<int> exited = new List<int>();
            foreach (int processId in session.Processes.Keys)
            {
                try
                {
                    using (Process process = Process.GetProcessById(processId))
                    {
                        if (process.HasExited || !string.Equals(process.ProcessName, session.Processes[processId].ProcessName, StringComparison.OrdinalIgnoreCase) ||
                            !IsSameProcess(process, session.Processes[processId])) exited.Add(processId);
                    }
                }
                catch { exited.Add(processId); }
            }
            foreach (int processId in exited)
            {
                EfficiencyModeProcessState saved = session.Processes[processId];
                RecoveryJournal.Remove(RecoveryChangeKind.Efficiency, saved.ProcessId, saved.StartTimeUtcTicks);
                session.Processes.Remove(processId);
            }
        }

        private static bool TryGetPowerState(IntPtr handle, out ProcessPowerThrottlingState state)
        {
            state = new ProcessPowerThrottlingState();
            try
            {
                return GetProcessInformation(handle, ProcessPowerThrottling, out state,
                    (uint)Marshal.SizeOf(typeof(ProcessPowerThrottlingState)));
            }
            catch (EntryPointNotFoundException) { return false; }
        }

        private static bool TrySetPowerState(IntPtr handle, ref ProcessPowerThrottlingState state)
        {
            try
            {
                return SetProcessInformation(handle, ProcessPowerThrottling, ref state,
                    (uint)Marshal.SizeOf(typeof(ProcessPowerThrottlingState)));
            }
            catch (EntryPointNotFoundException) { return false; }
        }

        private static bool TryGetMemoryPriority(IntPtr handle, out MemoryPriorityInformation state)
        {
            state = new MemoryPriorityInformation();
            try
            {
                return GetProcessMemoryInformation(handle, ProcessMemoryPriority, out state,
                    (uint)Marshal.SizeOf(typeof(MemoryPriorityInformation)));
            }
            catch (EntryPointNotFoundException) { return false; }
        }

        private static bool TrySetMemoryPriority(IntPtr handle, ref MemoryPriorityInformation state)
        {
            try
            {
                return SetProcessMemoryInformation(handle, ProcessMemoryPriority, ref state,
                    (uint)Marshal.SizeOf(typeof(MemoryPriorityInformation)));
            }
            catch (EntryPointNotFoundException) { return false; }
        }

        private static bool TryParkWorkingSet(Process process, EfficiencyModeResult result)
        {
            long before;
            try { before = Math.Max(0, process.WorkingSet64); }
            catch { before = 0; }

            IntPtr handle = OpenProcess(ProcessSetQuota | ProcessQueryLimitedInformation, false, process.Id);
            if (handle == IntPtr.Zero) return false;
            bool parked;
            try { parked = K32EmptyWorkingSet(handle); }
            catch (EntryPointNotFoundException) { parked = false; }
            finally { CloseHandle(handle); }
            if (!parked) return false;

            long after;
            try
            {
                process.Refresh();
                after = Math.Max(0, process.WorkingSet64);
            }
            catch { after = before; }
            result.ProcessesParked++;
            result.WorkingSetReleasedBytes += Math.Max(0, before - after);
            return true;
        }

        private static EfficiencyModeResult NewResult(string processName)
        {
            return new EfficiencyModeResult { ProcessName = processName ?? string.Empty };
        }

        private static long GetStartTimeUtcTicks(Process process)
        {
            try { return process.StartTime.ToUniversalTime().Ticks; }
            catch { return 0; }
        }

        private static bool IsSameProcess(Process process, EfficiencyModeProcessState saved)
        {
            if (saved.StartTimeUtcTicks == 0) return true;
            long current = GetStartTimeUtcTicks(process);
            return current != 0 && current == saved.StartTimeUtcTicks;
        }

        private static string GetForegroundProcess(out int foregroundProcessId)
        {
            foregroundProcessId = 0;
            IntPtr foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero) return string.Empty;
            uint processId;
            GetWindowThreadProcessId(foreground, out processId);
            if (processId == 0) return string.Empty;
            foregroundProcessId = (int)processId;
            try
            {
                using (Process process = Process.GetProcessById((int)processId))
                {
                    return process.ProcessName;
                }
            }
            catch { return string.Empty; }
        }

        private static void AddResult(EfficiencyModeResult total, EfficiencyModeResult current)
        {
            total.ProcessesFound += current.ProcessesFound;
            total.ProcessesChanged += current.ProcessesChanged;
            total.PriorityChanges += current.PriorityChanges;
            total.EfficiencyChanges += current.EfficiencyChanges;
            total.MemoryPriorityChanges += current.MemoryPriorityChanges;
            total.MemoryPriorityEffective += current.MemoryPriorityEffective;
            total.ProcessesParked += current.ProcessesParked;
            total.WorkingSetReleasedBytes += current.WorkingSetReleasedBytes;
            total.AvailableMemoryGainBytes += current.AvailableMemoryGainBytes;
            total.AccessErrors += current.AccessErrors;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetPriorityClass(IntPtr processHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetPriorityClass(IntPtr processHandle, uint priorityClass);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetProcessInformation(IntPtr processHandle, int processInformationClass,
            out ProcessPowerThrottlingState processInformation, uint processInformationSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessInformation(IntPtr processHandle, int processInformationClass,
            ref ProcessPowerThrottlingState processInformation, uint processInformationSize);

        [DllImport("kernel32.dll", EntryPoint = "GetProcessInformation", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetProcessMemoryInformation(IntPtr processHandle, int processInformationClass,
            out MemoryPriorityInformation processInformation, uint processInformationSize);

        [DllImport("kernel32.dll", EntryPoint = "SetProcessInformation", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessMemoryInformation(IntPtr processHandle, int processInformationClass,
            ref MemoryPriorityInformation processInformation, uint processInformationSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool K32EmptyWorkingSet(IntPtr processHandle);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

        private sealed class EfficiencyModeSession
        {
            public readonly string ProcessName;
            public readonly Dictionary<int, EfficiencyModeProcessState> Processes = new Dictionary<int, EfficiencyModeProcessState>();
            public bool IsThrottled;
            public bool IsForeground;
            public DateTime BackgroundSinceUtc;

            public EfficiencyModeSession(string processName)
            {
                ProcessName = processName;
            }
        }

        private sealed class EfficiencyModeProcessState
        {
            public int ProcessId;
            public string ProcessName;
            public long StartTimeUtcTicks;
            public uint OriginalPriority;
            public bool PriorityCaptured;
            public bool PriorityChanged;
            public ProcessPowerThrottlingState OriginalPower;
            public bool PowerCaptured;
            public bool PowerChanged;
            public MemoryPriorityInformation OriginalMemoryPriority;
            public bool MemoryPriorityCaptured;
            public bool MemoryPriorityChanged;
            public bool WorkingSetParked;
        }
    }
}
