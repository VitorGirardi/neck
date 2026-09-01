using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Neck")]
[assembly: System.Reflection.AssemblyDescription("Diagnóstico inteligente e manutenção segura para Windows")]
[assembly: System.Reflection.AssemblyCompany("Neck")]
[assembly: System.Reflection.AssemblyProduct("Neck")]
[assembly: System.Reflection.AssemblyVersion("1.18.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.18.0.0")]

namespace Neck
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (ElevatedOperations.IsElevatedInvocation(args))
            {
                Environment.ExitCode = ElevatedOperations.ExecuteElevatedInvocation(args);
                return;
            }

            if (args != null && args.Any(item => string.Equals(item, "--remove-startup", StringComparison.OrdinalIgnoreCase)))
            {
                try { StartupManager.SetEnabled(false); }
                catch { Environment.ExitCode = 1; }
                return;
            }

            ApplicationSafety.Configure();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool startHidden = args != null && args.Any(item => string.Equals(item, "--background", StringComparison.OrdinalIgnoreCase));
            bool firstInstance;
            using (System.Threading.Mutex singleInstance = new System.Threading.Mutex(true, @"Local\Neck.SingleInstance", out firstInstance))
            {
                if (!firstInstance)
                {
                    IntPtr existing = NativeMethods.FindWindow(null, "Neck");
                    if (existing != IntPtr.Zero)
                    {
                        NativeMethods.ShowWindow(existing, NativeMethods.SW_RESTORE);
                        NativeMethods.SetForegroundWindow(existing);
                    }
                    return;
                }
                RecoveryStartupResult recovery = RecoveryManager.BeginSession();
                SupportDiagnostics.RecordEvent("Inicialização", "Neck iniciado. " + recovery.Summary);
                try
                {
                    Application.Run(new MainForm(startHidden, false, recovery));
                }
                finally
                {
                    ApplicationSafety.RestoreActiveChanges("Encerramento seguro do Neck");
                    RecoveryManager.CompleteSession();
                    SupportDiagnostics.RecordEvent("Encerramento", "Neck encerrado com restauração segura.");
                }
                GC.KeepAlive(singleInstance);
            }
        }
    }

    internal static class Theme
    {
        public static readonly Color Background = Color.FromArgb(244, 243, 237);
        public static readonly Color Card = Color.FromArgb(255, 255, 252);
        public static readonly Color Ink = Color.FromArgb(31, 41, 37);
        public static readonly Color Navy = Ink;
        public static readonly Color NavySoft = Color.FromArgb(70, 86, 78);
        public static readonly Color Blue = Color.FromArgb(47, 107, 87);
        public static readonly Color BlueSoft = Color.FromArgb(232, 241, 236);
        public static readonly Color Lime = Color.FromArgb(182, 239, 103);
        public static readonly Color Cyan = Color.FromArgb(89, 132, 57);
        public static readonly Color FlowSoft = Color.FromArgb(242, 248, 232);
        public static readonly Color Green = Color.FromArgb(47, 125, 89);
        public static readonly Color GreenSoft = Color.FromArgb(235, 245, 237);
        public static readonly Color Amber = Color.FromArgb(211, 103, 55);
        public static readonly Color Coral = Color.FromArgb(238, 113, 77);
        public static readonly Color Text = Ink;
        public static readonly Color Muted = Color.FromArgb(102, 116, 108);
        public static readonly Color Border = Color.FromArgb(220, 224, 216);
        public static readonly Color Hairline = Color.FromArgb(230, 232, 226);
        public static readonly Font Title = new Font("Segoe UI Variable Display", 25f, FontStyle.Bold);
        public static readonly Font Heading = new Font("Segoe UI Semibold", 15f, FontStyle.Bold);
        public static readonly Font Brand = new Font("Segoe UI Variable Display", 24f, FontStyle.Bold);
        public static readonly Font Body = new Font("Segoe UI", 10f, FontStyle.Regular);
        public static readonly Font Small = new Font("Segoe UI", 9f, FontStyle.Regular);
    }

    internal sealed class FlowMark : Control
    {
        public FlowMark()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            Size = new Size(50, 50);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float scaleX = Width / 256f;
            float scaleY = Height / 256f;
            e.Graphics.ScaleTransform(scaleX, scaleY);
            using (GraphicsPath left = new GraphicsPath())
            using (GraphicsPath right = new GraphicsPath())
            using (SolidBrush ink = new SolidBrush(Theme.Ink))
            using (SolidBrush lime = new SolidBrush(Theme.Lime))
            using (Pen flow = new Pen(Theme.Lime, 18f))
            {
                left.StartFigure();
                left.AddLine(38, 24, 88, 24);
                left.AddBezier(88, 24, 88, 72, 97, 104, 122, 128);
                left.AddBezier(122, 128, 97, 152, 88, 184, 88, 232);
                left.AddLine(88, 232, 38, 232);
                left.AddBezier(38, 232, 38, 180, 51, 148, 76, 128);
                left.AddBezier(76, 128, 51, 108, 38, 76, 38, 24);
                left.CloseFigure();
                right.StartFigure();
                right.AddLine(218, 24, 168, 24);
                right.AddBezier(168, 24, 168, 72, 159, 104, 134, 128);
                right.AddBezier(134, 128, 159, 152, 168, 184, 168, 232);
                right.AddLine(168, 232, 218, 232);
                right.AddBezier(218, 232, 218, 180, 205, 148, 180, 128);
                right.AddBezier(180, 128, 205, 108, 218, 76, 218, 24);
                right.CloseFigure();
                e.Graphics.FillPath(ink, left);
                e.Graphics.FillPath(ink, right);
                flow.StartCap = LineCap.Round;
                flow.EndCap = LineCap.Round;
                e.Graphics.DrawLine(flow, 58, 128, 184, 128);
                e.Graphics.FillPolygon(lime, new[] { new PointF(176, 105), new PointF(206, 128), new PointF(176, 151) });
            }
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
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

    internal sealed class FlowIndicator : Control
    {
        private readonly Timer _timer = new Timer();
        private HealthLevel _level = HealthLevel.Stable;
        private float _phase;
        private int _frames;

        public FlowIndicator()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            Height = 34;
            BackColor = Color.Transparent;
            _timer.Interval = 40;
            _timer.Tick += delegate
            {
                _phase += 0.045f;
                _frames++;
                Invalidate();
                if (_frames >= 36) _timer.Stop();
            };
        }

        public void SetLevel(HealthLevel level)
        {
            _level = level;
            _frames = 0;
            _phase = 0f;
            if (!VisualEffects.ReduceMotion && Visible) _timer.Start();
            else _timer.Stop();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color state = _level == HealthLevel.Critical ? Theme.Coral :
                          _level == HealthLevel.Warning ? Theme.Amber : Theme.Green;
            float middle = Width / 2f;
            float centerY = Height / 2f;
            using (Pen channel = new Pen(VisualEffects.Blend(Theme.Ink, Theme.FlowSoft, 0.70d), 2.2f))
            using (Pen flow = new Pen(Theme.Lime, 4.2f))
            using (GraphicsPath upper = new GraphicsPath())
            using (GraphicsPath lower = new GraphicsPath())
            using (SolidBrush arrow = new SolidBrush(Theme.Lime))
            {
                channel.StartCap = channel.EndCap = LineCap.Round;
                upper.AddBezier(8, 7, middle - 38, 7, middle - 25, centerY - 7, middle, centerY - 7);
                upper.AddBezier(middle, centerY - 7, middle + 25, centerY - 7, middle + 38, 7, Width - 8, 7);
                lower.AddBezier(8, Height - 7, middle - 38, Height - 7, middle - 25, centerY + 7, middle, centerY + 7);
                lower.AddBezier(middle, centerY + 7, middle + 25, centerY + 7, middle + 38, Height - 7, Width - 8, Height - 7);
                e.Graphics.DrawPath(channel, upper);
                e.Graphics.DrawPath(channel, lower);
                flow.StartCap = LineCap.Round;
                flow.EndCap = LineCap.Round;
                e.Graphics.DrawLine(flow, 12, centerY, Width - 27, centerY);
                e.Graphics.FillPolygon(arrow, new[]
                {
                    new PointF(Width - 34, centerY - 8),
                    new PointF(Width - 12, centerY),
                    new PointF(Width - 34, centerY + 8)
                });
            }
            float progress = VisualEffects.ReduceMotion ? 0.74f : _phase;
            float x = 14 + (Width - 48) * Math.Min(1f, progress);
            using (SolidBrush dot = new SolidBrush(state)) e.Graphics.FillEllipse(dot, x - 4, centerY - 4, 8, 8);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer.Dispose();
            base.Dispose(disposing);
        }
    }

    internal static class VisualEffects
    {
        public static bool ReduceMotion { get; set; }

        public static Color Blend(Color from, Color to, double amount)
        {
            amount = Math.Max(0d, Math.Min(1d, amount));
            return Color.FromArgb(
                (int)(from.R + (to.R - from.R) * amount),
                (int)(from.G + (to.G - from.G) * amount),
                (int)(from.B + (to.B - from.B) * amount));
        }

        public static void FadeIn(Form form)
        {
            if (form == null || form.IsDisposed || ReduceMotion)
            {
                if (form != null && !form.IsDisposed) form.Opacity = 1d;
                return;
            }
            form.Opacity = 0.90d;
            Timer timer = new Timer { Interval = 16 };
            timer.Tick += delegate
            {
                if (form.IsDisposed || form.Disposing)
                {
                    timer.Stop();
                    timer.Dispose();
                    return;
                }
                form.Opacity = Math.Min(1d, form.Opacity + 0.035d);
                if (form.Opacity >= 1d)
                {
                    timer.Stop();
                    timer.Dispose();
                }
            };
            timer.Start();
        }
    }

    internal sealed class AnimatedButton : Button
    {
        private readonly Timer _timer = new Timer();
        private Color _baseColor = Theme.Blue;
        private Color _hoverColor = Color.FromArgb(29, 78, 216);
        private Color _targetColor = Theme.Blue;
        private bool _hovered;
        private bool _pressed;
        private bool _attentionPulse;
        private double _pulsePhase;

        public bool AttentionPulse
        {
            get { return _attentionPulse; }
            set
            {
                _attentionPulse = value;
                if (value && !VisualEffects.ReduceMotion) _timer.Start();
                else if (!_hovered && !_pressed)
                {
                    _targetColor = _baseColor;
                    BackColor = _baseColor;
                    _timer.Stop();
                }
            }
        }

        public AnimatedButton()
        {
            DoubleBuffered = true;
            _timer.Interval = 30;
            _timer.Tick += Animate;
        }

        public override void NotifyDefault(bool value)
        {
            base.NotifyDefault(false);
        }

        public void SetPalette(Color baseColor)
        {
            _baseColor = baseColor;
            _hoverColor = VisualEffects.Blend(baseColor, Color.White, 0.14d);
            _targetColor = baseColor;
            BackColor = baseColor;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovered = true;
            MoveTo(_hoverColor);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            _pressed = false;
            MoveTo(_baseColor);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            base.OnMouseDown(mevent);
            _pressed = true;
            MoveTo(VisualEffects.Blend(_baseColor, Color.Black, 0.12d));
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            base.OnMouseUp(mevent);
            _pressed = false;
            MoveTo(_hovered ? _hoverColor : _baseColor);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer.Dispose();
            base.Dispose(disposing);
        }

        private void MoveTo(Color color)
        {
            _targetColor = color;
            if (VisualEffects.ReduceMotion)
                BackColor = color;
            else
                _timer.Start();
        }

        private void Animate(object sender, EventArgs e)
        {
            Color target = _targetColor;
            if (_attentionPulse && !_hovered && !_pressed && !VisualEffects.ReduceMotion)
            {
                _pulsePhase += 0.10d;
                double light = 0.06d + (Math.Sin(_pulsePhase) + 1d) * 0.045d;
                target = VisualEffects.Blend(_baseColor, Color.White, light);
            }
            BackColor = VisualEffects.Blend(BackColor, target, 0.24d);
            if (!_attentionPulse && Math.Abs(BackColor.R - target.R) < 2 && Math.Abs(BackColor.G - target.G) < 2 && Math.Abs(BackColor.B - target.B) < 2)
            {
                BackColor = target;
                _timer.Stop();
            }
        }
    }

    internal sealed class ActivityBar : Control
    {
        private readonly Timer _timer = new Timer();
        private int _offset;
        private bool _running;

        public bool Running
        {
            get { return _running; }
            set
            {
                _running = value;
                if (value && !VisualEffects.ReduceMotion) _timer.Start();
                else _timer.Stop();
                Invalidate();
            }
        }

        public ActivityBar()
        {
            DoubleBuffered = true;
            Height = 5;
            _timer.Interval = 30;
            _timer.Tick += delegate { _offset = (_offset + 12) % Math.Max(1, Width + 180); Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(Theme.Border);
            if (!_running) return;
            if (VisualEffects.ReduceMotion)
            {
                using (SolidBrush brush = new SolidBrush(Theme.Blue)) e.Graphics.FillRectangle(brush, ClientRectangle);
                return;
            }
            int width = Math.Max(150, ClientSize.Width / 4);
            int left = _offset - 180;
            using (LinearGradientBrush brush = new LinearGradientBrush(
                new Rectangle(left, 0, width, Math.Max(1, Height)),
                VisualEffects.Blend(Theme.Blue, Color.White, 0.35d), Theme.Cyan, LinearGradientMode.Horizontal))
                e.Graphics.FillRectangle(brush, left, 0, width, Height);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer.Dispose();
            base.Dispose(disposing);
        }
    }

    internal sealed class RoundedPanel : Panel
    {
        public int CornerRadius { get; set; }
        public Color OutlineColor { get; set; }

        public RoundedPanel()
        {
            CornerRadius = 18;
            OutlineColor = Theme.Border;
            DoubleBuffered = true;
            Resize += delegate { UpdateShape(); };
        }

        private GraphicsPath BuildPath(Rectangle bounds)
        {
            int radius = Math.Max(4, CornerRadius);
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void UpdateShape()
        {
            if (Width < 4 || Height < 4) return;
            using (GraphicsPath path = BuildPath(new Rectangle(0, 0, Width - 1, Height - 1)))
            {
                Region = new Region(path);
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = BuildPath(new Rectangle(0, 0, Width - 1, Height - 1)))
            using (Pen pen = new Pen(OutlineColor, 1f))
            {
                e.Graphics.DrawPath(pen, path);
            }
        }

    }

    internal sealed class MainForm : Form
    {
        private readonly Label _memoryValue = new Label();
        private readonly Label _diskValue = new Label();
        private readonly Label _flowScoreValue = new Label();
        private readonly Label _lastRunValue = new Label();
        private readonly Label _recommendation = new Label();
        private readonly Label _analysisValue = new Label();
        private readonly ActivityBar _progress = new ActivityBar();
        private readonly RichTextBox _log = new RichTextBox();
        private readonly Button _analyzeButton = new Button();
        private readonly Button _runButton = new AnimatedButton();
        private readonly Button _advancedButton = new AnimatedButton();
        private readonly Button _bootButton = new Button();
        private readonly Button _driversButton = new Button();
        private readonly AnimatedButton _guardButton = new AnimatedButton();
        private readonly Button _meetingButton = new Button();
        private readonly Button _reportsButton = new Button();
        private readonly Button _settingsButton = new Button();
        private readonly Button _planButton = new Button();
        private readonly Button _toolsButton = new AnimatedButton();
        private readonly Label _heroTitle = new Label();
        private readonly Label _guardBadge = new Label();
        private readonly Label _guardMessage = new Label();
        private readonly Label _guardProcess = new Label();
        private readonly Label _activityStatus = new Label();
        private readonly Label _hardwareSummary = new Label();
        private readonly Label _hardwareTemperature = new Label();
        private readonly Label _actionTitle = new Label();
        private readonly Label _actionHelp = new Label();
        private readonly FlowIndicator _flowIndicator = new FlowIndicator();
        private readonly CheckBox _backgroundCheck = new CheckBox();
        private readonly CheckBox _tempCheck = new CheckBox();
        private readonly CheckBox _reportsCheck = new CheckBox();
        private readonly CheckBox _recycleCheck = new CheckBox();
        private readonly CheckBox _componentsCheck = new CheckBox();
        private readonly CheckBox _healthCheck = new CheckBox();
        private readonly CheckBox _drivesCheck = new CheckBox();
        private readonly Timer _statusTimer = new Timer();
        private readonly Timer _guardMonitorTimer = new Timer();
        private readonly Timer _adaptiveTimer = new Timer();
        private readonly Timer _hardwareTimer = new Timer();
        private readonly Timer _replayTimer = new Timer();
        private readonly NotifyIcon _trayIcon = new NotifyIcon();
        private Icon _trayStableIcon;
        private readonly GuardHistoryStore _guardHistory = new GuardHistoryStore();
        private readonly GuardPressureDetector _guardDetector = new GuardPressureDetector();
        private readonly SmartGuardMonitor _smartMonitor = new SmartGuardMonitor();
        private readonly ReplayEngine _replayEngine = new ReplayEngine();
        private readonly ReplayProbe _replayProbe = new ReplayProbe();
        private readonly BaselineEngine _baselineEngine = new BaselineEngine();
        private BaselineEvaluation _baselineEvaluation = new BaselineEvaluation();
        private readonly AutopilotEngine _autopilotEngine = new AutopilotEngine();
        private AutopilotDecision _autopilotDecision = new AutopilotDecision();
        private readonly RecoveryStartupResult _recoveryStartup;
        private GuardSettings _guardSettings;
        private ToolStripMenuItem _startupMenuItem;
        private ToolStripMenuItem _notificationsMenuItem;
        private ToolStripMenuItem _fullscreenMenuItem;
        private List<GuardSample> _guardSamples = new List<GuardSample>();
        private long _analyzedBytes;
        private bool _busy;
        private DateTime _lastHealthScan = DateTime.MinValue;
        private HealthSnapshot _healthSnapshot;
        private bool _meetingActive;
        private DateTime _meetingEndsAt;
        private bool _closing;
        private bool _allowExit;
        private bool _maintenanceRunning;
        private DateTime _lastHistoryCompactUtc = DateTime.UtcNow;
        private DateTime _lastAlertUtc = DateTime.MinValue;
        private GuardAlertKind _lastAlertKind = GuardAlertKind.None;
        private BottleneckAdvice _currentAdvice;
        private DateTime _lastOutcomeShownUtc = DateTime.MinValue;
        private HardwareSnapshot _hardwareSnapshot;
        private bool _hardwareRefreshing;
        private bool _replayCapturing;
        private bool _showReplayAsPrimary;
        private bool _showAutopilotAsPrimary;
        private DateTime _lastReplayCaptureUtc = DateTime.MinValue;

        private static readonly string DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Neck");
        private static readonly string LastRunFile = Path.Combine(DataDirectory, "ultima-manutencao.txt");
        private static readonly string ReportDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Neck", "Relatorios");

        public MainForm(bool startHidden = false, bool suppressOnboarding = false, RecoveryStartupResult recoveryStartup = null)
        {
            _recoveryStartup = recoveryStartup ?? RecoveryManager.LastResult;
            Text = "Neck";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(980, 720);
            Size = new Size(1080, 770);
            BackColor = Theme.Background;
            Font = Theme.Body;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;

            _tempCheck.Checked = true;
            _reportsCheck.Checked = true;
            _componentsCheck.Checked = true;
            _drivesCheck.Checked = true;

            _guardSettings = GuardSettings.Load();
            VisualEffects.ReduceMotion = _guardSettings.ReduceMotion;
            BuildInterface();
            InitializeGuardMonitoring();
            UpdateSystemStatus();
            LoadLastRun();

            _statusTimer.Interval = 3000;
            _statusTimer.Tick += delegate { if (!_busy) UpdateSystemStatus(); };
            _statusTimer.Start();
            _guardMonitorTimer.Interval = 60000;
            _guardMonitorTimer.Tick += delegate { CaptureGuardSample(); };
            _guardMonitorTimer.Start();
            _adaptiveTimer.Interval = 2000;
            _adaptiveTimer.Tick += delegate
            {
                if (_closing) return;
                try
                {
                    string focusBefore = FocusModeManager.ActiveDisplayName;
                    EfficiencyModeManager.RefreshAdaptiveModes();
                    FocusModeManager.Refresh();
                    OptimizationOutcome outcome = OptimizationOutcomeMonitor.Refresh();
                    if (outcome != null && outcome.Complete && outcome.StartedUtc > _lastOutcomeShownUtc)
                    {
                        _lastOutcomeShownUtc = outcome.StartedUtc;
                        _activityStatus.Text = outcome.Summary;
                        if (!Visible) _trayIcon.ShowBalloonTip(4500, "Resultado da aceleração", outcome.Summary, ToolTipIcon.Info);
                    }
                    if (!string.IsNullOrWhiteSpace(focusBefore) && string.IsNullOrWhiteSpace(FocusModeManager.ActiveDisplayName))
                        _trayIcon.ShowBalloonTip(3000, "Aceleração concluída", "O tempo terminou e o aplicativo voltou ao funcionamento normal.", ToolTipIcon.Info);
                }
                catch { }
            };
            _adaptiveTimer.Start();
            _hardwareTimer.Interval = 30000;
            _hardwareTimer.Tick += async delegate
            {
                if (Visible && !_closing) await RefreshHardwareAsync(false);
            };
            _hardwareTimer.Start();
            _replayTimer.Interval = 10000;
            _replayTimer.Tick += async delegate { await CaptureReplayAsync(); };
            _replayTimer.Start();

            if (!startHidden)
            {
                Shown += async delegate
                {
                    if (!suppressOnboarding && !_guardSettings.OnboardingCompleted) ShowPreferences(true);
                    await AnalyzeAsync(false);
                };
            }
            Shown += delegate { CaptureGuardSample(); };
            Shown += async delegate { await CaptureReplayAsync(); };
            Shown += async delegate { await RefreshHardwareAsync(true); };
            Shown += delegate { if (!startHidden) VisualEffects.FadeIn(this); };
            if (startHidden)
            {
                ShowInTaskbar = false;
                WindowState = FormWindowState.Minimized;
                Shown += delegate
                {
                    Hide();
                    ShowInTaskbar = true;
                };
            }
            FormClosing += HandleMainFormClosing;
            FormClosed += delegate
            {
                if (_meetingActive) NativeMethods.SetThreadExecutionState(NativeMethods.ES_CONTINUOUS);
                _statusTimer.Stop();
                _guardMonitorTimer.Stop();
                _adaptiveTimer.Stop();
                _hardwareTimer.Stop();
                _replayTimer.Stop();
                _replayProbe.Dispose();
                _baselineEngine.Dispose();
                AutopilotProtectionManager.Stop();
                FocusModeManager.Stop();
                EfficiencyModeManager.RestoreAll();
                _trayIcon.Visible = false;
                _trayIcon.Icon = null;
                _trayIcon.Dispose();
                if (_trayStableIcon != null) _trayStableIcon.Dispose();
            };
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
                Height = 86,
                BackColor = Theme.Card,
                Padding = new Padding(30, 12, 30, 10)
            };

            FlowMark mark = new FlowMark
            {
                Size = new Size(48, 48),
                Location = new Point(30, 19)
            };

            Label title = new Label
            {
                AutoSize = true,
                Text = "Neck",
                Font = Theme.Brand,
                ForeColor = Theme.Ink,
                Location = new Point(93, 11)
            };
            Label subtitle = new Label
            {
                AutoSize = true,
                Text = "Destrave o fluxo do seu computador",
                Font = Theme.Body,
                ForeColor = Theme.Muted,
                Location = new Point(96, 51)
            };

            Label promise = new Label
            {
                AutoSize = false,
                Size = new Size(205, 36),
                Text = "●  Acompanhamento local",
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI Semibold", 9f, FontStyle.Regular),
                ForeColor = Theme.Green,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(ClientSize.Width - 235, 25)
            };
            header.Resize += delegate { promise.Left = header.ClientSize.Width - promise.Width - 30; };
            header.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen line = new Pen(Theme.Hairline))
                    e.Graphics.DrawLine(line, 0, header.ClientSize.Height - 1, header.ClientSize.Width, header.ClientSize.Height - 1);
            };

            header.Controls.Add(mark);
            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            header.Controls.Add(promise);
            return header;
        }

        private Control BuildBody()
        {
            TableLayoutPanel body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background,
                Padding = new Padding(24, 20, 24, 18),
                ColumnCount = 2,
                RowCount = 3
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 288f));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 178f));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Control guard = BuildGuardCard();
            body.SetColumnSpan(guard, 2);
            body.Controls.Add(guard, 0, 0);
            body.Controls.Add(BuildQuickCard(), 0, 1);
            body.Controls.Add(BuildDeepCard(), 1, 1);
            Control tools = BuildToolsStrip();
            body.SetColumnSpan(tools, 2);
            body.Controls.Add(tools, 0, 2);
            return body;
        }

        private Control BuildQuickCard()
        {
            RoundedPanel card = MakeCard(Padding.Empty);
            card.Margin = new Padding(0, 10, 9, 10);
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(22, 15, 18, 15),
                BackColor = Theme.Card
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Panel content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Margin = Padding.Empty };
            Label eyebrow = new Label { Text = "Quando precisar", AutoSize = true, Font = Theme.Small, ForeColor = Theme.Blue, Location = new Point(0, 0) };
            Label title = new Label
            {
                Text = "Liberar espaço",
                AutoSize = true,
                Font = new Font("Segoe UI Variable Display", 16f, FontStyle.Bold),
                ForeColor = Theme.Text,
                Location = new Point(-1, 22)
            };
            Label description = new Label
            {
                Text = "Remove temporários antigos sem tocar nos seus arquivos.",
                AutoSize = false,
                Size = new Size(280, 36),
                Font = Theme.Body,
                ForeColor = Theme.Muted,
                Location = new Point(1, 55)
            };
            Label available = new Label
            {
                Text = "Para limpar:",
                AutoSize = true,
                Font = Theme.Small,
                ForeColor = Theme.Muted,
                Location = new Point(1, 99)
            };
            _analysisValue.Text = "Analisando…";
            _analysisValue.AutoSize = true;
            _analysisValue.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
            _analysisValue.ForeColor = Theme.Text;
            _analysisValue.Location = new Point(75, 97);

            ConfigureButton(_analyzeButton, "Analisar novamente", Theme.NavySoft, 1);
            _analyzeButton.Visible = false;
            ConfigureButton(_runButton, "Limpar arquivos", Theme.Lime, 172);
            _runButton.Size = new Size(172, 46);
            _runButton.Anchor = AnchorStyles.None;
            _runButton.ForeColor = Theme.Ink;
            _analyzeButton.Click += async delegate { await AnalyzeAsync(true); };
            _runButton.Click += async delegate
            {
                _tempCheck.Checked = true;
                _reportsCheck.Checked = true;
                _recycleCheck.Checked = false;
                _componentsCheck.Checked = false;
                _healthCheck.Checked = false;
                _drivesCheck.Checked = false;
                await RunMaintenanceAsync();
            };

            content.Resize += delegate { description.Width = Math.Max(120, content.ClientSize.Width - 6); };
            content.Controls.Add(eyebrow);
            content.Controls.Add(title);
            content.Controls.Add(description);
            content.Controls.Add(available);
            content.Controls.Add(_analysisValue);
            layout.Controls.Add(content, 0, 0);
            layout.Controls.Add(_runButton, 1, 0);
            card.Controls.Add(layout);
            return card;
        }

        private Control BuildDeepCard()
        {
            RoundedPanel card = MakeCard(Padding.Empty);
            card.Margin = new Padding(9, 10, 0, 10);
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(22, 15, 18, 15),
                BackColor = Theme.Card
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Panel content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Margin = Padding.Empty };
            Label eyebrow = new Label { Text = "Cuidado mensal", AutoSize = true, Font = Theme.Small, ForeColor = Theme.Green, Location = new Point(0, 0) };
            Label title = new Label
            {
                Text = "Cuidado completo",
                AutoSize = true,
                Font = new Font("Segoe UI Variable Display", 16f, FontStyle.Bold),
                ForeColor = Theme.Text,
                Location = new Point(-1, 22)
            };
            Label description = new Label
            {
                Text = "Revê o Windows e mostra só o que vale a pena.",
                AutoSize = false,
                Size = new Size(280, 36),
                Font = Theme.Body,
                ForeColor = Theme.Muted,
                Location = new Point(1, 55)
            };
            _recommendation.AutoSize = false;
            _recommendation.Size = new Size(280, 25);
            _recommendation.TextAlign = ContentAlignment.MiddleLeft;
            _recommendation.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            _recommendation.ForeColor = Theme.Green;
            _recommendation.BackColor = Color.Transparent;
            _recommendation.Location = new Point(1, 97);
            _lastRunValue.Visible = false;
            ConfigureButton(_advancedButton, "Revisar agora", Theme.Lime, 160);
            _advancedButton.Size = new Size(160, 46);
            _advancedButton.Anchor = AnchorStyles.None;
            _advancedButton.ForeColor = Theme.Ink;
            _advancedButton.Click += async delegate { await ShowAdvancedAndRunAsync(); };
            _bootButton.Click += delegate
            {
                using (StartupAppsForm form = new StartupAppsForm()) form.ShowDialog(this);
            };

            content.Resize += delegate
            {
                description.Width = Math.Max(120, content.ClientSize.Width - 6);
                _recommendation.Width = Math.Max(120, content.ClientSize.Width - 6);
            };
            content.Controls.Add(eyebrow);
            content.Controls.Add(title);
            content.Controls.Add(description);
            content.Controls.Add(_recommendation);
            layout.Controls.Add(content, 0, 0);
            layout.Controls.Add(_advancedButton, 1, 0);
            card.Controls.Add(layout);
            return card;
        }

        private Control BuildGuardCard()
        {
            RoundedPanel card = MakeCard(Padding.Empty);
            card.Margin = new Padding(0, 0, 0, 0);
            card.CornerRadius = 24;
            TableLayoutPanel shell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(16, 14, 16, 14),
                BackColor = Theme.Card
            };
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64f));
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36f));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            TableLayoutPanel overview = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(14, 4, 10, 4),
                Margin = Padding.Empty,
                BackColor = Theme.Card
            };
            overview.RowStyles.Add(new RowStyle(SizeType.Absolute, 27f));
            overview.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
            overview.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));
            overview.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
            overview.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _guardBadge.Text = "●  Lendo o fluxo";
            _guardBadge.AutoSize = false;
            _guardBadge.Dock = DockStyle.Fill;
            _guardBadge.BackColor = Color.Transparent;
            _guardBadge.ForeColor = Theme.Blue;
            _guardBadge.TextAlign = ContentAlignment.MiddleLeft;
            _guardBadge.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Regular);
            _guardBadge.Margin = Padding.Empty;
            _heroTitle.Text = "Entendendo seu computador";
            _heroTitle.AutoSize = false;
            _heroTitle.Dock = DockStyle.Fill;
            _heroTitle.Font = new Font("Segoe UI Variable Display", 23f, FontStyle.Bold);
            _heroTitle.ForeColor = Theme.Text;
            _heroTitle.TextAlign = ContentAlignment.MiddleLeft;
            _heroTitle.Margin = Padding.Empty;
            _guardMessage.Text = "Procurando sinais de sobrecarga...";
            _guardMessage.AutoSize = false;
            _guardMessage.Dock = DockStyle.Fill;
            _guardMessage.Font = Theme.Body;
            _guardMessage.ForeColor = Theme.Muted;
            _guardMessage.TextAlign = ContentAlignment.TopLeft;
            _guardMessage.Margin = Padding.Empty;
            _guardProcess.Text = "Maior uso de memória: calculando";
            _guardProcess.AutoSize = false;
            _guardProcess.Dock = DockStyle.Fill;
            _guardProcess.Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold);
            _guardProcess.ForeColor = Theme.Text;
            _guardProcess.TextAlign = ContentAlignment.MiddleLeft;
            _guardProcess.Margin = Padding.Empty;

            RoundedPanel actionArea = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.FlowSoft,
                OutlineColor = Theme.FlowSoft,
                CornerRadius = 20,
                Padding = new Padding(22, 18, 22, 18),
                Margin = new Padding(12, 0, 0, 0)
            };
            TableLayoutPanel actionLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Theme.FlowSoft };
            actionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            actionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55f));
            actionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));
            actionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            actionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50f));
            _actionTitle.Dock = DockStyle.Fill;
            _actionTitle.Text = "Analisando o fluxo do computador...";
            _actionTitle.Font = new Font("Segoe UI Variable Display", 13f, FontStyle.Bold);
            _actionTitle.ForeColor = Theme.Text;
            _actionTitle.TextAlign = ContentAlignment.MiddleLeft;
            _actionTitle.Margin = Padding.Empty;
            _actionHelp.Dock = DockStyle.Fill;
            _actionHelp.Text = "O Neck indicará a ação mais útil agora.";
            _actionHelp.Font = Theme.Small;
            _actionHelp.ForeColor = Theme.Muted;
            _actionHelp.TextAlign = ContentAlignment.TopLeft;
            _actionHelp.Margin = new Padding(0, 4, 0, 4);
            ConfigureButton(_guardButton, "Dar prioridade a um app", Theme.Lime, 244);
            _guardButton.Dock = DockStyle.Fill;
            _guardButton.ForeColor = Theme.Ink;
            _guardButton.Margin = new Padding(0, 4, 0, 0);
            ConfigureButton(_meetingButton, "Modo reunião", Theme.Blue, 1);
            _meetingButton.Visible = false;
            _meetingButton.Click += delegate { ToggleMeetingMode(); };
            _guardButton.Click += async delegate
            {
                if (_showReplayAsPrimary)
                {
                    await ShowReplayAsync();
                    return;
                }
                if (_showAutopilotAsPrimary)
                {
                    ShowAutopilot();
                    return;
                }
                if (_currentAdvice != null && _currentAdvice.Kind == BottleneckKind.Disk)
                {
                    _tempCheck.Checked = true;
                    _reportsCheck.Checked = true;
                    _recycleCheck.Checked = false;
                    _componentsCheck.Checked = false;
                    _healthCheck.Checked = false;
                    _drivesCheck.Checked = false;
                    await RunMaintenanceAsync();
                }
                else OpenSos();
            };
            _flowIndicator.Dock = DockStyle.Fill;
            _flowIndicator.Margin = Padding.Empty;
            actionLayout.Controls.Add(_actionTitle, 0, 0);
            actionLayout.Controls.Add(_flowIndicator, 0, 1);
            actionLayout.Controls.Add(_actionHelp, 0, 2);
            actionLayout.Controls.Add(_guardButton, 0, 3);
            actionArea.Controls.Add(actionLayout);

            TableLayoutPanel metrics = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Theme.FlowSoft,
                Padding = new Padding(4),
                Margin = new Padding(0, 5, 0, 0)
            };
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334f));
            metrics.Controls.Add(BuildInlineMetric("Memória em uso", _memoryValue), 0, 0);
            metrics.Controls.Add(BuildInlineMetric("Espaço livre", _diskValue), 1, 0);
            Control flowMetric = BuildInlineMetric("Índice de fluxo  ›", _flowScoreValue);
            MakeFlowMetricInteractive(flowMetric);
            metrics.Controls.Add(flowMetric, 2, 0);

            overview.Controls.Add(_guardBadge, 0, 0);
            overview.Controls.Add(_heroTitle, 0, 1);
            overview.Controls.Add(_guardMessage, 0, 2);
            overview.Controls.Add(_guardProcess, 0, 3);
            overview.Controls.Add(metrics, 0, 4);
            shell.Controls.Add(overview, 0, 0);
            shell.Controls.Add(actionArea, 1, 0);
            card.Controls.Add(shell);
            return card;
        }

        private static Control BuildInlineMetric(string caption, Label value)
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.FlowSoft, Margin = Padding.Empty };
            Label name = new Label
            {
                Text = caption,
                AutoSize = false,
                Location = new Point(0, 2),
                Size = new Size(100, 19),
                Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold),
                ForeColor = Theme.Muted,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = Padding.Empty
            };
            value.AutoSize = false;
            value.Location = new Point(0, 22);
            value.Size = new Size(100, 27);
            value.TextAlign = ContentAlignment.MiddleCenter;
            value.Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold);
            value.ForeColor = Theme.Text;
            value.Margin = Padding.Empty;
            panel.Controls.Add(name);
            panel.Controls.Add(value);
            panel.Resize += delegate
            {
                name.Width = panel.ClientSize.Width;
                value.Width = panel.ClientSize.Width;
            };
            return panel;
        }

        private void MakeFlowMetricInteractive(Control control)
        {
            control.Cursor = Cursors.Hand;
            control.Click += delegate { ShowBaseline(); };
            foreach (Control child in control.Controls) MakeFlowMetricInteractive(child);
        }

        private Control BuildToolsStrip()
        {
            RoundedPanel card = MakeCard(new Padding(22));
            card.Margin = new Padding(0, 0, 0, 0);
            Label title = new Label
            {
                Text = "Seu computador por dentro",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 12.5f, FontStyle.Bold),
                ForeColor = Theme.Text,
                Location = new Point(24, 13),
                Cursor = Cursors.Hand
            };
            _hardwareSummary.Text = "Identificando processador, vídeo, memória e armazenamento...";
            _hardwareSummary.AutoSize = false;
            _hardwareSummary.Size = new Size(700, 44);
            _hardwareSummary.Font = Theme.Small;
            _hardwareSummary.ForeColor = Theme.Text;
            _hardwareSummary.Location = new Point(25, 37);
            _hardwareSummary.AutoEllipsis = true;
            _hardwareSummary.Cursor = Cursors.Hand;
            _hardwareTemperature.Text = "Temperatura: procurando sensores locais...  •  Ver especificações";
            _hardwareTemperature.AutoSize = false;
            _hardwareTemperature.Size = new Size(700, 22);
            _hardwareTemperature.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
            _hardwareTemperature.ForeColor = Theme.Blue;
            _hardwareTemperature.Location = new Point(25, 84);
            _hardwareTemperature.Cursor = Cursors.Hand;
            _activityStatus.Text = "Tudo pronto. O Neck continua acompanhando o computador.";
            _activityStatus.AutoSize = false;
            _activityStatus.Size = new Size(650, 24);
            _activityStatus.Font = Theme.Small;
            _activityStatus.ForeColor = Theme.Muted;
            _activityStatus.Location = new Point(25, 48);
            _activityStatus.Visible = false;
            _progress.Dock = DockStyle.Bottom;
            _progress.Height = 5;
            _progress.Visible = false;
            _log.Visible = false;
            ConfigureButton(_toolsButton, "Ver ferramentas", Theme.FlowSoft, 170);
            _toolsButton.ForeColor = Theme.Ink;
            _toolsButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _toolsButton.Location = new Point(800, 37);
            _toolsButton.Click += async delegate { await ShowToolsHubAsync(); };
            EventHandler showHardware = async delegate { await ShowHardwareDetailsAsync(); };
            title.Click += showHardware;
            _hardwareSummary.Click += showHardware;
            _hardwareTemperature.Click += showHardware;
            card.Resize += delegate
            {
                _toolsButton.Left = card.ClientSize.Width - _toolsButton.Width - 24;
                int availableWidth = Math.Max(300, _toolsButton.Left - 44);
                _hardwareSummary.Width = availableWidth;
                _hardwareTemperature.Width = availableWidth;
                bool compact = card.ClientSize.Height < 100;
                _hardwareSummary.Height = compact ? 24 : 44;
                _hardwareTemperature.Visible = !compact;
                _hardwareSummary.Top = compact ? 39 : 37;
                _toolsButton.Top = compact ? 22 : 37;
                UpdateHardwareSummary(compact);
            };
            card.Controls.Add(title);
            card.Controls.Add(_hardwareSummary);
            card.Controls.Add(_hardwareTemperature);
            card.Controls.Add(_activityStatus);
            card.Controls.Add(_toolsButton);
            card.Controls.Add(_progress);
            card.Controls.Add(_log);
            return card;
        }

        private void InitializeGuardMonitoring()
        {
            if (_guardSettings == null) _guardSettings = GuardSettings.Load();
            _guardSamples = _guardHistory.LoadLast24Hours();
            _guardHistory.Compact(_guardSamples);
            _backgroundCheck.Checked = _guardSettings.ContinueInTray;
            _backgroundCheck.CheckedChanged += delegate
            {
                _guardSettings.ContinueInTray = _backgroundCheck.Checked;
                _guardSettings.Save();
            };

            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem status = new ToolStripMenuItem("Neck Guard iniciando...") { Enabled = false };
            status.Name = "status";
            menu.Items.Add(status);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Abrir Neck", null, delegate { ShowFromTray(); });
            menu.Items.Add("Acelerar aplicativo", null, delegate { ShowFromTray(); OpenSos(); });
            menu.Items.Add("Meu padrão Neck", null, delegate { ShowFromTray(); ShowBaseline(); });
            menu.Items.Add("Neck Autopilot", null, delegate { ShowFromTray(); ShowAutopilot(); });
            menu.Items.Add("Neck Replay", null, async delegate { ShowFromTray(); await ShowReplayAsync(); });
            menu.Items.Add("Parar aceleração", null, delegate
            {
                if (!FocusModeManager.IsActive)
                {
                    _trayIcon.ShowBalloonTip(2500, "Acelerar", "Nenhum aplicativo está sendo acelerado.", ToolTipIcon.Info);
                    return;
                }
                string displayName = FocusModeManager.ActiveDisplayName;
                FocusModeManager.Stop();
                _trayIcon.ShowBalloonTip(3000, "Aceleração encerrada", displayName + " voltou ao funcionamento normal.", ToolTipIcon.Info);
                UpdateGuardView(_healthSnapshot);
            });
            menu.Items.Add("Parar reduções em segundo plano", null, async delegate
            {
                if (FocusModeManager.IsActive) FocusModeManager.Stop();
                if (EfficiencyModeManager.ActiveCount == 0)
                {
                    _trayIcon.ShowBalloonTip(2500, "Segundo plano", "Nenhum aplicativo está usando menos recursos.", ToolTipIcon.Info);
                    return;
                }
                EfficiencyModeResult result = await Task.Run(delegate { return EfficiencyModeManager.RestoreAll(); });
                _trayIcon.ShowBalloonTip(3000, "Segundo plano", "Configurações originais restauradas em " + result.ProcessesChanged + " processo(s).", ToolTipIcon.Info);
            });
            menu.Items.Add("Abrir diagnóstico", null, delegate
            {
                ShowFromTray();
                HealthSnapshot snapshot = _healthSnapshot;
                if (snapshot == null || DateTime.UtcNow - _lastReplayCaptureUtc >= TimeSpan.FromSeconds(20))
                    snapshot = SystemInfo.GetHealthSnapshot();
                using (DiagnosticForm form = new DiagnosticForm(snapshot)) form.ShowDialog(this);
            });
            menu.Items.Add("Modo Reunião", null, delegate { ShowFromTray(); ToggleMeetingMode(); });
            menu.Items.Add("Suporte e recuperação", null, delegate { ShowFromTray(); ShowSupport(); });
            bool changingStartup = false;
            _startupMenuItem = new ToolStripMenuItem("Iniciar com o Windows") { CheckOnClick = true, Checked = StartupManager.IsEnabled() };
            _startupMenuItem.CheckedChanged += delegate
            {
                if (changingStartup) return;
                try
                {
                    StartupManager.SetEnabled(_startupMenuItem.Checked);
                    if (_startupMenuItem.Checked) _backgroundCheck.Checked = true;
                }
                catch (Exception ex)
                {
                    changingStartup = true;
                    _startupMenuItem.Checked = !_startupMenuItem.Checked;
                    changingStartup = false;
                    MessageBox.Show("Não foi possível alterar a inicialização do Neck.\n\n" + ex.Message, "Neck", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            menu.Items.Add(_startupMenuItem);
            _notificationsMenuItem = new ToolStripMenuItem("Exibir notificações") { CheckOnClick = true, Checked = _guardSettings.Notifications };
            _notificationsMenuItem.CheckedChanged += delegate { _guardSettings.Notifications = _notificationsMenuItem.Checked; _guardSettings.Save(); };
            menu.Items.Add(_notificationsMenuItem);
            _fullscreenMenuItem = new ToolStripMenuItem("Silenciar em tela cheia") { CheckOnClick = true, Checked = _guardSettings.SilenceFullscreen };
            _fullscreenMenuItem.CheckedChanged += delegate { _guardSettings.SilenceFullscreen = _fullscreenMenuItem.Checked; _guardSettings.Save(); };
            menu.Items.Add(_fullscreenMenuItem);
            menu.Items.Add("Preferências", null, delegate { ShowFromTray(); ShowPreferences(false); });
            menu.Items.Add("Silenciar alertas por 2 horas", null, delegate
            {
                _guardSettings.SilentUntilUtc = DateTime.UtcNow.AddHours(2);
                _guardSettings.Save();
                _trayIcon.ShowBalloonTip(2500, "Neck Guard", "Alertas silenciados por duas horas.", ToolTipIcon.Info);
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Sair do Neck", null, delegate
            {
                _allowExit = true;
                ShowFromTray();
                Close();
            });

            _trayStableIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? (Icon)SystemIcons.Application.Clone();
            _trayIcon.Icon = _trayStableIcon;
            _trayIcon.Text = "Neck Guard";
            _trayIcon.Visible = true;
            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += delegate { ShowFromTray(); };
            _trayIcon.BalloonTipClicked += delegate
            {
                ShowFromTray();
            };
        }

        private void OpenSos()
        {
            if (_closing || IsDisposed || _busy) return;
            using (SosForm form = new SosForm(_currentAdvice == null ? null : _currentAdvice.ProcessName)) form.ShowDialog(this);
            UpdateSystemStatus();
            UpdateGuardView(_healthSnapshot);
        }

        private void ShowPreferences(bool firstRun)
        {
            if (_closing || IsDisposed) return;
            using (PreferencesForm form = new PreferencesForm(_guardSettings, firstRun))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
            }

            _backgroundCheck.Checked = _guardSettings.ContinueInTray;
            if (_startupMenuItem != null) _startupMenuItem.Checked = StartupManager.IsEnabled();
            if (_notificationsMenuItem != null) _notificationsMenuItem.Checked = _guardSettings.Notifications;
            if (_fullscreenMenuItem != null) _fullscreenMenuItem.Checked = _guardSettings.SilenceFullscreen;
            VisualEffects.ReduceMotion = _guardSettings.ReduceMotion;
            if (!_guardSettings.AutopilotEnabled)
            {
                AutopilotProtectionManager.Stop();
                _autopilotDecision = _autopilotEngine.DisableNow();
                UpdateGuardView(_healthSnapshot);
            }
        }

        private async Task ShowPersonalPlanAsync()
        {
            if (_closing || IsDisposed || _busy) return;
            SetBusy(true, "Preparando seu plano personalizado...");
            PersonalPlan plan;
            try
            {
                plan = await Task.Run(delegate { return PersonalPlanAnalyzer.Build(); });
                if (_closing || IsDisposed) return;
            }
            catch (Exception ex)
            {
                if (!_closing && !IsDisposed)
                    MessageBox.Show("Não foi possível montar o plano agora.\n\n" + ex.Message, "Meu Plano Neck", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            finally
            {
                if (!_closing && !IsDisposed) SetBusy(false, null);
            }

            PlanActionKind selected;
            using (PersonalPlanForm form = new PersonalPlanForm(plan))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                selected = form.SelectedAction;
            }
            await ExecutePlanActionAsync(selected, plan.Health);
        }

        private async Task ExecutePlanActionAsync(PlanActionKind action, HealthSnapshot health)
        {
            if (action == PlanActionKind.Sos)
            {
                OpenSos();
            }
            else if (action == PlanActionKind.Clean)
            {
                _tempCheck.Checked = true;
                _reportsCheck.Checked = true;
                _recycleCheck.Checked = false;
                _componentsCheck.Checked = false;
                _healthCheck.Checked = false;
                _drivesCheck.Checked = false;
                await RunMaintenanceAsync();
            }
            else if (action == PlanActionKind.Startup)
            {
                using (StartupAppsForm form = new StartupAppsForm()) form.ShowDialog(this);
            }
            else if (action == PlanActionKind.WindowsUpdate)
            {
                OpenTarget("ms-settings:windowsupdate");
            }
            else if (action == PlanActionKind.Diagnostic)
            {
                using (DiagnosticForm form = new DiagnosticForm(health ?? SystemInfo.GetHealthSnapshot())) form.ShowDialog(this);
            }
        }

        private async Task ShowToolsHubAsync()
        {
            if (_closing || IsDisposed || _busy) return;
            ToolHubChoice choice;
            using (ToolsHubForm form = new ToolsHubForm(_backgroundCheck.Checked, _meetingActive, _activityStatus.Text))
            {
                form.ShowDialog(this);
                _backgroundCheck.Checked = form.ContinueInTray;
                choice = form.SelectedChoice;
            }

            if (choice == ToolHubChoice.Plan)
                await ShowPersonalPlanAsync();
            else if (choice == ToolHubChoice.Meeting)
                ToggleMeetingMode();
            else if (choice == ToolHubChoice.Startup)
            {
                using (StartupAppsForm form = new StartupAppsForm()) form.ShowDialog(this);
            }
            else if (choice == ToolHubChoice.Diagnostic)
            {
                using (DiagnosticForm form = new DiagnosticForm(_healthSnapshot ?? SystemInfo.GetHealthSnapshot())) form.ShowDialog(this);
            }
            else if (choice == ToolHubChoice.Drivers)
            {
                using (DriverCenterForm form = new DriverCenterForm()) form.ShowDialog(this);
            }
            else if (choice == ToolHubChoice.Bluetooth)
            {
                using (BluetoothDoctorForm form = new BluetoothDoctorForm()) form.ShowDialog(this);
            }
            else if (choice == ToolHubChoice.Replay)
            {
                await ShowReplayAsync();
            }
            else if (choice == ToolHubChoice.History)
            {
                using (GuardHistoryForm form = new GuardHistoryForm(_guardSamples.ToList(), ReportDirectory)) form.ShowDialog(this);
            }
            else if (choice == ToolHubChoice.Support)
                ShowSupport();
            else if (choice == ToolHubChoice.Preferences)
                ShowPreferences(false);
        }

        private void ShowSupport()
        {
            if (_closing || IsDisposed) return;
            using (SupportReportForm form = new SupportReportForm(_guardSettings, _guardSamples.ToList(),
                _hardwareSnapshot, _recoveryStartup)) form.ShowDialog(this);
        }

        private async Task ShowReplayAsync()
        {
            if (_closing || IsDisposed) return;
            ReplayActionKind action;
            string processName;
            bool historyRequested;
            using (ReplayForm form = new ReplayForm(_replayEngine.GetLatestIncident(), _replayEngine.GetSamples()))
            {
                form.ShowDialog(this);
                action = form.SelectedAction;
                processName = form.IncidentProcessName;
                historyRequested = form.HistoryRequested;
            }
            if (historyRequested)
            {
                using (GuardHistoryForm history = new GuardHistoryForm(_guardSamples.ToList(), ReportDirectory)) history.ShowDialog(this);
                return;
            }
            if (action == ReplayActionKind.Accelerate)
            {
                using (SosForm form = new SosForm(string.IsNullOrWhiteSpace(processName) ? null : processName)) form.ShowDialog(this);
                UpdateSystemStatus();
                UpdateGuardView(_healthSnapshot);
            }
            else if (action == ReplayActionKind.Diagnostic)
            {
                using (DiagnosticForm form = new DiagnosticForm(_healthSnapshot ?? SystemInfo.GetHealthSnapshot())) form.ShowDialog(this);
            }
            else if (action == ReplayActionKind.Hardware)
            {
                await ShowHardwareDetailsAsync();
            }
        }

        private void ShowBaseline()
        {
            if (_closing || IsDisposed) return;
            bool showAutopilot;
            using (BaselineForm form = new BaselineForm(_baselineEngine.GetView()))
            {
                form.ShowDialog(this);
                showAutopilot = form.AutopilotRequested;
            }
            if (showAutopilot) ShowAutopilot();
        }

        private void ShowAutopilot()
        {
            if (_closing || IsDisposed) return;
            using (AutopilotForm form = new AutopilotForm(_guardSettings, _autopilotEngine,
                _autopilotDecision, _baselineEngine.GetView())) form.ShowDialog(this);
            if (!_guardSettings.AutopilotEnabled)
            {
                AutopilotProtectionManager.Stop();
                _autopilotDecision = _autopilotEngine.DisableNow();
            }
            UpdateGuardView(_healthSnapshot);
        }

        private async Task RefreshHardwareAsync(bool fullInventory)
        {
            if (_hardwareRefreshing || _closing || IsDisposed) return;
            _hardwareRefreshing = true;
            try
            {
                if (fullInventory || _hardwareSnapshot == null)
                    _hardwareSnapshot = await Task.Run(delegate { return HardwareInfoProvider.Read(); });
                else
                {
                    List<TemperatureReading> temperatures = await Task.Run(delegate { return HardwareInfoProvider.ReadTemperatures(); });
                    if (_hardwareSnapshot != null)
                    {
                        _hardwareSnapshot.Temperatures = temperatures;
                        _hardwareSnapshot.CapturedUtc = DateTime.UtcNow;
                    }
                }
                if (_closing || IsDisposed) return;
                UpdateHardwareSummary(_hardwareSummary.Parent != null && _hardwareSummary.Parent.ClientSize.Height < 100);
            }
            catch
            {
                if (!_closing && !IsDisposed)
                {
                    _hardwareSummary.Text = "O Windows não disponibilizou o inventário de hardware agora.";
                    _hardwareTemperature.Text = "Temperatura: sensor não disponibilizado  •  Ver especificações";
                    _hardwareTemperature.ForeColor = Theme.Muted;
                }
            }
            finally { _hardwareRefreshing = false; }
        }

        private void UpdateHardwareSummary(bool compact)
        {
            if (_hardwareSummary == null || _hardwareSummary.IsDisposed) return;
            HardwareSnapshot snapshot = _hardwareSnapshot;
            if (snapshot == null)
            {
                _hardwareSummary.Text = "Identificando processador, vídeo, memória e armazenamento...";
                return;
            }
            if (compact)
            {
                _hardwareSummary.Text = "CPU " + ShortHardware(snapshot.ProcessorSummary, 23) + "  •  RAM " +
                    ShortHardware(snapshot.MemorySummary, 13) + "  •  GPU " + ShortHardware(snapshot.GraphicsSummary, 20) +
                    "  •  TEMP " + CompactTemperature(snapshot);
            }
            else
            {
                _hardwareSummary.Text = "CPU  " + ShortHardware(snapshot.ProcessorSummary, 58) + "     RAM  " + ShortHardware(snapshot.MemorySummary, 28) +
                    Environment.NewLine + "GPU  " + ShortHardware(snapshot.GraphicsSummary, 48) + "     DISCO  " + ShortHardware(snapshot.StorageSummary, 42);
            }
            _hardwareTemperature.Text = "Temperatura: " + snapshot.TemperatureSummary + "  •  Ver especificações e sensores";
            if (snapshot.Temperatures.Count == 0) _hardwareTemperature.ForeColor = Theme.Muted;
            else
            {
                double hottest = snapshot.Temperatures.Max(item => item.Celsius);
                _hardwareTemperature.ForeColor = hottest >= 90d ? Color.Firebrick : hottest >= 75d ? Theme.Amber : Theme.Green;
            }
        }

        private async Task ShowHardwareDetailsAsync()
        {
            if (_closing || IsDisposed || _hardwareRefreshing) return;
            if (_hardwareSnapshot == null) await RefreshHardwareAsync(true);
            if (_closing || IsDisposed || _hardwareSnapshot == null) return;
            using (HardwareDetailsForm form = new HardwareDetailsForm(_hardwareSnapshot)) form.ShowDialog(this);
            await RefreshHardwareAsync(false);
        }

        private static string ShortHardware(string value, int maximumLength)
        {
            string normalized = string.IsNullOrWhiteSpace(value) ? "Não informado" : value.Replace(Environment.NewLine, " ").Trim();
            return normalized.Length <= maximumLength ? normalized : normalized.Substring(0, Math.Max(1, maximumLength - 1)).TrimEnd() + "…";
        }

        private static string CompactTemperature(HardwareSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Temperatures.Count == 0) return "—";
            return snapshot.Temperatures.Max(item => item.Celsius).ToString("0", CultureInfo.CurrentCulture) + " °C";
        }

        private void HandleMainFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_maintenanceRunning && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                if (_allowExit)
                {
                    _allowExit = false;
                    MessageBox.Show("A manutenção ainda está em andamento e não pode ser encerrada com segurança. Deixe o Neck na bandeja até a conclusão.", "Neck", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                bool hide = _backgroundCheck.Checked;
                if (!hide)
                {
                    hide = MessageBox.Show(
                        "Uma tarefa do Windows ainda está em andamento. Deseja ocultar o Neck e deixar a manutenção continuar em segundo plano?",
                        "Continuar manutenção em segundo plano",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information) == DialogResult.Yes;
                }
                if (hide) HideToTray("A manutenção continua em segundo plano. O Neck avisará quando terminar.");
                return;
            }
            if (!_allowExit && e.CloseReason == CloseReason.UserClosing && _backgroundCheck.Checked)
            {
                e.Cancel = true;
                HideToTray("O monitoramento ficou ativo na bandeja. Clique duas vezes no ícone para voltar.");
                return;
            }
            _closing = true;
            _statusTimer.Stop();
            _guardMonitorTimer.Stop();
            _adaptiveTimer.Stop();
            _replayTimer.Stop();
        }

        private void HideToTray(string message)
        {
            _guardButton.AttentionPulse = false;
            Hide();
            _trayIcon.Visible = true;
            _trayIcon.ShowBalloonTip(3500, "Neck continua trabalhando", message, ToolTipIcon.Info);
        }

        private void ShowFromTray()
        {
            if (_closing || IsDisposed) return;
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            UpdateGuardView(_healthSnapshot);
        }

        private void CaptureGuardSample()
        {
            if (_closing || IsDisposed || _busy) return;
            try
            {
                HealthSnapshot snapshot = _healthSnapshot;
                if (snapshot == null || DateTime.UtcNow - _lastReplayCaptureUtc >= TimeSpan.FromSeconds(20))
                    snapshot = SystemInfo.GetHealthSnapshot();
                _healthSnapshot = snapshot;
                UpdateGuardView(snapshot);
                GuardSample sample = GuardSample.FromSnapshot(snapshot);
                _guardSamples.Add(sample);
                _guardSamples.RemoveAll(item => item.TimestampUtc < DateTime.UtcNow.AddHours(-24));
                _guardHistory.Append(sample);
                if (DateTime.UtcNow - _lastHistoryCompactUtc >= TimeSpan.FromHours(1))
                {
                    _guardHistory.Compact(_guardSamples);
                    _lastHistoryCompactUtc = DateTime.UtcNow;
                }
                UpdateTrayState(snapshot);
                SmartMonitorDecision monitoring = _smartMonitor.Evaluate(snapshot);
                _guardMonitorTimer.Interval = monitoring.NextIntervalMilliseconds;
                if (!_busy) _activityStatus.Text = monitoring.StatusMessage;
                if (monitoring.State == SmartMonitorState.Confirmed)
                    ShowGuardAlertIfNeeded(_guardDetector.Evaluate(_guardSamples));
                if (monitoring.RecoveryConfirmed) ShowRecoveryNotification();
            }
            catch { }
        }

        private async Task CaptureReplayAsync()
        {
            if (_closing || IsDisposed || _busy || _replayCapturing) return;
            _replayCapturing = true;
            try
            {
                double temperature = 0;
                if (_hardwareSnapshot != null && _hardwareSnapshot.Temperatures != null && _hardwareSnapshot.Temperatures.Count > 0)
                    temperature = _hardwareSnapshot.Temperatures.Max(item => item.Celsius);
                ReplayCapture capture = await Task.Run(delegate { return _replayProbe.Capture(temperature); });
                if (_closing || IsDisposed || capture == null || capture.Sample == null) return;
                _healthSnapshot = capture.Health;
                _lastReplayCaptureUtc = capture.Sample.TimestampUtc;
                ReplayDecision decision = _replayEngine.Record(capture.Sample);
                _baselineEvaluation = _baselineEngine.Observe(capture.Sample, _meetingActive);
                bool neckForeground = string.Equals(capture.Sample.ForegroundProcess,
                    Process.GetCurrentProcess().ProcessName, StringComparison.OrdinalIgnoreCase);
                AutopilotDecision autopilot = _autopilotEngine.Evaluate(capture.Sample, _baselineEngine.GetView(),
                    _guardSettings.AutopilotEnabled, _meetingActive, FocusModeManager.IsActive || neckForeground);
                bool protectionStarted = autopilot.ShouldProtect;
                if (autopilot.ShouldRestore)
                {
                    AutopilotProtectionResult restored = await Task.Run(delegate { return AutopilotProtectionManager.Stop(); });
                    _autopilotDecision = _autopilotEngine.ReportRestored();
                    if (restored.ApplicationsChanged > 0)
                        _activityStatus.Text = "Autopilot restaurou todos os aplicativos após a normalização do fluxo.";
                }
                else if (autopilot.ShouldProtect || autopilot.State == AutopilotState.Protecting)
                {
                    AutopilotProtectionResult protection = await Task.Run(delegate
                    {
                        string preferred = autopilot.Cause == AutopilotCause.Cpu
                            ? capture.Sample.TopCpuProcess : capture.Sample.TopMemoryProcess;
                        return autopilot.ShouldProtect
                            ? AutopilotProtectionManager.Start(capture.Sample.ForegroundProcess, autopilot.Cause, preferred)
                            : AutopilotProtectionManager.Refresh(capture.Sample.ForegroundProcess, autopilot.Cause, preferred);
                    });
                    _autopilotDecision = _autopilotEngine.ReportProtection(protection.ApplicationsProtected,
                        protection.Summary, capture.Sample.TimestampUtc);
                    if (protectionStarted)
                    {
                        _activityStatus.Text = protection.ApplicationsProtected > 0
                            ? "Autopilot protegeu o fluxo reduzindo temporariamente " + protection.ApplicationsProtected + " aplicativo(s)."
                            : "Autopilot previu pressão, mas não encontrou aplicativo seguro para reduzir.";
                        if (protection.ApplicationsProtected > 0 && _guardSettings.Notifications && !_meetingActive &&
                            _guardSettings.SilentUntilUtc <= DateTime.UtcNow &&
                            !(_guardSettings.SilenceFullscreen && SystemInfo.IsForegroundWindowFullScreen()))
                            _trayIcon.ShowBalloonTip(4500, "Neck Autopilot protegeu o fluxo",
                                protection.ApplicationsProtected + " aplicativo(s) em segundo plano usam temporariamente menos recursos.", ToolTipIcon.Info);
                    }
                }
                else _autopilotDecision = autopilot;
                UpdateGuardView(capture.Health);
                UpdateTrayState(capture.Health);
                if (decision.IncidentConfirmed && decision.Incident != null)
                {
                    _activityStatus.Text = "Neck Replay registrou um gargalo: " + decision.Incident.Title + ".";
                    if (_guardSettings.Notifications && !_meetingActive && _guardSettings.SilentUntilUtc <= DateTime.UtcNow &&
                        !(_guardSettings.SilenceFullscreen && SystemInfo.IsForegroundWindowFullScreen()))
                        _trayIcon.ShowBalloonTip(5000, "Neck Replay registrou o contexto", decision.Incident.Title + ". Abra o Neck Replay para entender o que aconteceu.", ToolTipIcon.Warning);
                }
                else if (decision.RecoveryConfirmed && decision.Incident != null)
                {
                    _activityStatus.Text = "O fluxo voltou. O Replay preservou a causa provável para você revisar.";
                }
            }
            catch { }
            finally { _replayCapturing = false; }
        }

        private void UpdateTrayState(HealthSnapshot snapshot)
        {
            if (snapshot == null || _trayIcon.ContextMenuStrip == null) return;
            if (_meetingActive)
            {
                _trayIcon.Icon = _trayStableIcon;
                _trayIcon.Text = "Neck Guard — Modo Reunião protegido";
                ToolStripItem protectedStatus = _trayIcon.ContextMenuStrip.Items["status"];
                if (protectedStatus != null) protectedStatus.Text = "Modo Reunião • protegido até " + _meetingEndsAt.ToString("HH:mm");
                return;
            }
            string state = snapshot.Level == HealthLevel.Critical ? "Gargalo" : snapshot.Level == HealthLevel.Warning ? "Atenção" : "Fluindo bem";
            if (_autopilotDecision != null && _autopilotDecision.State == AutopilotState.Protecting)
                state = "Autopilot protegendo";
            _trayIcon.Icon = snapshot.Level == HealthLevel.Critical ? SystemIcons.Error :
                             snapshot.Level == HealthLevel.Warning ? SystemIcons.Warning :
                             _trayStableIcon;
            string text = "Neck — " + state + " — RAM " + snapshot.Memory.PercentUsed.ToString("0", CultureInfo.CurrentCulture) + "% • CPU " + snapshot.CpuPercent.ToString("0", CultureInfo.CurrentCulture) + "%";
            _trayIcon.Text = text.Length > 63 ? text.Substring(0, 63) : text;
            ToolStripItem status = _trayIcon.ContextMenuStrip.Items["status"];
            if (status != null) status.Text = state + " • RAM " + snapshot.Memory.PercentUsed.ToString("0", CultureInfo.CurrentCulture) + "% • CPU " + snapshot.CpuPercent.ToString("0", CultureInfo.CurrentCulture) + "%";
        }

        private void ShowGuardAlertIfNeeded(GuardAlert alert)
        {
            if (alert == null || alert.Kind == GuardAlertKind.None || !_guardSettings.Notifications || _meetingActive) return;
            if (_guardSettings.SilentUntilUtc > DateTime.UtcNow) return;
            if (_guardSettings.SilenceFullscreen && SystemInfo.IsForegroundWindowFullScreen()) return;
            if (DateTime.UtcNow - _lastAlertUtc < TimeSpan.FromMinutes(10)) return;
            if (alert.Kind == _lastAlertKind && DateTime.UtcNow - _lastAlertUtc < TimeSpan.FromMinutes(30)) return;
            _lastAlertKind = alert.Kind;
            _lastAlertUtc = DateTime.UtcNow;
            string nextStep = alert.Kind == GuardAlertKind.LowDisk
                ? " Clique para ver a limpeza segura recomendada."
                : " Clique para ver a recomendação do Neck.";
            _trayIcon.ShowBalloonTip(6000, alert.Title, alert.Message + nextStep,
                alert.Kind == GuardAlertKind.LowDisk || alert.Kind == GuardAlertKind.CpuPressure ? ToolTipIcon.Warning : ToolTipIcon.Info);
        }

        private void ShowRecoveryNotification()
        {
            if (!_guardSettings.Notifications || _meetingActive || _guardSettings.SilentUntilUtc > DateTime.UtcNow) return;
            if (_guardSettings.SilenceFullscreen && SystemInfo.IsForegroundWindowFullScreen()) return;
            _trayIcon.ShowBalloonTip(4000, "Fluxo normalizado", "O monitor inteligente confirmou que a pressão voltou ao nível normal.", ToolTipIcon.Info);
        }

        internal void ForceCloseForTesting()
        {
            _allowExit = true;
            Close();
        }

        private Control BuildActivityCard()
        {
            Panel card = MakeCard(new Padding(20));
            card.Margin = new Padding(10, 0, 0, 0);
            Label heading = new Label
            {
                Text = "Atividade",
                Dock = DockStyle.Top,
                Height = 32,
                Font = Theme.Heading,
                ForeColor = Theme.Text
            };
            _progress.Dock = DockStyle.Bottom;
            _progress.Height = 6;
            _progress.Running = false;
            _log.Dock = DockStyle.Fill;
            _log.BorderStyle = BorderStyle.None;
            _log.BackColor = Color.White;
            _log.ForeColor = Theme.Muted;
            _log.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            _log.ReadOnly = true;
            _log.DetectUrls = false;
            _log.Text = "Tudo pronto. A análise não remove nenhum arquivo.\n";
            card.Controls.Add(_log);
            card.Controls.Add(heading);
            card.Controls.Add(_progress);
            return card;
        }

        private static RoundedPanel MakeCard(Padding padding)
        {
            RoundedPanel panel = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Card,
                Padding = padding,
                OutlineColor = Theme.Border,
                CornerRadius = 18
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
            value.Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold);
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
            button.UseVisualStyleBackColor = false;
            AnimatedButton animated = button as AnimatedButton;
            if (animated != null) animated.SetPalette(color);
            button.Resize += delegate
            {
                if (button.Width < 4 || button.Height < 4) return;
                using (GraphicsPath path = RoundedRectangle(new Rectangle(0, 0, button.Width, button.Height), 10))
                    button.Region = new Region(path);
            };
        }

        private static Label CreateBadge(string text, Color background, Color foreground)
        {
            Label badge = new Label
            {
                Text = text,
                AutoSize = false,
                Size = new Size(118, 26),
                BackColor = background,
                ForeColor = foreground,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold)
            };
            return badge;
        }

        private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
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

        private async Task ShowAdvancedAndRunAsync()
        {
            using (MaintenanceOptionsForm form = new MaintenanceOptionsForm(
                _tempCheck.Checked, _reportsCheck.Checked, _recycleCheck.Checked,
                _componentsCheck.Checked, _healthCheck.Checked, _drivesCheck.Checked))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                _tempCheck.Checked = form.CleanTemp;
                _reportsCheck.Checked = form.CleanReports;
                _recycleCheck.Checked = form.EmptyRecycleBin;
                _componentsCheck.Checked = form.CleanComponents;
                _healthCheck.Checked = form.CheckHealth;
                _drivesCheck.Checked = form.OptimizeDrive;
            }
            await RunMaintenanceAsync();
        }

        private void UpdateSystemStatus()
        {
            if (_meetingActive && DateTime.Now >= _meetingEndsAt) DeactivateMeetingMode("O tempo escolhido terminou.");

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

            if ((_healthSnapshot == null || (!_replayCapturing && DateTime.UtcNow - _lastReplayCaptureUtc >= TimeSpan.FromSeconds(30))) &&
                (DateTime.Now - _lastHealthScan).TotalSeconds >= 10)
            {
                _lastHealthScan = DateTime.Now;
                _healthSnapshot = SystemInfo.GetHealthSnapshot();
                UpdateGuardView(_healthSnapshot);
            }
        }

        private void UpdateGuardView(HealthSnapshot snapshot)
        {
            if (snapshot == null) return;
            UpdateFlowScore();
            if (_meetingActive)
            {
                UpdateMeetingDisplay();
                return;
            }
            if (snapshot.Level == HealthLevel.Critical)
            {
                _guardBadge.Text = "●  Gargalo agora";
                _guardBadge.ForeColor = Theme.Coral;
                _heroTitle.Text = "Tem um gargalo pedindo espaço";
                _guardButton.Text = "Destravar agora";
            }
            else if (snapshot.Level == HealthLevel.Warning)
            {
                _guardBadge.Text = "●  Fluxo sob pressão";
                _guardBadge.ForeColor = Theme.Amber;
                _heroTitle.Text = "O fluxo está mais apertado";
                _guardButton.Text = "Dar prioridade a um app";
            }
            else
            {
                _guardBadge.Text = "●  Fluxo livre";
                _guardBadge.ForeColor = Theme.Green;
                _heroTitle.Text = "Tudo está passando bem";
                _guardButton.Text = "Dar prioridade a um app";
            }
            _guardBadge.Width = 220;
            _guardBadge.BackColor = Color.Transparent;
            _guardButton.SetPalette(Theme.Lime);
            _guardButton.ForeColor = Theme.Ink;
            _flowIndicator.SetLevel(snapshot.Level);
            _guardButton.AttentionPulse = snapshot.Level == HealthLevel.Critical && !FocusModeManager.IsActive && Visible && WindowState != FormWindowState.Minimized;

            string turbo = FocusModeManager.IsActive
                ? " Acelerando " + FocusModeManager.ActiveDisplayName + " por mais " + Math.Max(1, (int)Math.Ceiling(FocusModeManager.Remaining.TotalMinutes)) + " min."
                : string.Empty;
            if (FocusShieldManager.ActiveCount > 0)
                turbo += " Escudo de Foco ativo contra " + FocusShieldManager.ActiveCount + " concorrente(s).";
            _guardMessage.Text = snapshot.Summary + turbo;
            ResourceProcess top = snapshot.TopProcesses.FirstOrDefault();
            string adaptive = EfficiencyModeManager.ActiveCount > 0
                ? "  •  Economizando: " + EfficiencyModeManager.ActiveCount + " app(s)"
                : string.Empty;
            _guardProcess.Text = (top == null
                ? "Nenhum processo pôde ser analisado."
                : "Maior uso: " + top.DisplayName + "  •  " + FormatBytes(top.MemoryBytes)) + adaptive;

            _currentAdvice = BottleneckAdvisor.Analyze(snapshot);
            ReplayIncident replay = _replayEngine.GetLatestIncident();
            _showReplayAsPrimary = replay != null && !replay.Ongoing && replay.EndedUtc != DateTime.MinValue &&
                DateTime.UtcNow - replay.EndedUtc <= TimeSpan.FromMinutes(30) && snapshot.Level == HealthLevel.Stable;
            _showAutopilotAsPrimary = !_showReplayAsPrimary && _guardSettings.AutopilotEnabled && _autopilotDecision != null &&
                (_autopilotDecision.State == AutopilotState.Protecting ||
                 (_autopilotDecision.State == AutopilotState.Watching && _currentAdvice.Kind == BottleneckKind.None));
            if (_showReplayAsPrimary)
            {
                _actionTitle.Text = "O fluxo voltou ao normal";
                _actionHelp.Text = "O Replay registrou a causa provável às " + replay.PeakUtc.ToLocalTime().ToString("HH:mm") + ".";
                _guardButton.Text = "Ver o que aconteceu";
            }
            else if (_showAutopilotAsPrimary)
            {
                _actionTitle.Text = _autopilotDecision.Title;
                _actionHelp.Text = _autopilotDecision.Explanation;
                _guardButton.Text = _autopilotDecision.State == AutopilotState.Protecting ? "Ver proteção" : "Ver previsão";
                if (_autopilotDecision.State == AutopilotState.Protecting && snapshot.Level != HealthLevel.Critical)
                {
                    _guardBadge.Text = "●  Autopilot cuidando";
                    _guardBadge.Width = 220;
                    _guardBadge.BackColor = Color.Transparent;
                    _guardBadge.ForeColor = Theme.Green;
                    _heroTitle.Text = "O Neck abriu espaço sozinho";
                }
            }
            else
            {
                bool showPersonalizedInsight = _currentAdvice.Kind == BottleneckKind.None &&
                    _baselineEvaluation != null && _baselineEvaluation.State == BaselineState.Personalized;
                _actionTitle.Text = showPersonalizedInsight ? _baselineEvaluation.Title : _currentAdvice.Title;
                _actionHelp.Text = showPersonalizedInsight ? _baselineEvaluation.Explanation : _currentAdvice.Explanation;
                _guardButton.Text = _currentAdvice.ActionText;
            }
        }

        private void UpdateFlowScore()
        {
            BaselineEvaluation evaluation = _baselineEvaluation ?? new BaselineEvaluation();
            if (evaluation.State == BaselineState.Learning)
            {
                _flowScoreValue.Text = evaluation.LearningPercent + "% aprendido";
                _flowScoreValue.ForeColor = Theme.Blue;
            }
            else
            {
                _flowScoreValue.Text = evaluation.Score + " / 100";
                _flowScoreValue.ForeColor = evaluation.Score >= 85 ? Theme.Green : evaluation.Score >= 60 ? Theme.Amber : Color.Firebrick;
            }
        }

        private void ToggleMeetingMode()
        {
            if (_meetingActive)
            {
                DeactivateMeetingMode("Encerrado por você.");
                return;
            }

            MeetingPreflight preflight = SystemInfo.GetMeetingPreflight();
            using (MeetingModeForm form = new MeetingModeForm(preflight))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                ActivateMeetingMode(form.DurationMinutes);
            }
        }

        private void ActivateMeetingMode(int durationMinutes)
        {
            uint state = NativeMethods.SetThreadExecutionState(
                NativeMethods.ES_CONTINUOUS | NativeMethods.ES_SYSTEM_REQUIRED | NativeMethods.ES_DISPLAY_REQUIRED);
            if (state == 0)
            {
                MessageBox.Show("O Windows não permitiu ativar a proteção contra suspensão.", "Neck", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _meetingActive = true;
            FocusShieldManager.SetSuspended(true);
            _meetingEndsAt = DateTime.Now.AddMinutes(Math.Max(15, durationMinutes));
            _analyzeButton.Enabled = false;
            _runButton.Enabled = false;
            _advancedButton.Enabled = false;
            _bootButton.Enabled = false;
            UpdateMeetingDisplay();
            AppendLog("MODO REUNIÃO — ativo até " + _meetingEndsAt.ToString("HH:mm") + ". Suspensão e tela apagada foram bloqueadas.");
        }

        private void UpdateMeetingDisplay()
        {
            TimeSpan remaining = _meetingEndsAt - DateTime.Now;
            int minutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
            _guardBadge.Text = "●  Reunião em fluxo";
            _guardBadge.Width = 220;
            _guardBadge.BackColor = Color.Transparent;
            _guardBadge.ForeColor = Theme.Green;
            _heroTitle.Text = "Sua apresentação está protegida";
            _guardMessage.Text = "Reunião protegida por mais " + minutes + " min. Manutenções estão pausadas.";
            _guardProcess.Text = "A tela e o computador não entrarão em suspensão.";
            _flowIndicator.SetLevel(HealthLevel.Stable);
            _meetingButton.Text = "Encerrar modo";
            _meetingButton.BackColor = Theme.Cyan;
            _recommendation.Text = "MODO REUNIÃO  •  até " + _meetingEndsAt.ToString("HH:mm");
            _recommendation.BackColor = Color.White;
            _recommendation.ForeColor = Theme.Cyan;
        }

        private void DeactivateMeetingMode(string reason)
        {
            NativeMethods.SetThreadExecutionState(NativeMethods.ES_CONTINUOUS);
            _meetingActive = false;
            FocusShieldManager.SetSuspended(false);
            _meetingButton.Text = "Modo reunião";
            _meetingButton.BackColor = Theme.Blue;
            _analyzeButton.Enabled = !_busy;
            _runButton.Enabled = !_busy;
            _advancedButton.Enabled = !_busy;
            _bootButton.Enabled = !_busy;
            _recommendation.BackColor = Color.White;
            _recommendation.ForeColor = Theme.Green;
            LoadLastRun();
            UpdateGuardView(_healthSnapshot);
            AppendLog("MODO REUNIÃO — desativado. " + reason);
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
                if (_closing || IsDisposed) return;
                _analyzedBytes = result.TotalBytes;
                _analysisValue.Text = FormatBytes(result.TotalBytes);
                _analysisValue.ForeColor = result.TotalBytes > 512L * 1024 * 1024 ? Theme.Green : Theme.Text;
                _runButton.Text = result.TotalBytes > 0 ? "Limpar " + FormatBytes(result.TotalBytes) : "Limpeza rápida";

                AppendLog("Temporários seguros: " + FormatBytes(result.TempBytes) + " em " + result.TempFiles + " arquivos");
                AppendLog("Relatórios de erro: " + FormatBytes(result.ReportBytes) + " em " + result.ReportFiles + " arquivos");
                AppendLog("Estimativa total: " + FormatBytes(result.TotalBytes));
                if (result.AccessErrors > 0) AppendLog("Itens protegidos ignorados: " + result.AccessErrors);
                AppendLog("Análise concluída. Nada foi apagado.");
                SupportDiagnostics.RecordEvent("Análise", "Análise segura concluída; nada foi apagado. Itens protegidos ignorados: " + result.AccessErrors + ".");
            }
            catch (Exception ex)
            {
                if (_closing || IsDisposed) return;
                AppendLog("Falha na análise: " + ex.Message);
                SupportDiagnostics.RecordException("Análise segura", ex);
            }
            finally
            {
                if (!_closing && !IsDisposed) SetBusy(false, null);
            }
        }

        private async Task RunMaintenanceAsync()
        {
            if (_busy) return;
            if (_meetingActive)
            {
                MessageBox.Show("Encerre o Modo Reunião antes de iniciar uma manutenção.", "Neck", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            List<string> selected = GetSelectedTasks();
            if (selected.Count == 0)
            {
                MessageBox.Show("Marque pelo menos uma opção.", "Neck", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string warning = "Serão executadas estas tarefas:\n\n• " + string.Join("\n• ", selected) +
                             "\n\nDocumentos, fotos, senhas e arquivos pessoais não serão tocados.";
            if (_recycleCheck.Checked) warning += "\n\nAtenção: o conteúdo da Lixeira será apagado definitivamente.";
            if (_componentsCheck.Checked || _healthCheck.Checked || _drivesCheck.Checked)
                warning += "\n\nO Windows solicitará permissão de administrador somente para as tarefas de sistema.";

            if (MessageBox.Show(warning, "Confirmar manutenção", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK)
                return;

            SetBusy(true, "Executando manutenção...");
            _maintenanceRunning = true;
            SupportDiagnostics.RecordEvent("Manutenção", "Manutenção iniciada com " + selected.Count + " tarefa(s) confirmada(s).");
            _log.Clear();
            AppendLog("MANUTENÇÃO — " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
            StringBuilder report = new StringBuilder();
            report.AppendLine("Neck — Relatório de manutenção");
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

                List<string> elevatedTasks = new List<string>();
                if (_componentsCheck.Checked) elevatedTasks.Add("components");
                if (_healthCheck.Checked) elevatedTasks.Add("health");
                if (_drivesCheck.Checked) elevatedTasks.Add("drives");
                if (elevatedTasks.Count > 0)
                {
                    AppendLog("Solicitando permissão para as tarefas de sistema...");
                    ElevatedTaskResult elevated = await ElevatedOperations.RunAsync(elevatedTasks);
                    if (elevated.Cancelled) throw new InvalidOperationException("A permissão de administrador foi cancelada. As tarefas comuns já concluídas não foram desfeitas.");
                    report.AppendLine("TAREFAS ADMINISTRATIVAS");
                    report.AppendLine(elevated.Output);
                    if (elevated.ExitCode != 0) throw new InvalidOperationException("Uma tarefa administrativa retornou o código " + elevated.ExitCode + ". Consulte os detalhes no relatório.");
                    AppendLog("Tarefas administrativas concluídas.");
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

                string completion = "Manutenção concluída. Espaço liberado: " + FormatBytes(freed) + ". O relatório foi salvo em Documentos.";
                if (Visible)
                    MessageBox.Show(completion, "Neck", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                    _trayIcon.ShowBalloonTip(6000, "Manutenção concluída", completion, ToolTipIcon.Info);
                SupportDiagnostics.RecordEvent("Manutenção", "Manutenção concluída. Espaço liberado diretamente: " + FormatBytes(freed) + ".");
            }
            catch (Exception ex)
            {
                AppendLog("A manutenção foi interrompida: " + ex.Message);
                SupportDiagnostics.RecordException("Manutenção", ex);
                try
                {
                    Directory.CreateDirectory(ReportDirectory);
                    report.AppendLine(new string('-', 64));
                    report.AppendLine("MANUTENÇÃO INTERROMPIDA");
                    report.AppendLine(ex.Message);
                    string partialReport = Path.Combine(ReportDirectory, "manutencao-interrompida-" + DateTime.Now.ToString("yyyy-MM-dd-HHmmss") + ".txt");
                    File.WriteAllText(partialReport, report.ToString(), new UTF8Encoding(true));
                    AppendLog("Relatório parcial salvo em: " + partialReport);
                }
                catch { }
                string failure = "A manutenção foi interrompida. Abra o Neck para consultar os detalhes. " + ex.Message;
                if (Visible)
                    MessageBox.Show(failure, "Neck", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                else
                    _trayIcon.ShowBalloonTip(6000, "Manutenção interrompida", failure, ToolTipIcon.Error);
            }
            finally
            {
                _maintenanceRunning = false;
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
            if (_closing || IsDisposed || Disposing) return;
            _busy = busy;
            _analyzeButton.Enabled = !busy && !_meetingActive;
            _runButton.Enabled = !busy && !_meetingActive;
            _advancedButton.Enabled = !busy && !_meetingActive;
            _bootButton.Enabled = !busy && !_meetingActive;
            _driversButton.Enabled = !busy;
            _settingsButton.Enabled = !busy;
            _planButton.Enabled = !busy;
            _guardButton.Enabled = !busy;
            _meetingButton.Enabled = !busy;
            _toolsButton.Enabled = !busy;
            _progress.Running = busy;
            _progress.Visible = busy;
            if (!string.IsNullOrEmpty(status)) AppendLog(status);
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private void AppendLog(string line)
        {
            if (_closing || IsDisposed || Disposing || _log.IsDisposed) return;
            if (InvokeRequired)
            {
                try
                {
                    if (IsHandleCreated) BeginInvoke(new Action<string>(AppendLog), line);
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
                return;
            }
            try
            {
                _log.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + line + Environment.NewLine);
                _log.SelectionStart = _log.TextLength;
                _log.ScrollToCaret();
                _activityStatus.Text = line;
            }
            catch (ObjectDisposedException) { }
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
            catch (Exception ex) { MessageBox.Show("Não foi possível abrir:\n" + target + "\n\n" + ex.Message, "Neck", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }

    internal enum ToolHubChoice
    {
        None,
        Plan,
        Meeting,
        Startup,
        Diagnostic,
        Drivers,
        Bluetooth,
        Replay,
        History,
        Support,
        Preferences
    }

    internal sealed class ToolsHubForm : Form
    {
        private readonly CheckBox _continueInTray = new CheckBox();
        private readonly bool _meetingActive;

        public ToolHubChoice SelectedChoice { get; private set; }
        public bool ContinueInTray { get { return _continueInTray.Checked; } }

        public ToolsHubForm(bool continueInTray, bool meetingActive, string activity)
        {
            _meetingActive = meetingActive;
            Text = "Mais ferramentas — Neck";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(850, 650);
            MinimumSize = new Size(780, 610);
            BackColor = Theme.Background;
            Font = Theme.Body;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            SelectedChoice = ToolHubChoice.None;
            BuildInterface(continueInTray, activity);
            Shown += delegate { VisualEffects.FadeIn(this); };
        }

        private void BuildInterface(bool continueInTray, string activity)
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 104, BackColor = Theme.Card, Padding = new Padding(30, 16, 30, 12) };
            FlowMark mark = new FlowMark { Size = new Size(42, 42), Location = new Point(28, 26) };
            header.Controls.Add(new Label
            {
                Text = "Ferramentas",
                AutoSize = true,
                Font = new Font("Segoe UI Variable Display", 21f, FontStyle.Bold),
                ForeColor = Theme.Ink,
                Location = new Point(88, 15)
            });
            header.Controls.Add(new Label
            {
                Text = "O que você usa menos fica aqui, sem poluir a tela inicial.",
                AutoSize = true,
                Font = Theme.Body,
                ForeColor = Theme.Muted,
                Location = new Point(91, 58)
            });
            header.Controls.Add(mark);
            header.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen line = new Pen(Theme.Hairline))
                    e.Graphics.DrawLine(line, 0, header.ClientSize.Height - 1, header.ClientSize.Width, header.ClientSize.Height - 1);
            };

            TableLayoutPanel grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 18, 24, 12),
                ColumnCount = 2,
                RowCount = 4,
                BackColor = Theme.Background
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int row = 0; row < 4; row++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 25f));
            grid.Controls.Add(CreateTool("✦", "Meu plano", "Três prioridades escolhidas para este computador.", ToolHubChoice.Plan), 0, 0);
            grid.Controls.Add(CreateTool("⌁", "Cura Bluetooth", "Restaura a conexão sem apagar pareamentos.", ToolHubChoice.Bluetooth), 1, 0);
            grid.Controls.Add(CreateTool("◷", _meetingActive ? "Encerrar modo reunião" : "Modo reunião", "Evita suspensão durante chamadas e apresentações.", ToolHubChoice.Meeting), 0, 1);
            grid.Controls.Add(CreateTool("↗", "Inicialização", "Veja o que abre junto com o Windows.", ToolHubChoice.Startup), 1, 1);
            grid.Controls.Add(CreateTool("▦", "Diagnóstico", "Detalhes de CPU, memória e armazenamento.", ToolHubChoice.Diagnostic), 0, 2);
            grid.Controls.Add(CreateTool("↓", "Drivers", "Confira atualizações pelas fontes oficiais.", ToolHubChoice.Drivers), 1, 2);
            grid.Controls.Add(CreateTool("↶", "Neck Replay", "Entenda por que o computador acabou de travar.", ToolHubChoice.Replay), 0, 3);
            grid.Controls.Add(CreateTool("?", "Suporte", "Crie um relatório privado e recupere interrupções.", ToolHubChoice.Support), 1, 3);

            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 82, BackColor = Theme.Card, Padding = new Padding(28, 12, 28, 12) };
            _continueInTray.Text = "Continuar acompanhando ao fechar";
            _continueInTray.Checked = continueInTray;
            _continueInTray.AutoSize = true;
            _continueInTray.Font = Theme.Small;
            _continueInTray.ForeColor = Theme.Muted;
            _continueInTray.Location = new Point(29, 15);
            LinkLabel history = new LinkLabel
            {
                Text = "Histórico",
                AutoSize = true,
                Font = Theme.Small,
                LinkColor = Theme.Blue,
                Location = new Point(425, 17)
            };
            history.Click += delegate { Choose(ToolHubChoice.History); };
            Button preferences = new AnimatedButton();
            ConfigureToolButton(preferences, "Preferências", Theme.FlowSoft, 130);
            preferences.ForeColor = Theme.Ink;
            preferences.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            preferences.Location = new Point(footer.Width - 286, 20);
            preferences.Click += delegate { Choose(ToolHubChoice.Preferences); };
            Button close = new AnimatedButton();
            ConfigureToolButton(close, "Voltar", Theme.Lime, 120);
            close.ForeColor = Theme.Ink;
            close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            close.Location = new Point(footer.Width - 146, 20);
            close.Click += delegate { Close(); };
            footer.Resize += delegate
            {
                close.Left = footer.ClientSize.Width - close.Width - 24;
                preferences.Left = close.Left - preferences.Width - 10;
            };
            string status = string.IsNullOrWhiteSpace(activity) ? "Tudo pronto." : activity;
            ToolTip tip = new ToolTip();
            tip.SetToolTip(_continueInTray, status);
            footer.Controls.Add(_continueInTray);
            footer.Controls.Add(history);
            footer.Controls.Add(preferences);
            footer.Controls.Add(close);

            Controls.Add(grid);
            Controls.Add(footer);
            Controls.Add(header);
            AcceptButton = close;
            CancelButton = close;
        }

        private Control CreateTool(string glyph, string title, string description, ToolHubChoice choice)
        {
            RoundedPanel card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(7),
                BackColor = Theme.Card,
                OutlineColor = Theme.Hairline,
                CornerRadius = 18,
                Cursor = Cursors.Hand
            };
            Label heading = new Label
            {
                Text = title,
                AutoSize = false,
                Height = 42,
                Dock = DockStyle.Top,
                Padding = new Padding(70, 14, 16, 0),
                Font = new Font("Segoe UI Variable Display", 13f, FontStyle.Bold),
                ForeColor = Theme.Text,
                Cursor = Cursors.Hand
            };
            Label detail = new Label
            {
                Text = description,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Padding = new Padding(70, 7, 20, 12),
                Font = Theme.Small,
                ForeColor = Theme.Muted,
                Cursor = Cursors.Hand
            };
            Label icon = new Label
            {
                Text = glyph,
                AutoSize = false,
                Size = new Size(40, 40),
                Location = new Point(18, 22),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Symbol", 15f, FontStyle.Bold),
                ForeColor = Theme.Ink,
                BackColor = Theme.FlowSoft,
                Cursor = Cursors.Hand
            };
            EventHandler select = delegate { Choose(choice); };
            EventHandler enter = delegate
            {
                card.BackColor = Theme.FlowSoft;
                card.Invalidate();
            };
            EventHandler leave = delegate
            {
                Point pointer = card.PointToClient(Cursor.Position);
                if (card.ClientRectangle.Contains(pointer)) return;
                card.BackColor = Theme.Card;
                card.Invalidate();
            };
            card.Click += select;
            heading.Click += select;
            detail.Click += select;
            icon.Click += select;
            card.MouseEnter += enter;
            heading.MouseEnter += enter;
            detail.MouseEnter += enter;
            icon.MouseEnter += enter;
            card.MouseLeave += leave;
            heading.MouseLeave += leave;
            detail.MouseLeave += leave;
            icon.MouseLeave += leave;
            card.Controls.Add(detail);
            card.Controls.Add(heading);
            card.Controls.Add(icon);
            icon.BringToFront();
            return card;
        }

        private void Choose(ToolHubChoice choice)
        {
            SelectedChoice = choice;
            DialogResult = DialogResult.OK;
            Close();
        }

        private static void ConfigureToolButton(Button button, string text, Color color, int width)
        {
            button.Text = text;
            button.Size = new Size(width, 42);
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

    internal sealed class MeetingModeForm : Form
    {
        private readonly MeetingPreflight _preflight;
        private readonly ComboBox _duration = new ComboBox();

        public int DurationMinutes
        {
            get
            {
                if (_duration.SelectedIndex == 0) return 30;
                if (_duration.SelectedIndex == 2) return 120;
                return 60;
            }
        }

        public MeetingModeForm(MeetingPreflight preflight)
        {
            _preflight = preflight ?? new MeetingPreflight();
            Text = "Modo Reunião — Neck";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(820, 700);
            MinimumSize = new Size(760, 650);
            BackColor = Theme.Background;
            Font = Theme.Body;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            BuildInterface();
        }

        private void BuildInterface()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 108, BackColor = Theme.Card };
            header.Controls.Add(new Label
            {
                Text = "Modo Reunião",
                AutoSize = true,
                Font = new Font("Segoe UI Variable Display", 21f, FontStyle.Bold),
                ForeColor = Theme.Ink,
                Location = new Point(28, 18)
            });
            header.Controls.Add(new Label
            {
                Text = "Prepare o computador antes de compartilhar sua tela",
                AutoSize = true,
                Font = Theme.Body,
                ForeColor = Theme.Muted,
                Location = new Point(31, 67)
            });

            TableLayoutPanel body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 20, 24, 20),
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Theme.Background
            };
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));

            RoundedPanel summary = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                OutlineColor = Theme.Border,
                CornerRadius = 16,
                Margin = new Padding(0, 0, 0, 12)
            };
            int warnings = _preflight.Checks.Count(item => item.Status != MeetingCheckStatus.Ready);
            Color summaryColor = warnings == 0 ? Theme.Green : warnings >= 2 ? Theme.Amber : Theme.Blue;
            summary.Controls.Add(new Label
            {
                Text = warnings == 0 ? "PRONTO" : warnings + " ALERTA" + (warnings == 1 ? "" : "S"),
                AutoSize = false,
                Size = new Size(128, 31),
                BackColor = warnings == 0 ? Theme.GreenSoft : Color.FromArgb(255, 247, 237),
                ForeColor = summaryColor,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
                Location = new Point(20, 20)
            });
            summary.Controls.Add(new Label
            {
                Text = warnings == 0 ? "Seu computador está pronto para apresentar." : "Revise os alertas antes de começar.",
                AutoSize = true,
                Font = Theme.Heading,
                ForeColor = Theme.Text,
                Location = new Point(166, 20)
            });
            summary.Controls.Add(new Label
            {
                Text = "Ao ativar, o Neck impede suspensão e tela apagada e pausa suas próprias manutenções.",
                AutoSize = false,
                Size = new Size(660, 37),
                Font = Theme.Small,
                ForeColor = Theme.Muted,
                Location = new Point(22, 64)
            });

            ListView checklist = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                ForeColor = Theme.Text,
                Font = Theme.Body,
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };
            checklist.Columns.Add("Estado", 90, HorizontalAlignment.Center);
            checklist.Columns.Add("Verificação", 185);
            checklist.Columns.Add("Resultado", 425);
            foreach (MeetingCheck check in _preflight.Checks)
            {
                string state = check.Status == MeetingCheckStatus.Ready ? "PRONTO" : check.Status == MeetingCheckStatus.Warning ? "ATENÇÃO" : "RISCO";
                ListViewItem item = new ListViewItem(state);
                item.SubItems.Add(check.Title);
                item.SubItems.Add(check.Message);
                item.ForeColor = check.Status == MeetingCheckStatus.Ready ? Theme.Green :
                                     check.Status == MeetingCheckStatus.Warning ? Theme.Amber : Color.Firebrick;
                checklist.Items.Add(item);
            }

            Panel durationPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
            durationPanel.Controls.Add(new Label
            {
                Text = "Duração da proteção",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                ForeColor = Theme.Text,
                Location = new Point(4, 13)
            });
            _duration.DropDownStyle = ComboBoxStyle.DropDownList;
            _duration.Items.AddRange(new object[] { "30 minutos", "1 hora", "2 horas" });
            _duration.SelectedIndex = 1;
            _duration.Size = new Size(180, 32);
            _duration.Location = new Point(190, 9);
            durationPanel.Controls.Add(_duration);

            FlowLayoutPanel footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 10, 0, 0)
            };
            Button activate = new Button { Text = "Ativar Modo Reunião", DialogResult = DialogResult.OK };
            Button cancel = new Button { Text = "Agora não", DialogResult = DialogResult.Cancel };
            ConfigureMeetingButton(activate, Theme.Blue, 190);
            ConfigureMeetingButton(cancel, Theme.NavySoft, 120);
            footer.Controls.Add(activate);
            footer.Controls.Add(cancel);
            AcceptButton = activate;
            CancelButton = cancel;

            body.Controls.Add(summary, 0, 0);
            body.Controls.Add(checklist, 0, 1);
            body.Controls.Add(durationPanel, 0, 2);
            body.Controls.Add(footer, 0, 3);
            Controls.Add(body);
            Controls.Add(header);
        }

        private static void ConfigureMeetingButton(Button button, Color color, int width)
        {
            button.Width = width;
            button.Height = 42;
            button.BackColor = color;
            button.ForeColor = Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
            button.Margin = new Padding(10, 0, 0, 0);
            button.Cursor = Cursors.Hand;
        }
    }

    internal sealed class DiagnosticForm : Form
    {
        private readonly HealthSnapshot _snapshot;

        public DiagnosticForm(HealthSnapshot snapshot)
        {
            _snapshot = snapshot ?? new HealthSnapshot();
            Text = "Diagnóstico inteligente — Neck Guard";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(800, 650);
            MinimumSize = new Size(740, 600);
            BackColor = Theme.Background;
            Font = Theme.Body;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            BuildInterface();
        }

        private void BuildInterface()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 104, BackColor = Theme.Card };
            header.Controls.Add(new Label
            {
                Text = "Neck Guard",
                AutoSize = true,
                Font = new Font("Segoe UI Variable Display", 21f, FontStyle.Bold),
                ForeColor = Theme.Ink,
                Location = new Point(28, 18)
            });
            header.Controls.Add(new Label
            {
                Text = "O que está pressionando seu computador agora",
                AutoSize = true,
                Font = Theme.Body,
                ForeColor = Theme.Muted,
                Location = new Point(31, 65)
            });

            TableLayoutPanel body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 20, 24, 20),
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Theme.Background
            };
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));

            RoundedPanel summary = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                OutlineColor = Theme.Border,
                CornerRadius = 16,
                Padding = new Padding(20),
                Margin = new Padding(0, 0, 0, 12)
            };
            Color levelColor = _snapshot.Level == HealthLevel.Critical ? Color.Firebrick :
                               _snapshot.Level == HealthLevel.Warning ? Theme.Amber : Theme.Green;
            Label score = new Label
            {
                Text = _snapshot.Score.ToString(CultureInfo.CurrentCulture),
                AutoSize = false,
                Size = new Size(92, 72),
                Location = new Point(18, 21),
                Font = new Font("Segoe UI Semibold", 30f, FontStyle.Bold),
                ForeColor = levelColor,
                TextAlign = ContentAlignment.MiddleCenter
            };
            summary.Controls.Add(score);
            summary.Controls.Add(new Label
            {
                Text = _snapshot.Title,
                AutoSize = true,
                Font = Theme.Heading,
                ForeColor = Theme.Text,
                Location = new Point(126, 22)
            });
            summary.Controls.Add(new Label
            {
                Text = _snapshot.Summary,
                AutoSize = false,
                Size = new Size(565, 55),
                Font = Theme.Body,
                ForeColor = Theme.Muted,
                Location = new Point(128, 54)
            });

            Label metrics = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Memória em uso: " + _snapshot.Memory.PercentUsed.ToString("0", CultureInfo.CurrentCulture) + "%" +
                       "     •     Disponível: " + MainForm.FormatBytes((long)_snapshot.Memory.AvailableBytes) +
                       "     •     Disco livre: " + MainForm.FormatBytes(_snapshot.DiskFreeBytes),
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                ForeColor = Theme.Text,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 0, 0)
            };

            ListView processes = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                ForeColor = Theme.Text,
                Font = Theme.Body,
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };
            processes.Columns.Add("Aplicativo", 390);
            processes.Columns.Add("Processos", 110, HorizontalAlignment.Center);
            processes.Columns.Add("Memória", 150, HorizontalAlignment.Right);
            foreach (ResourceProcess process in _snapshot.TopProcesses)
            {
                ListViewItem item = new ListViewItem(process.DisplayName);
                item.SubItems.Add(process.ProcessCount.ToString(CultureInfo.CurrentCulture));
                item.SubItems.Add(MainForm.FormatBytes(process.MemoryBytes));
                processes.Items.Add(item);
            }

            FlowLayoutPanel footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 10, 0, 0)
            };
            Button close = new Button { Text = "Entendi", DialogResult = DialogResult.OK };
            close.Width = 130;
            close.Height = 42;
            close.BackColor = Theme.Blue;
            close.ForeColor = Color.White;
            close.FlatStyle = FlatStyle.Flat;
            close.FlatAppearance.BorderSize = 0;
            close.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
            close.Cursor = Cursors.Hand;
            footer.Controls.Add(close);
            footer.Controls.Add(new Label
            {
                Text = "Somente leitura: o Neck não encerrou nem alterou nenhum aplicativo.",
                AutoSize = false,
                Width = 510,
                Height = 42,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Theme.Muted,
                Font = Theme.Small
            });
            AcceptButton = close;

            body.Controls.Add(summary, 0, 0);
            body.Controls.Add(metrics, 0, 1);
            body.Controls.Add(processes, 0, 2);
            body.Controls.Add(footer, 0, 3);
            Controls.Add(body);
            Controls.Add(header);
        }
    }

    internal sealed class MaintenanceOptionsForm : Form
    {
        private readonly CheckBox _temp = new CheckBox();
        private readonly CheckBox _reports = new CheckBox();
        private readonly CheckBox _recycle = new CheckBox();
        private readonly CheckBox _components = new CheckBox();
        private readonly CheckBox _health = new CheckBox();
        private readonly CheckBox _drives = new CheckBox();

        public bool CleanTemp { get { return _temp.Checked; } }
        public bool CleanReports { get { return _reports.Checked; } }
        public bool EmptyRecycleBin { get { return _recycle.Checked; } }
        public bool CleanComponents { get { return _components.Checked; } }
        public bool CheckHealth { get { return _health.Checked; } }
        public bool OptimizeDrive { get { return _drives.Checked; } }

        public MaintenanceOptionsForm(bool temp, bool reports, bool recycle, bool components, bool health, bool drives)
        {
            Text = "Manutenção completa — Neck";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(820, 720);
            MinimumSize = new Size(760, 680);
            BackColor = Theme.Background;
            Font = Theme.Body;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;

            _temp.Checked = temp;
            _reports.Checked = reports;
            _recycle.Checked = recycle;
            _components.Checked = components;
            _health.Checked = health;
            _drives.Checked = drives;
            BuildInterface();
        }

        private void BuildInterface()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 104, BackColor = Theme.Card };
            header.Controls.Add(new Label
            {
                Text = "O que você quer melhorar?",
                AutoSize = true,
                Font = new Font("Segoe UI Variable Display", 21f, FontStyle.Bold),
                ForeColor = Theme.Ink,
                Location = new Point(28, 20)
            });
            header.Controls.Add(new Label
            {
                Text = "As opções mais seguras já estão marcadas. Você revisará tudo antes de iniciar.",
                AutoSize = true,
                Font = Theme.Body,
                ForeColor = Theme.Muted,
                Location = new Point(31, 62)
            });

            TableLayoutPanel body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 20, 24, 20),
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Theme.Background
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));

            Panel safeCard = CreateOptionCard("Liberar espaço", "Arquivos que o computador não precisa mais");
            Panel systemCard = CreateOptionCard("Cuidar do Windows", "Verificações e otimização do sistema");
            safeCard.Margin = new Padding(0, 0, 10, 0);
            systemCard.Margin = new Padding(10, 0, 0, 0);

            FlowLayoutPanel safeList = OptionList();
            safeList.Controls.Add(CreateOptionRow(_temp, "Arquivos temporários", "Remove apenas arquivos antigos e preserva itens em uso.", false));
            safeList.Controls.Add(CreateOptionRow(_reports, "Relatórios de erro", "Apaga diagnósticos do Windows com mais de 14 dias.", false));
            safeList.Controls.Add(CreateOptionRow(_recycle, "Esvaziar a Lixeira", "Remove definitivamente o que já foi enviado para a Lixeira.", true));
            safeCard.Controls.Add(safeList);

            FlowLayoutPanel systemList = OptionList();
            systemList.Controls.Add(CreateOptionRow(_components, "Limpar atualizações antigas", "Remove versões do Windows que já foram substituídas.", false));
            systemList.Controls.Add(CreateOptionRow(_health, "Verificar o Windows", "Procura arquivos do sistema corrompidos ou inconsistentes.", false));
            systemList.Controls.Add(CreateOptionRow(_drives, "Otimizar armazenamento", "Escolhe automaticamente o cuidado correto para SSD ou HD.", false));
            systemCard.Controls.Add(systemList);

            FlowLayoutPanel footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 13, 0, 0)
            };
            Button run = new AnimatedButton { Text = "Revisar e iniciar", DialogResult = DialogResult.OK };
            Button cancel = new AnimatedButton { Text = "Voltar", DialogResult = DialogResult.Cancel };
            ConfigureDialogButton(run, Theme.Lime, 172);
            run.ForeColor = Theme.Ink;
            ConfigureDialogButton(cancel, Theme.FlowSoft, 110);
            cancel.ForeColor = Theme.Ink;
            footer.Controls.Add(run);
            footer.Controls.Add(cancel);
            AcceptButton = run;
            CancelButton = cancel;

            body.Controls.Add(safeCard, 0, 0);
            body.Controls.Add(systemCard, 1, 0);
            body.SetColumnSpan(footer, 2);
            body.Controls.Add(footer, 0, 1);
            Controls.Add(body);
            Controls.Add(header);
        }

        private static Panel CreateOptionCard(string title, string subtitle)
        {
            RoundedPanel card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                OutlineColor = Theme.Border,
                CornerRadius = 16,
                Padding = new Padding(16, 78, 16, 14)
            };
            card.Controls.Add(new Label
            {
                Text = title,
                AutoSize = true,
                Font = Theme.Heading,
                ForeColor = Theme.Text,
                Location = new Point(20, 19)
            });
            card.Controls.Add(new Label
            {
                Text = subtitle,
                AutoSize = true,
                Font = Theme.Small,
                ForeColor = Theme.Muted,
                Location = new Point(22, 51)
            });
            return card;
        }

        private static FlowLayoutPanel OptionList()
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = false,
                BackColor = Color.White
            };
        }

        private static Control CreateOptionRow(CheckBox box, string title, string description, bool warning)
        {
            RoundedPanel row = new RoundedPanel
            {
                Size = new Size(300, 100),
                Margin = new Padding(0, 0, 0, 8),
                BackColor = Color.White,
                OutlineColor = Theme.Border,
                CornerRadius = 12,
                Cursor = Cursors.Hand
            };
            box.Text = string.Empty;
            box.AutoSize = false;
            box.Size = new Size(24, 24);
            box.Location = new Point(17, 34);
            box.CheckAlign = ContentAlignment.MiddleCenter;
            box.Cursor = Cursors.Hand;
            Label heading = new Label
            {
                Text = title,
                AutoSize = false,
                Size = new Size(234, 25),
                Location = new Point(52, 15),
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                ForeColor = warning ? Theme.Amber : Theme.Text,
                Cursor = Cursors.Hand
            };
            Label detail = new Label
            {
                Text = description,
                AutoSize = false,
                Size = new Size(234, 48),
                Location = new Point(52, 42),
                Font = Theme.Small,
                ForeColor = Theme.Muted,
                Cursor = Cursors.Hand
            };
            Action refresh = delegate
            {
                row.BackColor = box.Checked ? Theme.FlowSoft : Color.White;
                row.OutlineColor = box.Checked ? VisualEffects.Blend(Theme.Cyan, Color.White, 0.25d) : Theme.Border;
                row.Invalidate();
            };
            EventHandler toggle = delegate { box.Checked = !box.Checked; };
            row.Click += toggle;
            heading.Click += toggle;
            detail.Click += toggle;
            box.CheckedChanged += delegate { refresh(); };
            row.Controls.Add(box);
            row.Controls.Add(heading);
            row.Controls.Add(detail);
            refresh();
            return row;
        }

        private static void ConfigureDialogButton(Button button, Color color, int width)
        {
            button.Width = width;
            button.Height = 42;
            button.BackColor = color;
            button.ForeColor = Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
            button.Margin = new Padding(10, 0, 0, 0);
            button.Cursor = Cursors.Hand;
            AnimatedButton animated = button as AnimatedButton;
            if (animated != null) animated.SetPalette(color);
        }
    }

    internal sealed class DriverCenterForm : Form
    {
        private readonly RichTextBox _versions = new RichTextBox();
        private readonly Button _restoreButton = new Button();
        private bool _closing;

        public DriverCenterForm()
        {
            Text = "Drivers e atualizações — Neck";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(820, 650);
            MinimumSize = new Size(760, 600);
            BackColor = Theme.Background;
            Font = Theme.Body;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            BuildInterface();
            Shown += async delegate { await LoadVersionsAsync(); };
            FormClosing += delegate { _closing = true; };
        }

        private void BuildInterface()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 102, BackColor = Theme.Card, Padding = new Padding(28, 20, 28, 16) };
            header.Controls.Add(new Label { Text = "Drivers e atualizações", AutoSize = true, Font = new Font("Segoe UI Variable Display", 21f, FontStyle.Bold), ForeColor = Theme.Ink, Location = new Point(27, 18) });
            header.Controls.Add(new Label { Text = "Somente fontes oficiais. Revise as atualizações antes de instalar.", AutoSize = true, Font = Theme.Body, ForeColor = Theme.Muted, Location = new Point(30, 61) });

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
                Width = 300,
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
            if (_closing || IsDisposed || _versions.IsDisposed) return;
            _versions.Text = string.IsNullOrWhiteSpace(result.Output) ? "Não foi possível consultar as versões instaladas." : result.Output.Trim();
        }

        private async Task CreateRestorePointAsync()
        {
            _restoreButton.Enabled = false;
            _restoreButton.Text = "Aguardando permissão...";
            try
            {
                ElevatedTaskResult result = await ElevatedOperations.RunAsync(new[] { "restorepoint" });
                if (_closing || IsDisposed) return;
                MessageBox.Show(result.ExitCode == 0 ? "Ponto de restauração criado." :
                    result.Cancelled ? "A permissão de administrador foi cancelada. Nenhuma alteração foi feita." :
                    "O Windows não criou o ponto de restauração. A Proteção do Sistema pode estar desativada.\n\n" + result.Output,
                    "Neck", MessageBoxButtons.OK, result.ExitCode == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            finally
            {
                if (!_closing && !IsDisposed && !_restoreButton.IsDisposed)
                {
                    _restoreButton.Enabled = true;
                    _restoreButton.Text = "Criar ponto de restauração";
                }
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

}
