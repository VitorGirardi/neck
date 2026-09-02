using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Neck
{
    internal static class SoakTest
    {
        private sealed class ResourceReading
        {
            public long PrivateBytes;
            public int Handles;
            public int Threads;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ThreadEntry
        {
            public uint Size;
            public uint Usage;
            public uint ThreadId;
            public uint OwnerProcessId;
            public int BasePriority;
            public int DeltaPriority;
            public uint Flags;
        }

        private const uint SnapshotThreads = 0x00000004;
        private static readonly IntPtr InvalidHandle = new IntPtr(-1);

        private static int Main(string[] args)
        {
            int durationSeconds = ReadArgument(args, "--duration-seconds", 600);
            int sampleSeconds = ReadArgument(args, "--sample-seconds", 2);
            if (durationSeconds < 20 || sampleSeconds < 1) return 2;

            string baselinePath = Path.Combine(Path.GetTempPath(), "neck-soak-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                return Run(durationSeconds, sampleSeconds, baselinePath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("SOAK_TEST_FAILED");
                Console.Error.WriteLine(ex);
                return 1;
            }
            finally
            {
                try { if (File.Exists(baselinePath)) File.Delete(baselinePath); }
                catch { }
                try { if (File.Exists(baselinePath + ".tmp")) File.Delete(baselinePath + ".tmp"); }
                catch { }
            }
        }

        private static int Run(int durationSeconds, int sampleSeconds, string baselinePath)
        {
            int captures = 0;
            int failures = 0;
            ResourceReading start;
            ResourceReading finish;
            ResourceReading peak = new ResourceReading();
            double averageCpuPercent;

            using (ReplayProbe probe = new ReplayProbe())
            using (BaselineEngine baseline = new BaselineEngine(baselinePath))
            {
                ReplayEngine replay = new ReplayEngine();
                FlowConsensusEngine consensus = new FlowConsensusEngine();

                for (int i = 0; i < 3; i++)
                {
                    probe.Capture(0);
                    Thread.Sleep(250);
                }
                ForceCollection();
                start = ReadResources();
                peak.PrivateBytes = start.PrivateBytes;
                peak.Handles = start.Handles;
                peak.Threads = start.Threads;
                TimeSpan processorStart = ReadProcessorTime();
                Stopwatch elapsed = Stopwatch.StartNew();

                while (elapsed.Elapsed.TotalSeconds < durationSeconds)
                {
                    Stopwatch iteration = Stopwatch.StartNew();
                    try
                    {
                        ReplayCapture capture = probe.Capture(0);
                        if (capture == null || capture.Sample == null || capture.Health == null)
                            throw new InvalidOperationException("A captura não retornou todos os sinais.");
                        replay.Record(capture.Sample);
                        BaselineEvaluation evaluation = baseline.Observe(capture.Sample, false);
                        FlowConsensusDecision decision = consensus.Evaluate(capture.Health, capture.Sample, evaluation);
                        if (decision == null || decision.Advice == null)
                            throw new InvalidOperationException("O consenso não retornou uma decisão.");
                        captures++;
                    }
                    catch (Exception ex)
                    {
                        failures++;
                        Console.Error.WriteLine("Falha de captura " + failures + ": " + ex.GetType().Name + " — " + ex.Message);
                    }

                    ResourceReading current = ReadResources();
                    peak.PrivateBytes = Math.Max(peak.PrivateBytes, current.PrivateBytes);
                    peak.Handles = Math.Max(peak.Handles, current.Handles);
                    peak.Threads = Math.Max(peak.Threads, current.Threads);
                    int remaining = sampleSeconds * 1000 - (int)iteration.ElapsedMilliseconds;
                    if (remaining > 0) Thread.Sleep(remaining);
                }

                elapsed.Stop();
                ForceCollection();
                finish = ReadResources();
                TimeSpan processorUsed = ReadProcessorTime() - processorStart;
                averageCpuPercent = elapsed.Elapsed.TotalMilliseconds <= 0 ? 0 :
                    processorUsed.TotalMilliseconds * 100d /
                    (elapsed.Elapsed.TotalMilliseconds * Math.Max(1, Environment.ProcessorCount));
            }

            long privateDrift = finish.PrivateBytes - start.PrivateBytes;
            int handleDrift = finish.Handles - start.Handles;
            int threadDrift = finish.Threads - start.Threads;
            long privatePeakGrowth = peak.PrivateBytes - start.PrivateBytes;
            bool passed = captures >= Math.Max(3, durationSeconds / Math.Max(1, sampleSeconds) - 2) && failures == 0 &&
                privateDrift <= 48L * 1024 * 1024 && privatePeakGrowth <= 96L * 1024 * 1024 &&
                handleDrift <= 48 && threadDrift <= 4 && averageCpuPercent <= 5d;

            Console.WriteLine("SOAK_TEST_" + (passed ? "OK" : "FAILED"));
            Console.WriteLine("DurationSeconds=" + durationSeconds);
            Console.WriteLine("Captures=" + captures);
            Console.WriteLine("Failures=" + failures);
            Console.WriteLine("PrivateStartBytes=" + start.PrivateBytes);
            Console.WriteLine("PrivateEndBytes=" + finish.PrivateBytes);
            Console.WriteLine("PrivateDriftBytes=" + privateDrift);
            Console.WriteLine("PrivatePeakGrowthBytes=" + privatePeakGrowth);
            Console.WriteLine("HandleDrift=" + handleDrift);
            Console.WriteLine("ThreadDrift=" + threadDrift);
            Console.WriteLine("AverageMachineCpuPercent=" + averageCpuPercent.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
            return passed ? 0 : 1;
        }

        private static ResourceReading ReadResources()
        {
            long privateBytes;
            using (Process process = Process.GetCurrentProcess())
                privateBytes = process.PrivateMemorySize64;
            uint handles;
            if (!GetProcessHandleCount(GetCurrentProcess(), out handles)) handles = 0;
            return new ResourceReading
            {
                PrivateBytes = privateBytes,
                Handles = (int)handles,
                Threads = CountCurrentThreads()
            };
        }

        private static TimeSpan ReadProcessorTime()
        {
            using (Process process = Process.GetCurrentProcess()) return process.TotalProcessorTime;
        }

        private static int CountCurrentThreads()
        {
            IntPtr snapshot = CreateToolhelp32Snapshot(SnapshotThreads, 0);
            if (snapshot == InvalidHandle) return 0;
            try
            {
                uint ownId = GetCurrentProcessId();
                int count = 0;
                ThreadEntry entry = new ThreadEntry { Size = (uint)Marshal.SizeOf(typeof(ThreadEntry)) };
                if (!Thread32First(snapshot, ref entry)) return 0;
                do
                {
                    if (entry.OwnerProcessId == ownId) count++;
                    entry.Size = (uint)Marshal.SizeOf(typeof(ThreadEntry));
                }
                while (Thread32Next(snapshot, ref entry));
                return count;
            }
            finally { CloseHandle(snapshot); }
        }

        private static void ForceCollection()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private static int ReadArgument(string[] args, string name, int fallback)
        {
            if (args == null) return fallback;
            for (int i = 0; i + 1 < args.Length; i++)
            {
                int value;
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && int.TryParse(args[i + 1], out value))
                    return value;
            }
            return fallback;
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetProcessHandleCount(IntPtr process, out uint handleCount);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Thread32First(IntPtr snapshot, ref ThreadEntry entry);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Thread32Next(IntPtr snapshot, ref ThreadEntry entry);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}
