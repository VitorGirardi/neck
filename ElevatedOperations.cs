using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neck
{
    internal sealed class ElevatedTaskResult
    {
        public int ExitCode;
        public string Output = "";
        public bool Cancelled;
    }

    internal static class ElevatedOperations
    {
        private static readonly HashSet<string> AllowedTasks = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "components", "health", "drives", "restorepoint", "bluetooth"
        };

        private static string JobsDirectory
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Neck", "jobs"); }
        }

        public static bool IsElevatedInvocation(string[] args)
        {
            return args != null && args.Length == 3 && string.Equals(args[0], "--elevated-maintenance", StringComparison.OrdinalIgnoreCase);
        }

        public static int ExecuteElevatedInvocation(string[] args)
        {
            if (!IsElevatedInvocation(args) || !SecurityHelper.IsAdministrator()) return 740;
            string[] tasks = ParseTasks(args[1]);
            if (tasks.Length == 0) return 87;
            string resultPath;
            try
            {
                resultPath = Path.GetFullPath(args[2]);
                string allowedRoot = Path.GetFullPath(JobsDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!resultPath.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(Path.GetExtension(resultPath), ".result", StringComparison.OrdinalIgnoreCase)) return 87;
                Directory.CreateDirectory(allowedRoot);
            }
            catch { return 87; }

            ElevatedTaskResult result = ExecuteTasks(tasks);
            try
            {
                File.WriteAllText(resultPath,
                    result.ExitCode.ToString(CultureInfo.InvariantCulture) + Environment.NewLine + result.Output,
                    new UTF8Encoding(false));
            }
            catch { return 5; }
            return result.ExitCode;
        }

        public static async Task<ElevatedTaskResult> RunAsync(IEnumerable<string> requestedTasks)
        {
            string[] tasks = requestedTasks == null ? new string[0] : ParseTasks(string.Join(",", requestedTasks));
            if (tasks.Length == 0) return new ElevatedTaskResult { ExitCode = 87, Output = "Nenhuma tarefa administrativa válida foi solicitada." };
            Directory.CreateDirectory(JobsDirectory);
            string resultPath = Path.Combine(JobsDirectory, Guid.NewGuid().ToString("N") + ".result");
            string arguments = "--elevated-maintenance " + Quote(string.Join(",", tasks)) + " " + Quote(resultPath);
            try
            {
                using (Process process = Process.Start(new ProcessStartInfo
                {
                    FileName = System.Windows.Forms.Application.ExecutablePath,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath) ?? Environment.CurrentDirectory,
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                }))
                {
                    bool exited = await Task.Run(delegate { return process.WaitForExit(70 * 60 * 1000); });
                    if (!exited) return new ElevatedTaskResult { ExitCode = -2, Output = "A tarefa administrativa ultrapassou o tempo limite e pode ainda estar em execução." };
                }
                for (int attempt = 0; attempt < 10 && !File.Exists(resultPath); attempt++) await Task.Delay(150);
                if (!File.Exists(resultPath)) return new ElevatedTaskResult { ExitCode = -1, Output = "O processo elevado terminou sem produzir um resultado." };
                string[] lines = File.ReadAllLines(resultPath, Encoding.UTF8);
                int exitCode;
                if (lines.Length == 0 || !int.TryParse(lines[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out exitCode)) exitCode = -1;
                return new ElevatedTaskResult { ExitCode = exitCode, Output = string.Join(Environment.NewLine, lines.Skip(1).ToArray()) };
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode == 1223) return new ElevatedTaskResult { ExitCode = 1223, Cancelled = true, Output = "A permissão de administrador foi cancelada." };
                return new ElevatedTaskResult { ExitCode = ex.NativeErrorCode, Output = ex.Message };
            }
            catch (Exception ex)
            {
                return new ElevatedTaskResult { ExitCode = -1, Output = ex.Message };
            }
            finally
            {
                try { if (File.Exists(resultPath)) File.Delete(resultPath); } catch { }
            }
        }

        internal static string[] ParseTasks(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return new string[0];
            string[] requested = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim().ToLowerInvariant()).Distinct().ToArray();
            return requested.All(AllowedTasks.Contains) ? requested : new string[0];
        }

        private static ElevatedTaskResult ExecuteTasks(IEnumerable<string> tasks)
        {
            StringBuilder output = new StringBuilder();
            int overallExit = 0;
            foreach (string task in tasks)
            {
                if (task == "components")
                    AppendResult(output, "DISM Component Cleanup", ProcessRunner.Run("dism.exe", "/Online /Cleanup-Image /StartComponentCleanup /NoRestart", 45 * 60 * 1000), ref overallExit);
                else if (task == "health")
                {
                    AppendResult(output, "DISM ScanHealth", ProcessRunner.Run("dism.exe", "/Online /Cleanup-Image /ScanHealth /NoRestart", 45 * 60 * 1000), ref overallExit);
                    AppendResult(output, "SFC VerifyOnly", ProcessRunner.Run("sfc.exe", "/verifyonly", 45 * 60 * 1000), ref overallExit);
                }
                else if (task == "drives")
                {
                    string systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
                    AppendResult(output, "Otimização da unidade " + systemDrive, ProcessRunner.Run("defrag.exe", systemDrive + " /O /H /U /V", 60 * 60 * 1000), ref overallExit);
                }
                else if (task == "restorepoint")
                {
                    string script = "Checkpoint-Computer -Description 'Neck - antes dos drivers' -RestorePointType MODIFY_SETTINGS";
                    string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
                    AppendResult(output, "Ponto de restauração", ProcessRunner.Run(powershell,
                        "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + script + "\"", 180000), ref overallExit);
                }
                else if (task == "bluetooth")
                {
                    BluetoothRepairResult repair = BluetoothRepairEngine.Repair();
                    output.AppendLine(repair.Output.Trim());
                    output.AppendLine();
                    if (repair.ExitCode != 0 && overallExit == 0) overallExit = repair.ExitCode;
                }
            }
            return new ElevatedTaskResult { ExitCode = overallExit, Output = output.ToString() };
        }

        private static void AppendResult(StringBuilder output, string title, ProcessResult result, ref int overallExit)
        {
            output.AppendLine(title + " — código " + result.ExitCode);
            if (!string.IsNullOrWhiteSpace(result.Output)) output.AppendLine(result.Output.Trim());
            output.AppendLine();
            if (result.ExitCode != 0 && overallExit == 0) overallExit = result.ExitCode;
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }
    }
}
