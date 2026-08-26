using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Neck")]
[assembly: System.Reflection.AssemblyDescription("Diagnóstico inteligente e manutenção segura para Windows")]
[assembly: System.Reflection.AssemblyCompany("Neck")]
[assembly: System.Reflection.AssemblyProduct("Neck")]
[assembly: System.Reflection.AssemblyVersion("0.4.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.4.0.0")]

namespace Neck
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

#if !NOELEVATION
            if (!SecurityHelper.IsAdministrator())
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Application.ExecutablePath,
                        UseShellExecute = true,
                        Verb = "runas",
                        WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath) ?? Environment.CurrentDirectory
                    });
                }
                catch
                {
                    MessageBox.Show(
                        "A manutenção do Windows precisa de permissão de administrador. Nenhuma alteração foi feita.",
                        "Neck",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                return;
            }
#endif

            Application.Run(new MainForm());
        }
    }

    internal static class Theme
    {
        public static readonly Color Background = Color.FromArgb(246, 248, 252);
        public static readonly Color Card = Color.White;
        public static readonly Color Navy = Color.FromArgb(15, 23, 42);
        public static readonly Color NavySoft = Color.FromArgb(51, 65, 85);
        public static readonly Color Blue = Color.FromArgb(37, 99, 235);
        public static readonly Color BlueSoft = Color.FromArgb(239, 246, 255);
        public static readonly Color Cyan = Color.FromArgb(6, 182, 212);
        public static readonly Color Green = Color.FromArgb(5, 150, 105);
        public static readonly Color GreenSoft = Color.FromArgb(236, 253, 245);
        public static readonly Color Amber = Color.FromArgb(217, 119, 6);
        public static readonly Color Text = Color.FromArgb(30, 41, 59);
        public static readonly Color Muted = Color.FromArgb(100, 116, 139);
        public static readonly Color Border = Color.FromArgb(226, 232, 240);
        public static readonly Font Title = new Font("Segoe UI Semibold", 25f, FontStyle.Bold);
        public static readonly Font Heading = new Font("Segoe UI Semibold", 15f, FontStyle.Bold);
        public static readonly Font Body = new Font("Segoe UI", 10f, FontStyle.Regular);
        public static readonly Font Small = new Font("Segoe UI", 9f, FontStyle.Regular);
    }

    internal sealed class RoundedPanel : Panel
    {
        public int CornerRadius { get; set; }
        public Color OutlineColor { get; set; }

        public RoundedPanel()
        {
            CornerRadius = 18;
            OutlineColor = Theme.Border;
            DoubleBuffered = true;
            Resize += delegate { UpdateShape(); };
        }

        private GraphicsPath BuildPath(Rectangle bounds)
        {
            int radius = Math.Max(4, CornerRadius);
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void UpdateShape()
        {
            if (Width < 4 || Height < 4) return;
            using (GraphicsPath path = BuildPath(new Rectangle(0, 0, Width - 1, Height - 1)))
            {
                Region = new Region(path);
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = BuildPath(new Rectangle(0, 0, Width - 1, Height - 1)))
            using (Pen pen = new Pen(OutlineColor, 1f))
            {
                e.Graphics.DrawPath(pen, path);
            }
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly Label _memoryValue = new Label();
        private readonly Label _diskValue = new Label();
        private readonly Label _lastRunValue = new Label();
        private readonly Label _recommendation = new Label();
        private readonly Label _analysisValue = new Label();
        private readonly ProgressBar _progress = new ProgressBar();
        private readonly RichTextBox _log = new RichTextBox();
        private readonly Button _analyzeButton = new Button();
        private readonly Button _runButton = new Button();
        private readonly Button _advancedButton = new Button();
        private readonly Button _driversButton = new Button();
        private readonly Button _guardButton = new Button();
        private readonly Button _meetingButton = new Button();
        private readonly Button _reportsButton = new Button();
        private readonly Label _guardBadge = new Label();
        private readonly Label _guardMessage = new Label();
        private readonly Label _guardProcess = new Label();
        private readonly CheckBox _tempCheck = new CheckBox();
        private readonly CheckBox _reportsCheck = new CheckBox();
        private readonly CheckBox _recycleCheck = new CheckBox();
        private readonly CheckBox _componentsCheck = new CheckBox();
        private readonly CheckBox _healthCheck = new CheckBox();
        private readonly CheckBox _drivesCheck = new CheckBox();
        private readonly Timer _statusTimer = new Timer();
        private long _analyzedBytes;
        private bool _busy;
        private DateTime _lastHealthScan = DateTime.MinValue;
        private HealthSnapshot _healthSnapshot;
        private bool _meetingActive;
        private DateTime _meetingEndsAt;

        private static readonly string DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Neck");
        private static readonly string LastRunFile = Path.Combine(DataDirectory, "ultima-manutencao.txt");
        private static readonly string ReportDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Neck", "Relatorios");

        public MainForm()
        {
            Text = "Neck";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1040, 740);
            Size = new Size(1120, 790);
            BackColor = Theme.Background;
            Font = Theme.Body;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;

            _tempCheck.Checked = true;
            _reportsCheck.Checked = true;
            _componentsCheck.Checked = true;
            _drivesCheck.Checked = true;

            BuildInterface();
            UpdateSystemStatus();
            LoadLastRun();

            _statusTimer.Interval = 3000;
            _statusTimer.Tick += delegate { if (!_busy) UpdateSystemStatus(); };
            _statusTimer.Start();

            Shown += async delegate { await AnalyzeAsync(false); };
            FormClosed += delegate
            {
                if (_meetingActive) NativeMethods.SetThreadExecutionState(NativeMethods.ES_CONTINUOUS);
            };
        }

        private void BuildInterface()
        {
            Controls.Add(BuildBody());
            Controls.Add(BuildHeader());
        }

        private Control BuildHeader()
        {
            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 108,
                BackColor = Theme.Navy,
                Padding = new Padding(30, 18, 30, 16)
            };

            RoundedPanel mark = new RoundedPanel
            {
                Size = new Size(54, 54),
                Location = new Point(31, 24),
                BackColor = Theme.Cyan,
                OutlineColor = Theme.Cyan,
                CornerRadius = 15
            };
            mark.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "N",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Semibold", 23f, FontStyle.Bold),
                ForeColor = Color.White
            });

            Label title = new Label
            {
                AutoSize = true,
                Text = "Neck",
                Font = Theme.Title,
                ForeColor = Color.White,
                Location = new Point(101, 18)
            };
            Label subtitle = new Label
            {
                AutoSize = true,
                Text = "Cuide do Windows sem complicação",
                Font = Theme.Body,
                ForeColor = Color.FromArgb(186, 199, 218),
                Location = new Point(105, 62)
            };
            _recommendation.AutoSize = false;
            _recommendation.Size = new Size(258, 44);
            _recommendation.TextAlign = ContentAlignment.MiddleCenter;
            _recommendation.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
            _recommendation.ForeColor = Color.White;
            _recommendation.BackColor = Theme.NavySoft;
            _recommendation.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _recommendation.Location = new Point(ClientSize.Width - 292, 31);
            header.Resize += delegate { _recommendation.Left = header.ClientSize.Width - _recommendation.Width - 32; };

            header.Controls.Add(mark);
            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            header.Controls.Add(_recommendation);
            return header;
        }

        private Control BuildBody()
        {
            TableLayoutPanel body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background,
                Padding = new Padding(26, 18, 26, 24),
                ColumnCount = 2,
                RowCount = 3
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 124f));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 272f));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Control summary = BuildSummaryCard();
            body.SetColumnSpan(summary, 2);
            body.Controls.Add(summary, 0, 0);
            body.Controls.Add(BuildQuickCard(), 0, 1);
            body.Controls.Add(BuildDeepCard(), 1, 1);
            body.Controls.Add(BuildGuardCard(), 0, 2);
            body.Controls.Add(BuildActivityCard(), 1, 2);
            return body;
        }

        private Control BuildSummaryCard()
        {
            Panel card = MakeCard(new Padding(18, 14, 18, 12));
            card.Margin = new Padding(0, 0, 0, 14);
            TableLayoutPanel grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 2,
                BackColor = Color.White
            };
            for (int i = 0; i < 4; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
            AddMetric(grid, 0, "MEMÓRIA EM USO", _memoryValue);
            AddMetric(grid, 1, "ESPAÇO LIVRE", _diskValue);
            AddMetric(grid, 2, "LIMPEZA DISPONÍVEL", _analysisValue);
            AddMetric(grid, 3, "ÚLTIMO CUIDADO", _lastRunValue);
            card.Controls.Add(grid);
            return card;
        }

        private Control BuildQuickCard()
        {
            Panel card = MakeCard(new Padding(24));
            card.Margin = new Padding(0, 0, 10, 14);

            Label badge = CreateBadge("RECOMENDADO", Theme.BlueSoft, Theme.Blue);
            badge.Location = new Point(24, 22);
            Label title = new Label
            {
                Text = "Limpeza rápida",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 19f, FontStyle.Bold),
                ForeColor = Theme.Text,
                Location = new Point(23, 60)
            };
            Label description = new Label
            {
                Text = "Remove apenas temporários antigos e relatórios de erro. Seus documentos, senhas e downloads ficam intactos.",
                AutoSize = false,
                Size = new Size(430, 58),
                Font = Theme.Body,
                ForeColor = Theme.Muted,
                Location = new Point(26, 105)
            };
            Label safe = new Label
            {
                Text = "✓  Não fecha programas    ✓  Não reinicia o PC",
                AutoSize = true,
                Font = Theme.Small,
                ForeColor = Theme.Green,
                Location = new Point(25, 166)
            };

            ConfigureButton(_analyzeButton, "Analisar agora", Theme.NavySoft, 142);
            ConfigureButton(_runButton, "Fazer limpeza segura", Theme.Blue, 218);
            _analyzeButton.Location = new Point(24, 202);
            _runButton.Location = new Point(176, 202);
            _analyzeButton.Click += async delegate { await AnalyzeAsync(true); };
            _runButton.Click += async delegate
            {
                _tempCheck.Checked = true;
                _reportsCheck.Checked = true;
                _recycleCheck.Checked = false;
                _componentsCheck.Checked = false;
                _healthCheck.Checked = false;
                _drivesCheck.Checked = false;
                await RunMaintenanceAsync();
            };

            card.Controls.Add(badge);
            card.Controls.Add(title);
            card.Controls.Add(description);
            card.Controls.Add(safe);
            card.Controls.Add(_analyzeButton);
            card.Controls.Add(_runButton);
            return card;
        }

        private Control BuildDeepCard()
        {
            Panel card = MakeCard(new Padding(24));
            card.Margin = new Padding(10, 0, 0, 14);

            Label badge = CreateBadge("MENSAL", Theme.GreenSoft, Theme.Green);
            badge.Location = new Point(24, 22);
            Label title = new Label
            {
                Text = "Manutenção completa",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 19f, FontStyle.Bold),
                ForeColor = Theme.Text,
                Location = new Point(23, 60)
            };
            Label description = new Label
            {
                Text = "Reúne as ferramentas nativas do Windows para componentes, integridade e otimização da unidade.",
                AutoSize = false,
                Size = new Size(430, 50),
                Font = Theme.Body,
                ForeColor = Theme.Muted,
                Location = new Point(26, 105)
            };
            Label included = new Label
            {
                Text = "Componentes  •  DISM/SFC  •  TRIM ou desfragmentação",
                AutoSize = true,
                Font = Theme.Small,
                ForeColor = Theme.Muted,
                Location = new Point(25, 166)
            };
            ConfigureButton(_advancedButton, "Escolher tarefas", Theme.Green, 190);
            _advancedButton.Location = new Point(24, 202);
            _advancedButton.Click += async delegate { await ShowAdvancedAndRunAsync(); };

            card.Controls.Add(badge);
            card.Controls.Add(title);
            card.Controls.Add(description);
            card.Controls.Add(included);
            card.Controls.Add(_advancedButton);
            return card;
        }

        private Control BuildGuardCard()
        {
            Panel card = MakeCard(new Padding(22));
            card.Margin = new Padding(0, 0, 10, 0);
            _guardBadge.Text = "ANALISANDO";
            _guardBadge.AutoSize = false;
            _guardBadge.Size = new Size(108, 25);
            _guardBadge.BackColor = Theme.BlueSoft;
            _guardBadge.ForeColor = Theme.Blue;
            _guardBadge.TextAlign = ContentAlignment.MiddleCenter;
            _guardBadge.Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold);
            _guardBadge.Location = new Point(22, 17);
            Label title = new Label
            {
                Text = "Neck Guard",
                AutoSize = true,
                Font = Theme.Heading,
                ForeColor = Theme.Text,
                Location = new Point(22, 49)
            };
            _guardMessage.Text = "Procurando sinais de sobrecarga...";
            _guardMessage.AutoSize = false;
            _guardMessage.Size = new Size(450, 38);
            _guardMessage.Font = Theme.Small;
            _guardMessage.ForeColor = Theme.Muted;
            _guardMessage.Location = new Point(23, 77);
            _guardProcess.Text = "Maior uso de memória: calculando";
            _guardProcess.AutoSize = false;
            _guardProcess.Size = new Size(450, 24);
            _guardProcess.Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold);
            _guardProcess.ForeColor = Theme.Text;
            _guardProcess.Location = new Point(23, 112);

            ConfigureButton(_meetingButton, "Modo reunião", Theme.Blue, 140);
            ConfigureButton(_guardButton, "Diagnóstico", Theme.NavySoft, 112);
            ConfigureButton(_driversButton, "Drivers", Theme.NavySoft, 82);
            ConfigureButton(_reportsButton, "Histórico", Theme.NavySoft, 96);
            _meetingButton.Location = new Point(22, 142);
            _guardButton.Location = new Point(172, 142);
            _driversButton.Location = new Point(294, 142);
            _reportsButton.Location = new Point(386, 142);
            _meetingButton.Click += delegate { ToggleMeetingMode(); };
            _guardButton.Click += delegate
            {
                HealthSnapshot snapshot = SystemInfo.GetHealthSnapshot();
                using (DiagnosticForm form = new DiagnosticForm(snapshot)) form.ShowDialog(this);
            };
            _driversButton.Click += delegate { using (DriverCenterForm form = new DriverCenterForm()) form.ShowDialog(this); };
            _reportsButton.Click += delegate
            {
                Directory.CreateDirectory(ReportDirectory);
                OpenTarget(ReportDirectory);
            };

            card.Controls.Add(_guardBadge);
            card.Controls.Add(title);
            card.Controls.Add(_guardMessage);
            card.Controls.Add(_guardProcess);
            card.Controls.Add(_meetingButton);
            card.Controls.Add(_guardButton);
            card.Controls.Add(_driversButton);
            card.Controls.Add(_reportsButton);
            return card;
        }

        private Control BuildActivityCard()
        {
            Panel card = MakeCard(new Padding(20));
            card.Margin = new Padding(10, 0, 0, 0);
            Label heading = new Label
            {
                Text = "Atividade",
                Dock = DockStyle.Top,
                Height = 32,
                Font = Theme.Heading,
                ForeColor = Theme.Text
            };
            _progress.Dock = DockStyle.Bottom;
            _progress.Height = 6;
            _progress.Style = ProgressBarStyle.Continuous;
            _log.Dock = DockStyle.Fill;
            _log.BorderStyle = BorderStyle.None;
            _log.BackColor = Color.White;
            _log.ForeColor = Theme.Muted;
            _log.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            _log.ReadOnly = true;
            _log.DetectUrls = false;
            _log.Text = "Tudo pronto. A análise não remove nenhum arquivo.\n";
            card.Controls.Add(_log);
            card.Controls.Add(heading);
            card.Controls.Add(_progress);
            return card;
        }

        private static Panel MakeCard(Padding padding)
        {
            RoundedPanel panel = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                Padding = padding,
                OutlineColor = Theme.Border,
                CornerRadius = 18
            };
            return panel;
        }

        private static Label MakeHeading(string text)
        {
            return new Label
            {
                Text = text,
                Font = Theme.Heading,
                ForeColor = Theme.Text,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static void AddMetric(TableLayoutPanel grid, int column, string caption, Label value)
        {
            Label top = new Label
            {
                Text = caption,
                Font = new Font("Segoe UI Semibold", 8.2f, FontStyle.Bold),
                ForeColor = Theme.Muted,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomCenter
            };
            value.Text = "—";
            value.Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold);
            value.ForeColor = Theme.Text;
            value.Dock = DockStyle.Fill;
            value.TextAlign = ContentAlignment.TopCenter;
            grid.Controls.Add(top, column, 0);
            grid.Controls.Add(value, column, 1);
        }

        private static void ConfigureCheck(CheckBox box, string title, string description, bool isChecked)
        {
            box.AutoSize = false;
            box.Width = 395;
            box.Height = 65;
            box.Checked = isChecked;
            box.Text = title + Environment.NewLine + description;
            box.Font = Theme.Body;
            box.ForeColor = Theme.Text;
            box.Padding = new Padding(4, 2, 4, 2);
            box.Margin = new Padding(0, 0, 0, 4);
            box.CheckAlign = ContentAlignment.TopLeft;
            box.TextAlign = ContentAlignment.TopLeft;
        }

        private static void ConfigureButton(Button button, string text, Color color, int width)
        {
            button.Text = text;
            button.Width = width;
            button.Height = 42;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = color;
            button.ForeColor = Color.White;
            button.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.Margin = new Padding(0, 0, 10, 8);
            button.UseVisualStyleBackColor = false;
            button.Resize += delegate
            {
                if (button.Width < 4 || button.Height < 4) return;
                using (GraphicsPath path = RoundedRectangle(new Rectangle(0, 0, button.Width, button.Height), 10))
                    button.Region = new Region(path);
            };
        }

        private static Label CreateBadge(string text, Color background, Color foreground)
        {
            Label badge = new Label
            {
                Text = text,
                AutoSize = false,
                Size = new Size(118, 26),
                BackColor = background,
                ForeColor = foreground,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold)
            };
            return badge;
        }

        private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private async Task ShowAdvancedAndRunAsync()
        {
            using (MaintenanceOptionsForm form = new MaintenanceOptionsForm(
                _tempCheck.Checked, _reportsCheck.Checked, _recycleCheck.Checked,
                _componentsCheck.Checked, _healthCheck.Checked, _drivesCheck.Checked))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                _tempCheck.Checked = form.CleanTemp;
                _reportsCheck.Checked = form.CleanReports;
                _recycleCheck.Checked = form.EmptyRecycleBin;
                _componentsCheck.Checked = form.CleanComponents;
                _healthCheck.Checked = form.CheckHealth;
                _drivesCheck.Checked = form.OptimizeDrive;
            }
            await RunMaintenanceAsync();
        }

        private void UpdateSystemStatus()
        {
            if (_meetingActive && DateTime.Now >= _meetingEndsAt) DeactivateMeetingMode("O tempo escolhido terminou.");

            MemoryStatus status = SystemInfo.GetMemoryStatus();
            _memoryValue.Text = status.PercentUsed.ToString("0.0", CultureInfo.CurrentCulture) + "%";
            _memoryValue.ForeColor = status.PercentUsed >= 85 ? Color.Firebrick : status.PercentUsed >= 70 ? Theme.Amber : Theme.Green;

            try
            {
                string root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
                DriveInfo drive = new DriveInfo(root);
                _diskValue.Text = FormatBytes(drive.AvailableFreeSpace);
                _diskValue.ForeColor = drive.AvailableFreeSpace < 15L * 1024 * 1024 * 1024 ? Theme.Amber : Theme.Text;
            }
            catch { _diskValue.Text = "—"; }

            if ((DateTime.Now - _lastHealthScan).TotalSeconds >= 10)
            {
                _lastHealthScan = DateTime.Now;
                _healthSnapshot = SystemInfo.GetHealthSnapshot();
                UpdateGuardView(_healthSnapshot);
            }
        }

        private void UpdateGuardView(HealthSnapshot snapshot)
        {
            if (snapshot == null) return;
            if (_meetingActive)
            {
                UpdateMeetingDisplay();
                return;
            }
            if (snapshot.Level == HealthLevel.Critical)
            {
                _guardBadge.Text = "CRÍTICO";
                _guardBadge.BackColor = Color.FromArgb(254, 226, 226);
                _guardBadge.ForeColor = Color.Firebrick;
            }
            else if (snapshot.Level == HealthLevel.Warning)
            {
                _guardBadge.Text = "ATENÇÃO";
                _guardBadge.BackColor = Color.FromArgb(255, 247, 237);
                _guardBadge.ForeColor = Theme.Amber;
            }
            else
            {
                _guardBadge.Text = "ESTÁVEL";
                _guardBadge.BackColor = Theme.GreenSoft;
                _guardBadge.ForeColor = Theme.Green;
            }

            _guardMessage.Text = snapshot.Summary;
            ResourceProcess top = snapshot.TopProcesses.FirstOrDefault();
            _guardProcess.Text = top == null
                ? "Nenhum processo pôde ser analisado."
                : "Maior uso: " + top.DisplayName + "  •  " + FormatBytes(top.MemoryBytes);
        }

        private void ToggleMeetingMode()
        {
            if (_meetingActive)
            {
                DeactivateMeetingMode("Encerrado por você.");
                return;
            }

            MeetingPreflight preflight = SystemInfo.GetMeetingPreflight();
            using (MeetingModeForm form = new MeetingModeForm(preflight))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                ActivateMeetingMode(form.DurationMinutes);
            }
        }

        private void ActivateMeetingMode(int durationMinutes)
        {
            uint state = NativeMethods.SetThreadExecutionState(
                NativeMethods.ES_CONTINUOUS | NativeMethods.ES_SYSTEM_REQUIRED | NativeMethods.ES_DISPLAY_REQUIRED);
            if (state == 0)
            {
                MessageBox.Show("O Windows não permitiu ativar a proteção contra suspensão.", "Neck", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _meetingActive = true;
            _meetingEndsAt = DateTime.Now.AddMinutes(Math.Max(15, durationMinutes));
            _analyzeButton.Enabled = false;
            _runButton.Enabled = false;
            _advancedButton.Enabled = false;
            UpdateMeetingDisplay();
            AppendLog("MODO REUNIÃO — ativo até " + _meetingEndsAt.ToString("HH:mm") + ". Suspensão e tela apagada foram bloqueadas.");
        }

        private void UpdateMeetingDisplay()
        {
            TimeSpan remaining = _meetingEndsAt - DateTime.Now;
            int minutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
            _guardBadge.Text = "PROTEGIDO";
            _guardBadge.BackColor = Color.FromArgb(236, 254, 255);
            _guardBadge.ForeColor = Theme.Cyan;
            _guardMessage.Text = "Reunião protegida por mais " + minutes + " min. Manutenções estão pausadas.";
            _guardProcess.Text = "A tela e o computador não entrarão em suspensão.";
            _meetingButton.Text = "Encerrar modo";
            _meetingButton.BackColor = Theme.Cyan;
            _recommendation.Text = "MODO REUNIÃO  •  até " + _meetingEndsAt.ToString("HH:mm");
            _recommendation.BackColor = Color.FromArgb(14, 116, 144);
        }

        private void DeactivateMeetingMode(string reason)
        {
            NativeMethods.SetThreadExecutionState(NativeMethods.ES_CONTINUOUS);
            _meetingActive = false;
            _meetingButton.Text = "Modo reunião";
            _meetingButton.BackColor = Theme.Blue;
            _analyzeButton.Enabled = !_busy;
            _runButton.Enabled = !_busy;
            _advancedButton.Enabled = !_busy;
            _recommendation.BackColor = Theme.NavySoft;
            LoadLastRun();
            UpdateGuardView(_healthSnapshot);
            AppendLog("MODO REUNIÃO — desativado. " + reason);
        }

        private void LoadLastRun()
        {
            try
            {
                if (!File.Exists(LastRunFile))
                {
                    _lastRunValue.Text = "Nunca";
                    _recommendation.Text = "Primeira manutenção recomendada";
                    return;
                }

                DateTime last = DateTime.Parse(File.ReadAllText(LastRunFile), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToLocalTime();
                int days = Math.Max(0, (int)(DateTime.Now - last).TotalDays);
                _lastRunValue.Text = days == 0 ? "Hoje" : days + " dias";
                _recommendation.Text = days >= 30 ? "Manutenção recomendada agora" : "Próxima revisão em " + Math.Max(0, 30 - days) + " dias";
            }
            catch
            {
                _lastRunValue.Text = "—";
                _recommendation.Text = "Faça uma análise para começar";
            }
        }

        private async Task AnalyzeAsync(bool clearLog)
        {
            if (_busy) return;
            SetBusy(true, "Analisando arquivos...");
            if (clearLog) _log.Clear();
            AppendLog("ANÁLISE — " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));

            try
            {
                ScanResult result = await Task.Run(delegate { return Cleaner.Analyze(); });
                _analyzedBytes = result.TotalBytes;
                _analysisValue.Text = FormatBytes(result.TotalBytes);
                _analysisValue.ForeColor = result.TotalBytes > 512L * 1024 * 1024 ? Theme.Green : Theme.Text;
                _runButton.Text = result.TotalBytes > 0 ? "Limpar " + FormatBytes(result.TotalBytes) : "Limpeza rápida";

                AppendLog("Temporários seguros: " + FormatBytes(result.TempBytes) + " em " + result.TempFiles + " arquivos");
                AppendLog("Relatórios de erro: " + FormatBytes(result.ReportBytes) + " em " + result.ReportFiles + " arquivos");
                AppendLog("Estimativa total: " + FormatBytes(result.TotalBytes));
                if (result.AccessErrors > 0) AppendLog("Itens protegidos ignorados: " + result.AccessErrors);
                AppendLog("Análise concluída. Nada foi apagado.");
            }
            catch (Exception ex)
            {
                AppendLog("Falha na análise: " + ex.Message);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private async Task RunMaintenanceAsync()
        {
            if (_busy) return;
            if (_meetingActive)
            {
                MessageBox.Show("Encerre o Modo Reunião antes de iniciar uma manutenção.", "Neck", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            List<string> selected = GetSelectedTasks();
            if (selected.Count == 0)
            {
                MessageBox.Show("Marque pelo menos uma opção.", "Neck", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string warning = "Serão executadas estas tarefas:\n\n• " + string.Join("\n• ", selected) +
                             "\n\nDocumentos, fotos, senhas e arquivos pessoais não serão tocados.";
            if (_recycleCheck.Checked) warning += "\n\nAtenção: o conteúdo da Lixeira será apagado definitivamente.";

            if (MessageBox.Show(warning, "Confirmar manutenção", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK)
                return;

            SetBusy(true, "Executando manutenção...");
            _log.Clear();
            AppendLog("MANUTENÇÃO — " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
            StringBuilder report = new StringBuilder();
            report.AppendLine("Neck — Relatório de manutenção");
            report.AppendLine(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
            report.AppendLine(new string('-', 64));
            long freed = 0;

            try
            {
                if (_tempCheck.Checked)
                {
                    AppendLog("Limpando temporários seguros...");
                    DeleteResult result = await Task.Run(delegate { return Cleaner.CleanTemp(); });
                    freed += result.BytesDeleted;
                    string line = "Temporários: " + FormatBytes(result.BytesDeleted) + " liberados; " + result.FilesDeleted + " arquivos removidos; " + result.Errors + " ignorados.";
                    AppendLog(line);
                    report.AppendLine(line);
                }

                if (_reportsCheck.Checked)
                {
                    AppendLog("Limpando relatórios de erro antigos...");
                    DeleteResult result = await Task.Run(delegate { return Cleaner.CleanReports(); });
                    freed += result.BytesDeleted;
                    string line = "Relatórios: " + FormatBytes(result.BytesDeleted) + " liberados; " + result.FilesDeleted + " arquivos removidos; " + result.Errors + " ignorados.";
                    AppendLog(line);
                    report.AppendLine(line);
                }

                if (_recycleCheck.Checked)
                {
                    AppendLog("Esvaziando a Lixeira...");
                    int code = NativeMethods.SHEmptyRecycleBin(IntPtr.Zero, null,
                        NativeMethods.SHERB_NOCONFIRMATION | NativeMethods.SHERB_NOPROGRESSUI | NativeMethods.SHERB_NOSOUND);
                    string line = code == 0 ? "Lixeira esvaziada." : "A Lixeira retornou o código " + code + ".";
                    AppendLog(line);
                    report.AppendLine(line);
                }

                if (_componentsCheck.Checked)
                {
                    AppendLog("Executando limpeza de componentes do Windows (DISM)...");
                    ProcessResult result = await Task.Run(delegate
                    {
                        return ProcessRunner.Run("dism.exe", "/Online /Cleanup-Image /StartComponentCleanup /NoRestart", 45 * 60 * 1000);
                    });
                    string line = "DISM Component Cleanup: " + (result.ExitCode == 0 ? "concluído" : "código " + result.ExitCode) + ".";
                    AppendLog(line);
                    report.AppendLine(line);
                    report.AppendLine(result.Output);
                }

                if (_healthCheck.Checked)
                {
                    AppendLog("Verificando a imagem do Windows...");
                    ProcessResult dism = await Task.Run(delegate
                    {
                        return ProcessRunner.Run("dism.exe", "/Online /Cleanup-Image /ScanHealth /NoRestart", 45 * 60 * 1000);
                    });
                    AppendLog("DISM ScanHealth: " + (dism.ExitCode == 0 ? "concluído" : "código " + dism.ExitCode) + ".");
                    report.AppendLine("DISM ScanHealth — código " + dism.ExitCode);
                    report.AppendLine(dism.Output);

                    AppendLog("Verificando arquivos protegidos do Windows...");
                    ProcessResult sfc = await Task.Run(delegate
                    {
                        return ProcessRunner.Run("sfc.exe", "/verifyonly", 45 * 60 * 1000);
                    });
                    AppendLog("SFC VerifyOnly: " + (sfc.ExitCode == 0 ? "nenhuma violação detectada" : "código " + sfc.ExitCode) + ".");
                    report.AppendLine("SFC VerifyOnly — código " + sfc.ExitCode);
                    report.AppendLine(sfc.Output);
                }

                if (_drivesCheck.Checked)
                {
                    string systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
                    AppendLog("Otimizando " + systemDrive + " com a ferramenta nativa do Windows...");
                    ProcessResult result = await Task.Run(delegate
                    {
                        return ProcessRunner.Run("defrag.exe", systemDrive + " /O /H /U /V", 60 * 60 * 1000);
                    });
                    string line = "Otimização da unidade: " + (result.ExitCode == 0 ? "concluída" : "código " + result.ExitCode) + ".";
                    AppendLog(line);
                    report.AppendLine(line);
                    report.AppendLine(result.Output);
                }

                Directory.CreateDirectory(DataDirectory);
                File.WriteAllText(LastRunFile, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                Directory.CreateDirectory(ReportDirectory);
                string reportPath = Path.Combine(ReportDirectory, "manutencao-" + DateTime.Now.ToString("yyyy-MM-dd-HHmmss") + ".txt");
                report.AppendLine(new string('-', 64));
                report.AppendLine("Espaço liberado diretamente: " + FormatBytes(freed));
                File.WriteAllText(reportPath, report.ToString(), new UTF8Encoding(true));

                AppendLog("Manutenção concluída. Espaço liberado diretamente: " + FormatBytes(freed));
                AppendLog("Relatório salvo em: " + reportPath);
                _analyzedBytes = Math.Max(0, _analyzedBytes - freed);
                _analysisValue.Text = FormatBytes(_analyzedBytes);
                LoadLastRun();
                UpdateSystemStatus();

                MessageBox.Show(
                    "Manutenção concluída.\n\nEspaço liberado diretamente: " + FormatBytes(freed) + "\nUm relatório detalhado foi salvo em Documentos.",
                    "Neck",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppendLog("A manutenção foi interrompida: " + ex.Message);
                MessageBox.Show("A manutenção foi interrompida. Consulte o relatório na tela.\n\n" + ex.Message,
                    "Neck", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private List<string> GetSelectedTasks()
        {
            List<string> tasks = new List<string>();
            if (_tempCheck.Checked) tasks.Add("Temporários seguros");
            if (_reportsCheck.Checked) tasks.Add("Relatórios de erro antigos");
            if (_recycleCheck.Checked) tasks.Add("Esvaziar Lixeira");
            if (_componentsCheck.Checked) tasks.Add("Limpeza de componentes do Windows");
            if (_healthCheck.Checked) tasks.Add("Verificação de integridade");
            if (_drivesCheck.Checked) tasks.Add("Otimização da unidade do sistema");
            return tasks;
        }

        private void SetBusy(bool busy, string status)
        {
            _busy = busy;
            _analyzeButton.Enabled = !busy && !_meetingActive;
            _runButton.Enabled = !busy && !_meetingActive;
            _advancedButton.Enabled = !busy && !_meetingActive;
            _driversButton.Enabled = !busy;
            _guardButton.Enabled = !busy;
            _meetingButton.Enabled = !busy;
            _progress.Style = busy ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            _progress.MarqueeAnimationSpeed = busy ? 25 : 0;
            if (!string.IsNullOrEmpty(status)) AppendLog(status);
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private void AppendLog(string line)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(AppendLog), line);
                return;
            }
            _log.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + line + Environment.NewLine);
            _log.SelectionStart = _log.TextLength;
            _log.ScrollToCaret();
        }

        internal static string FormatBytes(long bytes)
        {
            if (bytes < 0) bytes = 0;
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }
            return value.ToString(value >= 100 || unit == 0 ? "0" : value >= 10 ? "0.0" : "0.00", CultureInfo.CurrentCulture) + " " + units[unit];
        }

        internal static void OpenTarget(string target)
        {
            try { Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show("Não foi possível abrir:\n" + target + "\n\n" + ex.Message, "Neck", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }

    internal sealed class MeetingModeForm : Form
    {
        private readonly MeetingPreflight _preflight;
        private readonly ComboBox _duration = new ComboBox();

        public int DurationMinutes
        {
            get
            {
                if (_duration.SelectedIndex == 0) return 30;
                if (_duration.SelectedIndex == 2) return 120;
                return 60;
            }
        }

        public MeetingModeForm(MeetingPreflight preflight)
        {
            _preflight = preflight ?? new MeetingPreflight();
            Text = "Modo Reunião — Neck";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(820, 700);
            MinimumSize = new Size(760, 650);
            BackColor = Theme.Background;
            Font = Theme.Body;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            BuildInterface();
        }

        private void BuildInterface()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 116, BackColor = Theme.Navy };
            header.Controls.Add(new Label
            {
                Text = "Modo Reunião",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 22f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(28, 18)
            });
            header.Controls.Add(new Label
            {
                Text = "Prepare o computador antes de compartilhar sua tela",
                AutoSize = true,
                Font = Theme.Body,
                ForeColor = Color.FromArgb(186, 199, 218),
                Location = new Point(31, 67)
            });

            TableLayoutPanel body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 20, 24, 20),
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Theme.Background
            };
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));

            RoundedPanel summary = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                OutlineColor = Theme.Border,
                CornerRadius = 16,
                Margin = new Padding(0, 0, 0, 12)
            };
            int warnings = _preflight.Checks.Count(item => item.Status != MeetingCheckStatus.Ready);
            Color summaryColor = warnings == 0 ? Theme.Green : warnings >= 2 ? Theme.Amber : Theme.Blue;
            summary.Controls.Add(new Label
            {
                Text = warnings == 0 ? "PRONTO" : warnings + " ALERTA" + (warnings == 1 ? "" : "S"),
                AutoSize = false,
                Size = new Size(128, 31),
                BackColor = warnings == 0 ? Theme.GreenSoft : Color.FromArgb(255, 247, 237),
                ForeColor = summaryColor,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
                Location = new Point(20, 20)
            });
            summary.Controls.Add(new Label
            {
                Text = warnings == 0 ? "Seu computador está pronto para apresentar." : "Revise os alertas antes de começar.",
                AutoSize = true,
                Font = Theme.Heading,
                ForeColor = Theme.Text,
                Location = new Point(166, 20)
            });
            summary.Controls.Add(new Label
            {
                Text = "Ao ativar, o Neck impede suspensão e tela apagada e pausa suas próprias manutenções.",
                AutoSize = false,
                Size = new Size(660, 37),
                Font = Theme.Small,
                ForeColor = Theme.Muted,
                Location = new Point(22, 64)
            });

            ListView checklist = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                ForeColor = Theme.Text,
                Font = Theme.Body,
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };
            checklist.Columns.Add("Estado", 90, HorizontalAlignment.Center);
            checklist.Columns.Add("Verificação", 185);
            checklist.Columns.Add("Resultado", 425);
            foreach (MeetingCheck check in _preflight.Checks)
            {
                string state = check.Status == MeetingCheckStatus.Ready ? "PRONTO" : check.Status == MeetingCheckStatus.Warning ? "ATENÇÃO" : "RISCO";
                ListViewItem item = new ListViewItem(state);
                item.SubItems.Add(check.Title);
                item.SubItems.Add(check.Message);
                item.ForeColor = check.Status == MeetingCheckStatus.Ready ? Theme.Green :
                                     check.Status == MeetingCheckStatus.Warning ? Theme.Amber : Color.Firebrick;
                checklist.Items.Add(item);
            }

            Panel durationPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
            durationPanel.Controls.Add(new Label
            {
                Text = "Duração da proteção",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                ForeColor = Theme.Text,
                Location = new Point(4, 13)
            });
            _duration.DropDownStyle = ComboBoxStyle.DropDownList;
            _duration.Items.AddRange(new object[] { "30 minutos", "1 hora", "2 horas" });
            _duration.SelectedIndex = 1;
            _duration.Size = new Size(180, 32);
            _duration.Location = new Point(190, 9);
            durationPanel.Controls.Add(_duration);

            FlowLayoutPanel footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 10, 0, 0)
            };
            Button activate = new Button { Text = "Ativar Modo Reunião", DialogResult = DialogResult.OK };
            Button cancel = new Button { Text = "Agora não", DialogResult = DialogResult.Cancel };
            ConfigureMeetingButton(activate, Theme.Blue, 190);
            ConfigureMeetingButton(cancel, Theme.NavySoft, 120);
            footer.Controls.Add(activate);
            footer.Controls.Add(cancel);
            AcceptButton = activate;
            CancelButton = cancel;

            body.Controls.Add(summary, 0, 0);
            body.Controls.Add(checklist, 0, 1);
            body.Controls.Add(durationPanel, 0, 2);
            body.Controls.Add(footer, 0, 3);
            Controls.Add(body);
            Controls.Add(header);
        }

        private static void ConfigureMeetingButton(Button button, Color color, int width)
        {
            button.Width = width;
            button.Height = 42;
            button.BackColor = color;
            button.ForeColor = Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
            button.Margin = new Padding(10, 0, 0, 0);
            button.Cursor = Cursors.Hand;
        }
    }

    internal sealed class DiagnosticForm : Form
    {
        private readonly HealthSnapshot _snapshot;

        public DiagnosticForm(HealthSnapshot snapshot)
        {
            _snapshot = snapshot ?? new HealthSnapshot();
            Text = "Diagnóstico inteligente — Neck Guard";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(800, 650);
            MinimumSize = new Size(740, 600);
            BackColor = Theme.Background;
            Font = Theme.Body;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            BuildInterface();
        }

        private void BuildInterface()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 112, BackColor = Theme.Navy };
            header.Controls.Add(new Label
            {
                Text = "Neck Guard",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 22f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(28, 18)
            });
            header.Controls.Add(new Label
            {
                Text = "O que está pressionando seu computador agora",
                AutoSize = true,
                Font = Theme.Body,
                ForeColor = Color.FromArgb(186, 199, 218),
                Location = new Point(31, 65)
            });

            TableLayoutPanel body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 20, 24, 20),
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Theme.Background
            };
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));

            RoundedPanel summary = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                OutlineColor = Theme.Border,
                CornerRadius = 16,
                Padding = new Padding(20),
                Margin = new Padding(0, 0, 0, 12)
            };
            Color levelColor = _snapshot.Level == HealthLevel.Critical ? Color.Firebrick :
                               _snapshot.Level == HealthLevel.Warning ? Theme.Amber : Theme.Green;
            Label score = new Label
            {
                Text = _snapshot.Score.ToString(CultureInfo.CurrentCulture),
                AutoSize = false,
                Size = new Size(92, 72),
                Location = new Point(18, 21),
                Font = new Font("Segoe UI Semibold", 30f, FontStyle.Bold),
                ForeColor = levelColor,
                TextAlign = ContentAlignment.MiddleCenter
            };
            summary.Controls.Add(score);
            summary.Controls.Add(new Label
            {
                Text = _snapshot.Title,
                AutoSize = true,
                Font = Theme.Heading,
                ForeColor = Theme.Text,
                Location = new Point(126, 22)
            });
            summary.Controls.Add(new Label
            {
                Text = _snapshot.Summary,
                AutoSize = false,
                Size = new Size(565, 55),
                Font = Theme.Body,
                ForeColor = Theme.Muted,
                Location = new Point(128, 54)
            });

            Label metrics = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Memória em uso: " + _snapshot.Memory.PercentUsed.ToString("0", CultureInfo.CurrentCulture) + "%" +
                       "     •     Disponível: " + MainForm.FormatBytes((long)_snapshot.Memory.AvailableBytes) +
                       "     •     Disco livre: " + MainForm.FormatBytes(_snapshot.DiskFreeBytes),
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                ForeColor = Theme.Text,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 0, 0)
            };

            ListView processes = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                ForeColor = Theme.Text,
                Font = Theme.Body,
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };
            processes.Columns.Add("Aplicativo", 390);
            processes.Columns.Add("Processos", 110, HorizontalAlignment.Center);
            processes.Columns.Add("Memória", 150, HorizontalAlignment.Right);
            foreach (ResourceProcess process in _snapshot.TopProcesses)
            {
                ListViewItem item = new ListViewItem(process.DisplayName);
                item.SubItems.Add(process.ProcessCount.ToString(CultureInfo.CurrentCulture));
                item.SubItems.Add(MainForm.FormatBytes(process.MemoryBytes));
                processes.Items.Add(item);
            }

            FlowLayoutPanel footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 10, 0, 0)
            };
            Button close = new Button { Text = "Entendi", DialogResult = DialogResult.OK };
            close.Width = 130;
            close.Height = 42;
            close.BackColor = Theme.Blue;
            close.ForeColor = Color.White;
            close.FlatStyle = FlatStyle.Flat;
            close.FlatAppearance.BorderSize = 0;
            close.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
            close.Cursor = Cursors.Hand;
            footer.Controls.Add(close);
            footer.Controls.Add(new Label
            {
                Text = "Somente leitura: o Neck não encerrou nem alterou nenhum aplicativo.",
                AutoSize = false,
                Width = 510,
                Height = 42,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Theme.Muted,
                Font = Theme.Small
            });
            AcceptButton = close;

            body.Controls.Add(summary, 0, 0);
            body.Controls.Add(metrics, 0, 1);
            body.Controls.Add(processes, 0, 2);
            body.Controls.Add(footer, 0, 3);
            Controls.Add(body);
            Controls.Add(header);
        }
    }

    internal sealed class MaintenanceOptionsForm : Form
    {
        private readonly CheckBox _temp = new CheckBox();
        private readonly CheckBox _reports = new CheckBox();
        private readonly CheckBox _recycle = new CheckBox();
        private readonly CheckBox _components = new CheckBox();
        private readonly CheckBox _health = new CheckBox();
        private readonly CheckBox _drives = new CheckBox();

        public bool CleanTemp { get { return _temp.Checked; } }
        public bool CleanReports { get { return _reports.Checked; } }
        public bool EmptyRecycleBin { get { return _recycle.Checked; } }
        public bool CleanComponents { get { return _components.Checked; } }
        public bool CheckHealth { get { return _health.Checked; } }
        public bool OptimizeDrive { get { return _drives.Checked; } }

        public MaintenanceOptionsForm(bool temp, bool reports, bool recycle, bool components, bool health, bool drives)
        {
            Text = "Manutenção completa — Neck";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(760, 690);
            MinimumSize = new Size(720, 650);
            BackColor = Theme.Background;
            Font = Theme.Body;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;

            _temp.Checked = temp;
            _reports.Checked = reports;
            _recycle.Checked = recycle;
            _components.Checked = components;
            _health.Checked = health;
            _drives.Checked = drives;
            BuildInterface();
        }

        private void BuildInterface()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 104, BackColor = Theme.Navy };
            header.Controls.Add(new Label
            {
                Text = "Escolha o que o Neck deve fazer",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 21f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(28, 20)
            });
            header.Controls.Add(new Label
            {
                Text = "Nada será executado até você confirmar na próxima tela.",
                AutoSize = true,
                Font = Theme.Body,
                ForeColor = Color.FromArgb(186, 199, 218),
                Location = new Point(31, 62)
            });

            TableLayoutPanel body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 20, 24, 20),
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Theme.Background
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));

            Panel safeCard = CreateOptionCard("Limpeza", "Arquivos dispensáveis e antigos", Theme.Blue);
            Panel systemCard = CreateOptionCard("Sistema", "Ferramentas nativas do Windows", Theme.Green);
            safeCard.Margin = new Padding(0, 0, 10, 0);
            systemCard.Margin = new Padding(10, 0, 0, 0);

            FlowLayoutPanel safeList = OptionList();
            ConfigureOption(_temp, "Temporários seguros", "Preserva arquivos recentes e ignora itens em uso.");
            ConfigureOption(_reports, "Relatórios de erro antigos", "Remove dumps e relatórios com mais de 14 dias.");
            ConfigureOption(_recycle, "Esvaziar Lixeira", "Apaga definitivamente o conteúdo atual da Lixeira.");
            _recycle.ForeColor = Theme.Amber;
            safeList.Controls.AddRange(new Control[] { _temp, _reports, _recycle });
            safeCard.Controls.Add(safeList);

            FlowLayoutPanel systemList = OptionList();
            ConfigureOption(_components, "Componentes do Windows", "Remove versões substituídas usando DISM.");
            ConfigureOption(_health, "Verificar integridade", "Executa DISM ScanHealth e SFC VerifyOnly.");
            ConfigureOption(_drives, "Otimizar unidade", "Usa TRIM para SSD ou desfragmentação para HD.");
            systemList.Controls.AddRange(new Control[] { _components, _health, _drives });
            systemCard.Controls.Add(systemList);

            FlowLayoutPanel footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 13, 0, 0)
            };
            Button run = new Button { Text = "Continuar", DialogResult = DialogResult.OK };
            Button cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel };
            ConfigureDialogButton(run, Theme.Blue, 150);
            ConfigureDialogButton(cancel, Theme.NavySoft, 118);
            footer.Controls.Add(run);
            footer.Controls.Add(cancel);
            AcceptButton = run;
            CancelButton = cancel;

            body.Controls.Add(safeCard, 0, 0);
            body.Controls.Add(systemCard, 1, 0);
            body.SetColumnSpan(footer, 2);
            body.Controls.Add(footer, 0, 1);
            Controls.Add(body);
            Controls.Add(header);
        }

        private static Panel CreateOptionCard(string title, string subtitle, Color accent)
        {
            RoundedPanel card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                OutlineColor = Theme.Border,
                CornerRadius = 16,
                Padding = new Padding(20, 78, 20, 18)
            };
            card.Controls.Add(new Label
            {
                Text = title,
                AutoSize = true,
                Font = Theme.Heading,
                ForeColor = Theme.Text,
                Location = new Point(20, 19)
            });
            card.Controls.Add(new Label
            {
                Text = subtitle,
                AutoSize = true,
                Font = Theme.Small,
                ForeColor = accent,
                Location = new Point(22, 51)
            });
            return card;
        }

        private static FlowLayoutPanel OptionList()
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = false,
                BackColor = Color.White
            };
        }

        private static void ConfigureOption(CheckBox box, string title, string description)
        {
            box.AutoSize = false;
            box.Width = 295;
            box.Height = 98;
            box.Text = title + Environment.NewLine + description;
            box.Font = Theme.Body;
            box.ForeColor = Theme.Text;
            box.CheckAlign = ContentAlignment.TopLeft;
            box.TextAlign = ContentAlignment.TopLeft;
            box.Padding = new Padding(2, 5, 2, 2);
            box.Margin = new Padding(0, 0, 0, 6);
        }

        private static void ConfigureDialogButton(Button button, Color color, int width)
        {
            button.Width = width;
            button.Height = 42;
            button.BackColor = color;
            button.ForeColor = Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
            button.Margin = new Padding(10, 0, 0, 0);
            button.Cursor = Cursors.Hand;
        }
    }

    internal sealed class DriverCenterForm : Form
    {
        private readonly RichTextBox _versions = new RichTextBox();
        private readonly Button _restoreButton = new Button();

        public DriverCenterForm()
        {
            Text = "Drivers e atualizações — Neck";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(820, 650);
            MinimumSize = new Size(760, 600);
            BackColor = Theme.Background;
            Font = Theme.Body;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            BuildInterface();
            Shown += async delegate { await LoadVersionsAsync(); };
        }

        private void BuildInterface()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 106, BackColor = Theme.Navy, Padding = new Padding(28, 20, 28, 16) };
            header.Controls.Add(new Label { Text = "Drivers e atualizações", AutoSize = true, Font = new Font("Segoe UI Semibold", 21f, FontStyle.Bold), ForeColor = Color.White, Location = new Point(27, 18) });
            header.Controls.Add(new Label { Text = "Somente fontes oficiais. Revise as atualizações antes de instalar.", AutoSize = true, Font = Theme.Body, ForeColor = Color.FromArgb(194, 207, 225), Location = new Point(30, 61) });

            TableLayoutPanel body = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 2, RowCount = 2 };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));

            Panel sources = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(20), BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 0, 10, 0) };
            Label title = new Label { Text = "Canais recomendados", Dock = DockStyle.Top, Height = 38, Font = Theme.Heading, ForeColor = Theme.Text };
            FlowLayoutPanel sourceButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            sourceButtons.Controls.Add(MakeSourceButton("Windows Update", "Atualizações do Windows e drivers homologados", Theme.Blue, delegate { MainForm.OpenTarget("ms-settings:windowsupdate"); }));
            sourceButtons.Controls.Add(MakeSourceButton("Atualizações opcionais", "Drivers adicionais oferecidos pela Microsoft", Theme.NavySoft, delegate { MainForm.OpenTarget("ms-settings:windowsupdate-optionalupdates"); }));
            sourceButtons.Controls.Add(MakeSourceButton("Intel Driver & Support Assistant", "Verificação oficial da Intel", Theme.Green, delegate { MainForm.OpenTarget("https://www.intel.com/content/www/br/pt/support/detect.html"); }));
            sourceButtons.Controls.Add(MakeSourceButton("NVIDIA App", "Abrir o aplicativo NVIDIA ou o site oficial", Color.FromArgb(88, 143, 38), OpenNvidia));
            sourceButtons.Controls.Add(MakeSourceButton("Suporte HP", "Drivers e BIOS oficiais para notebooks HP", Theme.Amber, delegate { MainForm.OpenTarget("https://support.hp.com/br-pt/drivers/laptops"); }));
            sources.Controls.Add(sourceButtons);
            sources.Controls.Add(title);

            Panel installed = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(20), BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(10, 0, 0, 0) };
            installed.Controls.Add(_versions);
            installed.Controls.Add(new Label { Text = "Versões instaladas", Dock = DockStyle.Top, Height = 38, Font = Theme.Heading, ForeColor = Theme.Text });
            _versions.Dock = DockStyle.Fill;
            _versions.BorderStyle = BorderStyle.None;
            _versions.BackColor = Color.White;
            _versions.ForeColor = Theme.Text;
            _versions.Font = new Font("Consolas", 9.3f);
            _versions.ReadOnly = true;
            _versions.Text = "Consultando o hardware...";

            FlowLayoutPanel footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 12, 0, 0) };
            ConfigureFooterButton(_restoreButton, "Criar ponto de restauração", Theme.Green, 240);
            Button close = new Button();
            ConfigureFooterButton(close, "Fechar", Theme.NavySoft, 130);
            _restoreButton.Click += async delegate { await CreateRestorePointAsync(); };
            close.Click += delegate { Close(); };
            footer.Controls.Add(_restoreButton);
            footer.Controls.Add(close);

            body.Controls.Add(sources, 0, 0);
            body.Controls.Add(installed, 1, 0);
            body.SetColumnSpan(footer, 2);
            body.Controls.Add(footer, 0, 1);

            Controls.Add(body);
            Controls.Add(header);
        }

        private static Button MakeSourceButton(string title, string subtitle, Color color, EventHandler click)
        {
            Button button = new Button
            {
                Width = 300,
                Height = 66,
                Text = title + Environment.NewLine + subtitle,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 8, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = color,
                ForeColor = Color.White,
                Font = Theme.Small,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 9)
            };
            button.FlatAppearance.BorderSize = 0;
            button.Click += click;
            return button;
        }

        private static void ConfigureFooterButton(Button button, string text, Color color, int width)
        {
            button.Text = text;
            button.Width = width;
            button.Height = 42;
            button.BackColor = color;
            button.ForeColor = Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
            button.Margin = new Padding(0, 0, 10, 0);
            button.Cursor = Cursors.Hand;
        }

        private async Task LoadVersionsAsync()
        {
            string command = "$ErrorActionPreference='SilentlyContinue'; " +
                "$g=Get-CimInstance Win32_PnPSignedDriver | Where-Object {$_.DeviceClass -eq 'DISPLAY'} | Sort-Object DeviceName -Unique; " +
                "$b=Get-CimInstance Win32_BIOS; " +
                "'VIDEO'; $g | ForEach-Object {$_.DeviceName; '  Versão: '+$_.DriverVersion; '  Data: '+$_.DriverDate.ToString('dd/MM/yyyy'); ''}; " +
                "'BIOS'; $b.Manufacturer; '  Versão: '+$b.SMBIOSBIOSVersion; '  Data: '+$b.ReleaseDate.ToString('dd/MM/yyyy')";
            ProcessResult result = await Task.Run(delegate
            {
                return ProcessRunner.Run(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"),
                    "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + command.Replace("\"", "\\\"") + "\"", 120000);
            });
            _versions.Text = string.IsNullOrWhiteSpace(result.Output) ? "Não foi possível consultar as versões instaladas." : result.Output.Trim();
        }

        private async Task CreateRestorePointAsync()
        {
            _restoreButton.Enabled = false;
            _restoreButton.Text = "Criando...";
            try
            {
                string script = "Checkpoint-Computer -Description 'Neck - antes dos drivers' -RestorePointType MODIFY_SETTINGS";
                ProcessResult result = await Task.Run(delegate
                {
                    return ProcessRunner.Run(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"),
                        "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + script + "\"", 180000);
                });
                MessageBox.Show(result.ExitCode == 0 ? "Ponto de restauração criado." : "O Windows não criou o ponto de restauração. A Proteção do Sistema pode estar desativada.\n\n" + result.Output,
                    "Neck", MessageBoxButtons.OK, result.ExitCode == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            finally
            {
                _restoreButton.Enabled = true;
                _restoreButton.Text = "Criar ponto de restauração";
            }
        }

        private static void OpenNvidia(object sender, EventArgs e)
        {
            string[] candidates =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NVIDIA Corporation", "NVIDIA app", "CEF", "NVIDIA app.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NVIDIA Corporation", "NVIDIA GeForce Experience", "NVIDIA GeForce Experience.exe")
            };
            string installed = candidates.FirstOrDefault(File.Exists);
            MainForm.OpenTarget(installed ?? "https://www.nvidia.com/pt-br/software/nvidia-app/");
        }
    }

    internal static class Cleaner
    {
        private static readonly TimeSpan TempAge = TimeSpan.FromDays(2);
        private static readonly TimeSpan WindowsTempAge = TimeSpan.FromDays(7);
        private static readonly TimeSpan ReportAge = TimeSpan.FromDays(14);

        public static ScanResult Analyze()
        {
            ScanResult total = new ScanResult();
            foreach (PathRule rule in TempRules())
            {
                ScanResult part = Scan(rule);
                total.TempBytes += part.TotalBytes;
                total.TempFiles += part.TotalFiles;
                total.AccessErrors += part.AccessErrors;
            }
            foreach (PathRule rule in ReportRules())
            {
                ScanResult part = Scan(rule);
                total.ReportBytes += part.TotalBytes;
                total.ReportFiles += part.TotalFiles;
                total.AccessErrors += part.AccessErrors;
            }
            return total;
        }

        public static DeleteResult CleanTemp()
        {
            return Clean(TempRules());
        }

        public static DeleteResult CleanReports()
        {
            return Clean(ReportRules());
        }

        private static IEnumerable<PathRule> TempRules()
        {
            yield return new PathRule(Path.GetTempPath(), TempAge);
            yield return new PathRule(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"), WindowsTempAge);
        }

        private static IEnumerable<PathRule> ReportRules()
        {
            yield return new PathRule(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps"), ReportAge);
            yield return new PathRule(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows", "WER", "ReportArchive"), ReportAge);
            yield return new PathRule(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows", "WER", "ReportQueue"), ReportAge);
        }

        private static ScanResult Scan(PathRule rule)
        {
            ScanResult result = new ScanResult();
            DateTime cutoff = DateTime.UtcNow.Subtract(rule.MinimumAge);
            foreach (string file in SafeFiles(rule.Path, result))
            {
                try
                {
                    FileInfo info = new FileInfo(file);
                    if (info.LastWriteTimeUtc < cutoff)
                    {
                        result.TotalBytes += info.Length;
                        result.TotalFiles++;
                    }
                }
                catch { result.AccessErrors++; }
            }
            return result;
        }

        private static DeleteResult Clean(IEnumerable<PathRule> rules)
        {
            DeleteResult result = new DeleteResult();
            foreach (PathRule rule in rules)
            {
                DateTime cutoff = DateTime.UtcNow.Subtract(rule.MinimumAge);
                ScanResult scanState = new ScanResult();
                foreach (string file in SafeFiles(rule.Path, scanState))
                {
                    try
                    {
                        FileInfo info = new FileInfo(file);
                        if (info.LastWriteTimeUtc >= cutoff) continue;
                        long length = info.Length;
                        if (info.IsReadOnly) info.IsReadOnly = false;
                        info.Delete();
                        result.BytesDeleted += length;
                        result.FilesDeleted++;
                    }
                    catch { result.Errors++; }
                }
                result.Errors += scanState.AccessErrors;
            }
            return result;
        }

        private static IEnumerable<string> SafeFiles(string root, ScanResult state)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) yield break;
            Stack<string> pending = new Stack<string>();
            pending.Push(root);

            while (pending.Count > 0)
            {
                string current = pending.Pop();
                string[] files;
                try { files = Directory.GetFiles(current); }
                catch { state.AccessErrors++; continue; }

                foreach (string file in files) yield return file;

                string[] directories;
                try { directories = Directory.GetDirectories(current); }
                catch { state.AccessErrors++; continue; }

                foreach (string directory in directories)
                {
                    try
                    {
                        FileAttributes attributes = File.GetAttributes(directory);
                        if ((attributes & FileAttributes.ReparsePoint) == 0) pending.Push(directory);
                    }
                    catch { state.AccessErrors++; }
                }
            }
        }
    }

    internal sealed class PathRule
    {
        public string Path { get; private set; }
        public TimeSpan MinimumAge { get; private set; }
        public PathRule(string path, TimeSpan minimumAge) { Path = path; MinimumAge = minimumAge; }
    }

    internal sealed class ScanResult
    {
        public long TempBytes;
        public int TempFiles;
        public long ReportBytes;
        public int ReportFiles;
        public int AccessErrors;
        public long TotalBytes { get { return TempBytes + ReportBytes; } set { TempBytes = value; ReportBytes = 0; } }
        public int TotalFiles { get { return TempFiles + ReportFiles; } set { TempFiles = value; ReportFiles = 0; } }
    }

    internal sealed class DeleteResult
    {
        public long BytesDeleted;
        public int FilesDeleted;
        public int Errors;
    }

    internal sealed class ProcessResult
    {
        public int ExitCode;
        public string Output = string.Empty;
    }

    internal static class ProcessRunner
    {
        public static ProcessResult Run(string fileName, string arguments, int timeoutMilliseconds)
        {
            ProcessResult result = new ProcessResult();
            StringBuilder output = new StringBuilder();
            using (Process process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                if (!process.WaitForExit(timeoutMilliseconds))
                {
                    try { process.Kill(); } catch { }
                    result.ExitCode = -2;
                    result.Output = "Tempo limite excedido.";
                    return result;
                }
                process.WaitForExit();
                result.ExitCode = process.ExitCode;
                lock (output) result.Output = output.ToString();
                return result;
            }
        }
    }

    internal static class SecurityHelper
    {
        public static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
    }

    internal struct MemoryStatus
    {
        public double PercentUsed;
        public ulong AvailableBytes;
    }

    internal enum HealthLevel
    {
        Stable,
        Warning,
        Critical
    }

    internal sealed class ResourceProcess
    {
        public string DisplayName;
        public int ProcessCount;
        public long MemoryBytes;
    }

    internal sealed class HealthSnapshot
    {
        public int Score;
        public HealthLevel Level;
        public string Title = "Diagnóstico indisponível";
        public string Summary = "Não foi possível concluir a leitura agora.";
        public MemoryStatus Memory;
        public long DiskFreeBytes;
        public long DiskTotalBytes;
        public List<ResourceProcess> TopProcesses = new List<ResourceProcess>();
    }

    internal enum MeetingCheckStatus
    {
        Ready,
        Warning,
        Risk
    }

    internal sealed class MeetingCheck
    {
        public MeetingCheckStatus Status;
        public string Title;
        public string Message;
    }

    internal sealed class MeetingPreflight
    {
        public HealthSnapshot Health = new HealthSnapshot();
        public List<MeetingCheck> Checks = new List<MeetingCheck>();
    }

    internal static class SystemInfo
    {
        public static MemoryStatus GetMemoryStatus()
        {
            NativeMethods.MEMORYSTATUSEX data = new NativeMethods.MEMORYSTATUSEX();
            data.dwLength = (uint)Marshal.SizeOf(typeof(NativeMethods.MEMORYSTATUSEX));
            if (!NativeMethods.GlobalMemoryStatusEx(ref data)) return new MemoryStatus();
            return new MemoryStatus { PercentUsed = data.dwMemoryLoad, AvailableBytes = data.ullAvailPhys };
        }

        public static HealthSnapshot GetHealthSnapshot()
        {
            HealthSnapshot snapshot = new HealthSnapshot();
            snapshot.Memory = GetMemoryStatus();

            try
            {
                string root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
                DriveInfo drive = new DriveInfo(root);
                snapshot.DiskFreeBytes = drive.AvailableFreeSpace;
                snapshot.DiskTotalBytes = drive.TotalSize;
            }
            catch { }

            Dictionary<string, ResourceProcess> grouped = new Dictionary<string, ResourceProcess>(StringComparer.OrdinalIgnoreCase);
            string currentName = Process.GetCurrentProcess().ProcessName;
            foreach (Process process in Process.GetProcesses())
            {
                using (process)
                {
                    try
                    {
                        string name = process.ProcessName;
                        if (string.Equals(name, currentName, StringComparison.OrdinalIgnoreCase)) continue;
                        long memory = Math.Max(0, process.WorkingSet64);
                        ResourceProcess item;
                        if (!grouped.TryGetValue(name, out item))
                        {
                            item = new ResourceProcess { DisplayName = FriendlyProcessName(name) };
                            grouped.Add(name, item);
                        }
                        item.ProcessCount++;
                        item.MemoryBytes += memory;
                    }
                    catch { }
                }
            }
            snapshot.TopProcesses = grouped.Values.OrderByDescending(item => item.MemoryBytes).Take(5).ToList();

            bool diskCritical = snapshot.DiskTotalBytes > 0 &&
                                (snapshot.DiskFreeBytes < 2L * 1024 * 1024 * 1024 ||
                                 snapshot.DiskFreeBytes * 100 / snapshot.DiskTotalBytes < 5);
            bool diskWarning = snapshot.DiskTotalBytes > 0 && snapshot.DiskFreeBytes < 15L * 1024 * 1024 * 1024;
            if (snapshot.Memory.PercentUsed >= 90 || diskCritical)
                snapshot.Level = HealthLevel.Critical;
            else if (snapshot.Memory.PercentUsed >= 75 || diskWarning)
                snapshot.Level = HealthLevel.Warning;
            else
                snapshot.Level = HealthLevel.Stable;

            int memoryPenalty = (int)Math.Max(0, (snapshot.Memory.PercentUsed - 55) * 1.35);
            int diskPenalty = diskCritical ? 30 : diskWarning ? 15 : 0;
            snapshot.Score = Math.Max(10, Math.Min(100, 100 - memoryPenalty - diskPenalty));
            ResourceProcess top = snapshot.TopProcesses.FirstOrDefault();

            if (snapshot.Level == HealthLevel.Critical)
            {
                snapshot.Title = "Pressão alta detectada";
                snapshot.Summary = snapshot.Memory.PercentUsed >= 90
                    ? "A memória está quase cheia. " + TopProcessSentence(top)
                    : "O disco do Windows está praticamente cheio e pode deixar todo o sistema lento.";
            }
            else if (snapshot.Level == HealthLevel.Warning)
            {
                snapshot.Title = "O computador merece atenção";
                snapshot.Summary = snapshot.Memory.PercentUsed >= 75
                    ? "O uso de memória está elevado. " + TopProcessSentence(top)
                    : "Há pouco espaço livre no disco do Windows; uma limpeza segura pode ajudar.";
            }
            else
            {
                snapshot.Title = "Sistema estável agora";
                snapshot.Summary = "Não encontramos pressão crítica de memória ou disco. " + TopProcessSentence(top);
            }
            return snapshot;
        }

        public static MeetingPreflight GetMeetingPreflight()
        {
            MeetingPreflight preflight = new MeetingPreflight();
            preflight.Health = GetHealthSnapshot();
            HealthSnapshot health = preflight.Health;

            preflight.Checks.Add(new MeetingCheck
            {
                Status = health.Memory.PercentUsed >= 90 ? MeetingCheckStatus.Risk :
                         health.Memory.PercentUsed >= 75 ? MeetingCheckStatus.Warning : MeetingCheckStatus.Ready,
                Title = "Memória RAM",
                Message = health.Memory.PercentUsed.ToString("0", CultureInfo.CurrentCulture) + "% em uso; " +
                          MainForm.FormatBytes((long)health.Memory.AvailableBytes) + " disponíveis."
            });

            bool diskRisk = health.DiskTotalBytes > 0 &&
                            (health.DiskFreeBytes < 2L * 1024 * 1024 * 1024 || health.DiskFreeBytes * 100 / health.DiskTotalBytes < 5);
            bool diskWarning = health.DiskTotalBytes > 0 && health.DiskFreeBytes < 15L * 1024 * 1024 * 1024;
            preflight.Checks.Add(new MeetingCheck
            {
                Status = diskRisk ? MeetingCheckStatus.Risk : diskWarning ? MeetingCheckStatus.Warning : MeetingCheckStatus.Ready,
                Title = "Disco do Windows",
                Message = MainForm.FormatBytes(health.DiskFreeBytes) + " livres para arquivos e memória virtual."
            });

            bool restartPending = IsRestartPending();
            preflight.Checks.Add(new MeetingCheck
            {
                Status = restartPending ? MeetingCheckStatus.Warning : MeetingCheckStatus.Ready,
                Title = "Reinicialização",
                Message = restartPending ? "O Windows indica uma reinicialização pendente." : "Nenhuma reinicialização pendente foi detectada."
            });

            bool networkAvailable = false;
            try { networkAvailable = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable(); }
            catch { }
            preflight.Checks.Add(new MeetingCheck
            {
                Status = networkAvailable ? MeetingCheckStatus.Ready : MeetingCheckStatus.Warning,
                Title = "Rede",
                Message = networkAvailable ? "Uma conexão de rede ativa foi detectada." : "Nenhuma conexão de rede ativa foi detectada."
            });

            PowerStatus power = SystemInformation.PowerStatus;
            bool onBattery = power.PowerLineStatus == PowerLineStatus.Offline;
            string powerMessage;
            if (onBattery)
            {
                int battery = power.BatteryLifePercent >= 0 && power.BatteryLifePercent <= 1
                    ? (int)Math.Round(power.BatteryLifePercent * 100) : 0;
                powerMessage = "Usando bateria" + (battery > 0 ? " com " + battery + "% de carga." : ".");
            }
            else if (power.PowerLineStatus == PowerLineStatus.Online)
                powerMessage = "O computador está conectado à energia.";
            else
                powerMessage = "Estado de energia não aplicável ou não informado.";
            preflight.Checks.Add(new MeetingCheck
            {
                Status = onBattery ? MeetingCheckStatus.Warning : MeetingCheckStatus.Ready,
                Title = "Energia",
                Message = powerMessage
            });

            ResourceProcess top = health.TopProcesses.FirstOrDefault();
            bool heavy = top != null && top.MemoryBytes >= 3L * 1024 * 1024 * 1024;
            preflight.Checks.Add(new MeetingCheck
            {
                Status = heavy ? MeetingCheckStatus.Warning : MeetingCheckStatus.Ready,
                Title = "Aplicativos pesados",
                Message = top == null ? "Nenhum aplicativo pôde ser comparado." :
                          top.DisplayName + " lidera o uso com aproximadamente " + MainForm.FormatBytes(top.MemoryBytes) + "."
            });
            return preflight;
        }

        private static bool IsRestartPending()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey cbs = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"))
                    if (cbs != null) return true;
                using (Microsoft.Win32.RegistryKey update = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired"))
                    if (update != null) return true;
                using (Microsoft.Win32.RegistryKey session = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\Session Manager"))
                    if (session != null && session.GetValue("PendingFileRenameOperations") != null) return true;
            }
            catch { }
            return false;
        }

        private static string TopProcessSentence(ResourceProcess process)
        {
            return process == null
                ? "Nenhum aplicativo pôde ser comparado neste momento."
                : process.DisplayName + " é o maior consumidor de memória agora.";
        }

        private static string FriendlyProcessName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Processo desconhecido";
            if (string.Equals(name, "msedge", StringComparison.OrdinalIgnoreCase)) return "Microsoft Edge";
            if (string.Equals(name, "chrome", StringComparison.OrdinalIgnoreCase)) return "Google Chrome";
            if (string.Equals(name, "firefox", StringComparison.OrdinalIgnoreCase)) return "Mozilla Firefox";
            if (string.Equals(name, "explorer", StringComparison.OrdinalIgnoreCase)) return "Explorador do Windows";
            if (string.Equals(name, "Teams", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "ms-teams", StringComparison.OrdinalIgnoreCase)) return "Microsoft Teams";
            if (string.Equals(name, "Code", StringComparison.OrdinalIgnoreCase)) return "Visual Studio Code";
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.Replace('_', ' '));
        }
    }

    internal static class NativeMethods
    {
        public const uint SHERB_NOCONFIRMATION = 0x00000001;
        public const uint SHERB_NOPROGRESSUI = 0x00000002;
        public const uint SHERB_NOSOUND = 0x00000004;
        public const uint ES_CONTINUOUS = 0x80000000;
        public const uint ES_SYSTEM_REQUIRED = 0x00000001;
        public const uint ES_DISPLAY_REQUIRED = 0x00000002;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint SetThreadExecutionState(uint esFlags);

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);
    }
}
