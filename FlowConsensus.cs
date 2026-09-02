using System;

namespace Neck
{
    internal enum FlowConsensusState
    {
        Learning,
        Stable,
        BaselineShift,
        Observing,
        Confirmed,
        Recovering
    }

    internal sealed class FlowConsensusDecision
    {
        public FlowConsensusState State;
        public HealthLevel EffectiveLevel = HealthLevel.Stable;
        public BottleneckAdvice Advice = new BottleneckAdvice();
        public string Badge = "●  Lendo o fluxo";
        public string HeroTitle = "Entendendo seu computador";
        public string Summary = "O Neck está cruzando os sinais do computador.";
        public string InsightTitle = "Analisando o fluxo";
        public string InsightExplanation = "A recomendação aparecerá após leituras consistentes.";
        public string EvidenceText = "Coletando evidências locais";
        public string PrimaryActionText = "Acelerar um aplicativo";
        public bool CanAct = true;
        public int Confidence;
        public int ConsecutiveReadings;
        public int RequiredReadings;
        public DateTime EvidenceUtc;
    }

    internal sealed class FlowConsensusEngine
    {
        internal const int PressureReadingsRequired = 3;
        internal const int RecoveryReadingsRequired = 2;

        private DateTime _lastEvidenceUtc = DateTime.MinValue;
        private BottleneckSignalKind _candidateSignal;
        private int _pressureReadings;
        private int _stableReadings;
        private BottleneckAdvice _confirmedAdvice;

        public FlowConsensusDecision Evaluate(HealthSnapshot snapshot, ReplaySample sample, BaselineEvaluation baseline)
        {
            BottleneckAdvice current = BottleneckAdvisor.Analyze(snapshot, sample);
            bool freshEvidence = IsNewEvidence(sample);
            DateTime evidenceUtc = sample == null || sample.TimestampUtc == DateTime.MinValue
                ? DateTime.UtcNow : sample.TimestampUtc;

            if (_confirmedAdvice != null)
            {
                if (current.SignalKind == BottleneckSignalKind.None)
                {
                    if (freshEvidence) _stableReadings++;
                    if (_stableReadings >= RecoveryReadingsRequired)
                    {
                        _confirmedAdvice = null;
                        _candidateSignal = BottleneckSignalKind.None;
                        _pressureReadings = 0;
                        _stableReadings = 0;
                        return BuildNoPressure(current, baseline, evidenceUtc);
                    }
                    return BuildRecovering(_confirmedAdvice, evidenceUtc);
                }

                _stableReadings = 0;
                if (current.SignalKind == _confirmedAdvice.SignalKind)
                    _confirmedAdvice = current;
                return BuildConfirmed(_confirmedAdvice, evidenceUtc);
            }

            if (current.SignalKind != BottleneckSignalKind.None)
            {
                if (freshEvidence)
                {
                    if (_candidateSignal == current.SignalKind) _pressureReadings++;
                    else
                    {
                        _candidateSignal = current.SignalKind;
                        _pressureReadings = 1;
                    }
                }

                if (_pressureReadings >= PressureReadingsRequired)
                {
                    _confirmedAdvice = current;
                    _stableReadings = 0;
                    return BuildConfirmed(current, evidenceUtc);
                }
                return BuildObserving(current, evidenceUtc);
            }

            if (freshEvidence)
            {
                _candidateSignal = BottleneckSignalKind.None;
                _pressureReadings = 0;
            }
            return BuildNoPressure(current, baseline, evidenceUtc);
        }

        private bool IsNewEvidence(ReplaySample sample)
        {
            if (sample == null || sample.TimestampUtc == DateTime.MinValue || sample.TimestampUtc <= _lastEvidenceUtc)
                return false;
            _lastEvidenceUtc = sample.TimestampUtc;
            return true;
        }

        private FlowConsensusDecision BuildNoPressure(BottleneckAdvice advice, BaselineEvaluation baseline, DateTime evidenceUtc)
        {
            if (baseline != null && baseline.State == BaselineState.Personalized && baseline.Score < 85)
            {
                int confidence = baseline.Score < 60 ? 84 : 76;
                return new FlowConsensusDecision
                {
                    State = FlowConsensusState.BaselineShift,
                    EffectiveLevel = HealthLevel.Stable,
                    Advice = advice,
                    Badge = "●  Ritmo diferente",
                    HeroTitle = "Sem gargalo, fora do ritmo habitual",
                    Summary = "Nenhum gargalo absoluto foi confirmado. " + baseline.Explanation,
                    InsightTitle = "Seu padrão mudou nesta leitura",
                    InsightExplanation = "O padrão local mudou, mas não há um gargalo absoluto.",
                    EvidenceText = Evidence(confidence, evidenceUtc, "padrão local + atual"),
                    PrimaryActionText = advice.ActionText,
                    Confidence = confidence,
                    EvidenceUtc = evidenceUtc
                };
            }

            bool learning = baseline == null || baseline.State == BaselineState.Learning;
            int stableConfidence = Math.Max(60, advice.Confidence);
            return new FlowConsensusDecision
            {
                State = learning ? FlowConsensusState.Learning : FlowConsensusState.Stable,
                EffectiveLevel = HealthLevel.Stable,
                Advice = advice,
                Badge = learning ? "●  Aprendendo seu ritmo" : "●  Fluxo livre",
                HeroTitle = learning ? "Tudo flui enquanto o Neck aprende" : "Tudo está passando bem",
                Summary = advice.Explanation,
                InsightTitle = learning ? "Formando seu padrão local" : advice.Title,
                InsightExplanation = learning && baseline != null ? baseline.Explanation :
                    "RAM, CPU e disco não confirmam espera persistente.",
                EvidenceText = Evidence(stableConfidence, evidenceUtc, "RAM + CPU + disco"),
                PrimaryActionText = advice.ActionText,
                Confidence = stableConfidence,
                EvidenceUtc = evidenceUtc
            };
        }

        private FlowConsensusDecision BuildObserving(BottleneckAdvice advice, DateTime evidenceUtc)
        {
            int reading = Math.Max(1, _pressureReadings);
            int confidence = Math.Min(advice.Confidence, 45 + reading * 18);
            return new FlowConsensusDecision
            {
                State = FlowConsensusState.Observing,
                EffectiveLevel = HealthLevel.Warning,
                Advice = advice,
                Badge = "●  Sinal em observação",
                HeroTitle = "Confirmando uma possível pressão",
                Summary = "Ainda não é um gargalo confirmado. " + advice.Explanation,
                InsightTitle = "Confirmando antes de recomendar",
                InsightExplanation = "O Neck espera sinais consistentes antes de indicar uma ação.",
                EvidenceText = Evidence(confidence, evidenceUtc, reading + "/" + PressureReadingsRequired + " leituras"),
                PrimaryActionText = "Aguardando novas leituras",
                CanAct = false,
                Confidence = confidence,
                ConsecutiveReadings = reading,
                RequiredReadings = PressureReadingsRequired,
                EvidenceUtc = evidenceUtc
            };
        }

        private static FlowConsensusDecision BuildConfirmed(BottleneckAdvice advice, DateTime evidenceUtc)
        {
            int confidence = Math.Max(85, advice.Confidence);
            return new FlowConsensusDecision
            {
                State = FlowConsensusState.Confirmed,
                EffectiveLevel = confidence >= 92 ? HealthLevel.Critical : HealthLevel.Warning,
                Advice = advice,
                Badge = confidence >= 92 ? "●  Gargalo confirmado" : "●  Pressão confirmada",
                HeroTitle = ConfirmedTitle(advice),
                Summary = advice.Explanation,
                InsightTitle = "Ação baseada em evidências",
                InsightExplanation = "Sinais persistentes apontam esta ação como a mais útil agora.",
                EvidenceText = Evidence(confidence, evidenceUtc, PressureReadingsRequired + "/" + PressureReadingsRequired + " leituras"),
                PrimaryActionText = advice.ActionText,
                Confidence = confidence,
                ConsecutiveReadings = PressureReadingsRequired,
                RequiredReadings = PressureReadingsRequired,
                EvidenceUtc = evidenceUtc
            };
        }

        private static FlowConsensusDecision BuildRecovering(BottleneckAdvice confirmed, DateTime evidenceUtc)
        {
            return new FlowConsensusDecision
            {
                State = FlowConsensusState.Recovering,
                EffectiveLevel = HealthLevel.Warning,
                Advice = confirmed,
                Badge = "●  Confirmando recuperação",
                HeroTitle = "O fluxo começou a se soltar",
                Summary = "A pressão sumiu nesta leitura; falta mais uma leitura estável para confirmar a recuperação.",
                InsightTitle = "Recuperação em observação",
                InsightExplanation = "O Neck evita anunciar melhora por causa de uma oscilação isolada.",
                EvidenceText = Evidence(72, evidenceUtc, "1/" + RecoveryReadingsRequired + " leituras estáveis"),
                PrimaryActionText = "Confirmando melhora",
                CanAct = false,
                Confidence = 72,
                ConsecutiveReadings = 1,
                RequiredReadings = RecoveryReadingsRequired,
                EvidenceUtc = evidenceUtc
            };
        }

        private static string Evidence(int confidence, DateTime evidenceUtc, string evidence)
        {
            string level = confidence >= 85 ? "Confiança alta" : confidence >= 65 ? "Confiança moderada" : "Confiança inicial";
            string time = evidenceUtc == DateTime.MinValue ? "agora" : evidenceUtc.ToLocalTime().ToString("HH:mm");
            return level + " • " + evidence + " • " + time;
        }

        private static string ConfirmedTitle(BottleneckAdvice advice)
        {
            if (advice == null) return "Gargalo confirmado";
            if (advice.SignalKind == BottleneckSignalKind.DiskLatency) return "O armazenamento está com fila";
            if (advice.SignalKind == BottleneckSignalKind.DiskSpace) return "O disco está ficando sem espaço";
            if (advice.SignalKind == BottleneckSignalKind.ThermalPressure) return "O calor está limitando o desempenho";
            return advice.Title;
        }
    }
}
