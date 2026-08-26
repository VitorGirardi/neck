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

    internal sealed class EfficiencyModeResult
    {
        public string ProcessName;
        public int ProcessesFound;
        public int ProcessesChanged;
        public int PriorityChanges;
        public int EfficiencyChanges;
        public int AccessErrors;

        public bool HasChanges { get { return ProcessesChanged > 0; } }
    }

    internal static class EfficiencyModeManager
    {
        private const uint ProcessSetInformation = 0x0200;
        private const uint ProcessQueryLimitedInformation = 0x1000;
        private const uint NormalPriorityClass = 0x00000020;
        private const uint BelowNormalPriorityClass = 0x00004000;
        private const uint AboveNormalPriorityClass = 0x00008000;
        private const uint HighPriorityClass = 0x00000080;
        private const uint RealtimePriorityClass = 0x00000100;
        private const int ProcessPowerThrottling = 4;
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

        public static bool CanTarget(string processName)
        {
            return !string.IsNullOrWhiteSpace(processName) &&
                   !SosInspector.IsProtectedProcessName(processName) &&
                   !string.Equals(processName, Process.GetCurrentProcess().ProcessName, StringComparison.OrdinalIgnoreCase);
        }

        public static EfficiencyModeResult Apply(string processName)
        {
            EfficiencyModeResult result = NewResult(processName);
            if (!CanTarget(processName)) return result;

            lock (SyncRoot)
            {
                EfficiencyModeSession session;
                if (!Sessions.TryGetValue(processName, out session))
                {
                    session = new EfficiencyModeSession(processName);
                    Sessions.Add(processName, session);
                }
                ApplyToSession(session, result);
                if (session.Processes.Count == 0) Sessions.Remove(processName);
            }
            return result;
        }

        public static EfficiencyModeResult Restore(string processName)
        {
            EfficiencyModeResult result = NewResult(processName);
            lock (SyncRoot)
            {
                EfficiencyModeSession session;
                if (!Sessions.TryGetValue(processName, out session)) return result;
                Sessions.Remove(processName);
                RestoreSession(session, result);
            }
            return result;
        }

        public static EfficiencyModeResult RestoreAll()
        {
            EfficiencyModeResult total = NewResult(string.Empty);
            lock (SyncRoot)
            {
                List<EfficiencyModeSession> sessions = new List<EfficiencyModeSession>(Sessions.Values);
                Sessions.Clear();
                foreach (EfficiencyModeSession session in sessions)
                {
                    EfficiencyModeResult current = NewResult(session.ProcessName);
                    RestoreSession(session, current);
                    AddResult(total, current);
                }
            }
            return total;
        }

        public static void RefreshActiveModes()
        {
            lock (SyncRoot)
            {
                foreach (EfficiencyModeSession session in Sessions.Values)
                {
                    ApplyToSession(session, NewResult(session.ProcessName));
                    RemoveExitedProcesses(session);
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
            Process[] processes;
            try { processes = Process.GetProcessesByName(session.ProcessName); }
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
                saved.StartTimeUtcTicks = GetStartTimeUtcTicks(process);
                saved.OriginalPriority = priority;
                saved.PriorityCaptured = priority != 0;

                ProcessPowerThrottlingState originalPower;
                saved.PowerCaptured = TryGetPowerState(handle, out originalPower);
                saved.OriginalPower = originalPower;

                if (priority == NormalPriorityClass || priority == AboveNormalPriorityClass)
                {
                    saved.PriorityChanged = SetPriorityClass(handle, BelowNormalPriorityClass);
                    if (saved.PriorityChanged) result.PriorityChanges++;
                }

                ProcessPowerThrottlingState efficient = CreateEfficiencyState(true);
                saved.PowerChanged = TrySetPowerState(handle, ref efficient);
                if (saved.PowerChanged) result.EfficiencyChanges++;

                if (saved.PriorityChanged || saved.PowerChanged)
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
            foreach (EfficiencyModeProcessState saved in session.Processes.Values)
            {
                Process process = null;
                try
                {
                    process = Process.GetProcessById(saved.ProcessId);
                    if (!string.Equals(process.ProcessName, session.ProcessName, StringComparison.OrdinalIgnoreCase) || !IsSameProcess(process, saved)) continue;
                    result.ProcessesFound++;
                    RestoreProcess(process, saved, result);
                }
                catch (ArgumentException) { }
                catch { result.AccessErrors++; }
                finally { if (process != null) process.Dispose(); }
            }
        }

        private static void RestoreProcess(Process process, EfficiencyModeProcessState saved, EfficiencyModeResult result)
        {
            IntPtr handle = OpenProcess(ProcessSetInformation | ProcessQueryLimitedInformation, false, process.Id);
            if (handle == IntPtr.Zero)
            {
                result.AccessErrors++;
                return;
            }

            bool changed = false;
            try
            {
                if (saved.PriorityChanged && saved.PriorityCaptured && SetPriorityClass(handle, saved.OriginalPriority))
                {
                    result.PriorityChanges++;
                    changed = true;
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
                }

                if (changed) result.ProcessesChanged++;
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
                        if (process.HasExited || !string.Equals(process.ProcessName, session.ProcessName, StringComparison.OrdinalIgnoreCase) ||
                            !IsSameProcess(process, session.Processes[processId])) exited.Add(processId);
                    }
                }
                catch { exited.Add(processId); }
            }
            foreach (int processId in exited) session.Processes.Remove(processId);
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

        private static void AddResult(EfficiencyModeResult total, EfficiencyModeResult current)
        {
            total.ProcessesFound += current.ProcessesFound;
            total.ProcessesChanged += current.ProcessesChanged;
            total.PriorityChanges += current.PriorityChanges;
            total.EfficiencyChanges += current.EfficiencyChanges;
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

        private sealed class EfficiencyModeSession
        {
            public readonly string ProcessName;
            public readonly Dictionary<int, EfficiencyModeProcessState> Processes = new Dictionary<int, EfficiencyModeProcessState>();

            public EfficiencyModeSession(string processName)
            {
                ProcessName = processName;
            }
        }

        private sealed class EfficiencyModeProcessState
        {
            public int ProcessId;
            public long StartTimeUtcTicks;
            public uint OriginalPriority;
            public bool PriorityCaptured;
            public bool PriorityChanged;
            public ProcessPowerThrottlingState OriginalPower;
            public bool PowerCaptured;
            public bool PowerChanged;
        }
    }
}
