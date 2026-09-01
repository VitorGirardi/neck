using System;
using System.IO;

namespace Neck
{
    internal static class BluetoothPowerResetCoordinator
    {
        internal static string BuildShutdownArguments()
        {
            return "/s /t 0 /d p:0:0 /c \"Neck - reset eletrico do Bluetooth\"";
        }

        internal static bool IsSafeFullShutdownPlan(string arguments)
        {
            string value = " " + (arguments ?? "").Trim().ToLowerInvariant() + " ";
            return value.Contains(" /s ") && value.Contains(" /t 0 ") &&
                   !value.Contains(" /f ") && !value.Contains(" /hybrid ") &&
                   !value.Contains(" /r ") && !value.Contains(" /g ") &&
                   !value.Contains(" /p ");
        }

        public static ProcessResult StartFullShutdown()
        {
            string arguments = BuildShutdownArguments();
            if (!IsSafeFullShutdownPlan(arguments))
                return new ProcessResult { ExitCode = 87, Output = "O plano de desligamento não passou pela validação de segurança." };

            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string system = Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess ? "Sysnative" : "System32";
            string shutdown = Path.Combine(windows, system, "shutdown.exe");
            if (!File.Exists(shutdown))
                return new ProcessResult { ExitCode = 2, Output = "A ferramenta oficial de desligamento do Windows não foi encontrada." };

            return ProcessRunner.Run(shutdown, arguments, 10000);
        }
    }
}
