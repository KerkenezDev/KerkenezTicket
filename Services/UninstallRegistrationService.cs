using System;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace KerkenezTicket.Services
{
    public static class UninstallRegistrationService
    {
        private const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\KerkenezTicket";
        private const string DisplayName = "Kerkenez Ticket";
        private const string Publisher = "Kerkenez Dev";
        private const string UrlInfoAbout = "https://github.com/KerkenezDev/KerkenezTicket";
        private const string HelpLink = "https://github.com/KerkenezDev/KerkenezTicket";

        /// <summary>
        /// Gets the active application version dynamically from assembly metadata (e.g. "1.0.0").
        /// </summary>
        public static string CurrentVersion
        {
            get
            {
                try
                {
                    var infoVer = typeof(UninstallRegistrationService).Assembly
                        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                        .InformationalVersion;

                    if (!string.IsNullOrWhiteSpace(infoVer))
                    {
                        int plusIdx = infoVer.IndexOf('+');
                        string clean = plusIdx > 0 ? infoVer.Substring(0, plusIdx) : infoVer;
                        clean = clean.Trim();
                        if (!string.IsNullOrEmpty(clean)) return clean;
                    }
                }
                catch { }

                try
                {
                    var ver = typeof(UninstallRegistrationService).Assembly.GetName().Version;
                    if (ver != null)
                    {
                        return $"{ver.Major}.{ver.Minor}.{Math.Max(0, ver.Build)}";
                    }
                }
                catch { }

                return "1.0.0";
            }
        }

        /// <summary>
        /// Health checks and registers or heals the application in HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\KerkenezTicket.
        /// If the key is missing or any values point to an old/moved directory, updates them immediately.
        /// </summary>
        public static void RegisterOrHeal()
        {
            try
            {
                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
                {
                    exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "KerkenezTicket.exe");
                }

                if (!File.Exists(exePath)) return;

                UpdateUninstallEntry(exePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UninstallRegistrationService] Health check/heal error: {ex.Message}");
            }
        }

        private static void UpdateUninstallEntry(string exePath)
        {
            try
            {
                string installDir = Path.GetDirectoryName(exePath) ?? "";
                string uninstallCmd = $"\"{exePath}\" --uninstall";
                string quietUninstallCmd = $"\"{exePath}\" --uninstall --quiet";
                string displayIcon = $"\"{exePath}\",0";
                string activeVersion = CurrentVersion;

                using var key = Registry.CurrentUser.CreateSubKey(UninstallKeyPath, true);
                if (key != null)
                {
                    var currentUninstall = key.GetValue("UninstallString") as string;
                    var currentQuiet = key.GetValue("QuietUninstallString") as string;
                    var currentLocation = key.GetValue("InstallLocation") as string;
                    var currentIcon = key.GetValue("DisplayIcon") as string;
                    var currentRegVersion = key.GetValue("DisplayVersion") as string;
                    var currentName = key.GetValue("DisplayName") as string;
                    var currentPublisher = key.GetValue("Publisher") as string;

                    // Health check: check if any value is missing or differs from active runtime environment
                    bool needsHealing =
                        !string.Equals(currentUninstall, uninstallCmd, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(currentQuiet, quietUninstallCmd, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(currentLocation, installDir, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(currentIcon, displayIcon, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(currentRegVersion?.TrimStart('v', 'V'), activeVersion.TrimStart('v', 'V'), StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(currentName, DisplayName, StringComparison.Ordinal) ||
                        !string.Equals(currentPublisher, Publisher, StringComparison.Ordinal) ||
                        key.GetValue("InstallDate") == null;

                    if (needsHealing)
                    {
                        key.SetValue("DisplayName", DisplayName);
                        key.SetValue("DisplayVersion", activeVersion);
                        key.SetValue("Publisher", Publisher);
                        key.SetValue("DisplayIcon", displayIcon);
                        key.SetValue("InstallLocation", installDir);
                        key.SetValue("UninstallString", uninstallCmd);
                        key.SetValue("QuietUninstallString", quietUninstallCmd);
                        key.SetValue("URLInfoAbout", UrlInfoAbout);
                        key.SetValue("HelpLink", HelpLink);
                        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);

                        if (Version.TryParse(activeVersion.TrimStart('v', 'V'), out var parsedVer))
                        {
                            key.SetValue("MajorVersion", parsedVer.Major, RegistryValueKind.DWord);
                            key.SetValue("MinorVersion", parsedVer.Minor, RegistryValueKind.DWord);
                            key.SetValue("Version", (parsedVer.Major << 24) | (parsedVer.Minor << 16) | Math.Max(0, parsedVer.Build), RegistryValueKind.DWord);
                        }

                        if (key.GetValue("InstallDate") == null)
                        {
                            key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                        }

                        try
                        {
                            var fi = new FileInfo(exePath);
                            long sizeKb = fi.Length / 1024;
                            key.SetValue("EstimatedSize", (int)sizeKb, RegistryValueKind.DWord);
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UninstallRegistrationService] Error updating uninstall entry: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes the application registration key from HKCU Uninstall registry.
        /// </summary>
        public static void Unregister()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(UninstallKeyPath, false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UninstallRegistrationService] Error unregistering app: {ex.Message}");
            }
        }
    }
}
