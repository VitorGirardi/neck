using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Neck
{
    internal struct MemoryStatus
    {
        public double PercentUsed;
        public ulong AvailableBytes;
    }

    internal enum HealthLevel { Stable, Warning, Critical }

    internal sealed class ResourceProcess
    {
        public string DisplayName;
        public int ProcessCount;
        public long MemoryBytes;
    }

    internal sealed class HealthSnapshot
    {
        public int Score;
        public HealthLevel Level;
        public string Title = "Diagnóstico indisponível";
        public string Summary = "Não foi possível concluir a leitura agora.";
        public MemoryStatus Memory;
        public long DiskFreeBytes;
        public long DiskTotalBytes;
        public List<ResourceProcess> TopProcesses = new List<ResourceProcess>();
    }

    internal enum MeetingCheckStatus { Ready, Warning, Risk }

    internal sealed class MeetingCheck
    {
        public MeetingCheckStatus Status;
        public string Title;
        public string Message;
    }

    internal sealed class MeetingPreflight
    {
        public HealthSnapshot Health = new HealthSnapshot();
        public List<MeetingCheck> Checks = new List<MeetingCheck>();
    }

    internal static class SystemInfo
    {
        public static MemoryStatus GetMemoryStatus()
        {
            NativeMethods.MEMORYSTATUSEX data = new NativeMethods.MEMORYSTATUSEX();
            data.dwLength = (uint)Marshal.SizeOf(typeof(NativeMethods.MEMORYSTATUSEX));
            if (!NativeMethods.GlobalMemoryStatusEx(ref data)) return new MemoryStatus();
            return new MemoryStatus { PercentUsed = data.dwMemoryLoad, AvailableBytes = data.ullAvailPhys };
        }

        public static HealthSnapshot GetHealthSnapshot()
        {
            HealthSnapshot snapshot = new HealthSnapshot();
            snapshot.Memory = GetMemoryStatus();
            try
            {
                string root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
                DriveInfo drive = new DriveInfo(root);
                snapshot.DiskFreeBytes = drive.AvailableFreeSpace;
                snapshot.DiskTotalBytes = drive.TotalSize;
            }
            catch { }

            Dictionary<string, ResourceProcess> grouped = new Dictionary<string, ResourceProcess>(StringComparer.OrdinalIgnoreCase);
            string currentName = Process.GetCurrentProcess().ProcessName;
            foreach (Process process in Process.GetProcesses())
            {
                using (process)
                {
                    try
                    {
                        string name = process.ProcessName;
                        if (string.Equals(name, currentName, StringComparison.OrdinalIgnoreCase)) continue;
                        long memory = Math.Max(0, process.WorkingSet64);
                        ResourceProcess item;
                        if (!grouped.TryGetValue(name, out item))
                        {
                            item = new ResourceProcess { DisplayName = FriendlyProcessName(name) };
                            grouped.Add(name, item);
                        }
                        item.ProcessCount++;
                        item.MemoryBytes += memory;
                    }
                    catch { }
                }
            }
            snapshot.TopProcesses = grouped.Values.OrderByDescending(item => item.MemoryBytes).Take(5).ToList();

            bool diskCritical = snapshot.DiskTotalBytes > 0 &&
                                (snapshot.DiskFreeBytes < 2L * 1024 * 1024 * 1024 ||
                                 snapshot.DiskFreeBytes * 100 / snapshot.DiskTotalBytes < 5);
            bool diskWarning = snapshot.DiskTotalBytes > 0 && snapshot.DiskFreeBytes < 15L * 1024 * 1024 * 1024;
            snapshot.Level = snapshot.Memory.PercentUsed >= 90 || diskCritical ? HealthLevel.Critical :
                             snapshot.Memory.PercentUsed >= 75 || diskWarning ? HealthLevel.Warning : HealthLevel.Stable;

            int memoryPenalty = (int)Math.Max(0, (snapshot.Memory.PercentUsed - 55) * 1.35);
            int diskPenalty = diskCritical ? 30 : diskWarning ? 15 : 0;
            snapshot.Score = Math.Max(10, Math.Min(100, 100 - memoryPenalty - diskPenalty));
            ResourceProcess top = snapshot.TopProcesses.FirstOrDefault();
            if (snapshot.Level == HealthLevel.Critical)
            {
                snapshot.Title = "Pressão alta detectada";
                snapshot.Summary = snapshot.Memory.PercentUsed >= 90
                    ? "A memória está quase cheia. " + TopProcessSentence(top)
                    : "O disco do Windows está praticamente cheio e pode deixar todo o sistema lento.";
            }
            else if (snapshot.Level == HealthLevel.Warning)
            {
                snapshot.Title = "O computador merece atenção";
                snapshot.Summary = snapshot.Memory.PercentUsed >= 75
                    ? "O uso de memória está elevado. " + TopProcessSentence(top)
                    : "Há pouco espaço livre no disco do Windows; uma limpeza segura pode ajudar.";
            }
            else
            {
                snapshot.Title = "Sistema estável agora";
                snapshot.Summary = "Não encontramos pressão crítica de memória ou disco. " + TopProcessSentence(top);
            }
            return snapshot;
        }

        public static MeetingPreflight GetMeetingPreflight()
        {
            MeetingPreflight preflight = new MeetingPreflight();
            preflight.Health = GetHealthSnapshot();
            HealthSnapshot health = preflight.Health;
            preflight.Checks.Add(new MeetingCheck
            {
                Status = health.Memory.PercentUsed >= 90 ? MeetingCheckStatus.Risk :
                         health.Memory.PercentUsed >= 75 ? MeetingCheckStatus.Warning : MeetingCheckStatus.Ready,
                Title = "Memória RAM",
                Message = health.Memory.PercentUsed.ToString("0", CultureInfo.CurrentCulture) + "% em uso; " +
                          MainForm.FormatBytes((long)health.Memory.AvailableBytes) + " disponíveis."
            });
            bool diskRisk = health.DiskTotalBytes > 0 &&
                            (health.DiskFreeBytes < 2L * 1024 * 1024 * 1024 || health.DiskFreeBytes * 100 / health.DiskTotalBytes < 5);
            bool diskWarning = health.DiskTotalBytes > 0 && health.DiskFreeBytes < 15L * 1024 * 1024 * 1024;
            preflight.Checks.Add(new MeetingCheck
            {
                Status = diskRisk ? MeetingCheckStatus.Risk : diskWarning ? MeetingCheckStatus.Warning : MeetingCheckStatus.Ready,
                Title = "Disco do Windows",
                Message = MainForm.FormatBytes(health.DiskFreeBytes) + " livres para arquivos e memória virtual."
            });
            bool restartPending = IsRestartPending();
            preflight.Checks.Add(new MeetingCheck
            {
                Status = restartPending ? MeetingCheckStatus.Warning : MeetingCheckStatus.Ready,
                Title = "Reinicialização",
                Message = restartPending ? "O Windows indica uma reinicialização pendente." : "Nenhuma reinicialização pendente foi detectada."
            });
            bool networkAvailable = false;
            try { networkAvailable = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable(); } catch { }
            preflight.Checks.Add(new MeetingCheck
            {
                Status = networkAvailable ? MeetingCheckStatus.Ready : MeetingCheckStatus.Warning,
                Title = "Rede",
                Message = networkAvailable ? "Uma conexão de rede ativa foi detectada." : "Nenhuma conexão de rede ativa foi detectada."
            });
            PowerStatus power = SystemInformation.PowerStatus;
            bool onBattery = power.PowerLineStatus == PowerLineStatus.Offline;
            string powerMessage;
            if (onBattery)
            {
                int battery = power.BatteryLifePercent >= 0 && power.BatteryLifePercent <= 1
                    ? (int)Math.Round(power.BatteryLifePercent * 100) : 0;
                powerMessage = "Usando bateria" + (battery > 0 ? " com " + battery + "% de carga." : ".");
            }
            else if (power.PowerLineStatus == PowerLineStatus.Online) powerMessage = "O computador está conectado à energia.";
            else powerMessage = "Estado de energia não aplicável ou não informado.";
            preflight.Checks.Add(new MeetingCheck
            {
                Status = onBattery ? MeetingCheckStatus.Warning : MeetingCheckStatus.Ready,
                Title = "Energia",
                Message = powerMessage
            });
            ResourceProcess top = health.TopProcesses.FirstOrDefault();
            bool heavy = top != null && top.MemoryBytes >= 3L * 1024 * 1024 * 1024;
            preflight.Checks.Add(new MeetingCheck
            {
                Status = heavy ? MeetingCheckStatus.Warning : MeetingCheckStatus.Ready,
                Title = "Aplicativos pesados",
                Message = top == null ? "Nenhum aplicativo pôde ser comparado." :
                          top.DisplayName + " lidera o uso com aproximadamente " + MainForm.FormatBytes(top.MemoryBytes) + "."
            });
            return preflight;
        }

        public static bool IsForegroundWindowFullScreen()
        {
            try
            {
                IntPtr window = NativeMethods.GetForegroundWindow();
                if (window == IntPtr.Zero) return false;
                NativeMethods.RECT bounds;
                if (!NativeMethods.GetWindowRect(window, out bounds)) return false;
                Screen screen = Screen.FromHandle(window);
                Rectangle area = screen.Bounds;
                return Math.Abs(bounds.Left - area.Left) <= 2 && Math.Abs(bounds.Top - area.Top) <= 2 &&
                       Math.Abs(bounds.Right - area.Right) <= 2 && Math.Abs(bounds.Bottom - area.Bottom) <= 2;
            }
            catch { return false; }
        }

        private static bool IsRestartPending()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey cbs = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"))
                    if (cbs != null) return true;
                using (Microsoft.Win32.RegistryKey update = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired"))
                    if (update != null) return true;
                using (Microsoft.Win32.RegistryKey session = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\Session Manager"))
                    if (session != null && session.GetValue("PendingFileRenameOperations") != null) return true;
            }
            catch { }
            return false;
        }

        private static string TopProcessSentence(ResourceProcess process)
        {
            return process == null ? "Nenhum aplicativo pôde ser comparado neste momento." :
                process.DisplayName + " é o maior consumidor de memória agora.";
        }

        private static string FriendlyProcessName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Processo desconhecido";
            if (string.Equals(name, "msedge", StringComparison.OrdinalIgnoreCase)) return "Microsoft Edge";
            if (string.Equals(name, "chrome", StringComparison.OrdinalIgnoreCase)) return "Google Chrome";
            if (string.Equals(name, "firefox", StringComparison.OrdinalIgnoreCase)) return "Mozilla Firefox";
            if (string.Equals(name, "explorer", StringComparison.OrdinalIgnoreCase)) return "Explorador do Windows";
            if (string.Equals(name, "Teams", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "ms-teams", StringComparison.OrdinalIgnoreCase)) return "Microsoft Teams";
            if (string.Equals(name, "Code", StringComparison.OrdinalIgnoreCase)) return "Visual Studio Code";
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.Replace('_', ' '));
        }
    }

    internal static class NativeMethods
    {
        public const uint SHERB_NOCONFIRMATION = 0x00000001;
        public const uint SHERB_NOPROGRESSUI = 0x00000002;
        public const uint SHERB_NOSOUND = 0x00000004;
        public const uint ES_CONTINUOUS = 0x80000000;
        public const uint ES_SYSTEM_REQUIRED = 0x00000001;
        public const uint ES_DISPLAY_REQUIRED = 0x00000002;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint SetThreadExecutionState(uint esFlags);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);
    }
}
