using System;
using System.Drawing;
using System.Windows.Forms;

namespace Neck
{
    internal sealed class BluetoothPowerResetForm : Form
    {
        private readonly CheckBox _confirmation = new CheckBox();
        private readonly AnimatedButton _shutdownButton = new AnimatedButton();
        private readonly Label _activity = new Label();
        private bool _shutdownRequested;

        public BluetoothPowerResetForm()
        {
            Text = "Reset elétrico guiado — Neck";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(790, 690);
            MinimumSize = new Size(740, 690);
            BackColor = Theme.Background;
            Font = Theme.Body;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            BuildInterface();
            Shown += delegate { VisualEffects.FadeIn(this); };
        }

        private void BuildInterface()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 112, BackColor = Theme.Card };
            header.Controls.Add(new FlowMark { Location = new Point(28, 29), Size = new Size(48, 48) });
            header.Controls.Add(new Label
            {
                Text = "Reset elétrico guiado",
                AutoSize = true,
                Font = Theme.Title,
                ForeColor = Theme.Ink,
                Location = new Point(100, 20)
            });
            header.Controls.Add(new Label
            {
                Text = "O Neck prepara o Windows; você conclui a descarga com o computador desligado.",
                AutoSize = true,
                Font = Theme.Body,
                ForeColor = Theme.Muted,
                Location = new Point(104, 70)
            });

            TableLayoutPanel body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 20, 24, 20),
                ColumnCount = 1,
                RowCount = 6,
                BackColor = Theme.Background
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 79));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 79));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 79));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 98));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            body.Controls.Add(new Label
            {
                Text = "Isso não apaga arquivos nem redefine o Windows. A etapa física não pode ser feita por software.",
                Dock = DockStyle.Fill,
                Font = Theme.Body,
                ForeColor = Theme.Text,
                Padding = new Padding(4, 4, 4, 0)
            }, 0, 0);
            body.Controls.Add(CreateStep("1", "Prepare o computador", "Salve seu trabalho e retire pendrives, HDs externos e outros acessórios USB."), 0, 1);
            body.Controls.Add(CreateStep("2", "Desligamento completo", "O Neck solicitará ao Windows um desligamento completo, sem Inicialização Rápida e sem forçar programas."), 0, 2);
            body.Controls.Add(CreateStep("3", "Descarregue a energia residual", "Quando luzes e ventoinhas apagarem, retire o carregador e segure apenas o botão de ligar por 20 segundos. Depois reconecte e ligue."), 0, 3);

            RoundedPanel notice = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 6, 0, 8),
                Padding = new Padding(18, 12, 18, 10),
                BackColor = Color.FromArgb(255, 248, 235),
                OutlineColor = Color.FromArgb(238, 205, 157),
                CornerRadius = 15
            };
            Label noticeTitle = new Label
            {
                Text = "Espere o computador desligar por inteiro",
                Dock = DockStyle.Top,
                Height = 25,
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                ForeColor = Theme.Ink
            };
            Label noticeText = new Label
            {
                Text = "Tela preta não basta: aguarde as luzes e ventoinhas pararem antes de retirar o carregador.",
                Dock = DockStyle.Fill,
                Font = Theme.Small,
                ForeColor = Theme.Muted
            };
            notice.Controls.Add(noticeText);
            notice.Controls.Add(noticeTitle);
            body.Controls.Add(notice, 0, 4);

            Panel action = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background, Padding = new Padding(4, 4, 4, 0) };
            _confirmation.Text = "Salvei meu trabalho e posso desligar o computador agora.";
            _confirmation.AutoSize = true;
            _confirmation.Font = Theme.Body;
            _confirmation.ForeColor = Theme.Text;
            _confirmation.Location = new Point(0, 4);
            _confirmation.CheckedChanged += delegate { _shutdownButton.Enabled = _confirmation.Checked && !_shutdownRequested; };

            _shutdownButton.Text = "Desligar completamente";
            _shutdownButton.Size = new Size(245, 44);
            _shutdownButton.Location = new Point(0, 43);
            _shutdownButton.BackColor = Theme.Lime;
            _shutdownButton.ForeColor = Theme.Ink;
            _shutdownButton.FlatStyle = FlatStyle.Flat;
            _shutdownButton.FlatAppearance.BorderSize = 0;
            _shutdownButton.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
            _shutdownButton.Cursor = Cursors.Hand;
            _shutdownButton.Enabled = false;
            _shutdownButton.SetPalette(Theme.Lime);
            _shutdownButton.Click += delegate { BeginShutdown(); };

            Button cancel = new Button
            {
                Text = "Agora não",
                Size = new Size(140, 44),
                Location = new Point(257, 43),
                BackColor = Theme.Card,
                ForeColor = Theme.Ink,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            cancel.FlatAppearance.BorderColor = Theme.Border;
            cancel.Click += delegate { Close(); };

            _activity.AutoSize = false;
            _activity.Location = new Point(412, 46);
            _activity.Size = new Size(300, 42);
            _activity.Font = Theme.Small;
            _activity.ForeColor = Theme.Muted;
            _activity.Text = "O desligamento só começa após sua confirmação.";
            action.Resize += delegate { _activity.Width = Math.Max(120, action.ClientSize.Width - _activity.Left); };
            action.Controls.Add(_activity);
            action.Controls.Add(cancel);
            action.Controls.Add(_shutdownButton);
            action.Controls.Add(_confirmation);
            body.Controls.Add(action, 0, 5);

            Controls.Add(body);
            Controls.Add(header);
        }

        private static Control CreateStep(string number, string title, string detail)
        {
            RoundedPanel card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 0, 4),
                BackColor = Theme.Card,
                OutlineColor = Theme.Border,
                CornerRadius = 15
            };
            Label badge = new Label
            {
                Text = number,
                AutoSize = false,
                Size = new Size(38, 38),
                Location = new Point(16, 16),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold),
                ForeColor = Theme.Ink,
                BackColor = Theme.Lime
            };
            Label heading = new Label
            {
                Text = title,
                AutoSize = false,
                Location = new Point(70, 11),
                Size = new Size(630, 25),
                Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold),
                ForeColor = Theme.Ink
            };
            Label description = new Label
            {
                Text = detail,
                AutoSize = false,
                Location = new Point(70, 36),
                Size = new Size(630, 30),
                Font = Theme.Small,
                ForeColor = Theme.Muted
            };
            card.Resize += delegate
            {
                heading.Width = Math.Max(100, card.ClientSize.Width - 88);
                description.Width = Math.Max(100, card.ClientSize.Width - 88);
            };
            card.Controls.Add(description);
            card.Controls.Add(heading);
            card.Controls.Add(badge);
            return card;
        }

        private void BeginShutdown()
        {
            if (_shutdownRequested || !_confirmation.Checked) return;
            DialogResult confirmation = MessageBox.Show(
                "O computador será desligado por completo agora. O Neck não forçará o fechamento dos programas. Se algum aplicativo impedir o desligamento, salve e feche-o antes de tentar novamente.\n\n" +
                "Quando todas as luzes e ventoinhas apagarem: retire o carregador, segure o botão de ligar por 20 segundos, reconecte e ligue o computador.\n\nContinuar?",
                "Iniciar reset elétrico guiado", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (confirmation != DialogResult.Yes) return;

            _shutdownRequested = true;
            _shutdownButton.Enabled = false;
            _confirmation.Enabled = false;
            _activity.Text = "Restaurando ajustes temporários e solicitando o desligamento...";
            Application.DoEvents();

            ApplicationSafety.RestoreActiveChanges("Preparação para reset elétrico do Bluetooth");
            SupportDiagnostics.RecordEvent("Bluetooth", "Reset elétrico guiado confirmado; desligamento completo solicitado.");
            ProcessResult result = BluetoothPowerResetCoordinator.StartFullShutdown();
            if (result.ExitCode == 0)
            {
                _activity.Text = "Desligamento solicitado. Aguarde o computador apagar por inteiro.";
                return;
            }

            _shutdownRequested = false;
            _confirmation.Enabled = true;
            _shutdownButton.Enabled = _confirmation.Checked;
            _activity.Text = "O Windows não aceitou o desligamento. Nenhuma etapa física foi iniciada.";
            MessageBox.Show("Não foi possível iniciar o desligamento completo.\n\n" + (result.Output ?? "").Trim(),
                "Reset elétrico não iniciado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
