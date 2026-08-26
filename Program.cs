using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Mestre PC Care")]
[assembly: System.Reflection.AssemblyDescription("Manutenção periódica segura para Windows")]
[assembly: System.Reflection.AssemblyCompany("Mestre PC Care")]
[assembly: System.Reflection.AssemblyProduct("Mestre PC Care")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]

namespace MestrePCCare
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
                        "Mestre PC Care",
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
        public static readonly Color Background = Color.FromArgb(244, 247, 251);
        public static readonly Color Card = Color.White;
        public static readonly Color Navy = Color.FromArgb(19, 35, 58);
        public static readonly Color NavySoft = Color.FromArgb(43, 61, 86);
        public static readonly Color Blue = Color.FromArgb(35, 108, 228);
        public static readonly Color Green = Color.FromArgb(20, 148, 105);
        public static readonly Color Amber = Color.FromArgb(224, 139, 35);
        public static readonly Color Text = Color.FromArgb(34, 43, 56);
        public static readonly Color Muted = Color.FromArgb(101, 113, 130);
        public static readonly Color Border = Color.FromArgb(222, 228, 236);
        public static readonly Font Title = new Font("Segoe UI Semibold", 24f, FontStyle.Bold);
        public static readonly Font Heading = new Font("Segoe UI Semibold", 13f, FontStyle.Bold);
        public static readonly Font Body = new Font("Segoe UI", 10f, FontStyle.Regular);
        public static readonly Font Small = new Font("Segoe UI", 9f, FontStyle.Regular);
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
        private readonly Button _driversButton = new Button();
        private readonly Button _reportsButton = new Button();
        private readonly CheckBox _tempCheck = new CheckBox();
        private readonly CheckBox _reportsCheck = new CheckBox();
        private readonly CheckBox _recycleCheck = new CheckBox();
        private readonly CheckBox _componentsCheck = new CheckBox();
        private readonly CheckBox _healthCheck = new CheckBox();
        private readonly CheckBox _drivesCheck = new CheckBox();
        private readonly Timer _statusTimer = new Timer();
        private long _analyzedBytes;
        private bool _busy;

        private static readonly string DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MestrePCCare");
        private static readonly string LastRunFile = Path.Combine(DataDirectory, "ultima-manutencao.txt");
        private static readonly string ReportDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Mestre PC Care", "Relatorios");

        public MainForm()
        {
            Text = "Mestre PC Care";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1040, 720);
            Size = new Size(1120, 780);
            BackColor = Theme.Background;
            Font = Theme.Body;
            Icon = SystemIcons.Shield;

            BuildInterface();
            UpdateSystemStatus();
            LoadLastRun();

            _statusTimer.Interval = 3000;
            _statusTimer.Tick += delegate { if (!_busy) UpdateSystemStatus(); };
            _statusTimer.Start();

            Shown += async delegate { await AnalyzeAsync(false); };
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
                Height = 128,
                BackColor = Theme.Navy,
                Padding = new Padding(34, 22, 34, 18)
            };

            Label title = new Label
            {
                AutoSize = true,
                Text = "Mestre PC Care",
                Font = Theme.Title,
                ForeColor = Color.White,
                Location = new Point(32, 22)
            };
            Label subtitle = new Label
            {
                AutoSize = true,
                Text = "Manutenção periódica segura • análise antes de qualquer limpeza",
                Font = Theme.Body,
                ForeColor = Color.FromArgb(194, 207, 225),
                Location = new Point(36, 69)
            };
            _recommendation.AutoSize = false;
            _recommendation.Size = new Size(310, 55);
            _recommendation.TextAlign = ContentAlignment.MiddleCenter;
            _recommendation.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
            _recommendation.ForeColor = Color.White;
            _recommendation.BackColor = Theme.NavySoft;
            _recommendation.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _recommendation.Location = new Point(ClientSize.Width - 350, 31);
            header.Resize += delegate { _recommendation.Left = header.ClientSize.Width - _recommendation.Width - 34; };

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
                Padding = new Padding(26, 20, 26, 24),
                ColumnCount = 2,
                RowCount = 1
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47f));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53f));
            body.Controls.Add(BuildActionsCard(), 0, 0);
            body.Controls.Add(BuildStatusColumn(), 1, 0);
            return body;
        }

        private Control BuildActionsCard()
        {
            Panel card = MakeCard(new Padding(24));
            card.Margin = new Padding(0, 0, 12, 0);

            Label heading = MakeHeading("O que deseja verificar?");
            heading.Dock = DockStyle.Top;
            heading.Height = 34;

            Label help = new Label
            {
                Text = "As opções recomendadas não apagam documentos, fotos, senhas ou arquivos pessoais.",
                ForeColor = Theme.Muted,
                Font = Theme.Small,
                Dock = DockStyle.Top,
                Height = 44
            };

            FlowLayoutPanel tasks = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(0, 8, 0, 0)
            };

            ConfigureCheck(_tempCheck, "Temporários seguros", "Arquivos antigos das pastas temporárias do usuário e do Windows.", true);
            ConfigureCheck(_reportsCheck, "Relatórios de erro antigos", "Dumps e relatórios do Windows com mais de 14 dias.", true);
            ConfigureCheck(_recycleCheck, "Esvaziar Lixeira", "Apaga definitivamente o conteúdo atual da Lixeira.", false);
            ConfigureCheck(_componentsCheck, "Limpeza de componentes do Windows", "DISM remove versões substituídas de componentes. Pode demorar.", true);
            ConfigureCheck(_healthCheck, "Verificar integridade do Windows", "Executa DISM ScanHealth e SFC VerifyOnly, sem reparar automaticamente.", false);
            ConfigureCheck(_drivesCheck, "Otimizar unidade do sistema", "O Windows escolhe TRIM para SSD ou desfragmentação para HD.", true);

            tasks.Controls.AddRange(new Control[]
            {
                _tempCheck, _reportsCheck, _recycleCheck,
                _componentsCheck, _healthCheck, _drivesCheck
            });

            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 102,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Padding = new Padding(0, 12, 0, 0)
            };
            ConfigureButton(_analyzeButton, "Analisar", Theme.NavySoft, 136);
            ConfigureButton(_runButton, "Executar selecionados", Theme.Blue, 224);
            _analyzeButton.Click += async delegate { await AnalyzeAsync(true); };
            _runButton.Click += async delegate { await RunMaintenanceAsync(); };
            buttons.Controls.Add(_analyzeButton);
            buttons.Controls.Add(_runButton);

            card.Controls.Add(tasks);
            card.Controls.Add(help);
            card.Controls.Add(heading);
            card.Controls.Add(buttons);
            return card;
        }

        private Control BuildStatusColumn()
        {
            TableLayoutPanel right = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(12, 0, 0, 0),
                ColumnCount = 1,
                RowCount = 3
            };
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 152));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            right.Controls.Add(BuildSummaryCard(), 0, 0);
            right.Controls.Add(BuildLogCard(), 0, 1);
            right.Controls.Add(BuildFooterButtons(), 0, 2);
            return right;
        }

        private Control BuildSummaryCard()
        {
            Panel card = MakeCard(new Padding(20));
            card.Margin = new Padding(0, 0, 0, 12);

            TableLayoutPanel grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 2
            };
            for (int i = 0; i < 4; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 62));

            AddMetric(grid, 0, "RAM EM USO", _memoryValue);
            AddMetric(grid, 1, "LIVRE NO DISCO", _diskValue);
            AddMetric(grid, 2, "PODE LIBERAR", _analysisValue);
            AddMetric(grid, 3, "ÚLTIMA MANUTENÇÃO", _lastRunValue);
            card.Controls.Add(grid);
            return card;
        }

        private Control BuildLogCard()
        {
            Panel card = MakeCard(new Padding(20));
            card.Margin = new Padding(0, 0, 0, 10);
            Label heading = MakeHeading("Relatório da execução");
            heading.Dock = DockStyle.Top;
            heading.Height = 35;

            _progress.Dock = DockStyle.Bottom;
            _progress.Height = 8;
            _progress.Style = ProgressBarStyle.Continuous;

            _log.Dock = DockStyle.Fill;
            _log.BorderStyle = BorderStyle.None;
            _log.BackColor = Color.White;
            _log.ForeColor = Theme.Text;
            _log.Font = new Font("Consolas", 9.2f, FontStyle.Regular);
            _log.ReadOnly = true;
            _log.DetectUrls = false;
            _log.Text = "Pronto para analisar. Nenhum arquivo será removido durante a análise.\n";

            card.Controls.Add(_log);
            card.Controls.Add(heading);
            card.Controls.Add(_progress);
            return card;
        }

        private Control BuildFooterButtons()
        {
            FlowLayoutPanel footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 8, 0, 0)
            };
            ConfigureButton(_driversButton, "Drivers e atualizações", Theme.Green, 224);
            ConfigureButton(_reportsButton, "Abrir relatórios", Theme.NavySoft, 170);
            _driversButton.Click += delegate { using (DriverCenterForm form = new DriverCenterForm()) form.ShowDialog(this); };
            _reportsButton.Click += delegate
            {
                Directory.CreateDirectory(ReportDirectory);
                OpenTarget(ReportDirectory);
            };
            footer.Controls.Add(_driversButton);
            footer.Controls.Add(_reportsButton);
            return footer;
        }

        private static Panel MakeCard(Padding padding)
        {
            Panel panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                Padding = padding,
                BorderStyle = BorderStyle.FixedSingle
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
            value.Font = new Font("Segoe UI Semibold", 15f, FontStyle.Bold);
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
        }

        private void UpdateSystemStatus()
        {
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
            List<string> selected = GetSelectedTasks();
            if (selected.Count == 0)
            {
                MessageBox.Show("Marque pelo menos uma opção.", "Mestre PC Care", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            report.AppendLine("Mestre PC Care — Relatório de manutenção");
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
                    "Mestre PC Care",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppendLog("A manutenção foi interrompida: " + ex.Message);
                MessageBox.Show("A manutenção foi interrompida. Consulte o relatório na tela.\n\n" + ex.Message,
                    "Mestre PC Care", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            _analyzeButton.Enabled = !busy;
            _runButton.Enabled = !busy;
            _driversButton.Enabled = !busy;
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
            catch (Exception ex) { MessageBox.Show("Não foi possível abrir:\n" + target + "\n\n" + ex.Message, "Mestre PC Care", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }

    internal sealed class DriverCenterForm : Form
    {
        private readonly RichTextBox _versions = new RichTextBox();
        private readonly Button _restoreButton = new Button();

        public DriverCenterForm()
        {
            Text = "Drivers e atualizações — Mestre PC Care";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(820, 650);
            MinimumSize = new Size(760, 600);
            BackColor = Theme.Background;
            Font = Theme.Body;
            Icon = SystemIcons.Shield;
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
                Width = 330,
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
                string script = "Checkpoint-Computer -Description 'Mestre PC Care - antes dos drivers' -RestorePointType MODIFY_SETTINGS";
                ProcessResult result = await Task.Run(delegate
                {
                    return ProcessRunner.Run(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"),
                        "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + script + "\"", 180000);
                });
                MessageBox.Show(result.ExitCode == 0 ? "Ponto de restauração criado." : "O Windows não criou o ponto de restauração. A Proteção do Sistema pode estar desativada.\n\n" + result.Output,
                    "Mestre PC Care", MessageBoxButtons.OK, result.ExitCode == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
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

    internal static class SystemInfo
    {
        public static MemoryStatus GetMemoryStatus()
        {
            NativeMethods.MEMORYSTATUSEX data = new NativeMethods.MEMORYSTATUSEX();
            data.dwLength = (uint)Marshal.SizeOf(typeof(NativeMethods.MEMORYSTATUSEX));
            if (!NativeMethods.GlobalMemoryStatusEx(ref data)) return new MemoryStatus();
            return new MemoryStatus { PercentUsed = data.dwMemoryLoad, AvailableBytes = data.ullAvailPhys };
        }
    }

    internal static class NativeMethods
    {
        public const uint SHERB_NOCONFIRMATION = 0x00000001;
        public const uint SHERB_NOPROGRESSUI = 0x00000002;
        public const uint SHERB_NOSOUND = 0x00000004;

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

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);
    }
}
