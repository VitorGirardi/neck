using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Neck
{
    internal enum BaselineState { Learning, Personalized }

    internal sealed class BaselineMetric
    {
        public long Count;
        public double Mean;
        public double M2;
        public double Minimum = double.MaxValue;
        public double Maximum = double.MinValue;

        public double StandardDeviation
        {
            get { return Count > 1 ? Math.Sqrt(Math.Max(0, M2 / (Count - 1))) : 0; }
        }

        public void Add(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return;
            if (Count >= 10000)
            {
                Count = Math.Max(1, Count / 2);
                M2 /= 2d;
            }
            Count++;
            double delta = value - Mean;
            Mean += delta / Count;
            M2 += delta * (value - Mean);
            Minimum = Math.Min(Minimum, value);
            Maximum = Math.Max(Maximum, value);
        }

        public BaselineMetric Clone()
        {
            return (BaselineMetric)MemberwiseClone();
        }

        public string Serialize()
        {
            if (Count == 0) return "0,0,0,0,0";
            return string.Join(",", new[]
            {
                Count.ToString(CultureInfo.InvariantCulture),
                Mean.ToString("R", CultureInfo.InvariantCulture),
                M2.ToString("R", CultureInfo.InvariantCulture),
                Minimum.ToString("R", CultureInfo.InvariantCulture),
                Maximum.ToString("R", CultureInfo.InvariantCulture)
            });
        }

        public static BaselineMetric Parse(string value)
        {
            try
            {
                string[] parts = (value ?? "").Split(',');
                if (parts.Length != 5) return new BaselineMetric();
                BaselineMetric metric = new BaselineMetric
                {
                    Count = long.Parse(parts[0], CultureInfo.InvariantCulture),
                    Mean = double.Parse(parts[1], CultureInfo.InvariantCulture),
                    M2 = double.Parse(parts[2], CultureInfo.InvariantCulture),
                    Minimum = double.Parse(parts[3], CultureInfo.InvariantCulture),
                    Maximum = double.Parse(parts[4], CultureInfo.InvariantCulture)
                };
                if (metric.Count < 0 || metric.Count > 10000 || metric.M2 < 0 || double.IsNaN(metric.Mean) || double.IsInfinity(metric.Mean))
                    return new BaselineMetric();
                return metric;
            }
            catch { return new BaselineMetric(); }
        }
    }

    internal sealed class BaselineContextProfile
    {
        public BaselineMetric MemoryPercent = new BaselineMetric();
        public BaselineMetric AvailableMegabytes = new BaselineMetric();
        public BaselineMetric CommitPercent = new BaselineMetric();
        public BaselineMetric PageReadsPerSecond = new BaselineMetric();
        public BaselineMetric CpuPercent = new BaselineMetric();
        public BaselineMetric ProcessorQueueLength = new BaselineMetric();
        public BaselineMetric DiskActivePercent = new BaselineMetric();
        public BaselineMetric DiskLatencyMilliseconds = new BaselineMetric();
        public BaselineMetric DiskQueueLength = new BaselineMetric();
        public BaselineMetric TemperatureCelsius = new BaselineMetric();

        public long SampleCount { get { return MemoryPercent.Count; } }
        public bool IsReady { get { return SampleCount >= BaselineEngine.RequiredSamples; } }

        public void Add(ReplaySample sample)
        {
            MemoryPercent.Add(sample.MemoryPercent);
            AvailableMegabytes.Add(sample.AvailableBytes / 1024d / 1024d);
            CommitPercent.Add(sample.CommitPercent);
            PageReadsPerSecond.Add(sample.PageReadsPerSecond);
            CpuPercent.Add(sample.CpuPercent);
            ProcessorQueueLength.Add(sample.ProcessorQueueLength);
            DiskActivePercent.Add(sample.DiskActivePercent);
            DiskLatencyMilliseconds.Add(sample.DiskLatencyMilliseconds);
            DiskQueueLength.Add(sample.DiskQueueLength);
            if (sample.TemperatureCelsius > 0) TemperatureCelsius.Add(sample.TemperatureCelsius);
        }

        public BaselineContextProfile Clone()
        {
            return new BaselineContextProfile
            {
                MemoryPercent = MemoryPercent.Clone(),
                AvailableMegabytes = AvailableMegabytes.Clone(),
                CommitPercent = CommitPercent.Clone(),
                PageReadsPerSecond = PageReadsPerSecond.Clone(),
                CpuPercent = CpuPercent.Clone(),
                ProcessorQueueLength = ProcessorQueueLength.Clone(),
                DiskActivePercent = DiskActivePercent.Clone(),
                DiskLatencyMilliseconds = DiskLatencyMilliseconds.Clone(),
                DiskQueueLength = DiskQueueLength.Clone(),
                TemperatureCelsius = TemperatureCelsius.Clone()
            };
        }
    }

    internal sealed class BaselineProfile
    {
        public DateTime FirstSampleUtc = DateTime.MinValue;
        public DateTime LastSampleUtc = DateTime.MinValue;
        public BaselineContextProfile Normal = new BaselineContextProfile();
        public BaselineContextProfile Meeting = new BaselineContextProfile();

        public BaselineProfile Clone()
        {
            return new BaselineProfile
            {
                FirstSampleUtc = FirstSampleUtc,
                LastSampleUtc = LastSampleUtc,
                Normal = Normal.Clone(),
                Meeting = Meeting.Clone()
            };
        }
    }

    internal sealed class BaselineEvaluation
    {
        public BaselineState State;
        public int Score = 100;
        public string Title = "Aprendendo o comportamento deste computador";
        public string Explanation = "O Neck está formando um padrão local antes de comparar o desempenho.";
        public string PrimaryMetric = "Aprendizado";
        public long SamplesCollected;
        public int SamplesRequired = BaselineEngine.RequiredSamples;
        public bool UsedMeetingProfile;
        public bool SampleAccepted;

        public int LearningPercent
        {
            get { return (int)Math.Max(0, Math.Min(100, SamplesCollected * 100 / Math.Max(1, SamplesRequired))); }
        }

        public BaselineEvaluation Clone()
        {
            return (BaselineEvaluation)MemberwiseClone();
        }
    }

    internal sealed class BaselineView
    {
        public BaselineProfile Profile;
        public BaselineEvaluation Evaluation;
    }

    internal sealed class BaselineDeviation
    {
        public string Name;
        public int Penalty;
        public string Explanation;
    }

    internal sealed class BaselineEngine : IDisposable
    {
        public const int RequiredSamples = 30;
        private readonly object _syncRoot = new object();
        private readonly string _path;
        private BaselineProfile _profile;
        private BaselineEvaluation _current = new BaselineEvaluation();
        private int _acceptedSinceSave;

        public BaselineEngine(string path = null)
        {
            _path = string.IsNullOrWhiteSpace(path)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Neck", "baseline-v1.txt")
                : path;
            _profile = Load(_path);
        }

        public BaselineEvaluation Observe(ReplaySample sample, bool meetingActive)
        {
            if (sample == null) return _current.Clone();
            lock (_syncRoot)
            {
                BaselineContextProfile context = meetingActive ? _profile.Meeting : _profile.Normal;
                bool safeToLearn = ReplayClassifier.Analyze(sample).Cause == ReplayCause.None && sample.ForegroundResponsive;
                if (safeToLearn && context.IsReady)
                {
                    BaselineEvaluation beforeLearning = Evaluate(sample, context, meetingActive, false);
                    safeToLearn = beforeLearning.Score >= 85;
                }
                if (safeToLearn)
                {
                    context.Add(sample);
                    if (_profile.FirstSampleUtc == DateTime.MinValue) _profile.FirstSampleUtc = sample.TimestampUtc;
                    _profile.LastSampleUtc = sample.TimestampUtc;
                    _acceptedSinceSave++;
                    if (_acceptedSinceSave >= 6) SaveInternal();
                }
                BaselineContextProfile reference = context.IsReady ? context : _profile.Normal.IsReady ? _profile.Normal : context;
                _current = Evaluate(sample, reference, meetingActive && ReferenceEquals(reference, _profile.Meeting), safeToLearn);
                return _current.Clone();
            }
        }

        public BaselineView GetView()
        {
            lock (_syncRoot)
            {
                return new BaselineView { Profile = _profile.Clone(), Evaluation = _current.Clone() };
            }
        }

        public void Dispose()
        {
            lock (_syncRoot) SaveInternal();
        }

        private BaselineEvaluation Evaluate(ReplaySample sample, BaselineContextProfile reference, bool meetingProfile, bool accepted)
        {
            BaselineEvaluation result = new BaselineEvaluation
            {
                SamplesCollected = reference.SampleCount,
                UsedMeetingProfile = meetingProfile,
                SampleAccepted = accepted
            };
            if (!reference.IsReady)
            {
                result.State = BaselineState.Learning;
                result.Score = 100;
                result.Title = "Aprendendo o padrão deste computador";
                result.Explanation = "Leitura " + Math.Min(reference.SampleCount, RequiredSamples) + " de " + RequiredSamples + ". O índice personalizado aparecerá em poucos minutos e continuará refinando com o uso.";
                result.PrimaryMetric = "Aprendizado " + result.LearningPercent + "%";
                return result;
            }

            List<BaselineDeviation> deviations = new List<BaselineDeviation>();
            AddHigh(deviations, "Memória", sample.MemoryPercent, reference.MemoryPercent, 8, 35,
                "A RAM está acima da faixa habitual de " + Range(reference.MemoryPercent, 8, "%") + ".");
            AddLow(deviations, "Folga de memória", sample.AvailableBytes / 1024d / 1024d, reference.AvailableMegabytes, 512, 30,
                "A memória disponível caiu abaixo da folga habitual de " + RangeBytes(reference.AvailableMegabytes, 512) + ".");
            AddHigh(deviations, "Paginação", sample.PageReadsPerSecond, reference.PageReadsPerSecond, 30, 30,
                "O Windows está buscando no disco mais páginas de memória que o normal.");
            AddHigh(deviations, "CPU", sample.CpuPercent, reference.CpuPercent, 20, 35,
                "A CPU está acima da faixa habitual de " + Range(reference.CpuPercent, 20, "%") + ".");
            AddHigh(deviations, "Fila da CPU", sample.ProcessorQueueLength, reference.ProcessorQueueLength, 2, 25,
                "Mais trabalhos estão esperando pela CPU que no uso normal.");
            AddHigh(deviations, "Latência do armazenamento", sample.DiskLatencyMilliseconds, reference.DiskLatencyMilliseconds, 15, 35,
                "O armazenamento está respondendo mais devagar que o padrão local.");
            AddHigh(deviations, "Fila do armazenamento", sample.DiskQueueLength, reference.DiskQueueLength, 1.5d, 25,
                "As operações do armazenamento estão mais acumuladas que o normal.");
            if (sample.TemperatureCelsius > 0 && reference.TemperatureCelsius.Count >= 6)
                AddHigh(deviations, "Temperatura", sample.TemperatureCelsius, reference.TemperatureCelsius, 10, 25,
                    "A temperatura está acima da faixa habitual de " + Range(reference.TemperatureCelsius, 10, " °C") + ".");

            ReplayAssessment absolute = ReplayClassifier.Analyze(sample);
            int totalPenalty = deviations.OrderByDescending(item => item.Penalty).Take(3).Sum(item => item.Penalty);
            if (absolute.Cause != ReplayCause.None) totalPenalty = Math.Max(totalPenalty, Math.Max(35, absolute.Score - 30));
            result.Score = Math.Max(10, Math.Min(100, 100 - Math.Min(90, totalPenalty)));
            result.State = BaselineState.Personalized;
            result.SamplesCollected = reference.SampleCount;
            BaselineDeviation primary = deviations.OrderByDescending(item => item.Penalty).FirstOrDefault();
            if (result.Score >= 85)
            {
                result.Title = "Dentro do padrão deste computador";
                result.Explanation = "RAM normalmente em " + Range(reference.MemoryPercent, 8, "%") + "; agora " + sample.MemoryPercent.ToString("0", CultureInfo.CurrentCulture) + "%. Nenhum desvio persistente se destaca.";
                result.PrimaryMetric = "Fluxo habitual";
            }
            else if (result.Score >= 60)
            {
                result.Title = "Mais lento que o habitual";
                result.Explanation = primary == null ? "Alguns sinais estão acima do padrão local." : primary.Explanation;
                result.PrimaryMetric = primary == null ? "Desvio moderado" : primary.Name;
            }
            else
            {
                result.Title = "Fora do padrão deste computador";
                result.Explanation = primary == null ? "O conjunto de sinais está muito diferente do uso normal." : primary.Explanation;
                result.PrimaryMetric = primary == null ? "Desvio forte" : primary.Name;
            }
            if (meetingProfile) result.Explanation += " Comparação usando o padrão de reunião.";
            return result;
        }

        private static void AddHigh(List<BaselineDeviation> items, string name, double current, BaselineMetric metric,
            double floor, int maximumPenalty, string explanation)
        {
            if (metric == null || metric.Count == 0) return;
            double spread = Math.Max(floor, metric.StandardDeviation * 2d);
            double threshold = metric.Mean + spread;
            if (current <= threshold) return;
            int penalty = Math.Min(maximumPenalty, 8 + (int)Math.Ceiling((current - threshold) * 20d / Math.Max(1, spread)));
            items.Add(new BaselineDeviation { Name = name, Penalty = penalty, Explanation = explanation });
        }

        private static void AddLow(List<BaselineDeviation> items, string name, double current, BaselineMetric metric,
            double floor, int maximumPenalty, string explanation)
        {
            if (metric == null || metric.Count == 0 || current <= 0) return;
            double spread = Math.Max(floor, metric.StandardDeviation * 2d);
            double threshold = metric.Mean - spread;
            if (current >= threshold) return;
            int penalty = Math.Min(maximumPenalty, 8 + (int)Math.Ceiling((threshold - current) * 20d / Math.Max(1, spread)));
            items.Add(new BaselineDeviation { Name = name, Penalty = penalty, Explanation = explanation });
        }

        internal static string Range(BaselineMetric metric, double floor, string suffix)
        {
            if (metric == null || metric.Count == 0) return "—";
            double spread = Math.Max(floor, metric.StandardDeviation * 2d);
            double low = Math.Max(0, metric.Mean - spread);
            double high = metric.Mean + spread;
            return low.ToString("0", CultureInfo.CurrentCulture) + "–" + high.ToString("0", CultureInfo.CurrentCulture) + suffix;
        }

        internal static string RangeBytes(BaselineMetric metric, double floor)
        {
            if (metric == null || metric.Count == 0) return "—";
            double spread = Math.Max(floor, metric.StandardDeviation * 2d);
            long low = (long)(Math.Max(0, metric.Mean - spread) * 1024d * 1024d);
            long high = (long)((metric.Mean + spread) * 1024d * 1024d);
            return MainForm.FormatBytes(low) + "–" + MainForm.FormatBytes(high);
        }

        private void SaveInternal()
        {
            try
            {
                if (_profile.Normal.SampleCount == 0 && _profile.Meeting.SampleCount == 0) return;
                string directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                string temporary = _path + ".tmp";
                File.WriteAllLines(temporary, Serialize(_profile), new UTF8Encoding(false));
                if (File.Exists(_path)) File.Replace(temporary, _path, null);
                else File.Move(temporary, _path);
                _acceptedSinceSave = 0;
            }
            catch { }
        }

        private static string[] Serialize(BaselineProfile profile)
        {
            List<string> lines = new List<string>
            {
                "Version=1",
                "FirstSampleUtc=" + profile.FirstSampleUtc.Ticks.ToString(CultureInfo.InvariantCulture),
                "LastSampleUtc=" + profile.LastSampleUtc.Ticks.ToString(CultureInfo.InvariantCulture)
            };
            WriteContext(lines, "Normal", profile.Normal);
            WriteContext(lines, "Meeting", profile.Meeting);
            return lines.ToArray();
        }

        private static void WriteContext(List<string> lines, string prefix, BaselineContextProfile context)
        {
            lines.Add(prefix + ".MemoryPercent=" + context.MemoryPercent.Serialize());
            lines.Add(prefix + ".AvailableMegabytes=" + context.AvailableMegabytes.Serialize());
            lines.Add(prefix + ".CommitPercent=" + context.CommitPercent.Serialize());
            lines.Add(prefix + ".PageReadsPerSecond=" + context.PageReadsPerSecond.Serialize());
            lines.Add(prefix + ".CpuPercent=" + context.CpuPercent.Serialize());
            lines.Add(prefix + ".ProcessorQueueLength=" + context.ProcessorQueueLength.Serialize());
            lines.Add(prefix + ".DiskActivePercent=" + context.DiskActivePercent.Serialize());
            lines.Add(prefix + ".DiskLatencyMilliseconds=" + context.DiskLatencyMilliseconds.Serialize());
            lines.Add(prefix + ".DiskQueueLength=" + context.DiskQueueLength.Serialize());
            lines.Add(prefix + ".TemperatureCelsius=" + context.TemperatureCelsius.Serialize());
        }

        private static BaselineProfile Load(string path)
        {
            BaselineProfile profile = new BaselineProfile();
            try
            {
                if (!File.Exists(path)) return profile;
                foreach (string line in File.ReadAllLines(path))
                {
                    int separator = line.IndexOf('=');
                    if (separator < 1) continue;
                    string key = line.Substring(0, separator);
                    string value = line.Substring(separator + 1);
                    if (key == "FirstSampleUtc") profile.FirstSampleUtc = ReadDate(value);
                    else if (key == "LastSampleUtc") profile.LastSampleUtc = ReadDate(value);
                    else if (key.StartsWith("Normal.", StringComparison.Ordinal)) ReadMetric(profile.Normal, key.Substring(7), value);
                    else if (key.StartsWith("Meeting.", StringComparison.Ordinal)) ReadMetric(profile.Meeting, key.Substring(8), value);
                }
            }
            catch { return new BaselineProfile(); }
            return profile;
        }

        private static DateTime ReadDate(string value)
        {
            long ticks;
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks) && ticks > 0
                ? new DateTime(ticks, DateTimeKind.Utc) : DateTime.MinValue;
        }

        private static void ReadMetric(BaselineContextProfile context, string name, string value)
        {
            BaselineMetric metric = BaselineMetric.Parse(value);
            if (name == "MemoryPercent") context.MemoryPercent = metric;
            else if (name == "AvailableMegabytes") context.AvailableMegabytes = metric;
            else if (name == "CommitPercent") context.CommitPercent = metric;
            else if (name == "PageReadsPerSecond") context.PageReadsPerSecond = metric;
            else if (name == "CpuPercent") context.CpuPercent = metric;
            else if (name == "ProcessorQueueLength") context.ProcessorQueueLength = metric;
            else if (name == "DiskActivePercent") context.DiskActivePercent = metric;
            else if (name == "DiskLatencyMilliseconds") context.DiskLatencyMilliseconds = metric;
            else if (name == "DiskQueueLength") context.DiskQueueLength = metric;
            else if (name == "TemperatureCelsius") context.TemperatureCelsius = metric;
        }
    }
}
