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
                        if (process.MainWindowHandle != IntPtr.Zero) candidate.VisibleWindows++;
                    }
                    catch { }
                }
            }
            return grouped.Values
                .Where(item => item.VisibleWindows > 0)
                .OrderByDescending(item => item.MemoryBytes)
                .Take(12)
                .ToList();
        }

        public static SosCloseResult RequestGracefulClose(string processName)
        {
            SosCloseResult result = new SosCloseResult();
            if (IsProtectedProcessName(processName) ||
                string.Equals(processName, Process.GetCurrentProcess().ProcessName, StringComparison.OrdinalIgnoreCase))
                return result;

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
        private readonly Label _result = new Label();
        private readonly Label _memory = new Label();
        private readonly Button _lightMode = new Button();
        private readonly Button _closeApplication = new Button();
        private readonly Button _clean = new Button();
        private readonly Button _taskManager = new Button();
        private List<SosCandidate> _candidates = new List<SosCandidate>();
        private bool _closing;
        private bool _busy;

        public SosForm()
        {
            Text = "SOS Neck — alívio seguro";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(880, 700);
            MinimumSize = new Size(800, 640);
            BackColor = Theme.Background;
            Font = Theme.Body;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            BuildInterface();
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (_busy)
                {
                    e.Cancel = true;
                    MessageBox.Show("A ação do SOS ainda está terminando. Aguarde alguns segundos.", "SOS Neck", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                _closing = true;
            };
            Shown += delegate { RefreshSnapshot(); };
        }

        private void BuildInterface()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 118, BackColor = Theme.Navy };
            Label badge = new Label
            {
                Text = "SOS",
                AutoSize = false,
                Size = new Size(70, 31),
                BackColor = Color.FromArgb(127, 29, 29),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
                Location = new Point(30, 22)
            };
            header.Controls.Add(badge);
            header.Controls.Add(new Label
            {
                Text = "Alívio seguro, sem força bruta",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 21f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(118, 19)
            });
            header.Controls.Add(new Label
            {
                Text = "Escolha uma ação. O Neck nunca força o encerramento de um aplicativo.",
                AutoSize = true,
                Font = Theme.Body,
                ForeColor = Color.FromArgb(186, 199, 218),
                Location = new Point(32, 72)
            });

            TableLayoutPanel body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 20, 24, 20),
                ColumnCount = 1,
                RowCount = 5,
                BackColor = Theme.Background
            };
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));

            _memory.Dock = DockStyle.Fill;
            _memory.BackColor = Color.White;
            _memory.ForeColor = Theme.Text;
            _memory.Font = Theme.Heading;
            _memory.TextAlign = ContentAlignment.MiddleLeft;
            _memory.Padding = new Padding(18, 0, 10, 0);

            Label explanation = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Aplicativos com janela aberta, ordenados pelo uso aproximado de memória:",
                Font = Theme.Small,
                ForeColor = Theme.Muted,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _applications.Dock = DockStyle.Fill;
            _applications.View = View.Details;
            _applications.FullRowSelect = true;
            _applications.MultiSelect = false;
            _applications.BorderStyle = BorderStyle.FixedSingle;
            _applications.BackColor = Color.White;
            _applications.ForeColor = Theme.Text;
            _applications.Font = Theme.Body;
            _applications.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            _applications.Columns.Add("Aplicativo", 330);
            _applications.Columns.Add("Processos", 100, HorizontalAlignment.Center);
            _applications.Columns.Add("Memória", 145, HorizontalAlignment.Right);
            _applications.Columns.Add("Modo", 100, HorizontalAlignment.Center);
            _applications.SelectedIndexChanged += delegate { UpdateSelectionState(); };

            _result.Dock = DockStyle.Fill;
            _result.Text = "Selecione um aplicativo somente se você tiver salvo seu trabalho.";
            _result.Font = Theme.Small;
            _result.ForeColor = Theme.Muted;
            _result.TextAlign = ContentAlignment.MiddleLeft;

            FlowLayoutPanel footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 12, 0, 0) };
            ConfigureButton(_lightMode, "Ativar Modo Leve", Theme.Green, 150);
            ConfigureButton(_closeApplication, "Pedir fechamento", Color.FromArgb(185, 28, 28), 145);
            ConfigureButton(_clean, "Limpeza segura", Theme.Blue, 130);
            ConfigureButton(_taskManager, "Gerenciador", Theme.NavySoft, 145);
            Button close = new Button();
            ConfigureButton(close, "Fechar", Theme.NavySoft, 90);
            _lightMode.Enabled = false;
            _closeApplication.Enabled = false;
            _lightMode.Click += async delegate { await ToggleLightModeAsync(); };
            _closeApplication.Click += async delegate { await CloseSelectedAsync(); };
            _clean.Click += async delegate { await CleanAsync(); };
            _taskManager.Click += delegate { MainForm.OpenTarget("taskmgr.exe"); };
            close.Click += delegate { Close(); };
            footer.Controls.Add(_lightMode);
            footer.Controls.Add(_closeApplication);
            footer.Controls.Add(_clean);
            footer.Controls.Add(_taskManager);
            footer.Controls.Add(close);

            body.Controls.Add(_memory, 0, 0);
            body.Controls.Add(explanation, 0, 1);
            body.Controls.Add(_applications, 0, 2);
            body.Controls.Add(_result, 0, 3);
            body.Controls.Add(footer, 0, 4);
            Controls.Add(body);
            Controls.Add(header);
        }

        private void RefreshSnapshot()
        {
            if (_closing || IsDisposed) return;
            MemoryStatus status = SystemInfo.GetMemoryStatus();
            _memory.Text = "RAM em uso: " + status.PercentUsed.ToString("0", CultureInfo.CurrentCulture) + "%     •     Disponível: " + MainForm.FormatBytes((long)status.AvailableBytes);
            _memory.ForeColor = status.PercentUsed >= 90 ? Color.Firebrick : status.PercentUsed >= 75 ? Theme.Amber : Theme.Green;
            _candidates = SosInspector.GetCandidates();
            _applications.Items.Clear();
            foreach (SosCandidate candidate in _candidates)
            {
                ListViewItem item = new ListViewItem(candidate.DisplayName) { Tag = candidate };
                item.SubItems.Add(candidate.ProcessCount.ToString(CultureInfo.CurrentCulture));
                item.SubItems.Add(MainForm.FormatBytes(candidate.MemoryBytes));
                item.SubItems.Add(EfficiencyModeManager.IsActive(candidate.ProcessName) ? "Leve" : "Normal");
                _applications.Items.Add(item);
            }
            UpdateSelectionState();
        }

        private async Task ToggleLightModeAsync()
        {
            if (_busy || _applications.SelectedItems.Count != 1) return;
            SosCandidate candidate = _applications.SelectedItems[0].Tag as SosCandidate;
            if (candidate == null) return;

            bool active = EfficiencyModeManager.IsActive(candidate.ProcessName);
            if (!active)
            {
                string warning = "O " + candidate.DisplayName + " continuará aberto, mas receberá menos prioridade de CPU e o modo de eficiência do Windows.\n\n" +
                                 "Isso reduz a disputa por processamento, energia e temperatura. Não libera a RAM já usada imediatamente e pode deixar o aplicativo um pouco menos responsivo.\n\n" +
                                 "Você poderá restaurar o desempenho normal a qualquer momento.";
                if (MessageBox.Show(warning, "Ativar Modo Leve em " + candidate.DisplayName + "?", MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Information) != DialogResult.OK) return;
            }

            SetBusy(true, active ? "Restaurando o desempenho normal..." : "Ativando o Modo Leve...");
            try
            {
                EfficiencyModeResult result = await Task.Run(delegate
                {
                    return active ? EfficiencyModeManager.Restore(candidate.ProcessName) : EfficiencyModeManager.Apply(candidate.ProcessName);
                });
                if (_closing || IsDisposed) return;

                if (active)
                {
                    _result.Text = result.ProcessesChanged > 0
                        ? candidate.DisplayName + " voltou ao modo normal em " + result.ProcessesChanged + " processo(s)."
                        : "O Modo Leve de " + candidate.DisplayName + " foi removido; os processos anteriores já não estavam disponíveis.";
                }
                else if (result.ProcessesChanged > 0)
                {
                    _result.Text = "Modo Leve ativo em " + result.ProcessesChanged + " processo(s) de " + candidate.DisplayName +
                                   ". O aplicativo continua aberto e o Neck acompanhará novos processos.";
                    if (result.AccessErrors > 0) _result.Text += " Alguns processos protegidos não aceitaram a alteração.";
                }
                else
                {
                    _result.Text = "O Windows não permitiu otimizar " + candidate.DisplayName + ". Nenhuma configuração foi deixada ativa.";
                }

                RefreshSnapshot();
                SelectProcess(candidate.ProcessName);
            }
            catch (Exception ex)
            {
                if (!_closing && !IsDisposed) _result.Text = "Não foi possível alterar o Modo Leve: " + ex.Message;
            }
            finally
            {
                if (!_closing && !IsDisposed) SetBusy(false, null);
            }
        }

        private async Task CloseSelectedAsync()
        {
            if (_busy || _applications.SelectedItems.Count != 1) return;
            SosCandidate candidate = _applications.SelectedItems[0].Tag as SosCandidate;
            if (candidate == null) return;
            string warning = "O Neck enviará um pedido normal de fechamento para " + candidate.DisplayName + ".\n\n" +
                             "O aplicativo poderá perguntar se deseja salvar. Se ele não aceitar o pedido, o Neck não forçará o encerramento.\n\nSalve seu trabalho antes de continuar.";
            if (MessageBox.Show(warning, "Fechar " + candidate.DisplayName + "?", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;

            ulong before = SystemInfo.GetMemoryStatus().AvailableBytes;
            SetBusy(true, "Enviando pedido normal de fechamento...");
            try
            {
                SosCloseResult closeResult = await Task.Run(delegate { return SosInspector.RequestGracefulClose(candidate.ProcessName); });
                await Task.Delay(2200);
                if (_closing || IsDisposed) return;
                ulong after = SystemInfo.GetMemoryStatus().AvailableBytes;
                long difference = (long)after - (long)before;
                _result.Text = closeResult.RequestsSent == 0
                    ? candidate.DisplayName + " não aceitou um pedido automático. Use o próprio aplicativo ou o Gerenciador de Tarefas."
                    : "Pedido enviado para " + closeResult.RequestsSent + " janela(s). Variação disponível: " + (difference >= 0 ? "+" : "−") + MainForm.FormatBytes(Math.Abs(difference)) + ".";
                RefreshSnapshot();
            }
            catch (Exception ex)
            {
                if (!_closing && !IsDisposed) _result.Text = "Não foi possível enviar o pedido: " + ex.Message;
            }
            finally
            {
                if (!_closing && !IsDisposed) SetBusy(false, null);
            }
        }

        private async Task CleanAsync()
        {
            if (_busy) return;
            if (MessageBox.Show("Remover temporários antigos e relatórios de erro seguros? Documentos, downloads, senhas e Lixeira não serão tocados.",
                "Limpeza segura do SOS", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;
            SetBusy(true, "Executando limpeza segura...");
            try
            {
                DeleteResult temp = await Task.Run(delegate { return Cleaner.CleanTemp(); });
                DeleteResult reports = await Task.Run(delegate { return Cleaner.CleanReports(); });
                if (_closing || IsDisposed) return;
                long total = temp.BytesDeleted + reports.BytesDeleted;
                _result.Text = "Limpeza concluída: " + MainForm.FormatBytes(total) + " liberados; " +
                               (temp.FilesDeleted + reports.FilesDeleted) + " arquivos removidos.";
                RefreshSnapshot();
            }
            catch (Exception ex)
            {
                if (!_closing && !IsDisposed) _result.Text = "A limpeza segura não foi concluída: " + ex.Message;
            }
            finally
            {
                if (!_closing && !IsDisposed) SetBusy(false, null);
            }
        }

        private void SetBusy(bool busy, string message)
        {
            if (_closing || IsDisposed) return;
            _busy = busy;
            UpdateSelectionState();
            _clean.Enabled = !busy;
            _taskManager.Enabled = !busy;
            if (!string.IsNullOrWhiteSpace(message)) _result.Text = message;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private void UpdateSelectionState()
        {
            bool selected = !_busy && _applications.SelectedIndices.Count == 1;
            _closeApplication.Enabled = selected;
            _lightMode.Enabled = selected;
            if (!selected)
            {
                _lightMode.Text = "Ativar Modo Leve";
                _lightMode.BackColor = Theme.Green;
                return;
            }

            SosCandidate candidate = _applications.SelectedItems[0].Tag as SosCandidate;
            bool active = candidate != null && EfficiencyModeManager.IsActive(candidate.ProcessName);
            _lightMode.Text = active ? "Restaurar normal" : "Ativar Modo Leve";
            _lightMode.BackColor = active ? Theme.Amber : Theme.Green;
        }

        private void SelectProcess(string processName)
        {
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
            button.Height = 42;
            button.BackColor = color;
            button.ForeColor = Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            button.Margin = new Padding(0, 0, 10, 0);
            button.Cursor = Cursors.Hand;
        }
    }
}
