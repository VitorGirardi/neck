using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Neck
{
    internal static class StartupManager
    {
        private const string ValueName = "Neck";
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        internal static string BuildCommand(string executablePath)
        {
            return "\"" + executablePath + "\" --background";
        }

        public static bool IsEnabled()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    string value = key == null ? null : key.GetValue(ValueName) as string;
                    return string.Equals(value, BuildCommand(Application.ExecutablePath), StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { return false; }
        }

        public static void SetEnabled(bool enabled)
        {
            using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKeyPath))
            {
                if (key == null) throw new InvalidOperationException("O Windows não permitiu abrir as configurações de inicialização.");
                if (enabled) key.SetValue(ValueName, BuildCommand(Application.ExecutablePath), Microsoft.Win32.RegistryValueKind.String);
                else key.DeleteValue(ValueName, false);
            }
        }
    }

    internal sealed class GuardSample
    {
        public DateTime TimestampUtc;
        public double MemoryPercent;
        public double CpuPercent;
        public long AvailableBytes;
        public long DiskFreeBytes;
        public string TopProcess = "";
        public long TopProcessBytes;

        public static GuardSample FromSnapshot(HealthSnapshot snapshot)
        {
            ResourceProcess top = snapshot.TopProcesses.FirstOrDefault();
            return new GuardSample
            {
                TimestampUtc = DateTime.UtcNow,
                MemoryPercent = snapshot.Memory.PercentUsed,
                CpuPercent = snapshot.CpuPercent,
                AvailableBytes = (long)snapshot.Memory.AvailableBytes,
                DiskFreeBytes = snapshot.DiskFreeBytes,
                TopProcess = top == null ? "" : top.DisplayName,
                TopProcessBytes = top == null ? 0 : top.MemoryBytes
            };
        }
    }

    internal enum GuardAlertKind { None, MemoryPressure, CpuPressure, ProcessGrowth, LowDisk }

    internal sealed class GuardAlert
    {
        public GuardAlertKind Kind;
        public string Title = "";
        public string Message = "";
    }

    internal sealed class GuardPressureDetector
    {
        public GuardAlert Evaluate(IList<GuardSample> samples)
        {
            if (samples == null || samples.Count == 0) return new GuardAlert();
            GuardSample latest = samples[samples.Count - 1];
            List<GuardSample> recent = samples.Where(item => item.TimestampUtc >= latest.TimestampUtc.AddMinutes(-4)).ToList();
            if (recent.Count >= 6 && recent.Skip(recent.Count - 6).All(item => item.MemoryPercent >= 85))
            {
                return new GuardAlert
                {
                    Kind = GuardAlertKind.MemoryPressure,
                    Title = "Memória sob pressão",
                    Message = "A RAM permanece acima de 85%. " + ProcessMessage(latest)
                };
            }

            if (recent.Count >= 3 && recent.Skip(recent.Count - 3).All(item => item.CpuPercent >= 90))
            {
                return new GuardAlert
                {
                    Kind = GuardAlertKind.CpuPressure,
                    Title = "CPU sob pressão",
                    Message = "A CPU permanece acima de 90%. Escolha o aplicativo mais importante e use Acelerar por 1 hora."
                };
            }

            if (!string.IsNullOrWhiteSpace(latest.TopProcess))
            {
                GuardSample oldestSame = samples
                    .Where(item => item.TimestampUtc >= latest.TimestampUtc.AddMinutes(-10) &&
                                   string.Equals(item.TopProcess, latest.TopProcess, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(item => item.TimestampUtc)
                    .FirstOrDefault();
                if (oldestSame != null && latest.TimestampUtc - oldestSame.TimestampUtc >= TimeSpan.FromMinutes(5) &&
                    latest.TopProcessBytes - oldestSame.TopProcessBytes >= 1536L * 1024 * 1024)
                {
                    return new GuardAlert
                    {
                        Kind = GuardAlertKind.ProcessGrowth,
                        Title = latest.TopProcess + " continua crescendo",
                        Message = "O uso aumentou cerca de " + MainForm.FormatBytes(latest.TopProcessBytes - oldestSame.TopProcessBytes) + " nos últimos minutos."
                    };
                }
            }

            if (latest.DiskFreeBytes > 0 && latest.DiskFreeBytes < 10L * 1024 * 1024 * 1024)
            {
                return new GuardAlert
                {
                    Kind = GuardAlertKind.LowDisk,
                    Title = "Pouco espaço no disco",
                    Message = "Restam " + MainForm.FormatBytes(latest.DiskFreeBytes) + " no disco do Windows."
                };
            }
            return new GuardAlert();
        }

        private static string ProcessMessage(GuardSample sample)
        {
            return string.IsNullOrWhiteSpace(sample.TopProcess) ? "Abra o diagnóstico para investigar." :
                sample.TopProcess + " lidera o uso com aproximadamente " + MainForm.FormatBytes(sample.TopProcessBytes) + ".";
        }
    }

    internal sealed class GuardHistoryStore
    {
        private readonly string _directory;
        private readonly string _historyPath;

        public string HistoryPath { get { return _historyPath; } }

        public GuardHistoryStore(string directory = null)
        {
            _directory = string.IsNullOrWhiteSpace(directory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Neck")
                : directory;
            _historyPath = Path.Combine(_directory, "guard-history.log");
        }

        public List<GuardSample> LoadLast24Hours()
        {
            List<GuardSample> samples = new List<GuardSample>();
            try
            {
                if (!File.Exists(_historyPath)) return samples;
                DateTime cutoff = DateTime.UtcNow.AddHours(-24);
                foreach (string line in File.ReadLines(_historyPath))
                {
                    GuardSample sample = Parse(line);
                    if (sample != null && sample.TimestampUtc >= cutoff) samples.Add(sample);
                }
            }
            catch (Exception ex) { SupportDiagnostics.RecordThrottledException("Leitura do histórico Guard", ex); }
            return samples.OrderBy(item => item.TimestampUtc).ToList();
        }

        public void Append(GuardSample sample)
        {
            try
            {
                Directory.CreateDirectory(_directory);
                File.AppendAllText(_historyPath, Serialize(sample) + Environment.NewLine, new UTF8Encoding(false));
            }
            catch (Exception ex) { SupportDiagnostics.RecordThrottledException("Gravação do histórico Guard", ex); }
        }

        public void Compact(IList<GuardSample> samples)
        {
            try
            {
                Directory.CreateDirectory(_directory);
                string temporary = _historyPath + ".tmp";
                File.WriteAllLines(temporary, samples.Select(Serialize).ToArray(), new UTF8Encoding(false));
                if (File.Exists(_historyPath)) File.Replace(temporary, _historyPath, null);
                else File.Move(temporary, _historyPath);
            }
            catch (Exception ex) { SupportDiagnostics.RecordThrottledException("Compactação do histórico Guard", ex); }
        }

        private static string Serialize(GuardSample sample)
        {
            string process = Convert.ToBase64String(Encoding.UTF8.GetBytes(sample.TopProcess ?? ""));
            return string.Join("|", new[]
            {
                sample.TimestampUtc.Ticks.ToString(CultureInfo.InvariantCulture),
                sample.MemoryPercent.ToString("0.0", CultureInfo.InvariantCulture),
                sample.AvailableBytes.ToString(CultureInfo.InvariantCulture),
                sample.DiskFreeBytes.ToString(CultureInfo.InvariantCulture),
                process,
                sample.TopProcessBytes.ToString(CultureInfo.InvariantCulture),
                sample.CpuPercent.ToString("0.0", CultureInfo.InvariantCulture)
            });
        }

        private static GuardSample Parse(string line)
        {
            try
            {
                string[] parts = line.Split('|');
                if (parts.Length != 6 && parts.Length != 7) return null;
                return new GuardSample
                {
                    TimestampUtc = new DateTime(long.Parse(parts[0], CultureInfo.InvariantCulture), DateTimeKind.Utc),
                    MemoryPercent = double.Parse(parts[1], CultureInfo.InvariantCulture),
                    AvailableBytes = long.Parse(parts[2], CultureInfo.InvariantCulture),
                    DiskFreeBytes = long.Parse(parts[3], CultureInfo.InvariantCulture),
                    TopProcess = Encoding.UTF8.GetString(Convert.FromBase64String(parts[4])),
                    TopProcessBytes = long.Parse(parts[5], CultureInfo.InvariantCulture),
                    CpuPercent = parts.Length == 7 ? double.Parse(parts[6], CultureInfo.InvariantCulture) : 0
                };
            }
            catch { return null; }
        }
    }

    internal sealed class GuardSettings
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Neck", "guard-settings.txt");

        public bool ContinueInTray;
        public bool Notifications = true;
        public bool SilenceFullscreen = true;
        public bool ReduceMotion;
        public bool AutopilotEnabled;
        public bool OnboardingCompleted;
        public DateTime SilentUntilUtc = DateTime.MinValue;

        public static GuardSettings Load()
        {
            GuardSettings settings = new GuardSettings();
            try
            {
                if (!File.Exists(SettingsPath)) return settings;
                foreach (string line in File.ReadAllLines(SettingsPath))
                {
                    int separator = line.IndexOf('=');
                    if (separator < 1) continue;
                    string key = line.Substring(0, separator);
                    string value = line.Substring(separator + 1);
                    if (key == "ContinueInTray") bool.TryParse(value, out settings.ContinueInTray);
                    else if (key == "Notifications") bool.TryParse(value, out settings.Notifications);
                    else if (key == "SilenceFullscreen") bool.TryParse(value, out settings.SilenceFullscreen);
                    else if (key == "ReduceMotion") bool.TryParse(value, out settings.ReduceMotion);
                    else if (key == "AutopilotEnabled") bool.TryParse(value, out settings.AutopilotEnabled);
                    else if (key == "OnboardingCompleted") bool.TryParse(value, out settings.OnboardingCompleted);
                    else if (key == "SilentUntilUtc") DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out settings.SilentUntilUtc);
                }
            }
            catch (Exception ex) { SupportDiagnostics.RecordThrottledException("Leitura das preferências", ex); }
            return settings;
        }

        public void Save()
        {
            try
            {
                string directory = Path.GetDirectoryName(SettingsPath);
                Directory.CreateDirectory(directory);
                File.WriteAllLines(SettingsPath, new[]
                {
                    "ContinueInTray=" + ContinueInTray,
                    "Notifications=" + Notifications,
                    "SilenceFullscreen=" + SilenceFullscreen,
                    "ReduceMotion=" + ReduceMotion,
                    "AutopilotEnabled=" + AutopilotEnabled,
                    "OnboardingCompleted=" + OnboardingCompleted,
                    "SilentUntilUtc=" + SilentUntilUtc.ToString("O", CultureInfo.InvariantCulture)
                }, new UTF8Encoding(false));
            }
            catch (Exception ex) { SupportDiagnostics.RecordThrottledException("Gravação das preferências", ex); }
        }
    }

    internal sealed class GuardHistoryForm : Form
    {
        public GuardHistoryForm(IList<GuardSample> samples, string reportDirectory)
        {
            Text = "Histórico de desempenho — Neck Guard";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(860, 650);
            MinimumSize = new Size(780, 580);
            BackColor = Theme.Background;
            Font = Theme.Body;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            BuildInterface(samples ?? new List<GuardSample>(), reportDirectory);
        }

        private void BuildInterface(IList<GuardSample> samples, string reportDirectory)
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 102, BackColor = Theme.Card };
            header.Controls.Add(new Label { Text = "Últimas 24 horas", AutoSize = true, Font = new Font("Segoe UI Variable Display", 22f, FontStyle.Bold), ForeColor = Theme.Ink, Location = new Point(28, 18) });
            header.Controls.Add(new Label { Text = "Apenas métricas locais; nenhum dado é enviado.", AutoSize = true, Font = Theme.Body, ForeColor = Theme.Muted, Location = new Point(31, 64) });

            TableLayoutPanel body = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24, 20, 24, 20), ColumnCount = 1, RowCount = 3, BackColor = Theme.Background };
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            double average = samples.Count == 0 ? 0 : samples.Average(item => item.MemoryPercent);
            double maximum = samples.Count == 0 ? 0 : samples.Max(item => item.MemoryPercent);
            double cpuMaximum = samples.Count == 0 ? 0 : samples.Max(item => item.CpuPercent);
            Label summary = new Label
            {
                Dock = DockStyle.Fill,
                Text = samples.Count == 0 ? "O histórico começará após a primeira medição." :
                    "RAM média: " + average.ToString("0", CultureInfo.CurrentCulture) + "%  •  Pico RAM: " + maximum.ToString("0", CultureInfo.CurrentCulture) + "%  •  Pico CPU: " + cpuMaximum.ToString("0", CultureInfo.CurrentCulture) + "%  •  " + samples.Count + " medições",
                Font = Theme.Heading,
                ForeColor = Theme.Text,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.White,
                Padding = new Padding(18)
            };
            ListView list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, BorderStyle = BorderStyle.FixedSingle, Font = Theme.Body, BackColor = Color.White };
            list.Columns.Add("Horário", 150);
            list.Columns.Add("RAM", 75, HorizontalAlignment.Right);
            list.Columns.Add("CPU", 75, HorizontalAlignment.Right);
            list.Columns.Add("Disponível", 115, HorizontalAlignment.Right);
            list.Columns.Add("Maior consumidor", 230);
            list.Columns.Add("Uso", 105, HorizontalAlignment.Right);
            foreach (GuardSample sample in samples.OrderByDescending(item => item.TimestampUtc).Take(250))
            {
                ListViewItem item = new ListViewItem(sample.TimestampUtc.ToLocalTime().ToString("dd/MM HH:mm:ss"));
                item.SubItems.Add(sample.MemoryPercent.ToString("0", CultureInfo.CurrentCulture) + "%");
                item.SubItems.Add(sample.CpuPercent.ToString("0", CultureInfo.CurrentCulture) + "%");
                item.SubItems.Add(MainForm.FormatBytes(sample.AvailableBytes));
                item.SubItems.Add(string.IsNullOrWhiteSpace(sample.TopProcess) ? "—" : sample.TopProcess);
                item.SubItems.Add(MainForm.FormatBytes(sample.TopProcessBytes));
                list.Items.Add(item);
            }
            FlowLayoutPanel footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 10, 0, 0) };
            Button close = MakeButton("Fechar", Theme.Blue, 120);
            Button reports = MakeButton("Relatórios de manutenção", Theme.NavySoft, 220);
            close.Click += delegate { Close(); };
            reports.Click += delegate { Directory.CreateDirectory(reportDirectory); MainForm.OpenTarget(reportDirectory); };
            footer.Controls.Add(close);
            footer.Controls.Add(reports);
            body.Controls.Add(summary, 0, 0);
            body.Controls.Add(list, 0, 1);
            body.Controls.Add(footer, 0, 2);
            Controls.Add(body);
            Controls.Add(header);
        }

        private static Button MakeButton(string text, Color color, int width)
        {
            Button button = new Button { Text = text, Width = width, Height = 42, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold), Margin = new Padding(10, 0, 0, 0), Cursor = Cursors.Hand };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }
    }
}
