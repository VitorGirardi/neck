using System;
using System.Drawing;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Neck
{
    internal sealed class HardwareDetailsForm : Form
    {
        private readonly ListView _components = new ListView();
        private readonly ListView _temperatures = new ListView();
        private readonly Label _captured = new Label();
        private readonly Label _sensorNote = new Label();
        private readonly Button _refresh = new AnimatedButton();
        private HardwareSnapshot _snapshot;
        private bool _refreshing;

        public HardwareDetailsForm(HardwareSnapshot snapshot)
        {
            _snapshot = snapshot ?? new HardwareSnapshot();
            Text = "Hardware — Neck";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(920, 720);
            MinimumSize = new Size(820, 640);
            BackColor = Theme.Background;
            Font = Theme.Body;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            BuildInterface();
            Populate();
            Shown += delegate { VisualEffects.FadeIn(this); };
        }

        private void BuildInterface()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 104, BackColor = Theme.Card };
            header.Controls.Add(new Label
            {
                Text = "Hardware deste computador",
                AutoSize = true,
                Font = new Font("Segoe UI Variable Display", 23f, FontStyle.Bold),
                ForeColor = Theme.Ink,
                Location = new Point(30, 19)
            });
            header.Controls.Add(new Label
            {
                Text = "Especificações lidas localmente. Nenhuma informação é enviada pela internet.",
                AutoSize = true,
                Font = Theme.Body,
                ForeColor = Theme.Muted,
                Location = new Point(33, 68)
            });

            TableLayoutPanel body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background,
                Padding = new Padding(24, 18, 24, 14),
                ColumnCount = 1,
                RowCount = 2
            };
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
            body.Controls.Add(BuildComponentsCard(), 0, 0);
            body.Controls.Add(BuildTemperatureCard(), 0, 1);

            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 76, BackColor = Color.White };
            _captured.AutoSize = false;
            _captured.Size = new Size(480, 40);
            _captured.Location = new Point(28, 19);
            _captured.Font = Theme.Small;
            _captured.ForeColor = Theme.Muted;
            _captured.TextAlign = ContentAlignment.MiddleLeft;
            ConfigureButton(_refresh, "Atualizar leitura", Theme.NavySoft, 165);
            _refresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _refresh.Location = new Point(560, 17);
            _refresh.Click += async delegate { await RefreshAsync(); };
            Button close = new AnimatedButton();
            ConfigureButton(close, "Voltar", Theme.Blue, 120);
            close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            close.Location = new Point(735, 17);
            close.Click += delegate { Close(); };
            footer.Resize += delegate
            {
                close.Left = footer.ClientSize.Width - close.Width - 24;
                _refresh.Left = close.Left - _refresh.Width - 10;
                _captured.Width = Math.Max(250, _refresh.Left - 44);
            };
            footer.Controls.Add(_captured);
            footer.Controls.Add(_refresh);
            footer.Controls.Add(close);

            Controls.Add(body);
            Controls.Add(footer);
            Controls.Add(header);
            AcceptButton = close;
            CancelButton = close;
        }

        private Control BuildComponentsCard()
        {
            RoundedPanel card = CreateCard();
            card.Margin = new Padding(0, 0, 0, 8);
            Label title = new Label
            {
                Text = "Componentes e especificações",
                Dock = DockStyle.Top,
                Height = 42,
                Font = new Font("Segoe UI Semibold", 15f, FontStyle.Bold),
                ForeColor = Theme.Text
            };
            ConfigureList(_components);
            _components.Columns.Add("Componente", 120);
            _components.Columns.Add("Modelo", 300);
            _components.Columns.Add("Especificações", 390);
            _components.Resize += delegate { ResizeColumns(_components, 0.15f, 0.37f); };
            card.Controls.Add(_components);
            card.Controls.Add(title);
            return card;
        }

        private Control BuildTemperatureCard()
        {
            RoundedPanel card = CreateCard();
            card.Margin = new Padding(0, 8, 0, 0);
            Label title = new Label
            {
                Text = "Temperaturas disponíveis",
                Dock = DockStyle.Top,
                Height = 36,
                Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold),
                ForeColor = Theme.Text
            };
            _sensorNote.Dock = DockStyle.Bottom;
            _sensorNote.Height = 42;
            _sensorNote.Font = Theme.Small;
            _sensorNote.ForeColor = Theme.Muted;
            _sensorNote.Text = "A ausência de leitura significa que o fabricante não expôs o sensor ao Windows; não indica defeito.";
            ConfigureList(_temperatures);
            _temperatures.Columns.Add("Sensor", 290);
            _temperatures.Columns.Add("Temperatura", 130);
            _temperatures.Columns.Add("Fonte da leitura", 390);
            _temperatures.Resize += delegate { ResizeColumns(_temperatures, 0.36f, 0.16f); };
            card.Controls.Add(_temperatures);
            card.Controls.Add(_sensorNote);
            card.Controls.Add(title);
            return card;
        }

        private static RoundedPanel CreateCard()
        {
            return new RoundedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                OutlineColor = Theme.Border,
                CornerRadius = 18,
                Padding = new Padding(20, 16, 20, 14)
            };
        }

        private static void ConfigureList(ListView list)
        {
            list.Dock = DockStyle.Fill;
            list.View = View.Details;
            list.FullRowSelect = true;
            list.MultiSelect = false;
            list.HideSelection = false;
            list.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            list.BorderStyle = BorderStyle.None;
            list.BackColor = Color.White;
            list.ForeColor = Theme.Text;
            list.Font = new Font("Segoe UI", 10f);
        }

        private static void ResizeColumns(ListView list, float firstRatio, float secondRatio)
        {
            if (list.Columns.Count != 3 || list.ClientSize.Width < 120) return;
            int available = Math.Max(120, list.ClientSize.Width - 4);
            list.Columns[0].Width = Math.Max(90, (int)(available * firstRatio));
            list.Columns[1].Width = Math.Max(110, (int)(available * secondRatio));
            list.Columns[2].Width = Math.Max(160, available - list.Columns[0].Width - list.Columns[1].Width);
        }

        private void Populate()
        {
            if (IsDisposed) return;
            _components.BeginUpdate();
            _components.Items.Clear();
            foreach (HardwareComponent component in _snapshot.Components)
            {
                ListViewItem item = new ListViewItem(component.Category ?? string.Empty);
                item.SubItems.Add(component.Name ?? string.Empty);
                item.SubItems.Add(component.Details ?? string.Empty);
                _components.Items.Add(item);
            }
            if (_components.Items.Count == 0)
            {
                ListViewItem unavailable = new ListViewItem("Windows");
                unavailable.SubItems.Add("Inventário não disponibilizado");
                unavailable.SubItems.Add("Tente atualizar a leitura.");
                _components.Items.Add(unavailable);
            }
            _components.EndUpdate();

            _temperatures.BeginUpdate();
            _temperatures.Items.Clear();
            foreach (TemperatureReading reading in _snapshot.Temperatures)
            {
                ListViewItem item = new ListViewItem(reading.Name ?? "Sensor");
                item.SubItems.Add(reading.Celsius.ToString("0.0", CultureInfo.CurrentCulture) + " °C");
                item.SubItems.Add(reading.Source ?? "Fonte local");
                item.ForeColor = reading.Celsius >= 90d ? Color.Firebrick : reading.Celsius >= 75d ? Theme.Amber : Theme.Green;
                _temperatures.Items.Add(item);
            }
            if (_temperatures.Items.Count == 0)
            {
                ListViewItem unavailable = new ListViewItem("Sensor não disponibilizado");
                unavailable.SubItems.Add("—");
                unavailable.SubItems.Add("O Windows ou o fabricante não expôs uma leitura compatível.");
                unavailable.ForeColor = Theme.Muted;
                _temperatures.Items.Add(unavailable);
                _sensorNote.Text = "O Neck não estima temperaturas. Para exibir CPU/GPU, o sensor precisa ser publicado pelo firmware ou por um monitor compatível já instalado.";
            }
            else _sensorNote.Text = "Leituras locais informadas pela fonte indicada. Sensores ACPI podem representar uma zona do sistema, não necessariamente CPU ou GPU.";
            _temperatures.EndUpdate();
            _captured.Text = "Leitura atualizada em " + _snapshot.CapturedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
        }

        private async Task RefreshAsync()
        {
            if (_refreshing || IsDisposed) return;
            _refreshing = true;
            _refresh.Enabled = false;
            _refresh.Text = "Atualizando...";
            Cursor = Cursors.WaitCursor;
            try
            {
                HardwareSnapshot fresh = await Task.Run(delegate { return HardwareInfoProvider.Read(); });
                if (IsDisposed) return;
                _snapshot = fresh;
                Populate();
            }
            catch
            {
                if (!IsDisposed) _captured.Text = "Não foi possível atualizar a leitura agora.";
            }
            finally
            {
                if (!IsDisposed)
                {
                    _refreshing = false;
                    _refresh.Enabled = true;
                    _refresh.Text = "Atualizar leitura";
                    Cursor = Cursors.Default;
                }
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
            button.Cursor = Cursors.Hand;
        }
    }
}
