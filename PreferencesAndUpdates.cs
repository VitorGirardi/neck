using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Neck
{
    internal enum UpdateCheckState
    {
        Current,
        Available,
        RepositoryUnavailable,
        Failed
    }

    internal sealed class UpdateCheckResult
    {
        public UpdateCheckState State;
        public Version CurrentVersion;
        public Version LatestVersion;
        public string Message;
        public string ReleaseUrl;
    }

    internal static class UpdateChecker
    {
        internal const string RepositoryUrl = "https://github.com/VitorGirardi/neck";
        internal const string ReleasesUrl = RepositoryUrl + "/releases";
        private const string LatestReleaseApi = "https://api.github.com/repos/VitorGirardi/neck/releases/latest";

        public static UpdateCheckResult Check()
        {
            Version current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);
            try
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(LatestReleaseApi);
                request.Method = "GET";
                request.UserAgent = "Neck/" + current.ToString(3) + " (Windows; update-check)";
                request.Accept = "application/vnd.github+json";
                request.Timeout = 10000;
                request.ReadWriteTimeout = 10000;

                string json;
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    json = reader.ReadToEnd();
                }

                string tag = ReadJsonString(json, "tag_name");
                string releaseUrl = ReadJsonString(json, "html_url");
                Version latest;
                if (string.IsNullOrWhiteSpace(tag) || !Version.TryParse(tag.Trim().TrimStart('v', 'V'), out latest))
                    throw new InvalidDataException("A versão publicada não pôde ser interpretada.");

                bool available = latest > current;
                return new UpdateCheckResult
                {
                    State = available ? UpdateCheckState.Available : UpdateCheckState.Current,
                    CurrentVersion = current,
                    LatestVersion = latest,
                    ReleaseUrl = string.IsNullOrWhiteSpace(releaseUrl) ? ReleasesUrl : releaseUrl,
                    Message = available
                        ? "A versão " + latest.ToString(3) + " está disponível. O Neck abrirá apenas a página oficial para você revisar o download."
                        : "Você já está usando a versão mais recente do Neck."
                };
            }
            catch (WebException ex)
            {
                HttpWebResponse response = ex.Response as HttpWebResponse;
                if (response != null && response.StatusCode == HttpStatusCode.NotFound)
                {
                    return new UpdateCheckResult
                    {
                        State = UpdateCheckState.RepositoryUnavailable,
                        CurrentVersion = current,
                        ReleaseUrl = ReleasesUrl,
                        Message = "O GitHub não permite consultar versões enquanto o repositório estiver privado. Nenhuma credencial é armazenada pelo Neck."
                    };
                }

                return Failure(current, "Não foi possível consultar o GitHub agora. Verifique a internet e tente novamente.");
            }
            catch (Exception ex)
            {
                return Failure(current, "Não foi possível verificar atualizações: " + ex.Message);
            }
        }

        private static UpdateCheckResult Failure(Version current, string message)
        {
            return new UpdateCheckResult
            {
                State = UpdateCheckState.Failed,
                CurrentVersion = current,
                ReleaseUrl = ReleasesUrl,
                Message = message
            };
        }

        private static string ReadJsonString(string json, string key)
        {
            Match match = Regex.Match(json ?? "", "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*\\\"(?<value>(?:\\\\.|[^\\\"])*)\\\"");
            if (!match.Success) return null;
            return Regex.Unescape(match.Groups["value"].Value.Replace("\\/", "/"));
        }
    }

    internal sealed class PreferencesForm : Form
    {
        private readonly GuardSettings _settings;
        private readonly bool _firstRun;
        private readonly CheckBox _startup = new CheckBox();
        private readonly CheckBox _tray = new CheckBox();
        private readonly CheckBox _notifications = new CheckBox();
        private readonly CheckBox _fullscreen = new CheckBox();
        private readonly Label _updateStatus = new Label();
        private readonly Button _checkUpdates = new Button();
        private readonly Button _openRelease = new Button();
        private UpdateCheckResult _lastUpdate;

        public PreferencesForm(GuardSettings settings, bool firstRun)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            _settings = settings;
            _firstRun = firstRun;
            Text = firstRun ? "Primeiros passos — Neck" : "Preferências — Neck";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(720, 700);
            MinimumSize = new Size(680, 670);
            MaximizeBox = false;
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
                Text = _firstRun ? "Bem-vindo ao Neck" : "Preferências",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 23f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(30, 20)
            });
            header.Controls.Add(new Label
            {
                Text = _firstRun ? "Escolha como o Neck deve cuidar do computador. Você pode mudar tudo depois." : "Controle o monitoramento, os alertas e a inicialização.",
                AutoSize = false,
                Size = new Size(640, 35),
                Font = Theme.Body,
                ForeColor = Color.FromArgb(190, 203, 222),
                Location = new Point(33, 67)
            });

            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 76, BackColor = Color.White, Padding = new Padding(24, 15, 24, 14) };
            Button save = MakeButton(_firstRun ? "Salvar e começar" : "Salvar alterações", Theme.Blue, 174);
            save.Dock = DockStyle.Right;
            save.Click += delegate { SaveAndClose(); };
            footer.Controls.Add(save);
            if (!_firstRun)
            {
                Button cancel = MakeButton("Cancelar", Theme.NavySoft, 110);
                cancel.Dock = DockStyle.Right;
                cancel.Margin = new Padding(0, 0, 10, 0);
                cancel.DialogResult = DialogResult.Cancel;
                footer.Controls.Add(cancel);
                CancelButton = cancel;
            }
            AcceptButton = save;

            FlowLayoutPanel content = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(26, 20, 26, 20),
                BackColor = Theme.Background
            };

            content.Controls.Add(BuildBehaviorCard());
            content.Controls.Add(BuildUpdateCard());
            Controls.Add(content);
            Controls.Add(footer);
            Controls.Add(header);
        }

        private Control BuildBehaviorCard()
        {
            RoundedPanel card = new RoundedPanel
            {
                Size = new Size(642, 242),
                BackColor = Color.White,
                OutlineColor = Theme.Border,
                CornerRadius = 16,
                Margin = new Padding(0, 0, 0, 14)
            };
            card.Controls.Add(new Label { Text = "Como o Neck funciona", AutoSize = true, Font = Theme.Heading, ForeColor = Theme.Text, Location = new Point(22, 18) });

            ConfigureChoice(_startup, "Iniciar com o Windows", "Abre oculto, sem pedir administrador, para o Guard acompanhar o computador.", 58);
            ConfigureChoice(_tray, "Continuar na bandeja ao fechar", "Mantém o monitoramento ativo quando a janela principal é fechada.", 104);
            ConfigureChoice(_notifications, "Avisar sobre sobrecarga persistente", "Exibe alerta somente após vários sinais consecutivos — não por um pico isolado.", 150);
            ConfigureChoice(_fullscreen, "Silenciar alertas em tela cheia", "Evita interromper apresentações, vídeos e jogos.", 196);

            _startup.Checked = StartupManager.IsEnabled();
            _tray.Checked = _settings.ContinueInTray || _startup.Checked;
            _notifications.Checked = _settings.Notifications;
            _fullscreen.Checked = _settings.SilenceFullscreen;
            _startup.CheckedChanged += delegate { if (_startup.Checked) _tray.Checked = true; };

            card.Controls.Add(_startup);
            card.Controls.Add(_tray);
            card.Controls.Add(_notifications);
            card.Controls.Add(_fullscreen);
            return card;
        }

        private Control BuildUpdateCard()
        {
            RoundedPanel card = new RoundedPanel
            {
                Size = new Size(642, 154),
                BackColor = Color.White,
                OutlineColor = Theme.Border,
                CornerRadius = 16,
                Margin = new Padding(0)
            };
            Version version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);
            card.Controls.Add(new Label { Text = "Atualizações", AutoSize = true, Font = Theme.Heading, ForeColor = Theme.Text, Location = new Point(22, 17) });
            card.Controls.Add(new Label { Text = "Versão instalada: " + version.ToString(3), AutoSize = true, Font = Theme.Small, ForeColor = Theme.Muted, Location = new Point(24, 48) });

            _updateStatus.Text = "A verificação é manual e consulta somente o repositório oficial no GitHub.";
            _updateStatus.AutoSize = false;
            _updateStatus.Size = new Size(590, 36);
            _updateStatus.Font = Theme.Small;
            _updateStatus.ForeColor = Theme.Muted;
            _updateStatus.Location = new Point(24, 73);

            ConfigureSmallButton(_checkUpdates, "Verificar agora", Theme.Green, 132);
            _checkUpdates.Location = new Point(24, 111);
            _checkUpdates.Click += async delegate { await CheckUpdatesAsync(); };
            ConfigureSmallButton(_openRelease, "Abrir GitHub", Theme.NavySoft, 112);
            _openRelease.Location = new Point(166, 111);
            _openRelease.Click += delegate
            {
                MainForm.OpenTarget(_lastUpdate == null || string.IsNullOrWhiteSpace(_lastUpdate.ReleaseUrl) ? UpdateChecker.ReleasesUrl : _lastUpdate.ReleaseUrl);
            };

            card.Controls.Add(_updateStatus);
            card.Controls.Add(_checkUpdates);
            card.Controls.Add(_openRelease);
            return card;
        }

        private async Task CheckUpdatesAsync()
        {
            _checkUpdates.Enabled = false;
            _checkUpdates.Text = "Consultando...";
            _updateStatus.Text = "Consultando a versão mais recente no GitHub...";
            try
            {
                _lastUpdate = await Task.Run(delegate { return UpdateChecker.Check(); });
                if (IsDisposed) return;
                _updateStatus.Text = _lastUpdate.Message;
                _updateStatus.ForeColor = _lastUpdate.State == UpdateCheckState.Available ? Theme.Green :
                    _lastUpdate.State == UpdateCheckState.Current ? Theme.Blue : Theme.Amber;
                _openRelease.Text = _lastUpdate.State == UpdateCheckState.Available ? "Ver versão" : "Abrir GitHub";
            }
            finally
            {
                if (!IsDisposed)
                {
                    _checkUpdates.Enabled = true;
                    _checkUpdates.Text = "Verificar agora";
                }
            }
        }

        private void SaveAndClose()
        {
            try
            {
                StartupManager.SetEnabled(_startup.Checked);
                _settings.ContinueInTray = _tray.Checked || _startup.Checked;
                _settings.Notifications = _notifications.Checked;
                _settings.SilenceFullscreen = _fullscreen.Checked;
                _settings.OnboardingCompleted = true;
                _settings.Save();
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível salvar todas as preferências.\n\n" + ex.Message, "Neck", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static void ConfigureChoice(CheckBox checkBox, string title, string description, int top)
        {
            checkBox.Text = title + Environment.NewLine + description;
            checkBox.AutoSize = false;
            checkBox.Size = new Size(590, 44);
            checkBox.Location = new Point(23, top);
            checkBox.Font = Theme.Small;
            checkBox.ForeColor = Theme.Text;
            checkBox.Cursor = Cursors.Hand;
            checkBox.TextAlign = ContentAlignment.TopLeft;
        }

        private static Button MakeButton(string text, Color color, int width)
        {
            Button button = new Button
            {
                Text = text,
                Width = width,
                Height = 45,
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private static void ConfigureSmallButton(Button button, string text, Color color, int width)
        {
            button.Text = text;
            button.Size = new Size(width, 32);
            button.BackColor = color;
            button.ForeColor = Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
        }
    }
}
