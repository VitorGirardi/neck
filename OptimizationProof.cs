using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;

namespace Neck
{
    internal enum OptimizationProofStage
    {
        WaitingForTarget,
        MeasuringBaseline,
        WarmingOptimized,
        MeasuringOptimized,
        Complete
    }

    internal enum OptimizationVerdict
    {
        Inconclusive,
        Improved,
        Neutral,
        Worse
    }

    internal sealed class OptimizationMeasurement
    {
        public bool ResponseAvailable;
        public bool Responsive;
        public double ResponseMilliseconds;
        public double ProcessorQueueLength;
        public double DiskLatencyMilliseconds;
        public long AvailableBytes;
        public long AppMemoryBytes;
    }

    internal sealed class OptimizationEvaluation
    {
        public OptimizationVerdict Verdict;
        public int Score;
        public int Confidence;
        public int ComparableSignals;
        public string Evidence = "atividade insuficiente para uma comparação confiável";
    }

    internal static class OptimizationGainEvaluator
    {
        private const long MemoryThresholdBytes = 64L * 1024 * 1024;

        public static bool ShouldKeep(OptimizationEvaluation evaluation)
        {
            return evaluation != null && evaluation.Verdict == OptimizationVerdict.Improved;
        }

        public static OptimizationEvaluation Evaluate(IList<OptimizationMeasurement> baseline, IList<OptimizationMeasurement> optimized)
        {
            OptimizationEvaluation result = new OptimizationEvaluation();
            if (baseline == null || optimized == null || baseline.Count < 3 || optimized.Count < 3) return result;

            int score = 0;
            int comparable = 0;
            List<string> evidence = new List<string>();
            List<OptimizationMeasurement> baselineResponse = baseline.Where(item => item.ResponseAvailable).ToList();
            List<OptimizationMeasurement> optimizedResponse = optimized.Where(item => item.ResponseAvailable).ToList();
            if (baselineResponse.Count >= 2 && optimizedResponse.Count >= 2)
            {
                int beforeTimeouts = baselineResponse.Count(item => !item.Responsive);
                int afterTimeouts = optimizedResponse.Count(item => !item.Responsive);
                if (beforeTimeouts != afterTimeouts)
                {
                    comparable++;
                    int direction = beforeTimeouts > afterTimeouts ? 1 : -1;
                    score += direction * 45;
                    evidence.Add(direction > 0 ? "a janela voltou a responder com mais consistência" : "a janela respondeu com menos consistência");
                }
                else if (beforeTimeouts == 0)
                {
                    double beforeResponse = baselineResponse.Average(item => item.ResponseMilliseconds);
                    double afterResponse = optimizedResponse.Average(item => item.ResponseMilliseconds);
                    double responseDelta = beforeResponse - afterResponse;
                    if (Math.Max(beforeResponse, afterResponse) >= 2d && Math.Abs(responseDelta) >= 1d)
                    {
                        comparable++;
                        int direction = responseDelta > 0 ? 1 : -1;
                        score += direction * 25;
                        evidence.Add("resposta da janela " + FormatChange(beforeResponse, afterResponse, "ms"));
                    }
                }
            }

            double baselineQueue = baseline.Average(item => item.ProcessorQueueLength);
            double optimizedQueue = optimized.Average(item => item.ProcessorQueueLength);
            if (Math.Max(baselineQueue, optimizedQueue) >= 1d)
            {
                comparable++;
                double queueDelta = baselineQueue - optimizedQueue;
                double relativeQueueDelta = queueDelta / Math.Max(0.5d, baselineQueue);
                if (Math.Abs(queueDelta) >= 0.3d && Math.Abs(relativeQueueDelta) >= 0.20d)
                {
                    int direction = queueDelta > 0 ? 1 : -1;
                    score += direction * 20;
                    evidence.Add("fila da CPU " + FormatChange(baselineQueue, optimizedQueue, string.Empty));
                }
                else evidence.Add("fila da CPU permaneceu semelhante");
            }

            double baselineDisk = baseline.Average(item => item.DiskLatencyMilliseconds);
            double optimizedDisk = optimized.Average(item => item.DiskLatencyMilliseconds);
            if (Math.Max(baselineDisk, optimizedDisk) >= 5d)
            {
                comparable++;
                double diskDelta = baselineDisk - optimizedDisk;
                double relativeDiskDelta = diskDelta / Math.Max(2d, baselineDisk);
                if (Math.Abs(diskDelta) >= 2d && Math.Abs(relativeDiskDelta) >= 0.25d)
                {
                    int direction = diskDelta > 0 ? 1 : -1;
                    score += direction * 20;
                    evidence.Add("latência do disco " + FormatChange(baselineDisk, optimizedDisk, "ms"));
                }
                else evidence.Add("latência do disco permaneceu semelhante");
            }

            long baselineAvailable = (long)baseline.Average(item => (double)item.AvailableBytes);
            long optimizedAvailable = (long)optimized.Average(item => (double)item.AvailableBytes);
            long memoryDelta = optimizedAvailable - baselineAvailable;
            if (Math.Abs(memoryDelta) >= MemoryThresholdBytes)
            {
                comparable++;
                int direction = memoryDelta > 0 ? 1 : -1;
                score += direction * 15;
                evidence.Add(MainForm.FormatBytes(Math.Abs(memoryDelta)) +
                    (direction > 0 ? " a mais de RAM disponível" : " a menos de RAM disponível"));
            }

            result.Score = Math.Max(-100, Math.Min(100, score));
            result.ComparableSignals = comparable;
            result.Confidence = comparable == 0 ? 0 : Math.Min(95, 55 + comparable * 8 + Math.Abs(result.Score) / 5);
            result.Evidence = evidence.Count == 0
                ? "os sinais ficaram abaixo do limite mínimo para uma comparação confiável"
                : string.Join("; ", evidence.ToArray());
            if (comparable == 0) result.Verdict = OptimizationVerdict.Inconclusive;
            else if (result.Score >= 15) result.Verdict = OptimizationVerdict.Improved;
            else if (result.Score <= -15) result.Verdict = OptimizationVerdict.Worse;
            else result.Verdict = OptimizationVerdict.Neutral;
            return result;
        }

        private static string FormatChange(double before, double after, string unit)
        {
            string suffix = string.IsNullOrWhiteSpace(unit) ? string.Empty : " " + unit;
            return "de " + before.ToString("0.0", CultureInfo.CurrentCulture) + suffix + " para " +
                   after.ToString("0.0", CultureInfo.CurrentCulture) + suffix;
        }
    }

    internal sealed class OptimizationOutcome
    {
        public string ProcessName;
        public string DisplayName;
        public DateTime StartedUtc;
        public DateTime MeasurementStartedUtc;
        public int ProcessesChanged;
        public int ShieldedApplications;
        public bool MeasurementPaused;
        public bool Complete;
        public bool RolledBack;
        public OptimizationProofStage Stage;
        public OptimizationEvaluation Evaluation;
        internal DateTime NextSampleUtc;
        internal readonly List<OptimizationMeasurement> Baseline = new List<OptimizationMeasurement>();
        internal readonly List<OptimizationMeasurement> Optimized = new List<OptimizationMeasurement>();

        public string Summary
        {
            get
            {
                if (!Complete && MeasurementPaused)
                    return "Medição pausada. Volte ao " + DisplayName + " para o Neck comparar o uso real.";
                if (!Complete && Stage == OptimizationProofStage.WaitingForTarget)
                    return "Prova de ganho pronta. Volte ao " + DisplayName + "; primeiro o Neck mede o modo normal.";
                if (!Complete && Stage == OptimizationProofStage.MeasuringBaseline)
                    return "Etapa 1 de 2: medindo " + DisplayName + " no modo normal (" + Baseline.Count + "/4).";
                if (!Complete && Stage == OptimizationProofStage.WarmingOptimized)
                    return "Etapa 2 de 2: otimização reversível aplicada; preparando uma comparação justa...";
                if (!Complete && Stage == OptimizationProofStage.MeasuringOptimized)
                    return "Etapa 2 de 2: medindo com a otimização (" + Optimized.Count + "/4).";
                if (!Complete) return "Preparando a prova de ganho...";

                string evidence = Evaluation == null ? "atividade insuficiente" : Evaluation.Evidence;
                if (Evaluation != null && Evaluation.Verdict == OptimizationVerdict.Improved)
                    return "Ganho observado nesta sessão: " + evidence + ". A aceleração foi mantida por até 1 hora.";
                if (Evaluation != null && Evaluation.Verdict == OptimizationVerdict.Worse)
                    return "A otimização não ajudou nesta sessão: " + evidence + ". O Neck restaurou tudo automaticamente.";
                if (Evaluation != null && Evaluation.Verdict == OptimizationVerdict.Neutral)
                    return "Nenhum ganho relevante foi observado: " + evidence + ". O Neck restaurou tudo automaticamente.";
                return "Não foi possível comprovar ganho: " + evidence + ". O Neck restaurou tudo automaticamente.";
            }
        }
    }

    internal static class OptimizationOutcomeMonitor
    {
        private const int RequiredSamples = 4;
        private static readonly object SyncRoot = new object();
        private static OptimizationOutcome _current;
        private static ReplayPerformanceSampler _performance;

        public static bool IsPending(string processName)
        {
            lock (SyncRoot)
            {
                return _current != null && !_current.Complete &&
                       string.Equals(_current.ProcessName, processName, StringComparison.OrdinalIgnoreCase);
            }
        }

        public static void Begin(string processName, string displayName)
        {
            lock (SyncRoot)
            {
                if (_current != null && !_current.Complete && FocusModeManager.IsTarget(_current.ProcessName))
                    FocusModeManager.Stop();
                DisposeSampler();
                _performance = new ReplayPerformanceSampler();
                _current = new OptimizationOutcome
                {
                    ProcessName = processName,
                    DisplayName = string.IsNullOrWhiteSpace(displayName) ? SystemInfo.FriendlyProcessName(processName) : displayName,
                    StartedUtc = DateTime.UtcNow,
                    Stage = OptimizationProofStage.WaitingForTarget
                };
            }
        }

        public static OptimizationOutcome Refresh()
        {
            lock (SyncRoot)
            {
                if (_current == null || _current.Complete) return _current;
                DateTime now = DateTime.UtcNow;
                if (now - _current.StartedUtc > TimeSpan.FromMinutes(2))
                {
                    CompleteInconclusive("o aplicativo não permaneceu em uso pelo tempo necessário");
                    return _current;
                }

                bool foreground = TurboModeManager.IsProcessForeground(_current.ProcessName);
                _current.MeasurementPaused = !foreground;
                if (!foreground) return _current;

                if (_current.Stage == OptimizationProofStage.WaitingForTarget)
                {
                    _current.Stage = OptimizationProofStage.MeasuringBaseline;
                    _current.MeasurementStartedUtc = now;
                    _current.NextSampleUtc = now;
                }

                if (_current.Stage == OptimizationProofStage.MeasuringBaseline)
                {
                    CaptureWhenDue(_current.Baseline, now);
                    if (_current.Baseline.Count < RequiredSamples) return _current;
                    FocusModeResult mode = FocusModeManager.Start(_current.ProcessName, _current.DisplayName, 60);
                    _current.ProcessesChanged = Math.Max(mode.TurboProcessesChanged, mode.AdaptiveProcessesChanged);
                    if (!FocusModeManager.IsTarget(_current.ProcessName))
                    {
                        CompleteInconclusive("o Windows não permitiu aplicar a otimização reversível");
                        return _current;
                    }
                    _current.Stage = OptimizationProofStage.WarmingOptimized;
                    _current.NextSampleUtc = now.AddSeconds(4);
                    return _current;
                }

                if (_current.Stage == OptimizationProofStage.WarmingOptimized)
                {
                    if (!FocusModeManager.IsTarget(_current.ProcessName))
                    {
                        CompleteInconclusive("a otimização terminou antes da segunda medição");
                        return _current;
                    }
                    _current.ShieldedApplications = Math.Max(_current.ShieldedApplications, FocusShieldManager.ActiveCount);
                    if (now < _current.NextSampleUtc) return _current;
                    _current.Stage = OptimizationProofStage.MeasuringOptimized;
                    _current.NextSampleUtc = now;
                }

                if (_current.Stage == OptimizationProofStage.MeasuringOptimized)
                {
                    if (!FocusModeManager.IsTarget(_current.ProcessName))
                    {
                        CompleteInconclusive("a otimização terminou antes da comparação");
                        return _current;
                    }
                    _current.ShieldedApplications = Math.Max(_current.ShieldedApplications, FocusShieldManager.ActiveCount);
                    CaptureWhenDue(_current.Optimized, now);
                    if (_current.Optimized.Count < RequiredSamples) return _current;
                    _current.Evaluation = OptimizationGainEvaluator.Evaluate(_current.Baseline, _current.Optimized);
                    if (!OptimizationGainEvaluator.ShouldKeep(_current.Evaluation))
                    {
                        FocusModeManager.Stop();
                        _current.RolledBack = true;
                    }
                    Finish();
                }
                return _current;
            }
        }

        public static void Cancel(string processName)
        {
            lock (SyncRoot)
            {
                if (_current == null || !string.Equals(_current.ProcessName, processName, StringComparison.OrdinalIgnoreCase)) return;
                if (!_current.Complete && FocusModeManager.IsTarget(processName)) FocusModeManager.Stop();
                DisposeSampler();
                _current = null;
            }
        }

        private static void CaptureWhenDue(List<OptimizationMeasurement> destination, DateTime now)
        {
            if (now < _current.NextSampleUtc) return;
            destination.Add(Capture(_current.ProcessName));
            _current.NextSampleUtc = now.AddSeconds(2);
        }

        private static OptimizationMeasurement Capture(string processName)
        {
            ReplayPerformanceValues performance = _performance == null ? new ReplayPerformanceValues() : _performance.Capture();
            MemoryStatus memory = SystemInfo.GetMemoryStatus();
            ProcessFamilyMetrics family = ProcessFamilyInspector.GetMetrics(processName);
            WindowResponseMeasurement response = WindowResponseProbe.Measure(processName);
            return new OptimizationMeasurement
            {
                ResponseAvailable = response.Available,
                Responsive = response.Responsive,
                ResponseMilliseconds = response.ElapsedMilliseconds,
                ProcessorQueueLength = performance.ProcessorQueueLength,
                DiskLatencyMilliseconds = performance.DiskLatencyMilliseconds,
                AvailableBytes = (long)memory.AvailableBytes,
                AppMemoryBytes = family.WorkingSetBytes
            };
        }

        private static void CompleteInconclusive(string reason)
        {
            if (FocusModeManager.IsTarget(_current.ProcessName))
            {
                FocusModeManager.Stop();
                _current.RolledBack = true;
            }
            _current.Evaluation = new OptimizationEvaluation
            {
                Verdict = OptimizationVerdict.Inconclusive,
                Evidence = reason
            };
            Finish();
        }

        private static void Finish()
        {
            _current.Stage = OptimizationProofStage.Complete;
            _current.Complete = true;
            _current.MeasurementPaused = false;
            DisposeSampler();
            try { SupportDiagnostics.RecordEvent("Prova de ganho", _current.DisplayName + ": " + _current.Summary); }
            catch { }
        }

        private static void DisposeSampler()
        {
            if (_performance == null) return;
            _performance.Dispose();
            _performance = null;
        }
    }

    internal sealed class WindowResponseMeasurement
    {
        public bool Available;
        public bool Responsive;
        public double ElapsedMilliseconds;
    }

    internal static class WindowResponseProbe
    {
        private const uint WmNull = 0x0000;
        private const uint SmtoBlock = 0x0001;
        private const uint SmtoAbortIfHung = 0x0002;

        public static WindowResponseMeasurement Measure(string processName)
        {
            WindowResponseMeasurement result = new WindowResponseMeasurement();
            List<Process> processes = ProcessFamilyInspector.GetProcesses(processName);
            foreach (Process process in processes)
            {
                using (process)
                {
                    try
                    {
                        IntPtr window = process.MainWindowHandle;
                        if (window == IntPtr.Zero) continue;
                        result.Available = true;
                        Stopwatch timer = Stopwatch.StartNew();
                        UIntPtr messageResult;
                        IntPtr sent = SendMessageTimeout(window, WmNull, UIntPtr.Zero, IntPtr.Zero,
                            SmtoBlock | SmtoAbortIfHung, 150, out messageResult);
                        timer.Stop();
                        result.Responsive = sent != IntPtr.Zero;
                        result.ElapsedMilliseconds = timer.Elapsed.TotalMilliseconds;
                        return result;
                    }
                    catch { }
                }
            }
            return result;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam,
            uint flags, uint timeoutMilliseconds, out UIntPtr result);
    }
}
