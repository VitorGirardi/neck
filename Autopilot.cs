using System;
using System.Collections.Generic;
using System.Linq;

namespace Neck
{
    internal enum AutopilotState
    {
        Disabled,
        Learning,
        Flowing,
        Watching,
        Protecting,
        Cooling,
        Paused
    }

    internal enum AutopilotCause { None, Memory, Cpu, Disk, Thermal }

    internal sealed class AutopilotDecision
    {
        public AutopilotState State;
        public AutopilotCause Cause;
        public string Title = "Autopilot desativado";
        public string Explanation = "Ative quando quiser que o Neck proteja o fluxo preventivamente.";
        public int Confidence;
        public int EstimatedSeconds;
        public int ProtectedApplications;
        public string ProtectedSummary = string.Empty;
        public bool ShouldProtect;
        public bool ShouldRestore;
        public bool Actionable;
        public bool Simulated;

        public AutopilotDecision Clone()
        {
            return (AutopilotDecision)MemberwiseClone();
        }
    }

    internal sealed class AutopilotForecast
    {
        public AutopilotCause Cause;
        public int Confidence;
        public int EstimatedSeconds;
        public bool Actionable;
        public string Explanation = string.Empty;
    }

    internal sealed class AutopilotEngine
    {
        private const int MinimumSamples = 4;
        private const int HorizonSeconds = 60;
        private readonly object _syncRoot = new object();
        private readonly List<ReplaySample> _samples = new List<ReplaySample>();
        private AutopilotDecision _current = new AutopilotDecision();
        private AutopilotCause _streakCause;
        private int _predictionStreak;
        private int _stableStreak;
        private bool _protecting;
        private DateTime _protectingSinceUtc = DateTime.MinValue;
        private DateTime _cooldownUntilUtc = DateTime.MinValue;

        public AutopilotDecision Evaluate(ReplaySample sample, BaselineView baseline, bool enabled,
            bool meetingActive, bool temporarilyPaused)
        {
            lock (_syncRoot)
            {
                DateTime now = sample == null || sample.TimestampUtc == DateTime.MinValue ? DateTime.UtcNow : sample.TimestampUtc;
                if (!enabled)
                    return StopEvaluation(AutopilotState.Disabled, "Autopilot desativado",
                        "O monitoramento continua, mas nenhuma proteção preventiva será aplicada.");
                if (temporarilyPaused)
                    return StopEvaluation(AutopilotState.Paused, "Autopilot em pausa",
                        "A aceleração manual já está controlando os recursos agora.");

                BaselineContextProfile context = SelectContext(baseline, meetingActive);
                if (context == null || !context.IsReady)
                    return StopEvaluation(AutopilotState.Learning, "Autopilot aprendendo",
                        "O Índice de Fluxo precisa concluir o primeiro padrão antes da proteção preventiva.");
                if (sample == null) return _current.Clone();

                if (_samples.Count > 0 && now - _samples[_samples.Count - 1].TimestampUtc > TimeSpan.FromSeconds(45))
                    ClearTrend();
                _samples.Add(sample);
                if (_samples.Count > 6) _samples.RemoveAt(0);
                if (_samples.Count < MinimumSamples)
                {
                    _current = Decision(AutopilotState.Flowing, AutopilotCause.None,
                        "Autopilot observando o fluxo", "Reunindo uma sequência curta para prever tendências, sem agir por um pico isolado.");
                    return _current.Clone();
                }

                ReplayAssessment currentPressure = ReplayClassifier.Analyze(sample);
                AutopilotForecast forecast = BuildForecast(_samples, context);
                if (_protecting)
                {
                    bool timedOut = now - _protectingSinceUtc >= TimeSpan.FromMinutes(10);
                    if ((forecast.Cause == AutopilotCause.None || !forecast.Actionable) && currentPressure.Cause == ReplayCause.None)
                        _stableStreak++;
                    else _stableStreak = 0;

                    if (timedOut || _stableStreak >= 3)
                    {
                        _protecting = false;
                        _stableStreak = 0;
                        _predictionStreak = 0;
                        _cooldownUntilUtc = now.AddMinutes(2);
                        _current = Decision(AutopilotState.Cooling, AutopilotCause.None,
                            "Proteção preventiva concluída", timedOut
                                ? "O limite seguro de dez minutos foi atingido; todos os aplicativos serão restaurados."
                                : "Três leituras confirmaram a recuperação; todos os aplicativos serão restaurados.");
                        _current.ShouldRestore = true;
                        return _current.Clone();
                    }

                    int protectedApplications = _current.ProtectedApplications;
                    string protectedSummary = _current.ProtectedSummary;
                    _current = ForecastDecision(AutopilotState.Protecting, forecast);
                    _current.ProtectedApplications = protectedApplications;
                    _current.ProtectedSummary = protectedSummary;
                    _current.Title = "Autopilot protegendo o fluxo";
                    _current.Explanation = _current.ProtectedApplications > 0
                        ? "Redução temporária em " + _current.ProtectedApplications + " aplicativo(s) em segundo plano. Tudo volta ao normal automaticamente."
                        : "A proteção preventiva está ativa e será restaurada assim que o fluxo estabilizar.";
                    return _current.Clone();
                }

                if (currentPressure.Cause != ReplayCause.None)
                {
                    ResetPrediction();
                    _current = Decision(AutopilotState.Watching, Map(currentPressure.Cause),
                        "O gargalo já chegou", "O Replay assumiu o diagnóstico. O Autopilot não inicia uma nova intervenção depois que o incidente já começou.");
                    return _current.Clone();
                }

                if (forecast.Cause == AutopilotCause.None)
                {
                    ResetPrediction();
                    _current = Decision(now < _cooldownUntilUtc ? AutopilotState.Cooling : AutopilotState.Flowing,
                        AutopilotCause.None, now < _cooldownUntilUtc ? "Fluxo recuperado" : "Autopilot acompanhando",
                        now < _cooldownUntilUtc
                            ? "A proteção foi restaurada e o Neck aguarda um pouco antes de considerar uma nova intervenção."
                            : "Nenhuma tendência de gargalo foi confirmada.");
                    return _current.Clone();
                }

                _current = ForecastDecision(AutopilotState.Watching, forecast);
                if (!forecast.Actionable)
                {
                    ResetPrediction();
                    _current.Title = "Tendência fora do padrão";
                    _current.Explanation += " O Neck apenas orientará; esta causa não permite intervenção automática segura.";
                    return _current.Clone();
                }
                if (_streakCause == forecast.Cause) _predictionStreak++;
                else
                {
                    _streakCause = forecast.Cause;
                    _predictionStreak = 1;
                }
                if (_predictionStreak < 2 || now < _cooldownUntilUtc) return _current.Clone();

                _protecting = true;
                _protectingSinceUtc = now;
                _stableStreak = 0;
                _current = ForecastDecision(AutopilotState.Protecting, forecast);
                _current.Title = "Proteção preventiva iniciada";
                _current.ShouldProtect = true;
                return _current.Clone();
            }
        }

        public AutopilotDecision ReportProtection(int applications, string summary, DateTime utcNow)
        {
            lock (_syncRoot)
            {
                _current.ShouldProtect = false;
                _current.ProtectedApplications = Math.Max(0, applications);
                _current.ProtectedSummary = summary ?? string.Empty;
                if (applications <= 0)
                {
                    _protecting = false;
                    _cooldownUntilUtc = utcNow.AddMinutes(1);
                    _current.State = AutopilotState.Cooling;
                    _current.Title = "Nenhum aplicativo seguro para reduzir";
                    _current.Explanation = "A previsão foi mantida, mas o Neck não encontrou um aplicativo opcional em segundo plano. Nada foi alterado.";
                }
                else
                {
                    _current.State = AutopilotState.Protecting;
                    _current.Title = "Autopilot protegendo o fluxo";
                    _current.Explanation = applications + " aplicativo(s) em segundo plano usam temporariamente menos recursos" +
                        (string.IsNullOrWhiteSpace(summary) ? "." : ": " + summary + ".");
                }
                return _current.Clone();
            }
        }

        public AutopilotDecision ReportRestored()
        {
            lock (_syncRoot)
            {
                _current.ShouldRestore = false;
                _current.ProtectedApplications = 0;
                _current.ProtectedSummary = string.Empty;
                return _current.Clone();
            }
        }

        public AutopilotDecision DisableNow()
        {
            lock (_syncRoot)
            {
                _protecting = false;
                _cooldownUntilUtc = DateTime.MinValue;
                ClearTrend();
                _current = Decision(AutopilotState.Disabled, AutopilotCause.None, "Autopilot desativado",
                    "Todas as proteções preventivas foram restauradas.");
                return _current.Clone();
            }
        }

        public AutopilotDecision GetCurrent()
        {
            lock (_syncRoot) return _current.Clone();
        }

        private AutopilotDecision StopEvaluation(AutopilotState state, string title, string explanation)
        {
            bool restore = _protecting || _current.ProtectedApplications > 0;
            _protecting = false;
            ClearTrend();
            _current = Decision(state, AutopilotCause.None, title, explanation);
            _current.ShouldRestore = restore;
            return _current.Clone();
        }

        private void ClearTrend()
        {
            _samples.Clear();
            ResetPrediction();
            _stableStreak = 0;
        }

        private void ResetPrediction()
        {
            _predictionStreak = 0;
            _streakCause = AutopilotCause.None;
        }

        private static BaselineContextProfile SelectContext(BaselineView baseline, bool meetingActive)
        {
            if (baseline == null || baseline.Profile == null) return null;
            if (meetingActive && baseline.Profile.Meeting != null && baseline.Profile.Meeting.IsReady) return baseline.Profile.Meeting;
            return baseline.Profile.Normal;
        }

        private static AutopilotForecast BuildForecast(IList<ReplaySample> samples, BaselineContextProfile context)
        {
            ReplaySample last = samples[samples.Count - 1];
            double memorySlope = Slope(samples, item => item.MemoryPercent);
            double availableSlope = Slope(samples, item => item.AvailableBytes / 1024d / 1024d);
            double commitSlope = Slope(samples, item => item.CommitPercent);
            double pagingSlope = Slope(samples, item => item.PageReadsPerSecond);
            double cpuSlope = Slope(samples, item => item.CpuPercent);
            double cpuQueueSlope = Slope(samples, item => item.ProcessorQueueLength);
            double diskActiveSlope = Slope(samples, item => item.DiskActivePercent);
            double diskLatencySlope = Slope(samples, item => item.DiskLatencyMilliseconds);
            double diskQueueSlope = Slope(samples, item => item.DiskQueueLength);
            double temperatureSlope = Slope(samples, item => item.TemperatureCelsius);

            ReplaySample projected = new ReplaySample
            {
                TimestampUtc = last.TimestampUtc.AddSeconds(HorizonSeconds),
                MemoryPercent = Clamp(last.MemoryPercent + memorySlope * HorizonSeconds, 0, 100),
                AvailableBytes = (long)(Clamp(last.AvailableBytes / 1024d / 1024d + availableSlope * HorizonSeconds, 0, 1048576) * 1024d * 1024d),
                CommitPercent = Clamp(last.CommitPercent + commitSlope * HorizonSeconds, 0, 100),
                PageReadsPerSecond = Clamp(last.PageReadsPerSecond + pagingSlope * HorizonSeconds, 0, 100000),
                CpuPercent = Clamp(last.CpuPercent + cpuSlope * HorizonSeconds, 0, 100),
                ProcessorQueueLength = Clamp(last.ProcessorQueueLength + cpuQueueSlope * HorizonSeconds, 0, 1000),
                DiskActivePercent = Clamp(last.DiskActivePercent + diskActiveSlope * HorizonSeconds, 0, 100),
                DiskLatencyMilliseconds = Clamp(last.DiskLatencyMilliseconds + diskLatencySlope * HorizonSeconds, 0, 10000),
                DiskQueueLength = Clamp(last.DiskQueueLength + diskQueueSlope * HorizonSeconds, 0, 1000),
                TemperatureCelsius = last.TemperatureCelsius <= 0 ? 0 : Clamp(last.TemperatureCelsius + temperatureSlope * HorizonSeconds, 0, 125),
                TopMemoryProcess = last.TopMemoryProcess,
                TopCpuProcess = last.TopCpuProcess,
                ForegroundProcess = last.ForegroundProcess,
                ForegroundResponsive = true
            };

            ReplayAssessment absolute = ReplayClassifier.Analyze(projected);
            AutopilotCause absoluteCause = Map(absolute.Cause);
            if (absoluteCause != AutopilotCause.None && IsRising(absoluteCause, memorySlope, availableSlope, cpuSlope,
                cpuQueueSlope, diskLatencySlope, diskQueueSlope, temperatureSlope))
            {
                int seconds = EstimateSeconds(absoluteCause, last, memorySlope, availableSlope, commitSlope, cpuSlope,
                    cpuQueueSlope, diskLatencySlope, diskQueueSlope, temperatureSlope);
                return new AutopilotForecast
                {
                    Cause = absoluteCause,
                    Confidence = Math.Min(96, 58 + absolute.Score / 3 + samples.Count * 2),
                    EstimatedSeconds = seconds,
                    Actionable = absoluteCause == AutopilotCause.Memory || absoluteCause == AutopilotCause.Cpu,
                    Explanation = ForecastText(absoluteCause, seconds)
                };
            }

            List<Tuple<AutopilotCause, double, string>> drift = new List<Tuple<AutopilotCause, double, string>>();
            double memoryHigh = context.MemoryPercent.Mean + Math.Max(8, context.MemoryPercent.StandardDeviation * 2);
            if (memorySlope >= 0.04d && projected.MemoryPercent >= Math.Max(82, memoryHigh))
                drift.Add(Tuple.Create(AutopilotCause.Memory, projected.MemoryPercent - Math.Max(82, memoryHigh), "A RAM está subindo para fora da faixa habitual."));
            double availableLow = context.AvailableMegabytes.Mean - Math.Max(512, context.AvailableMegabytes.StandardDeviation * 2);
            if (availableSlope <= -8d && projected.AvailableBytes / 1024d / 1024d <= Math.Min(2048, Math.Max(512, availableLow)))
                drift.Add(Tuple.Create(AutopilotCause.Memory, Math.Abs(availableSlope) / 8d, "A folga de memória está diminuindo de forma contínua."));
            double cpuHigh = Math.Max(60, context.CpuPercent.Mean + Math.Max(20, context.CpuPercent.StandardDeviation * 2));
            if (cpuSlope >= 0.20d && projected.CpuPercent >= cpuHigh)
                drift.Add(Tuple.Create(AutopilotCause.Cpu, projected.CpuPercent - cpuHigh, "A CPU está acelerando para fora do padrão local."));
            double diskHigh = Math.Max(25, context.DiskLatencyMilliseconds.Mean + Math.Max(15, context.DiskLatencyMilliseconds.StandardDeviation * 2));
            if (diskLatencySlope >= 0.25d && projected.DiskLatencyMilliseconds >= diskHigh)
                drift.Add(Tuple.Create(AutopilotCause.Disk, projected.DiskLatencyMilliseconds - diskHigh, "A resposta do armazenamento está piorando."));
            double temperatureHigh = Math.Max(85, context.TemperatureCelsius.Mean + Math.Max(10, context.TemperatureCelsius.StandardDeviation * 2));
            if (last.TemperatureCelsius > 0 && temperatureSlope >= 0.025d && projected.TemperatureCelsius >= temperatureHigh)
                drift.Add(Tuple.Create(AutopilotCause.Thermal, projected.TemperatureCelsius - temperatureHigh, "A temperatura está subindo além da faixa habitual."));

            Tuple<AutopilotCause, double, string> primary = drift.OrderByDescending(item => item.Item2).FirstOrDefault();
            if (primary == null) return new AutopilotForecast();
            return new AutopilotForecast
            {
                Cause = primary.Item1,
                Confidence = Math.Min(82, 55 + samples.Count * 3 + (int)Math.Min(9, primary.Item2)),
                EstimatedSeconds = HorizonSeconds,
                Actionable = false,
                Explanation = primary.Item3
            };
        }

        private static AutopilotDecision ForecastDecision(AutopilotState state, AutopilotForecast forecast)
        {
            AutopilotDecision decision = Decision(state, forecast.Cause, "Tendência de " + CauseName(forecast.Cause) + " confirmada", forecast.Explanation);
            decision.Confidence = forecast.Confidence;
            decision.EstimatedSeconds = forecast.EstimatedSeconds;
            decision.Actionable = forecast.Actionable;
            return decision;
        }

        private static AutopilotDecision Decision(AutopilotState state, AutopilotCause cause, string title, string explanation)
        {
            return new AutopilotDecision { State = state, Cause = cause, Title = title, Explanation = explanation };
        }

        private static double Slope(IList<ReplaySample> samples, Func<ReplaySample, double> selector)
        {
            DateTime origin = samples[0].TimestampUtc;
            double meanX = samples.Average(item => (item.TimestampUtc - origin).TotalSeconds);
            double meanY = samples.Average(selector);
            double numerator = 0;
            double denominator = 0;
            foreach (ReplaySample sample in samples)
            {
                double x = (sample.TimestampUtc - origin).TotalSeconds - meanX;
                numerator += x * (selector(sample) - meanY);
                denominator += x * x;
            }
            return denominator <= 0 ? 0 : numerator / denominator;
        }

        private static bool IsRising(AutopilotCause cause, double memory, double available, double cpu, double cpuQueue,
            double diskLatency, double diskQueue, double temperature)
        {
            if (cause == AutopilotCause.Memory) return memory >= 0.04d || available <= -8d;
            if (cause == AutopilotCause.Cpu) return cpu >= 0.20d || cpuQueue >= 0.03d;
            if (cause == AutopilotCause.Disk) return diskLatency >= 0.25d || diskQueue >= 0.02d;
            if (cause == AutopilotCause.Thermal) return temperature >= 0.025d;
            return false;
        }

        private static int EstimateSeconds(AutopilotCause cause, ReplaySample sample, double memory, double available,
            double commit, double cpu, double cpuQueue, double diskLatency, double diskQueue, double temperature)
        {
            List<double> estimates = new List<double>();
            if (cause == AutopilotCause.Memory)
            {
                AddEstimate(estimates, 85 - sample.MemoryPercent, memory);
                AddEstimate(estimates, sample.AvailableBytes / 1024d / 1024d - 1024, -available);
                AddEstimate(estimates, 90 - sample.CommitPercent, commit);
            }
            else if (cause == AutopilotCause.Cpu)
            {
                AddEstimate(estimates, 85 - sample.CpuPercent, cpu);
                AddEstimate(estimates, Math.Max(2, Environment.ProcessorCount * 0.5d) - sample.ProcessorQueueLength, cpuQueue);
            }
            else if (cause == AutopilotCause.Disk)
            {
                AddEstimate(estimates, 25 - sample.DiskLatencyMilliseconds, diskLatency);
                AddEstimate(estimates, 2 - sample.DiskQueueLength, diskQueue);
            }
            else if (cause == AutopilotCause.Thermal) AddEstimate(estimates, 90 - sample.TemperatureCelsius, temperature);
            double best = estimates.Where(value => value >= 0 && !double.IsInfinity(value) && !double.IsNaN(value)).DefaultIfEmpty(HorizonSeconds).Min();
            return Math.Max(5, Math.Min(HorizonSeconds, (int)Math.Ceiling(best)));
        }

        private static void AddEstimate(List<double> values, double remaining, double slope)
        {
            if (remaining <= 0) values.Add(0);
            else if (slope > 0) values.Add(remaining / slope);
        }

        private static string ForecastText(AutopilotCause cause, int seconds)
        {
            string time = seconds >= 55 ? "no próximo minuto" : "em cerca de " + Math.Max(5, seconds) + " segundos";
            if (cause == AutopilotCause.Memory) return "A tendência indica perda de folga de memória " + time + ".";
            if (cause == AutopilotCause.Cpu) return "A tendência indica disputa persistente de CPU " + time + ".";
            if (cause == AutopilotCause.Disk) return "A latência do armazenamento pode atingir uma faixa crítica " + time + ".";
            if (cause == AutopilotCause.Thermal) return "A temperatura pode atingir uma faixa crítica " + time + ".";
            return string.Empty;
        }

        internal static AutopilotCause Map(ReplayCause cause)
        {
            if (cause == ReplayCause.MemoryPressure) return AutopilotCause.Memory;
            if (cause == ReplayCause.CpuContention) return AutopilotCause.Cpu;
            if (cause == ReplayCause.DiskStall) return AutopilotCause.Disk;
            if (cause == ReplayCause.ThermalPressure) return AutopilotCause.Thermal;
            return AutopilotCause.None;
        }

        internal static string CauseName(AutopilotCause cause)
        {
            if (cause == AutopilotCause.Memory) return "memória";
            if (cause == AutopilotCause.Cpu) return "CPU";
            if (cause == AutopilotCause.Disk) return "armazenamento";
            if (cause == AutopilotCause.Thermal) return "temperatura";
            return "gargalo";
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }

    internal sealed class AutopilotProtectionResult
    {
        public int ApplicationsProtected;
        public int ApplicationsChanged;
        public int ProcessesChanged;
        public int AccessErrors;
        public string Summary = string.Empty;
    }

    internal static class AutopilotProtectionManager
    {
        private static readonly object SyncRoot = new object();
        private static readonly HashSet<string> OwnedApplications = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static DateTime _lastRefreshUtc = DateTime.MinValue;

        public static int ActiveCount { get { lock (SyncRoot) return OwnedApplications.Count; } }
        public static string ActiveSummary
        {
            get
            {
                lock (SyncRoot)
                    return string.Join(", ", OwnedApplications.OrderBy(name => name).Take(2).Select(SystemInfo.FriendlyProcessName));
            }
        }

        public static AutopilotProtectionResult Start(string foregroundProcess, AutopilotCause cause, string preferredProcess)
        {
            return RefreshCore(foregroundProcess, cause, preferredProcess, SosInspector.GetFocusShieldCandidates(), SystemInfo.GetMemoryStatus(), DateTime.UtcNow, true);
        }

        public static AutopilotProtectionResult Refresh(string foregroundProcess, AutopilotCause cause, string preferredProcess)
        {
            lock (SyncRoot)
            {
                if (DateTime.UtcNow - _lastRefreshUtc < TimeSpan.FromSeconds(30)) return CurrentResult();
            }
            return RefreshCore(foregroundProcess, cause, preferredProcess, SosInspector.GetFocusShieldCandidates(), SystemInfo.GetMemoryStatus(), DateTime.UtcNow, false);
        }

        internal static AutopilotProtectionResult StartForTesting(string foregroundProcess, IEnumerable<SosCandidate> candidates,
            MemoryStatus memory, DateTime utcNow, AutopilotCause cause = AutopilotCause.Memory, string preferredProcess = null)
        {
            return RefreshCore(foregroundProcess, cause, preferredProcess, candidates, memory, utcNow, true);
        }

        public static AutopilotProtectionResult Stop()
        {
            AutopilotProtectionResult result = new AutopilotProtectionResult();
            lock (SyncRoot)
            {
                foreach (string processName in OwnedApplications.ToList())
                {
                    EfficiencyModeResult restored = EfficiencyModeManager.Restore(processName);
                    result.ProcessesChanged += restored.ProcessesChanged;
                    result.AccessErrors += restored.AccessErrors;
                    result.ApplicationsChanged++;
                }
                OwnedApplications.Clear();
                _lastRefreshUtc = DateTime.MinValue;
            }
            return result;
        }

        public static EfficiencyModeResult ReleaseForManualControl(string processName)
        {
            lock (SyncRoot)
            {
                if (!OwnedApplications.Remove(processName)) return new EfficiencyModeResult { ProcessName = processName ?? string.Empty };
            }
            return EfficiencyModeManager.Restore(processName);
        }

        private static AutopilotProtectionResult RefreshCore(string foregroundProcess, AutopilotCause cause, string preferredProcess,
            IEnumerable<SosCandidate> candidates, MemoryStatus memory, DateTime utcNow, bool force)
        {
            if (string.IsNullOrWhiteSpace(foregroundProcess) || FocusModeManager.IsActive) return CurrentResult();
            List<SosCandidate> available = candidates == null ? new List<SosCandidate>() : candidates.Where(item => item != null).ToList();
            List<SosCandidate> selected = FocusShieldManager.SelectCandidates(available, foregroundProcess, memory).ToList();
            SosCandidate preferred = available.FirstOrDefault(item =>
                string.Equals(item.ProcessName, preferredProcess, StringComparison.OrdinalIgnoreCase) &&
                item.VisibleWindows > 0 && !string.Equals(item.ProcessName, foregroundProcess, StringComparison.OrdinalIgnoreCase) &&
                FocusShieldManager.CanShield(item.ProcessName) &&
                (cause == AutopilotCause.Cpu || item.MemoryBytes >= 192L * 1024 * 1024));
            if (preferred != null)
            {
                selected.RemoveAll(item => string.Equals(item.ProcessName, preferred.ProcessName, StringComparison.OrdinalIgnoreCase));
                selected.Insert(0, preferred);
            }
            selected = selected.Take(2).ToList();
            HashSet<string> selectedNames = new HashSet<string>(selected.Select(item => item.ProcessName), StringComparer.OrdinalIgnoreCase);
            AutopilotProtectionResult result = new AutopilotProtectionResult();
            lock (SyncRoot)
            {
                if (!force && utcNow - _lastRefreshUtc < TimeSpan.FromSeconds(30)) return CurrentResult();
                _lastRefreshUtc = utcNow;
                foreach (string owned in OwnedApplications.ToList())
                {
                    if (selectedNames.Contains(owned)) continue;
                    EfficiencyModeResult restored = EfficiencyModeManager.Restore(owned);
                    result.ProcessesChanged += restored.ProcessesChanged;
                    result.AccessErrors += restored.AccessErrors;
                    result.ApplicationsChanged++;
                    OwnedApplications.Remove(owned);
                }
                foreach (SosCandidate candidate in selected)
                {
                    if (OwnedApplications.Contains(candidate.ProcessName) || EfficiencyModeManager.IsActive(candidate.ProcessName)) continue;
                    EfficiencyModeResult applied = EfficiencyModeManager.Apply(candidate.ProcessName);
                    result.ProcessesChanged += applied.ProcessesChanged;
                    result.AccessErrors += applied.AccessErrors;
                    if (!applied.HasChanges) continue;
                    OwnedApplications.Add(candidate.ProcessName);
                    result.ApplicationsChanged++;
                }
                result.ApplicationsProtected = OwnedApplications.Count;
                result.Summary = string.Join(", ", OwnedApplications.OrderBy(name => name).Take(2).Select(SystemInfo.FriendlyProcessName));
            }
            return result;
        }

        private static AutopilotProtectionResult CurrentResult()
        {
            lock (SyncRoot)
            {
                return new AutopilotProtectionResult
                {
                    ApplicationsProtected = OwnedApplications.Count,
                    Summary = string.Join(", ", OwnedApplications.OrderBy(name => name).Take(2).Select(SystemInfo.FriendlyProcessName))
                };
            }
        }
    }

    internal static class AutopilotSimulation
    {
        public static AutopilotDecision Run()
        {
            BaselineProfile profile = new BaselineProfile();
            DateTime start = DateTime.UtcNow.AddMinutes(-10);
            for (int i = 0; i < BaselineEngine.RequiredSamples; i++)
                profile.Normal.Add(Sample(start.AddSeconds(i * 10), 69 + i % 3, 4096 - i % 2 * 32, 72, 20 + i % 4, 2));
            BaselineView view = new BaselineView { Profile = profile, Evaluation = new BaselineEvaluation { State = BaselineState.Personalized } };
            AutopilotEngine engine = new AutopilotEngine();
            AutopilotDecision decision = null;
            double[] memory = { 70, 72, 75, 79, 82, 84 };
            double[] available = { 4096, 3800, 3300, 2600, 1900, 1450 };
            double[] commit = { 72, 74, 78, 82, 86, 89 };
            for (int i = 0; i < memory.Length; i++)
            {
                ReplaySample sample = Sample(start.AddMinutes(6).AddSeconds(i * 10), memory[i], available[i], commit[i], 24, 3);
                sample.ForegroundProcess = "AplicativoImportanteSimulado";
                decision = engine.Evaluate(sample, view, true, false, false);
                if (decision.ShouldProtect)
                    decision = engine.ReportProtection(2, "dois aplicativos simulados", sample.TimestampUtc);
            }
            if (decision == null) decision = engine.GetCurrent();
            decision.Simulated = true;
            return decision;
        }

        private static ReplaySample Sample(DateTime time, double memory, double availableMegabytes, double commit,
            double cpu, double paging)
        {
            return new ReplaySample
            {
                TimestampUtc = time,
                MemoryPercent = memory,
                AvailableBytes = (long)(availableMegabytes * 1024d * 1024d),
                CommitPercent = commit,
                PageReadsPerSecond = paging,
                CpuPercent = cpu,
                ProcessorQueueLength = 0.2d,
                DiskActivePercent = 5,
                DiskLatencyMilliseconds = 2,
                DiskQueueLength = 0.1d,
                TemperatureCelsius = 65,
                ForegroundResponsive = true
            };
        }
    }
}
