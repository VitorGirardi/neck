using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Neck
{
    internal sealed class TurboModeResult
    {
        public string ProcessName = string.Empty;
        public int ProcessesFound;
        public int ProcessesChanged;
        public int AccessErrors;
    }

    internal static class TurboModeManager
    {
        private const uint ProcessSetInformation = 0x0200;
        private const uint ProcessQueryLimitedInformation = 0x1000;
        private const uint NormalPriorityClass = 0x00000020;
        private const uint BelowNormalPriorityClass = 0x00004000;
        private const uint AboveNormalPriorityClass = 0x00008000;
        private const uint HighPriorityClass = 0x00000080;
        private const uint RealtimePriorityClass = 0x00000100;

        private static readonly object SyncRoot = new object();
        private static TurboSession _session;

        public static bool IsActive
        {
            get { lock (SyncRoot) return _session != null && _session.EndsAtUtc > DateTime.UtcNow; }
        }

        public static bool IsForeground
        {
            get { lock (SyncRoot) return _session != null && _session.IsForeground; }
        }

        public static string ActiveProcessName
        {
            get { lock (SyncRoot) return _session == null ? string.Empty : _session.ProcessName; }
        }

        public static string ActiveDisplayName
        {
            get { lock (SyncRoot) return _session == null ? string.Empty : _session.DisplayName; }
        }

        public static TimeSpan Remaining
        {
            get
            {
                lock (SyncRoot)
                {
                    if (_session == null) return TimeSpan.Zero;
                    TimeSpan remaining = _session.EndsAtUtc - DateTime.UtcNow;
                    return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
                }
            }
        }

        public static bool IsTarget(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return false;
            lock (SyncRoot)
            {
                return _session != null && _session.EndsAtUtc > DateTime.UtcNow &&
                       string.Equals(_session.ProcessName, processName, StringComparison.OrdinalIgnoreCase);
            }
        }

        public static string GetStateLabel(string processName)
        {
            lock (SyncRoot)
            {
                if (_session == null || _session.EndsAtUtc <= DateTime.UtcNow ||
                    !string.Equals(_session.ProcessName, processName, StringComparison.OrdinalIgnoreCase)) return string.Empty;
                return _session.IsForeground ? "Turbo ativo" : "Turbo pronto";
            }
        }

        public static TurboModeResult Start(string processName, string displayName, int durationMinutes)
        {
            TurboModeResult result = NewResult(processName);
            if (!EfficiencyModeManager.CanTarget(processName)) return result;
            lock (SyncRoot)
            {
                StopCore(result);
                _session = new TurboSession
                {
                    ProcessName = processName,
                    DisplayName = string.IsNullOrWhiteSpace(displayName) ? SystemInfo.FriendlyProcessName(processName) : displayName,
                    EndsAtUtc = DateTime.UtcNow.AddMinutes(Math.Max(15, Math.Min(180, durationMinutes)))
                };
            }
            Refresh();
            return result;
        }

        public static TurboModeResult Stop()
        {
            TurboModeResult result = NewResult(ActiveProcessName);
            lock (SyncRoot) StopCore(result);
            return result;
        }

        public static void Refresh()
        {
            int foregroundProcessId;
            string foregroundProcessName = GetForegroundProcess(out foregroundProcessId);
            RefreshCore(foregroundProcessName, foregroundProcessId, DateTime.UtcNow);
        }

        internal static void RefreshForTesting(string foregroundProcessName, DateTime utcNow)
        {
            RefreshCore(foregroundProcessName, 0, utcNow);
        }

        private static void RefreshCore(string foregroundProcessName, int foregroundProcessId, DateTime utcNow)
        {
            lock (SyncRoot)
            {
                if (_session == null) return;
                if (utcNow >= _session.EndsAtUtc)
                {
                    StopCore(NewResult(_session.ProcessName));
                    return;
                }

                bool foreground = string.Equals(_session.ProcessName, foregroundProcessName, StringComparison.OrdinalIgnoreCase) ||
                                  (foregroundProcessId > 0 && ProcessFamilyInspector.IsProcessInFamily(_session.ProcessName, foregroundProcessId));
                _session.IsForeground = foreground;
                if (foreground) ApplyToForeground(_session, NewResult(_session.ProcessName));
                else RestoreProcesses(_session, NewResult(_session.ProcessName));
            }
        }

        private static void ApplyToForeground(TurboSession session, TurboModeResult result)
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
                    TurboProcessState existing;
                    if (session.Processes.TryGetValue(process.Id, out existing))
                    {
                        if (IsSameProcess(process, existing)) continue;
                        session.Processes.Remove(process.Id);
                    }
                    ApplyToProcess(session, process, result);
                }
            }
            RemoveExitedProcesses(session);
        }

        private static void ApplyToProcess(TurboSession session, Process process, TurboModeResult result)
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
                if (priority == 0 || priority == HighPriorityClass || priority == RealtimePriorityClass || priority == AboveNormalPriorityClass)
                    return;
                if (priority != NormalPriorityClass && priority != BelowNormalPriorityClass) return;

                TurboProcessState saved = new TurboProcessState
                {
                    ProcessId = process.Id,
                    ProcessName = process.ProcessName,
                    StartTimeUtcTicks = GetStartTimeUtcTicks(process),
                    OriginalPriority = priority
                };
                if (SetPriorityClass(handle, AboveNormalPriorityClass))
                {
                    session.Processes[process.Id] = saved;
                    result.ProcessesChanged++;
                }
                else result.AccessErrors++;
            }
            catch { result.AccessErrors++; }
            finally { CloseHandle(handle); }
        }

        private static void StopCore(TurboModeResult result)
        {
            if (_session == null) return;
            RestoreProcesses(_session, result);
            _session = null;
        }

        private static void RestoreProcesses(TurboSession session, TurboModeResult result)
        {
            List<int> restored = new List<int>();
            foreach (TurboProcessState saved in new List<TurboProcessState>(session.Processes.Values))
            {
                Process process = null;
                try
                {
                    process = Process.GetProcessById(saved.ProcessId);
                    if (!IsSameProcess(process, saved))
                    {
                        restored.Add(saved.ProcessId);
                        continue;
                    }
                    result.ProcessesFound++;
                    IntPtr handle = OpenProcess(ProcessSetInformation | ProcessQueryLimitedInformation, false, saved.ProcessId);
                    if (handle == IntPtr.Zero)
                    {
                        result.AccessErrors++;
                        continue;
                    }
                    try
                    {
                        if (SetPriorityClass(handle, saved.OriginalPriority))
                        {
                            restored.Add(saved.ProcessId);
                            result.ProcessesChanged++;
                        }
                        else result.AccessErrors++;
                    }
                    finally { CloseHandle(handle); }
                }
                catch (ArgumentException) { restored.Add(saved.ProcessId); }
                catch { result.AccessErrors++; }
                finally { if (process != null) process.Dispose(); }
            }
            foreach (int processId in restored) session.Processes.Remove(processId);
        }

        private static void RemoveExitedProcesses(TurboSession session)
        {
            List<int> exited = new List<int>();
            foreach (TurboProcessState saved in session.Processes.Values)
            {
                try
                {
                    using (Process process = Process.GetProcessById(saved.ProcessId))
                        if (!IsSameProcess(process, saved)) exited.Add(saved.ProcessId);
                }
                catch { exited.Add(saved.ProcessId); }
            }
            foreach (int processId in exited) session.Processes.Remove(processId);
        }

        private static bool IsSameProcess(Process process, TurboProcessState saved)
        {
            try
            {
                return string.Equals(process.ProcessName, saved.ProcessName, StringComparison.OrdinalIgnoreCase) &&
                       GetStartTimeUtcTicks(process) == saved.StartTimeUtcTicks;
            }
            catch { return false; }
        }

        private static long GetStartTimeUtcTicks(Process process)
        {
            try { return process.StartTime.ToUniversalTime().Ticks; }
            catch { return 0; }
        }

        private static string GetForegroundProcess(out int foregroundProcessId)
        {
            foregroundProcessId = 0;
            IntPtr foreground = NativeMethods.GetForegroundWindow();
            if (foreground == IntPtr.Zero) return string.Empty;
            uint processId;
            GetWindowThreadProcessId(foreground, out processId);
            if (processId == 0) return string.Empty;
            foregroundProcessId = (int)processId;
            try
            {
                using (Process process = Process.GetProcessById((int)processId)) return process.ProcessName;
            }
            catch { return string.Empty; }
        }

        private static TurboModeResult NewResult(string processName)
        {
            return new TurboModeResult { ProcessName = processName ?? string.Empty };
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

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

        private sealed class TurboSession
        {
            public string ProcessName;
            public string DisplayName;
            public DateTime EndsAtUtc;
            public bool IsForeground;
            public readonly Dictionary<int, TurboProcessState> Processes = new Dictionary<int, TurboProcessState>();
        }

        private sealed class TurboProcessState
        {
            public int ProcessId;
            public string ProcessName;
            public long StartTimeUtcTicks;
            public uint OriginalPriority;
        }
    }
}
