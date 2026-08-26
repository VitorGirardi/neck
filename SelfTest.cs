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
                using (MainForm form = new MainForm())
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
                    form.Close();
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
