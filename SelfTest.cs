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
                BluetoothSnapshot bluetooth = BluetoothDoctor.Read();
                if (bluetooth.CapturedUtc == DateTime.MinValue)
                    throw new InvalidOperationException("O diagnóstico Bluetooth não registrou o horário da leitura.");
                if (!BluetoothRepairEngine.IsSafeAdapterId(@"USB\VID_13D3&PID_3567\TESTE"))
                    throw new InvalidOperationException("Um adaptador Bluetooth físico válido foi recusado.");
                if (BluetoothRepairEngine.IsSafeAdapterId(@"BTHENUM\DEV_TESTE") || BluetoothRepairEngine.IsSafeAdapterId("USB\\TESTE\" MALICIOSO"))
                    throw new InvalidOperationException("Um identificador Bluetooth não confiável foi aceito para reparo.");
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
                TestTurboModeRoundTrip();
                TestFocusModeRoundTrip();
                TestFocusShieldRoundTrip();
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
