using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using KerkenezTicket.Models;
using KerkenezTicket.Services;
using KerkenezTicket.UI.Controls;
using KerkenezTicket.UI.Tabs;

namespace KerkenezTicket.UI
{
    public class MainForm : Form
    {
        private readonly ConfigService _configService;
        private readonly TicketDatabaseService _dbService;

        private SidebarNav _sidebar = null!;
        private Panel _contentPanel = null!;
        private StatusStrip _statusStrip = null!;
        private ToolStripStatusLabel _lblStatus = null!;
        private ToolStripStatusLabel _lblMetrics = null!;

        private TicketsView _ticketsView = null!;
        private CreateTicketView _createTicketView = null!;
        private AppsView _appsView = null!;
        private SettingsView _settingsView = null!;
        private LogsView _logsView = null!;

        public MainForm(ConfigService? configService = null)
        {
            this.AutoScaleMode = AutoScaleMode.Dpi;

            _configService = configService ?? new ConfigService();
            _dbService = new TicketDatabaseService();
            _configService.SyncKnownApps(_dbService.GetAllAppNames());

            InitializeComponent();

            this.Shown += async (s, e) =>
            {
                await Task.Yield();

                // If window was previously saved as maximized, apply now before applying splitter distances
                if (_configService.Settings.IsMaximized)
                {
                    this.WindowState = FormWindowState.Maximized;
                }

                // Apply saved splitter distances once window layout is fully rendered
                this.BeginInvoke(new Action(() =>
                {
                    _ticketsView.ApplySavedSplitterDistance();
                    _appsView.ApplySavedSplitterDistance();
                }));

                // Check and ensure CLI is installed and in PATH silently
                if (_configService.Settings.CliAutoRegisterPath)
                {
                    await Task.Run(() =>
                    {
                        try
                        {
                            CliInstallerService.EnsureInstalledAndRegistered();
                        }
                        catch { }
                    });
                }

                // First-run: offer to create Desktop & Start Menu shortcuts
                if (!_configService.Settings.ShortcutsCreated)
                {
                    var result = MessageBox.Show(
                        "Would you like to create Desktop and Start Menu shortcuts for Kerkenez Ticket?\n\nYou can always add or remove them later from Settings.",
                        "Create Shortcuts",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        ShortcutService.CreateShortcuts();
                    }

                    // Mark as handled so we never ask again
                    _configService.Settings.ShortcutsCreated = true;
                    _configService.SaveConfig(_configService.Settings);
                }
            };
        }

        private void InitializeComponent()
        {
            this.Text = "Kerkenez Ticket";

            // Load window icon
            try
            {
                using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("KerkenezTicket.app.ico");
                if (stream != null)
                {
                    this.Icon = new Icon(stream);
                }
                else if (File.Exists("app.ico"))
                {
                    this.Icon = new Icon("app.ico");
                }
            }
            catch { }

            // ================= Restore Window Dimensions & Position =================
            var currentScreen = Screen.FromPoint(Cursor.Position) ?? Screen.PrimaryScreen;
            var workingArea = currentScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);

            double widthScale = _configService.Settings.WindowWidthScale > 0.1 && _configService.Settings.WindowWidthScale <= 1.0
                ? _configService.Settings.WindowWidthScale
                : 0.60;
            double heightScale = _configService.Settings.WindowHeightScale > 0.1 && _configService.Settings.WindowHeightScale <= 1.0
                ? _configService.Settings.WindowHeightScale
                : 0.56;

            int targetWidth = _configService.Settings.WindowWidth >= 960
                ? _configService.Settings.WindowWidth
                : (int)Math.Round(workingArea.Width * widthScale);
            int targetHeight = _configService.Settings.WindowHeight >= 540
                ? _configService.Settings.WindowHeight
                : (int)Math.Round(workingArea.Height * heightScale);

            targetWidth = Math.Clamp(targetWidth, 960, workingArea.Width);
            targetHeight = Math.Clamp(targetHeight, 540, workingArea.Height);

            this.MinimumSize = new Size(960, 540);
            this.Size = new Size(targetWidth, targetHeight);
            this.StartPosition = FormStartPosition.Manual;

            int savedLeft = _configService.Settings.WindowLeft;
            int savedTop = _configService.Settings.WindowTop;
            bool isPositionValid = false;

            if (savedLeft >= -500 && savedTop >= -500)
            {
                var candidateRect = new Rectangle(savedLeft, savedTop, targetWidth, targetHeight);
                isPositionValid = Screen.AllScreens.Any(sc => sc.WorkingArea.IntersectsWith(candidateRect));
            }

            if (isPositionValid)
            {
                this.Location = new Point(savedLeft, savedTop);
            }
            else
            {
                this.Location = new Point(
                    workingArea.Left + Math.Max(0, (workingArea.Width - targetWidth) / 2),
                    workingArea.Top + Math.Max(0, (workingArea.Height - targetHeight) / 2)
                );
            }

            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.BackColor = Color.FromArgb(248, 249, 250);

            // 1. Status Strip
            _statusStrip = new StatusStrip
            {
                Dock = DockStyle.Bottom,
                BackColor = Color.FromArgb(242, 244, 247),
                Font = new Font("Segoe UI", 8.5F),
                Height = 26
            };

            _lblStatus = new ToolStripStatusLabel
            {
                Text = "Ready | SQLite DB: %APPDATA%\\Kerkenez\\ticket\\tickets.db",
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(50, 50, 50)
            };

            _lblMetrics = new ToolStripStatusLabel
            {
                Text = "Loading tickets...",
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.FromArgb(80, 80, 80)
            };

            _statusStrip.Items.Add(_lblStatus);
            _statusStrip.Items.Add(_lblMetrics);

            // 2. Tab Views
            _ticketsView = new TicketsView(_configService, _dbService);
            _ticketsView.StatusUpdated += (msg, metrics) =>
            {
                _lblStatus.Text = msg;
                _lblMetrics.Text = metrics;
            };
            _ticketsView.CreateTicketRequested += () => ShowTab(1);

            _createTicketView = new CreateTicketView(_configService, _dbService);
            _createTicketView.TicketCreated += (created) =>
            {
                ShowTab(0);
                _ticketsView.SelectTicketById(created.Id);
            };

            _appsView = new AppsView(_configService, _dbService);
            _appsView.AppsChanged += () =>
            {
                _ticketsView.RefreshAll();
                _createTicketView.ResetFormDefaults();
                _settingsView.LoadSettings();
            };

            _settingsView = new SettingsView(_configService, _dbService);
            _settingsView.TicketsRestored += () =>
            {
                _ticketsView.RefreshAll();
                _appsView.LoadData();
                _createTicketView.ResetFormDefaults();
            };
            _settingsView.SettingsSaved += () =>
            {
                if (_sidebar.IsCollapsed != _configService.Settings.CollapseSidebarByDefault)
                {
                    _sidebar.IsCollapsed = _configService.Settings.CollapseSidebarByDefault;
                }

                _ticketsView.RefreshAll();
                _createTicketView.ResetFormDefaults();
                _appsView.LoadData();
                _lblStatus.Text = "Settings saved to %APPDATA%\\Kerkenez\\ticket\\config.json";
            };

            _logsView = new LogsView();

            // 3. Content Panel
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(248, 249, 250)
            };

            _contentPanel.Controls.Add(_ticketsView);
            _contentPanel.Controls.Add(_createTicketView);
            _contentPanel.Controls.Add(_appsView);
            _contentPanel.Controls.Add(_settingsView);
            _contentPanel.Controls.Add(_logsView);

            // 4. Left Sidebar Navigation
            _sidebar = new SidebarNav();
            _sidebar.IsCollapsed = _configService.Settings.CollapseSidebarByDefault;
            _sidebar.TabChanged += (s, index) => ShowTab(index);
            _sidebar.CollapsedChanged += (s, isCollapsed) =>
            {
                _configService.Settings.CollapseSidebarByDefault = isCollapsed;
                _configService.SaveConfig(_configService.Settings);
            };

            // Assemble Form
            this.Controls.Add(_contentPanel);
            this.Controls.Add(_sidebar);
            this.Controls.Add(_statusStrip);

            // Log startup
            LogService.Info("Application", "Kerkenez Ticket initialized.");
            LogService.Info("Database", $"SQLite storage active: {ConfigService.DatabaseFilePath}");

            // Persist window size on resize end
            this.ResizeEnd += (s, e) =>
            {
                if (this.WindowState == FormWindowState.Normal)
                {
                    _configService.Settings.IsMaximized = false;
                    _configService.Settings.WindowWidth = this.Width;
                    _configService.Settings.WindowHeight = this.Height;
                    _configService.Settings.WindowLeft = this.Left;
                    _configService.Settings.WindowTop = this.Top;
                    _configService.SaveConfig(_configService.Settings);
                }
            };

            // Persist all user states, positions, splitters, and column widths on close
            this.FormClosing += (s, e) =>
            {
                try
                {
                    if (this.WindowState == FormWindowState.Normal)
                    {
                        _configService.Settings.IsMaximized = false;
                        _configService.Settings.WindowWidth = this.Width;
                        _configService.Settings.WindowHeight = this.Height;
                        _configService.Settings.WindowLeft = this.Left;
                        _configService.Settings.WindowTop = this.Top;
                    }
                    else if (this.WindowState == FormWindowState.Maximized)
                    {
                        _configService.Settings.IsMaximized = true;
                        _configService.Settings.WindowWidth = this.RestoreBounds.Width;
                        _configService.Settings.WindowHeight = this.RestoreBounds.Height;
                        _configService.Settings.WindowLeft = this.RestoreBounds.Left;
                        _configService.Settings.WindowTop = this.RestoreBounds.Top;
                    }

                    _configService.Settings.CollapseSidebarByDefault = _sidebar.IsCollapsed;
                    _configService.Settings.SidebarWidth = SidebarNav.ExpandedWidth;

                    _ticketsView.SaveColumnWidths();
                    _appsView.SaveColumnWidths();
                    _settingsView.SaveBackupColumnWidths();

                    _configService.SaveConfig(_configService.Settings);

                    if (_configService.Settings.AutoBackupOnExit)
                    {
                        var backup = new BackupService(_dbService, _configService);
                        backup.CreateBackup();
                    }
                }
                catch { }
            };

            // Initial view
            ShowTab(0);
        }

        private void ShowTab(int index)
        {
            if (index >= 0 && index < 5)
            {
                _sidebar.SelectedIndex = index;
            }
            else
            {
                _sidebar.SelectedIndex = -1;
            }

            _ticketsView.Visible = (index == 0);
            _createTicketView.Visible = (index == 1);
            _appsView.Visible = (index == 2);
            _settingsView.Visible = (index == 3);
            _logsView.Visible = (index == 4);

            if (index == 0)
            {
                _ticketsView.BringToFront();
                _ticketsView.RefreshAll();
                _ticketsView.ApplySavedSplitterDistance();
            }
            else if (index == 1)
            {
                _createTicketView.BringToFront();
                _createTicketView.ResetFormDefaults();
            }
            else if (index == 2)
            {
                _appsView.BringToFront();
                _appsView.LoadData();
                _appsView.ApplySavedSplitterDistance();
            }
            else if (index == 3)
            {
                _settingsView.BringToFront();
                _settingsView.LoadSettings();
            }
            else if (index == 4)
            {
                _logsView.BringToFront();
            }
        }
    }
}
