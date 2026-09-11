using System;
using System.Collections.Generic;
using System.IO;

namespace KerkenezTicket.Models
{
    public class AppSettings
    {
        // Application Version
        public string AppVersion { get; set; } = "1.0.0";

        // Language / Localization ("en", "tr", etc.)
        public string Language { get; set; } = "en";

        // Ticket Defaults
        public string DefaultApp { get; set; } = "";
        public string DefaultPriority { get; set; } = "Medium";
        public string DefaultType { get; set; } = "Bug";
        public string TicketIdPrefix { get; set; } = "KT-";

        // User Applications (clean list, no bloated defaults)
        public List<string> KnownApps { get; set; } = new List<string>();

        // User Ticket Types (dynamically manageable)
        public List<string> KnownTypes { get; set; } = new List<string>
        {
            "Bug",
            "Feature",
            "Sync",
            "Task",
            "Improvement"
        };

        // Window Position & Dimensions Persistence (Default: 60% x 56% screen scale)
        public int WindowWidth { get; set; } = 0;
        public int WindowHeight { get; set; } = 0;
        public int WindowLeft { get; set; } = -1;
        public int WindowTop { get; set; } = -1;
        public bool IsMaximized { get; set; } = false;

        public double WindowWidthScale { get; set; } = 0.60;
        public double WindowHeightScale { get; set; } = 0.56;

        // Navigation & Splitters Persistence
        public bool CollapseSidebarByDefault { get; set; } = false;
        public int SidebarWidth { get; set; } = 168;
        public int TicketsSplitterDistance { get; set; } = 460;
        public int AppsSplitterDistance { get; set; } = 460;
        public int TicketDetailPanelWidth { get; set; } = 0;
        public int AppDetailPanelWidth { get; set; } = 0;
        public string LastSelectedTicketId { get; set; } = "";

        // Column Widths Persistence
        public Dictionary<string, int> TicketsColumnWidths { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> AppsColumnWidths { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> BackupsColumnWidths { get; set; } = new Dictionary<string, int>();

        // Behavior Options
        public string ListViewGrouping { get; set; } = "Status"; // "Status", "App", "None"
        public bool ConfirmBeforeDelete { get; set; } = true;

        // Background / CLI Options (handled silently)
        public bool CliAutoRegisterPath { get; set; } = true;

        // Storage & Backup Options
        public string DatabasePath { get; set; } = "";
        public string BackupDirectory { get; set; } = "";
        public bool AutoBackupOnExit { get; set; } = false;
        public int MaxBackupsToRetain { get; set; } = 10;

        // Shortcuts (Desktop + Start Menu)
        public bool ShortcutsCreated { get; set; } = false;

        public static AppSettings CreateDefault()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string defaultBackupDir = Path.Combine(appData, "Kerkenez", "ticket", "backups");
            string defaultDbPath = Path.Combine(appData, "Kerkenez", "ticket", "tickets.db");

            return new AppSettings
            {
                AppVersion = "1.0.0",
                Language = "en",
                DefaultApp = "",
                DefaultPriority = "Medium",
                DefaultType = "Bug",
                TicketIdPrefix = "KT-",
                KnownApps = new List<string>(),
                KnownTypes = new List<string> { "Bug", "Feature", "Sync", "Task", "Improvement" },
                WindowWidth = 0,
                WindowHeight = 0,
                WindowLeft = -1,
                WindowTop = -1,
                IsMaximized = false,
                WindowWidthScale = 0.60,
                WindowHeightScale = 0.56,
                CollapseSidebarByDefault = false,
                SidebarWidth = 168,
                TicketsSplitterDistance = 460,
                AppsSplitterDistance = 460,
                TicketDetailPanelWidth = 0,
                AppDetailPanelWidth = 0,
                LastSelectedTicketId = "",
                ListViewGrouping = "Status",
                ConfirmBeforeDelete = true,
                CliAutoRegisterPath = true,
                DatabasePath = defaultDbPath,
                BackupDirectory = defaultBackupDir,
                AutoBackupOnExit = false,
                MaxBackupsToRetain = 10,
                ShortcutsCreated = false
            };
        }
    }
}
