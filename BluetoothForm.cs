using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Neck
{
    internal enum BluetoothRowState
    {
        Good,
        Attention,
        Missing
    }

    internal sealed class BluetoothGlyph : Control
    {
        public BluetoothGlyph()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            Size = new Size(58, 58);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(1, 1, Width - 3, Height - 3);
            using (GraphicsPath background = Rounded(bounds, 17))
            using (SolidBrush brush = new SolidBrush(Theme.Cyan))
                e.Graphics.FillPath(brush, background);

            float middle = Width / 2f;
            using (Pen pen = new Pen(Color.White, 3.2f))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                e.Graphics.DrawLine(pen, middle, 12, middle, Height - 12);
                e.Graphics.DrawLine(pen, middle, 12, middle + 12, 22);
                e.Graphics.DrawLine(pen, middle + 12, 22, middle - 9, Height - 18);
                e.Graphics.DrawLine(pen, middle - 9, 18, middle + 12, Height - 22);
                e.Graphics.DrawLine(pen, middle + 12, Height - 22, middle, Height - 12);
            }
        }

        private static GraphicsPath Rounded(Rectangle bounds, int radius)
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
    }

    internal sealed class BluetoothCheckRow : UserControl
    {
        private readonly RoundedPanel _card = new RoundedPanel();
        private readonly Label _symbol = new Label();
        private readonly Label _title = new Label();
        private readonly Label _detail = new Label();

        public BluetoothCheckRow(string title)
        {
            Height = 62;
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Margin = new Padding(0, 0, 0, 8);
            _card.Dock = DockStyle.Fill;
            _card.BackColor = Color.White;
            _card.OutlineColor = Theme.Border;
            _card.CornerRadius = 14;

            _symbol.AutoSize = false;
            _symbol.Size = new Size(38, 38);
            _symbol.Location = new Point(14, 12);
            _symbol.TextAlign = ContentAlignment.MiddleCenter;
            _symbol.Font = new Font("Segoe UI Symbol", 14f, FontStyle.Bold);

            _title.Text = title;
            _title.AutoSize = false;
            _title.Location = new Point(62, 10);
            _title.Size = new Size(220, 22);
            _title.Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold);
            _title.ForeColor = Theme.Text;

            _detail.AutoEllipsis = true;
            _detail.AutoSize = false;
            _detail.Location = new Point(62, 32);
            _detail.Size = new Size(650, 20);
            _detail.Font = Theme.Small;
            _detail.ForeColor = Theme.Muted;

            Resize += delegate { _detail.Width = Math.Max(80, ClientSize.Width - 82); };
            _card.Controls.Add(_detail);
            _card.Controls.Add(_title);
            _card.Controls.Add(_symbol);
            Controls.Add(_card);
        }

        public void SetState(BluetoothRowState state, string detail)
        {
            Color color = state == BluetoothRowState.Good ? Theme.Green :
                          state == BluetoothRowState.Attention ? Theme.Amber : Color.FromArgb(190, 55, 55);
            _symbol.Text = state == BluetoothRowState.Good ? "✓" : "!";
            _symbol.ForeColor = color;
            _symbol.BackColor = VisualEffects.Blend(color, Color.White, 0.88d);
            _detail.Text = detail;
        }
    }

    internal sealed class BluetoothDoctorForm : Form
    {
        private readonly Label _badge = new Label();
        private readonly Label _headline = new Label();
        private readonly Label _summary = new Label();
        private readonly BluetoothCheckRow _adapterRow = new BluetoothCheckRow("Adaptador");
        private readonly BluetoothCheckRow _driverRow = new BluetoothCheckRow("Driver");
        private readonly BluetoothCheckRow _servicesRow = new BluetoothCheckRow("Serviços do Windows");
        private readonly AnimatedButton _repairButton = new AnimatedButton();
        private readonly AnimatedButton _settingsButton = new AnimatedButton();
        private readonly Label _actionDetail = new Label();
        private readonly Label _activity = new Label();
        private readonly ActivityBar _progress = new ActivityBar();
        private readonly LinkLabel _reportLink = new LinkLabel();
        private readonly LinkLabel _updatesLink = new LinkLabel();
        private readonly BluetoothSnapshot _initialSnapshot;
        private BluetoothSnapshot _snapshot;
        private string _technicalReport = "";
        private bool _busy;
        private bool _closing;
        private bool _initialized;

        public BluetoothDoctorForm() : this(null) { }

        internal BluetoothDoctorForm(BluetoothSnapshot initialSnapshot)
        {
            _initialSnapshot = initialSnapshot;
            Text = "Cura Bluetooth — Neck";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(870, 720);
            MinimumSize = new Size(790, 700);
            BackColor = Theme.Background;
            Font = Theme.Body;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            BuildInterface();
            Shown += async delegate
            {
                VisualEffects.FadeIn(this);
                if (_initialSnapshot != null) ApplySnapshot(_initialSnapshot, "Diagnóstico local pronto.");
                else await RefreshAsync("Verificando o Bluetooth...");
                _initialized = true;
            };
            Activated += async delegate
            {
                if (_initialized && !_busy && !_closing) await RefreshAsync("Confirmando o estado do Bluetooth...");
            };
            FormClosing += delegate { _closing = true; };
        }

        private void BuildInterface()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 110, BackColor = Theme.Card };
            BluetoothGlyph glyph = new BluetoothGlyph { Location = new Point(28, 27) };
            header.Controls.Add(glyph);
            header.Controls.Add(new Label
            {
                Text = "Cura Bluetooth",
                AutoSize = true,
                Font = Theme.Title,
                ForeColor = Theme.Ink,
                Location = new Point(104, 22)
            });
            header.Controls.Add(new Label
            {
                Text = "Diagnostica e restaura a conexão sem apagar seus dispositivos.",
                AutoSize = true,
                Font = Theme.Body,
                ForeColor = Theme.Muted,
                Location = new Point(108, 70)
            });

            TableLayoutPanel body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 20, 24, 20),
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Theme.Background
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            RoundedPanel status = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                OutlineColor = Theme.Border,
                CornerRadius = 17,
                Margin = new Padding(0, 0, 0, 12),
                Padding = new Padding(24, 16, 24, 14)
            };
            _badge.AutoSize = false;
            _badge.Size = new Size(142, 25);
            _badge.Location = new Point(24, 15);
            _badge.TextAlign = ContentAlignment.MiddleCenter;
            _badge.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
            _headline.AutoSize = false;
            _headline.Location = new Point(24, 48);
            _headline.Size = new Size(750, 31);
            _headline.Font = Theme.Heading;
            _headline.ForeColor = Theme.Text;
            _summary.AutoSize = false;
            _summary.Location = new Point(24, 81);
            _summary.Size = new Size(750, 24);
            _summary.AutoEllipsis = true;
            _summary.Font = Theme.Small;
            _summary.ForeColor = Theme.Muted;
            status.Resize += delegate
            {
                _headline.Width = Math.Max(120, status.ClientSize.Width - 48);
                _summary.Width = Math.Max(120, status.ClientSize.Width - 48);
            };
            status.Controls.Add(_summary);
            status.Controls.Add(_headline);
            status.Controls.Add(_badge);

            TableLayoutPanel checks = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0, 0, 0, 12),
                BackColor = Theme.Background
            };
            checks.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            checks.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333f));
            checks.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333f));
            checks.RowStyles.Add(new RowStyle(SizeType.Percent, 33.334f));
            checks.Controls.Add(_adapterRow, 0, 0);
            checks.Controls.Add(_driverRow, 0, 1);
            checks.Controls.Add(_servicesRow, 0, 2);

            RoundedPanel action = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                OutlineColor = Theme.Border,
                CornerRadius = 17,
                Margin = new Padding(0),
                Padding = new Padding(24, 17, 24, 15)
            };
            Label actionTitle = new Label
            {
                Text = "O Bluetooth parou de conectar?",
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 26,
                Font = Theme.Heading,
                ForeColor = Theme.Text
            };
            _actionDetail.Text = "A cura reinicia somente o rádio e os serviços Bluetooth. Fones, mouse ou teclado podem desconectar por alguns segundos.";
            _actionDetail.AutoSize = false;
            _actionDetail.Dock = DockStyle.Top;
            _actionDetail.Height = 38;
            _actionDetail.Font = Theme.Small;
            _actionDetail.ForeColor = Theme.Muted;
            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 45,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };
            ConfigureButton(_repairButton, "Tentar corrigir agora", Theme.Blue, 250);
            _repairButton.AttentionPulse = true;
            _repairButton.Click += async delegate { await RepairAsync(); };
            ConfigureButton(_settingsButton, "Abrir Bluetooth", Theme.NavySoft, 175);
            _settingsButton.Click += delegate { OpenBluetoothSettings(); };
            actions.Controls.Add(_repairButton);
            actions.Controls.Add(_settingsButton);

            Panel result = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            _progress.Dock = DockStyle.Bottom;
            _progress.Height = 4;
            _activity.AutoSize = false;
            _activity.Dock = DockStyle.Fill;
            _activity.Padding = new Padding(0, 8, 285, 0);
            _activity.Font = Theme.Small;
            _activity.ForeColor = Theme.Muted;
            _activity.Text = "O diagnóstico é feito somente neste computador.";
            _reportLink.Text = "Ver relatório";
            _reportLink.AutoSize = true;
            _reportLink.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _reportLink.LinkColor = Theme.Blue;
            _reportLink.Visible = false;
            _reportLink.Location = new Point(result.Width - 105, 9);
            _reportLink.Click += delegate
            {
                MessageBox.Show(_technicalReport, "Relatório da cura Bluetooth", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            _updatesLink.Text = "Procurar driver oficial";
            _updatesLink.AutoSize = true;
            _updatesLink.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _updatesLink.LinkColor = Theme.Blue;
            _updatesLink.Visible = false;
            _updatesLink.Location = new Point(result.Width - 250, 9);
            _updatesLink.Click += delegate { MainForm.OpenTarget("ms-settings:windowsupdate-optionalupdates"); };
            result.Resize += delegate
            {
                _reportLink.Left = result.ClientSize.Width - _reportLink.Width - 4;
                _updatesLink.Left = _reportLink.Left - _updatesLink.Width - 22;
            };
            result.Controls.Add(_reportLink);
            result.Controls.Add(_updatesLink);
            result.Controls.Add(_activity);
            result.Controls.Add(_progress);
            _reportLink.BringToFront();
            _updatesLink.BringToFront();
            _progress.BringToFront();

            action.Controls.Add(result);
            action.Controls.Add(actions);
            action.Controls.Add(_actionDetail);
            action.Controls.Add(actionTitle);
            body.Controls.Add(status, 0, 0);
            body.Controls.Add(checks, 0, 1);
            body.Controls.Add(action, 0, 2);
            Controls.Add(body);
            Controls.Add(header);
        }

        private async Task RefreshAsync(string activity)
        {
            if (_busy || _closing || IsDisposed) return;
            SetBusy(true, activity);
            try
            {
                BluetoothSnapshot snapshot = await Task.Run(delegate { return BluetoothDoctor.Read(); });
                if (_closing || IsDisposed) return;
                ApplySnapshot(snapshot, "Diagnóstico atualizado agora.");
            }
            catch (Exception ex)
            {
                if (!_closing && !IsDisposed) _activity.Text = "Não foi possível concluir o diagnóstico: " + ex.Message;
            }
            finally
            {
                if (!_closing && !IsDisposed) SetBusy(false, null);
            }
        }

        private async Task RepairAsync()
        {
            if (_busy || _closing || IsDisposed) return;
            if (IsOnlyTurnedOff(_snapshot))
            {
                OpenBluetoothSettings();
                return;
            }

            DialogResult confirmation = MessageBox.Show(
                "O Neck vai reiniciar o adaptador e os serviços Bluetooth. Seus pareamentos serão preservados, mas os acessórios podem desconectar por alguns segundos.\n\nContinuar?",
                "Executar cura Bluetooth", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
            if (confirmation != DialogResult.OK) return;

            SetBusy(true, "Aguardando a permissão de administrador...");
            try
            {
                _activity.Text = "Reiniciando somente o adaptador Bluetooth...";
                ElevatedTaskResult result = await ElevatedOperations.RunAsync(new[] { "bluetooth" });
                if (_closing || IsDisposed) return;
                _technicalReport = "REPARO DO ADAPTADOR" + Environment.NewLine + (result.Output ?? "");
                _reportLink.Visible = !string.IsNullOrWhiteSpace(_technicalReport);
                if (result.Cancelled)
                {
                    _activity.Text = "Permissão cancelada. Nenhuma alteração foi feita.";
                    return;
                }

                _activity.Text = "Confirmando o estado final...";
                await Task.Delay(900);
                BluetoothSnapshot refreshed = await Task.Run(delegate { return BluetoothDoctor.Read(); });
                if (_closing || IsDisposed) return;
                string message = refreshed.IsHealthy
                    ? "Cura concluída. O Bluetooth está respondendo e seus pareamentos foram preservados."
                    : refreshed.PowerState == BluetoothPowerState.Off
                        ? "O adaptador voltou. Agora use Abrir e ligar no Windows; o Neck não força essa chave."
                        : "A cura terminou, mas o driver ainda precisa de atenção. Abra as atualizações oficiais abaixo.";
                ApplySnapshot(refreshed, message);
            }
            catch (Exception ex)
            {
                if (!_closing && !IsDisposed) _activity.Text = "A cura não pôde ser concluída: " + ex.Message;
            }
            finally
            {
                if (!_closing && !IsDisposed) SetBusy(false, null);
            }
        }

        private void ApplySnapshot(BluetoothSnapshot snapshot, string activity)
        {
            _snapshot = snapshot ?? new BluetoothSnapshot();
            BluetoothAdapterInfo adapter = _snapshot.PrimaryAdapter;
            BluetoothServiceInfo support = _snapshot.SupportService;

            if (_snapshot.IsHealthy)
            {
                SetOverall("PRONTO AGORA", "Bluetooth está respondendo", Theme.Green);
                _summary.Text = adapter.Name + "  •  driver " + adapter.DriverVersion +
                    (_snapshot.KnownDeviceEntries > 0 ? "  •  dispositivos conhecidos: " + _snapshot.KnownDeviceEntries.ToString(CultureInfo.CurrentCulture) : "");
                _repairButton.Text = "Reiniciar com segurança";
                _repairButton.Width = 250;
                _settingsButton.Visible = true;
                _actionDetail.Text = "Se a conexão falhar, a cura reinicia somente o adaptador e os serviços Bluetooth. Seus pareamentos são preservados.";
            }
            else if (adapter != null && adapter.IsReady &&
                     (_snapshot.PowerState == BluetoothPowerState.Off || _snapshot.PowerState == BluetoothPowerState.Disabled))
            {
                SetOverall("DESLIGADO", "O adaptador está pronto, mas o Bluetooth está desligado", Theme.Amber);
                _summary.Text = adapter.Name + "  •  driver " + adapter.DriverVersion + "  •  pareamentos preservados";
                _repairButton.Text = "Abrir e ligar no Windows";
                _repairButton.Width = 285;
                _settingsButton.Visible = false;
                _actionDetail.Text = "O hardware está saudável. O Neck abre a chave oficial do Windows e não reinicia o driver nesse caso.";
            }
            else if (adapter == null)
            {
                SetOverall("NÃO DETECTADO", "O Windows não está enxergando o rádio Bluetooth", Color.FromArgb(190, 55, 55));
                _summary.Text = "A cura tentará redetectar o hardware e restaurar os serviços necessários.";
                _repairButton.Text = "Tentar corrigir agora";
                _repairButton.Width = 250;
                _settingsButton.Visible = true;
                _actionDetail.Text = "A cura redetecta o rádio e restaura os serviços sem apagar dispositivos pareados.";
            }
            else
            {
                SetOverall("PRECISA DE ATENÇÃO", "O Bluetooth foi encontrado, mas não está pronto", Theme.Amber);
                _summary.Text = adapter.Name + " — " + BluetoothDoctor.ExplainErrorCode(adapter.ErrorCode);
                _repairButton.Text = "Tentar corrigir agora";
                _repairButton.Width = 250;
                _settingsButton.Visible = true;
                _actionDetail.Text = "A cura reinicia somente o adaptador com falha e os serviços Bluetooth. Seus pareamentos são preservados.";
            }

            if (adapter == null)
                _adapterRow.SetState(BluetoothRowState.Missing, "Nenhum rádio físico foi encontrado pelo Windows.");
            else
                _adapterRow.SetState(adapter.IsReady && _snapshot.PowerState == BluetoothPowerState.On ? BluetoothRowState.Good : BluetoothRowState.Attention,
                    adapter.IsReady && _snapshot.PowerState == BluetoothPowerState.Off
                        ? adapter.Name + " — hardware pronto; chave Bluetooth desligada"
                        : adapter.Name + " — " + BluetoothDoctor.ExplainErrorCode(adapter.ErrorCode));

            if (adapter != null && adapter.DriverBacked && !string.IsNullOrWhiteSpace(adapter.DriverVersion))
            {
                string date = adapter.DriverDate.HasValue ? " • " + adapter.DriverDate.Value.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture) : "";
                _driverRow.SetState(BluetoothRowState.Good, adapter.Manufacturer + " • versão " + adapter.DriverVersion + date);
            }
            else
                _driverRow.SetState(BluetoothRowState.Attention, "O Windows não informou um driver Bluetooth válido.");

            _servicesRow.SetState(support != null && support.IsRunning ? BluetoothRowState.Good : BluetoothRowState.Attention,
                support == null ? "Serviço de suporte não encontrado." :
                support.IsRunning ? "Suporte a Bluetooth ativo e pronto para conexões." : "Suporte a Bluetooth está parado.");

            _activity.Text = activity;
            _updatesLink.Visible = adapter == null || !adapter.IsReady || !_snapshot.HasDriver;
        }

        private static bool IsOnlyTurnedOff(BluetoothSnapshot snapshot)
        {
            BluetoothAdapterInfo adapter = snapshot == null ? null : snapshot.PrimaryAdapter;
            BluetoothServiceInfo support = snapshot == null ? null : snapshot.SupportService;
            return adapter != null && adapter.IsReady && snapshot.HasDriver &&
                   support != null && support.IsRunning &&
                   (snapshot.PowerState == BluetoothPowerState.Off || snapshot.PowerState == BluetoothPowerState.Disabled);
        }

        private void OpenBluetoothSettings()
        {
            MainForm.OpenTarget("ms-settings:bluetooth");
            _activity.Text = "Ligue a chave na tela oficial do Windows e volte ao Neck; o diagnóstico será atualizado automaticamente.";
        }

        private void SetOverall(string badge, string headline, Color color)
        {
            _badge.Text = badge;
            _badge.ForeColor = color;
            _badge.BackColor = VisualEffects.Blend(color, Color.White, 0.89d);
            _headline.Text = headline;
        }

        private void SetBusy(bool busy, string activity)
        {
            _busy = busy;
            _repairButton.Enabled = !busy;
            _repairButton.AttentionPulse = !busy;
            _progress.Running = busy;
            if (!string.IsNullOrWhiteSpace(activity)) _activity.Text = activity;
        }

        private static void ConfigureButton(Button button, string text, Color color, int width)
        {
            button.Text = text;
            button.Size = new Size(width, 43);
            button.BackColor = color;
            button.ForeColor = Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.Margin = new Padding(0, 0, 10, 0);
            AnimatedButton animated = button as AnimatedButton;
            if (animated != null) animated.SetPalette(color);
        }
    }
}
