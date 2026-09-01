using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Neck
{
    internal sealed class SosCandidate
    {
        public string ProcessName;
        public string DisplayName;
        public int ProcessCount;
        public int VisibleWindows;
        public long MemoryBytes;
        public double CpuPercent;
        public string ExecutablePath;
    }

    internal sealed class SosCloseResult
    {
        public int RequestsSent;
        public int AccessErrors;
    }

    internal static class SosInspector
    {
        private static readonly HashSet<string> ProtectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ApplicationFrameHost", "csrss", "dwm", "explorer", "fontdrvhost", "LockApp", "lsass",
            "Memory Compression", "Registry", "RuntimeBroker", "SearchHost", "services", "ShellExperienceHost",
            "smss", "spoolsv", "StartMenuExperienceHost", "svchost", "System", "TextInputHost", "wininit", "winlogon"
        };

        public static bool IsProtectedProcessName(string processName)
        {
            return string.IsNullOrWhiteSpace(processName) || ProtectedNames.Contains(processName);
        }

        public static List<SosCandidate> GetCandidates()
        {
            return InspectCandidates().OrderByDescending(item => item.MemoryBytes).Take(12).ToList();
        }

        internal static List<SosCandidate> GetFocusShieldCandidates()
        {
            return InspectCandidates()
                .OrderByDescending(item => item.CpuPercent >= 12d ? 100000d + item.CpuPercent : item.MemoryBytes / (1024d * 1024d))
                .Take(16)
                .ToList();
        }

        private static List<SosCandidate> InspectCandidates()
        {
            Dictionary<string, SosCandidate> grouped = new Dictionary<string, SosCandidate>(StringComparer.OrdinalIgnoreCase);
            string current = Process.GetCurrentProcess().ProcessName;
            foreach (Process process in Process.GetProcesses())
            {
                using (process)
                {
                    try
                    {
                        string name = process.ProcessName;
                        if (string.Equals(name, current, StringComparison.OrdinalIgnoreCase) || IsProtectedProcessName(name)) continue;
                        SosCandidate candidate;
                        if (!grouped.TryGetValue(name, out candidate))
                        {
                            candidate = new SosCandidate { ProcessName = name, DisplayName = SystemInfo.FriendlyProcessName(name) };
                            grouped.Add(name, candidate);
                        }
                        candidate.ProcessCount++;
                        candidate.MemoryBytes += Math.Max(0, process.WorkingSet64);
                        if (string.IsNullOrWhiteSpace(candidate.ExecutablePath))
                        {
                            try { candidate.ExecutablePath = process.MainModule.FileName; }
                            catch { }
                        }
                        if (process.MainWindowHandle != IntPtr.Zero) candidate.VisibleWindows++;
                    }
                    catch { }
                }
            }
            List<SosCandidate> visible = grouped.Values.Where(item => item.VisibleWindows > 0).ToList();
            foreach (SosCandidate candidate in visible)
            {
                ProcessFamilyMetrics family = ProcessFamilyInspector.GetMetrics(candidate.ProcessName);
                if (family.ProcessCount > candidate.ProcessCount) candidate.ProcessCount = family.ProcessCount;
                if (family.WorkingSetBytes > candidate.MemoryBytes) candidate.MemoryBytes = family.WorkingSetBytes;
                candidate.CpuPercent = ProcessFamilyCpuTracker.Measure(candidate.ProcessName, family.ProcessorTimeTicks);
            }
            return visible;
        }

        public static SosCloseResult RequestGracefulClose(string processName)
        {
            SosCloseResult result = new SosCloseResult();
            if (IsProtectedProcessName(processName) ||
                string.Equals(processName, Process.GetCurrentProcess().ProcessName, StringComparison.OrdinalIgnoreCase)) return result;
            foreach (Process process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    try
                    {
                        if (process.MainWindowHandle != IntPtr.Zero && process.CloseMainWindow()) result.RequestsSent++;
                    }
                    catch { result.AccessErrors++; }
                }
            }
            return result;
        }
    }

    internal sealed class SosForm : Form
    {
        private readonly ListView _applications = new ListView();
        private readonly ImageList _applicationIcons = new ImageList();
        private readonly Label _memory = new Label();
        private readonly Label _result = new Label();
        private readonly Button _focus = new Button();
        private readonly Button _advanced = new Button();
        private readonly Timer _outcomeTimer = new Timer();
        private readonly string _recommendedProcessName;
        private List<SosCandidate> _candidates = new List<SosCandidate>();
        private bool _closing;
        private bool _busy;

        public SosForm(string recommendedProcessName = null)
        {
            _recommendedProcessName = recommendedProcessName;
            Text = "Acelerar aplicativo — Neck";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(820, 680);
            MinimumSize = new Size(760, 620);
            BackColor = Theme.Background;
            Font = Theme.Body;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            _applicationIcons.ColorDepth = ColorDepth.Depth32Bit;
            _applicationIcons.ImageSize = new Size(20, 20);
            BuildInterface();
            _outcomeTimer.Interval = 2000;
            _outcomeTimer.Tick += delegate
            {
                OptimizationOutcome outcome = OptimizationOutcomeMonitor.Refresh();
                if (outcome == null) return;
                _result.Text = outcome.Summary;
                if (outcome.Complete) _outcomeTimer.Stop();
            };
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (_busy)
                {
                    e.Cancel = true;
                    MessageBox.Show("Aguarde só mais alguns segundos para o Neck terminar.", "Neck", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                _closing = true;
            };
            FormClosed += delegate
            {
                _outcomeTimer.Stop();
                _outcomeTimer.Dispose();
                _applicationIcons.Dispose();
            };
            Shown += delegate { RefreshSnapshot(); };
        }

        private void BuildInterface()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 120, BackColor = Theme.Navy };
            header.Controls.Add(new Label
            {
                Text = "ACELERAR",
                AutoSize = false,
                Size = new Size(92, 31),
                BackColor = Theme.Blue,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
                Location = new Point(30, 22)
            });
            header.Controls.Add(new Label
            {
                Text = "Qual aplicativo precisa ficar mais rápido?",
                AutoSize = true,
                Font = new Font("Bahnschrift", 20f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(138, 20)
            });
            header.Controls.Add(new Label
            {
                Text = "Escolha um aplicativo. O Neck cuida do resto automaticamente.",
                AutoSize = true,
                Font = Theme.Body,
                ForeColor = Color.FromArgb(186, 199, 218),
                Location = new Point(32, 74)
            });

            TableLayoutPanel body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(26, 20, 26, 18),
                ColumnCount = 1,
                RowCount = 5,
                BackColor = Theme.Background
            };
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));

            _memory.Dock = DockStyle.Fill;
            _memory.BackColor = Color.White;
            _memory.ForeColor = Theme.Text;
            _memory.Font = Theme.Heading;
            _memory.TextAlign = ContentAlignment.MiddleLeft;
            _memory.Padding = new Padding(18, 0, 10, 0);

            Label instruction = new Label
            {
                Dock = DockStyle.Fill,
                Text = "1. Selecione o aplicativo",
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                ForeColor = Theme.Text,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _applications.Dock = DockStyle.Fill;
            _applications.View = View.Details;
            _applications.FullRowSelect = true;
            _applications.MultiSelect = false;
            _applications.HideSelection = false;
            _applications.BorderStyle = BorderStyle.FixedSingle;
            _applications.BackColor = Color.White;
            _applications.ForeColor = Theme.Text;
            _applications.Font = new Font("Segoe UI", 10.5f);
            _applications.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            _applications.SmallImageList = _applicationIcons;
            _applications.Columns.Add("Aplicativo", 365);
            _applications.Columns.Add("Uso de memória", 155, HorizontalAlignment.Right);
            _applications.Columns.Add("Situação", 205);
            _applications.SelectedIndexChanged += delegate { UpdateSelectionState(); };
            _applications.DoubleClick += async delegate { await ToggleFocusAsync(); };

            _result.Dock = DockStyle.Fill;
            _result.BackColor = Color.White;
            _result.Text = "Selecione um aplicativo acima. Nenhum arquivo ou janela será fechado.";
            _result.Font = Theme.Small;
            _result.ForeColor = Theme.Muted;
            _result.TextAlign = ContentAlignment.MiddleLeft;
            _result.Padding = new Padding(16, 0, 10, 0);

            FlowLayoutPanel footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 12, 0, 0)
            };
            ConfigureButton(_focus, "Acelerar por 1 hora", Theme.Blue, 260);
            ConfigureButton(_advanced, "Mais opções", Theme.NavySoft, 150);
            Button back = new Button();
            ConfigureButton(back, "Voltar", Color.FromArgb(100, 116, 139), 110);
            _focus.Enabled = false;
            _advanced.Enabled = false;
            _focus.Click += async delegate { await ToggleFocusAsync(); };
            _advanced.Click += delegate { ShowAdvancedOptions(); };
            back.Click += delegate { Close(); };
            footer.Controls.Add(_focus);
            footer.Controls.Add(_advanced);
            footer.Controls.Add(back);

            body.Controls.Add(_memory, 0, 0);
            body.Controls.Add(instruction, 0, 1);
            body.Controls.Add(_applications, 0, 2);
            body.Controls.Add(_result, 0, 3);
            body.Controls.Add(footer, 0, 4);
            Controls.Add(body);
            Controls.Add(header);
        }

        private void RefreshSnapshot()
        {
            if (_closing || IsDisposed) return;
            string selectedName = _applications.SelectedItems.Count == 1
                ? ((_applications.SelectedItems[0].Tag as SosCandidate) ?? new SosCandidate()).ProcessName
                : !string.IsNullOrWhiteSpace(FocusModeManager.ActiveProcessName)
                    ? FocusModeManager.ActiveProcessName
                    : _recommendedProcessName;
            MemoryStatus status = SystemInfo.GetMemoryStatus();
            _memory.Text = status.PercentUsed.ToString("0", CultureInfo.CurrentCulture) + "% da memória em uso     |     " +
                           MainForm.FormatBytes((long)status.AvailableBytes) + " disponíveis";
            _memory.ForeColor = status.PercentUsed >= 90 ? Color.Firebrick : status.PercentUsed >= 75 ? Theme.Amber : Theme.Green;

            _candidates = SosInspector.GetCandidates();
            _applications.Items.Clear();
            foreach (SosCandidate candidate in _candidates)
            {
                string imageKey = EnsureApplicationIcon(candidate);
                ListViewItem item = new ListViewItem(candidate.DisplayName) { Tag = candidate, ImageKey = imageKey };
                item.SubItems.Add(MainForm.FormatBytes(candidate.MemoryBytes));
                string state = FocusModeManager.IsTarget(candidate.ProcessName)
                    ? FocusModeManager.GetStateLabel(candidate.ProcessName)
                    : EfficiencyModeManager.IsActive(candidate.ProcessName) ? "Usando menos recursos"
                    : string.Equals(candidate.ProcessName, _recommendedProcessName, StringComparison.OrdinalIgnoreCase)
                        ? "Recomendado pelo Neck" : "Disponível";
                item.SubItems.Add(state);
                if (string.Equals(candidate.ProcessName, _recommendedProcessName, StringComparison.OrdinalIgnoreCase))
                {
                    item.BackColor = Theme.BlueSoft;
                    item.Font = new Font(_applications.Font, FontStyle.Bold);
                }
                _applications.Items.Add(item);
            }
            SelectProcess(selectedName);
            if (!_outcomeTimer.Enabled && !FocusModeManager.IsActive && !string.IsNullOrWhiteSpace(_recommendedProcessName))
            {
                SosCandidate recommended = _candidates.FirstOrDefault(item =>
                    string.Equals(item.ProcessName, _recommendedProcessName, StringComparison.OrdinalIgnoreCase));
                if (recommended != null)
                    _result.Text = "Recomendado: " + recommended.DisplayName + " concentra o maior uso no gargalo atual. Você continua no controle.";
            }
            UpdateSelectionState();
        }

        private string EnsureApplicationIcon(SosCandidate candidate)
        {
            string key = string.IsNullOrWhiteSpace(candidate.ProcessName) ? "__default" : candidate.ProcessName.ToLowerInvariant();
            if (_applicationIcons.Images.ContainsKey(key)) return key;
            try
            {
                if (!string.IsNullOrWhiteSpace(candidate.ExecutablePath))
                {
                    using (Icon icon = Icon.ExtractAssociatedIcon(candidate.ExecutablePath))
                    {
                        if (icon != null)
                        {
                            using (Bitmap bitmap = icon.ToBitmap()) _applicationIcons.Images.Add(key, bitmap);
                            return key;
                        }
                    }
                }
            }
            catch { }
            if (!_applicationIcons.Images.ContainsKey("__default"))
            {
                using (Bitmap bitmap = SystemIcons.Application.ToBitmap()) _applicationIcons.Images.Add("__default", bitmap);
            }
            return "__default";
        }

        private async Task ToggleFocusAsync()
        {
            if (_busy || _applications.SelectedItems.Count != 1) return;
            SosCandidate candidate = _applications.SelectedItems[0].Tag as SosCandidate;
            if (candidate == null) return;

            if (FocusModeManager.IsTarget(candidate.ProcessName))
            {
                SetBusy(true, "Parando a aceleração e restaurando as configurações...");
                try
                {
                    await Task.Run(delegate { FocusModeManager.Stop(); });
                    OptimizationOutcomeMonitor.Cancel(candidate.ProcessName);
                    _outcomeTimer.Stop();
                    if (!_closing && !IsDisposed) _result.Text = candidate.DisplayName + " voltou ao funcionamento normal.";
                }
                finally
                {
                    if (!_closing && !IsDisposed)
                    {
                        SetBusy(false, null);
                        RefreshSnapshot();
                        SelectProcess(candidate.ProcessName);
                    }
                }
                return;
            }

            string replacing = FocusModeManager.IsActive
                ? " A aceleração atual de " + FocusModeManager.ActiveDisplayName + " será substituída."
                : string.Empty;
            string explanation = candidate.DisplayName + " ficará mais rápido quando você estiver usando a janela." + replacing + "\n\n" +
                                 "Se houver pressão, o Escudo de Foco poderá reduzir temporariamente até três aplicativos pesados que estejam em segundo plano. Aplicativos dedicados conhecidos de comunicação, áudio e vídeo ficam protegidos.\n\n" +
                                 "Ao trocar de janela, o Escudo restaura os concorrentes e reduz o consumo de " + candidate.DisplayName + ". Depois de uma hora, tudo volta ao normal automaticamente.\n\n" +
                                 "Nenhuma janela, documento ou arquivo será fechado.";
            if (MessageBox.Show(explanation, "Acelerar " + candidate.DisplayName + "?", MessageBoxButtons.OKCancel,
                MessageBoxIcon.Information) != DialogResult.OK) return;

            SetBusy(true, "Preparando " + candidate.DisplayName + "...");
            try
            {
                MemoryStatus memoryBefore = SystemInfo.GetMemoryStatus();
                long appMemoryBefore = ProcessFamilyInspector.GetMetrics(candidate.ProcessName).WorkingSetBytes;
                FocusModeResult modeResult = null;
                await Task.Run(delegate { modeResult = FocusModeManager.Start(candidate.ProcessName, candidate.DisplayName, 60); });
                if (_closing || IsDisposed) return;
                if (FocusModeManager.IsTarget(candidate.ProcessName))
                {
                    OptimizationOutcomeMonitor.Begin(candidate.ProcessName, candidate.DisplayName, modeResult, memoryBefore, appMemoryBefore);
                    _result.Text = "Aceleração ativa. O Neck está medindo o resultado observado por alguns segundos...";
                    _outcomeTimer.Start();
                }
                else _result.Text = "O Windows não permitiu preparar esse aplicativo agora.";
            }
            catch (Exception ex)
            {
                if (!_closing && !IsDisposed) _result.Text = "Não foi possível preparar o aplicativo: " + ex.Message;
            }
            finally
            {
                if (!_closing && !IsDisposed)
                {
                    SetBusy(false, null);
                    RefreshSnapshot();
                    SelectProcess(candidate.ProcessName);
                }
            }
        }

        private void ShowAdvancedOptions()
        {
            if (_busy || _applications.SelectedItems.Count != 1) return;
            SosCandidate candidate = _applications.SelectedItems[0].Tag as SosCandidate;
            if (candidate == null) return;
            using (AdvancedAppOptionsForm form = new AdvancedAppOptionsForm(candidate)) form.ShowDialog(this);
            RefreshSnapshot();
            SelectProcess(candidate.ProcessName);
        }

        private void SetBusy(bool busy, string message)
        {
            if (_closing || IsDisposed) return;
            _busy = busy;
            UpdateSelectionState();
            if (!string.IsNullOrWhiteSpace(message)) _result.Text = message;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private void UpdateSelectionState()
        {
            bool selected = !_busy && _applications.SelectedItems.Count == 1;
            _focus.Enabled = selected;
            _advanced.Enabled = selected;
            SosCandidate candidate = selected ? _applications.SelectedItems[0].Tag as SosCandidate : null;
            bool active = candidate != null && FocusModeManager.IsTarget(candidate.ProcessName);
            _focus.Text = active ? "Parar aceleração" : "Acelerar por 1 hora";
            _focus.BackColor = active ? Theme.Amber : Theme.Blue;
        }

        private void SelectProcess(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return;
            foreach (ListViewItem item in _applications.Items)
            {
                SosCandidate candidate = item.Tag as SosCandidate;
                if (candidate == null || !string.Equals(candidate.ProcessName, processName, StringComparison.OrdinalIgnoreCase)) continue;
                item.Selected = true;
                item.Focused = true;
                item.EnsureVisible();
                break;
            }
        }

        private static void ConfigureButton(Button button, string text, Color color, int width)
        {
            button.Text = text;
            button.Width = width;
            button.Height = 44;
            button.BackColor = color;
            button.ForeColor = Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
            button.Margin = new Padding(0, 0, 12, 0);
            button.Cursor = Cursors.Hand;
        }
    }

    internal sealed class AdvancedAppOptionsForm : Form
    {
        private readonly SosCandidate _candidate;
        private readonly Button _adaptive = new Button();
        private readonly Label _status = new Label();
        private bool _busy;

        public AdvancedAppOptionsForm(SosCandidate candidate)
        {
            _candidate = candidate;
            Text = "Mais opções — " + candidate.DisplayName;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(620, 430);
            MinimumSize = new Size(580, 400);
            BackColor = Theme.Background;
            Font = Theme.Body;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            BuildInterface();
            UpdateState();
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (!_busy) return;
                e.Cancel = true;
                MessageBox.Show("Aguarde o Neck terminar esta ação.", "Neck", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
        }

        private void BuildInterface()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 104, BackColor = Theme.Navy };
            header.Controls.Add(new Label { Text = "Controles avançados", AutoSize = true, Font = new Font("Segoe UI Semibold", 21f, FontStyle.Bold), ForeColor = Color.White, Location = new Point(28, 18) });
            header.Controls.Add(new Label { Text = _candidate.DisplayName + " • opções para usuários experientes", AutoSize = true, Font = Theme.Body, ForeColor = Color.FromArgb(186, 199, 218), Location = new Point(30, 61) });

            Panel body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(28), BackColor = Theme.Background };
            Label explanation = new Label
            {
                Text = "Aqui ficam ações que normalmente não são necessárias. O modo Acelerar já alterna sozinho entre desempenho e economia.",
                AutoSize = false,
                Size = new Size(540, 54),
                Location = new Point(28, 24),
                Font = Theme.Body,
                ForeColor = Theme.Muted
            };
            _status.AutoSize = false;
            _status.Size = new Size(540, 48);
            _status.Location = new Point(28, 82);
            _status.BackColor = Color.White;
            _status.Padding = new Padding(14, 0, 8, 0);
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.ForeColor = Theme.Text;

            ConfigureButton(_adaptive, "Reduzir em segundo plano", Theme.Green, 220);
            _adaptive.Location = new Point(28, 150);
            Button closeApp = new Button();
            ConfigureButton(closeApp, "Pedir para fechar", Color.FromArgb(185, 28, 28), 160);
            closeApp.Location = new Point(260, 150);
            Button manager = new Button();
            ConfigureButton(manager, "Gerenciador de Tarefas", Theme.NavySoft, 220);
            manager.Location = new Point(28, 210);
            Button back = new Button();
            ConfigureButton(back, "Voltar", Color.FromArgb(100, 116, 139), 120);
            back.Location = new Point(260, 210);

            _adaptive.Click += async delegate { await ToggleAdaptiveAsync(); };
            closeApp.Click += async delegate { await RequestCloseAsync(); };
            manager.Click += delegate { MainForm.OpenTarget("taskmgr.exe"); };
            back.Click += delegate { Close(); };
            body.Controls.Add(explanation);
            body.Controls.Add(_status);
            body.Controls.Add(_adaptive);
            body.Controls.Add(closeApp);
            body.Controls.Add(manager);
            body.Controls.Add(back);
            Controls.Add(body);
            Controls.Add(header);
        }

        private async Task ToggleAdaptiveAsync()
        {
            if (_busy || FocusModeManager.IsTarget(_candidate.ProcessName)) return;
            bool active = EfficiencyModeManager.IsActive(_candidate.ProcessName);
            if (!active && MessageBox.Show(
                    _candidate.DisplayName + " continuará aberto, mas usará menos CPU e memória quando estiver em segundo plano.",
                    "Reduzir consumo de " + _candidate.DisplayName + "?", MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Information) != DialogResult.OK) return;
            SetBusy(true, active ? "Restaurando..." : "Reduzindo consumo...");
            try
            {
                await Task.Run(delegate
                {
                    AutopilotProtectionManager.ReleaseForManualControl(_candidate.ProcessName);
                    if (active) EfficiencyModeManager.Restore(_candidate.ProcessName);
                    else EfficiencyModeManager.Apply(_candidate.ProcessName);
                });
            }
            finally
            {
                if (!IsDisposed)
                {
                    SetBusy(false, null);
                    UpdateState();
                }
            }
        }

        private async Task RequestCloseAsync()
        {
            if (_busy) return;
            if (MessageBox.Show("O Neck pedirá que " + _candidate.DisplayName + " feche normalmente. Salve seu trabalho antes de continuar.",
                "Fechar " + _candidate.DisplayName + "?", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
            SetBusy(true, "Enviando pedido de fechamento...");
            try
            {
                SosCloseResult result = await Task.Run(delegate { return SosInspector.RequestGracefulClose(_candidate.ProcessName); });
                if (!IsDisposed) _status.Text = result.RequestsSent > 0
                    ? "Pedido enviado. O aplicativo pode solicitar que você salve o trabalho."
                    : "O aplicativo não aceitou o pedido. Nada foi forçado.";
            }
            finally { if (!IsDisposed) SetBusy(false, null); }
        }

        private void UpdateState()
        {
            bool focus = FocusModeManager.IsTarget(_candidate.ProcessName);
            bool adaptive = EfficiencyModeManager.IsActive(_candidate.ProcessName);
            _adaptive.Enabled = !_busy && !focus;
            _adaptive.Text = adaptive && !focus ? "Parar redução" : "Reduzir em segundo plano";
            _adaptive.BackColor = adaptive && !focus ? Theme.Amber : Theme.Green;
            _status.Text = focus
                ? "O modo Acelerar está cuidando automaticamente deste aplicativo."
                : adaptive ? "O aplicativo está usando menos recursos em segundo plano." : "Nenhuma opção avançada está ativa.";
        }

        private void SetBusy(bool busy, string message)
        {
            _busy = busy;
            _adaptive.Enabled = !busy && !FocusModeManager.IsTarget(_candidate.ProcessName);
            if (!string.IsNullOrWhiteSpace(message)) _status.Text = message;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private static void ConfigureButton(Button button, string text, Color color, int width)
        {
            button.Text = text;
            button.Width = width;
            button.Height = 44;
            button.BackColor = color;
            button.ForeColor = Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
        }
    }
}
