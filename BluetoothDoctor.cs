using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;

namespace Neck
{
    internal sealed class BluetoothAdapterInfo
    {
        public string Name = "";
        public string DeviceId = "";
        public string Manufacturer = "";
        public string DriverVersion = "";
        public DateTime? DriverDate;
        public string Status = "";
        public int ErrorCode;
        public bool DriverBacked;
        public bool SeenByWindows;

        public bool IsReady { get { return SeenByWindows && ErrorCode == 0 && !string.IsNullOrWhiteSpace(DeviceId); } }
        public bool CanRepair { get { return DriverBacked && BluetoothRepairEngine.IsSafeAdapterId(DeviceId); } }
    }

    internal sealed class BluetoothServiceInfo
    {
        public string Name = "";
        public string DisplayName = "";
        public string State = "";
        public string StartMode = "";
        public bool IsRunning { get { return string.Equals(State, "Running", StringComparison.OrdinalIgnoreCase); } }
    }

    internal sealed class BluetoothSnapshot
    {
        public DateTime CapturedUtc = DateTime.UtcNow;
        public DateTime? BootedUtc;
        public List<BluetoothAdapterInfo> Adapters = new List<BluetoothAdapterInfo>();
        public List<BluetoothServiceInfo> Services = new List<BluetoothServiceInfo>();
        public int KnownDeviceEntries;
        public int StaleDeviceEntries;
        public string ReadError = "";
        public BluetoothPowerState PowerState = BluetoothPowerState.Unknown;
        public int RecentTransportTimeouts;
        public int RecentDriverUnloads;
        public DateTime? LastTransportFailureUtc;
        public bool EventHistoryAvailable;

        public BluetoothAdapterInfo PrimaryAdapter
        {
            get { return Adapters.OrderByDescending(item => item.IsReady).ThenByDescending(item => item.CanRepair).FirstOrDefault(); }
        }

        public BluetoothServiceInfo SupportService
        {
            get { return Services.FirstOrDefault(item => string.Equals(item.Name, "bthserv", StringComparison.OrdinalIgnoreCase)); }
        }

        public BluetoothServiceInfo AssociationService
        {
            get { return Services.FirstOrDefault(item => string.Equals(item.Name, "DeviceAssociationService", StringComparison.OrdinalIgnoreCase)); }
        }

        public bool HasDriver
        {
            get { return Adapters.Any(item => item.DriverBacked && !string.IsNullOrWhiteSpace(item.DriverVersion)); }
        }

        public bool HasRecentDriverFailure
        {
            get { return RecentDriverUnloads > 0 || RecentTransportTimeouts >= 2; }
        }

        public bool IsCoreHealthy
        {
            get
            {
                BluetoothAdapterInfo adapter = PrimaryAdapter;
                BluetoothServiceInfo service = SupportService;
                return adapter != null && adapter.IsReady && HasDriver && service != null && service.IsRunning &&
                       PowerState == BluetoothPowerState.On;
            }
        }

        public bool IsHealthy
        {
            get { return IsCoreHealthy && !HasRecentDriverFailure; }
        }
    }

    internal static class BluetoothDoctor
    {
        public static BluetoothSnapshot Read()
        {
            BluetoothSnapshot snapshot = new BluetoothSnapshot();
            Dictionary<string, BluetoothAdapterInfo> adapters = new Dictionary<string, BluetoothAdapterInfo>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> knownDevices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> failures = new List<string>();

            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT DeviceName, DeviceID, Manufacturer, DriverVersion, DriverDate FROM Win32_PnPSignedDriver WHERE DeviceClass = 'BLUETOOTH'"))
                using (ManagementObjectCollection rows = searcher.Get())
                {
                    foreach (ManagementObject row in rows)
                    {
                        string deviceId = Text(row["DeviceID"]);
                        string name = Text(row["DeviceName"]);
                        if (string.IsNullOrWhiteSpace(deviceId) || !LooksLikePhysicalRadio(deviceId, name)) continue;
                        BluetoothAdapterInfo adapter = new BluetoothAdapterInfo
                        {
                            DeviceId = deviceId,
                            Name = name,
                            Manufacturer = Text(row["Manufacturer"]),
                            DriverVersion = Text(row["DriverVersion"]),
                            DriverDate = WmiDate(row["DriverDate"]),
                            DriverBacked = true
                        };
                        adapters[deviceId] = adapter;
                    }
                }
            }
            catch (Exception ex) { failures.Add("driver: " + ex.Message); }

            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT Name, DeviceID, Manufacturer, Status, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE PNPClass = 'Bluetooth'"))
                using (ManagementObjectCollection rows = searcher.Get())
                {
                    foreach (ManagementObject row in rows)
                    {
                        string deviceId = Text(row["DeviceID"]);
                        string name = Text(row["Name"]);
                        int errorCode = Number(row["ConfigManagerErrorCode"]);
                        string knownDevice = KnownDeviceKey(deviceId);
                        if (!string.IsNullOrWhiteSpace(knownDevice)) knownDevices.Add(knownDevice);
                        if (errorCode == 45) snapshot.StaleDeviceEntries++;

                        BluetoothAdapterInfo adapter;
                        if (adapters.TryGetValue(deviceId, out adapter) || LooksLikePhysicalRadio(deviceId, name))
                        {
                            if (adapter == null)
                            {
                                adapter = new BluetoothAdapterInfo { DeviceId = deviceId, Name = name };
                                adapters[deviceId] = adapter;
                            }
                            if (string.IsNullOrWhiteSpace(adapter.Name)) adapter.Name = name;
                            if (string.IsNullOrWhiteSpace(adapter.Manufacturer)) adapter.Manufacturer = Text(row["Manufacturer"]);
                            adapter.Status = Text(row["Status"]);
                            adapter.ErrorCode = errorCode;
                            adapter.SeenByWindows = true;
                        }
                    }
                }
            }
            catch (Exception ex) { failures.Add("adaptador: " + ex.Message); }

            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT Name, DisplayName, State, StartMode FROM Win32_Service WHERE Name = 'bthserv' OR Name = 'DeviceAssociationService' OR Name LIKE 'BluetoothUserService%'"))
                using (ManagementObjectCollection rows = searcher.Get())
                {
                    foreach (ManagementObject row in rows)
                    {
                        snapshot.Services.Add(new BluetoothServiceInfo
                        {
                            Name = Text(row["Name"]),
                            DisplayName = Text(row["DisplayName"]),
                            State = Text(row["State"]),
                            StartMode = Text(row["StartMode"])
                        });
                    }
                }
            }
            catch (Exception ex) { failures.Add("serviços: " + ex.Message); }

            snapshot.BootedUtc = ReadBootTimeUtc(failures);
            ReadTransportFailures(snapshot, failures);

            snapshot.Adapters = adapters.Values
                .Where(item => !string.IsNullOrWhiteSpace(item.DeviceId))
                .OrderByDescending(item => item.IsReady)
                .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            snapshot.KnownDeviceEntries = knownDevices.Count;
            snapshot.PowerState = BluetoothRadioController.ReadState();
            snapshot.ReadError = string.Join("; ", failures.ToArray());
            return snapshot;
        }

        private static void ReadTransportFailures(BluetoothSnapshot snapshot, List<string> failures)
        {
            try
            {
                DateTime cutoffUtc = snapshot.BootedUtc ?? DateTime.UtcNow.Subtract(BluetoothRepairGuard.EventLookback);
                DateTime cutoff = cutoffUtc.ToLocalTime();
                using (EventLog log = new EventLog("System"))
                {
                    EventLogEntryCollection entries = log.Entries;
                    int scanned = 0;
                    for (int index = entries.Count - 1; index >= 0 && scanned < 5000; index--, scanned++)
                    {
                        EventLogEntry entry = entries[index];
                        if (entry.TimeGenerated < cutoff) break;
                        if (string.IsNullOrWhiteSpace(entry.Source) ||
                            entry.Source.IndexOf("BTHUSB", StringComparison.OrdinalIgnoreCase) < 0) continue;

                        int eventId = unchecked((int)(entry.InstanceId & 0xFFFFL));
                        if (eventId != 3 && eventId != 17) continue;

                        if (eventId == 3) snapshot.RecentTransportTimeouts++;
                        else snapshot.RecentDriverUnloads++;

                        DateTime eventUtc = entry.TimeGenerated.ToUniversalTime();
                        if (!snapshot.LastTransportFailureUtc.HasValue || eventUtc > snapshot.LastTransportFailureUtc.Value)
                            snapshot.LastTransportFailureUtc = eventUtc;
                    }
                }
                snapshot.EventHistoryAvailable = true;
            }
            catch (Exception ex)
            {
                failures.Add("histórico BTHUSB: " + ex.Message);
            }
        }

        private static DateTime? ReadBootTimeUtc(List<string> failures)
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT LastBootUpTime FROM Win32_OperatingSystem"))
                using (ManagementObjectCollection rows = searcher.Get())
                {
                    foreach (ManagementObject row in rows)
                    using (row)
                    {
                        DateTime? value = WmiDate(row["LastBootUpTime"]);
                        return value.HasValue ? value.Value.ToUniversalTime() : (DateTime?)null;
                    }
                }
            }
            catch (Exception ex) { failures.Add("inicialização do Windows: " + ex.Message); }
            return null;
        }

        internal static string ExplainErrorCode(int code)
        {
            if (code == 0) return "respondendo normalmente";
            if (code == 22) return "desativado pelo Windows";
            if (code == 28) return "driver não instalado";
            if (code == 31) return "driver não pôde ser carregado";
            if (code == 43) return "o Windows interrompeu o dispositivo";
            if (code == 45) return "não está conectado agora";
            return "erro do Windows (código " + code.ToString(CultureInfo.InvariantCulture) + ")";
        }

        private static bool LooksLikePhysicalRadio(string deviceId, string name)
        {
            if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(name)) return false;
            bool physicalBus = deviceId.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase) ||
                               deviceId.StartsWith("PCI\\", StringComparison.OrdinalIgnoreCase) ||
                               deviceId.StartsWith("ACPI\\", StringComparison.OrdinalIgnoreCase);
            if (!physicalBus) return false;
            string lower = name.ToLowerInvariant();
            return lower.Contains("bluetooth") || lower.Contains("bt adapter") || lower.Contains("bt radio");
        }

        private static string KnownDeviceKey(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId) ||
                (!deviceId.StartsWith("BTHENUM\\DEV_", StringComparison.OrdinalIgnoreCase) &&
                 !deviceId.StartsWith("BTHLE\\DEV_", StringComparison.OrdinalIgnoreCase))) return "";
            int marker = deviceId.IndexOf("DEV_", StringComparison.OrdinalIgnoreCase);
            if (marker < 0) return "";
            int start = marker + 4;
            int end = deviceId.IndexOf('\\', start);
            return end > start ? deviceId.Substring(start, end - start) : deviceId.Substring(start);
        }

        private static string Text(object value)
        {
            return value == null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        }

        private static int Number(object value)
        {
            try { return value == null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture); }
            catch { return 0; }
        }

        private static DateTime? WmiDate(object value)
        {
            string text = Text(value);
            if (string.IsNullOrWhiteSpace(text)) return null;
            try { return ManagementDateTimeConverter.ToDateTime(text); }
            catch { return null; }
        }
    }

    internal sealed class BluetoothRepairResult
    {
        public int ExitCode;
        public string Output = "";
    }

    internal sealed class BluetoothRepairBlock
    {
        public bool IsBlocked;
        public bool UntilRestart;
        public DateTime? UntilUtc;
        public string Reason = "";

        public int RemainingMinutes(DateTime nowUtc)
        {
            if (!IsBlocked || !UntilUtc.HasValue) return 0;
            return Math.Max(1, (int)Math.Ceiling((UntilUtc.Value - nowUtc).TotalMinutes));
        }
    }

    internal static class BluetoothRepairGuard
    {
        internal static readonly TimeSpan EventLookback = TimeSpan.FromHours(6);
        private static readonly TimeSpan RapidRepeatCooldown = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan FailureCooldown = TimeSpan.FromMinutes(10);

        private static string StatePath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Neck", "bluetooth-repair.state");
            }
        }

        public static BluetoothRepairBlock Current(BluetoothSnapshot snapshot)
        {
            return Evaluate(snapshot, DateTime.UtcNow, ReadLastAttemptUtc());
        }

        internal static BluetoothRepairBlock Evaluate(BluetoothSnapshot snapshot, DateTime nowUtc, DateTime? lastAttemptUtc)
        {
            BluetoothRepairBlock block = new BluetoothRepairBlock();
            DateTime until = DateTime.MinValue;
            string reason = "";

            if (lastAttemptUtc.HasValue && snapshot != null && snapshot.BootedUtc.HasValue &&
                lastAttemptUtc.Value < snapshot.BootedUtc.Value) lastAttemptUtc = null;

            if (snapshot != null && snapshot.LastTransportFailureUtc.HasValue &&
                (snapshot.RecentDriverUnloads >= 2 || snapshot.RecentTransportTimeouts >= 4))
            {
                block.IsBlocked = true;
                block.UntilRestart = true;
                block.Reason = "O Windows registrou várias quedas do driver Bluetooth nesta inicialização.";
            }

            if (lastAttemptUtc.HasValue)
            {
                DateTime rapidUntil = lastAttemptUtc.Value.Add(RapidRepeatCooldown);
                if (rapidUntil > until)
                {
                    until = rapidUntil;
                    reason = "Uma correção Bluetooth acabou de ser executada.";
                }

                if (snapshot != null && snapshot.HasRecentDriverFailure && snapshot.LastTransportFailureUtc.HasValue &&
                    snapshot.LastTransportFailureUtc.Value >= lastAttemptUtc.Value.Subtract(TimeSpan.FromSeconds(30)))
                {
                    DateTime failureUntil = snapshot.LastTransportFailureUtc.Value.Add(FailureCooldown);
                    if (failureUntil > until)
                    {
                        until = failureUntil;
                        reason = "O driver voltou a cair depois da última correção.";
                    }
                }
            }

            if (!block.UntilRestart && until > nowUtc)
            {
                block.IsBlocked = true;
                block.UntilUtc = until;
                block.Reason = reason;
            }
            return block;
        }

        public static void RecordAttempt(DateTime attemptedUtc)
        {
            try
            {
                string path = StatePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, attemptedUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                    new UTF8Encoding(false));
            }
            catch { }
        }

        private static DateTime? ReadLastAttemptUtc()
        {
            try
            {
                string path = StatePath;
                if (!File.Exists(path)) return null;
                DateTime value;
                if (!DateTime.TryParse(File.ReadAllText(path).Trim(), CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out value)) return null;
                return value.ToUniversalTime();
            }
            catch { return null; }
        }
    }

    internal static class BluetoothRepairEngine
    {
        public static BluetoothRepairResult Repair()
        {
            StringBuilder report = new StringBuilder();
            BluetoothSnapshot before = BluetoothDoctor.Read();
            BluetoothAdapterInfo initial = before.PrimaryAdapter;
            report.AppendLine("DIAGNÓSTICO INICIAL");
            report.AppendLine(initial == null ? "Adaptador — não detectado" :
                "Adaptador — " + initial.Name + " — " + BluetoothDoctor.ExplainErrorCode(initial.ErrorCode));
            report.AppendLine("Serviço de suporte — " + ServiceState(before.SupportService));
            report.AppendLine("Chave do Bluetooth — " + PowerState(before.PowerState));
            AppendFailureEvidence(report, before);
            report.AppendLine();

            BluetoothRepairBlock block = BluetoothRepairGuard.Current(before);
            if (block.IsBlocked)
            {
                report.AppendLine("PROTEÇÃO ANTI-LOOP");
                report.AppendLine(block.Reason);
                report.AppendLine(block.UntilRestart
                    ? "Novas correções foram bloqueadas até um desligamento completo."
                    : "Nova tentativa pausada por aproximadamente " +
                        block.RemainingMinutes(DateTime.UtcNow).ToString(CultureInfo.InvariantCulture) + " minuto(s).");
                report.AppendLine("Repetir a reinicialização agora apenas faria o adaptador reaparecer e cair novamente.");
                return new BluetoothRepairResult { ExitCode = 3, Output = report.ToString() };
            }

            string pnputil = PnpUtilPath();
            if (!File.Exists(pnputil))
            {
                report.AppendLine("O PnPUtil oficial do Windows não foi encontrado.");
                return new BluetoothRepairResult { ExitCode = 2, Output = report.ToString() };
            }

            BluetoothRepairGuard.RecordAttempt(DateTime.UtcNow);

            StopSupportService(report);

            List<BluetoothAdapterInfo> repairable = before.Adapters.Where(item => item.CanRepair).Take(4).ToList();
            if (repairable.Count == 0)
            {
                report.AppendLine("Adaptador — ainda não havia um rádio validado; iniciando nova detecção.");
                AppendProcess(report, "Nova detecção de hardware", ProcessRunner.Run(pnputil, "/scan-devices", 90000));
                Thread.Sleep(900);
                before = BluetoothDoctor.Read();
                repairable = before.Adapters.Where(item => item.CanRepair).Take(4).ToList();
            }

            foreach (BluetoothAdapterInfo adapter in repairable)
            {
                string command = adapter.ErrorCode == 22 ? "/enable-device " : "/restart-device ";
                string action = adapter.ErrorCode == 22 ? "Reativar " : "Reiniciar ";
                AppendProcess(report, action + adapter.Name,
                    ProcessRunner.Run(pnputil, command + Quote(adapter.DeviceId), 90000));
            }

            AppendProcess(report, "Redetectar hardware", ProcessRunner.Run(pnputil, "/scan-devices", 90000));
            EnsureServiceRunning("DeviceAssociationService", report, "Associação de dispositivos");
            EnsureServiceRunning("bthserv", report, "Suporte a Bluetooth");
            Thread.Sleep(1500);

            BluetoothSnapshot after = ReadStableResult(report);
            BluetoothAdapterInfo finalAdapter = after.PrimaryAdapter;
            report.AppendLine();
            report.AppendLine("RESULTADO");
            report.AppendLine(finalAdapter == null ? "Adaptador — não detectado" :
                "Adaptador — " + finalAdapter.Name + " — " + BluetoothDoctor.ExplainErrorCode(finalAdapter.ErrorCode));
            report.AppendLine("Serviço de suporte — " + ServiceState(after.SupportService));
            report.AppendLine("Chave do Bluetooth — " + PowerState(after.PowerState));
            AppendFailureEvidence(report, after);
            if (after.IsHealthy)
            {
                report.AppendLine("Bluetooth pronto. Os pareamentos foram preservados.");
                return new BluetoothRepairResult { ExitCode = 0, Output = report.ToString() };
            }

            report.AppendLine(after.HasRecentDriverFailure
                ? "O adaptador reapareceu, mas o Windows registrou uma queda recente do driver. A correção não será repetida em ciclo."
                : after.PowerState == BluetoothPowerState.Off
                    ? "O adaptador voltou, mas a chave do Bluetooth continua desligada. Abra as configurações para ligá-la."
                    : "O Bluetooth ainda precisa de atenção. Verifique as atualizações opcionais de driver do Windows.");
            return new BluetoothRepairResult { ExitCode = 2, Output = report.ToString() };
        }

        private static BluetoothSnapshot ReadStableResult(StringBuilder report)
        {
            BluetoothSnapshot snapshot = BluetoothDoctor.Read();
            if (!snapshot.IsCoreHealthy || snapshot.HasRecentDriverFailure) return snapshot;

            const int confirmations = 3;
            for (int confirmation = 1; confirmation <= confirmations; confirmation++)
            {
                Thread.Sleep(2500);
                snapshot = BluetoothDoctor.Read();
                if (!snapshot.IsCoreHealthy || snapshot.HasRecentDriverFailure)
                {
                    report.AppendLine("Validação de estabilidade — o adaptador falhou durante a observação.");
                    return snapshot;
                }
            }
            report.AppendLine("Validação de estabilidade — 4 leituras aprovadas em aproximadamente 8 segundos.");
            return snapshot;
        }

        private static void AppendFailureEvidence(StringBuilder report, BluetoothSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.EventHistoryAvailable) return;
            if (!snapshot.HasRecentDriverFailure)
            {
                report.AppendLine("Falhas BTHUSB recentes — nenhuma falha persistente detectada");
                return;
            }

            string last = snapshot.LastTransportFailureUtc.HasValue
                ? snapshot.LastTransportFailureUtc.Value.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture)
                : "horário não informado";
            report.AppendLine("Falhas BTHUSB recentes — " +
                snapshot.RecentTransportTimeouts.ToString(CultureInfo.InvariantCulture) + " timeout(s), " +
                snapshot.RecentDriverUnloads.ToString(CultureInfo.InvariantCulture) + " descarga(s) do driver; última às " + last);
        }

        internal static bool IsSafeAdapterId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 512 || value.IndexOf('"') >= 0 || value.Any(char.IsControl)) return false;
            return value.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase) ||
                   value.StartsWith("PCI\\", StringComparison.OrdinalIgnoreCase) ||
                   value.StartsWith("ACPI\\", StringComparison.OrdinalIgnoreCase);
        }

        private static void StopSupportService(StringBuilder report)
        {
            BluetoothServiceInfo service = ReadService("bthserv");
            if (service == null || !service.IsRunning)
            {
                report.AppendLine("Serviço de suporte — já estava parado; será iniciado ao final.");
                return;
            }
            bool stopped = InvokeService("bthserv", "StopService", false);
            report.AppendLine("Serviço de suporte — " + (stopped ? "reinicialização iniciada" : "não pôde ser parado; continuando com segurança"));
        }

        private static void EnsureServiceRunning(string name, StringBuilder report, string label)
        {
            BluetoothServiceInfo current = ReadService(name);
            if (current != null && current.IsRunning)
            {
                report.AppendLine(label + " — em execução");
                return;
            }
            bool started = InvokeService(name, "StartService", true);
            report.AppendLine(label + " — " + (started ? "iniciado" : "não respondeu ao comando"));
        }

        private static bool InvokeService(string name, string method, bool expectRunning)
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_Service WHERE Name = '" + name.Replace("'", "''") + "'"))
                using (ManagementObjectCollection rows = searcher.Get())
                {
                    ManagementObject service = rows.Cast<ManagementObject>().FirstOrDefault();
                    if (service == null) return false;
                    using (service) service.InvokeMethod(method, null);
                }
                for (int attempt = 0; attempt < 24; attempt++)
                {
                    Thread.Sleep(250);
                    BluetoothServiceInfo state = ReadService(name);
                    if (state != null && state.IsRunning == expectRunning) return true;
                }
            }
            catch { }
            return false;
        }

        private static BluetoothServiceInfo ReadService(string name)
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT Name, DisplayName, State, StartMode FROM Win32_Service WHERE Name = '" + name.Replace("'", "''") + "'"))
                using (ManagementObjectCollection rows = searcher.Get())
                {
                    foreach (ManagementObject row in rows)
                    using (row)
                    {
                        return new BluetoothServiceInfo
                        {
                            Name = Convert.ToString(row["Name"], CultureInfo.InvariantCulture) ?? "",
                            DisplayName = Convert.ToString(row["DisplayName"], CultureInfo.InvariantCulture) ?? "",
                            State = Convert.ToString(row["State"], CultureInfo.InvariantCulture) ?? "",
                            StartMode = Convert.ToString(row["StartMode"], CultureInfo.InvariantCulture) ?? ""
                        };
                    }
                }
            }
            catch { }
            return null;
        }

        private static void AppendProcess(StringBuilder report, string action, ProcessResult result)
        {
            report.AppendLine(action + " — código " + result.ExitCode.ToString(CultureInfo.InvariantCulture));
            string text = (result.Output ?? "").Trim();
            if (text.Length > 600) text = text.Substring(0, 600).Trim() + "…";
            if (!string.IsNullOrWhiteSpace(text)) report.AppendLine(text);
        }

        private static string ServiceState(BluetoothServiceInfo service)
        {
            if (service == null) return "não encontrado";
            return service.IsRunning ? "em execução" : "parado";
        }

        private static string PowerState(BluetoothPowerState state)
        {
            if (state == BluetoothPowerState.On) return "ligada";
            if (state == BluetoothPowerState.Off) return "desligada";
            if (state == BluetoothPowerState.Disabled) return "bloqueada pelo Windows";
            return "não confirmada";
        }

        private static string PnpUtilPath()
        {
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string system = Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess ? "Sysnative" : "System32";
            return Path.Combine(windows, system, "pnputil.exe");
        }

        private static string Quote(string value)
        {
            return "\"" + value + "\"";
        }
    }
}
