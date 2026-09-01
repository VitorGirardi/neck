using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace Neck
{
    internal sealed class BaselineForm : Form
    {
        private readonly BaselineView _view;

        public BaselineForm(BaselineView view)
        {
            _view = view ?? new BaselineView { Profile = new BaselineProfile(), Evaluation = new BaselineEvaluation() };
            if (_view.Profile == null) _view.Profile = new BaselineProfile();
            if (_view.Evaluation == null) _view.Evaluation = new BaselineEvaluation();
            Text = "Meu padrão — Neck";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(880, 680);
            MinimumSize = new Size(800, 620);
            BackColor = Theme.Background;
            Font = Theme.Body;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            BuildInterface();
            Shown += delegate { VisualEffects.FadeIn(this); };
        }

        private void BuildInterface()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 118, BackColor = Theme.Navy };
            FlowMark mark = new FlowMark { Location = new Point(28, 30), Size = new Size(54, 54) };
            Label title = new Label
            {
                Text = "Meu padrão Neck",
                AutoSize = true,
                Font = new Font("Bahnschrift", 24f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(103, 25)
            };
            Label subtitle = new Label
            {
                Text = "O padrão normal deste PC, aprendido apenas no dispositivo.",
                AutoSize = true,
                Font = Theme.Body,
                ForeColor = Color.FromArgb(191, 203, 220),
                Location = new Point(106, 70)
            };
            Label privacy = new Label
            {
                Text = "LOCAL  •  ADAPTATIVO  •  PRIVADO",
                AutoSize = false,
                Size = new Size(248, 38),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(191, 219, 254),
                BackColor = Color.FromArgb(30, 41, 59),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(ClientSize.Width - 280, 40)
            };
            header.Resize += delegate { privacy.Left = header.ClientSize.Width - privacy.Width - 28; };
            header.Controls.Add(mark);
            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            header.Controls.Add(privacy);

            TableLayoutPanel body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 18, 24, 18),
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Theme.Background
            };
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 160));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
            body.Controls.Add(BuildScore(), 0, 0);
            body.Controls.Add(BuildProgress(), 0, 1);
            body.Controls.Add(BuildRanges(), 0, 2);
            body.Controls.Add(BuildFooter(), 0, 3);

            Controls.Add(body);
            Controls.Add(header);
        }

        private Control BuildScore()
        {
            RoundedPanel card = NewCard(new Padding(24));
            card.Margin = new Padding(0, 0, 0, 12);
            BaselineEvaluation evaluation = _view.Evaluation;
            bool personalized = evaluation.State == BaselineState.Personalized;
            Label number = new Label
            {
                Text = personalized ? evaluation.Score.ToString(CultureInfo.CurrentCulture) : evaluation.LearningPercent.ToString(CultureInfo.CurrentCulture) + "%",
                AutoSize = false,
                Size = new Size(150, 72),
                Location = new Point(24, 29),
                Font = new Font("Bahnschrift", 35f, FontStyle.Bold),
                ForeColor = personalized ? ScoreColor(evaluation.Score) : Theme.Blue,
                TextAlign = ContentAlignment.MiddleCenter
            };
            Label caption = new Label
            {
                Text = personalized ? "ÍNDICE DE FLUXO" : "APRENDIZADO",
                AutoSize = false,
                Size = new Size(150, 22),
                Location = new Point(24, 103),
                Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold),
                ForeColor = Theme.Muted,
                TextAlign = ContentAlignment.MiddleCenter
            };
            Label title = new Label
            {
                Text = evaluation.Title,
                AutoSize = false,
                Location = new Point(198, 27),
                Size = new Size(610, 38),
                Font = new Font("Bahnschrift", 18f, FontStyle.Bold),
                ForeColor = Theme.Text
            };
            Label explanation = new Label
            {
                Text = evaluation.Explanation,
                AutoSize = false,
                Location = new Point(200, 70),
                Size = new Size(610, 58),
                Font = Theme.Body,
                ForeColor = Theme.Muted
            };
            card.Resize += delegate
            {
                title.Width = Math.Max(280, card.ClientSize.Width - 224);
                explanation.Width = Math.Max(280, card.ClientSize.Width - 226);
            };
            card.Controls.Add(number);
            card.Controls.Add(caption);
            card.Controls.Add(title);
            card.Controls.Add(explanation);
            return card;
        }

        private Control BuildProgress()
        {
            RoundedPanel card = NewCard(new Padding(20, 15, 20, 12));
            card.Margin = new Padding(0, 0, 0, 10);
            BaselineEvaluation evaluation = _view.Evaluation;
            long normal = _view.Profile.Normal.SampleCount;
            long meeting = _view.Profile.Meeting.SampleCount;
            Label text = new Label
            {
                Text = evaluation.State == BaselineState.Learning
                    ? "Formando o primeiro padrão: " + normal + " de " + BaselineEngine.RequiredSamples + " leituras válidas"
                    : normal + " leituras normais  •  " + meeting + " em reunião  •  o padrão continua se adaptando",
                Dock = DockStyle.Top,
                Height = 24,
                Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
                ForeColor = Theme.Text
            };
            ProgressBar progress = new ProgressBar
            {
                Dock = DockStyle.Bottom,
                Height = 9,
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = 100,
                Value = evaluation.State == BaselineState.Personalized ? 100 : evaluation.LearningPercent
            };
            card.Controls.Add(progress);
            card.Controls.Add(text);
            return card;
        }

        private Control BuildRanges()
        {
            BaselineContextProfile context = _view.Evaluation.UsedMeetingProfile && _view.Profile.Meeting.IsReady
                ? _view.Profile.Meeting : _view.Profile.Normal;
            TableLayoutPanel grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Theme.Background
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            grid.Controls.Add(BuildRangeCard("MEMÓRIA RAM", BaselineEngine.Range(context.MemoryPercent, 8, "%"), "Faixa habitual de uso"), 0, 0);
            grid.Controls.Add(BuildRangeCard("CPU", BaselineEngine.Range(context.CpuPercent, 20, "%"), "Faixa habitual de atividade"), 1, 0);
            grid.Controls.Add(BuildRangeCard("ARMAZENAMENTO", BaselineEngine.Range(context.DiskLatencyMilliseconds, 15, " ms"), "Latência habitual"), 0, 1);
            grid.Controls.Add(BuildRangeCard("TEMPERATURA", context.TemperatureCelsius.Count == 0 ? "Sensor ainda sem histórico" : BaselineEngine.Range(context.TemperatureCelsius, 10, " °C"), "Somente sensores locais disponíveis"), 1, 1);
            return grid;
        }

        private Control BuildRangeCard(string title, string value, string detail)
        {
            RoundedPanel card = NewCard(Padding.Empty);
            card.Margin = new Padding(0, 0, 10, 10);
            Label caption = new Label { Text = title, Location = new Point(20, 13), Size = new Size(330, 18), Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold), ForeColor = Theme.Muted };
            Label range = new Label { Text = value, Location = new Point(20, 35), Size = new Size(330, 31), Font = new Font("Bahnschrift", 16f, FontStyle.Bold), ForeColor = Theme.Text, AutoEllipsis = true };
            ToolTip tip = new ToolTip();
            tip.SetToolTip(card, detail);
            tip.SetToolTip(caption, detail);
            tip.SetToolTip(range, detail);
            card.Resize += delegate
            {
                bool compact = card.ClientSize.Height < 72;
                caption.Top = compact ? 7 : 13;
                range.Top = compact ? 24 : 35;
                caption.Width = Math.Max(80, card.ClientSize.Width - 40);
                range.Width = Math.Max(80, card.ClientSize.Width - 40);
            };
            card.Controls.Add(range);
            card.Controls.Add(caption);
            return card;
        }

        private Control BuildFooter()
        {
            Panel footer = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background, Margin = new Padding(0) };
            Label privacy = new Label
            {
                Text = "Somente médias e faixas são salvas. Aplicativos e amostras individuais não entram no padrão.",
                AutoSize = true,
                Font = Theme.Small,
                ForeColor = Theme.Muted,
                Location = new Point(2, 24)
            };
            Button close = new AnimatedButton
            {
                Text = "Voltar",
                Size = new Size(126, 46),
                BackColor = Theme.Blue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(footer.Width - 128, 10)
            };
            close.FlatAppearance.BorderSize = 0;
            close.Click += delegate { Close(); };
            footer.Resize += delegate { close.Left = footer.ClientSize.Width - close.Width; };
            footer.Controls.Add(privacy);
            footer.Controls.Add(close);
            AcceptButton = close;
            CancelButton = close;
            return footer;
        }

        private static RoundedPanel NewCard(Padding padding)
        {
            return new RoundedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                OutlineColor = Theme.Border,
                CornerRadius = 16,
                Padding = padding
            };
        }

        private static Color ScoreColor(int score)
        {
            return score >= 85 ? Theme.Green : score >= 60 ? Theme.Amber : Color.Firebrick;
        }
    }
}
