using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Neck
{
    internal sealed class StartupEntry
    {
        public string Name;
        public string Command;
        public string ExecutablePath;
        public string Source;
        public bool Enabled;
        public string State;
        public string Impact;
        public string Category;
        public string Recommendation;
        public string Explanation;
        public long ObservedMemoryBytes;
    }

    internal static class StartupAnalyzer
    {
        private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ApprovedRunPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        private const string ApprovedFolderPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

        public static List<StartupEntry> Analyze()
        {
            List<StartupEntry> entries = new List<StartupEntry>();
            ReadRegistryRun(entries, RegistryHive.CurrentUser, RegistryView.Registry64, "Seu usuário");
            ReadRegistryRun(entries, RegistryHive.CurrentUser, RegistryView.Registry32, "Seu usuário (32 bits)");
            ReadRegistryRun(entries, RegistryHive.LocalMachine, RegistryView.Registry64, "Todos os usuários");
            ReadRegistryRun(entries, RegistryHive.LocalMachine, RegistryView.Registry32, "Todos os usuários (32 bits)");
            ReadStartupFolder(entries, Environment.GetFolderPath(Environment.SpecialFolder.Startup), RegistryHive.CurrentUser, "Pasta Inicializar do usuário");
            ReadStartupFolder(entries, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), RegistryHive.LocalMachine, "Pasta Inicializar compartilhada");

            List<StartupEntry> result = entries
                .GroupBy(item => (item.Name ?? "") + "\n" + (item.Command ?? ""), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            foreach (StartupEntry entry in result) Enrich(entry);
            return result
                .OrderByDescending(item => item.Enabled)
                .ThenBy(item => RecommendationOrder(item.Recommendation))
                .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static void ReadRegistryRun(List<StartupEntry> entries, RegistryHive hive, RegistryView view, string source)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey runKey = baseKey.OpenSubKey(RunPath, false))
                using (RegistryKey approvedKey = baseKey.OpenSubKey(ApprovedRunPath, false))
                {
                    if (runKey == null) return;
                    foreach (string name in runKey.GetValueNames())
                    {
                        string command = Convert.ToString(runKey.GetValue(name, "", RegistryValueOptions.DoNotExpandEnvironmentNames));
                        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(command)) continue;
                        bool enabled = ReadApprovedState(approvedKey, name, true);
                        entries.Add(CreateEntry(name, command, source, enabled));
                    }
                }
            }
            catch { }
        }

        private static void ReadStartupFolder(List<StartupEntry> entries, string directory, RegistryHive hive, string source)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default))
                using (RegistryKey approvedKey = baseKey.OpenSubKey(ApprovedFolderPath, false))
                {
                    foreach (string path in Directory.GetFiles(directory))
                    {
                        string fileName = Path.GetFileName(path);
                        if (string.Equals(fileName, "desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                        bool enabled = ReadApprovedState(approvedKey, fileName, true);
                        entries.Add(CreateEntry(Path.GetFileNameWithoutExtension(path), path, source, enabled));
                    }
                }
            }
            catch { }
        }

        private static bool ReadApprovedState(RegistryKey approvedKey, string valueName, bool defaultValue)
        {
            try
            {
                if (approvedKey == null) return defaultValue;
                byte[] data = approvedKey.GetValue(valueName) as byte[];
                if (data == null || data.Length == 0) return defaultValue;
                if (data[0] == 2) return true;
                if (data[0] == 3) return false;
            }
            catch { }
            return defaultValue;
        }

        private static StartupEntry CreateEntry(string name, string command, string source, bool enabled)
        {
            return new StartupEntry
            {
                Name = name.Trim(),
                Command = command.Trim(),
                ExecutablePath = ExtractExecutablePath(command),
                Source = source,
                Enabled = enabled,
                State = enabled ? "Ativo" : "Desativado"
            };
        }

        internal static string ExtractExecutablePath(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return null;
            string expanded = Environment.ExpandEnvironmentVariables(command.Trim());
            if (expanded.StartsWith("\"", StringComparison.Ordinal))
            {
                int closing = expanded.IndexOf('"', 1);
                if (closing > 1) return expanded.Substring(1, closing - 1);
            }

            Match executable = Regex.Match(expanded, @"^(?<path>.+?\.exe)(?:\s|$)", RegexOptions.IgnoreCase);
            if (executable.Success) return executable.Groups["path"].Value.Trim('"');
            if (File.Exists(expanded)) return expanded;
            return null;
        }

        private static void Enrich(StartupEntry entry)
        {
            string searchable = ((entry.Name ?? "") + " " + (entry.Command ?? "")).ToLowerInvariant();
            string company = "";
            try
            {
                if (!string.IsNullOrWhiteSpace(entry.ExecutablePath) && File.Exists(entry.ExecutablePath))
                    company = FileVersionInfo.GetVersionInfo(entry.ExecutablePath).CompanyName ?? "";
            }
            catch { }

            bool security = ContainsAny(searchable + " " + company.ToLowerInvariant(),
                "securityhealth", "windows defender", "antivirus", "avast", "avg", "bitdefender", "eset", "kaspersky", "malwarebytes", "norton", "mcafee");
            bool hardware = ContainsAny(searchable + " " + company.ToLowerInvariant(),
                "intel", "realtek", "synaptics", "touchpad", "audio", "nvidia", "radeon", "advanced micro devices", "logitech", "hp system", "lenovo utility", "dell support");
            string windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            bool executableInsideWindows = !string.IsNullOrWhiteSpace(entry.ExecutablePath) &&
                entry.ExecutablePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                entry.ExecutablePath.StartsWith(windowsDirectory, StringComparison.OrdinalIgnoreCase);
            bool system = executableInsideWindows || company.IndexOf("Microsoft Windows", StringComparison.OrdinalIgnoreCase) >= 0;
            bool optional = ContainsAny(searchable,
                "anydesk", "lightshot", "googleupdater", "discord", "spotify", "steam", "epicgames", "epic games", "teams", "zoom", "skype", "slack", "telegram", "whatsapp", "adobe", "creative cloud", "dropbox", "google drive", "onedrive", "chrome", "edge", "opera", "brave", "battle.net", "riotclient", "ubisoft");

            if (security)
            {
                entry.Category = "Segurança";
                entry.Recommendation = "Mantenha ativo";
                entry.Explanation = "Parece pertencer à proteção do computador. Desativar pode reduzir avisos ou recursos de segurança.";
            }
            else if (hardware)
            {
                entry.Category = "Hardware/driver";
                entry.Recommendation = "Mantenha ativo";
                entry.Explanation = "Parece oferecer funções de hardware, áudio, vídeo ou periféricos. Só desative se souber que o recurso é dispensável.";
            }
            else if (system)
            {
                entry.Category = "Windows";
                entry.Recommendation = "Mantenha ativo";
                entry.Explanation = "Parece ser um componente do Windows ou da Microsoft. O Neck recomenda preservá-lo.";
            }
            else if (optional)
            {
                entry.Category = "Aplicativo";
                entry.Recommendation = entry.Enabled ? "Pode revisar" : "Já desativado";
                entry.Explanation = entry.Enabled
                    ? "É um aplicativo de uso comum que normalmente pode ser aberto apenas quando necessário. Revise se você precisa dele logo ao entrar no Windows."
                    : "Este item já aparece como desativado na configuração de inicialização do Windows.";
            }
            else
            {
                entry.Category = "Não identificado";
                entry.Recommendation = entry.Enabled ? "Verifique antes" : "Já desativado";
                entry.Explanation = entry.Enabled
                    ? "O Neck não reconheceu este item com segurança. Confira o nome e o fornecedor antes de alterar sua inicialização."
                    : "Este item já aparece como desativado. O Neck não alterou nenhuma configuração.";
            }

            entry.ObservedMemoryBytes = GetObservedMemory(entry.ExecutablePath);
            entry.Impact = EstimateImpact(entry, optional);
        }

        private static long GetObservedMemory(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath)) return 0;
            string processName;
            try { processName = Path.GetFileNameWithoutExtension(executablePath); }
            catch { return 0; }
            if (string.IsNullOrWhiteSpace(processName)) return 0;

            long total = 0;
            try
            {
                foreach (Process process in Process.GetProcessesByName(processName))
                {
                    using (process)
                    {
                        try { total += Math.Max(0, process.WorkingSet64); }
                        catch { }
                    }
                }
            }
            catch { }
            return total;
        }

        private static string EstimateImpact(StartupEntry entry, bool optional)
        {
            if (!entry.Enabled) return "—";
            if (entry.ObservedMemoryBytes >= 500L * 1024 * 1024) return "Alto";
            if (entry.ObservedMemoryBytes >= 150L * 1024 * 1024) return "Médio";
            if (optional) return "Médio";
            if (entry.Category == "Segurança" || entry.Category == "Hardware/driver" || entry.Category == "Windows") return "Baixo";
            return "Incerto";
        }

        private static bool ContainsAny(string value, params string[] terms)
        {
            return terms.Any(term => value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static int RecommendationOrder(string recommendation)
        {
            if (recommendation == "Pode revisar") return 0;
            if (recommendation == "Verifique antes") return 1;
            if (recommendation == "Mantenha ativo") return 2;
            return 3;
        }
    }

    internal sealed class StartupAppsForm : Form
    {
        private readonly ListView _list = new ListView();
        private readonly Label _totalValue = new Label();
        private readonly Label _activeValue = new Label();
        private readonly Label _reviewValue = new Label();
        private readonly Label _detailTitle = new Label();
        private readonly Label _detailText = new Label();
        private readonly Button _refreshButton = new Button();
        private bool _closing;

        public StartupAppsForm(IList<StartupEntry> initialEntries = null)
        {
            Text = "Neck Boot — Inicialização do Windows";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(980, 700);
            MinimumSize = new Size(880, 620);
            BackColor = Theme.Background;
            Font = Theme.Body;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            BuildInterface();
            FormClosing += delegate { _closing = true; };
            if (initialEntries == null) Shown += async delegate { await ReloadAsync(); };
            else BindEntries(initialEntries);
        }

        private void BuildInterface()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 112, BackColor = Theme.Navy };
            header.Controls.Add(new Label { Text = "Neck Boot", AutoSize = true, Font = new Font("Segoe UI Semibold", 23f, FontStyle.Bold), ForeColor = Color.White, Location = new Point(28, 17) });
            header.Controls.Add(new Label
            {
                Text = "Descubra o que acompanha o Windows e decida com segurança o que realmente precisa iniciar junto.",
                AutoSize = false,
                Size = new Size(850, 34),
                Font = Theme.Body,
                ForeColor = Color.FromArgb(190, 203, 222),
                Location = new Point(31, 64)
            });

            Panel summary = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Theme.Background, Padding = new Padding(24, 14, 24, 10) };
            TableLayoutPanel metrics = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Color.White, Padding = new Padding(10, 4, 10, 4) };
            for (int i = 0; i < 3; i++) metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
            metrics.Controls.Add(MakeMetric("ITENS ENCONTRADOS", _totalValue), 0, 0);
            metrics.Controls.Add(MakeMetric("ATIVOS", _activeValue), 1, 0);
            metrics.Controls.Add(MakeMetric("PODEM SER REVISTOS", _reviewValue), 2, 0);
            summary.Controls.Add(metrics);

            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 72, BackColor = Color.White, Padding = new Padding(24, 14, 24, 13) };
            Button windowsSettings = MakeButton("Abrir Inicialização do Windows", Theme.Blue, 240);
            windowsSettings.Dock = DockStyle.Right;
            windowsSettings.Click += delegate { MainForm.OpenTarget("ms-settings:startupapps"); };
            _refreshButton.Text = "Atualizar análise";
            StyleButton(_refreshButton, Theme.NavySoft, 145);
            _refreshButton.Dock = DockStyle.Left;
            _refreshButton.Click += async delegate { await ReloadAsync(); };
            footer.Controls.Add(windowsSettings);
            footer.Controls.Add(_refreshButton);

            Panel details = new Panel { Dock = DockStyle.Bottom, Height = 128, BackColor = Color.White, Padding = new Padding(25, 15, 25, 12) };
            _detailTitle.Text = "Selecione um item para entender a recomendação";
            _detailTitle.AutoSize = true;
            _detailTitle.Font = Theme.Heading;
            _detailTitle.ForeColor = Theme.Text;
            _detailTitle.Location = new Point(24, 14);
            _detailText.Text = "O Neck apenas analisa. Qualquer mudança é feita por você na tela oficial do Windows.";
            _detailText.AutoSize = false;
            _detailText.Size = new Size(900, 68);
            _detailText.Font = Theme.Body;
            _detailText.ForeColor = Theme.Muted;
            _detailText.Location = new Point(26, 48);
            details.Controls.Add(_detailTitle);
            details.Controls.Add(_detailText);

            Panel listHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background, Padding = new Padding(24, 0, 24, 12) };
            _list.Dock = DockStyle.Fill;
            _list.View = View.Details;
            _list.FullRowSelect = true;
            _list.HideSelection = false;
            _list.MultiSelect = false;
            _list.BorderStyle = BorderStyle.FixedSingle;
            _list.Font = Theme.Small;
            _list.Columns.Add("Aplicativo", 210);
            _list.Columns.Add("Estado", 90);
            _list.Columns.Add("Impacto", 85);
            _list.Columns.Add("Recomendação", 130);
            _list.Columns.Add("Origem", 300);
            _list.SelectedIndexChanged += delegate { ShowSelectedDetails(); };
            listHost.Controls.Add(_list);

            Controls.Add(listHost);
            Controls.Add(details);
            Controls.Add(footer);
            Controls.Add(summary);
            Controls.Add(header);
        }

        private async Task ReloadAsync()
        {
            _refreshButton.Enabled = false;
            _refreshButton.Text = "Analisando...";
            try
            {
                List<StartupEntry> entries = await Task.Run(delegate { return StartupAnalyzer.Analyze(); });
                if (_closing || IsDisposed) return;
                BindEntries(entries);
            }
            catch (Exception ex)
            {
                if (!_closing && !IsDisposed)
                    MessageBox.Show("Não foi possível analisar a inicialização.\n\n" + ex.Message, "Neck Boot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                if (!_closing && !IsDisposed)
                {
                    _refreshButton.Enabled = true;
                    _refreshButton.Text = "Atualizar análise";
                }
            }
        }

        private void BindEntries(IList<StartupEntry> entries)
        {
            IList<StartupEntry> safeEntries = entries ?? new List<StartupEntry>();
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (StartupEntry entry in safeEntries)
            {
                ListViewItem item = new ListViewItem(entry.Name);
                item.SubItems.Add(entry.State);
                item.SubItems.Add(entry.Impact);
                item.SubItems.Add(entry.Recommendation);
                item.SubItems.Add(entry.Source);
                item.Tag = entry;
                if (!entry.Enabled) item.ForeColor = Theme.Muted;
                else if (entry.Recommendation == "Pode revisar") item.ForeColor = Theme.Amber;
                else if (entry.Recommendation == "Mantenha ativo") item.ForeColor = Theme.Green;
                _list.Items.Add(item);
            }
            _list.EndUpdate();

            _totalValue.Text = safeEntries.Count.ToString();
            _activeValue.Text = safeEntries.Count(item => item.Enabled).ToString();
            _reviewValue.Text = safeEntries.Count(item => item.Enabled && item.Recommendation == "Pode revisar").ToString();
            _reviewValue.ForeColor = safeEntries.Any(item => item.Enabled && item.Recommendation == "Pode revisar") ? Theme.Amber : Theme.Green;
            _detailTitle.Text = safeEntries.Count == 0 ? "Inicialização enxuta" : "Selecione um item para entender a recomendação";
            _detailText.Text = safeEntries.Count == 0
                ? "Nenhum item foi encontrado nos locais comuns de inicialização. O Neck não alterou o computador."
                : "O Neck apenas analisa. Qualquer mudança é feita por você na tela oficial do Windows.";
        }

        private void ShowSelectedDetails()
        {
            if (_list.SelectedItems.Count == 0) return;
            StartupEntry entry = _list.SelectedItems[0].Tag as StartupEntry;
            if (entry == null) return;
            _detailTitle.Text = entry.Name + " — " + entry.Recommendation;
            string memory = entry.ObservedMemoryBytes > 0 ? " Memória observada agora: " + MainForm.FormatBytes(entry.ObservedMemoryBytes) + "." : "";
            _detailText.Text = entry.Explanation + memory + Environment.NewLine + "Tipo: " + entry.Category + "  •  Origem: " + entry.Source;
        }

        private static Control MakeMetric(string caption, Label value)
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            Label title = new Label { Text = caption, AutoSize = false, Dock = DockStyle.Top, Height = 25, TextAlign = ContentAlignment.BottomCenter, Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold), ForeColor = Theme.Muted };
            value.Text = "—";
            value.AutoSize = false;
            value.Dock = DockStyle.Fill;
            value.TextAlign = ContentAlignment.TopCenter;
            value.Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold);
            value.ForeColor = Theme.Text;
            panel.Controls.Add(value);
            panel.Controls.Add(title);
            return panel;
        }

        private static Button MakeButton(string text, Color color, int width)
        {
            Button button = new Button { Text = text };
            StyleButton(button, color, width);
            return button;
        }

        private static void StyleButton(Button button, Color color, int width)
        {
            button.Width = width;
            button.Height = 44;
            button.BackColor = color;
            button.ForeColor = Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
        }
    }
}
