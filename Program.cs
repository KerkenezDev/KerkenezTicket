using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using KerkenezTicket.Services;
using KerkenezTicket.UI;

namespace KerkenezTicket
{
    static class Program
    {
        private const string MainUiMutexName = @"Global\KerkenezTicket_MainUI_Mutex";

        [STAThread]
        static void Main(string[] args)
        {
            // Clean temp folder on exit
            AppDomain.CurrentDomain.ProcessExit += (s, e) => ConfigService.CleanTempFolder();

            // Single instance mutex
            using var mainMutex = new Mutex(true, MainUiMutexName, out bool createdNew);
            if (!createdNew)
            {
                // App is already running; focus existing process or exit cleanly
                return;
            }

            try
            {
                ApplicationConfiguration.Initialize();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Run main UI
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                try
                {
                    string crashPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                    File.WriteAllText(crashPath, ex.ToString());
                }
                catch { }
            }
            finally
            {
                try { mainMutex.ReleaseMutex(); } catch { }
            }
        }
    }
}
