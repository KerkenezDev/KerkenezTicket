using System;
using System.IO;
using System.Text.Json;
using KerkenezTicket.Models;

namespace KerkenezTicket.Services
{
    public class ConfigService
    {
        public static readonly string SuiteFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Kerkenez");

        public static readonly string AppDataFolder = Path.Combine(SuiteFolder, "ticket");

        public static readonly string DatabaseFilePath = Path.Combine(AppDataFolder, "tickets.db");

        public static readonly string ConfigFilePath = Path.Combine(AppDataFolder, "config.json");

        public static readonly string BackupsFolder = Path.Combine(AppDataFolder, "backups");

        public static readonly string AttachmentsFolder = Path.Combine(AppDataFolder, "attachments");

        public static readonly string DefaultExportsFolder = Path.Combine(AppDataFolder, "exports");

        public static readonly string TempFolder = Path.Combine(
            Path.GetTempPath(),
            "Kerkenez", "ticket");

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static bool IsFirstInstallation { get; } = !File.Exists(ConfigFilePath);

        public AppSettings Settings { get; private set; }

        public event Action? SettingsChanged;

        public ConfigService()
        {
            EnsureDirectories();
            Settings = LoadConfig();
        }

        public string GetExportDirectory()
        {
            if (!string.IsNullOrWhiteSpace(Settings.ExportDirectory))
            {
                try
                {
                    if (!Directory.Exists(Settings.ExportDirectory))
                    {
                        Directory.CreateDirectory(Settings.ExportDirectory);
                    }
                    return Settings.ExportDirectory;
                }
                catch { }
            }

            if (!Directory.Exists(DefaultExportsFolder))
            {
                Directory.CreateDirectory(DefaultExportsFolder);
            }
            return DefaultExportsFolder;
        }

        public static void EnsureDirectories()
        {
            try
            {
                if (!Directory.Exists(SuiteFolder))
                {
                    Directory.CreateDirectory(SuiteFolder);
                }

                if (!Directory.Exists(AppDataFolder))
                {
                    Directory.CreateDirectory(AppDataFolder);
                }

                if (!Directory.Exists(BackupsFolder))
                {
                    Directory.CreateDirectory(BackupsFolder);
                }

                if (!Directory.Exists(AttachmentsFolder))
                {
                    Directory.CreateDirectory(AttachmentsFolder);
                }

                if (!Directory.Exists(DefaultExportsFolder))
                {
                    Directory.CreateDirectory(DefaultExportsFolder);
                }

                if (!Directory.Exists(TempFolder))
                {
                    Directory.CreateDirectory(TempFolder);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Directory init error: {ex.Message}");
            }
        }

        public AppSettings LoadConfig()
        {
            try
            {
                EnsureDirectories();

                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    if (loaded != null)
                    {
                        Settings = HealSettings(loaded);
                        SaveConfig(Settings);
                        return Settings;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Load error: {ex.Message}");
            }

            var defaults = AppSettings.CreateDefault();
            SaveConfig(defaults);
            Settings = defaults;
            return defaults;
        }

        public bool SaveConfig(AppSettings settings)
        {
            try
            {
                EnsureDirectories();
                Settings = HealSettings(settings);

                string json = JsonSerializer.Serialize(Settings, JsonOptions);
                string tempFile = ConfigFilePath + ".tmp";
                File.WriteAllText(tempFile, json);

                if (File.Exists(ConfigFilePath))
                {
                    File.Delete(ConfigFilePath);
                }
                File.Move(tempFile, ConfigFilePath);

                SettingsChanged?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Save error: {ex.Message}");
                return false;
            }
        }

        public void SyncKnownApps(System.Collections.Generic.IEnumerable<string> apps)
        {
            if (apps == null) return;
            bool changed = false;
            if (Settings.KnownApps == null)
            {
                Settings.KnownApps = new System.Collections.Generic.List<string>();
                changed = true;
            }

            foreach (var app in apps)
            {
                if (!string.IsNullOrWhiteSpace(app) &&
                    !Settings.KnownApps.Contains(app.Trim(), StringComparer.OrdinalIgnoreCase))
                {
                    Settings.KnownApps.Add(app.Trim());
                    changed = true;
                }
            }

            if (changed)
            {
                Settings.KnownApps.Sort(StringComparer.OrdinalIgnoreCase);
                SaveConfig(Settings);
            }
        }

        private static AppSettings HealSettings(AppSettings s)
        {
            if (s == null) return AppSettings.CreateDefault();

            if (s.KnownApps == null)
            {
                s.KnownApps = new System.Collections.Generic.List<string>();
            }

            if (string.IsNullOrWhiteSpace(s.DefaultApp)) s.DefaultApp = s.KnownApps?.FirstOrDefault() ?? "";
            if (string.IsNullOrWhiteSpace(s.DefaultPriority)) s.DefaultPriority = "Medium";
            if (string.IsNullOrWhiteSpace(s.DefaultType)) s.DefaultType = "Bug";

            if (s.KnownTypes == null || s.KnownTypes.Count == 0)
            {
                s.KnownTypes = new System.Collections.Generic.List<string>
                {
                    "Bug", "Feature", "Sync", "Task", "Improvement"
                };
            }

            if (s.TicketsColumnWidths == null) s.TicketsColumnWidths = new System.Collections.Generic.Dictionary<string, int>();
            if (s.AppsColumnWidths == null) s.AppsColumnWidths = new System.Collections.Generic.Dictionary<string, int>();
            if (s.BackupsColumnWidths == null) s.BackupsColumnWidths = new System.Collections.Generic.Dictionary<string, int>();

            if (s.TicketsSplitterDistance < 100) s.TicketsSplitterDistance = 460;
            if (s.AppsSplitterDistance < 100) s.AppsSplitterDistance = 460;
            if (s.TicketDetailPanelWidth < 100 || s.TicketDetailPanelWidth > 1600) s.TicketDetailPanelWidth = 0;
            if (s.AppDetailPanelWidth < 100 || s.AppDetailPanelWidth > 1600) s.AppDetailPanelWidth = 0;
            s.SidebarWidth = 168;

            if (string.IsNullOrWhiteSpace(s.BackupDirectory))
            {
                s.BackupDirectory = BackupsFolder;
            }

            if (string.IsNullOrWhiteSpace(s.DatabasePath))
            {
                s.DatabasePath = DatabaseFilePath;
            }

            if (string.IsNullOrWhiteSpace(s.TicketIdPrefix)) s.TicketIdPrefix = "KT-";
            if (string.IsNullOrWhiteSpace(s.Language)) s.Language = "en";
            if (string.IsNullOrWhiteSpace(s.ListViewGrouping)) s.ListViewGrouping = "Status";
            if (s.MaxBackupsToRetain < 1) s.MaxBackupsToRetain = 10;

            if (s.WindowWidthScale <= 0.1 || s.WindowWidthScale > 1.0) s.WindowWidthScale = 0.60;
            if (s.WindowHeightScale <= 0.1 || s.WindowHeightScale > 1.0) s.WindowHeightScale = 0.56;

            return s;
        }

        public static void CleanTempFolder()
        {
            try
            {
                if (Directory.Exists(TempFolder))
                {
                    Directory.Delete(TempFolder, true);
                }
            }
            catch { }
        }

        public static bool Uninstall()
        {
            try
            {
                // 1. Remove Windows Uninstall / Add-Remove Programs registration
                UninstallRegistrationService.Unregister();

                // 2. Remove CLI files and User PATH registration
                CliInstallerService.UninstallCli();

                // 3. Remove Desktop and Start Menu shortcuts
                ShortcutService.RemoveShortcuts();

                // 4. Clean temporary files
                CleanTempFolder();

                // 5. Release all SQLite connection pools so files can be deleted without locks
                try
                {
                    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                }
                catch { }

                // 6. Delete AppData directory (%APPDATA%\Kerkenez\ticket)
                if (Directory.Exists(AppDataFolder))
                {
                    for (int i = 0; i < 3; i++)
                    {
                        try
                        {
                            Directory.Delete(AppDataFolder, true);
                            break;
                        }
                        catch
                        {
                            System.Threading.Thread.Sleep(150);
                        }
                    }
                }

                // 7. If parent directory (%APPDATA%\Kerkenez) is now empty, delete it
                string? parent = Path.GetDirectoryName(AppDataFolder);
                if (parent != null && Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any())
                {
                    try { Directory.Delete(parent, false); } catch { }
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Error during uninstall: {ex.Message}");
                return false;
            }
        }
    }
}
