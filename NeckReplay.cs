using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;

namespace Neck
{
    internal enum ReplayCause
    {
        None,
        MemoryPressure,
        CpuContention,
        DiskStall,
        ForegroundFreeze,
        ThermalPressure
    }

    internal enum ReplayActionKind { None, Accelerate, Diagnostic, Hardware }

    internal sealed class ReplaySample
    {
        public DateTime TimestampUtc;
        public double MemoryPercent;
        public long AvailableBytes;
        public double CommitPercent;
        public double PageReadsPerSecond;
        public double CpuPercent;
        public double ProcessorQueueLength;
        public double DiskActivePercent;
        public double DiskLatencyMilliseconds;
        public double DiskQueueLength;
        public double TemperatureCelsius;
        public string TopMemoryProcess = "";
        public long TopMemoryBytes;
        public string TopCpuProcess = "";
        public double TopCpuPercent;
        public string ForegroundProcess = "";
        public bool ForegroundResponsive = true;
    }

    internal sealed class ReplayCapture
    {
        public HealthSnapshot Health;
        public ReplaySample Sample;
    }

    internal sealed class ReplayIncident
    {
        public ReplayCause Cause;
        public DateTime StartedUtc;
        public DateTime PeakUtc;
        public DateTime EndedUtc;
        public bool Ongoing;
        public int Confidence;
        public int SampleCount;
        public string Title = "Gargalo registrado";
        public string Explanation = "O Neck registrou uma pressão persistente.";
        public string Evidence = "";
        public string ProcessName = "";
        public string DisplayName = "";
        public ReplayActionKind ActionKind;
        public string ActionText = "Entender o gargalo";
        public double PeakMemoryPercent;
        public long LowestAvailableBytes;
        public double PeakPageReadsPerSecond;
        public double PeakCpuPercent;
        public double PeakProcessorQueue;
        public double PeakDiskActivePercent;
        public double PeakDiskLatencyMilliseconds;
        public double PeakDiskQueue;
        public double PeakTemperatureCelsius;

        public TimeSpan Duration
        {
            get
            {
                DateTime end = Ongoing || EndedUtc == DateTime.MinValue ? DateTime.UtcNow : EndedUtc;
                return end > StartedUtc ? end - StartedUtc : TimeSpan.Zero;
            }
        }

        public string ShortSummary
        {
            get
            {
                string state = Ongoing ? "ainda está acontecendo" : "já terminou";
                return Title + " — " + state + ".";
            }
        }

        public ReplayIncident Clone()
        {
            return (ReplayIncident)MemberwiseClone();
        }
    }

    internal sealed class ReplayDecision
    {
        public bool IncidentConfirmed;
        public bool RecoveryConfirmed;
        public ReplayIncident Incident;
    }

    internal sealed class ReplayAssessment
    {
        public ReplayCause Cause;
        public int Score;
        public string ProcessName = "";
        public string DisplayName = "";
    }

    internal static class ReplayClassifier
    {
        public static ReplayAssessment Analyze(ReplaySample sample)
        {
            ReplayAssessment best = new ReplayAssessment();
            if (sample == null) return best;

            if (!sample.ForegroundResponsive && !string.IsNullOrWhiteSpace(sample.ForegroundProcess))
            {
                best.Cause = ReplayCause.ForegroundFreeze;
                best.Score = 100;
                best.ProcessName = sample.ForegroundProcess;
                best.DisplayName = SystemInfo.FriendlyProcessName(sample.ForegroundProcess);
                return best;
            }

            int memory = 0;
            if (sample.MemoryPercent >= 90) memory += 62;
            else if (sample.MemoryPercent >= 85) memory += 35;
            if (sample.AvailableBytes > 0 && sample.AvailableBytes < 512L * 1024 * 1024) memory += 45;
            else if (sample.AvailableBytes > 0 && sample.AvailableBytes < 1024L * 1024 * 1024) memory += 30;
            if (sample.CommitPercent >= 95) memory += 45;
            else if (sample.CommitPercent >= 90) memory += 25;
            if (sample.PageReadsPerSecond >= 50 &&
                (sample.MemoryPercent >= 85 || (sample.AvailableBytes > 0 && sample.AvailableBytes < 2L * 1024 * 1024 * 1024)))
                memory += Math.Min(40, 20 + (int)(sample.PageReadsPerSecond / 25));
            Consider(best, ReplayCause.MemoryPressure, memory, sample.TopMemoryProcess,
                Friendly(sample.TopMemoryProcess));

            int cpu = 0;
            if (sample.CpuPercent >= 92) cpu += 70;
            else if (sample.CpuPercent >= 85) cpu += 55;
            else if (sample.CpuPercent >= 75) cpu += 25;
            double queueThreshold = Math.Max(2, Environment.ProcessorCount * 0.5d);
            if (sample.ProcessorQueueLength >= queueThreshold) cpu += 40;
            else if (sample.ProcessorQueueLength >= 2) cpu += 18;
            Consider(best, ReplayCause.CpuContention, cpu, sample.TopCpuProcess,
                Friendly(sample.TopCpuProcess));

            int disk = 0;
            if (sample.DiskActivePercent >= 95) disk += 45;
            else if (sample.DiskActivePercent >= 80) disk += 30;
            if (sample.DiskLatencyMilliseconds >= 100) disk += 60;
            else if (sample.DiskLatencyMilliseconds >= 50) disk += 45;
            else if (sample.DiskLatencyMilliseconds >= 25) disk += 28;
            if (sample.DiskQueueLength >= 4) disk += 40;
            else if (sample.DiskQueueLength >= 2) disk += 25;
            Consider(best, ReplayCause.DiskStall, disk, sample.TopCpuProcess,
                Friendly(sample.TopCpuProcess));

            int thermal = sample.TemperatureCelsius >= 95 ? 90 : sample.TemperatureCelsius >= 90 ? 70 : 0;
            Consider(best, ReplayCause.ThermalPressure, thermal, "", "");

            if (best.Score < 55) return new ReplayAssessment();
            best.Score = Math.Min(100, best.Score);
            return best;
        }

        private static void Consider(ReplayAssessment best, ReplayCause cause, int score, string processName, string displayName)
        {
            if (score <= best.Score) return;
            best.Cause = cause;
            best.Score = score;
            best.ProcessName = processName ?? "";
            best.DisplayName = string.IsNullOrWhiteSpace(displayName) ? "" : displayName;
        }

        private static string Friendly(string processName)
        {
            return string.IsNullOrWhiteSpace(processName) ? "" : SystemInfo.FriendlyProcessName(processName);
        }
    }

    internal sealed class ReplayEngine
    {
        private readonly object _syncRoot = new object();
        private readonly List<ReplaySample> _samples = new List<ReplaySample>();
        private int _pressureStreak;
        private int _stableStreak;
        private DateTime _candidateStartedUtc;
        private ReplayIncident _active;
        private ReplayIncident _latest;

        public ReplayDecision Record(ReplaySample sample)
        {
            ReplayDecision decision = new ReplayDecision();
            if (sample == null) return decision;
            lock (_syncRoot)
            {
                if (sample.TimestampUtc == DateTime.MinValue) sample.TimestampUtc = DateTime.UtcNow;
                _samples.Add(sample);
                DateTime cutoff = sample.TimestampUtc.AddMinutes(-5);
                _samples.RemoveAll(item => item.TimestampUtc < cutoff);
                if (_samples.Count > 90) _samples.RemoveRange(0, _samples.Count - 90);

                ReplayAssessment assessment = ReplayClassifier.Analyze(sample);
                bool pressured = assessment.Cause != ReplayCause.None;
                if (_active == null)
                {
                    if (pressured)
                    {
                        if (_pressureStreak == 0) _candidateStartedUtc = sample.TimestampUtc;
                        _pressureStreak++;
                    }
                    else
                    {
                        _pressureStreak = 0;
                        _candidateStartedUtc = DateTime.MinValue;
                    }

                    int needed = assessment.Score >= 90 ? 2 : 3;
                    if (pressured && _pressureStreak >= needed)
                    {
                        _active = BuildIncident(_samples.Where(item => item.TimestampUtc >= _candidateStartedUtc).ToList(), true);
                        _latest = _active;
                        _stableStreak = 0;
                        decision.IncidentConfirmed = true;
                    }
                }
                else
                {
                    if (pressured) _stableStreak = 0;
                    else _stableStreak++;
                    DateTime activeStart = _active.StartedUtc;
                    ReplayIncident updated = BuildIncident(_samples.Where(item => item.TimestampUtc >= activeStart).ToList(), true);
                    updated.StartedUtc = activeStart;
                    _active = updated;
                    _latest = _active;
                    if (_stableStreak >= 2)
                    {
                        _active.Ongoing = false;
                        _active.EndedUtc = sample.TimestampUtc;
                        _latest = _active;
                        decision.RecoveryConfirmed = true;
                        _active = null;
                        _pressureStreak = 0;
                        _stableStreak = 0;
                    }
                }
                decision.Incident = _latest == null ? null : _latest.Clone();
                return decision;
            }
        }

        public List<ReplaySample> GetSamples()
        {
            lock (_syncRoot) return _samples.ToList();
        }

        public ReplayIncident GetLatestIncident()
        {
            lock (_syncRoot) return _latest == null ? null : _latest.Clone();
        }

        private static ReplayIncident BuildIncident(IList<ReplaySample> samples, bool ongoing)
        {
            ReplayIncident incident = new ReplayIncident { Ongoing = ongoing };
            if (samples == null || samples.Count == 0) return incident;
            incident.StartedUtc = samples.Min(item => item.TimestampUtc);
            incident.SampleCount = samples.Count;
            incident.PeakMemoryPercent = samples.Max(item => item.MemoryPercent);
            incident.LowestAvailableBytes = samples.Where(item => item.AvailableBytes > 0).Select(item => item.AvailableBytes).DefaultIfEmpty(0).Min();
            incident.PeakPageReadsPerSecond = samples.Max(item => item.PageReadsPerSecond);
            incident.PeakCpuPercent = samples.Max(item => item.CpuPercent);
            incident.PeakProcessorQueue = samples.Max(item => item.ProcessorQueueLength);
            incident.PeakDiskActivePercent = samples.Max(item => item.DiskActivePercent);
            incident.PeakDiskLatencyMilliseconds = samples.Max(item => item.DiskLatencyMilliseconds);
            incident.PeakDiskQueue = samples.Max(item => item.DiskQueueLength);
            incident.PeakTemperatureCelsius = samples.Max(item => item.TemperatureCelsius);

            var assessments = samples.Select(item => new { Sample = item, Assessment = ReplayClassifier.Analyze(item) })
                .Where(item => item.Assessment.Cause != ReplayCause.None).ToList();
            var dominant = assessments.GroupBy(item => item.Assessment.Cause)
                .Select(group => new
                {
                    Cause = group.Key,
                    Strength = group.Max(item => item.Assessment.Score) + group.Count() * 6,
                    Peak = group.OrderByDescending(item => item.Assessment.Score).First()
                })
                .OrderByDescending(item => item.Strength).FirstOrDefault();
            if (dominant == null) return incident;
            incident.Cause = dominant.Cause;
            incident.Confidence = Math.Min(99, dominant.Strength);
            incident.PeakUtc = dominant.Peak.Sample.TimestampUtc;
            incident.ProcessName = dominant.Peak.Assessment.ProcessName;
            incident.DisplayName = dominant.Peak.Assessment.DisplayName;
            Explain(incident);
            return incident;
        }

        private static void Explain(ReplayIncident incident)
        {
            string culprit = string.IsNullOrWhiteSpace(incident.DisplayName) ? "" : " " + incident.DisplayName + " foi o aplicativo mais associado ao pico.";
            if (incident.Cause == ReplayCause.MemoryPressure)
            {
                incident.Title = "A memória virou um gargalo real";
                incident.Explanation = "A RAM perdeu folga e o Windows precisou buscar páginas no armazenamento." + culprit;
                incident.Evidence = "RAM " + incident.PeakMemoryPercent.ToString("0", CultureInfo.CurrentCulture) + "%  •  menor folga " + MainForm.FormatBytes(incident.LowestAvailableBytes) + "  •  paginação " + incident.PeakPageReadsPerSecond.ToString("0", CultureInfo.CurrentCulture) + "/s";
                incident.ActionKind = ReplayActionKind.Accelerate;
                incident.ActionText = "Aliviar disputa agora";
            }
            else if (incident.Cause == ReplayCause.CpuContention)
            {
                incident.Title = "Muitos trabalhos disputaram a CPU";
                incident.Explanation = "A CPU permaneceu ocupada e houve fila para executar novos trabalhos." + culprit;
                incident.Evidence = "CPU " + incident.PeakCpuPercent.ToString("0", CultureInfo.CurrentCulture) + "%  •  fila " + incident.PeakProcessorQueue.ToString("0.0", CultureInfo.CurrentCulture) + "  •  amostras " + incident.SampleCount;
                incident.ActionKind = ReplayActionKind.Accelerate;
                incident.ActionText = "Priorizar aplicativo importante";
            }
            else if (incident.Cause == ReplayCause.DiskStall)
            {
                incident.Title = "O armazenamento demorou para responder";
                incident.Explanation = "As operações ficaram acumuladas e o tempo de resposta do disco subiu além do normal.";
                incident.Evidence = "Atividade " + incident.PeakDiskActivePercent.ToString("0", CultureInfo.CurrentCulture) + "%  •  latência " + incident.PeakDiskLatencyMilliseconds.ToString("0", CultureInfo.CurrentCulture) + " ms  •  fila " + incident.PeakDiskQueue.ToString("0.0", CultureInfo.CurrentCulture);
                incident.ActionKind = ReplayActionKind.Diagnostic;
                incident.ActionText = "Abrir diagnóstico";
            }
            else if (incident.Cause == ReplayCause.ForegroundFreeze)
            {
                incident.Title = string.IsNullOrWhiteSpace(incident.DisplayName) ? "O aplicativo em uso parou de responder" : incident.DisplayName + " parou de responder";
                incident.Explanation = "O Replay confirmou falta de resposta da janela em primeiro plano e preservou o contexto dos recursos naquele momento.";
                incident.Evidence = "CPU " + incident.PeakCpuPercent.ToString("0", CultureInfo.CurrentCulture) + "%  •  RAM " + incident.PeakMemoryPercent.ToString("0", CultureInfo.CurrentCulture) + "%  •  pico às " + incident.PeakUtc.ToLocalTime().ToString("HH:mm:ss");
                incident.ActionKind = ReplayActionKind.Accelerate;
                incident.ActionText = "Aliviar concorrentes agora";
            }
            else if (incident.Cause == ReplayCause.ThermalPressure)
            {
                incident.Title = "A temperatura pode estar limitando o desempenho";
                incident.Explanation = "Um sensor confiável permaneceu em uma faixa em que o hardware pode reduzir frequência para se proteger.";
                incident.Evidence = "Maior temperatura observada: " + incident.PeakTemperatureCelsius.ToString("0", CultureInfo.CurrentCulture) + " °C";
                incident.ActionKind = ReplayActionKind.Hardware;
                incident.ActionText = "Ver temperaturas";
            }
        }
    }

    internal sealed class ReplayProbe : IDisposable
    {
        private readonly ReplayPerformanceSampler _performance = new ReplayPerformanceSampler();
        private readonly ReplayProcessSampler _processes = new ReplayProcessSampler();

        public ReplayCapture Capture(double temperatureCelsius)
        {
            HealthSnapshot health = SystemInfo.GetHealthSnapshot();
            ReplayPerformanceValues performance = _performance.Capture();
            ReplayProcessLoad topCpu = _processes.Sample();
            ResourceProcess topMemory = health.TopProcesses.FirstOrDefault();
            string foreground;
            bool responsive;
            ReadForeground(out foreground, out responsive);
            ReplaySample sample = new ReplaySample
            {
                TimestampUtc = DateTime.UtcNow,
                MemoryPercent = health.Memory.PercentUsed,
                AvailableBytes = (long)health.Memory.AvailableBytes,
                CommitPercent = performance.CommitPercent,
                PageReadsPerSecond = performance.PageReadsPerSecond,
                CpuPercent = health.CpuPercent,
                ProcessorQueueLength = performance.ProcessorQueueLength,
                DiskActivePercent = performance.DiskActivePercent,
                DiskLatencyMilliseconds = performance.DiskLatencyMilliseconds,
                DiskQueueLength = performance.DiskQueueLength,
                TemperatureCelsius = temperatureCelsius,
                TopMemoryProcess = topMemory == null ? "" : topMemory.ProcessName,
                TopMemoryBytes = topMemory == null ? 0 : topMemory.MemoryBytes,
                TopCpuProcess = topCpu == null ? "" : topCpu.ProcessName,
                TopCpuPercent = topCpu == null ? 0 : topCpu.CpuPercent,
                ForegroundProcess = foreground,
                ForegroundResponsive = responsive
            };
            FlowHealthRefiner.Apply(health, sample);
            return new ReplayCapture { Health = health, Sample = sample };
        }

        public void Dispose()
        {
            _performance.Dispose();
        }

        private static void ReadForeground(out string processName, out bool responsive)
        {
            processName = "";
            responsive = true;
            try
            {
                IntPtr window = NativeMethods.GetForegroundWindow();
                uint processId;
                if (window == IntPtr.Zero || ReplayNativeMethods.GetWindowThreadProcessId(window, out processId) == 0 || processId == 0) return;
                using (Process process = Process.GetProcessById((int)processId))
                {
                    processName = process.ProcessName;
                    responsive = process.Responding;
                }
            }
            catch { responsive = true; }
        }
    }

    internal sealed class ReplayProcessLoad
    {
        public string ProcessName;
        public double CpuPercent;
    }

    internal sealed class ReplayProcessSampler
    {
        private sealed class Point
        {
            public string Name;
            public long CpuTicks;
        }

        private Dictionary<int, Point> _previous = new Dictionary<int, Point>();
        private DateTime _previousUtc = DateTime.MinValue;

        public ReplayProcessLoad Sample()
        {
            DateTime now = DateTime.UtcNow;
            Dictionary<int, Point> current = new Dictionary<int, Point>();
            Dictionary<string, double> usage = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            int ownId = Process.GetCurrentProcess().Id;
            foreach (Process process in Process.GetProcesses())
            {
                using (process)
                {
                    try
                    {
                        if (process.Id == ownId) continue;
                        Point point = new Point { Name = process.ProcessName, CpuTicks = process.TotalProcessorTime.Ticks };
                        current[process.Id] = point;
                        Point old;
                        if (_previousUtc == DateTime.MinValue || !_previous.TryGetValue(process.Id, out old) ||
                            !string.Equals(old.Name, point.Name, StringComparison.OrdinalIgnoreCase)) continue;
                        long delta = Math.Max(0, point.CpuTicks - old.CpuTicks);
                        double value;
                        usage.TryGetValue(point.Name, out value);
                        usage[point.Name] = value + delta;
                    }
                    catch { }
                }
            }
            double elapsedTicks = _previousUtc == DateTime.MinValue ? 0 : Math.Max(1, (now - _previousUtc).Ticks);
            _previous = current;
            _previousUtc = now;
            if (elapsedTicks <= 0 || usage.Count == 0) return null;
            var top = usage.OrderByDescending(item => item.Value).First();
            return new ReplayProcessLoad
            {
                ProcessName = top.Key,
                CpuPercent = Math.Max(0, Math.Min(100, top.Value * 100d / elapsedTicks / Math.Max(1, Environment.ProcessorCount)))
            };
        }
    }

    internal sealed class ReplayPerformanceValues
    {
        public double CommitPercent;
        public double PageReadsPerSecond;
        public double ProcessorQueueLength;
        public double DiskActivePercent;
        public double DiskLatencyMilliseconds;
        public double DiskQueueLength;
    }

    internal sealed class ReplayPerformanceSampler : IDisposable
    {
        private const uint ErrorSuccess = 0;
        private const uint PdhFormatDouble = 0x00000200;
        private readonly object _syncRoot = new object();
        private readonly Dictionary<string, IntPtr> _counters = new Dictionary<string, IntPtr>();
        private IntPtr _query;

        public ReplayPerformanceSampler()
        {
            if (ReplayNativeMethods.PdhOpenQuery(null, UIntPtr.Zero, out _query) != ErrorSuccess) return;
            Add("commit", @"\Memory\% Committed Bytes In Use");
            Add("pageReads", @"\Memory\Page Reads/sec");
            Add("cpuQueue", @"\System\Processor Queue Length");
            Add("diskActive", @"\PhysicalDisk(_Total)\% Disk Time");
            Add("diskLatency", @"\PhysicalDisk(_Total)\Avg. Disk sec/Transfer");
            Add("diskQueue", @"\PhysicalDisk(_Total)\Current Disk Queue Length");
            ReplayNativeMethods.PdhCollectQueryData(_query);
        }

        public ReplayPerformanceValues Capture()
        {
            lock (_syncRoot)
            {
                ReplayPerformanceValues values = new ReplayPerformanceValues();
                if (_query == IntPtr.Zero || ReplayNativeMethods.PdhCollectQueryData(_query) != ErrorSuccess) return values;
                values.CommitPercent = Read("commit");
                values.PageReadsPerSecond = Read("pageReads");
                values.ProcessorQueueLength = Read("cpuQueue");
                values.DiskActivePercent = Math.Max(0, Math.Min(100, Read("diskActive")));
                values.DiskLatencyMilliseconds = Math.Max(0, Read("diskLatency") * 1000d);
                values.DiskQueueLength = Math.Max(0, Read("diskQueue"));
                return values;
            }
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_query == IntPtr.Zero) return;
                ReplayNativeMethods.PdhCloseQuery(_query);
                _query = IntPtr.Zero;
                _counters.Clear();
            }
        }

        private void Add(string key, string path)
        {
            if (_query == IntPtr.Zero) return;
            IntPtr counter;
            if (ReplayNativeMethods.PdhAddEnglishCounter(_query, path, UIntPtr.Zero, out counter) == ErrorSuccess)
                _counters[key] = counter;
        }

        private double Read(string key)
        {
            IntPtr counter;
            if (!_counters.TryGetValue(key, out counter)) return 0;
            uint type;
            ReplayNativeMethods.PdhFormattedCounterValue value;
            uint result = ReplayNativeMethods.PdhGetFormattedCounterValue(counter, PdhFormatDouble, out type, out value);
            if (result != ErrorSuccess || (value.Status != 0 && value.Status != 1) || double.IsNaN(value.DoubleValue) || double.IsInfinity(value.DoubleValue)) return 0;
            return value.DoubleValue;
        }
    }

    internal static class ReplayNativeMethods
    {
        [StructLayout(LayoutKind.Explicit)]
        internal struct PdhFormattedCounterValue
        {
            [FieldOffset(0)] public uint Status;
            [FieldOffset(8)] public double DoubleValue;
        }

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        internal static extern uint PdhOpenQuery(string dataSource, UIntPtr userData, out IntPtr query);

        [DllImport("pdh.dll", CharSet = CharSet.Unicode, EntryPoint = "PdhAddEnglishCounterW")]
        internal static extern uint PdhAddEnglishCounter(IntPtr query, string path, UIntPtr userData, out IntPtr counter);

        [DllImport("pdh.dll")]
        internal static extern uint PdhCollectQueryData(IntPtr query);

        [DllImport("pdh.dll")]
        internal static extern uint PdhGetFormattedCounterValue(IntPtr counter, uint format, out uint type, out PdhFormattedCounterValue value);

        [DllImport("pdh.dll")]
        internal static extern uint PdhCloseQuery(IntPtr query);

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    }
}
