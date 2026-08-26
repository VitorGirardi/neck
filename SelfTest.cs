using System;
using System.Threading;
using System.Windows.Forms;

namespace Neck
{
    internal static class SelfTest
    {
        private static int Main()
        {
            try
            {
                ScanResult result = Cleaner.Analyze();
                MemoryStatus memory = SystemInfo.GetMemoryStatus();
                System.Diagnostics.Stopwatch healthTimer = System.Diagnostics.Stopwatch.StartNew();
                HealthSnapshot health = SystemInfo.GetHealthSnapshot();
                healthTimer.Stop();
                if (health.Score < 0 || health.Score > 100) throw new InvalidOperationException("Pontuação de saúde inválida.");
                MeetingPreflight meeting = SystemInfo.GetMeetingPreflight();
                if (meeting.Checks.Count < 6) throw new InvalidOperationException("Checklist de reunião incompleto.");
                if (ElevatedOperations.ParseTasks("health,drives").Length != 2) throw new InvalidOperationException("Plano elevado válido foi recusado.");
                if (ElevatedOperations.ParseTasks("health,comando-invalido").Length != 0) throw new InvalidOperationException("Plano elevado inválido foi aceito.");
                string startupCommand = StartupManager.BuildCommand(@"C:\Program Files\Neck\Neck.exe");
                if (startupCommand != "\"C:\\Program Files\\Neck\\Neck.exe\" --background") throw new InvalidOperationException("Comando de inicialização inválido.");
                using (MainForm form = new MainForm(false, true))
                {
                    Console.WriteLine("MainFormSize=" + form.ClientSize.Width + "x" + form.ClientSize.Height);
                    form.ShowInTaskbar = false;
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new System.Drawing.Point(-32000, -32000);
                    form.Show();
                    for (int i = 0; i < 40; i++)
                    {
                        Application.DoEvents();
                        Thread.Sleep(50);
                    }
                    Console.WriteLine("MainFormShown=" + form.Visible);
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(form.Width, form.Height))
                    {
                        form.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
                        string previewPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neck.UI.png");
                        preview.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("UIPreview=" + previewPath);
                    }
                    form.ForceCloseForTesting();
                }
                if (!UpdateChecker.RepositoryUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("O verificador de atualizações não aponta para HTTPS.");
                using (PreferencesForm preferences = new PreferencesForm(GuardSettings.Load(), false))
                {
                    preferences.ShowInTaskbar = false;
                    preferences.StartPosition = FormStartPosition.Manual;
                    preferences.Location = new System.Drawing.Point(-32000, -32000);
                    preferences.Show();
                    Application.DoEvents();
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(preferences.Width, preferences.Height))
                    {
                        preferences.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, preferences.Width, preferences.Height));
                        string previewPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neck.Preferences.png");
                        preview.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("PreferencesPreview=" + previewPath);
                    }
                    preferences.Close();
                }
                using (MaintenanceOptionsForm options = new MaintenanceOptionsForm(true, true, false, true, false, true))
                {
                    options.ShowInTaskbar = false;
                    options.StartPosition = FormStartPosition.Manual;
                    options.Location = new System.Drawing.Point(-32000, -32000);
                    options.Show();
                    Application.DoEvents();
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(options.Width, options.Height))
                    {
                        options.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, options.Width, options.Height));
                        string previewPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neck.Advanced.png");
                        preview.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("AdvancedPreview=" + previewPath);
                    }
                    options.Close();
                }
                using (DiagnosticForm diagnostic = new DiagnosticForm(health))
                {
                    diagnostic.ShowInTaskbar = false;
                    diagnostic.StartPosition = FormStartPosition.Manual;
                    diagnostic.Location = new System.Drawing.Point(-32000, -32000);
                    diagnostic.Show();
                    Application.DoEvents();
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(diagnostic.Width, diagnostic.Height))
                    {
                        diagnostic.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, diagnostic.Width, diagnostic.Height));
                        string previewPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neck.Guard.png");
                        preview.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("GuardPreview=" + previewPath);
                    }
                    diagnostic.Close();
                }
                using (MeetingModeForm meetingForm = new MeetingModeForm(meeting))
                {
                    meetingForm.ShowInTaskbar = false;
                    meetingForm.StartPosition = FormStartPosition.Manual;
                    meetingForm.Location = new System.Drawing.Point(-32000, -32000);
                    meetingForm.Show();
                    Application.DoEvents();
                    if (meetingForm.DurationMinutes != 60) throw new InvalidOperationException("Duração padrão do Modo Reunião inválida.");
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(meetingForm.Width, meetingForm.Height))
                    {
                        meetingForm.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, meetingForm.Width, meetingForm.Height));
                        string previewPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neck.Meeting.png");
                        preview.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("MeetingPreview=" + previewPath);
                    }
                    meetingForm.Close();
                }
                System.Collections.Generic.List<GuardSample> synthetic = new System.Collections.Generic.List<GuardSample>();
                for (int i = 0; i < 6; i++)
                {
                    synthetic.Add(new GuardSample
                    {
                        TimestampUtc = DateTime.UtcNow.AddSeconds(-30 * (5 - i)),
                        MemoryPercent = 90,
                        AvailableBytes = 1024L * 1024 * 1024,
                        DiskFreeBytes = 100L * 1024 * 1024 * 1024,
                        TopProcess = "Aplicativo de teste",
                        TopProcessBytes = 4L * 1024 * 1024 * 1024
                    });
                }
                GuardAlert syntheticAlert = new GuardPressureDetector().Evaluate(synthetic);
                if (syntheticAlert.Kind != GuardAlertKind.MemoryPressure) throw new InvalidOperationException("Pressão persistente não detectada.");
                using (GuardHistoryForm history = new GuardHistoryForm(synthetic, System.IO.Path.GetTempPath()))
                {
                    history.ShowInTaskbar = false;
                    history.StartPosition = FormStartPosition.Manual;
                    history.Location = new System.Drawing.Point(-32000, -32000);
                    history.Show();
                    Application.DoEvents();
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(history.Width, history.Height))
                    {
                        history.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, history.Width, history.Height));
                        string previewPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neck.History.png");
                        preview.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("HistoryPreview=" + previewPath);
                    }
                    history.Close();
                }
                System.Collections.Generic.List<SosCandidate> sosCandidates = SosInspector.GetCandidates();
                using (SosForm sos = new SosForm())
                {
                    sos.ShowInTaskbar = false;
                    sos.StartPosition = FormStartPosition.Manual;
                    sos.Location = new System.Drawing.Point(-32000, -32000);
                    sos.Show();
                    Application.DoEvents();
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(sos.Width, sos.Height))
                    {
                        sos.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, sos.Width, sos.Height));
                        string previewPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neck.SOS.png");
                        preview.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("SosPreview=" + previewPath);
                    }
                    sos.Close();
                }
                Console.WriteLine("SELF_TEST_OK");
                Console.WriteLine("TempBytes=" + result.TempBytes);
                Console.WriteLine("TempFiles=" + result.TempFiles);
                Console.WriteLine("ReportBytes=" + result.ReportBytes);
                Console.WriteLine("ReportFiles=" + result.ReportFiles);
                Console.WriteLine("AccessErrors=" + result.AccessErrors);
                Console.WriteLine("MemoryPercent=" + memory.PercentUsed.ToString("0.0"));
                Console.WriteLine("HealthScore=" + health.Score);
                Console.WriteLine("HealthLevel=" + health.Level);
                Console.WriteLine("ObservedProcessGroups=" + health.TopProcesses.Count);
                Console.WriteLine("HealthScanMilliseconds=" + healthTimer.ElapsedMilliseconds);
                Console.WriteLine("MeetingChecks=" + meeting.Checks.Count);
                Console.WriteLine("SyntheticGuardAlert=" + syntheticAlert.Kind);
                Console.WriteLine("SosVisibleCandidates=" + sosCandidates.Count);
                Console.WriteLine("SelfTestIsAdministrator=" + SecurityHelper.IsAdministrator());
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("SELF_TEST_FAILED");
                Console.Error.WriteLine(ex);
                return 1;
            }
        }
    }
}
