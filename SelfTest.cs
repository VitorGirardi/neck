using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Neck
{
    internal static class SelfTest
    {
        private static int Main(string[] args)
        {
            if (args != null && args.Length == 1 && args[0] == "--efficiency-helper")
            {
                Thread.Sleep(15000);
                return 0;
            }
            string recoveryTestPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "recovery-test.log");
            try { if (System.IO.File.Exists(recoveryTestPath)) System.IO.File.Delete(recoveryTestPath); }
            catch { }
            RecoveryJournal.OverridePathForTesting(recoveryTestPath);
            try
            {
                ScanResult result = Cleaner.Analyze();
                MemoryStatus memory = SystemInfo.GetMemoryStatus();
                HardwareSnapshot hardware = HardwareInfoProvider.Read();
                if (hardware.CapturedUtc == DateTime.MinValue || string.IsNullOrWhiteSpace(hardware.ProcessorSummary) ||
                    string.IsNullOrWhiteSpace(hardware.MemorySummary))
                    throw new InvalidOperationException("O inventário de hardware não retornou um resumo válido.");
                if (hardware.Temperatures.Any(item => item.Celsius < 5d || item.Celsius > 125d))
                    throw new InvalidOperationException("Uma temperatura de hardware fora do intervalo seguro foi aceita.");
                TestSupportReportPrivacy(hardware);
                TestRecoveryLedgerRoundTrip();
                BluetoothSnapshot bluetooth = BluetoothDoctor.Read();
                if (bluetooth.CapturedUtc == DateTime.MinValue)
                    throw new InvalidOperationException("O diagnóstico Bluetooth não registrou o horário da leitura.");
                if (!BluetoothRepairEngine.IsSafeAdapterId(@"USB\VID_13D3&PID_3567\TESTE"))
                    throw new InvalidOperationException("Um adaptador Bluetooth físico válido foi recusado.");
                if (BluetoothRepairEngine.IsSafeAdapterId(@"BTHENUM\DEV_TESTE") || BluetoothRepairEngine.IsSafeAdapterId("USB\\TESTE\" MALICIOSO"))
                    throw new InvalidOperationException("Um identificador Bluetooth não confiável foi aceito para reparo.");
                TestBluetoothLoopProtection();
                TestBluetoothPowerResetPlan();
                System.Diagnostics.Stopwatch healthTimer = System.Diagnostics.Stopwatch.StartNew();
                HealthSnapshot health = SystemInfo.GetHealthSnapshot();
                healthTimer.Stop();
                if (health.Score < 0 || health.Score > 100) throw new InvalidOperationException("Pontuação de saúde inválida.");
                if (health.CpuPercent < 0 || health.CpuPercent > 100) throw new InvalidOperationException("Leitura de CPU inválida.");
                MeetingPreflight meeting = SystemInfo.GetMeetingPreflight();
                if (meeting.Checks.Count < 6) throw new InvalidOperationException("Checklist de reunião incompleto.");
                if (ElevatedOperations.ParseTasks("health,drives").Length != 2) throw new InvalidOperationException("Plano elevado válido foi recusado.");
                if (ElevatedOperations.ParseTasks("bluetooth").Length != 1) throw new InvalidOperationException("A cura Bluetooth não entrou na lista administrativa fechada.");
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
                    form.Size = new System.Drawing.Size(1052, 759);
                    for (int i = 0; i < 4; i++)
                    {
                        Application.DoEvents();
                        Thread.Sleep(25);
                    }
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(form.Width, form.Height))
                    {
                        form.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
                        string previewPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neck.UI.Regression.png");
                        preview.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("UIRegressionPreview=" + previewPath);
                    }
                    form.Size = form.MinimumSize;
                    for (int i = 0; i < 4; i++)
                    {
                        Application.DoEvents();
                        Thread.Sleep(25);
                    }
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(form.Width, form.Height))
                    {
                        form.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
                        string previewPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neck.UI.Minimum.png");
                        preview.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("UIMinimumPreview=" + previewPath);
                    }
                    form.ForceCloseForTesting();
                }
                using (ToolsHubForm tools = new ToolsHubForm(true, false, "Tudo pronto. O Neck está acompanhando o computador."))
                {
                    tools.ShowInTaskbar = false;
                    tools.StartPosition = FormStartPosition.Manual;
                    tools.Location = new System.Drawing.Point(-32000, -32000);
                    tools.Show();
                    Application.DoEvents();
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(tools.Width, tools.Height))
                    {
                        tools.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, tools.Width, tools.Height));
                        string previewPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neck.Tools.png");
                        preview.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("ToolsPreview=" + previewPath);
                    }
                    tools.Close();
                }
                using (SupportReportForm support = new SupportReportForm(new GuardSettings(),
                    new[] { new GuardSample { TimestampUtc = DateTime.UtcNow, MemoryPercent = 65, CpuPercent = 24 } },
                    hardware, new RecoveryStartupResult()))
                {
                    support.ShowInTaskbar = false;
                    support.StartPosition = FormStartPosition.Manual;
                    support.Location = new System.Drawing.Point(-32000, -32000);
                    support.Show();
                    Application.DoEvents();
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(support.Width, support.Height))
                    {
                        support.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, support.Width, support.Height));
                        string previewPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neck.Support.png");
                        preview.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("SupportPreview=" + previewPath);
                    }
                    support.Size = support.MinimumSize;
                    Application.DoEvents();
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(support.Width, support.Height))
                    {
                        support.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, support.Width, support.Height));
                        string previewPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neck.Support.Minimum.png");
                        preview.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("SupportMinimumPreview=" + previewPath);
                    }
                    support.Close();
                }
                if (!UpdateChecker.RepositoryUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("O verificador de atualizações não aponta para HTTPS.");
                string parsedStartupPath = StartupAnalyzer.ExtractExecutablePath("\"C:\\Program Files\\Aplicativo\\app.exe\" --background");
                if (parsedStartupPath != @"C:\Program Files\Aplicativo\app.exe")
                    throw new InvalidOperationException("O comando de inicialização não foi interpretado com segurança.");
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
                using (HardwareDetailsForm hardwareForm = new HardwareDetailsForm(hardware))
                {
                    hardwareForm.ShowInTaskbar = false;
                    hardwareForm.StartPosition = FormStartPosition.Manual;
                    hardwareForm.Location = new System.Drawing.Point(-32000, -32000);
                    hardwareForm.Show();
                    Application.DoEvents();
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(hardwareForm.Width, hardwareForm.Height))
                    {
                        hardwareForm.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, hardwareForm.Width, hardwareForm.Height));
                        string previewPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neck.Hardware.png");
                        preview.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("HardwarePreview=" + previewPath);
                    }
                    hardwareForm.Close();
                }
                BluetoothSnapshot bluetoothPreview = new BluetoothSnapshot
                {
                    CapturedUtc = DateTime.UtcNow,
                    KnownDeviceEntries = 4,
                    PowerState = BluetoothPowerState.On,
                    Adapters = new System.Collections.Generic.List<BluetoothAdapterInfo>
                    {
                        new BluetoothAdapterInfo
                        {
                            Name = "MediaTek Bluetooth Adapter",
                            DeviceId = @"USB\VID_13D3&PID_3567\TESTE",
                            Manufacturer = "MediaTek Inc.",
                            DriverVersion = "1.3.17.166",
                            DriverDate = new DateTime(2025, 5, 1),
                            ErrorCode = 0,
                            DriverBacked = true,
                            SeenByWindows = true
                        }
                    },
                    Services = new System.Collections.Generic.List<BluetoothServiceInfo>
                    {
                        new BluetoothServiceInfo { Name = "bthserv", DisplayName = "Serviço de Suporte a Bluetooth", State = "Running", StartMode = "Manual" },
                        new BluetoothServiceInfo { Name = "DeviceAssociationService", DisplayName = "Associação de Dispositivo", State = "Running", StartMode = "Manual" }
                    }
                };
                using (BluetoothDoctorForm bluetoothForm = new BluetoothDoctorForm(bluetoothPreview))
                {
                    bluetoothForm.ShowInTaskbar = false;
                    bluetoothForm.StartPosition = FormStartPosition.Manual;
                    bluetoothForm.Location = new System.Drawing.Point(-32000, -32000);
                    bluetoothForm.Show();
                    Application.DoEvents();
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(bluetoothForm.Width, bluetoothForm.Height))
                    {
                        bluetoothForm.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, bluetoothForm.Width, bluetoothForm.Height));
                        string previewPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neck.Bluetooth.png");
                        preview.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("BluetoothPreview=" + previewPath);
                    }
                    bluetoothForm.Size = bluetoothForm.MinimumSize;
                    Application.DoEvents();
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(bluetoothForm.Width, bluetoothForm.Height))
                    {
                        bluetoothForm.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, bluetoothForm.Width, bluetoothForm.Height));
                        string previewPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neck.Bluetooth.Minimum.png");
                        preview.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("BluetoothMinimumPreview=" + previewPath);
                    }
                    bluetoothForm.Close();
                }
                bluetoothPreview.PowerState = BluetoothPowerState.Off;
                using (BluetoothDoctorForm powerOffForm = new BluetoothDoctorForm(bluetoothPreview))
                {
                    powerOffForm.ShowInTaskbar = false;
                    powerOffForm.StartPosition = FormStartPosition.Manual;
                    powerOffForm.Location = new System.Drawing.Point(-32000, -32000);
                    powerOffForm.Show();
                    Application.DoEvents();
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(powerOffForm.Width, powerOffForm.Height))
                    {
                        powerOffForm.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, powerOffForm.Width, powerOffForm.Height));
                        string previewPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neck.Bluetooth.PowerOff.png");
                        preview.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("BluetoothPowerOffPreview=" + previewPath);
                    }
                    powerOffForm.Close();
                }
                bluetoothPreview.PowerState = BluetoothPowerState.On;
                bluetoothPreview.Adapters[0].ErrorCode = 43;
                bluetoothPreview.Services[0].State = "Stopped";
                using (BluetoothDoctorForm attentionForm = new BluetoothDoctorForm(bluetoothPreview))
                {
                    attentionForm.ShowInTaskbar = false;
                    attentionForm.StartPosition = FormStartPosition.Manual;
                    attentionForm.Location = new System.Drawing.Point(-32000, -32000);
                    attentionForm.Show();
                    Application.DoEvents();
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(attentionForm.Width, attentionForm.Height))
                    {
                        attentionForm.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, attentionForm.Width, attentionForm.Height));
                        string previewPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neck.Bluetooth.Attention.png");
                        preview.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("BluetoothAttentionPreview=" + previewPath);
                    }
                    attentionForm.Close();
                }
                bluetoothPreview.Adapters[0].ErrorCode = 0;
                bluetoothPreview.Services[0].State = "Running";
                bluetoothPreview.RecentTransportTimeouts = 5;
                bluetoothPreview.RecentDriverUnloads = 2;
                bluetoothPreview.LastTransportFailureUtc = DateTime.UtcNow.AddSeconds(-20);
                bluetoothPreview.EventHistoryAvailable = true;
                using (BluetoothDoctorForm loopGuardForm = new BluetoothDoctorForm(bluetoothPreview))
                {
                    loopGuardForm.ShowInTaskbar = false;
                    loopGuardForm.StartPosition = FormStartPosition.Manual;
                    loopGuardForm.Location = new System.Drawing.Point(-32000, -32000);
                    loopGuardForm.Show();
                    Application.DoEvents();
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(loopGuardForm.Width, loopGuardForm.Height))
                    {
                        loopGuardForm.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, loopGuardForm.Width, loopGuardForm.Height));
                        string previewPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neck.Bluetooth.LoopGuard.png");
                        preview.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("BluetoothLoopGuardPreview=" + previewPath);
                    }
                    loopGuardForm.Close();
                }
                using (BluetoothPowerResetForm powerResetForm = new BluetoothPowerResetForm())
                {
                    powerResetForm.ShowInTaskbar = false;
                    powerResetForm.StartPosition = FormStartPosition.Manual;
                    powerResetForm.Location = new System.Drawing.Point(-32000, -32000);
                    powerResetForm.Show();
                    Application.DoEvents();
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(powerResetForm.Width, powerResetForm.Height))
                    {
                        powerResetForm.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, powerResetForm.Width, powerResetForm.Height));
                        string previewPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neck.Bluetooth.PowerReset.png");
                        preview.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("BluetoothPowerResetPreview=" + previewPath);
                    }
                    powerResetForm.Size = powerResetForm.MinimumSize;
                    Application.DoEvents();
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(powerResetForm.Width, powerResetForm.Height))
                    {
                        powerResetForm.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, powerResetForm.Width, powerResetForm.Height));
                        string previewPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neck.Bluetooth.PowerReset.Minimum.png");
                        preview.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("BluetoothPowerResetMinimumPreview=" + previewPath);
                    }
                    powerResetForm.Close();
                }
                System.Collections.Generic.List<StartupEntry> startupEntries = StartupAnalyzer.Analyze();
                using (StartupAppsForm startup = new StartupAppsForm(startupEntries))
                {
                    startup.ShowInTaskbar = false;
                    startup.StartPosition = FormStartPosition.Manual;
                    startup.Location = new System.Drawing.Point(-32000, -32000);
                    startup.Show();
                    Application.DoEvents();
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(startup.Width, startup.Height))
                    {
                        startup.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, startup.Width, startup.Height));
                        string previewPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neck.Boot.png");
                        preview.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("BootPreview=" + previewPath);
                    }
                    startup.Close();
                }
                HealthSnapshot planHealth = new HealthSnapshot
                {
                    Score = 32,
                    Level = HealthLevel.Critical,
                    Summary = "Memória e disco sob pressão.",
                    Memory = new MemoryStatus { PercentUsed = 92, AvailableBytes = 512UL * 1024 * 1024 },
                    DiskFreeBytes = 5L * 1024 * 1024 * 1024,
                    DiskTotalBytes = 256L * 1024 * 1024 * 1024,
                    TopProcesses = new System.Collections.Generic.List<ResourceProcess>
                    {
                        new ResourceProcess { DisplayName = "Aplicativo de teste", MemoryBytes = 4L * 1024 * 1024 * 1024, ProcessCount = 1 }
                    }
                };
                ScanResult planCleanup = new ScanResult { TempBytes = 900L * 1024 * 1024, TempFiles = 1200 };
                System.Collections.Generic.List<StartupEntry> planStartup = new System.Collections.Generic.List<StartupEntry>
                {
                    new StartupEntry { Name = "Aplicativo opcional", Enabled = true, Recommendation = "Pode revisar" }
                };
                PersonalPlan syntheticPlan = PersonalPlanAnalyzer.BuildFromInputs(planHealth, planCleanup, planStartup, true);
                if (syntheticPlan.Actions.Count != 3) throw new InvalidOperationException("O plano não retornou exatamente três prioridades.");
                if (syntheticPlan.Actions[0].Kind != PlanActionKind.Sos) throw new InvalidOperationException("A pressão crítica de memória não recebeu prioridade.");
                PersonalPlan currentPlan = PersonalPlanAnalyzer.Build();
                using (PersonalPlanForm planForm = new PersonalPlanForm(syntheticPlan))
                {
                    planForm.ShowInTaskbar = false;
                    planForm.StartPosition = FormStartPosition.Manual;
                    planForm.Location = new System.Drawing.Point(-32000, -32000);
                    planForm.Show();
                    Application.DoEvents();
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(planForm.Width, planForm.Height))
                    {
                        planForm.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, planForm.Width, planForm.Height));
                        string previewPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neck.Plan.png");
                        preview.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("PlanPreview=" + previewPath);
                    }
                    planForm.Close();
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
                    options.Size = options.MinimumSize;
                    Application.DoEvents();
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(options.Width, options.Height))
                    {
                        options.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, options.Width, options.Height));
                        string previewPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neck.Advanced.Minimum.png");
                        preview.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("AdvancedMinimumPreview=" + previewPath);
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
                System.Collections.Generic.List<GuardSample> syntheticCpu = new System.Collections.Generic.List<GuardSample>();
                for (int i = 0; i < 3; i++)
                {
                    syntheticCpu.Add(new GuardSample
                    {
                        TimestampUtc = DateTime.UtcNow.AddSeconds(-30 * (2 - i)),
                        MemoryPercent = 55,
                        CpuPercent = 95,
                        AvailableBytes = 6L * 1024 * 1024 * 1024,
                        DiskFreeBytes = 100L * 1024 * 1024 * 1024
                    });
                }
                if (new GuardPressureDetector().Evaluate(syntheticCpu).Kind != GuardAlertKind.CpuPressure)
                    throw new InvalidOperationException("Pressão persistente de CPU não detectada.");
                HealthSnapshot guidedMemory = new HealthSnapshot
                {
                    Level = HealthLevel.Warning,
                    CpuPercent = 45,
                    Memory = new MemoryStatus { PercentUsed = 82 },
                    DiskFreeBytes = 100L * 1024 * 1024 * 1024,
                    DiskTotalBytes = 256L * 1024 * 1024 * 1024,
                    TopProcesses = new System.Collections.Generic.List<ResourceProcess>
                    {
                        new ResourceProcess { ProcessName = "NeckGuidedApp", DisplayName = "Aplicativo guiado", MemoryBytes = 2L * 1024 * 1024 * 1024 }
                    }
                };
                BottleneckAdvice guidedAdvice = BottleneckAdvisor.Analyze(guidedMemory);
                if (guidedAdvice.Kind != BottleneckKind.Memory || guidedAdvice.ProcessName != "NeckGuidedApp")
                    throw new InvalidOperationException("O Gargalo Guiado não recomendou o maior consumidor de memória.");
                HealthSnapshot guidedDisk = new HealthSnapshot
                {
                    Level = HealthLevel.Critical,
                    CpuPercent = 20,
                    Memory = new MemoryStatus { PercentUsed = 40 },
                    DiskFreeBytes = 1024L * 1024 * 1024,
                    DiskTotalBytes = 256L * 1024 * 1024 * 1024
                };
                if (BottleneckAdvisor.Analyze(guidedDisk).Kind != BottleneckKind.Disk)
                    throw new InvalidOperationException("O Gargalo Guiado não priorizou armazenamento crítico.");
                HealthSnapshot guidedCpu = new HealthSnapshot
                {
                    Level = HealthLevel.Warning,
                    CpuPercent = 92,
                    Memory = new MemoryStatus { PercentUsed = 50 },
                    DiskFreeBytes = 100L * 1024 * 1024 * 1024,
                    DiskTotalBytes = 256L * 1024 * 1024 * 1024
                };
                if (BottleneckAdvisor.Analyze(guidedCpu).Kind != BottleneckKind.Cpu)
                    throw new InvalidOperationException("O Gargalo Guiado não identificou pressão de CPU.");
                SmartGuardMonitor smartMonitor = new SmartGuardMonitor();
                smartMonitor.Evaluate(guidedMemory);
                smartMonitor.Evaluate(guidedMemory);
                SmartMonitorDecision confirmedPressure = smartMonitor.Evaluate(guidedMemory);
                if (confirmedPressure.State != SmartMonitorState.Confirmed || !confirmedPressure.PressureConfirmed)
                    throw new InvalidOperationException("O monitor inteligente alertou sem confirmar três leituras.");
                smartMonitor.Evaluate(new HealthSnapshot { Level = HealthLevel.Stable });
                SmartMonitorDecision recoveredFlow = smartMonitor.Evaluate(new HealthSnapshot { Level = HealthLevel.Stable });
                if (!recoveredFlow.RecoveryConfirmed || recoveredFlow.NextIntervalMilliseconds != 60000)
                    throw new InvalidOperationException("O monitor inteligente não confirmou a recuperação do fluxo.");
                OptimizationOutcome syntheticOutcome = new OptimizationOutcome
                {
                    Complete = true,
                    AvailableBefore = 2L * 1024 * 1024 * 1024,
                    AvailableAfter = 3L * 1024 * 1024 * 1024,
                    ProcessesChanged = 2
                };
                if (!syntheticOutcome.Summary.StartsWith("Resultado observado:", StringComparison.Ordinal))
                    throw new InvalidOperationException("A medição de resultado não produziu um resumo verificável.");
                ReplaySample healthyMemory = new ReplaySample
                {
                    TimestampUtc = DateTime.UtcNow,
                    MemoryPercent = 74,
                    AvailableBytes = 4L * 1024 * 1024 * 1024,
                    CommitPercent = 78,
                    PageReadsPerSecond = 220,
                    CpuPercent = 35,
                    ForegroundResponsive = true
                };
                if (ReplayClassifier.Analyze(healthyMemory).Cause != ReplayCause.None)
                    throw new InvalidOperationException("O Replay tratou RAM alta com folga como gargalo real.");
                using (ReplayPerformanceSampler performanceSampler = new ReplayPerformanceSampler())
                {
                    System.Threading.Thread.Sleep(250);
                    ReplayPerformanceValues performanceValues = performanceSampler.Capture();
                    if (performanceValues.CommitPercent <= 0 || performanceValues.CommitPercent > 100)
                        throw new InvalidOperationException("Os contadores PDH independentes do idioma não retornaram a pressão de commit.");
                    Console.WriteLine("ReplayCounters=Commit " + performanceValues.CommitPercent.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%/Disk " + performanceValues.DiskLatencyMilliseconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "ms");
                }
                using (ReplayProbe replayProbe = new ReplayProbe())
                {
                    System.Threading.Thread.Sleep(250);
                    System.Diagnostics.Stopwatch replayTimer = System.Diagnostics.Stopwatch.StartNew();
                    ReplayCapture liveReplay = replayProbe.Capture(0);
                    replayTimer.Stop();
                    if (liveReplay == null || liveReplay.Sample == null || liveReplay.Health == null)
                        throw new InvalidOperationException("A captura real do Replay não retornou contexto.");
                    if (replayTimer.ElapsedMilliseconds > 2000)
                        throw new InvalidOperationException("A captura do Replay excedeu o limite conservador de 2 segundos.");
                    Console.WriteLine("ReplayCaptureMilliseconds=" + replayTimer.ElapsedMilliseconds);
                }
                ReplaySample diskStall = new ReplaySample
                {
                    TimestampUtc = DateTime.UtcNow,
                    MemoryPercent = 55,
                    AvailableBytes = 6L * 1024 * 1024 * 1024,
                    CpuPercent = 35,
                    DiskActivePercent = 96,
                    DiskLatencyMilliseconds = 85,
                    DiskQueueLength = 3,
                    ForegroundResponsive = true
                };
                if (ReplayClassifier.Analyze(diskStall).Cause != ReplayCause.DiskStall)
                    throw new InvalidOperationException("O Replay não reconheceu espera real do armazenamento.");
                ReplaySample cpuContention = new ReplaySample
                {
                    TimestampUtc = DateTime.UtcNow,
                    MemoryPercent = 52,
                    AvailableBytes = 7L * 1024 * 1024 * 1024,
                    CpuPercent = 96,
                    ProcessorQueueLength = Math.Max(4, Environment.ProcessorCount),
                    TopCpuProcess = "NeckReplayCpuApp",
                    ForegroundResponsive = true
                };
                if (ReplayClassifier.Analyze(cpuContention).Cause != ReplayCause.CpuContention)
                    throw new InvalidOperationException("O Replay não reconheceu disputa real de CPU.");
                ReplayEngine replayEngine = new ReplayEngine();
                DateTime replayStart = DateTime.UtcNow.AddSeconds(-50);
                ReplayDecision replayDecision = null;
                bool replayConfirmed = false;
                for (int i = 0; i < 3; i++)
                {
                    replayDecision = replayEngine.Record(new ReplaySample
                    {
                        TimestampUtc = replayStart.AddSeconds(i * 10),
                        MemoryPercent = 93,
                        AvailableBytes = 620L * 1024 * 1024,
                        CommitPercent = 96,
                        PageReadsPerSecond = 140,
                        CpuPercent = 48,
                        TopMemoryProcess = "Claude",
                        TopMemoryBytes = 3L * 1024 * 1024 * 1024,
                        ForegroundResponsive = true
                    });
                    replayConfirmed = replayConfirmed || replayDecision.IncidentConfirmed;
                }
                if (replayDecision == null || !replayConfirmed || replayDecision.Incident == null ||
                    replayDecision.Incident.Cause != ReplayCause.MemoryPressure)
                    throw new InvalidOperationException("O Replay não congelou o contexto após pressão persistente.");
                replayEngine.Record(new ReplaySample
                {
                    TimestampUtc = replayStart.AddSeconds(30),
                    MemoryPercent = 58,
                    AvailableBytes = 6L * 1024 * 1024 * 1024,
                    CommitPercent = 65,
                    CpuPercent = 30,
                    ForegroundResponsive = true
                });
                ReplayDecision replayRecovered = replayEngine.Record(new ReplaySample
                {
                    TimestampUtc = replayStart.AddSeconds(40),
                    MemoryPercent = 55,
                    AvailableBytes = 7L * 1024 * 1024 * 1024,
                    CommitPercent = 62,
                    CpuPercent = 24,
                    ForegroundResponsive = true
                });
                if (!replayRecovered.RecoveryConfirmed || replayRecovered.Incident == null || replayRecovered.Incident.Ongoing)
                    throw new InvalidOperationException("O Replay não confirmou a recuperação com duas leituras estáveis.");
                string baselinePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "neck-baseline-" + Guid.NewGuid().ToString("N") + ".txt");
                BaselineView baselineView;
                try
                {
                    using (BaselineEngine baseline = new BaselineEngine(baselinePath))
                    {
                        BaselineEvaluation learned = null;
                        for (int i = 0; i < BaselineEngine.RequiredSamples; i++)
                        {
                            learned = baseline.Observe(new ReplaySample
                            {
                                TimestampUtc = DateTime.UtcNow.AddSeconds(i * 10),
                                MemoryPercent = 70 + (i % 3 - 1),
                                AvailableBytes = (4L * 1024 * 1024 * 1024) + (i % 3 - 1) * 32L * 1024 * 1024,
                                CommitPercent = 72 + (i % 3 - 1),
                                PageReadsPerSecond = 2 + i % 2,
                                CpuPercent = 20 + (i % 5 - 2),
                                ProcessorQueueLength = 0.2d,
                                DiskActivePercent = 5 + i % 3,
                                DiskLatencyMilliseconds = 2 + i % 2,
                                DiskQueueLength = 0.1d,
                                TemperatureCelsius = 65 + i % 2,
                                TopMemoryProcess = "SensitiveAppMustNotPersist",
                                TopCpuProcess = "SensitiveAppMustNotPersist",
                                ForegroundProcess = "SensitiveAppMustNotPersist",
                                ForegroundResponsive = true
                            }, false);
                        }
                        if (learned == null || learned.State != BaselineState.Personalized || learned.Score < 85)
                            throw new InvalidOperationException("O Baseline não formou um padrão local saudável.");

                        ReplaySample personalizedIncident = new ReplaySample
                        {
                            TimestampUtc = DateTime.UtcNow.AddMinutes(6),
                            MemoryPercent = 84,
                            AvailableBytes = 2L * 1024 * 1024 * 1024,
                            CommitPercent = 89,
                            PageReadsPerSecond = 10,
                            CpuPercent = 23,
                            ProcessorQueueLength = 0.2d,
                            DiskActivePercent = 6,
                            DiskLatencyMilliseconds = 3,
                            DiskQueueLength = 0.1d,
                            TemperatureCelsius = 66,
                            TopMemoryProcess = "SensitiveIncidentMustNotPersist",
                            ForegroundResponsive = true
                        };
                        if (ReplayClassifier.Analyze(personalizedIncident).Cause != ReplayCause.None)
                            throw new InvalidOperationException("A amostra personalizada de teste acionou um limite absoluto.");
                        BaselineEvaluation incidentEvaluation = baseline.Observe(personalizedIncident, false);
                        if (incidentEvaluation.SampleAccepted || incidentEvaluation.Score >= 85 ||
                            baseline.GetView().Profile.Normal.SampleCount != BaselineEngine.RequiredSamples)
                            throw new InvalidOperationException("O Baseline contaminou o padrão ao aprender um desvio local.");

                        for (int i = 0; i < 6; i++)
                        {
                            baseline.Observe(new ReplaySample
                            {
                                TimestampUtc = DateTime.UtcNow.AddMinutes(7).AddSeconds(i * 10),
                                MemoryPercent = 72,
                                AvailableBytes = 4L * 1024 * 1024 * 1024,
                                CommitPercent = 74,
                                PageReadsPerSecond = 3,
                                CpuPercent = 30,
                                ProcessorQueueLength = 0.4d,
                                DiskActivePercent = 7,
                                DiskLatencyMilliseconds = 3,
                                DiskQueueLength = 0.2d,
                                TemperatureCelsius = 67,
                                ForegroundResponsive = true
                            }, true);
                        }
                        baselineView = baseline.GetView();
                        if (baselineView.Profile.Normal.SampleCount != BaselineEngine.RequiredSamples ||
                            baselineView.Profile.Meeting.SampleCount != 6)
                            throw new InvalidOperationException("O Baseline misturou os contextos normal e reunião.");
                    }
                    string persistedBaseline = System.IO.File.ReadAllText(baselinePath);
                    if (persistedBaseline.IndexOf("SensitiveAppMustNotPersist", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        persistedBaseline.IndexOf("SensitiveIncidentMustNotPersist", StringComparison.OrdinalIgnoreCase) >= 0)
                        throw new InvalidOperationException("O Baseline persistiu identidade de aplicativo ou processo.");
                    using (BaselineEngine reloadedBaseline = new BaselineEngine(baselinePath))
                    {
                        BaselineView reloaded = reloadedBaseline.GetView();
                        if (reloaded.Profile.Normal.SampleCount != BaselineEngine.RequiredSamples || reloaded.Profile.Meeting.SampleCount != 6)
                            throw new InvalidOperationException("O Baseline não restaurou os agregados locais.");
                    }
                }
                finally
                {
                    try { if (System.IO.File.Exists(baselinePath)) System.IO.File.Delete(baselinePath); }
                    catch { }
                    try { if (System.IO.File.Exists(baselinePath + ".tmp")) System.IO.File.Delete(baselinePath + ".tmp"); }
                    catch { }
                }
                using (BaselineForm baseline = new BaselineForm(baselineView))
                {
                    baseline.ShowInTaskbar = false;
                    baseline.StartPosition = FormStartPosition.Manual;
                    baseline.Location = new System.Drawing.Point(-32000, -32000);
                    baseline.Show();
                    Application.DoEvents();
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(baseline.Width, baseline.Height))
                    {
                        baseline.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, baseline.Width, baseline.Height));
                        string previewPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neck.Baseline.png");
                        preview.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("BaselinePreview=" + previewPath);
                    }
                    baseline.Size = baseline.MinimumSize;
                    Application.DoEvents();
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(baseline.Width, baseline.Height))
                    {
                        baseline.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, baseline.Width, baseline.Height));
                        string previewPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neck.Baseline.Minimum.png");
                        preview.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("BaselineMinimumPreview=" + previewPath);
                    }
                    baseline.Close();
                }
                AutopilotDecision simulatedAutopilot = AutopilotSimulation.Run();
                if (simulatedAutopilot.State != AutopilotState.Protecting ||
                    simulatedAutopilot.Cause != AutopilotCause.Memory ||
                    simulatedAutopilot.ProtectedApplications != 2 || !simulatedAutopilot.Simulated)
                    throw new InvalidOperationException("A simulação do Autopilot não antecipou a pressão de memória.");
                AutopilotEngine cautiousAutopilot = new AutopilotEngine();
                DateTime cautiousStart = DateTime.UtcNow.AddMinutes(-2);
                AutopilotDecision singlePrediction = null;
                double[] cautiousMemory = { 70, 70, 70, 82 };
                double[] cautiousAvailable = { 4096, 4096, 4096, 1800 };
                for (int i = 0; i < cautiousMemory.Length; i++)
                {
                    singlePrediction = cautiousAutopilot.Evaluate(new ReplaySample
                    {
                        TimestampUtc = cautiousStart.AddSeconds(i * 10),
                        MemoryPercent = cautiousMemory[i],
                        AvailableBytes = (long)(cautiousAvailable[i] * 1024d * 1024d),
                        CommitPercent = i == 3 ? 86 : 72,
                        PageReadsPerSecond = 3,
                        CpuPercent = 22,
                        DiskLatencyMilliseconds = 2,
                        ForegroundProcess = "AplicativoImportante",
                        ForegroundResponsive = true
                    }, baselineView, true, false, false);
                }
                if (singlePrediction == null || singlePrediction.State != AutopilotState.Watching || singlePrediction.ShouldProtect)
                    throw new InvalidOperationException("O Autopilot agiu depois de uma única previsão isolada.");
                using (AutopilotForm autopilot = new AutopilotForm(
                    new GuardSettings { AutopilotEnabled = true }, new AutopilotEngine(),
                    new AutopilotDecision
                    {
                        State = AutopilotState.Flowing,
                        Title = "Autopilot acompanhando",
                        Explanation = "Nenhuma tendência de gargalo foi confirmada."
                    }, baselineView))
                {
                    autopilot.ShowInTaskbar = false;
                    autopilot.StartPosition = FormStartPosition.Manual;
                    autopilot.Location = new System.Drawing.Point(-32000, -32000);
                    autopilot.Show();
                    autopilot.ShowSimulationForTesting();
                    Application.DoEvents();
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(autopilot.Width, autopilot.Height))
                    {
                        autopilot.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, autopilot.Width, autopilot.Height));
                        string previewPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neck.Autopilot.png");
                        preview.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("AutopilotPreview=" + previewPath);
                    }
                    autopilot.Size = autopilot.MinimumSize;
                    Application.DoEvents();
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(autopilot.Width, autopilot.Height))
                    {
                        autopilot.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, autopilot.Width, autopilot.Height));
                        string previewPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neck.Autopilot.Minimum.png");
                        preview.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("AutopilotMinimumPreview=" + previewPath);
                    }
                    autopilot.Close();
                }
                using (ReplayForm replay = new ReplayForm(replayEngine.GetLatestIncident(), replayEngine.GetSamples()))
                {
                    replay.ShowInTaskbar = false;
                    replay.StartPosition = FormStartPosition.Manual;
                    replay.Location = new System.Drawing.Point(-32000, -32000);
                    replay.Show();
                    Application.DoEvents();
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(replay.Width, replay.Height))
                    {
                        replay.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, replay.Width, replay.Height));
                        string previewPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neck.Replay.png");
                        preview.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("ReplayPreview=" + previewPath);
                    }
                    replay.Size = replay.MinimumSize;
                    Application.DoEvents();
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(replay.Width, replay.Height))
                    {
                        replay.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, replay.Width, replay.Height));
                        string previewPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neck.Replay.Minimum.png");
                        preview.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("ReplayMinimumPreview=" + previewPath);
                    }
                    replay.Close();
                }
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
                if (EfficiencyModeManager.CanTarget("svchost")) throw new InvalidOperationException("Processo protegido aceito pelo Neck Adaptive.");
                if (!EfficiencyModeManager.CanTarget("NeckEfficiencyCandidate")) throw new InvalidOperationException("Candidato seguro rejeitado pelo Neck Adaptive.");
                ProcessPowerThrottlingState efficiencyState = EfficiencyModeManager.CreateEfficiencyState(true);
                if (efficiencyState.Version != 1 || efficiencyState.ControlMask != 1 || efficiencyState.StateMask != 1)
                    throw new InvalidOperationException("Estado EcoQoS inválido.");
                EfficiencyModeResult missingMode = EfficiencyModeManager.Apply("NeckProcessThatDoesNotExist");
                if (missingMode.HasChanges || EfficiencyModeManager.IsActive("NeckProcessThatDoesNotExist"))
                    throw new InvalidOperationException("Neck Adaptive vazio permaneceu ativo.");
                TestProcessFamilyDiscovery();
                TestEfficiencyModeRoundTrip();
                TestInterruptedRecovery();
                TestTurboModeRoundTrip();
                TestFocusModeRoundTrip();
                TestFocusShieldRoundTrip();
                TestAutopilotProtectionRoundTrip();
                string guidedProcess = sosCandidates.Count == 0 ? null : sosCandidates[0].ProcessName;
                using (SosForm sos = new SosForm(guidedProcess))
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
                SosCandidate advancedCandidate = sosCandidates.FirstOrDefault() ?? new SosCandidate
                {
                    ProcessName = "NeckAdvancedPreview",
                    DisplayName = "Aplicativo de exemplo",
                    ProcessCount = 1,
                    MemoryBytes = 512L * 1024 * 1024
                };
                using (AdvancedAppOptionsForm advancedApp = new AdvancedAppOptionsForm(advancedCandidate))
                {
                    advancedApp.ShowInTaskbar = false;
                    advancedApp.StartPosition = FormStartPosition.Manual;
                    advancedApp.Location = new System.Drawing.Point(-32000, -32000);
                    advancedApp.Show();
                    Application.DoEvents();
                    using (System.Drawing.Bitmap preview = new System.Drawing.Bitmap(advancedApp.Width, advancedApp.Height))
                    {
                        advancedApp.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, advancedApp.Width, advancedApp.Height));
                        string previewPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Neck.AppOptions.png");
                        preview.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("AppOptionsPreview=" + previewPath);
                    }
                    advancedApp.Close();
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
                Console.WriteLine("BluetoothPowerState=" + bluetooth.PowerState);
                Console.WriteLine("SyntheticGuardAlert=" + syntheticAlert.Kind);
                Console.WriteLine("CpuPercent=" + health.CpuPercent.ToString("0.0"));
                Console.WriteLine("SosVisibleCandidates=" + sosCandidates.Count);
                Console.WriteLine("StartupEntries=" + startupEntries.Count);
                Console.WriteLine("PlanActions=" + currentPlan.Actions.Count);
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

        private static void TestBluetoothLoopProtection()
        {
            DateTime nowUtc = DateTime.UtcNow;
            BluetoothSnapshot unstable = new BluetoothSnapshot
            {
                CapturedUtc = nowUtc,
                PowerState = BluetoothPowerState.On,
                RecentTransportTimeouts = 5,
                RecentDriverUnloads = 2,
                LastTransportFailureUtc = nowUtc.AddSeconds(-20),
                EventHistoryAvailable = true,
                Adapters = new System.Collections.Generic.List<BluetoothAdapterInfo>
                {
                    new BluetoothAdapterInfo
                    {
                        Name = "Adaptador Bluetooth de teste",
                        DeviceId = @"USB\VID_0000&PID_0000\TESTE",
                        DriverVersion = "1.0.0.0",
                        DriverBacked = true,
                        SeenByWindows = true,
                        ErrorCode = 0
                    }
                },
                Services = new System.Collections.Generic.List<BluetoothServiceInfo>
                {
                    new BluetoothServiceInfo { Name = "bthserv", State = "Running" }
                }
            };

            if (!unstable.IsCoreHealthy || unstable.IsHealthy)
                throw new InvalidOperationException("Uma queda BTHUSB recente foi confundida com Bluetooth estável.");

            BluetoothRepairBlock repeated = BluetoothRepairGuard.Evaluate(unstable, nowUtc, null);
            if (!repeated.IsBlocked || repeated.RemainingMinutes(nowUtc) < 1)
                throw new InvalidOperationException("A proteção anti-loop não bloqueou falhas repetidas do driver.");

            unstable.RecentTransportTimeouts = 1;
            unstable.RecentDriverUnloads = 1;
            BluetoothRepairBlock firstAttempt = BluetoothRepairGuard.Evaluate(unstable, nowUtc, null);
            if (firstAttempt.IsBlocked)
                throw new InvalidOperationException("Uma falha isolada bloqueou indevidamente a primeira correção segura.");

            BluetoothRepairBlock failedAttempt = BluetoothRepairGuard.Evaluate(unstable, nowUtc, nowUtc.AddSeconds(-30));
            if (!failedAttempt.IsBlocked)
                throw new InvalidOperationException("A queda posterior à correção não ativou o bloqueio temporário.");
        }

        private static void TestBluetoothPowerResetPlan()
        {
            string plan = BluetoothPowerResetCoordinator.BuildShutdownArguments();
            if (!BluetoothPowerResetCoordinator.IsSafeFullShutdownPlan(plan))
                throw new InvalidOperationException("O desligamento completo do reset elétrico foi recusado pela validação.");
            if (BluetoothPowerResetCoordinator.IsSafeFullShutdownPlan("/s /t 0 /f") ||
                BluetoothPowerResetCoordinator.IsSafeFullShutdownPlan("/s /t 0 /hybrid") ||
                BluetoothPowerResetCoordinator.IsSafeFullShutdownPlan("/r /t 0"))
                throw new InvalidOperationException("Um desligamento forçado, híbrido ou uma reinicialização foi aceito no reset elétrico.");
        }

        private static void TestSupportReportPrivacy(HardwareSnapshot hardware)
        {
            string sensitive = @"C:\Users\" + Environment.UserName + @"\Documents\arquivo-pessoal.txt " + Environment.MachineName;
            string sanitized = SupportDiagnostics.Sanitize(sensitive);
            if (Environment.UserName.Length >= 3 && sanitized.IndexOf(Environment.UserName, StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException("O sanitizador preservou o nome do usuário.");
            if (Environment.MachineName.Length >= 3 && sanitized.IndexOf(Environment.MachineName, StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException("O sanitizador preservou o nome do computador.");
            GuardSample privateSample = new GuardSample
            {
                TimestampUtc = DateTime.UtcNow,
                MemoryPercent = 72,
                CpuPercent = 31,
                DiskFreeBytes = 100L * 1024 * 1024 * 1024,
                TopProcess = "AplicativoPrivadoDoTeste",
                TopProcessBytes = 512L * 1024 * 1024
            };
            string report = SupportReportBuilder.BuildText(new GuardSettings { AutopilotEnabled = true },
                new[] { privateSample }, hardware, new RecoveryStartupResult());
            if (report.IndexOf("AplicativoPrivadoDoTeste", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException("O relatório de suporte expôs o nome de um aplicativo.");
            if ((Environment.UserName.Length >= 3 && report.IndexOf(Environment.UserName, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (Environment.MachineName.Length >= 3 && report.IndexOf(Environment.MachineName, StringComparison.OrdinalIgnoreCase) >= 0))
                throw new InvalidOperationException("O relatório de suporte expôs a identidade local.");
            if (report.IndexOf("RELATÓRIO DE SUPORTE SANITIZADO", StringComparison.OrdinalIgnoreCase) < 0 ||
                report.IndexOf("PRIVACIDADE", StringComparison.OrdinalIgnoreCase) < 0 ||
                report.IndexOf("não inclui nome de usuário", StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidOperationException("O relatório de suporte não explica sua privacidade.");
            Console.WriteLine("SupportReportPrivacy=OK");
        }

        private static void TestRecoveryLedgerRoundTrip()
        {
            RecoveryRecord record = new RecoveryRecord
            {
                Kind = RecoveryChangeKind.Efficiency,
                CreatedUtc = DateTime.UtcNow,
                ProcessId = 424242,
                ProcessName = "NeckRecoveryLedgerProbe",
                StartTimeUtcTicks = DateTime.UtcNow.Ticks,
                OriginalPriority = 0x20,
                PriorityChanged = true
            };
            if (!RecoveryJournal.Put(record) || RecoveryJournal.Load().Count != 1)
                throw new InvalidOperationException("O diário de recuperação não persistiu a alteração.");
            if (!RecoveryJournal.Remove(record.Kind, record.ProcessId, record.StartTimeUtcTicks) || RecoveryJournal.PendingCount != 0)
                throw new InvalidOperationException("O diário de recuperação não removeu a alteração restaurada.");
            Console.WriteLine("RecoveryLedgerRoundTrip=OK");
        }

        private static void TestInterruptedRecovery()
        {
            string probePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NeckRecoveryProbe.exe");
            System.Diagnostics.Process probe = null;
            try
            {
                System.IO.File.Copy(Application.ExecutablePath, probePath, true);
                probe = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = probePath,
                    Arguments = "--efficiency-helper",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                Thread.Sleep(500);
                EfficiencyModeResult applied = EfficiencyModeManager.Apply("NeckRecoveryProbe");
                if (!applied.HasChanges)
                    throw new InvalidOperationException("A preparação do processo de recuperação não aplicou nenhuma ação de teste.");
                if (RecoveryJournal.PendingCount > 0) RecoveryManager.RestoreInterruptedChanges();
                EfficiencyModeManager.Restore("NeckRecoveryProbe");

                probe.Refresh();
                System.Diagnostics.ProcessPriorityClass originalPriority = probe.PriorityClass;
                System.Diagnostics.ProcessPriorityClass temporaryPriority = originalPriority == System.Diagnostics.ProcessPriorityClass.BelowNormal
                    ? System.Diagnostics.ProcessPriorityClass.Normal
                    : System.Diagnostics.ProcessPriorityClass.BelowNormal;
                RecoveryRecord interrupted = new RecoveryRecord
                {
                    Kind = RecoveryChangeKind.Turbo,
                    CreatedUtc = DateTime.UtcNow,
                    ProcessId = probe.Id,
                    ProcessName = probe.ProcessName,
                    StartTimeUtcTicks = probe.StartTime.ToUniversalTime().Ticks,
                    OriginalPriority = (uint)(int)originalPriority,
                    PriorityChanged = true
                };
                if (!RecoveryJournal.Put(interrupted))
                    throw new InvalidOperationException("A alteração reversível não entrou no diário antes de ser aplicada.");
                probe.PriorityClass = temporaryPriority;
                RecoveryStartupResult recovered = RecoveryManager.RestoreInterruptedChanges();
                if (recovered.RestoredEntries < 1 || recovered.FailedEntries != 0 || RecoveryJournal.PendingCount != 0)
                    throw new InvalidOperationException("A recuperação após interrupção não restaurou o processo de teste.");
                probe.Refresh();
                if (probe.PriorityClass != originalPriority)
                    throw new InvalidOperationException("A prioridade original não voltou após a recuperação simulada.");
                Console.WriteLine("InterruptedRecovery=OK; Restored=" + recovered.RestoredEntries);
            }
            finally
            {
                EfficiencyModeManager.Restore("NeckRecoveryProbe");
                if (probe != null)
                {
                    try { if (!probe.HasExited) probe.Kill(); }
                    catch { }
                    probe.Dispose();
                }
                try { if (System.IO.File.Exists(probePath)) System.IO.File.Delete(probePath); }
                catch { }
            }
        }

        private static void TestAutopilotProtectionRoundTrip()
        {
            string probePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NeckAutopilotProbe.exe");
            System.Diagnostics.Process probe = null;
            try
            {
                System.IO.File.Copy(Application.ExecutablePath, probePath, true);
                probe = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = probePath,
                    Arguments = "--efficiency-helper",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                Thread.Sleep(500);
                AutopilotProtectionResult applied = AutopilotProtectionManager.StartForTesting(
                    "NeckImportantForeground",
                    new[]
                    {
                        new SosCandidate
                        {
                            ProcessName = "NeckAutopilotProbe",
                            DisplayName = "Aplicativo opcional de teste",
                            ProcessCount = 1,
                            VisibleWindows = 1,
                            MemoryBytes = 512L * 1024 * 1024,
                            CpuPercent = 0
                        }
                    },
                    new MemoryStatus { PercentUsed = 50 }, DateTime.UtcNow,
                    AutopilotCause.Cpu, "NeckAutopilotProbe");
                if (applied.ApplicationsProtected != 1 || !EfficiencyModeManager.IsActive("NeckAutopilotProbe"))
                    throw new InvalidOperationException("O Autopilot não aplicou a proteção reversível ao processo de teste.");
                AutopilotProtectionResult restored = AutopilotProtectionManager.Stop();
                if (AutopilotProtectionManager.ActiveCount != 0 || EfficiencyModeManager.IsActive("NeckAutopilotProbe"))
                    throw new InvalidOperationException("O Autopilot não restaurou o processo protegido.");
                Console.WriteLine("AutopilotRoundTrip=" + applied.ProcessesChanged + "/" + restored.ProcessesChanged);
            }
            finally
            {
                AutopilotProtectionManager.Stop();
                EfficiencyModeManager.Restore("NeckAutopilotProbe");
                if (probe != null)
                {
                    try { if (!probe.HasExited) probe.Kill(); }
                    catch { }
                    probe.Dispose();
                }
                try { if (System.IO.File.Exists(probePath)) System.IO.File.Delete(probePath); }
                catch { }
            }
        }

        private static void TestEfficiencyModeRoundTrip()
        {
            string probePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NeckEcoProbe.exe");
            System.Diagnostics.Process probe = null;
            try
            {
                System.IO.File.Copy(Application.ExecutablePath, probePath, true);
                probe = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = probePath,
                    Arguments = "--efficiency-helper",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                Thread.Sleep(500);
                EfficiencyModeResult applied = EfficiencyModeManager.Apply("NeckEcoProbe");
                if (!applied.HasChanges || !EfficiencyModeManager.IsActive("NeckEcoProbe"))
                    throw new InvalidOperationException("Neck Adaptive não foi aplicado ao processo de teste.");
                if (applied.MemoryPriorityEffective < 1 || EfficiencyModeManager.GetState("NeckEcoProbe") != AdaptiveModeState.Optimized)
                    throw new InvalidOperationException("Neck Adaptive não confirmou baixa prioridade de memória no processo de teste.");
                if (applied.ProcessesParked < 1)
                    throw new InvalidOperationException("RAM Park não foi aplicado ao processo de teste.");
                DateTime transitionStart = DateTime.UtcNow;
                EfficiencyModeManager.RefreshAdaptiveModesForTesting("NeckEcoProbe", transitionStart);
                if (EfficiencyModeManager.GetState("NeckEcoProbe") != AdaptiveModeState.Foreground)
                    throw new InvalidOperationException("Neck Adaptive não restaurou o processo em primeiro plano.");
                EfficiencyModeManager.RefreshAdaptiveModesForTesting("OutroProcesso", transitionStart.AddSeconds(1));
                if (EfficiencyModeManager.GetState("NeckEcoProbe") != AdaptiveModeState.Waiting)
                    throw new InvalidOperationException("Neck Adaptive ignorou o período de espera após a troca de foco.");
                EfficiencyModeManager.RefreshAdaptiveModesForTesting("OutroProcesso", transitionStart.AddSeconds(17));
                if (EfficiencyModeManager.GetState("NeckEcoProbe") != AdaptiveModeState.Optimized)
                    throw new InvalidOperationException("Neck Adaptive não retomou a otimização em segundo plano.");
                EfficiencyModeResult restored = EfficiencyModeManager.Restore("NeckEcoProbe");
                if (!restored.HasChanges || EfficiencyModeManager.IsActive("NeckEcoProbe"))
                    throw new InvalidOperationException("Neck Adaptive não foi restaurado no processo de teste.");
                Console.WriteLine("AdaptiveRoundTrip=" + applied.ProcessesChanged + "/" + restored.ProcessesChanged +
                                  "; MemoryEffective=" + applied.MemoryPriorityEffective + "; MemoryChanged=" +
                                  applied.MemoryPriorityChanges + "/" + restored.MemoryPriorityChanges +
                                  "; Parked=" + applied.ProcessesParked + "; Released=" + applied.WorkingSetReleasedBytes);
            }
            finally
            {
                EfficiencyModeManager.Restore("NeckEcoProbe");
                if (probe != null)
                {
                    try { if (!probe.HasExited) probe.Kill(); }
                    catch { }
                    probe.Dispose();
                }
                try { if (System.IO.File.Exists(probePath)) System.IO.File.Delete(probePath); }
                catch { }
            }
        }

        private static void TestTurboModeRoundTrip()
        {
            string probePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NeckTurboProbe.exe");
            System.Diagnostics.Process probe = null;
            try
            {
                System.IO.File.Copy(Application.ExecutablePath, probePath, true);
                probe = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = probePath,
                    Arguments = "--efficiency-helper",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                Thread.Sleep(500);
                probe.Refresh();
                System.Diagnostics.ProcessPriorityClass originalPriority = probe.PriorityClass;
                TurboModeManager.Start("NeckTurboProbe", "Teste Turbo", 60);
                TurboModeManager.RefreshForTesting("NeckTurboProbe", DateTime.UtcNow);
                probe.Refresh();
                if (!TurboModeManager.IsActive || !TurboModeManager.IsForeground ||
                    probe.PriorityClass != System.Diagnostics.ProcessPriorityClass.AboveNormal)
                    throw new InvalidOperationException("Neck Turbo não acelerou o processo de teste em primeiro plano.");
                TurboModeManager.RefreshForTesting("OutroProcesso", DateTime.UtcNow.AddSeconds(2));
                probe.Refresh();
                if (TurboModeManager.IsForeground || probe.PriorityClass != originalPriority)
                    throw new InvalidOperationException("Neck Turbo não restaurou a prioridade ao perder o foco.");
                TurboModeResult stopped = TurboModeManager.Stop();
                if (TurboModeManager.IsActive) throw new InvalidOperationException("Neck Turbo permaneceu ativo após ser encerrado.");
                Console.WriteLine("TurboRoundTrip=OK; Restored=" + stopped.ProcessesChanged);
            }
            finally
            {
                TurboModeManager.Stop();
                if (probe != null)
                {
                    try { if (!probe.HasExited) probe.Kill(); }
                    catch { }
                    probe.Dispose();
                }
                try { if (System.IO.File.Exists(probePath)) System.IO.File.Delete(probePath); }
                catch { }
            }
        }

        private static void TestFocusModeRoundTrip()
        {
            string probePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NeckFocusProbe.exe");
            System.Diagnostics.Process probe = null;
            try
            {
                System.IO.File.Copy(Application.ExecutablePath, probePath, true);
                probe = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = probePath,
                    Arguments = "--efficiency-helper",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                Thread.Sleep(500);
                probe.Refresh();
                System.Diagnostics.ProcessPriorityClass originalPriority = probe.PriorityClass;
                FocusModeManager.Start("NeckFocusProbe", "Teste Acelerar", 60);
                if (!FocusModeManager.IsActive || !EfficiencyModeManager.IsActive("NeckFocusProbe"))
                    throw new InvalidOperationException("O modo Acelerar não combinou Turbo e Adaptive.");
                DateTime now = DateTime.UtcNow;
                EfficiencyModeManager.RefreshAdaptiveModesForTesting("NeckFocusProbe", now);
                TurboModeManager.RefreshForTesting("NeckFocusProbe", now);
                probe.Refresh();
                if (probe.PriorityClass != System.Diagnostics.ProcessPriorityClass.AboveNormal ||
                    FocusModeManager.GetStateLabel("NeckFocusProbe") != "Mais rápido agora")
                    throw new InvalidOperationException("O modo Acelerar não priorizou o aplicativo em uso.");
                FocusModeManager.Stop();
                probe.Refresh();
                if (FocusModeManager.IsActive || EfficiencyModeManager.IsActive("NeckFocusProbe") ||
                    probe.PriorityClass != originalPriority)
                    throw new InvalidOperationException("O modo Acelerar não restaurou todos os controles.");
                Console.WriteLine("FocusModeRoundTrip=OK");
            }
            finally
            {
                FocusModeManager.Stop();
                EfficiencyModeManager.Restore("NeckFocusProbe");
                if (probe != null)
                {
                    try { if (!probe.HasExited) probe.Kill(); }
                    catch { }
                    probe.Dispose();
                }
                try { if (System.IO.File.Exists(probePath)) System.IO.File.Delete(probePath); }
                catch { }
            }
        }

        private static void TestFocusShieldRoundTrip()
        {
            string probePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NeckShieldProbe.exe");
            System.Diagnostics.Process probe = null;
            try
            {
                System.Collections.Generic.List<SosCandidate> selectionInput = new System.Collections.Generic.List<SosCandidate>
                {
                    new SosCandidate { ProcessName = "TargetApp", VisibleWindows = 1, MemoryBytes = 2L * 1024 * 1024 * 1024 },
                    new SosCandidate { ProcessName = "WhatsApp", VisibleWindows = 1, MemoryBytes = 2L * 1024 * 1024 * 1024 },
                    new SosCandidate { ProcessName = "SmallApp", VisibleWindows = 1, MemoryBytes = 100L * 1024 * 1024 },
                    new SosCandidate { ProcessName = "HeavyApp", VisibleWindows = 1, MemoryBytes = 900L * 1024 * 1024 }
                };
                System.Collections.Generic.List<SosCandidate> selected = FocusShieldManager.SelectCandidates(
                    selectionInput, "TargetApp", new MemoryStatus { PercentUsed = 82 });
                if (selected.Count != 1 || selected[0].ProcessName != "HeavyApp")
                    throw new InvalidOperationException("O Escudo de Foco não respeitou alvo, sensibilidade e tamanho mínimo.");
                if (FocusShieldManager.SelectCandidates(selectionInput, "TargetApp", new MemoryStatus { PercentUsed = 60 }).Count != 0)
                    throw new InvalidOperationException("O Escudo de Foco foi ativado sem pressão de memória.");
                selectionInput.Add(new SosCandidate
                {
                    ProcessName = "CpuHeavyApp",
                    VisibleWindows = 1,
                    MemoryBytes = 120L * 1024 * 1024,
                    CpuPercent = 24
                });
                System.Collections.Generic.List<SosCandidate> cpuSelected = FocusShieldManager.SelectCandidates(
                    selectionInput, "TargetApp", new MemoryStatus { PercentUsed = 60 });
                if (cpuSelected.Count != 1 || cpuSelected[0].ProcessName != "CpuHeavyApp")
                    throw new InvalidOperationException("O Escudo de Foco não reagiu a um concorrente pesado de CPU.");

                System.IO.File.Copy(Application.ExecutablePath, probePath, true);
                probe = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = probePath,
                    Arguments = "--efficiency-helper",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                Thread.Sleep(500);
                probe.Refresh();
                System.Diagnostics.ProcessPriorityClass originalPriority = probe.PriorityClass;
                FocusShieldResult activated = FocusShieldManager.RefreshForTesting("NeckFocusProbe", true,
                    new[]
                    {
                        new SosCandidate
                        {
                            ProcessName = "NeckShieldProbe",
                            DisplayName = "Concorrente de teste",
                            VisibleWindows = 1,
                            MemoryBytes = 800L * 1024 * 1024
                        }
                    },
                    new MemoryStatus { PercentUsed = 82 }, DateTime.UtcNow);
                probe.Refresh();
                if (activated.ApplicationsShielded != 1 || !EfficiencyModeManager.IsActive("NeckShieldProbe") ||
                    probe.PriorityClass != System.Diagnostics.ProcessPriorityClass.BelowNormal)
                    throw new InvalidOperationException("O Escudo de Foco não reduziu o concorrente em segundo plano.");
                FocusShieldManager.RefreshForTesting("NeckFocusProbe", false, null, new MemoryStatus(), DateTime.UtcNow.AddSeconds(1));
                probe.Refresh();
                if (FocusShieldManager.ActiveCount != 0 || EfficiencyModeManager.IsActive("NeckShieldProbe") ||
                    probe.PriorityClass != originalPriority)
                    throw new InvalidOperationException("O Escudo de Foco não restaurou o concorrente ao perder o foco.");
                Console.WriteLine("FocusShieldRoundTrip=OK");
            }
            finally
            {
                FocusShieldManager.Stop();
                EfficiencyModeManager.Restore("NeckShieldProbe");
                if (probe != null)
                {
                    try { if (!probe.HasExited) probe.Kill(); }
                    catch { }
                    probe.Dispose();
                }
                try { if (System.IO.File.Exists(probePath)) System.IO.File.Delete(probePath); }
                catch { }
            }
        }

        private static void TestProcessFamilyDiscovery()
        {
            System.Collections.Generic.List<ProcessTreeEntry> entries = new System.Collections.Generic.List<ProcessTreeEntry>
            {
                new ProcessTreeEntry { ProcessId = 10, ParentProcessId = 1, ProcessName = "claude" },
                new ProcessTreeEntry { ProcessId = 11, ParentProcessId = 10, ProcessName = "chrome" },
                new ProcessTreeEntry { ProcessId = 12, ParentProcessId = 11, ProcessName = "node" },
                new ProcessTreeEntry { ProcessId = 20, ParentProcessId = 1, ProcessName = "chrome" },
                new ProcessTreeEntry { ProcessId = 13, ParentProcessId = 10, ProcessName = "svchost" }
            };
            System.Collections.Generic.HashSet<int> family = ProcessFamilyInspector.BuildFamilyIds("claude", entries);
            if (!family.Contains(10) || !family.Contains(11) || !family.Contains(12) || family.Contains(20) || family.Contains(13))
                throw new InvalidOperationException("A descoberta de família de processos aceitou ou rejeitou um descendente incorretamente.");
        }
    }
}
