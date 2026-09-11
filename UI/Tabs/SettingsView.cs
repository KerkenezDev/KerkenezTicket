using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using KerkenezTicket.Models;
using KerkenezTicket.Services;

namespace KerkenezTicket.UI.Tabs
{
    public class SettingsView : UserControl
    {
        private readonly ConfigService _configService;

        private readonly TicketDatabaseService _dbService;
        private readonly BackupService _backupService;

        public event Action? SettingsSaved;
        public event Action? TicketsRestored;

        // Card 1: Ticket Defaults
        private ComboBox _cboDefaultApp = null!;
        private ComboBox _cboDefaultPriority = null!;
        private ComboBox _cboDefaultType = null!;
        private TextBox _txtPrefix = null!;
        private ComboBox _cboGrouping = null!;

        // Card 2: UI & Window Scaling
        private CheckBox _chkCollapseSidebar = null!;
        private CheckBox _chkConfirmDelete = null!;
        private Button _btnPresetDefault = null!;
        private Button _btnPresetCompact = null!;
        private Button _btnPresetLarge = null!;
        private Button _btnPresetMax = null!;
        private NumericUpDown _numWidthScale = null!;
        private NumericUpDown _numHeightScale = null!;
        private Label _lblScalePreview = null!;
        private Button _btnApplyWindowSizeNow = null!;

        // Card 3: DPAPI Storage & Backup
        private TextBox _txtDbPath = null!;
        private Button _btnOpenDbFolder = null!;
        private TextBox _txtBackupDir = null!;
        private Button _btnBrowseBackupDir = null!;
        private CheckBox _chkAutoBackupOnExit = null!;
        private NumericUpDown _numMaxBackups = null!;

        private Button _btnCreateBackupNow = null!;
        private Button _btnRestoreBackupFile = null!;
        private ListView _lvBackups = null!;
        private Button _btnRestoreSelectedBackup = null!;
        private Button _btnDeleteSelectedBackup = null!;
        private Button _btnRefreshBackups = null!;
        private Label _lblBackupStatus = null!;

        // Action Bar
        private Button _btnSave = null!;
        private Button _btnReset = null!;
        private Label _lblSaveToast = null!;

        private const int CardWidth = 780;

        public SettingsView(ConfigService configService, TicketDatabaseService? dbService = null)
        {
            _configService = configService;
            _dbService = dbService ?? new TicketDatabaseService();
            _backupService = new BackupService(_dbService, _configService);

            InitializeComponent();
            LoadSettings();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(248, 249, 250);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            var scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(28, 20, 28, 28)
            };

            var mainFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0)
            };

            // Page Header
            var headerPanel = new Panel
            {
                Width = CardWidth,
                Height = 78,
                Margin = new Padding(0, 0, 0, 14)
            };

            var lblTitle = new Label
            {
                Text = "⚙️  Settings & Preferences",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.FromArgb(24, 28, 36),
                AutoSize = true,
                Location = new Point(0, 0)
            };

            var lblSubtitle = new Label
            {
                Text = "Configure ticket workflow defaults, window layout preferences, and local DPAPI backup options.",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(108, 117, 125),
                AutoSize = true,
                Location = new Point(2, 40)
            };

            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(lblSubtitle);
            mainFlow.Controls.Add(headerPanel);

            // ==================== Card 1: Ticket Defaults ====================
            var card1 = CreateCardPanel(CardWidth);
            card1.Controls.Add(CreateSectionHeader("🎟️  Ticket Workflow & Defaults"));

            var rowDefaults = new FlowLayoutPanel
            {
                Width = CardWidth - 48,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 10)
            };

            // Default App
            _cboDefaultApp = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F), Height = 28 };
            var pnlApp = CreateFormColumn("Default Application:", "Pre-selected app for new tickets", _cboDefaultApp, 226);
            rowDefaults.Controls.Add(pnlApp);

            // Default Priority
            _cboDefaultPriority = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F), Height = 28 };
            _cboDefaultPriority.Items.AddRange(new[] { "Low", "Medium", "High", "Urgent" });
            var pnlPri = CreateFormColumn("Default Priority:", "Urgency level applied to new tickets", _cboDefaultPriority, 226);
            rowDefaults.Controls.Add(pnlPri);

            // Default Type
            _cboDefaultType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F), Height = 28 };
            var pnlType = CreateFormColumn("Default Type:", "Category type for new tickets", _cboDefaultType, 226);
            rowDefaults.Controls.Add(pnlType);

            card1.Controls.Add(rowDefaults);

            var rowExtra = new FlowLayoutPanel
            {
                Width = CardWidth - 48,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 6)
            };

            // Prefix
            _txtPrefix = new TextBox { Font = new Font("Segoe UI", 9F), Text = "KT-", Height = 28 };
            var pnlPrefix = CreateFormColumn("Ticket ID Prefix:", "Prefix used for ticket sequence codes", _txtPrefix, 226);
            rowExtra.Controls.Add(pnlPrefix);

            // List Grouping
            _cboGrouping = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F), Height = 28 };
            _cboGrouping.Items.AddRange(new[] { "Status (To Do, Doing, Done, Killed)", "App (mail, ticket...)", "None (Flat List)" });
            var pnlGroup = CreateFormColumn("Tickets List Grouping:", "How tickets are grouped in the list", _cboGrouping, 340);
            rowExtra.Controls.Add(pnlGroup);

            card1.Controls.Add(rowExtra);
            mainFlow.Controls.Add(card1);

            // ==================== Card 2: UI & Window Scaling ====================
            var cardUi = CreateCardPanel(CardWidth);
            cardUi.Controls.Add(CreateSectionHeader("🎨  Interface & Window Scaling"));

            _chkCollapseSidebar = new CheckBox
            {
                Text = "Start with left navigation sidebar collapsed by default (compact rail on launch)",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 0, 0, 8)
            };
            cardUi.Controls.Add(_chkCollapseSidebar);

            _chkConfirmDelete = new CheckBox
            {
                Text = "Show confirmation prompt before deleting a ticket",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 0, 0, 14)
            };
            cardUi.Controls.Add(_chkConfirmDelete);

            var lblPresets = new Label
            {
                Text = "Window Dimension Scaling Presets:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 6)
            };
            cardUi.Controls.Add(lblPresets);

            var rowPresets = new FlowLayoutPanel
            {
                Width = CardWidth - 48,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 12)
            };

            _btnPresetDefault = CreatePresetButton("Default (60% × 56%)", 60, 56);
            _btnPresetCompact = CreatePresetButton("Compact (50% × 50%)", 50, 50);
            _btnPresetLarge = CreatePresetButton("Large (75% × 70%)", 75, 70);
            _btnPresetMax = CreatePresetButton("Expanded (90% × 85%)", 90, 85);

            rowPresets.Controls.Add(_btnPresetDefault);
            rowPresets.Controls.Add(_btnPresetCompact);
            rowPresets.Controls.Add(_btnPresetLarge);
            rowPresets.Controls.Add(_btnPresetMax);
            cardUi.Controls.Add(rowPresets);

            var rowScaleSliders = new FlowLayoutPanel
            {
                Width = CardWidth - 48,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 8)
            };

            var lblW = new Label { Text = "Width Scale %:", AutoSize = true, Margin = new Padding(0, 5, 6, 0), Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _numWidthScale = new NumericUpDown { Minimum = 40, Maximum = 100, Value = 65, Width = 65, Font = new Font("Segoe UI", 9F) };
            _numWidthScale.ValueChanged += (s, e) => UpdateScalePreview();

            var lblH = new Label { Text = "Height Scale %:", AutoSize = true, Margin = new Padding(20, 5, 6, 0), Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _numHeightScale = new NumericUpDown { Minimum = 40, Maximum = 100, Value = 60, Width = 65, Font = new Font("Segoe UI", 9F) };
            _numHeightScale.ValueChanged += (s, e) => UpdateScalePreview();

            _btnApplyWindowSizeNow = new Button
            {
                Text = "📐 Apply Size Now",
                AutoSize = true,
                Height = 28,
                Padding = new Padding(10, 0, 10, 0),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                BackColor = Color.FromArgb(240, 242, 245),
                Margin = new Padding(20, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            _btnApplyWindowSizeNow.FlatAppearance.BorderColor = Color.FromArgb(215, 222, 230);
            _btnApplyWindowSizeNow.Click += OnApplyWindowSizeNowClicked;

            rowScaleSliders.Controls.Add(lblW);
            rowScaleSliders.Controls.Add(_numWidthScale);
            rowScaleSliders.Controls.Add(lblH);
            rowScaleSliders.Controls.Add(_numHeightScale);
            rowScaleSliders.Controls.Add(_btnApplyWindowSizeNow);
            cardUi.Controls.Add(rowScaleSliders);

            _lblScalePreview = new Label
            {
                Text = "Preview:",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(108, 117, 125),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            cardUi.Controls.Add(_lblScalePreview);

            mainFlow.Controls.Add(cardUi);

            // ==================== Card 3: DPAPI Storage & Backups ====================
            var card3 = CreateCardPanel(CardWidth);
            card3.Controls.Add(CreateSectionHeader("🔒  Local Storage & DPAPI Encryption"));

            var lblSecNotice = new Label
            {
                Text = "All ticket titles, descriptions, work notes, and tags are encrypted with Windows Data Protection API (DPAPI) tied to your local interactive Windows account with suite entropy.",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(0, 102, 204),
                BackColor = Color.FromArgb(235, 243, 252),
                Padding = new Padding(10, 8, 10, 8),
                Width = CardWidth - 48,
                Height = 38,
                Margin = new Padding(0, 0, 0, 14)
            };
            card3.Controls.Add(lblSecNotice);

            // Database File Row
            var lblDb = new Label { Text = "SQLite Database Path:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
            card3.Controls.Add(lblDb);

            var rowDb = new FlowLayoutPanel
            {
                Width = CardWidth - 48,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 12)
            };

            _txtDbPath = new TextBox
            {
                Width = 570,
                ReadOnly = true,
                BackColor = Color.FromArgb(248, 249, 250),
                Font = new Font("Consolas", 9F)
            };

            _btnOpenDbFolder = new Button
            {
                Text = "📁 Open Folder",
                Width = 120,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                BackColor = Color.FromArgb(240, 242, 245),
                Margin = new Padding(8, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            _btnOpenDbFolder.FlatAppearance.BorderColor = Color.FromArgb(215, 222, 230);
            _btnOpenDbFolder.Click += (s, e) => OpenDirectory(Path.GetDirectoryName(ConfigService.DatabaseFilePath)!);

            rowDb.Controls.Add(_txtDbPath);
            rowDb.Controls.Add(_btnOpenDbFolder);
            card3.Controls.Add(rowDb);

            // Backup Directory Row
            var lblBackup = new Label { Text = "Single-File Backup Folder:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
            card3.Controls.Add(lblBackup);

            var rowBackup = new FlowLayoutPanel
            {
                Width = CardWidth - 48,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 12)
            };

            _txtBackupDir = new TextBox
            {
                Width = 570,
                Font = new Font("Consolas", 9F)
            };

            _btnBrowseBackupDir = new Button
            {
                Text = "Browse...",
                Width = 120,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                BackColor = Color.FromArgb(240, 242, 245),
                Margin = new Padding(8, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            _btnBrowseBackupDir.FlatAppearance.BorderColor = Color.FromArgb(215, 222, 230);
            _btnBrowseBackupDir.Click += OnBrowseBackupDirClicked;

            rowBackup.Controls.Add(_txtBackupDir);
            rowBackup.Controls.Add(_btnBrowseBackupDir);
            card3.Controls.Add(rowBackup);

            _chkAutoBackupOnExit = new CheckBox
            {
                Text = "Create an automated single-file JSON backup when closing Kerkenez Ticket",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 0, 0, 8)
            };
            card3.Controls.Add(_chkAutoBackupOnExit);

            var rowRetain = new FlowLayoutPanel
            {
                Width = CardWidth - 48,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 4)
            };

            var lblRetain = new Label { Text = "Maximum Backups to Retain:", AutoSize = true, Margin = new Padding(0, 5, 8, 0), Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _numMaxBackups = new NumericUpDown { Minimum = 3, Maximum = 100, Value = 10, Width = 65, Font = new Font("Segoe UI", 9F) };
            var lblRetainHelp = new Label { Text = "(older backups are cleaned up automatically)", AutoSize = true, ForeColor = Color.FromArgb(108, 117, 125), Margin = new Padding(8, 5, 0, 0), Font = new Font("Segoe UI", 8.5F) };

            rowRetain.Controls.Add(lblRetain);
            rowRetain.Controls.Add(_numMaxBackups);
            rowRetain.Controls.Add(lblRetainHelp);
            card3.Controls.Add(rowRetain);

            // Quick Backup Actions Row
            var rowBackupActions = new FlowLayoutPanel
            {
                Width = CardWidth - 48,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 10, 0, 10)
            };

            _btnCreateBackupNow = new Button
            {
                Text = "💾  Create Single-File Backup (.ktbackup)",
                AutoSize = true,
                Height = 32,
                Padding = new Padding(12, 0, 12, 0),
                BackColor = Color.FromArgb(0, 102, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnCreateBackupNow.FlatAppearance.BorderSize = 0;
            _btnCreateBackupNow.Click += OnCreateBackupNowClicked;

            _btnRestoreBackupFile = new Button
            {
                Text = "📥  Restore Tickets from Backup...",
                AutoSize = true,
                Height = 32,
                Padding = new Padding(12, 0, 12, 0),
                BackColor = Color.FromArgb(245, 247, 250),
                ForeColor = Color.FromArgb(45, 55, 72),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnRestoreBackupFile.FlatAppearance.BorderColor = Color.FromArgb(215, 222, 230);
            _btnRestoreBackupFile.Click += OnRestoreBackupFileClicked;

            rowBackupActions.Controls.Add(_btnCreateBackupNow);
            rowBackupActions.Controls.Add(_btnRestoreBackupFile);
            card3.Controls.Add(rowBackupActions);

            // Backups Archive List
            var lblArchive = new Label
            {
                Text = "📦  Saved Backup Files in Folder:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 6)
            };
            card3.Controls.Add(lblArchive);

            _lvBackups = new ListView
            {
                Width = CardWidth - 48,
                Height = 135,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                GridLines = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 8.5F),
                Margin = new Padding(0, 0, 0, 8)
            };
            _lvBackups.Columns.Add("File Name", 330);
            _lvBackups.Columns.Add("Created Date", 160);
            _lvBackups.Columns.Add("Size", 90);
            _lvBackups.ColumnWidthChanged += (s, e) => SaveBackupColumnWidths();
            RestoreBackupColumnWidths();
            card3.Controls.Add(_lvBackups);

            var rowArchiveActions = new FlowLayoutPanel
            {
                Width = CardWidth - 48,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 4)
            };

            _btnRestoreSelectedBackup = new Button
            {
                Text = "📥  Restore Selected",
                AutoSize = true,
                Height = 28,
                Padding = new Padding(10, 0, 10, 0),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                BackColor = Color.FromArgb(245, 247, 250),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnRestoreSelectedBackup.FlatAppearance.BorderColor = Color.FromArgb(215, 222, 230);
            _btnRestoreSelectedBackup.Click += OnRestoreSelectedBackupClicked;

            _btnDeleteSelectedBackup = new Button
            {
                Text = "🗑️  Delete Selected",
                AutoSize = true,
                Height = 28,
                Padding = new Padding(10, 0, 10, 0),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                BackColor = Color.FromArgb(254, 242, 242),
                ForeColor = Color.FromArgb(220, 38, 38),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnDeleteSelectedBackup.FlatAppearance.BorderColor = Color.FromArgb(252, 165, 165);
            _btnDeleteSelectedBackup.Click += OnDeleteSelectedBackupClicked;

            _btnRefreshBackups = new Button
            {
                Text = "🔄  Refresh List",
                AutoSize = true,
                Height = 28,
                Padding = new Padding(10, 0, 10, 0),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                BackColor = Color.FromArgb(245, 247, 250),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnRefreshBackups.FlatAppearance.BorderColor = Color.FromArgb(215, 222, 230);
            _btnRefreshBackups.Click += (s, e) => LoadExistingBackups();

            rowArchiveActions.Controls.Add(_btnRestoreSelectedBackup);
            rowArchiveActions.Controls.Add(_btnDeleteSelectedBackup);
            rowArchiveActions.Controls.Add(_btnRefreshBackups);
            card3.Controls.Add(rowArchiveActions);

            _lblBackupStatus = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 167, 69),
                Margin = new Padding(0, 4, 0, 4)
            };
            card3.Controls.Add(_lblBackupStatus);

            mainFlow.Controls.Add(card3);

            // ==================== Card 4: Terminal CLI (kticket) Quick Reference ====================
            var card4 = CreateCardPanel(CardWidth);
            card4.Controls.Add(CreateSectionHeader("💻  Terminal CLI (kticket) Quick Reference"));

            var lblCliDesc = new Label
            {
                Text = "kticket is registered in your Windows User PATH and can be run from PowerShell, CMD, or Windows Terminal.",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(70, 75, 85),
                Width = CardWidth - 48,
                Height = 24,
                Margin = new Padding(0, 0, 0, 10)
            };
            card4.Controls.Add(lblCliDesc);

            var txtCliHelp = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Width = CardWidth - 48,
                Height = 210,
                BackColor = Color.FromArgb(30, 32, 38),
                ForeColor = Color.FromArgb(235, 238, 242),
                Font = new Font("Cascadia Code, Consolas, Courier New", 8.5F),
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
                Margin = new Padding(0, 0, 0, 10),
                Text =
@"# 1. Create a ticket (DPAPI encrypted)
kticket add ""<title>"" -a <app> -p <priority> -t <type> [-d <desc>]
  Example: kticket add ""fix a refresh error sync on x situation"" -a myapp -p high -t sync

# 2. List tickets (with optional filters)
kticket list
kticket list --app myapp --status doing
kticket list --type bug --status todo

# 3. View ticket details & work notes
kticket view KT-1

# 4. Update status (todo | doing | done | killed)
kticket status KT-1 doing
kticket done KT-1
kticket kill KT-1

# 5. Export single-file unencrypted JSON backup
kticket export
kticket export my_tickets.json

# 6. Check / re-register CLI in PATH
kticket register"
            };
            card4.Controls.Add(txtCliHelp);

            var rowCliActions = new FlowLayoutPanel
            {
                Width = CardWidth - 48,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 4)
            };

            var btnCopySample = new Button
            {
                Text = "📋  Copy Sample Add Command",
                AutoSize = true,
                Height = 30,
                Padding = new Padding(12, 0, 12, 0),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                BackColor = Color.FromArgb(240, 243, 248),
                ForeColor = Color.FromArgb(0, 102, 204),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 10, 0)
            };
            btnCopySample.FlatAppearance.BorderColor = Color.FromArgb(190, 210, 235);
            btnCopySample.Click += (s, e) =>
            {
                Clipboard.SetText("kticket add \"fix a refresh error sync on x situation\" -a myapp -p high -t sync");
                _lblSaveToast.Text = "✓ Sample command copied to clipboard!";
            };

            var btnRegisterCli = new Button
            {
                Text = "⚡  Register CLI in PATH Now",
                AutoSize = true,
                Height = 30,
                Padding = new Padding(12, 0, 12, 0),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                BackColor = Color.FromArgb(245, 247, 250),
                ForeColor = Color.FromArgb(50, 54, 62),
                Cursor = Cursors.Hand
            };
            btnRegisterCli.FlatAppearance.BorderColor = Color.FromArgb(215, 222, 230);
            btnRegisterCli.Click += (s, e) =>
            {
                bool ok = CliInstallerService.EnsureInstalledAndRegistered();
                _lblSaveToast.Text = ok ? "✓ kticket registered in User PATH!" : "⚠️ Registration completed.";
            };

            rowCliActions.Controls.Add(btnCopySample);
            rowCliActions.Controls.Add(btnRegisterCli);
            card4.Controls.Add(rowCliActions);

            mainFlow.Controls.Add(card4);

            // ==================== Bottom Sticky Actions Bar ====================
            var actionBar = new Panel
            {
                Width = CardWidth,
                Height = 60,
                Margin = new Padding(0, 8, 0, 30)
            };

            _btnSave = new Button
            {
                Text = "💾  Save Configuration",
                Location = new Point(0, 10),
                Size = new Size(185, 40),
                BackColor = Color.FromArgb(0, 102, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.Click += OnSaveSettingsClicked;

            _btnReset = new Button
            {
                Text = "🔄  Reset Defaults",
                Location = new Point(195, 10),
                Size = new Size(150, 40),
                BackColor = Color.FromArgb(240, 242, 245),
                ForeColor = Color.FromArgb(50, 54, 62),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            _btnReset.FlatAppearance.BorderColor = Color.FromArgb(215, 222, 230);
            _btnReset.Click += OnResetDefaultsClicked;

            _lblSaveToast = new Label
            {
                Location = new Point(360, 20),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 167, 69)
            };

            actionBar.Controls.Add(_btnSave);
            actionBar.Controls.Add(_btnReset);
            actionBar.Controls.Add(_lblSaveToast);
            mainFlow.Controls.Add(actionBar);

            scrollPanel.Controls.Add(mainFlow);
            this.Controls.Add(scrollPanel);
        }

        private static FlowLayoutPanel CreateCardPanel(int width)
        {
            var pnl = new FlowLayoutPanel
            {
                Width = width,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.White,
                Padding = new Padding(24, 18, 24, 20),
                Margin = new Padding(0, 0, 0, 18)
            };

            pnl.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(222, 226, 230), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, pnl.Width - 1, pnl.Height - 1);
            };

            return pnl;
        }

        private static Label CreateSectionHeader(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 35, 45),
                Margin = new Padding(0, 0, 0, 14)
            };
        }

        private static Panel CreateFormColumn(string title, string subtitle, Control control, int width)
        {
            var pnl = new Panel
            {
                Width = width,
                Height = 78,
                Margin = new Padding(0, 0, 16, 10)
            };

            var lblT = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(35, 40, 50),
                AutoSize = true,
                Location = new Point(0, 2)
            };

            control.Location = new Point(0, 24);
            control.Width = width;

            var lblS = new Label
            {
                Text = subtitle,
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(120, 125, 135),
                AutoSize = true,
                Location = new Point(0, 54)
            };

            pnl.Controls.Add(lblT);
            pnl.Controls.Add(control);
            pnl.Controls.Add(lblS);
            return pnl;
        }

        private Button CreatePresetButton(string text, int wScale, int hScale)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                Height = 32,
                Padding = new Padding(12, 0, 12, 0),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                BackColor = Color.FromArgb(245, 247, 250),
                ForeColor = Color.FromArgb(45, 55, 72),
                Margin = new Padding(0, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(215, 225, 235);
            btn.Click += (s, e) =>
            {
                _numWidthScale.Value = wScale;
                _numHeightScale.Value = hScale;
                UpdateScalePreview();
            };
            return btn;
        }

        private static Button CreateActionButton(string text, Action onClick)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                Height = 28,
                Padding = new Padding(10, 0, 10, 0),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                BackColor = Color.FromArgb(245, 247, 250),
                ForeColor = Color.FromArgb(45, 55, 72),
                Margin = new Padding(0, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(215, 225, 235);
            btn.Click += (s, e) => onClick();
            return btn;
        }

        public void LoadSettings()
        {
            var s = _configService.Settings;

            // Apps
            _cboDefaultApp.Items.Clear();
            foreach (var app in s.KnownApps)
            {
                _cboDefaultApp.Items.Add(app);
            }
            _cboDefaultApp.SelectedItem = s.DefaultApp;
            if (_cboDefaultApp.SelectedIndex < 0 && _cboDefaultApp.Items.Count > 0) _cboDefaultApp.SelectedIndex = 0;

            // Types
            _cboDefaultType.Items.Clear();
            foreach (var t in s.KnownTypes)
            {
                _cboDefaultType.Items.Add(t);
            }
            _cboDefaultType.SelectedItem = s.DefaultType;
            if (_cboDefaultType.SelectedIndex < 0 && _cboDefaultType.Items.Count > 0) _cboDefaultType.SelectedIndex = 0;

            // Priority
            _cboDefaultPriority.SelectedItem = s.DefaultPriority;
            if (_cboDefaultPriority.SelectedIndex < 0) _cboDefaultPriority.SelectedItem = "Medium";

            // Prefix & Grouping
            _txtPrefix.Text = s.TicketIdPrefix;
            int groupIdx = s.ListViewGrouping switch
            {
                "Status" => 0,
                "App" => 1,
                "None" => 2,
                _ => 0
            };
            _cboGrouping.SelectedIndex = groupIdx;

            // UI
            _chkCollapseSidebar.Checked = s.CollapseSidebarByDefault;
            _chkConfirmDelete.Checked = s.ConfirmBeforeDelete;
            _numWidthScale.Value = (decimal)Math.Clamp(Math.Round(s.WindowWidthScale * 100), 40, 100);
            _numHeightScale.Value = (decimal)Math.Clamp(Math.Round(s.WindowHeightScale * 100), 40, 100);
            UpdateScalePreview();

            // Storage
            _txtDbPath.Text = ConfigService.DatabaseFilePath;
            _txtBackupDir.Text = !string.IsNullOrWhiteSpace(s.BackupDirectory) ? s.BackupDirectory : ConfigService.BackupsFolder;
            _chkAutoBackupOnExit.Checked = s.AutoBackupOnExit;
            _numMaxBackups.Value = Math.Clamp(s.MaxBackupsToRetain, 3, 100);

            LoadExistingBackups();
        }

        private void UpdateScalePreview()
        {
            var screen = Screen.FromControl(this) ?? Screen.PrimaryScreen;
            var wa = screen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            int pw = (int)Math.Round(wa.Width * ((double)_numWidthScale.Value / 100.0));
            int ph = (int)Math.Round(wa.Height * ((double)_numHeightScale.Value / 100.0));
            _lblScalePreview.Text = $"Preview on current monitor ({wa.Width}×{wa.Height}): {pw} × {ph} px ({(int)_numWidthScale.Value}% × {(int)_numHeightScale.Value}%)";
        }

        private void OnApplyWindowSizeNowClicked(object? sender, EventArgs e)
        {
            var topForm = this.FindForm();
            if (topForm == null) return;

            var screen = Screen.FromControl(this) ?? Screen.PrimaryScreen;
            var wa = screen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);

            double wScale = (double)_numWidthScale.Value / 100.0;
            double hScale = (double)_numHeightScale.Value / 100.0;
            int targetW = (int)Math.Clamp(Math.Round(wa.Width * wScale), 960, wa.Width);
            int targetH = (int)Math.Clamp(Math.Round(wa.Height * hScale), 540, wa.Height);

            topForm.Size = new Size(targetW, targetH);
            topForm.Location = new Point(
                wa.Left + Math.Max(0, (wa.Width - targetW) / 2),
                wa.Top + Math.Max(0, (wa.Height - targetH) / 2)
            );

            _configService.Settings.WindowWidth = targetW;
            _configService.Settings.WindowHeight = targetH;
            _configService.Settings.WindowLeft = topForm.Left;
            _configService.Settings.WindowTop = topForm.Top;
            _configService.Settings.WindowWidthScale = wScale;
            _configService.Settings.WindowHeightScale = hScale;
            _configService.SaveConfig(_configService.Settings);
        }

        private void OnBrowseBackupDirClicked(object? sender, EventArgs e)
        {
            using var fbd = new FolderBrowserDialog
            {
                Description = "Select Folder for Single-File Backups",
                SelectedPath = Directory.Exists(_txtBackupDir.Text) ? _txtBackupDir.Text : ConfigService.BackupsFolder
            };

            if (fbd.ShowDialog(this) == DialogResult.OK)
            {
                _txtBackupDir.Text = fbd.SelectedPath;
            }
        }

        private void OnSaveSettingsClicked(object? sender, EventArgs e)
        {
            SaveSettings();
        }

        public void SaveSettings()
        {
            var s = _configService.Settings;

            s.DefaultApp = _cboDefaultApp.SelectedItem?.ToString() ?? (_cboDefaultApp.Text ?? "");
            s.DefaultPriority = _cboDefaultPriority.SelectedItem?.ToString() ?? "Medium";
            s.DefaultType = _cboDefaultType.SelectedItem?.ToString() ?? "Bug";
            s.TicketIdPrefix = string.IsNullOrWhiteSpace(_txtPrefix.Text) ? "KT-" : _txtPrefix.Text.Trim();

            s.ListViewGrouping = _cboGrouping.SelectedIndex switch
            {
                0 => "Status",
                1 => "App",
                2 => "None",
                _ => "Status"
            };

            s.CollapseSidebarByDefault = _chkCollapseSidebar.Checked;
            s.ConfirmBeforeDelete = _chkConfirmDelete.Checked;
            s.WindowWidthScale = (double)_numWidthScale.Value / 100.0;
            s.WindowHeightScale = (double)_numHeightScale.Value / 100.0;

            if (!string.IsNullOrWhiteSpace(_txtBackupDir.Text))
            {
                s.BackupDirectory = _txtBackupDir.Text.Trim();
            }

            s.AutoBackupOnExit = _chkAutoBackupOnExit.Checked;
            s.MaxBackupsToRetain = (int)_numMaxBackups.Value;

            bool ok = _configService.SaveConfig(s);
            if (ok)
            {
                _lblSaveToast.Text = "✓ Configuration saved to %APPDATA%\\Kerkenez\\ticket\\config.json!";
                SettingsSaved?.Invoke();
            }
        }

        private void OnResetDefaultsClicked(object? sender, EventArgs e)
        {
            var res = MessageBox.Show(this, "Are you sure you want to reset all settings to defaults?", "Reset Defaults", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (res == DialogResult.Yes)
            {
                var defaults = AppSettings.CreateDefault();
                _configService.SaveConfig(defaults);
                LoadSettings();
                _lblSaveToast.Text = "Defaults restored!";
                SettingsSaved?.Invoke();
            }
        }

        public void LoadExistingBackups()
        {
            _lvBackups.Items.Clear();
            string dir = !string.IsNullOrWhiteSpace(_configService.Settings.BackupDirectory)
                ? _configService.Settings.BackupDirectory
                : ConfigService.BackupsFolder;

            if (Directory.Exists(dir))
            {
                var files = new DirectoryInfo(dir).GetFiles("*.ktbackup")
                    .Concat(new DirectoryInfo(dir).GetFiles("*.json"))
                    .OrderByDescending(f => f.LastWriteTimeUtc);

                foreach (var f in files)
                {
                    var lvi = new ListViewItem(f.Name);
                    lvi.SubItems.Add(f.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    lvi.SubItems.Add($"{f.Length / 1024.0:F1} KB");
                    lvi.Tag = f.FullName;
                    _lvBackups.Items.Add(lvi);
                }
            }
        }

        private void OnCreateBackupNowClicked(object? sender, EventArgs e)
        {
            try
            {
                string path = _backupService.CreateBackup();
                _lblBackupStatus.ForeColor = Color.FromArgb(40, 167, 69);
                _lblBackupStatus.Text = $"✓ Backup created: {Path.GetFileName(path)} ({new FileInfo(path).Length} bytes)";
                LogService.Success("Backup", $"Single-file backup created: {path}");
                LoadExistingBackups();
            }
            catch (Exception ex)
            {
                _lblBackupStatus.ForeColor = Color.FromArgb(220, 53, 69);
                _lblBackupStatus.Text = $"Backup failed: {ex.Message}";
                LogService.Error("Backup", $"Failed to create backup: {ex.Message}");
            }
        }

        private void OnRestoreBackupFileClicked(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select Backup File to Restore",
                Filter = "Kerkenez Ticket Backup (*.ktbackup;*.json)|*.ktbackup;*.json|All Files (*.*)|*.*",
                InitialDirectory = !string.IsNullOrWhiteSpace(_configService.Settings.BackupDirectory)
                    ? _configService.Settings.BackupDirectory
                    : ConfigService.BackupsFolder
            };

            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                RestoreBackupFromPath(ofd.FileName);
            }
        }

        private void OnRestoreSelectedBackupClicked(object? sender, EventArgs e)
        {
            if (_lvBackups.SelectedItems.Count == 0)
            {
                MessageBox.Show(this, "Please select a backup from the list to restore.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string? path = _lvBackups.SelectedItems[0].Tag as string;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                RestoreBackupFromPath(path);
            }
        }

        private void RestoreBackupFromPath(string path)
        {
            var askOverwrite = MessageBox.Show(this, "Do you want to overwrite existing tickets if they have matching IDs?", "Restore Options", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (askOverwrite == DialogResult.Cancel) return;

            bool overwrite = (askOverwrite == DialogResult.Yes);
            var result = _backupService.RestoreBackup(path, overwrite);

            _lblBackupStatus.ForeColor = Color.FromArgb(40, 167, 69);
            _lblBackupStatus.Text = result.Message;
            LogService.Success("Backup", $"Restored tickets from {Path.GetFileName(path)}: {result.Message}");

            LoadExistingBackups();
            TicketsRestored?.Invoke();
            MessageBox.Show(this, result.Message, "Restore Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnDeleteSelectedBackupClicked(object? sender, EventArgs e)
        {
            if (_lvBackups.SelectedItems.Count == 0) return;

            string? path = _lvBackups.SelectedItems[0].Tag as string;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                var confirm = MessageBox.Show(this, $"Are you sure you want to delete backup file:\n{Path.GetFileName(path)}?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm == DialogResult.Yes)
                {
                    try
                    {
                        File.Delete(path);
                        LogService.Warn("Backup", $"Deleted backup file: {path}");
                        LoadExistingBackups();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, $"Failed to delete file: {ex.Message}", "Delete Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        public void SaveBackupColumnWidths()
        {
            if (_lvBackups.Columns.Count >= 3)
            {
                _configService.Settings.BackupsColumnWidths["Name"] = _lvBackups.Columns[0].Width;
                _configService.Settings.BackupsColumnWidths["Date"] = _lvBackups.Columns[1].Width;
                _configService.Settings.BackupsColumnWidths["Size"] = _lvBackups.Columns[2].Width;
                _configService.SaveConfig(_configService.Settings);
            }
        }

        private void RestoreBackupColumnWidths()
        {
            var dict = _configService.Settings.BackupsColumnWidths;
            if (dict == null || dict.Count == 0) return;

            if (dict.TryGetValue("Name", out int wName) && wName > 30) _lvBackups.Columns[0].Width = wName;
            if (dict.TryGetValue("Date", out int wDate) && wDate > 30) _lvBackups.Columns[1].Width = wDate;
            if (dict.TryGetValue("Size", out int wSize) && wSize > 30) _lvBackups.Columns[2].Width = wSize;
        }

        private static void OpenDirectory(string path)
        {
            try
            {
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch { }
        }
    }
}
