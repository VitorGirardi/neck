using System;
using System.Linq;

namespace Neck
{
    internal enum BottleneckKind { None, Memory, Cpu, Disk }
    internal enum BottleneckActionKind { Accelerate, Cleanup, Diagnostic, Hardware }
    internal enum BottleneckSignalKind
    {
        None,
        MemoryPressure,
        CpuContention,
        DiskLatency,
        DiskSpace,
        ForegroundFreeze,
        ThermalPressure,
        MemoryObservation,
        CpuObservation
    }

    internal sealed class BottleneckAdvice
    {
        public BottleneckKind Kind;
        public HealthLevel Level;
        public string Title = "Nenhum gargalo persistente";
        public string Explanation = "O computador está fluindo normalmente agora.";
        public string ActionText = "Acelerar um aplicativo";
        public string ProcessName;
        public string DisplayName;
        public BottleneckActionKind ActionKind = BottleneckActionKind.Accelerate;
        public BottleneckSignalKind SignalKind;
        public int Confidence;
    }

    internal static class BottleneckAdvisor
    {
        public static BottleneckAdvice Analyze(HealthSnapshot snapshot)
        {
            return Analyze(snapshot, null);
        }

        public static BottleneckAdvice Analyze(HealthSnapshot snapshot, ReplaySample signals)
        {
            BottleneckAdvice advice = new BottleneckAdvice();
            if (snapshot == null) return advice;
            advice.Level = snapshot.Level;
            ResourceProcess top = snapshot.TopProcesses.FirstOrDefault(item => EfficiencyModeManager.CanTarget(item.ProcessName));
            bool diskCritical = snapshot.DiskTotalBytes > 0 &&
                (snapshot.DiskFreeBytes < 2L * 1024 * 1024 * 1024 || snapshot.DiskFreeBytes * 100 / snapshot.DiskTotalBytes < 5);
            bool diskWarning = snapshot.DiskTotalBytes > 0 && snapshot.DiskFreeBytes < 15L * 1024 * 1024 * 1024;

            if (diskCritical)
            {
                advice.Kind = BottleneckKind.Disk;
                advice.SignalKind = BottleneckSignalKind.DiskSpace;
                advice.Title = "O armazenamento está bloqueando o fluxo";
                advice.Explanation = "Restam " + MainForm.FormatBytes(snapshot.DiskFreeBytes) + " no disco do Windows. Comece pela limpeza segura.";
                advice.ActionText = "Liberar espaço com segurança";
                advice.ActionKind = BottleneckActionKind.Cleanup;
                advice.Confidence = 98;
                return advice;
            }

            ReplayAssessment measured = IsFresh(signals) ? ReplayClassifier.Analyze(signals) : new ReplayAssessment();
            if (measured.Cause == ReplayCause.MemoryPressure)
            {
                advice.Kind = BottleneckKind.Memory;
                advice.SignalKind = BottleneckSignalKind.MemoryPressure;
                ResourceProcess measuredTop = FindTarget(snapshot, measured.ProcessName) ?? top;
                advice.ProcessName = measuredTop == null ? null : measuredTop.ProcessName;
                advice.DisplayName = measuredTop == null ? null : measuredTop.DisplayName;
                advice.Title = measuredTop == null ? "A memória está limitando o fluxo" : measuredTop.DisplayName + " concentra o maior uso";
                advice.Explanation = signals.MemoryPercent.ToString("0") + "% da RAM, commit em " +
                    signals.CommitPercent.ToString("0") + "% e " + MainForm.FormatBytes(signals.AvailableBytes) + " disponíveis confirmam pressão real.";
                advice.ActionText = measuredTop != null && !string.IsNullOrWhiteSpace(measuredTop.ProcessName)
                    ? "Acelerar " + measuredTop.DisplayName : "Escolher aplicativo importante";
                advice.Confidence = measured.Score;
                return advice;
            }

            if (measured.Cause == ReplayCause.CpuContention)
            {
                advice.Kind = BottleneckKind.Cpu;
                advice.SignalKind = BottleneckSignalKind.CpuContention;
                advice.ProcessName = EfficiencyModeManager.CanTarget(measured.ProcessName) ? measured.ProcessName : null;
                advice.DisplayName = string.IsNullOrWhiteSpace(advice.ProcessName) ? null : SystemInfo.FriendlyProcessName(advice.ProcessName);
                advice.Title = "A CPU está limitando a resposta";
                advice.Explanation = "CPU em " + signals.CpuPercent.ToString("0") + "% com fila de " +
                    signals.ProcessorQueueLength.ToString("0.0") + " confirma disputa por tempo de processamento.";
                advice.ActionText = string.IsNullOrWhiteSpace(advice.DisplayName)
                    ? "Escolher aplicativo importante" : "Acelerar " + advice.DisplayName;
                advice.Confidence = measured.Score;
                return advice;
            }

            if (measured.Cause == ReplayCause.DiskStall)
            {
                advice.Kind = BottleneckKind.Disk;
                advice.SignalKind = BottleneckSignalKind.DiskLatency;
                advice.Title = "O armazenamento está demorando para responder";
                advice.Explanation = "Latência de " + signals.DiskLatencyMilliseconds.ToString("0") + " ms e fila de " +
                    signals.DiskQueueLength.ToString("0.0") + " indicam espera real; apagar arquivos não resolveria esse pico.";
                advice.ActionText = "Entender a lentidão do disco";
                advice.ActionKind = BottleneckActionKind.Diagnostic;
                advice.Confidence = measured.Score;
                return advice;
            }

            if (measured.Cause == ReplayCause.ForegroundFreeze)
            {
                advice.Kind = BottleneckKind.Cpu;
                advice.SignalKind = BottleneckSignalKind.ForegroundFreeze;
                advice.ProcessName = EfficiencyModeManager.CanTarget(measured.ProcessName) ? measured.ProcessName : null;
                advice.DisplayName = string.IsNullOrWhiteSpace(advice.ProcessName) ? null : SystemInfo.FriendlyProcessName(advice.ProcessName);
                advice.Title = string.IsNullOrWhiteSpace(advice.DisplayName)
                    ? "O aplicativo em uso parou de responder" : advice.DisplayName + " parou de responder";
                advice.Explanation = "O Neck confirmou a falta de resposta da janela e pode aliviar os concorrentes sem fechá-la.";
                advice.ActionText = "Aliviar concorrentes agora";
                advice.Confidence = measured.Score;
                return advice;
            }

            if (measured.Cause == ReplayCause.ThermalPressure)
            {
                advice.Kind = BottleneckKind.Cpu;
                advice.SignalKind = BottleneckSignalKind.ThermalPressure;
                advice.Title = "A temperatura pode estar reduzindo o desempenho";
                advice.Explanation = "Um sensor confiável chegou a " + signals.TemperatureCelsius.ToString("0") + " °C; aumentar prioridade poderia piorar o aquecimento.";
                advice.ActionText = "Ver temperaturas e hardware";
                advice.ActionKind = BottleneckActionKind.Hardware;
                advice.Confidence = measured.Score;
                return advice;
            }

            if (diskWarning)
            {
                advice.Kind = BottleneckKind.Disk;
                advice.SignalKind = BottleneckSignalKind.DiskSpace;
                advice.Title = "O armazenamento precisa de espaço";
                advice.Explanation = "Restam " + MainForm.FormatBytes(snapshot.DiskFreeBytes) + " no disco do Windows. Comece pela limpeza segura.";
                advice.ActionText = "Liberar espaço com segurança";
                advice.ActionKind = BottleneckActionKind.Cleanup;
                advice.Confidence = 82;
                return advice;
            }

            if (!IsFresh(signals) && snapshot.Memory.PercentUsed >= 75)
            {
                advice.Kind = BottleneckKind.Memory;
                advice.SignalKind = BottleneckSignalKind.MemoryObservation;
                advice.ProcessName = top == null ? null : top.ProcessName;
                advice.DisplayName = top == null ? null : top.DisplayName;
                advice.Title = top == null ? "A memória merece observação" : top.DisplayName + " concentra o maior uso";
                advice.Explanation = snapshot.Memory.PercentUsed.ToString("0") + "% da RAM está em uso; o Neck está coletando paginação e commit antes de confirmar um gargalo.";
                advice.ActionText = top != null && !string.IsNullOrWhiteSpace(top.ProcessName)
                    ? "Acelerar " + top.DisplayName : "Escolher aplicativo importante";
                advice.Confidence = 45;
                return advice;
            }

            if (!IsFresh(signals) && snapshot.CpuPercent >= 80)
            {
                advice.Kind = BottleneckKind.Cpu;
                advice.SignalKind = BottleneckSignalKind.CpuObservation;
                advice.Title = "A CPU merece observação";
                advice.Explanation = "A CPU chegou a " + snapshot.CpuPercent.ToString("0") + "%; o Neck está verificando se existe fila persistente.";
                advice.ActionText = "Escolher aplicativo importante";
                advice.Confidence = 45;
                return advice;
            }

            advice.Kind = BottleneckKind.None;
            advice.Title = "Nenhum gargalo persistente";
            advice.Explanation = IsFresh(signals)
                ? "O Neck cruzou RAM, commit, paginação, fila da CPU e resposta do disco; nenhum gargalo real foi confirmado."
                : top == null
                    ? "CPU, memória e armazenamento estão fluindo normalmente."
                    : "O sistema está fluindo bem. " + top.DisplayName + " é o maior uso de memória agora, sem pressão crítica.";
            advice.ActionText = "Acelerar um aplicativo";
            advice.ProcessName = top == null ? null : top.ProcessName;
            advice.DisplayName = top == null ? null : top.DisplayName;
            advice.Confidence = IsFresh(signals) ? 85 : 60;
            return advice;
        }

        private static bool IsFresh(ReplaySample sample)
        {
            if (sample == null || sample.TimestampUtc == DateTime.MinValue) return false;
            TimeSpan age = DateTime.UtcNow - sample.TimestampUtc;
            return age >= TimeSpan.FromSeconds(-5) && age <= TimeSpan.FromSeconds(30);
        }

        private static ResourceProcess FindTarget(HealthSnapshot snapshot, string processName)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(processName) || !EfficiencyModeManager.CanTarget(processName)) return null;
            return snapshot.TopProcesses.FirstOrDefault(item => string.Equals(item.ProcessName, processName, StringComparison.OrdinalIgnoreCase));
        }
    }

    internal static class FlowHealthRefiner
    {
        public static ReplayAssessment Apply(HealthSnapshot health, ReplaySample sample)
        {
            ReplayAssessment assessment = ReplayClassifier.Analyze(sample);
            if (health == null || sample == null) return assessment;
            bool lowDisk = health.DiskTotalBytes > 0 &&
                (health.DiskFreeBytes < 15L * 1024 * 1024 * 1024 || health.DiskFreeBytes * 100 / health.DiskTotalBytes < 5);

            if (assessment.Cause == ReplayCause.None && !lowDisk)
            {
                health.Level = HealthLevel.Stable;
                health.Score = Math.Max(85, health.Score);
                health.Title = "Nenhum gargalo real agora";
                health.Summary = sample.MemoryPercent.ToString("0") + "% da RAM está em uso, mas ainda há " +
                    MainForm.FormatBytes(sample.AvailableBytes) + " disponíveis e nenhuma fila persistente de CPU ou disco.";
                return assessment;
            }

            if (assessment.Cause == ReplayCause.None) return assessment;
            health.Level = assessment.Score >= 85 ? HealthLevel.Critical : HealthLevel.Warning;
            health.Score = Math.Min(health.Score, Math.Max(10, 100 - assessment.Score / 2));
            if (assessment.Cause == ReplayCause.MemoryPressure)
            {
                health.Title = "Pressão real de memória";
                health.Summary = "RAM, commit e paginação indicam que a memória perdeu folga de verdade.";
            }
            else if (assessment.Cause == ReplayCause.CpuContention)
            {
                health.Title = "Disputa real pela CPU";
                health.Summary = "A ocupação e a fila da CPU confirmam espera por processamento.";
            }
            else if (assessment.Cause == ReplayCause.DiskStall)
            {
                health.Title = "Armazenamento com resposta lenta";
                health.Summary = "A latência e a fila do disco subiram juntas; falta de espaço não é necessariamente a causa.";
            }
            else if (assessment.Cause == ReplayCause.ForegroundFreeze)
            {
                health.Title = "Aplicativo sem responder";
                health.Summary = "A janela em uso parou de responder; o Neck preservou o contexto antes de sugerir uma ação.";
            }
            else if (assessment.Cause == ReplayCause.ThermalPressure)
            {
                health.Title = "Desempenho limitado pela temperatura";
                health.Summary = "O calor pode estar reduzindo automaticamente a frequência do hardware.";
            }
            return assessment;
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

}
