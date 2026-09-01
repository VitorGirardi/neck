using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace Neck
{
    internal sealed class ReplayForm : Form
    {
        private readonly ReplayIncident _incident;
        private readonly List<ReplaySample> _samples;

        public ReplayActionKind SelectedAction { get; private set; }
        public bool HistoryRequested { get; private set; }
        public string IncidentProcessName { get { return _incident == null ? "" : _incident.ProcessName; } }

        public ReplayForm(ReplayIncident incident, IList<ReplaySample> samples)
        {
            _incident = incident == null ? null : incident.Clone();
            _samples = samples == null ? new List<ReplaySample>() : samples.OrderBy(item => item.TimestampUtc).ToList();
            Text = "Neck Replay — o que acabou de acontecer?";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(930, 720);
            MinimumSize = new Size(840, 660);
            BackColor = Theme.Background;
            Font = Theme.Body;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            BuildInterface();
            Shown += delegate { VisualEffects.FadeIn(this); };
        }

        private void BuildInterface()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 110, BackColor = Theme.Card };
            FlowMark mark = new FlowMark { Location = new Point(28, 30), Size = new Size(54, 54) };
            Label title = new Label
            {
                Text = "Neck Replay",
                AutoSize = true,
                Font = new Font("Segoe UI Variable Display", 24f, FontStyle.Bold),
                ForeColor = Theme.Ink,
                Location = new Point(103, 25)
            };
            Label subtitle = new Label
            {
                Text = "A caixa-preta local que explica onde o fluxo foi estrangulado.",
                AutoSize = true,
                Font = Theme.Body,
                ForeColor = Theme.Muted,
                Location = new Point(106, 70)
            };
            Label privacy = new Label
            {
                Text = "●  Últimos 5 min neste PC",
                AutoSize = false,
                Size = new Size(260, 38),
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI Semibold", 9f, FontStyle.Regular),
                ForeColor = Theme.Green,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(ClientSize.Width - 292, 40)
            };
            header.Resize += delegate { privacy.Left = header.ClientSize.Width - privacy.Width - 28; };
            header.Controls.Add(mark);
            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            header.Controls.Add(privacy);

            TableLayoutPanel body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background,
                Padding = new Padding(24, 18, 24, 18),
                ColumnCount = 1,
                RowCount = 4
            };
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 152));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            body.Controls.Add(BuildSummary(), 0, 0);
            body.Controls.Add(BuildEvidence(), 0, 1);
            body.Controls.Add(BuildTimeline(), 0, 2);
            body.Controls.Add(BuildFooter(), 0, 3);

            Controls.Add(body);
            Controls.Add(header);
        }

        private Control BuildSummary()
        {
            RoundedPanel card = NewCard(new Padding(24));
            card.Margin = new Padding(0, 0, 0, 12);
            bool hasIncident = _incident != null && _incident.Cause != ReplayCause.None;
            Label badge = new Label
            {
                AutoSize = false,
                Size = new Size(hasIncident ? 156 : 128, 25),
                Location = new Point(24, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold),
                Text = hasIncident ? (_incident.Ongoing ? "ACONTECENDO AGORA" : "INCIDENTE REGISTRADO") : "FLUXO NORMAL",
                BackColor = hasIncident ? Color.FromArgb(255, 247, 237) : Theme.FlowSoft,
                ForeColor = hasIncident ? Theme.Amber : Theme.Cyan
            };
            Label title = new Label
            {
                AutoSize = false,
                Location = new Point(23, 51),
                Size = new Size(820, 34),
                Font = new Font("Segoe UI Variable Display", 18f, FontStyle.Bold),
                ForeColor = Theme.Text,
                Text = hasIncident ? _incident.Title : "Nenhum gargalo foi confirmado nesta janela"
            };
            Label explanation = new Label
            {
                AutoSize = false,
                Location = new Point(25, 88),
                Size = new Size(820, 42),
                Font = Theme.Body,
                ForeColor = Theme.Muted,
                Text = hasIncident
                    ? _incident.Explanation
                    : _samples.Count < 3
                        ? "O Replay está formando a primeira janela de contexto. Ele só registra pressão persistente, não picos isolados."
                        : "As leituras não mostram paginação, disputa de CPU ou espera do armazenamento suficientes para caracterizar lentidão real."
            };
            card.Resize += delegate
            {
                title.Width = Math.Max(300, card.ClientSize.Width - 48);
                explanation.Width = Math.Max(300, card.ClientSize.Width - 50);
            };
            card.Controls.Add(badge);
            card.Controls.Add(title);
            card.Controls.Add(explanation);
            return card;
        }

        private Control BuildEvidence()
        {
            TableLayoutPanel grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Theme.Background
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334f));
            ReplaySample latest = _samples.LastOrDefault();
            if (_incident == null || _incident.Cause == ReplayCause.None)
            {
                grid.Controls.Add(BuildMetric("MEMÓRIA", latest == null ? "—" : latest.MemoryPercent.ToString("0", CultureInfo.CurrentCulture) + "%", latest == null ? "Aguardando leitura" : MainForm.FormatBytes(latest.AvailableBytes) + " disponíveis"), 0, 0);
                grid.Controls.Add(BuildMetric("CPU", latest == null ? "—" : latest.CpuPercent.ToString("0", CultureInfo.CurrentCulture) + "%", latest == null ? "Aguardando leitura" : "Fila " + latest.ProcessorQueueLength.ToString("0.0", CultureInfo.CurrentCulture)), 1, 0);
                grid.Controls.Add(BuildMetric("ARMAZENAMENTO", latest == null ? "—" : latest.DiskLatencyMilliseconds.ToString("0", CultureInfo.CurrentCulture) + " ms", latest == null ? "Aguardando leitura" : "Fila " + latest.DiskQueueLength.ToString("0.0", CultureInfo.CurrentCulture)), 2, 0);
            }
            else
            {
                string duration = Math.Max(1, (int)Math.Ceiling(_incident.Duration.TotalSeconds)) + " s";
                grid.Controls.Add(BuildMetric("DURAÇÃO", duration, _incident.Ongoing ? "Ainda em observação" : "Terminou às " + _incident.EndedUtc.ToLocalTime().ToString("HH:mm:ss")), 0, 0);
                grid.Controls.Add(BuildMetric("PICO DO GARGALO", PeakValue(_incident), PeakCaption(_incident)), 1, 0);
                grid.Controls.Add(BuildMetric("ASSOCIADO A", string.IsNullOrWhiteSpace(_incident.DisplayName) ? "Sistema" : _incident.DisplayName, AssociationCaption(_incident)), 2, 0);
            }
            return grid;
        }

        private Control BuildMetric(string caption, string value, string detail)
        {
            RoundedPanel card = NewCard(new Padding(18));
            card.Margin = new Padding(0, 0, 10, 10);
            Label captionLabel = new Label { Text = caption, Dock = DockStyle.Top, Height = 20, Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold), ForeColor = Theme.Muted };
            Label valueLabel = new Label { Text = value, Dock = DockStyle.Top, Height = 31, Font = new Font("Segoe UI Variable Display", 16f, FontStyle.Bold), ForeColor = Theme.Text, AutoEllipsis = true };
            Label detailLabel = new Label { Text = detail, Dock = DockStyle.Fill, Font = Theme.Small, ForeColor = Theme.Muted, AutoEllipsis = true };
            card.Controls.Add(detailLabel);
            card.Controls.Add(valueLabel);
            card.Controls.Add(captionLabel);
            return card;
        }

        private Control BuildTimeline()
        {
            RoundedPanel card = NewCard(new Padding(18));
            card.Margin = new Padding(0, 2, 0, 8);
            Label title = new Label
            {
                Text = "Fluxo dos últimos minutos",
                Dock = DockStyle.Top,
                Height = 27,
                Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
                ForeColor = Theme.Text
            };
            Label legend = new Label
            {
                Text = "RAM  — azul-petróleo     CPU  — azul     latência do disco  — laranja",
                Dock = DockStyle.Top,
                Height = 23,
                Font = Theme.Small,
                ForeColor = Theme.Muted
            };
            ReplayTimeline timeline = new ReplayTimeline(_samples, _incident) { Dock = DockStyle.Fill };
            card.Controls.Add(timeline);
            card.Controls.Add(legend);
            card.Controls.Add(title);
            return card;
        }

        private Control BuildFooter()
        {
            Panel footer = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background, Margin = new Padding(0) };
            Label privacy = new Label
            {
                Text = "As amostras ficam na memória e são descartadas ao encerrar o Neck.",
                AutoSize = true,
                Font = Theme.Small,
                ForeColor = Theme.Muted,
                Location = new Point(2, 26)
            };
            LinkLabel history = new LinkLabel
            {
                Text = "Ver leituras das últimas 24 horas",
                AutoSize = true,
                Font = Theme.Small,
                LinkColor = Theme.Blue,
                Location = new Point(2, 48)
            };
            history.Click += delegate { HistoryRequested = true; DialogResult = DialogResult.Retry; Close(); };
            Button close = MakeButton("Voltar", Theme.NavySoft, 112);
            close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            close.Location = new Point(footer.Width - 114, 13);
            close.Click += delegate { Close(); };
            Button action = MakeButton(_incident == null ? "" : _incident.ActionText, Theme.Blue, 250);
            action.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            action.Location = new Point(close.Left - action.Width - 10, 13);
            action.Visible = _incident != null && _incident.ActionKind != ReplayActionKind.None;
            action.Click += delegate
            {
                SelectedAction = _incident.ActionKind;
                DialogResult = DialogResult.OK;
                Close();
            };
            footer.Resize += delegate
            {
                close.Left = footer.ClientSize.Width - close.Width;
                action.Left = close.Left - action.Width - 10;
            };
            footer.Controls.Add(privacy);
            footer.Controls.Add(history);
            footer.Controls.Add(action);
            footer.Controls.Add(close);
            AcceptButton = action.Visible ? action : close;
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

        private static Button MakeButton(string text, Color color, int width)
        {
            Button button = new AnimatedButton
            {
                Text = text,
                Size = new Size(width, 46),
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private static string PeakValue(ReplayIncident incident)
        {
            if (incident.Cause == ReplayCause.MemoryPressure) return incident.PeakMemoryPercent.ToString("0", CultureInfo.CurrentCulture) + "% RAM";
            if (incident.Cause == ReplayCause.CpuContention) return incident.PeakCpuPercent.ToString("0", CultureInfo.CurrentCulture) + "% CPU";
            if (incident.Cause == ReplayCause.DiskStall) return incident.PeakDiskLatencyMilliseconds.ToString("0", CultureInfo.CurrentCulture) + " ms";
            if (incident.Cause == ReplayCause.ThermalPressure) return incident.PeakTemperatureCelsius.ToString("0", CultureInfo.CurrentCulture) + " °C";
            return incident.PeakUtc.ToLocalTime().ToString("HH:mm:ss");
        }

        private static string PeakCaption(ReplayIncident incident)
        {
            if (incident.Cause == ReplayCause.MemoryPressure) return MainForm.FormatBytes(incident.LowestAvailableBytes) + " de menor folga";
            if (incident.Cause == ReplayCause.CpuContention) return "Fila " + incident.PeakProcessorQueue.ToString("0.0", CultureInfo.CurrentCulture);
            if (incident.Cause == ReplayCause.DiskStall) return "Atividade " + incident.PeakDiskActivePercent.ToString("0", CultureInfo.CurrentCulture) + "%";
            if (incident.Cause == ReplayCause.ThermalPressure) return "Sensor local confiável";
            return "Janela sem resposta";
        }

        private static string AssociationCaption(ReplayIncident incident)
        {
            if (incident.Cause == ReplayCause.MemoryPressure)
                return "Paginação " + incident.PeakPageReadsPerSecond.ToString("0", CultureInfo.CurrentCulture) + "/s";
            if (incident.Cause == ReplayCause.CpuContention)
                return "Fila da CPU " + incident.PeakProcessorQueue.ToString("0.0", CultureInfo.CurrentCulture);
            if (incident.Cause == ReplayCause.DiskStall)
                return "Fila do disco " + incident.PeakDiskQueue.ToString("0.0", CultureInfo.CurrentCulture);
            if (incident.Cause == ReplayCause.ThermalPressure)
                return "Sensor local confiável";
            return incident.SampleCount + " leituras preservadas";
        }
    }

    internal sealed class ReplayTimeline : Control
    {
        private readonly List<ReplaySample> _samples;
        private readonly ReplayIncident _incident;

        public ReplayTimeline(IList<ReplaySample> samples, ReplayIncident incident)
        {
            _samples = samples == null ? new List<ReplaySample>() : samples.OrderBy(item => item.TimestampUtc).ToList();
            _incident = incident;
            DoubleBuffered = true;
            BackColor = Color.White;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            bool compact = Height < 70;
            int plotTop = compact ? 3 : 8;
            int bottomSpace = compact ? 19 : 34;
            Rectangle plot = new Rectangle(42, plotTop, Math.Max(40, Width - 58), Math.Max(8, Height - bottomSpace));
            using (Pen grid = new Pen(Color.FromArgb(231, 236, 244), 1f))
            {
                for (int i = 0; i <= 4; i++)
                {
                    int y = plot.Top + i * plot.Height / 4;
                    e.Graphics.DrawLine(grid, plot.Left, y, plot.Right, y);
                }
            }
            using (Brush label = new SolidBrush(Theme.Muted))
            using (Font font = new Font("Segoe UI", 7.5f))
            {
                e.Graphics.DrawString("100", font, label, 8, plot.Top - 5);
                e.Graphics.DrawString("50", font, label, 14, plot.Top + plot.Height / 2 - 5);
                e.Graphics.DrawString("0", font, label, 20, plot.Bottom - 8);
            }
            if (_samples.Count == 0)
            {
                using (Brush muted = new SolidBrush(Theme.Muted))
                    e.Graphics.DrawString("A primeira linha aparecerá depois da próxima leitura.", Theme.Small, muted, plot.Left + 18, plot.Top + plot.Height / 2 - 8);
                return;
            }

            DateTime start = _samples.First().TimestampUtc;
            DateTime end = _samples.Last().TimestampUtc;
            if (end <= start) end = start.AddSeconds(1);
            if (_incident != null)
            {
                DateTime incidentEnd = _incident.Ongoing || _incident.EndedUtc == DateTime.MinValue ? end : _incident.EndedUtc;
                float left = X(_incident.StartedUtc, start, end, plot);
                float right = X(incidentEnd, start, end, plot);
                using (Brush shade = new SolidBrush(Color.FromArgb(24, Theme.Amber)))
                    e.Graphics.FillRectangle(shade, Math.Min(left, right), plot.Top, Math.Max(3, Math.Abs(right - left)), plot.Height);
            }

            DrawSeries(e.Graphics, plot, start, end, Theme.Cyan, item => item.MemoryPercent);
            DrawSeries(e.Graphics, plot, start, end, Theme.Blue, item => item.CpuPercent);
            DrawSeries(e.Graphics, plot, start, end, Theme.Amber, item => Math.Min(100, item.DiskLatencyMilliseconds));

            using (Brush label = new SolidBrush(Theme.Muted))
            using (Font font = new Font("Segoe UI", 7.5f))
            {
                float timeTop = Math.Min(Height - 13, plot.Bottom + (compact ? 2 : 4));
                e.Graphics.DrawString(start.ToLocalTime().ToString("HH:mm:ss"), font, label, plot.Left, timeTop);
                string endText = end.ToLocalTime().ToString("HH:mm:ss");
                SizeF size = e.Graphics.MeasureString(endText, font);
                e.Graphics.DrawString(endText, font, label, plot.Right - size.Width, timeTop);
            }
        }

        private void DrawSeries(Graphics graphics, Rectangle plot, DateTime start, DateTime end, Color color, Func<ReplaySample, double> selector)
        {
            if (_samples.Count == 1)
            {
                float x = plot.Left;
                float y = Y(selector(_samples[0]), plot);
                using (Brush brush = new SolidBrush(color)) graphics.FillEllipse(brush, x - 3, y - 3, 6, 6);
                return;
            }
            PointF[] points = _samples.Select(item => new PointF(X(item.TimestampUtc, start, end, plot), Y(selector(item), plot))).ToArray();
            using (Pen pen = new Pen(color, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                graphics.DrawLines(pen, points);
        }

        private static float X(DateTime value, DateTime start, DateTime end, Rectangle plot)
        {
            double ratio = Math.Max(0, Math.Min(1, (value - start).TotalMilliseconds / Math.Max(1, (end - start).TotalMilliseconds)));
            return plot.Left + (float)(ratio * plot.Width);
        }

        private static float Y(double value, Rectangle plot)
        {
            double normalized = Math.Max(0, Math.Min(100, value));
            return plot.Bottom - (float)(normalized / 100d * plot.Height);
        }
    }
}
