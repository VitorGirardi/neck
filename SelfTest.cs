using System;

namespace MestrePCCare
{
    internal static class SelfTest
    {
        private static int Main()
        {
            try
            {
                ScanResult result = Cleaner.Analyze();
                MemoryStatus memory = SystemInfo.GetMemoryStatus();
                Console.WriteLine("SELF_TEST_OK");
                Console.WriteLine("TempBytes=" + result.TempBytes);
                Console.WriteLine("TempFiles=" + result.TempFiles);
                Console.WriteLine("ReportBytes=" + result.ReportBytes);
                Console.WriteLine("ReportFiles=" + result.ReportFiles);
                Console.WriteLine("AccessErrors=" + result.AccessErrors);
                Console.WriteLine("MemoryPercent=" + memory.PercentUsed.ToString("0.0"));
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
