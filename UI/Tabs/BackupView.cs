using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using KerkenezTicket.Services;

namespace KerkenezTicket.UI.Tabs
{
    public class BackupView : UserControl
    {
        private readonly ConfigService _configService;
        private readonly TicketDatabaseService _dbService;
        private readonly BackupService _backupService;

        private Label _lblDbPath = null!;
        private Label _lblDbSize = null!;
        private Label _lblDbTickets = null!;
        private Label _lblStatus = null!;
        private Button _btnExport = null!;
        private Button _btnRestore = null!;
        private Button _btnOpenBackupsFolder = null!;
        private ListView _lvBackups = null!;

        public event Action? TicketsRestored;

        private const int ContentWidth = 760;

        public BackupView(ConfigService configService, TicketDatabaseService dbService)
        {
            _configService = configService;
            _dbService = dbService;
            _backupService = new BackupService(dbService, configService);

            InitializeComponent();
            RefreshInfo();
            LoadExistingBackups();
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
                Width = ContentWidth,
                Height = 65,
                Margin = new Padding(0, 0, 0, 10)
            };

            var lblTitle = new Label
            {
                Text = "💾  Backup & Storage",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.FromArgb(24, 28, 36),
                AutoSize = true,
                Location = new Point(0, 2)
            };

            var lblSubtitle = new Label
            {
                Text = "Export single-file backup packages, restore tickets, and inspect local database storage.",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(108, 117, 125),
                AutoSize = true,
                Location = new Point(2, 34)
            };

            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(lblSubtitle);
            mainFlow.Controls.Add(headerPanel);

            // ==================== Card 1: Single File Backup ====================
            var card1 = CreateCardPanel(ContentWidth);
            card1.Controls.Add(CreateSectionHeader("💾  Export Single-File Backup Package"));

            var lblBackupDesc = new Label
            {
                Text = "Export all tickets, apps, and categories into a single portable backup file (.ktbackup). The file contains everything needed to restore your tickets on any machine or keep safe-keeping archives.",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(70, 75, 85),
                AutoSize = true,
                MaximumSize = new Size(ContentWidth - 40, 0),
                Margin = new Padding(0, 0, 0, 12)
            };
            card1.Controls.Add(lblBackupDesc);

            var rowBtns1 = new FlowLayoutPanel
            {
                Width = ContentWidth - 40,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 8)
            };

            _btnExport = new Button
            {
                Text = "💾  Create Single-File Backup Now (.ktbackup)",
                AutoSize = true,
                Height = 34,
                Padding = new Padding(14, 0, 14, 0),
                BackColor = Color.FromArgb(0, 102, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnExport.FlatAppearance.BorderSize = 0;
            _btnExport.Click += OnExportClicked;

            _btnOpenBackupsFolder = new Button
            {
                Text = "📁 Open Backups Folder",
                AutoSize = true,
                Height = 34,
                Padding = new Padding(12, 0, 12, 0),
                BackColor = Color.FromArgb(240, 242, 245),
                ForeColor = Color.FromArgb(50, 55, 65),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                Margin = new Padding(10, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            _btnOpenBackupsFolder.FlatAppearance.BorderColor = Color.FromArgb(215, 222, 230);
            _btnOpenBackupsFolder.Click += OnOpenBackupsFolderClicked;

            rowBtns1.Controls.Add(_btnExport);
            rowBtns1.Controls.Add(_btnOpenBackupsFolder);
            card1.Controls.Add(rowBtns1);

            _lblStatus = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 167, 69),
                Margin = new Padding(0, 4, 0, 0)
            };
            card1.Controls.Add(_lblStatus);

            mainFlow.Controls.Add(card1);

            // ==================== Card 2: Restore from Backup ====================
            var card2 = CreateCardPanel(ContentWidth);
            card2.Controls.Add(CreateSectionHeader("📥  Restore from Backup Archive"));

            var lblRestoreDesc = new Label
            {
                Text = "Select an existing .ktbackup or .json backup package to import. New tickets are automatically inserted into the local SQLite database.",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(70, 75, 85),
                AutoSize = true,
                MaximumSize = new Size(ContentWidth - 40, 0),
                Margin = new Padding(0, 0, 0, 12)
            };
            card2.Controls.Add(lblRestoreDesc);

            _btnRestore = new Button
            {
                Text = "📥  Select and Restore Backup File...",
                AutoSize = true,
                Height = 34,
                Padding = new Padding(14, 0, 14, 0),
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnRestore.FlatAppearance.BorderSize = 0;
            _btnRestore.Click += OnRestoreClicked;
            card2.Controls.Add(_btnRestore);

            mainFlow.Controls.Add(card2);

            // ==================== Card 3: Storage Info ====================
            var card3 = CreateCardPanel(ContentWidth);
            card3.Controls.Add(CreateSectionHeader("📁  Local Storage Status"));

            _lblDbPath = new Label { Font = new Font("Segoe UI", 9F), ForeColor = Color.FromArgb(40, 40, 40), AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
            _lblDbSize = new Label { Font = new Font("Segoe UI", 9F), ForeColor = Color.FromArgb(40, 40, 40), AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
            _lblDbTickets = new Label { Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(20, 20, 20), AutoSize = true, Margin = new Padding(0, 0, 0, 10) };

            card3.Controls.Add(_lblDbPath);
            card3.Controls.Add(_lblDbSize);
            card3.Controls.Add(_lblDbTickets);

            mainFlow.Controls.Add(card3);

            // ==================== Card 4: Existing Backup Files ====================
            var card4 = CreateCardPanel(ContentWidth);
            card4.Controls.Add(CreateSectionHeader("📂  Saved Backup Files"));

            _lvBackups = new ListView
            {
                Width = ContentWidth - 40,
                Height = 150,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                GridLines = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 8.5F)
            };
            _lvBackups.Columns.Add("File Name", 340);
            _lvBackups.Columns.Add("Date", 160);
            _lvBackups.Columns.Add("Size", 90);
            _lvBackups.ColumnWidthChanged += OnLvBackupsColumnWidthChanged;
            RestoreBackupColumnWidths();
            card4.Controls.Add(_lvBackups);

            mainFlow.Controls.Add(card4);

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
                Padding = new Padding(20, 18, 20, 20),
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
                Margin = new Padding(0, 0, 0, 10)
            };
        }

        public void RefreshInfo()
        {
            string dbPath = ConfigService.DatabaseFilePath;
            _lblDbPath.Text = $"📁 Database File: {dbPath}";

            if (File.Exists(dbPath))
            {
                long bytes = new FileInfo(dbPath).Length;
                _lblDbSize.Text = $"📊 Database Size: {bytes / 1024.0:F1} KB";
            }
            else
            {
                _lblDbSize.Text = "📊 Database Size: 0 KB";
            }

            var stats = _dbService.GetStatistics();
            _lblDbTickets.Text = $"🎟️ Total Stored Tickets: {stats.Total} (Backlog: {stats.Backlog} • Todo: {stats.Todo} • Doing: {stats.Doing} • Done: {stats.Done} • Killed: {stats.Killed})";
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

        private void OnExportClicked(object? sender, EventArgs e)
        {
            using var sfd = new SaveFileDialog
            {
                Title = "Export Kerkenez Ticket Backup",
                Filter = "Kerkenez Ticket Backup (*.ktbackup)|*.ktbackup|JSON Backup (*.json)|*.json",
                FileName = $"kticket_backup_{DateTime.Now:yyyyMMdd_HHmmss}.ktbackup",
                InitialDirectory = !string.IsNullOrWhiteSpace(_configService.Settings.BackupDirectory)
                    ? _configService.Settings.BackupDirectory
                    : ConfigService.BackupsFolder
            };

            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    string path = _backupService.CreateBackup(sfd.FileName);
                    _lblStatus.ForeColor = Color.FromArgb(40, 167, 69);
                    _lblStatus.Text = $"✓ Single-file backup created: {Path.GetFileName(path)} ({new FileInfo(path).Length} bytes)";
                    RefreshInfo();
                    LoadExistingBackups();
                    MessageBox.Show(this, $"Backup successfully saved to:\n{path}", "Backup Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    _lblStatus.ForeColor = Color.FromArgb(220, 53, 69);
                    _lblStatus.Text = $"Backup failed: {ex.Message}";
                }
            }
        }

        private void OnRestoreClicked(object? sender, EventArgs e)
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
                var askOverwrite = MessageBox.Show(this, "Do you want to overwrite existing tickets if they have matching IDs?", "Restore Options", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (askOverwrite == DialogResult.Cancel) return;

                bool overwrite = (askOverwrite == DialogResult.Yes);
                var result = _backupService.RestoreBackup(ofd.FileName, overwrite);

                _lblStatus.ForeColor = Color.FromArgb(40, 167, 69);
                _lblStatus.Text = result.Message;

                RefreshInfo();
                LoadExistingBackups();
                TicketsRestored?.Invoke();

                MessageBox.Show(this, result.Message, "Restore Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void OnOpenBackupsFolderClicked(object? sender, EventArgs e)
        {
            string dir = !string.IsNullOrWhiteSpace(_configService.Settings.BackupDirectory)
                ? _configService.Settings.BackupDirectory
                : ConfigService.BackupsFolder;

            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        public void SaveColumnWidths()
        {
            if (_lvBackups.Columns.Count >= 3)
            {
                _configService.Settings.BackupsColumnWidths["Name"] = _lvBackups.Columns[0].Width;
                _configService.Settings.BackupsColumnWidths["Date"] = _lvBackups.Columns[1].Width;
                _configService.Settings.BackupsColumnWidths["Size"] = _lvBackups.Columns[2].Width;
            }
        }

        private void OnLvBackupsColumnWidthChanged(object? sender, ColumnWidthChangedEventArgs e)
        {
            SaveColumnWidths();
            _configService.SaveConfig(_configService.Settings);
        }

        private void RestoreBackupColumnWidths()
        {
            var dict = _configService.Settings.BackupsColumnWidths;
            if (dict == null || dict.Count == 0) return;

            if (dict.TryGetValue("Name", out int wName) && wName > 30) _lvBackups.Columns[0].Width = wName;
            if (dict.TryGetValue("Date", out int wDate) && wDate > 30) _lvBackups.Columns[1].Width = wDate;
            if (dict.TryGetValue("Size", out int wSize) && wSize > 30) _lvBackups.Columns[2].Width = wSize;
        }
    }
}
