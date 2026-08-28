using System;
using System.Collections.Generic;
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
        public List<BluetoothAdapterInfo> Adapters = new List<BluetoothAdapterInfo>();
        public List<BluetoothServiceInfo> Services = new List<BluetoothServiceInfo>();
        public int KnownDeviceEntries;
        public int StaleDeviceEntries;
        public string ReadError = "";
        public BluetoothPowerState PowerState = BluetoothPowerState.Unknown;

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

        public bool IsHealthy
        {
            get
            {
                BluetoothAdapterInfo adapter = PrimaryAdapter;
                BluetoothServiceInfo service = SupportService;
                return adapter != null && adapter.IsReady && HasDriver && service != null && service.IsRunning &&
                       PowerState == BluetoothPowerState.On;
            }
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
            report.AppendLine();

            string pnputil = PnpUtilPath();
            if (!File.Exists(pnputil))
            {
                report.AppendLine("O PnPUtil oficial do Windows não foi encontrado.");
                return new BluetoothRepairResult { ExitCode = 2, Output = report.ToString() };
            }

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
            Thread.Sleep(1400);

            BluetoothSnapshot after = BluetoothDoctor.Read();
            BluetoothAdapterInfo finalAdapter = after.PrimaryAdapter;
            report.AppendLine();
            report.AppendLine("RESULTADO");
            report.AppendLine(finalAdapter == null ? "Adaptador — não detectado" :
                "Adaptador — " + finalAdapter.Name + " — " + BluetoothDoctor.ExplainErrorCode(finalAdapter.ErrorCode));
            report.AppendLine("Serviço de suporte — " + ServiceState(after.SupportService));
            report.AppendLine("Chave do Bluetooth — " + PowerState(after.PowerState));
            if (after.IsHealthy)
            {
                report.AppendLine("Bluetooth pronto. Os pareamentos foram preservados.");
                return new BluetoothRepairResult { ExitCode = 0, Output = report.ToString() };
            }

            report.AppendLine(after.PowerState == BluetoothPowerState.Off
                ? "O adaptador voltou, mas a chave do Bluetooth continua desligada. Abra as configurações para ligá-la."
                : "O Bluetooth ainda precisa de atenção. Verifique as atualizações opcionais de driver do Windows.");
            return new BluetoothRepairResult { ExitCode = 2, Output = report.ToString() };
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
