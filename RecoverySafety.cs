using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Neck
{
    internal enum RecoveryChangeKind
    {
        Efficiency,
        Turbo
    }

    internal sealed class RecoveryRecord
    {
        public RecoveryChangeKind Kind;
        public DateTime CreatedUtc;
        public int ProcessId;
        public string ProcessName = string.Empty;
        public long StartTimeUtcTicks;
        public uint OriginalPriority;
        public bool PriorityChanged;
        public bool PowerCaptured;
        public bool PowerChanged;
        public ProcessPowerThrottlingState OriginalPower;
        public bool MemoryPriorityCaptured;
        public bool MemoryPriorityChanged;
        public MemoryPriorityInformation OriginalMemoryPriority;

        public string Key
        {
            get
            {
                return Kind + ":" + ProcessId.ToString(CultureInfo.InvariantCulture) + ":" +
                    StartTimeUtcTicks.ToString(CultureInfo.InvariantCulture);
            }
        }
    }

    internal sealed class RecoveryLedger
    {
        private readonly object _syncRoot = new object();
        private readonly string _path;

        public RecoveryLedger(string path)
        {
            _path = path;
        }

        public string Path { get { return _path; } }

        public List<RecoveryRecord> Load()
        {
            lock (_syncRoot) return LoadCore();
        }

        public bool Put(RecoveryRecord record)
        {
            if (record == null || record.ProcessId <= 0 || record.StartTimeUtcTicks <= 0 ||
                string.IsNullOrWhiteSpace(record.ProcessName)) return false;
            lock (_syncRoot)
            {
                List<RecoveryRecord> records = LoadCore();
                records.RemoveAll(item => string.Equals(item.Key, record.Key, StringComparison.Ordinal));
                records.Add(record);
                return SaveCore(records);
            }
        }

        public bool Remove(RecoveryChangeKind kind, int processId, long startTimeUtcTicks)
        {
            lock (_syncRoot)
            {
                List<RecoveryRecord> records = LoadCore();
                int removed = records.RemoveAll(item => item.Kind == kind && item.ProcessId == processId &&
                    item.StartTimeUtcTicks == startTimeUtcTicks);
                return removed == 0 || SaveCore(records);
            }
        }

        private List<RecoveryRecord> LoadCore()
        {
            List<RecoveryRecord> records = new List<RecoveryRecord>();
            try
            {
                if (!File.Exists(_path)) return records;
                foreach (string line in File.ReadAllLines(_path))
                {
                    RecoveryRecord record = Parse(line);
                    if (record != null) records.Add(record);
                }
            }
            catch { }
            return records;
        }

        private bool SaveCore(IList<RecoveryRecord> records)
        {
            string temporary = _path + ".tmp";
            try
            {
                string directory = System.IO.Path.GetDirectoryName(_path);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                File.WriteAllLines(temporary, records.Select(Serialize).ToArray(), new UTF8Encoding(false));
                if (File.Exists(_path)) File.Replace(temporary, _path, null);
                else File.Move(temporary, _path);
                return true;
            }
            catch
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch { }
                return false;
            }
        }

        private static string Serialize(RecoveryRecord record)
        {
            string processName = Convert.ToBase64String(Encoding.UTF8.GetBytes(record.ProcessName ?? string.Empty));
            return string.Join("|", new[]
            {
                "1",
                record.Kind.ToString(),
                record.CreatedUtc.Ticks.ToString(CultureInfo.InvariantCulture),
                record.ProcessId.ToString(CultureInfo.InvariantCulture),
                record.StartTimeUtcTicks.ToString(CultureInfo.InvariantCulture),
                processName,
                record.OriginalPriority.ToString(CultureInfo.InvariantCulture),
                Flag(record.PriorityChanged),
                Flag(record.PowerCaptured),
                Flag(record.PowerChanged),
                record.OriginalPower.Version.ToString(CultureInfo.InvariantCulture),
                record.OriginalPower.ControlMask.ToString(CultureInfo.InvariantCulture),
                record.OriginalPower.StateMask.ToString(CultureInfo.InvariantCulture),
                Flag(record.MemoryPriorityCaptured),
                Flag(record.MemoryPriorityChanged),
                record.OriginalMemoryPriority.MemoryPriority.ToString(CultureInfo.InvariantCulture)
            });
        }

        private static RecoveryRecord Parse(string line)
        {
            try
            {
                string[] parts = (line ?? string.Empty).Split('|');
                if (parts.Length != 16 || parts[0] != "1") return null;
                RecoveryChangeKind kind;
                if (!Enum.TryParse(parts[1], true, out kind)) return null;
                return new RecoveryRecord
                {
                    Kind = kind,
                    CreatedUtc = new DateTime(long.Parse(parts[2], CultureInfo.InvariantCulture), DateTimeKind.Utc),
                    ProcessId = int.Parse(parts[3], CultureInfo.InvariantCulture),
                    StartTimeUtcTicks = long.Parse(parts[4], CultureInfo.InvariantCulture),
                    ProcessName = Encoding.UTF8.GetString(Convert.FromBase64String(parts[5])),
                    OriginalPriority = uint.Parse(parts[6], CultureInfo.InvariantCulture),
                    PriorityChanged = parts[7] == "1",
                    PowerCaptured = parts[8] == "1",
                    PowerChanged = parts[9] == "1",
                    OriginalPower = new ProcessPowerThrottlingState
                    {
                        Version = uint.Parse(parts[10], CultureInfo.InvariantCulture),
                        ControlMask = uint.Parse(parts[11], CultureInfo.InvariantCulture),
                        StateMask = uint.Parse(parts[12], CultureInfo.InvariantCulture)
                    },
                    MemoryPriorityCaptured = parts[13] == "1",
                    MemoryPriorityChanged = parts[14] == "1",
                    OriginalMemoryPriority = new MemoryPriorityInformation
                    {
                        MemoryPriority = uint.Parse(parts[15], CultureInfo.InvariantCulture)
                    }
                };
            }
            catch { return null; }
        }

        private static string Flag(bool value)
        {
            return value ? "1" : "0";
        }
    }

    internal static class RecoveryJournal
    {
        private static readonly string DefaultPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Neck", "recovery-state.log");
        private static RecoveryLedger _ledger = new RecoveryLedger(DefaultPath);

        public static int PendingCount { get { return _ledger.Load().Count; } }
        public static string Path { get { return _ledger.Path; } }

        public static bool Put(RecoveryRecord record) { return _ledger.Put(record); }
        public static bool Remove(RecoveryChangeKind kind, int processId, long startTimeUtcTicks)
        {
            return _ledger.Remove(kind, processId, startTimeUtcTicks);
        }
        public static List<RecoveryRecord> Load() { return _ledger.Load(); }

        internal static void OverridePathForTesting(string path)
        {
            _ledger = new RecoveryLedger(path);
        }
    }

    internal sealed class RecoveryStartupResult
    {
        public bool PreviousSessionInterrupted;
        public int PendingEntries;
        public int RestoredEntries;
        public int StaleEntries;
        public int FailedEntries;

        public string Summary
        {
            get
            {
                if (!PreviousSessionInterrupted && PendingEntries == 0)
                    return "Nenhuma interrupção anterior foi encontrada.";
                if (FailedEntries > 0)
                    return "A recuperação restaurou " + RestoredEntries + " alteração(ões), mas " + FailedEntries + " ainda precisam de nova tentativa.";
                if (RestoredEntries > 0)
                    return "O Neck restaurou " + RestoredEntries + " alteração(ões) deixadas por uma interrupção anterior.";
                return "A interrupção anterior não deixou prioridades pendentes.";
            }
        }
    }

    internal static class RecoveryManager
    {
        private const uint ProcessSetInformation = 0x0200;
        private const uint ProcessQueryLimitedInformation = 0x1000;
        private const int ProcessMemoryPriority = 0;
        private const int ProcessPowerThrottling = 4;
        private static readonly string SessionMarkerPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Neck", "active-session.flag");
        private static RecoveryStartupResult _lastResult = new RecoveryStartupResult();

        public static RecoveryStartupResult LastResult { get { return _lastResult; } }

        public static RecoveryStartupResult BeginSession()
        {
            bool interrupted = false;
            try { interrupted = File.Exists(SessionMarkerPath); }
            catch { }
            RecoveryStartupResult result = RestoreInterruptedChanges();
            result.PreviousSessionInterrupted = interrupted;
            _lastResult = result;
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SessionMarkerPath));
                File.WriteAllText(SessionMarkerPath,
                    DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "|" +
                    typeof(RecoveryManager).Assembly.GetName().Version.ToString(3), new UTF8Encoding(false));
            }
            catch { }
            if (interrupted || result.PendingEntries > 0)
                SupportDiagnostics.RecordEvent("Recuperação", result.Summary);
            return result;
        }

        public static void CompleteSession()
        {
            try
            {
                if (RecoveryJournal.PendingCount == 0 && File.Exists(SessionMarkerPath)) File.Delete(SessionMarkerPath);
            }
            catch { }
        }

        public static RecoveryStartupResult RestoreInterruptedChanges()
        {
            RecoveryStartupResult result = new RecoveryStartupResult();
            List<RecoveryRecord> records = RecoveryJournal.Load();
            result.PendingEntries = records.Count;
            records.Reverse();
            HashSet<string> blockedProcesses = new HashSet<string>(StringComparer.Ordinal);
            foreach (RecoveryRecord record in records)
            {
                string processKey = record.ProcessId.ToString(CultureInfo.InvariantCulture) + ":" +
                    record.StartTimeUtcTicks.ToString(CultureInfo.InvariantCulture);
                if (blockedProcesses.Contains(processKey))
                {
                    result.FailedEntries++;
                    continue;
                }
                if (!RestoreRecord(record, result)) blockedProcesses.Add(processKey);
            }
            _lastResult = result;
            return result;
        }

        private static bool RestoreRecord(RecoveryRecord record, RecoveryStartupResult result)
        {
            Process process = null;
            try
            {
                process = Process.GetProcessById(record.ProcessId);
                if (process.HasExited || !string.Equals(process.ProcessName, record.ProcessName, StringComparison.OrdinalIgnoreCase) ||
                    GetStartTimeUtcTicks(process) != record.StartTimeUtcTicks)
                {
                    RecoveryJournal.Remove(record.Kind, record.ProcessId, record.StartTimeUtcTicks);
                    result.StaleEntries++;
                    return true;
                }
                IntPtr handle = OpenProcess(ProcessSetInformation | ProcessQueryLimitedInformation, false, process.Id);
                if (handle == IntPtr.Zero)
                {
                    result.FailedEntries++;
                    return false;
                }
                bool restored = true;
                try
                {
                    if (record.PriorityChanged && record.OriginalPriority != 0)
                        restored = SetPriorityClass(handle, record.OriginalPriority) && restored;
                    if (record.PowerChanged && record.PowerCaptured)
                    {
                        ProcessPowerThrottlingState power = record.OriginalPower;
                        restored = TrySetPowerState(handle, ref power) && restored;
                    }
                    if (record.MemoryPriorityChanged && record.MemoryPriorityCaptured)
                    {
                        MemoryPriorityInformation memory = record.OriginalMemoryPriority;
                        restored = TrySetMemoryPriority(handle, ref memory) && restored;
                    }
                }
                finally { CloseHandle(handle); }
                if (restored)
                {
                    RecoveryJournal.Remove(record.Kind, record.ProcessId, record.StartTimeUtcTicks);
                    result.RestoredEntries++;
                    return true;
                }
                result.FailedEntries++;
                return false;
            }
            catch (ArgumentException)
            {
                RecoveryJournal.Remove(record.Kind, record.ProcessId, record.StartTimeUtcTicks);
                result.StaleEntries++;
                return true;
            }
            catch
            {
                result.FailedEntries++;
                return false;
            }
            finally { if (process != null) process.Dispose(); }
        }

        private static long GetStartTimeUtcTicks(Process process)
        {
            try { return process.StartTime.ToUniversalTime().Ticks; }
            catch { return 0; }
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

        private static bool TrySetMemoryPriority(IntPtr handle, ref MemoryPriorityInformation state)
        {
            try
            {
                return SetProcessMemoryInformation(handle, ProcessMemoryPriority, ref state,
                    (uint)Marshal.SizeOf(typeof(MemoryPriorityInformation)));
            }
            catch (EntryPointNotFoundException) { return false; }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetPriorityClass(IntPtr processHandle, uint priorityClass);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessInformation(IntPtr processHandle, int processInformationClass,
            ref ProcessPowerThrottlingState processInformation, uint processInformationSize);

        [DllImport("kernel32.dll", EntryPoint = "SetProcessInformation", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessMemoryInformation(IntPtr processHandle, int processInformationClass,
            ref MemoryPriorityInformation processInformation, uint processInformationSize);
    }

    internal static class ApplicationSafety
    {
        private static int _handling;

        public static void Configure()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs args)
            {
                Handle("Interface", args.Exception, true);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs args)
            {
                Handle("Aplicativo", args.ExceptionObject as Exception, false);
            };
            TaskScheduler.UnobservedTaskException += delegate(object sender, UnobservedTaskExceptionEventArgs args)
            {
                SupportDiagnostics.RecordException("Tarefa em segundo plano", args.Exception);
                args.SetObserved();
            };
        }

        public static void RestoreActiveChanges(string reason)
        {
            try { AutopilotProtectionManager.Stop(); }
            catch { }
            try { FocusModeManager.Stop(); }
            catch { }
            try { FocusShieldManager.Stop(); }
            catch { }
            try { TurboModeManager.Stop(); }
            catch { }
            try { EfficiencyModeManager.RestoreAll(); }
            catch { }
            RecoveryStartupResult result = RecoveryManager.RestoreInterruptedChanges();
            SupportDiagnostics.RecordEvent("Restauração", reason + ". " + result.Summary);
        }

        private static void Handle(string scope, Exception exception, bool showMessage)
        {
            if (Interlocked.Exchange(ref _handling, 1) != 0) return;
            try
            {
                SupportDiagnostics.RecordException(scope, exception);
                RestoreActiveChanges("Proteções temporárias encerradas após uma falha");
                if (showMessage)
                {
                    MessageBox.Show(
                        "O Neck encontrou um erro inesperado nesta tela e restaurou as alterações temporárias.\n\n" +
                        "Você pode continuar ou reiniciar o Neck. Um registro sanitizado ficou disponível em Suporte.",
                        "O Neck se recuperou", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch { }
            finally { Interlocked.Exchange(ref _handling, 0); }
        }
    }
}
