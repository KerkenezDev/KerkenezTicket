using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

            // 1. Handle --uninstall switch
            if (args != null && args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase) ||
                                              a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase) ||
                                              a.Equals("-uninstall", StringComparison.OrdinalIgnoreCase)))
            {
                bool isQuiet = args.Any(a => a.Equals("--quiet", StringComparison.OrdinalIgnoreCase) ||
                                             a.Equals("/quiet", StringComparison.OrdinalIgnoreCase) ||
                                             a.Equals("-quiet", StringComparison.OrdinalIgnoreCase) ||
                                             a.Equals("--silent", StringComparison.OrdinalIgnoreCase) ||
                                             a.Equals("/silent", StringComparison.OrdinalIgnoreCase) ||
                                             a.Equals("-silent", StringComparison.OrdinalIgnoreCase) ||
                                             a.Equals("-s", StringComparison.OrdinalIgnoreCase) ||
                                             a.Equals("-q", StringComparison.OrdinalIgnoreCase));

                if (!isQuiet)
                {
                    ApplicationConfiguration.Initialize();
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                }
                HandleUninstall(isQuiet);
                return;
            }

            // 2. Health check and register/heal HKCU Uninstall registry key on every startup
            UninstallRegistrationService.RegisterOrHeal();

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

        private static void HandleUninstall(bool isQuiet)
        {
            if (isQuiet)
            {
                try
                {
                    ConfigService.Uninstall();
                }
                catch { }
                return;
            }

            var res = MessageBox.Show(
                "Are you sure you want to completely uninstall Kerkenez Ticket?\n\nThis will remove the Windows Settings app registration, CLI from user PATH, %LOCALAPPDATA%\\Programs\\Kerkenez\\ticket, and all ticket databases, backups, and settings from %APPDATA%\\Kerkenez\\ticket.",
                "Uninstall Kerkenez Ticket",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (res == DialogResult.Yes)
            {
                bool success = ConfigService.Uninstall();
                if (success)
                {
                    MessageBox.Show(
                        "Kerkenez Ticket has been completely uninstalled.\n\nAll registry entries, PATH registrations, CLI components, and local databases have been removed.",
                        "Uninstall Complete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(
                        "Uninstall encountered an issue while cleaning some files. Please ensure the app is closed and check %APPDATA%\\Kerkenez\\ticket.",
                        "Uninstall Warning",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
        }
    }
}
