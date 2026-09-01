using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Neck
{
    internal enum PlanActionKind
    {
        None,
        Sos,
        Clean,
        Startup,
        WindowsUpdate,
        Diagnostic
    }

    internal sealed class NeckPlanAction
    {
        public int Priority;
        public string Title;
        public string Reason;
        public string ButtonText;
        public PlanActionKind Kind;
    }

    internal sealed class PersonalPlan
    {
        public DateTime GeneratedAt;
        public HealthSnapshot Health;
        public ScanResult Cleanup;
        public List<StartupEntry> StartupEntries = new List<StartupEntry>();
        public bool RestartPending;
        public string Title;
        public string Summary;
        public List<NeckPlanAction> Actions = new List<NeckPlanAction>();
    }

    internal static class PersonalPlanAnalyzer
    {
        public static PersonalPlan Build()
        {
            HealthSnapshot health = SystemInfo.GetHealthSnapshot();
            ScanResult cleanup = Cleaner.Analyze();
            List<StartupEntry> startup = StartupAnalyzer.Analyze();
            bool restartPending = SystemInfo.IsRestartPending();
            return BuildFromInputs(health, cleanup, startup, restartPending);
        }

        internal static PersonalPlan BuildFromInputs(HealthSnapshot health, ScanResult cleanup, IList<StartupEntry> startup, bool restartPending)
        {
            health = health ?? new HealthSnapshot();
            cleanup = cleanup ?? new ScanResult();
            List<StartupEntry> entries = (startup ?? new List<StartupEntry>()).ToList();
            PersonalPlan plan = new PersonalPlan
            {
                GeneratedAt = DateTime.Now,
                Health = health,
                Cleanup = cleanup,
                StartupEntries = entries,
                RestartPending = restartPending
            };

            List<NeckPlanAction> candidates = new List<NeckPlanAction>();
            ResourceProcess top = health.TopProcesses.FirstOrDefault();
            bool heavyProcess = top != null && top.MemoryBytes >= 3L * 1024 * 1024 * 1024;
            if (health.Memory.PercentUsed >= 75 || heavyProcess)
            {
                int priority = health.Memory.PercentUsed >= 90 ? 100 : health.Memory.PercentUsed >= 80 ? 90 : heavyProcess ? 78 : 75;
                string processText = top == null ? "os aplicativos abertos" : top.DisplayName + " (aprox. " + MainForm.FormatBytes(top.MemoryBytes) + ")";
                candidates.Add(new NeckPlanAction
                {
                    Priority = priority,
                    Title = "Aliviar a memória antes de continuar",
                    Reason = "A RAM está em " + health.Memory.PercentUsed.ToString("0") + "% e " + processText + " lidera o consumo. Revise os aplicativos abertos antes de limpar arquivos.",
                    ButtonText = "Acelerar aplicativo",
                    Kind = PlanActionKind.Sos
                });
            }

            bool diskCritical = health.DiskTotalBytes > 0 &&
                (health.DiskFreeBytes < 2L * 1024 * 1024 * 1024 || health.DiskFreeBytes * 100 / health.DiskTotalBytes < 5);
            bool diskLow = health.DiskTotalBytes > 0 && health.DiskFreeBytes < 15L * 1024 * 1024 * 1024;
            if (diskLow || cleanup.TotalBytes >= 256L * 1024 * 1024)
            {
                string reason = diskLow
                    ? "O disco do Windows tem apenas " + MainForm.FormatBytes(health.DiskFreeBytes) + " livres. A limpeza segura encontrou " + MainForm.FormatBytes(cleanup.TotalBytes) + " em temporários conhecidos."
                    : "A análise encontrou " + MainForm.FormatBytes(cleanup.TotalBytes) + " em temporários antigos e relatórios de erro que podem ser removidos com segurança.";
                candidates.Add(new NeckPlanAction
                {
                    Priority = diskCritical ? 96 : diskLow ? 84 : cleanup.TotalBytes >= 1024L * 1024 * 1024 ? 70 : 58,
                    Title = diskLow ? "Recuperar espaço no disco" : "Remover o acúmulo seguro",
                    Reason = reason,
                    ButtonText = "Fazer limpeza segura",
                    Kind = PlanActionKind.Clean
                });
            }

            List<StartupEntry> optional = entries
                .Where(item => item.Enabled && item.Recommendation == "Pode revisar")
                .ToList();
            if (optional.Count > 0)
            {
                string names = string.Join(", ", optional.Take(3).Select(item => item.Name).ToArray());
                if (optional.Count > 3) names += " e mais " + (optional.Count - 3);
                candidates.Add(new NeckPlanAction
                {
                    Priority = optional.Count >= 5 ? 76 : optional.Count >= 3 ? 66 : 55,
                    Title = "Enxugar a inicialização",
                    Reason = optional.Count + " aplicativo(s) opcional(is) iniciam com o Windows: " + names + ". Revise apenas os que você não precisa imediatamente.",
                    ButtonText = "Abrir Neck Boot",
                    Kind = PlanActionKind.Startup
                });
            }

            if (restartPending)
            {
                candidates.Add(new NeckPlanAction
                {
                    Priority = 82,
                    Title = "Concluir a atualização do Windows",
                    Reason = "O Windows indica uma reinicialização pendente. Salve seu trabalho e reinicie em um momento conveniente para concluir a instalação.",
                    ButtonText = "Abrir Windows Update",
                    Kind = PlanActionKind.WindowsUpdate
                });
            }

            candidates.Add(new NeckPlanAction
            {
                Priority = health.Level == HealthLevel.Stable ? 42 : 50,
                Title = "Entender o estado atual",
                Reason = health.Summary + " O diagnóstico detalhado mostra a pontuação e os maiores consumidores sem fazer alterações.",
                ButtonText = "Ver diagnóstico",
                Kind = PlanActionKind.Diagnostic
            });
            candidates.Add(new NeckPlanAction
            {
                Priority = 28,
                Title = "Manter a limpeza preventiva",
                Reason = cleanup.TotalBytes > 0
                    ? "Há " + MainForm.FormatBytes(cleanup.TotalBytes) + " disponíveis para limpeza segura. Não é urgente, mas pode entrar na próxima revisão."
                    : "Não há acúmulo relevante agora. Uma revisão mensal é suficiente; não é necessário limpar todos os dias.",
                ButtonText = "Revisar limpeza",
                Kind = PlanActionKind.Clean
            });
            candidates.Add(new NeckPlanAction
            {
                Priority = 22,
                Title = "Conferir o que acompanha o Windows",
                Reason = "Uma revisão ocasional da inicialização ajuda a evitar acúmulo de aplicativos em segundo plano. O Neck não desativa nada sozinho.",
                ButtonText = "Abrir Neck Boot",
                Kind = PlanActionKind.Startup
            });

            plan.Actions = candidates
                .GroupBy(item => item.Kind)
                .Select(group => group.OrderByDescending(item => item.Priority).First())
                .OrderByDescending(item => item.Priority)
                .Take(3)
                .ToList();

            while (plan.Actions.Count < 3)
            {
                plan.Actions.Add(new NeckPlanAction
                {
                    Priority = 10,
                    Title = "Nenhuma ação adicional necessária",
                    Reason = "Os sinais analisados estão dentro de uma faixa segura. Continue usando o computador normalmente.",
                    ButtonText = "Ver diagnóstico",
                    Kind = PlanActionKind.Diagnostic
                });
            }

            NeckPlanAction first = plan.Actions.First();
            if (first.Priority >= 90)
            {
                plan.Title = "Há uma prioridade clara agora";
                plan.Summary = "Comece pela primeira ação e meça o resultado antes de fazer outras mudanças.";
            }
            else if (first.Priority >= 60)
            {
                plan.Title = "Seu computador merece alguns ajustes";
                plan.Summary = "Estas são as três ações com melhor relação entre benefício e segurança neste momento.";
            }
            else
            {
                plan.Title = "O computador está razoavelmente equilibrado";
                plan.Summary = "Não há urgência. O plano prioriza prevenção e decisões reversíveis.";
            }
            return plan;
        }
    }

    internal sealed class PersonalPlanForm : Form
    {
        public PlanActionKind SelectedAction { get; private set; }

        public PersonalPlanForm(PersonalPlan plan)
        {
            if (plan == null) throw new ArgumentNullException("plan");
            Text = "Meu Plano Neck";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(920, 720);
            MinimumSize = new Size(840, 660);
            BackColor = Theme.Background;
            Font = Theme.Body;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            BuildInterface(plan);
        }

        private void BuildInterface(PersonalPlan plan)
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 116, BackColor = Theme.Card };
            header.Controls.Add(new Label { Text = "Meu Plano Neck", AutoSize = true, Font = new Font("Segoe UI Variable Display", 24f, FontStyle.Bold), ForeColor = Theme.Ink, Location = new Point(30, 18) });
            header.Controls.Add(new Label
            {
                Text = plan.Title + " — " + plan.Summary,
                AutoSize = false,
                Size = new Size(700, 46),
                Font = Theme.Body,
                ForeColor = Theme.Muted,
                Location = new Point(33, 66)
            });
            RoundedPanel score = new RoundedPanel { Size = new Size(92, 72), BackColor = Theme.NavySoft, OutlineColor = Theme.NavySoft, CornerRadius = 14, Location = new Point(790, 25), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            score.Controls.Add(new Label { Text = plan.Health.Score.ToString(), AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI Semibold", 24f, FontStyle.Bold), ForeColor = Color.White });
            score.Controls.Add(new Label { Text = "SAÚDE", AutoSize = false, Dock = DockStyle.Bottom, Height = 20, TextAlign = ContentAlignment.TopCenter, Font = new Font("Segoe UI Semibold", 7f, FontStyle.Bold), ForeColor = Color.FromArgb(190, 203, 222) });
            header.Controls.Add(score);
            header.Resize += delegate { score.Left = header.ClientSize.Width - score.Width - 28; };

            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 72, BackColor = Color.White, Padding = new Padding(26, 14, 26, 13) };
            Label safety = new Label { Text = "✓ Nenhuma ação é executada automaticamente", AutoSize = true, Font = Theme.Small, ForeColor = Theme.Green, Location = new Point(27, 27) };
            Button close = MakeButton("Fechar", Theme.NavySoft, 108);
            close.Dock = DockStyle.Right;
            close.DialogResult = DialogResult.Cancel;
            footer.Controls.Add(safety);
            footer.Controls.Add(close);
            CancelButton = close;

            FlowLayoutPanel content = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(26, 18, 26, 18),
                BackColor = Theme.Background
            };
            for (int i = 0; i < plan.Actions.Count; i++) content.Controls.Add(BuildActionCard(plan.Actions[i], i + 1));

            Controls.Add(content);
            Controls.Add(footer);
            Controls.Add(header);
        }

        private Control BuildActionCard(NeckPlanAction action, int index)
        {
            RoundedPanel card = new RoundedPanel
            {
                Size = new Size(850, 146),
                BackColor = Color.White,
                OutlineColor = Theme.Border,
                CornerRadius = 16,
                Margin = new Padding(0, 0, 0, 12)
            };
            Color accent = index == 1 ? Theme.Blue : index == 2 ? Theme.Green : Theme.NavySoft;
            Label number = new Label
            {
                Text = index.ToString(),
                AutoSize = false,
                Size = new Size(42, 42),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = accent,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 16f, FontStyle.Bold),
                Location = new Point(20, 20)
            };
            Label urgency = new Label
            {
                Text = index == 1 ? "FAÇA PRIMEIRO" : action.Priority >= 60 ? "PRÓXIMO PASSO" : "PREVENTIVO",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold),
                ForeColor = accent,
                Location = new Point(78, 17)
            };
            Label title = new Label { Text = action.Title, AutoSize = true, Font = Theme.Heading, ForeColor = Theme.Text, Location = new Point(76, 38) };
            Label reason = new Label
            {
                Text = action.Reason,
                AutoSize = false,
                Size = new Size(590, 61),
                Font = Theme.Small,
                ForeColor = Theme.Muted,
                Location = new Point(78, 70)
            };
            Button execute = MakeButton(action.ButtonText, accent, 158);
            execute.Location = new Point(672, 51);
            execute.Click += delegate
            {
                SelectedAction = action.Kind;
                DialogResult = DialogResult.OK;
                Close();
            };
            card.Controls.Add(number);
            card.Controls.Add(urgency);
            card.Controls.Add(title);
            card.Controls.Add(reason);
            card.Controls.Add(execute);
            return card;
        }

        private static Button MakeButton(string text, Color color, int width)
        {
            Button button = new Button
            {
                Text = text,
                Size = new Size(width, 44),
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }
    }
}
