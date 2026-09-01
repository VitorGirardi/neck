using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Neck
{
    internal sealed class SupportEvent
    {
        public DateTime TimestampUtc;
        public string Category = string.Empty;
        public string Message = string.Empty;
        public string Detail = string.Empty;
    }

    internal static class SupportDiagnostics
    {
        private static readonly object SyncRoot = new object();
        private static readonly string DirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Neck", "Diagnostics");
        private static readonly string EventsPath = Path.Combine(DirectoryPath, "events.log");

        public static void RecordEvent(string category, string message)
        {
            Append(new SupportEvent
            {
                TimestampUtc = DateTime.UtcNow,
                Category = Sanitize(category),
                Message = Sanitize(message)
            });
        }

        public static void RecordException(string scope, Exception exception)
        {
            string type = exception == null ? "Erro desconhecido" : exception.GetType().FullName;
            string message = exception == null ? "A exceção não forneceu detalhes." : exception.Message;
            string detail = exception == null ? string.Empty : exception.ToString();
            Append(new SupportEvent
            {
                TimestampUtc = DateTime.UtcNow,
                Category = "Falha em " + Sanitize(scope),
                Message = Sanitize(type + ": " + message),
                Detail = Sanitize(detail)
            });
        }

        public static List<SupportEvent> LoadRecent(int maximum)
        {
            lock (SyncRoot)
            {
                List<SupportEvent> events = LoadCore();
                return events.OrderByDescending(item => item.TimestampUtc).Take(Math.Max(1, maximum)).ToList();
            }
        }

        internal static string Sanitize(string value)
        {
            string sanitized = value ?? string.Empty;
            sanitized = Regex.Replace(sanitized, @"(?im)(?:[A-Z]:\\|\\\\)[^\r\n""]+", "[caminho-local]");
            sanitized = Regex.Replace(sanitized, @"(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", "[email]");
            Dictionary<string, string> replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            AddReplacement(replacements, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "[perfil-do-usuario]");
            AddReplacement(replacements, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "[area-de-trabalho]");
            AddReplacement(replacements, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "[documentos]");
            AddReplacement(replacements, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "[dados-locais]");
            AddReplacement(replacements, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "[dados-do-aplicativo]");
            AddReplacement(replacements, Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), "[temporarios]");
            AddReplacement(replacements, Environment.UserName, "[usuario]");
            AddReplacement(replacements, Environment.MachineName, "[computador]");
            foreach (KeyValuePair<string, string> replacement in replacements.OrderByDescending(item => item.Key.Length))
                sanitized = ReplaceInsensitive(sanitized, replacement.Key, replacement.Value);
            return sanitized.Trim();
        }

        private static void Append(SupportEvent item)
        {
            lock (SyncRoot)
            {
                try
                {
                    Directory.CreateDirectory(DirectoryPath);
                    File.AppendAllText(EventsPath, Serialize(item) + Environment.NewLine, new UTF8Encoding(false));
                    FileInfo info = new FileInfo(EventsPath);
                    if (info.Length <= 512 * 1024) return;
                    List<SupportEvent> recent = LoadCore().OrderByDescending(entry => entry.TimestampUtc).Take(250)
                        .OrderBy(entry => entry.TimestampUtc).ToList();
                    File.WriteAllLines(EventsPath, recent.Select(Serialize).ToArray(), new UTF8Encoding(false));
                }
                catch { }
            }
        }

        private static List<SupportEvent> LoadCore()
        {
            List<SupportEvent> events = new List<SupportEvent>();
            try
            {
                if (!File.Exists(EventsPath)) return events;
                foreach (string line in File.ReadAllLines(EventsPath))
                {
                    SupportEvent item = Parse(line);
                    if (item != null) events.Add(item);
                }
            }
            catch { }
            return events;
        }

        private static string Serialize(SupportEvent item)
        {
            return string.Join("|", new[]
            {
                item.TimestampUtc.Ticks.ToString(CultureInfo.InvariantCulture),
                Encode(item.Category),
                Encode(item.Message),
                Encode(item.Detail)
            });
        }

        private static SupportEvent Parse(string line)
        {
            try
            {
                string[] parts = (line ?? string.Empty).Split('|');
                if (parts.Length != 4) return null;
                return new SupportEvent
                {
                    TimestampUtc = new DateTime(long.Parse(parts[0], CultureInfo.InvariantCulture), DateTimeKind.Utc),
                    Category = Sanitize(Decode(parts[1])),
                    Message = Sanitize(Decode(parts[2])),
                    Detail = Sanitize(Decode(parts[3]))
                };
            }
            catch { return null; }
        }

        private static string Encode(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string Decode(string value)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value ?? string.Empty));
        }

        private static void AddReplacement(IDictionary<string, string> replacements, string source, string replacement)
        {
            if (string.IsNullOrWhiteSpace(source) || source.Length < 3 || replacements.ContainsKey(source)) return;
            replacements.Add(source, replacement);
        }

        private static string ReplaceInsensitive(string source, string oldValue, string newValue)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(oldValue)) return source;
            int start = 0;
            StringBuilder result = new StringBuilder();
            while (true)
            {
                int index = source.IndexOf(oldValue, start, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    result.Append(source, start, source.Length - start);
                    break;
                }
                result.Append(source, start, index - start);
                result.Append(newValue);
                start = index + oldValue.Length;
            }
            return result.ToString();
        }
    }

    internal static class SupportReportBuilder
    {
        public static readonly string ReportDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Neck", "Suporte");

        public static string Save(GuardSettings settings, IList<GuardSample> samples, HardwareSnapshot hardware,
            RecoveryStartupResult recovery)
        {
            Directory.CreateDirectory(ReportDirectory);
            string path = Path.Combine(ReportDirectory, "Neck-Suporte-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
            File.WriteAllText(path, BuildText(settings, samples, hardware, recovery), new UTF8Encoding(false));
            SupportDiagnostics.RecordEvent("Suporte", "Relatório sanitizado criado localmente.");
            return path;
        }

        internal static string BuildText(GuardSettings settings, IList<GuardSample> samples, HardwareSnapshot hardware,
            RecoveryStartupResult recovery)
        {
            settings = settings ?? new GuardSettings();
            samples = samples ?? new List<GuardSample>();
            recovery = recovery ?? new RecoveryStartupResult();
            StringBuilder text = new StringBuilder();
            text.AppendLine("NECK — RELATÓRIO DE SUPORTE SANITIZADO");
            text.AppendLine("Gerado em: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.CurrentCulture));
            text.AppendLine();
            text.AppendLine("PRIVACIDADE");
            text.AppendLine("Este arquivo não inclui nome de usuário, nome do computador, títulos de janelas, lista de processos, caminhos de arquivos pessoais, documentos, senhas, conteúdo de tela ou histórico de navegação.");
            text.AppendLine("Revise o conteúdo antes de anexá-lo a uma issue pública.");
            text.AppendLine();
            text.AppendLine("APLICATIVO");
            Version version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);
            text.AppendLine("Versão do Neck: " + version.ToString(3));
            text.AppendLine("Windows: " + SupportDiagnostics.Sanitize(Environment.OSVersion.VersionString));
            text.AppendLine("Arquitetura do Windows: " + (Environment.Is64BitOperatingSystem ? "64 bits" : "32 bits"));
            text.AppendLine("Processo do Neck: " + (Environment.Is64BitProcess ? "64 bits" : "32 bits"));
            text.AppendLine("Runtime .NET: " + Environment.Version);
            text.AppendLine("Executando como administrador: " + (SecurityHelper.IsAdministrator() ? "sim" : "não"));
            text.AppendLine();
            text.AppendLine("PREFERÊNCIAS SEGURAS");
            text.AppendLine("Autopilot: " + Enabled(settings.AutopilotEnabled));
            text.AppendLine("Permanecer na bandeja: " + Enabled(settings.ContinueInTray));
            text.AppendLine("Notificações: " + Enabled(settings.Notifications));
            text.AppendLine("Silenciar em tela cheia: " + Enabled(settings.SilenceFullscreen));
            text.AppendLine("Reduzir animações: " + Enabled(settings.ReduceMotion));
            text.AppendLine();
            text.AppendLine("HARDWARE RESUMIDO");
            if (hardware == null)
            {
                text.AppendLine("Inventário ainda não disponível nesta execução.");
            }
            else
            {
                text.AppendLine("CPU: " + SupportDiagnostics.Sanitize(hardware.ProcessorSummary));
                text.AppendLine("RAM: " + SupportDiagnostics.Sanitize(hardware.MemorySummary));
                text.AppendLine("GPU: " + SupportDiagnostics.Sanitize(hardware.GraphicsSummary));
                text.AppendLine("Armazenamento: " + SupportDiagnostics.Sanitize(hardware.StorageSummary));
                text.AppendLine("Temperatura: " + SupportDiagnostics.Sanitize(hardware.TemperatureSummary));
            }
            text.AppendLine();
            text.AppendLine("MÉTRICAS AGREGADAS — ÚLTIMAS 24 HORAS");
            if (samples.Count == 0)
            {
                text.AppendLine("Nenhuma medição disponível.");
            }
            else
            {
                text.AppendLine("Medições: " + samples.Count);
                text.AppendLine("RAM média / pico: " + samples.Average(item => item.MemoryPercent).ToString("0.0", CultureInfo.InvariantCulture) + "% / " +
                    samples.Max(item => item.MemoryPercent).ToString("0.0", CultureInfo.InvariantCulture) + "%");
                text.AppendLine("CPU média / pico: " + samples.Average(item => item.CpuPercent).ToString("0.0", CultureInfo.InvariantCulture) + "% / " +
                    samples.Max(item => item.CpuPercent).ToString("0.0", CultureInfo.InvariantCulture) + "%");
                long free = samples.Where(item => item.DiskFreeBytes > 0).Select(item => item.DiskFreeBytes).DefaultIfEmpty(0).Min();
                text.AppendLine("Menor espaço livre observado: " + (free > 0 ? MainForm.FormatBytes(free) : "não informado"));
            }
            text.AppendLine("Nomes dos aplicativos foram omitidos desta seção.");
            text.AppendLine();
            text.AppendLine("RECUPERAÇÃO AUTOMÁTICA");
            text.AppendLine("Sessão anterior interrompida: " + (recovery.PreviousSessionInterrupted ? "sim" : "não"));
            text.AppendLine("Alterações encontradas / restauradas / obsoletas / pendentes: " + recovery.PendingEntries + " / " +
                recovery.RestoredEntries + " / " + recovery.StaleEntries + " / " + recovery.FailedEntries);
            text.AppendLine("Estado atual do diário de recuperação: " + RecoveryJournal.PendingCount + " pendência(s)");
            text.AppendLine();
            text.AppendLine("EVENTOS RECENTES");
            List<SupportEvent> events = SupportDiagnostics.LoadRecent(20);
            if (events.Count == 0) text.AppendLine("Nenhum evento registrado.");
            foreach (SupportEvent item in events.OrderBy(entry => entry.TimestampUtc))
            {
                text.AppendLine("[" + item.TimestampUtc.ToLocalTime().ToString("dd/MM HH:mm:ss") + "] " +
                    SupportDiagnostics.Sanitize(item.Category) + " — " + SupportDiagnostics.Sanitize(item.Message));
                if (!string.IsNullOrWhiteSpace(item.Detail))
                {
                    string detail = SupportDiagnostics.Sanitize(item.Detail);
                    if (detail.Length > 4000) detail = detail.Substring(0, 4000) + "\n[detalhe reduzido]";
                    foreach (string line in detail.Replace("\r", string.Empty).Split('\n')) text.AppendLine("    " + line);
                }
            }
            text.AppendLine();
            text.AppendLine("FIM DO RELATÓRIO");
            return SupportDiagnostics.Sanitize(text.ToString()) + Environment.NewLine;
        }

        private static string Enabled(bool value)
        {
            return value ? "ativado" : "desativado";
        }
    }

    internal sealed class SupportReportForm : Form
    {
        private readonly GuardSettings _settings;
        private readonly IList<GuardSample> _samples;
        private readonly HardwareSnapshot _hardware;
        private readonly RecoveryStartupResult _recovery;
        private readonly Label _status = new Label();
        private readonly Button _openFolder = new Button();

        public SupportReportForm(GuardSettings settings, IList<GuardSample> samples, HardwareSnapshot hardware,
            RecoveryStartupResult recovery)
        {
            _settings = settings;
            _samples = samples;
            _hardware = hardware;
            _recovery = recovery;
            Text = "Suporte e recuperação — Neck";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(780, 610);
            MinimumSize = new Size(720, 560);
            BackColor = Theme.Background;
            Font = Theme.Body;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            BuildInterface();
            Shown += delegate { VisualEffects.FadeIn(this); };
        }

        private void BuildInterface()
        {
            Panel header = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(34, 22, 34, 16) };
            header.Controls.Add(new Label
            {
                Text = "Suporte e recuperação",
                AutoSize = true,
                Font = new Font("Bahnschrift", 23f, FontStyle.Bold),
                ForeColor = Theme.Text,
                Location = new Point(32, 20)
            });
            header.Controls.Add(new Label
            {
                Text = "Entenda uma falha e restaure o computador sem expor seus dados.",
                AutoSize = true,
                Font = Theme.Body,
                ForeColor = Theme.Muted,
                Location = new Point(35, 68)
            });

            TableLayoutPanel content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(28, 22, 28, 20),
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Theme.Background
            };
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));

            RoundedPanel recoveryCard = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                OutlineColor = Theme.Border,
                CornerRadius = 16,
                Padding = new Padding(24, 18, 24, 14)
            };
            Label recoveryTitle = new Label
            {
                Text = RecoveryTitle(),
                AutoSize = false,
                Location = new Point(24, 17),
                Height = 30,
                Font = Theme.Heading,
                ForeColor = RecoveryJournal.PendingCount > 0 ? Theme.Amber : Theme.Green
            };
            Label recoveryDetail = new Label
            {
                Text = (_recovery ?? new RecoveryStartupResult()).Summary,
                AutoSize = false,
                Location = new Point(24, 54),
                Height = 34,
                Font = Theme.Small,
                ForeColor = Theme.Muted
            };
            recoveryCard.Resize += delegate
            {
                recoveryTitle.Width = Math.Max(100, recoveryCard.ClientSize.Width - 48);
                recoveryDetail.Width = Math.Max(100, recoveryCard.ClientSize.Width - 48);
            };
            recoveryCard.Controls.Add(recoveryDetail);
            recoveryCard.Controls.Add(recoveryTitle);

            RoundedPanel privacyCard = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 14, 0, 12),
                BackColor = Color.White,
                OutlineColor = Theme.Border,
                CornerRadius = 16,
                Padding = new Padding(24, 20, 24, 16)
            };
            Label privacyTitle = new Label
            {
                Text = "Um relatório que você pode revisar",
                AutoSize = false,
                Location = new Point(24, 18),
                Height = 36,
                Font = Theme.Heading,
                ForeColor = Theme.Text
            };
            Label included = new Label
            {
                Text = "Inclui\n• versão do Neck e do Windows\n• hardware resumido\n• médias de RAM e CPU\n• falhas e recuperações",
                AutoSize = false,
                Location = new Point(24, 62),
                Height = 92,
                Font = Theme.Small,
                ForeColor = Theme.Muted
            };
            Label excluded = new Label
            {
                Text = "Não inclui\n• usuário ou nome do computador\n• lista de aplicativos ou janelas\n• caminhos, documentos ou senhas\n• conteúdo da tela ou navegação",
                AutoSize = false,
                Location = new Point(360, 62),
                Height = 92,
                Font = Theme.Small,
                ForeColor = Theme.Muted
            };
            Label localNote = new Label
            {
                Text = "Criado somente em Documentos\\Neck\\Suporte. Nada é enviado automaticamente.",
                AutoSize = false,
                Location = new Point(24, 174),
                Height = 34,
                Font = Theme.Small,
                ForeColor = Theme.Green,
                TextAlign = ContentAlignment.MiddleLeft
            };
            privacyCard.Resize += delegate
            {
                int usable = Math.Max(240, privacyCard.ClientSize.Width - 48);
                int leftWidth = (int)(usable * 0.45d);
                privacyTitle.Width = usable;
                included.Width = leftWidth;
                excluded.Left = 24 + leftWidth + 20;
                excluded.Width = Math.Max(100, usable - leftWidth - 20);
                localNote.Width = usable;
                localNote.Top = Math.Max(166, privacyCard.ClientSize.Height - localNote.Height - 12);
                localNote.Visible = privacyCard.ClientSize.Height >= 190;
            };
            privacyCard.Controls.Add(included);
            privacyCard.Controls.Add(excluded);
            privacyCard.Controls.Add(localNote);
            privacyCard.Controls.Add(privacyTitle);

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 9, 0, 0)
            };
            Button close = CreateButton("Voltar", Theme.NavySoft, 112);
            close.Click += delegate { Close(); };
            Button create = CreateButton("Criar relatório", Theme.Blue, 170);
            create.Click += delegate { CreateReport(); };
            _openFolder.Text = "Abrir pasta";
            ConfigureButton(_openFolder, Theme.Green, 130);
            _openFolder.Visible = Directory.Exists(SupportReportBuilder.ReportDirectory);
            _openFolder.Click += delegate { MainForm.OpenTarget(SupportReportBuilder.ReportDirectory); };
            _status.AutoSize = false;
            _status.Width = 270;
            _status.Height = 42;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.Font = Theme.Small;
            _status.ForeColor = Theme.Muted;
            _status.Text = "Nada será enviado automaticamente.";
            actions.Controls.Add(close);
            actions.Controls.Add(create);
            actions.Controls.Add(_openFolder);
            actions.Controls.Add(_status);

            content.Controls.Add(recoveryCard, 0, 0);
            content.Controls.Add(privacyCard, 0, 1);
            content.Controls.Add(actions, 0, 2);
            TableLayoutPanel page = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
            page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            page.Controls.Add(header, 0, 0);
            page.Controls.Add(content, 0, 1);
            Controls.Add(page);
            AcceptButton = create;
            CancelButton = close;
        }

        private string RecoveryTitle()
        {
            if (RecoveryJournal.PendingCount > 0) return "Restauração aguardando nova tentativa";
            if (_recovery != null && _recovery.RestoredEntries > 0) return "O Neck corrigiu uma interrupção anterior";
            return "Sistema de recuperação pronto";
        }

        private void CreateReport()
        {
            try
            {
                string path = SupportReportBuilder.Save(_settings, _samples, _hardware, _recovery);
                _status.Text = "Relatório criado: " + Path.GetFileName(path);
                _status.ForeColor = Theme.Green;
                _openFolder.Visible = true;
            }
            catch (Exception ex)
            {
                SupportDiagnostics.RecordException("Criação do relatório", ex);
                _status.Text = "Não foi possível criar o relatório.";
                _status.ForeColor = Color.Firebrick;
                MessageBox.Show("Não foi possível criar o relatório de suporte.\n\n" + ex.Message,
                    "Suporte do Neck", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static Button CreateButton(string text, Color color, int width)
        {
            Button button = new AnimatedButton { Text = text };
            ConfigureButton(button, color, width);
            return button;
        }

        private static void ConfigureButton(Button button, Color color, int width)
        {
            button.Size = new Size(width, 42);
            button.Margin = new Padding(8, 0, 0, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = color;
            button.ForeColor = Color.White;
            button.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            AnimatedButton animated = button as AnimatedButton;
            if (animated != null) animated.SetPalette(color);
        }
    }
}
