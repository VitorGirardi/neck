using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Neck
{
    internal sealed class AutopilotForm : Form
    {
        private readonly GuardSettings _settings;
        private readonly AutopilotEngine _engine;
        private readonly BaselineView _baseline;
        private AutopilotDecision _decision;
        private readonly Label _badge = new Label();
        private readonly Label _title = new Label();
        private readonly Label _explanation = new Label();
        private readonly Label _protection = new Label();
        private readonly Button _toggle = new AnimatedButton();
        private readonly Button _simulate = new AnimatedButton();
        private readonly Label _simulationResult = new Label();
        private readonly ActivityBar _simulationProgress = new ActivityBar();

        public AutopilotForm(GuardSettings settings, AutopilotEngine engine, AutopilotDecision decision, BaselineView baseline)
        {
            _settings = settings ?? new GuardSettings();
            _engine = engine ?? new AutopilotEngine();
            _decision = decision ?? _engine.GetCurrent();
            _baseline = baseline ?? new BaselineView { Profile = new BaselineProfile(), Evaluation = new BaselineEvaluation() };
            Text = "Neck Autopilot — proteção preventiva";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(880, 760);
            MinimumSize = new Size(800, 720);
            BackColor = Theme.Background;
            Font = Theme.Body;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            BuildInterface();
            RefreshStatus();
            Shown += delegate { VisualEffects.FadeIn(this); };
            FormClosed += delegate { _simulationProgress.Running = false; };
        }

        internal void ShowSimulationForTesting()
        {
            PresentSimulation(AutopilotSimulation.Run());
        }

        private void BuildInterface()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 112, BackColor = Theme.Card };
            FlowMark mark = new FlowMark { Location = new Point(28, 31), Size = new Size(54, 54) };
            header.Controls.Add(mark);
            header.Controls.Add(new Label
            {
                Text = "Neck Autopilot",
                AutoSize = true,
                Font = new Font("Segoe UI Variable Display", 24f, FontStyle.Bold),
                ForeColor = Theme.Ink,
                Location = new Point(104, 24)
            });
            header.Controls.Add(new Label
            {
                Text = "Prevê gargalos e protege o que importa antes do travamento.",
                AutoSize = true,
                Font = Theme.Body,
                ForeColor = Theme.Muted,
                Location = new Point(107, 70)
            });
            Label promise = new Label
            {
                Text = "●  Temporário e reversível",
                AutoSize = false,
                Size = new Size(230, 38),
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI Semibold", 9f, FontStyle.Regular),
                ForeColor = Theme.Green,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(ClientSize.Width - 260, 41)
            };
            header.Resize += delegate { promise.Left = header.ClientSize.Width - promise.Width - 28; };
            header.Controls.Add(promise);

            TableLayoutPanel body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 18, 24, 14),
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Theme.Background
            };
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 175));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body.Controls.Add(BuildStatusCard(), 0, 0);
            body.Controls.Add(BuildProtectionCard(), 0, 1);
            body.Controls.Add(BuildSimulationCard(), 0, 2);

            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 70, BackColor = Color.White };
            Label privacy = new Label
            {
                Text = "A previsão e as decisões ficam somente neste computador.",
                AutoSize = true,
                Font = Theme.Small,
                ForeColor = Theme.Muted,
                Location = new Point(28, 26)
            };
            Button close = MakeButton("Voltar", Theme.Blue, 126);
            close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            close.Location = new Point(footer.Width - 154, 12);
            close.Click += delegate { Close(); };
            footer.Resize += delegate { close.Left = footer.ClientSize.Width - close.Width - 28; };
            footer.Controls.Add(privacy);
            footer.Controls.Add(close);
            AcceptButton = close;
            CancelButton = close;

            Controls.Add(body);
            Controls.Add(footer);
            Controls.Add(header);
        }

        private Control BuildStatusCard()
        {
            RoundedPanel card = NewCard();
            card.Margin = new Padding(0, 0, 0, 12);
            _badge.AutoSize = false;
            _badge.Size = new Size(128, 26);
            _badge.Location = new Point(24, 22);
            _badge.TextAlign = ContentAlignment.MiddleCenter;
            _badge.Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold);
            _title.AutoSize = false;
            _title.Location = new Point(24, 58);
            _title.Size = new Size(745, 34);
            _title.Font = new Font("Segoe UI Variable Display", 18f, FontStyle.Bold);
            _title.ForeColor = Theme.Text;
            _explanation.AutoSize = false;
            _explanation.Location = new Point(26, 96);
            _explanation.Size = new Size(760, 38);
            _explanation.Font = Theme.Body;
            _explanation.ForeColor = Theme.Muted;
            card.Resize += delegate
            {
                _title.Width = Math.Max(320, card.ClientSize.Width - 50);
                _explanation.Width = Math.Max(320, card.ClientSize.Width - 52);
            };
            card.Controls.Add(_badge);
            card.Controls.Add(_title);
            card.Controls.Add(_explanation);
            return card;
        }

        private Control BuildProtectionCard()
        {
            RoundedPanel card = NewCard();
            card.Margin = new Padding(0, 0, 0, 12);
            Label title = new Label
            {
                Text = "Proteção com limites claros",
                AutoSize = true,
                Font = new Font("Segoe UI Variable Display", 16f, FontStyle.Bold),
                ForeColor = Theme.Text,
                Location = new Point(24, 14)
            };
            Label rules = new Label
            {
                Text = "✓ Confirma duas previsões consecutivas\n✓ No máximo dois aplicativos seguros em segundo plano\n✓ Restaura ao estabilizar ou após dez minutos\n✓ Nunca fecha programas, apaga arquivos ou pede administrador",
                AutoSize = false,
                Size = new Size(500, 104),
                Font = Theme.Body,
                ForeColor = Theme.Muted,
                Location = new Point(26, 57)
            };
            _protection.AutoSize = false;
            _protection.Size = new Size(245, 46);
            _protection.Location = new Point(540, 57);
            _protection.Font = Theme.Small;
            _protection.ForeColor = Theme.Muted;
            _protection.TextAlign = ContentAlignment.MiddleCenter;
            ConfigureButton(_toggle, "Ativar Autopilot", Theme.Green, 236);
            _toggle.Location = new Point(544, 112);
            _toggle.Click += async delegate { await ToggleAsync(); };
            card.Resize += delegate
            {
                _toggle.Left = card.ClientSize.Width - _toggle.Width - 24;
                _protection.Left = card.ClientSize.Width - _protection.Width - 20;
            };
            card.Controls.Add(title);
            card.Controls.Add(rules);
            card.Controls.Add(_protection);
            card.Controls.Add(_toggle);
            return card;
        }

        private Control BuildSimulationCard()
        {
            RoundedPanel card = NewCard();
            card.Margin = new Padding(0);
            Label title = new Label
            {
                Text = "Teste sem sobrecarregar o computador",
                AutoSize = true,
                Font = new Font("Segoe UI Variable Display", 15f, FontStyle.Bold),
                ForeColor = Theme.Text,
                Location = new Point(24, 20)
            };
            Label help = new Label
            {
                Text = "Simula uma tendência crescente de RAM. Nenhum aplicativo real ou configuração do Windows será alterado.",
                AutoSize = false,
                Size = new Size(510, 35),
                Font = Theme.Small,
                ForeColor = Theme.Muted,
                Location = new Point(26, 44)
            };
            ConfigureButton(_simulate, "Executar simulação", Theme.NavySoft, 205);
            _simulate.Location = new Point(24, 82);
            _simulate.Click += async delegate { await SimulateAsync(); };
            _simulationResult.AutoSize = false;
            _simulationResult.Size = new Size(520, 55);
            _simulationResult.Location = new Point(255, 75);
            _simulationResult.Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold);
            _simulationResult.ForeColor = Theme.Muted;
            _simulationResult.TextAlign = ContentAlignment.MiddleLeft;
            _simulationResult.Text = "Pronto para testar o motor preditivo.";
            _simulationProgress.Location = new Point(24, 132);
            _simulationProgress.Size = new Size(750, 6);
            card.Resize += delegate
            {
                _simulationResult.Width = Math.Max(250, card.ClientSize.Width - 280);
                _simulationProgress.Width = Math.Max(300, card.ClientSize.Width - 48);
                _simulationProgress.Top = Math.Max(125, card.ClientSize.Height - 10);
            };
            card.Controls.Add(title);
            card.Controls.Add(help);
            card.Controls.Add(_simulate);
            card.Controls.Add(_simulationResult);
            card.Controls.Add(_simulationProgress);
            return card;
        }

        private async Task ToggleAsync()
        {
            bool enabling = !_settings.AutopilotEnabled;
            if (enabling && MessageBox.Show(
                    "O Autopilot poderá reduzir temporariamente o consumo de até dois aplicativos seguros em segundo plano quando duas previsões confirmarem risco de RAM ou CPU.\n\nAplicativos continuam abertos e voltam ao normal automaticamente.",
                    "Ativar Neck Autopilot?", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;
            _toggle.Enabled = false;
            try
            {
                _settings.AutopilotEnabled = enabling;
                _settings.Save();
                if (!enabling)
                {
                    await Task.Run(delegate { return AutopilotProtectionManager.Stop(); });
                    _decision = _engine.DisableNow();
                }
                else
                {
                    bool ready = _baseline.Profile != null && _baseline.Profile.Normal != null && _baseline.Profile.Normal.IsReady;
                    _decision = new AutopilotDecision
                    {
                        State = ready ? AutopilotState.Flowing : AutopilotState.Learning,
                        Title = ready ? "Autopilot ativado" : "Autopilot aguardando o padrão",
                        Explanation = ready
                            ? "A proteção começará a observar tendências na próxima leitura, sem agir por picos isolados."
                            : "Conclua as 30 leituras válidas do Índice de Fluxo; depois a previsão começa automaticamente."
                    };
                }
                RefreshStatus();
            }
            finally { if (!IsDisposed) _toggle.Enabled = true; }
        }

        private async Task SimulateAsync()
        {
            _simulate.Enabled = false;
            _simulate.Text = "Simulando...";
            _simulationProgress.Running = true;
            _simulationResult.ForeColor = Theme.Muted;
            _simulationResult.Text = "Reproduzindo seis leituras locais em um minuto virtual...";
            try
            {
                await Task.Delay(800);
                PresentSimulation(AutopilotSimulation.Run());
            }
            finally
            {
                _simulationProgress.Running = false;
                if (!IsDisposed)
                {
                    _simulate.Enabled = true;
                    _simulate.Text = "Executar novamente";
                }
            }
        }

        private void PresentSimulation(AutopilotDecision result)
        {
            if (result != null && result.State == AutopilotState.Protecting && result.Cause == AutopilotCause.Memory)
            {
                _simulationResult.ForeColor = Theme.Green;
                _simulationResult.Text = "✓ Previsão reconhecida: tendência de memória.\n✓ Proteção simulada para 2 aplicativos; nenhum aplicativo real foi tocado.";
            }
            else
            {
                _simulationResult.ForeColor = Color.Firebrick;
                _simulationResult.Text = "A simulação não confirmou a previsão esperada. Consulte o diagnóstico antes de ativar.";
            }
        }

        private void RefreshStatus()
        {
            _badge.Text = StateLabel(_decision.State);
            Color color = StateColor(_decision.State);
            _badge.ForeColor = color;
            _badge.BackColor = VisualEffects.Blend(color, Color.White, 0.86d);
            _title.Text = _decision.Title;
            _explanation.Text = _decision.Explanation +
                (_decision.State == AutopilotState.Watching && _decision.Confidence > 0
                    ? " Confiança da tendência: " + _decision.Confidence + "%." : string.Empty);
            _toggle.Text = _settings.AutopilotEnabled ? "Desativar Autopilot" : "Ativar Autopilot";
            _toggle.BackColor = _settings.AutopilotEnabled ? Theme.NavySoft : Theme.Green;
            _protection.Text = AutopilotProtectionManager.ActiveCount > 0
                ? "Protegendo agora:\n" + AutopilotProtectionManager.ActiveSummary
                : _settings.AutopilotEnabled ? "Ativo e aguardando uma tendência confirmada." : "Desativado por padrão. Você decide quando usar.";
        }

        private static string StateLabel(AutopilotState state)
        {
            if (state == AutopilotState.Protecting) return "PROTEGENDO";
            if (state == AutopilotState.Watching) return "PREVENDO";
            if (state == AutopilotState.Learning) return "APRENDENDO";
            if (state == AutopilotState.Paused) return "EM PAUSA";
            if (state == AutopilotState.Cooling) return "RESTAURANDO";
            if (state == AutopilotState.Flowing) return "ACOMPANHANDO";
            return "DESATIVADO";
        }

        private static Color StateColor(AutopilotState state)
        {
            if (state == AutopilotState.Protecting || state == AutopilotState.Flowing) return Theme.Green;
            if (state == AutopilotState.Watching || state == AutopilotState.Cooling) return Theme.Amber;
            if (state == AutopilotState.Learning) return Theme.Blue;
            return Theme.Muted;
        }

        private static RoundedPanel NewCard()
        {
            return new RoundedPanel { Dock = DockStyle.Fill, BackColor = Color.White, OutlineColor = Theme.Border, CornerRadius = 16 };
        }

        private static void ConfigureButton(Button button, string text, Color color, int width)
        {
            button.Text = text;
            button.Size = new Size(width, 46);
            button.BackColor = color;
            button.ForeColor = Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
        }

        private static Button MakeButton(string text, Color color, int width)
        {
            Button button = new AnimatedButton();
            ConfigureButton(button, text, color, width);
            return button;
        }
    }
}
