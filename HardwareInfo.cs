using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;

namespace Neck
{
    internal sealed class HardwareComponent
    {
        public string Category;
        public string Name;
        public string Details;
    }

    internal sealed class TemperatureReading
    {
        public string Name;
        public double Celsius;
        public string Source;
    }

    internal sealed class HardwareSnapshot
    {
        public string ProcessorSummary = "Processador não identificado";
        public string GraphicsSummary = "Vídeo não identificado";
        public string MemorySummary = "Memória não identificada";
        public string StorageSummary = "Armazenamento não identificado";
        public DateTime CapturedUtc;
        public List<HardwareComponent> Components = new List<HardwareComponent>();
        public List<TemperatureReading> Temperatures = new List<TemperatureReading>();

        public string TemperatureSummary
        {
            get
            {
                if (Temperatures.Count == 0) return "Sensor não disponibilizado";
                TemperatureReading hottest = Temperatures.OrderByDescending(item => item.Celsius).First();
                return hottest.Name + " " + hottest.Celsius.ToString("0", CultureInfo.CurrentCulture) + " °C";
            }
        }
    }

    internal static class HardwareInfoProvider
    {
        private sealed class NvidiaGpuInfo
        {
            public string Name;
            public ulong MemoryBytes;
            public string DriverVersion;
        }

        public static HardwareSnapshot Read()
        {
            HardwareSnapshot snapshot = new HardwareSnapshot { CapturedUtc = DateTime.UtcNow };
            ReadProcessors(snapshot);
            ReadMemory(snapshot);
            ReadGraphics(snapshot);
            ReadStorage(snapshot);
            ReadBaseboard(snapshot);
            snapshot.Temperatures = ReadTemperatures();
            return snapshot;
        }

        public static List<TemperatureReading> ReadTemperatures()
        {
            List<TemperatureReading> readings = ReadMonitorSensors(@"root\LibreHardwareMonitor", "LibreHardwareMonitor");
            if (readings.Count == 0) readings = ReadMonitorSensors(@"root\OpenHardwareMonitor", "OpenHardwareMonitor");
            if (!readings.Any(item => item.Name.IndexOf("GPU", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      item.Name.IndexOf("GeForce", StringComparison.OrdinalIgnoreCase) >= 0))
                readings.AddRange(ReadNvidiaTemperatures());
            readings.AddRange(ReadAcpiTemperatures());
            return readings
                .Where(item => item.Celsius >= 5d && item.Celsius <= 125d)
                .GroupBy(item => item.Name + "|" + item.Source, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(item => item.Celsius).First())
                .OrderByDescending(item => item.Celsius)
                .ToList();
        }

        private static void ReadProcessors(HardwareSnapshot snapshot)
        {
            List<string> names = new List<string>();
            int cores = 0;
            int logical = 0;
            uint maxClock = 0;
            foreach (ManagementObject item in Query(@"root\CIMV2", "SELECT Name, Manufacturer, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor"))
            {
                using (item)
                {
                    string name = CleanName(Value(item, "Name"));
                    if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
                    cores += IntValue(item, "NumberOfCores");
                    logical += IntValue(item, "NumberOfLogicalProcessors");
                    maxClock = Math.Max(maxClock, UIntValue(item, "MaxClockSpeed"));
                }
            }
            string processor = names.Distinct(StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(processor)) processor = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Processador não identificado";
            string topology = cores > 0 ? cores + " núcleo(s) • " + Math.Max(cores, logical) + " thread(s)" : "Topologia não informada";
            string clock = maxClock > 0 ? " • frequência informada " + (maxClock / 1000d).ToString("0.00", CultureInfo.CurrentCulture) + " GHz" : string.Empty;
            snapshot.ProcessorSummary = processor + " • " + topology;
            snapshot.Components.Add(new HardwareComponent { Category = "CPU", Name = processor, Details = topology + clock });
        }

        private static void ReadMemory(HardwareSnapshot snapshot)
        {
            ulong total = 0;
            uint speed = 0;
            string type = string.Empty;
            List<string> modules = new List<string>();
            foreach (ManagementObject item in Query(@"root\CIMV2", "SELECT Capacity, Speed, ConfiguredClockSpeed, SMBIOSMemoryType, Manufacturer, PartNumber FROM Win32_PhysicalMemory"))
            {
                using (item)
                {
                    ulong capacity = ULongValue(item, "Capacity");
                    total += capacity;
                    uint configured = UIntValue(item, "ConfiguredClockSpeed");
                    speed = Math.Max(speed, configured > 0 ? configured : UIntValue(item, "Speed"));
                    if (string.IsNullOrWhiteSpace(type)) type = MemoryType(UIntValue(item, "SMBIOSMemoryType"));
                    string maker = CleanName(Value(item, "Manufacturer"));
                    string part = CleanName(Value(item, "PartNumber"));
                    modules.Add(MainForm.FormatBytes((long)capacity) +
                        (string.IsNullOrWhiteSpace(maker) ? string.Empty : " • " + maker) +
                        (string.IsNullOrWhiteSpace(part) ? string.Empty : " " + part));
                }
            }
            if (total == 0)
            {
                MemoryStatus memory = SystemInfo.GetMemoryStatus();
                double availableRatio = Math.Max(0.01d, 1d - memory.PercentUsed / 100d);
                total = (ulong)(memory.AvailableBytes / availableRatio);
            }
            string specification = MainForm.FormatBytes((long)total) +
                (string.IsNullOrWhiteSpace(type) ? string.Empty : " " + type) +
                (speed > 0 ? " • " + speed + " MT/s" : string.Empty);
            snapshot.MemorySummary = specification;
            snapshot.Components.Add(new HardwareComponent
            {
                Category = "RAM",
                Name = specification,
                Details = modules.Count == 0 ? "Módulos não informados pelo firmware" : string.Join(" | ", modules)
            });
        }

        private static void ReadGraphics(HardwareSnapshot snapshot)
        {
            List<HardwareComponent> graphics = new List<HardwareComponent>();
            List<NvidiaGpuInfo> nvidia = ReadNvidiaGpuInfo();
            foreach (ManagementObject item in Query(@"root\CIMV2", "SELECT Name, AdapterRAM, DriverVersion, VideoProcessor FROM Win32_VideoController"))
            {
                using (item)
                {
                    string name = CleanName(Value(item, "Name"));
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    ulong memory = ULongValue(item, "AdapterRAM");
                    string driver = Value(item, "DriverVersion");
                    NvidiaGpuInfo official = nvidia.FirstOrDefault(gpu =>
                        string.Equals(CleanName(gpu.Name), name, StringComparison.OrdinalIgnoreCase));
                    if (official != null)
                    {
                        memory = official.MemoryBytes;
                        driver = official.DriverVersion;
                    }
                    string details = memory > 0 ? MainForm.FormatBytes((long)memory) + " de memória informada" : "Memória compartilhada ou não informada";
                    if (!string.IsNullOrWhiteSpace(driver)) details += " • driver " + driver;
                    graphics.Add(new HardwareComponent { Category = "GPU", Name = name, Details = details });
                }
            }
            snapshot.Components.AddRange(graphics);
            snapshot.GraphicsSummary = graphics.Count == 0
                ? "Vídeo não identificado"
                : string.Join(" + ", graphics.OrderByDescending(item => GraphicsRank(item.Name)).Select(item => item.Name).Take(2));
        }

        private static void ReadStorage(HardwareSnapshot snapshot)
        {
            List<HardwareComponent> disks = new List<HardwareComponent>();
            foreach (ManagementObject item in Query(@"root\Microsoft\Windows\Storage", "SELECT FriendlyName, Size, MediaType, BusType FROM MSFT_PhysicalDisk"))
            {
                using (item)
                {
                    string model = CleanName(Value(item, "FriendlyName"));
                    if (string.IsNullOrWhiteSpace(model)) continue;
                    ulong size = ULongValue(item, "Size");
                    string media = StorageMediaType(UIntValue(item, "MediaType"));
                    string connection = StorageBusType(UIntValue(item, "BusType"));
                    string details = MainForm.FormatBytes((long)size) +
                        (string.IsNullOrWhiteSpace(media) ? string.Empty : " • " + media) +
                        (string.IsNullOrWhiteSpace(connection) ? string.Empty : " • " + connection);
                    disks.Add(new HardwareComponent { Category = "Disco", Name = model, Details = details });
                }
            }
            if (disks.Count == 0)
            {
                foreach (ManagementObject item in Query(@"root\CIMV2", "SELECT Model, Size, MediaType, InterfaceType FROM Win32_DiskDrive"))
                {
                    using (item)
                    {
                        string model = CleanName(Value(item, "Model"));
                        if (string.IsNullOrWhiteSpace(model)) continue;
                        ulong size = ULongValue(item, "Size");
                        string media = CleanName(Value(item, "MediaType"));
                        string connection = CleanName(Value(item, "InterfaceType"));
                        string details = MainForm.FormatBytes((long)size) +
                            (string.IsNullOrWhiteSpace(media) ? string.Empty : " • " + media) +
                            (string.IsNullOrWhiteSpace(connection) ? string.Empty : " • " + connection);
                        disks.Add(new HardwareComponent { Category = "Disco", Name = model, Details = details });
                    }
                }
            }
            snapshot.Components.AddRange(disks);
            HardwareComponent primary = disks.FirstOrDefault();
            snapshot.StorageSummary = primary == null ? "Armazenamento não identificado" : primary.Name + " • " + primary.Details.Split('•')[0].Trim();
        }

        private static void ReadBaseboard(HardwareSnapshot snapshot)
        {
            foreach (ManagementObject item in Query(@"root\CIMV2", "SELECT Manufacturer, Product, Version FROM Win32_BaseBoard"))
            {
                using (item)
                {
                    string maker = CleanName(Value(item, "Manufacturer"));
                    string product = CleanName(Value(item, "Product"));
                    string version = CleanName(Value(item, "Version"));
                    if (string.IsNullOrWhiteSpace(maker) && string.IsNullOrWhiteSpace(product)) continue;
                    snapshot.Components.Add(new HardwareComponent
                    {
                        Category = "Placa-mãe",
                        Name = (maker + " " + product).Trim(),
                        Details = string.IsNullOrWhiteSpace(version) ? "Versão não informada" : "Versão " + version
                    });
                    break;
                }
            }
        }

        private static List<TemperatureReading> ReadMonitorSensors(string scopePath, string source)
        {
            List<TemperatureReading> readings = new List<TemperatureReading>();
            foreach (ManagementObject item in Query(scopePath, "SELECT Name, Value, Parent FROM Sensor WHERE SensorType = 'Temperature'"))
            {
                using (item)
                {
                    double value = DoubleValue(item, "Value");
                    string name = CleanName(Value(item, "Name"));
                    if (string.IsNullOrWhiteSpace(name)) name = "Sensor de temperatura";
                    readings.Add(new TemperatureReading { Name = name, Celsius = value, Source = source });
                }
            }
            return readings;
        }

        private static List<TemperatureReading> ReadAcpiTemperatures()
        {
            List<TemperatureReading> readings = new List<TemperatureReading>();
            foreach (ManagementObject item in Query(@"root\WMI", "SELECT CurrentTemperature, InstanceName FROM MSAcpi_ThermalZoneTemperature"))
            {
                using (item)
                {
                    double raw = DoubleValue(item, "CurrentTemperature");
                    double celsius = raw / 10d - 273.15d;
                    if (celsius < 5d || celsius > 125d) continue;
                    readings.Add(new TemperatureReading
                    {
                        Name = "Zona térmica do sistema",
                        Celsius = celsius,
                        Source = "Windows/ACPI — pode não representar CPU ou GPU"
                    });
                }
            }
            return readings;
        }

        private static List<TemperatureReading> ReadNvidiaTemperatures()
        {
            List<TemperatureReading> readings = new List<TemperatureReading>();
            string executable = FindNvidiaSmi();
            if (string.IsNullOrWhiteSpace(executable)) return readings;
            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = executable,
                        Arguments = "--query-gpu=name,temperature.gpu --format=csv,noheader,nounits",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    };
                    process.Start();
                    if (!process.WaitForExit(2000))
                    {
                        try { process.Kill(); }
                        catch { }
                        return readings;
                    }
                    string output = process.StandardOutput.ReadToEnd();
                    foreach (string rawLine in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        int separator = rawLine.LastIndexOf(',');
                        if (separator <= 0) continue;
                        string name = CleanName(rawLine.Substring(0, separator));
                        double celsius;
                        if (!double.TryParse(rawLine.Substring(separator + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out celsius)) continue;
                        readings.Add(new TemperatureReading
                        {
                            Name = string.IsNullOrWhiteSpace(name) ? "GPU NVIDIA" : name,
                            Celsius = celsius,
                            Source = "Driver NVIDIA (nvidia-smi)"
                        });
                    }
                }
            }
            catch { }
            return readings;
        }

        private static List<NvidiaGpuInfo> ReadNvidiaGpuInfo()
        {
            List<NvidiaGpuInfo> result = new List<NvidiaGpuInfo>();
            string executable = FindNvidiaSmi();
            if (string.IsNullOrWhiteSpace(executable)) return result;
            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = executable,
                        Arguments = "--query-gpu=name,memory.total,driver_version --format=csv,noheader,nounits",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    };
                    process.Start();
                    if (!process.WaitForExit(2000))
                    {
                        try { process.Kill(); }
                        catch { }
                        return result;
                    }
                    string output = process.StandardOutput.ReadToEnd();
                    foreach (string rawLine in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string[] parts = rawLine.Split(',');
                        if (parts.Length < 3) continue;
                        ulong memoryMiB;
                        if (!ulong.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out memoryMiB)) memoryMiB = 0;
                        result.Add(new NvidiaGpuInfo
                        {
                            Name = CleanName(parts[0]),
                            MemoryBytes = memoryMiB * 1024UL * 1024UL,
                            DriverVersion = parts[2].Trim()
                        });
                    }
                }
            }
            catch { }
            return result;
        }

        private static string FindNvidiaSmi()
        {
            string system = Path.Combine(Environment.SystemDirectory, "nvidia-smi.exe");
            if (File.Exists(system)) return system;
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string alternate = Path.Combine(programFiles, "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe");
            return File.Exists(alternate) ? alternate : string.Empty;
        }

        private static List<ManagementObject> Query(string scopePath, string query)
        {
            List<ManagementObject> result = new List<ManagementObject>();
            try
            {
                ManagementScope scope = new ManagementScope(@"\\.\" + scopePath);
                scope.Connect();
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query)))
                using (ManagementObjectCollection collection = searcher.Get())
                {
                    foreach (ManagementObject item in collection) result.Add(item);
                }
            }
            catch
            {
                foreach (ManagementObject item in result) item.Dispose();
                result.Clear();
            }
            return result;
        }

        private static string Value(ManagementBaseObject item, string property)
        {
            try { return Convert.ToString(item[property], CultureInfo.InvariantCulture) ?? string.Empty; }
            catch { return string.Empty; }
        }

        private static int IntValue(ManagementBaseObject item, string property)
        {
            try { return Convert.ToInt32(item[property], CultureInfo.InvariantCulture); }
            catch { return 0; }
        }

        private static uint UIntValue(ManagementBaseObject item, string property)
        {
            try { return Convert.ToUInt32(item[property], CultureInfo.InvariantCulture); }
            catch { return 0; }
        }

        private static ulong ULongValue(ManagementBaseObject item, string property)
        {
            try { return Convert.ToUInt64(item[property], CultureInfo.InvariantCulture); }
            catch { return 0; }
        }

        private static double DoubleValue(ManagementBaseObject item, string property)
        {
            try { return Convert.ToDouble(item[property], CultureInfo.InvariantCulture); }
            catch { return 0d; }
        }

        private static string CleanName(string value)
        {
            return (value ?? string.Empty).Replace("(R)", string.Empty).Replace("(TM)", string.Empty).Trim();
        }

        private static string MemoryType(uint code)
        {
            switch (code)
            {
                case 24: return "DDR3";
                case 26: return "DDR4";
                case 30: return "LPDDR4";
                case 34: return "DDR5";
                case 35: return "LPDDR5";
                default: return string.Empty;
            }
        }

        private static string StorageMediaType(uint code)
        {
            switch (code)
            {
                case 3: return "HDD";
                case 4: return "SSD";
                case 5: return "Memória persistente";
                default: return string.Empty;
            }
        }

        private static string StorageBusType(uint code)
        {
            switch (code)
            {
                case 7: return "USB";
                case 8: return "RAID";
                case 10: return "SAS";
                case 11: return "SATA";
                case 14: return "Virtual";
                case 17: return "NVMe";
                case 18: return "SCM";
                default: return string.Empty;
            }
        }

        private static int GraphicsRank(string name)
        {
            string value = name ?? string.Empty;
            if (value.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("GeForce", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Radeon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf(" AMD ", StringComparison.OrdinalIgnoreCase) >= 0) return 2;
            if (value.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0) return 1;
            return 0;
        }
    }
}
