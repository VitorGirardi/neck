using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Neck
{
    internal sealed class ProcessFamilyMetrics
    {
        public int ProcessCount;
        public long WorkingSetBytes;
    }

    internal static class ProcessFamilyInspector
    {
        private const uint Th32csSnapProcess = 0x00000002;
        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

        public static ProcessFamilyMetrics GetMetrics(string rootProcessName)
        {
            ProcessFamilyMetrics metrics = new ProcessFamilyMetrics();
            foreach (Process process in GetProcesses(rootProcessName))
            {
                using (process)
                {
                    try
                    {
                        metrics.ProcessCount++;
                        metrics.WorkingSetBytes += Math.Max(0, process.WorkingSet64);
                    }
                    catch { }
                }
            }
            return metrics;
        }

        public static List<Process> GetProcesses(string rootProcessName)
        {
            if (string.IsNullOrWhiteSpace(rootProcessName) || SosInspector.IsProtectedProcessName(rootProcessName))
                return new List<Process>();

            List<ProcessTreeEntry> entries = CaptureTree();
            if (entries.Count == 0) return FallbackByName(rootProcessName);

            HashSet<int> familyIds = new HashSet<int>();
            foreach (ProcessTreeEntry entry in entries)
            {
                if (string.Equals(entry.ProcessName, rootProcessName, StringComparison.OrdinalIgnoreCase))
                    familyIds.Add(entry.ProcessId);
            }

            bool expanded;
            do
            {
                expanded = false;
                foreach (ProcessTreeEntry entry in entries)
                {
                    if (familyIds.Contains(entry.ProcessId) || !familyIds.Contains(entry.ParentProcessId) ||
                        SosInspector.IsProtectedProcessName(entry.ProcessName)) continue;
                    familyIds.Add(entry.ProcessId);
                    expanded = true;
                }
            }
            while (expanded);

            int currentId;
            using (Process current = Process.GetCurrentProcess()) currentId = current.Id;
            Dictionary<int, string> expectedNames = new Dictionary<int, string>();
            foreach (ProcessTreeEntry entry in entries) expectedNames[entry.ProcessId] = entry.ProcessName;
            List<Process> processes = new List<Process>();
            foreach (int processId in familyIds)
            {
                if (processId == currentId) continue;
                try
                {
                    Process process = Process.GetProcessById(processId);
                    string expectedName;
                    if (!expectedNames.TryGetValue(processId, out expectedName) ||
                        !string.Equals(process.ProcessName, expectedName, StringComparison.OrdinalIgnoreCase) ||
                        SosInspector.IsProtectedProcessName(process.ProcessName))
                    {
                        process.Dispose();
                        continue;
                    }
                    processes.Add(process);
                }
                catch { }
            }
            return processes;
        }

        public static bool IsProcessInFamily(string rootProcessName, int processId)
        {
            if (processId <= 0) return false;
            return BuildFamilyIds(rootProcessName, CaptureTree()).Contains(processId);
        }

        internal static HashSet<int> BuildFamilyIds(string rootProcessName, IList<ProcessTreeEntry> entries)
        {
            HashSet<int> familyIds = new HashSet<int>();
            foreach (ProcessTreeEntry entry in entries)
            {
                if (string.Equals(entry.ProcessName, rootProcessName, StringComparison.OrdinalIgnoreCase)) familyIds.Add(entry.ProcessId);
            }
            bool expanded;
            do
            {
                expanded = false;
                foreach (ProcessTreeEntry entry in entries)
                {
                    if (familyIds.Contains(entry.ProcessId) || !familyIds.Contains(entry.ParentProcessId) ||
                        SosInspector.IsProtectedProcessName(entry.ProcessName)) continue;
                    familyIds.Add(entry.ProcessId);
                    expanded = true;
                }
            }
            while (expanded);
            return familyIds;
        }

        private static List<ProcessTreeEntry> CaptureTree()
        {
            List<ProcessTreeEntry> entries = new List<ProcessTreeEntry>();
            IntPtr snapshot = CreateToolhelp32Snapshot(Th32csSnapProcess, 0);
            if (snapshot == InvalidHandleValue) return entries;
            try
            {
                ProcessEntry32 native = new ProcessEntry32();
                native.Size = (uint)Marshal.SizeOf(typeof(ProcessEntry32));
                if (!Process32First(snapshot, ref native)) return entries;
                do
                {
                    entries.Add(new ProcessTreeEntry
                    {
                        ProcessId = (int)native.ProcessId,
                        ParentProcessId = (int)native.ParentProcessId,
                        ProcessName = Path.GetFileNameWithoutExtension(native.ExecutableFile ?? string.Empty)
                    });
                    native.Size = (uint)Marshal.SizeOf(typeof(ProcessEntry32));
                }
                while (Process32Next(snapshot, ref native));
            }
            finally
            {
                CloseHandle(snapshot);
            }
            return entries;
        }

        private static List<Process> FallbackByName(string processName)
        {
            try { return new List<Process>(Process.GetProcessesByName(processName)); }
            catch { return new List<Process>(); }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry32 entry);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry32 entry);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ProcessEntry32
        {
            public uint Size;
            public uint Usage;
            public uint ProcessId;
            public IntPtr DefaultHeapId;
            public uint ModuleId;
            public uint Threads;
            public uint ParentProcessId;
            public int BasePriority;
            public uint Flags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string ExecutableFile;
        }
    }

    internal sealed class ProcessTreeEntry
    {
        public int ProcessId;
        public int ParentProcessId;
        public string ProcessName;
    }
}
