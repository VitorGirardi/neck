using System;
using System.Linq;

namespace Neck
{
    internal enum BottleneckKind { None, Memory, Cpu, Disk }

    internal sealed class BottleneckAdvice
    {
        public BottleneckKind Kind;
        public HealthLevel Level;
        public string Title = "Nenhum gargalo persistente";
        public string Explanation = "O computador está fluindo normalmente agora.";
        public string ActionText = "Acelerar um aplicativo";
        public string ProcessName;
        public string DisplayName;
    }

    internal static class BottleneckAdvisor
    {
        public static BottleneckAdvice Analyze(HealthSnapshot snapshot)
        {
            BottleneckAdvice advice = new BottleneckAdvice();
            if (snapshot == null) return advice;
            advice.Level = snapshot.Level;
            ResourceProcess top = snapshot.TopProcesses.FirstOrDefault();
            bool diskCritical = snapshot.DiskTotalBytes > 0 &&
                (snapshot.DiskFreeBytes < 2L * 1024 * 1024 * 1024 || snapshot.DiskFreeBytes * 100 / snapshot.DiskTotalBytes < 5);
            bool diskWarning = snapshot.DiskTotalBytes > 0 && snapshot.DiskFreeBytes < 15L * 1024 * 1024 * 1024;

            if (diskCritical || (diskWarning && snapshot.Memory.PercentUsed < 75 && snapshot.CpuPercent < 80))
            {
                advice.Kind = BottleneckKind.Disk;
                advice.Title = diskCritical ? "O armazenamento está bloqueando o fluxo" : "O armazenamento precisa de espaço";
                advice.Explanation = "Restam " + MainForm.FormatBytes(snapshot.DiskFreeBytes) + " no disco do Windows. Comece pela limpeza segura.";
                advice.ActionText = "Liberar espaço com segurança";
                return advice;
            }

            if (snapshot.Memory.PercentUsed >= 75)
            {
                advice.Kind = BottleneckKind.Memory;
                advice.ProcessName = top == null ? null : top.ProcessName;
                advice.DisplayName = top == null ? null : top.DisplayName;
                advice.Title = top == null ? "A memória está limitando o fluxo" : top.DisplayName + " concentra o maior uso";
                advice.Explanation = snapshot.Memory.PercentUsed.ToString("0") + "% da RAM está em uso" +
                    (top == null ? "." : "; " + top.DisplayName + " ocupa cerca de " + MainForm.FormatBytes(top.MemoryBytes) + ".");
                advice.ActionText = top != null && !string.IsNullOrWhiteSpace(top.ProcessName)
                    ? "Acelerar " + top.DisplayName : "Escolher aplicativo importante";
                return advice;
            }

            if (snapshot.CpuPercent >= 80)
            {
                advice.Kind = BottleneckKind.Cpu;
                advice.Title = "A CPU está limitando a resposta";
                advice.Explanation = "A CPU chegou a " + snapshot.CpuPercent.ToString("0") + "%. Escolha o aplicativo que precisa responder primeiro.";
                advice.ActionText = "Escolher aplicativo importante";
                return advice;
            }

            advice.Kind = BottleneckKind.None;
            advice.Title = "Nenhum gargalo persistente";
            advice.Explanation = top == null
                ? "CPU, memória e armazenamento estão fluindo normalmente."
                : "O sistema está fluindo bem. " + top.DisplayName + " é o maior uso de memória agora, sem pressão crítica.";
            advice.ActionText = "Acelerar um aplicativo";
            advice.ProcessName = top == null ? null : top.ProcessName;
            advice.DisplayName = top == null ? null : top.DisplayName;
            return advice;
        }
    }

    internal enum SmartMonitorState { Flowing, Observing, Confirmed }

    internal sealed class SmartMonitorDecision
    {
        public SmartMonitorState State;
        public int NextIntervalMilliseconds;
        public bool PressureConfirmed;
        public bool RecoveryConfirmed;
        public string StatusMessage;
    }

    internal sealed class SmartGuardMonitor
    {
        private int _pressureReadings;
        private int _stableReadings;
        private bool _confirmed;

        public SmartMonitorDecision Evaluate(HealthSnapshot snapshot)
        {
            HealthLevel level = snapshot == null ? HealthLevel.Stable : snapshot.Level;
            if (level == HealthLevel.Stable)
            {
                _stableReadings++;
                _pressureReadings = Math.Max(0, _pressureReadings - 1);
            }
            else
            {
                _pressureReadings++;
                _stableReadings = 0;
            }

            bool newlyConfirmed = !_confirmed && _pressureReadings >= 3;
            if (newlyConfirmed) _confirmed = true;
            bool recovered = _confirmed && _stableReadings >= 2;
            if (recovered)
            {
                _confirmed = false;
                _pressureReadings = 0;
            }

            SmartMonitorDecision decision = new SmartMonitorDecision();
            decision.PressureConfirmed = newlyConfirmed;
            decision.RecoveryConfirmed = recovered;
            if (_confirmed)
            {
                decision.State = SmartMonitorState.Confirmed;
                decision.NextIntervalMilliseconds = level == HealthLevel.Critical ? 15000 : 30000;
                decision.StatusMessage = "Monitor inteligente: gargalo confirmado; acompanhando mais de perto.";
            }
            else if (_pressureReadings > 0)
            {
                decision.State = SmartMonitorState.Observing;
                decision.NextIntervalMilliseconds = level == HealthLevel.Critical ? 15000 : 30000;
                decision.StatusMessage = "Monitor inteligente: observando se a pressão persiste.";
            }
            else
            {
                decision.State = SmartMonitorState.Flowing;
                decision.NextIntervalMilliseconds = 60000;
                decision.StatusMessage = recovered
                    ? "Monitor inteligente: o fluxo voltou ao normal."
                    : "Monitor inteligente ativo; nenhuma pressão persistente.";
            }
            return decision;
        }
    }

    internal sealed class OptimizationOutcome
    {
        public string ProcessName;
        public string DisplayName;
        public DateTime StartedUtc;
        public DateTime MeasurementStartedUtc;
        public long AvailableBefore;
        public long AvailableAfter;
        public long AppMemoryBefore;
        public long AppMemoryAfter;
        public int ProcessesChanged;
        public int ShieldedApplications;
        public bool MeasurementPaused;
        public bool EndedBeforeMeasurement;
        public bool Complete;

        public string Summary
        {
            get
            {
                if (EndedBeforeMeasurement)
                    return "A aceleração terminou antes de completar a medição. Nenhum ganho foi estimado.";
                if (!Complete && MeasurementStartedUtc == DateTime.MinValue)
                    return "Aceleração pronta. Volte ao aplicativo para o Neck medir o resultado durante o uso real.";
                if (!Complete && MeasurementPaused)
                    return "Medição pausada. Volte ao aplicativo acelerado para continuar.";
                if (!Complete)
                    return ShieldedApplications > 0
                        ? "Escudo de Foco ativo em " + ShieldedApplications + " aplicativo(s) pesado(s) em segundo plano; medindo o resultado..."
                        : "Medindo o resultado sem interromper o aplicativo...";
                long availableDelta = AvailableAfter - AvailableBefore;
                long appDelta = AppMemoryBefore - AppMemoryAfter;
                string observed = availableDelta >= 50L * 1024 * 1024
                    ? MainForm.FormatBytes(availableDelta) + " a mais de RAM disponível"
                    : appDelta >= 50L * 1024 * 1024
                        ? MainForm.FormatBytes(appDelta) + " a menos na memória física do aplicativo"
                        : "uso de memória geral permaneceu semelhante";
                string shield = ShieldedApplications > 0
                    ? "; Escudo de Foco protegeu contra " + ShieldedApplications + " aplicativo(s) concorrente(s)"
                    : string.Empty;
                return "Resultado observado: " + observed + "; " + ProcessesChanged + " processo(s) do aplicativo configurado(s)" + shield + ".";
            }
        }
    }

    internal static class OptimizationOutcomeMonitor
    {
        private static readonly object SyncRoot = new object();
        private static OptimizationOutcome _current;

        public static void Begin(string processName, string displayName, FocusModeResult modeResult, MemoryStatus memoryBefore, long appMemoryBefore)
        {
            lock (SyncRoot)
            {
                _current = new OptimizationOutcome
                {
                    ProcessName = processName,
                    DisplayName = displayName,
                    StartedUtc = DateTime.UtcNow,
                    AvailableBefore = (long)memoryBefore.AvailableBytes,
                    AppMemoryBefore = appMemoryBefore,
                    ProcessesChanged = modeResult == null ? 0 : Math.Max(modeResult.TurboProcessesChanged, modeResult.AdaptiveProcessesChanged)
                };
            }
        }

        public static OptimizationOutcome Refresh()
        {
            lock (SyncRoot)
            {
                if (_current == null || _current.Complete) return _current;
                bool targetActive = FocusModeManager.IsTarget(_current.ProcessName);
                bool targetForeground = targetActive && TurboModeManager.IsForeground;
                if (!targetActive)
                {
                    _current.EndedBeforeMeasurement = true;
                    _current.Complete = true;
                    return _current;
                }
                if (_current.MeasurementStartedUtc == DateTime.MinValue)
                {
                    if (!targetForeground) return _current;
                    _current.MeasurementStartedUtc = DateTime.UtcNow;
                }
                _current.MeasurementPaused = !targetForeground;
                if (_current.MeasurementPaused) return _current;
                _current.ShieldedApplications = Math.Max(_current.ShieldedApplications, FocusShieldManager.ActiveCount);
                if (DateTime.UtcNow - _current.MeasurementStartedUtc < TimeSpan.FromSeconds(18)) return _current;
                MemoryStatus memory = SystemInfo.GetMemoryStatus();
                ProcessFamilyMetrics family = ProcessFamilyInspector.GetMetrics(_current.ProcessName);
                _current.AvailableAfter = (long)memory.AvailableBytes;
                _current.AppMemoryAfter = family.WorkingSetBytes;
                _current.Complete = true;
                return _current;
            }
        }

        public static void Cancel(string processName)
        {
            lock (SyncRoot)
            {
                if (_current != null && string.Equals(_current.ProcessName, processName, StringComparison.OrdinalIgnoreCase)) _current = null;
            }
        }
    }
}
