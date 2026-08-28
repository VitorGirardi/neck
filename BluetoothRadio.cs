using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Neck
{
    internal enum BluetoothPowerState
    {
        Unknown,
        On,
        Off,
        Disabled
    }

    internal static class BluetoothRadioController
    {
        private const string RadioTypeName = "Windows.Devices.Radios.Radio, Windows, ContentType=WindowsRuntime";
        private const string RuntimeExtensionsTypeName = "System.WindowsRuntimeSystemExtensions, System.Runtime.WindowsRuntime";

        public static BluetoothPowerState ReadState()
        {
            try
            {
                Type radioType = Type.GetType(RadioTypeName, true);
                MethodInfo getRadios = radioType.GetMethod("GetRadiosAsync", BindingFlags.Public | BindingFlags.Static);
                object radios = AwaitOperation(getRadios.Invoke(null, null), ResultType(getRadios)).GetAwaiter().GetResult();
                object bluetooth = FindBluetooth(radios);
                BluetoothPowerState state = bluetooth == null ? BluetoothPowerState.Unknown : Map(ReadProperty(bluetooth, "State"));
                return state == BluetoothPowerState.Unknown ? ReadStateWithWindowsPowerShell() : state;
            }
            catch { return ReadStateWithWindowsPowerShell(); }
        }

        private static async Task<object> AwaitOperation(object operation, Type resultType)
        {
            if (operation == null) throw new InvalidOperationException("A operação do Windows não foi criada.");
            Type extensions = Type.GetType(RuntimeExtensionsTypeName, true);
            MethodInfo asTask = extensions.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(item => item.Name == "AsTask" && item.IsGenericMethodDefinition && item.GetParameters().Length == 1);
            Task task = (Task)asTask.MakeGenericMethod(resultType).Invoke(null, new[] { operation });
            await task;
            return task.GetType().GetProperty("Result").GetValue(task, null);
        }

        private static Type ResultType(MethodInfo method)
        {
            Type[] arguments = method.ReturnType.GetGenericArguments();
            if (arguments.Length != 1) throw new InvalidOperationException("A operação do rádio não informou o tipo de resultado.");
            return arguments[0];
        }

        private static BluetoothPowerState ReadStateWithWindowsPowerShell()
        {
            try
            {
                string script =
                    "Add-Type -AssemblyName System.Runtime.WindowsRuntime;" +
                    "[Windows.Devices.Radios.Radio,Windows.System.Devices,ContentType=WindowsRuntime]|Out-Null;" +
                    "$o=[Windows.Devices.Radios.Radio]::GetRadiosAsync();" +
                    "$m=[System.WindowsRuntimeSystemExtensions].GetMethods()|?{$_.Name -eq 'AsTask' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 1}|select -First 1;" +
                    "$t=$m.MakeGenericMethod([System.Collections.Generic.IReadOnlyList[Windows.Devices.Radios.Radio]]).Invoke($null,@($o));" +
                    "$t.Wait();" +
                    "($t.Result|? Kind -eq Bluetooth|select -First 1).State.ToString()";
                string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
                string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
                ProcessResult result = ProcessRunner.Run(powershell, "-NoProfile -NonInteractive -EncodedCommand " + encoded, 15000);
                string value = (result.Output ?? "").Trim();
                string[] lines = value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(item => item.Trim().TrimStart('\uFEFF')).ToArray();
                if (lines.Any(item => string.Equals(item, "On", StringComparison.OrdinalIgnoreCase))) return BluetoothPowerState.On;
                if (lines.Any(item => string.Equals(item, "Off", StringComparison.OrdinalIgnoreCase))) return BluetoothPowerState.Off;
                if (lines.Any(item => string.Equals(item, "Disabled", StringComparison.OrdinalIgnoreCase))) return BluetoothPowerState.Disabled;
            }
            catch { }
            return BluetoothPowerState.Unknown;
        }

        private static object FindBluetooth(object radios)
        {
            IEnumerable items = radios as IEnumerable;
            if (items == null) return null;
            foreach (object item in items)
                if (string.Equals(ReadProperty(item, "Kind"), "Bluetooth", StringComparison.OrdinalIgnoreCase)) return item;
            return null;
        }

        private static string ReadProperty(object instance, string name)
        {
            if (instance == null) return "";
            object value = instance.GetType().GetProperty(name).GetValue(instance, null);
            return Convert.ToString(value) ?? "";
        }

        private static BluetoothPowerState Map(string value)
        {
            BluetoothPowerState state;
            return Enum.TryParse(value, true, out state) ? state : BluetoothPowerState.Unknown;
        }

    }
}
