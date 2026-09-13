using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using KerkenezTicket.Models;
using KerkenezTicket.Services;

namespace KerkenezTicket.UI.Tabs
{
    public class AppsView : UserControl
    {
        private readonly ConfigService _configService;
        private readonly TicketDatabaseService _dbService;

        // Top Navigation Switcher (Applications vs Ticket Types)
        private Button _btnSwitchApps = null!;
        private Button _btnSwitchTypes = null!;
        private bool _isShowingTypes = false;

        // SplitContainer
        private SplitContainer _split = null!;

        // ================= Left Views =================
        private ListView _lvApps = null!;
        private ListView _lvTypes = null!;

        // Column Sorting State
        private enum SortState
        {
            Default = 0,
            Descending = 1,
            Ascending = 2
        }

        private int _sortColumnApps = -1;
        private SortState _sortStateApps = SortState.Default;

        private static readonly string[] AppsColumnTitles = new[]
        {
            "App Key", "Display Name", "Open", "Total", "Description"
        };

        // ================= Right Form: Apps =================
        private Panel _pnlRightHolder = null!;
        private FlowLayoutPanel _cardApp = null!;
        private TextBox _txtAppName = null!;
        private TextBox _txtAppDisplayName = null!;
        private TextBox _txtAppColorHex = null!;
        private TextBox _txtAppDesc = null!;
        private Button _btnSaveApp = null!;
        private Button _btnDeleteApp = null!;
        private Panel _pnlAppColorSwatch = null!;
        private Button _btnPickAppColor = null!;

        // ================= Right Form: Types =================
        private FlowLayoutPanel _cardType = null!;
        private TextBox _txtTypeName = null!;
        private TextBox _txtTypeColorHex = null!;
        private Button _btnSaveType = null!;
        private Button _btnDeleteType = null!;
        private Panel _pnlTypeColorSwatch = null!;
        private Button _btnPickTypeColor = null!;

        public event Action? AppsChanged;

        public int SplitterDistance
        {
            get => _split.SplitterDistance;
            set
            {
                try
                {
                    if (_split.Width > 400 && value >= _split.Panel1MinSize && value <= _split.Width - _split.Panel2MinSize)
                    {
                        _split.SplitterDistance = value;
                    }
                }
                catch { }
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            this.BeginInvoke(new Action(() => ApplySavedSplitterDistance()));
        }

        public void ApplySavedSplitterDistance()
        {
            try
            {
                int totalW = _split.ClientSize.Width > 0 ? _split.ClientSize.Width : _split.Width;
                if (totalW <= 200)
                {
                    EventHandler? onResize = null;
                    onResize = (s, e) =>
                    {
                        if (_split.Width > 200)
                        {
                            _split.Resize -= onResize;
                            ApplySavedSplitterDistance();
                        }
                    };
                    _split.Resize += onResize;
                    return;
                }

                int savedW = _configService.Settings.AppDetailPanelWidth;
                int rightW;

                if (savedW >= 150 && savedW <= totalW - 150)
                {
                    rightW = savedW;
                }
                else
                {
                    // Screen-proportional initial size: ~40% of available content width (clamped 380..500)
                    rightW = Math.Clamp((int)(totalW * 0.40), 380, Math.Min(500, totalW - 200));
                    if (rightW < 150) rightW = Math.Max(100, totalW / 2);
                    _configService.Settings.AppDetailPanelWidth = rightW;
                    _configService.SaveConfig(_configService.Settings);
                }

                int targetDist = totalW - rightW - _split.SplitterWidth;
                if (targetDist < 100) targetDist = 100;
                if (targetDist > totalW - 100) targetDist = totalW - 100;

                try
                {
                    _split.Panel1MinSize = Math.Min(150, targetDist);
                    _split.Panel2MinSize = Math.Min(150, rightW);
                }
                catch { }

                _split.SplitterDistance = targetDist;
                _configService.Settings.AppsSplitterDistance = _split.SplitterDistance;
                LogService.Info("UI", $"Apps right sidebar width restored: {rightW}px");
            }
            catch (Exception ex)
            {
                LogService.Warn("UI", $"Could not apply apps right sidebar width: {ex.Message}");
            }
        }

        public AppsView(ConfigService configService, TicketDatabaseService dbService)
        {
            _configService = configService;
            _dbService = dbService;

            InitializeComponent();
            LoadData();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(248, 249, 250);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // 1. Top Panel with Segment Switcher
            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 84,
                BackColor = Color.White,
                Padding = new Padding(22, 14, 20, 14)
            };
            topPanel.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(222, 226, 230), 1);
                e.Graphics.DrawLine(p, 0, topPanel.Height - 1, topPanel.Width, topPanel.Height - 1);
            };

            // Switcher Buttons on Top Right
            var flowSwitcher = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };

            _btnSwitchApps = new Button
            {
                Text = "📱  Applications",
                AutoSize = true,
                Height = 34,
                Padding = new Padding(14, 0, 14, 0),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 102, 204),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 11, 8, 0)
            };
            _btnSwitchApps.FlatAppearance.BorderSize = 0;
            _btnSwitchApps.Click += (s, e) => SwitchSegment(false);

            _btnSwitchTypes = new Button
            {
                Text = "🏷️  Ticket Types",
                AutoSize = true,
                Height = 34,
                Padding = new Padding(14, 0, 14, 0),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                BackColor = Color.FromArgb(245, 247, 250),
                ForeColor = Color.FromArgb(50, 55, 65),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 11, 0, 0)
            };
            _btnSwitchTypes.FlatAppearance.BorderColor = Color.FromArgb(215, 222, 230);
            _btnSwitchTypes.Click += (s, e) => SwitchSegment(true);

            flowSwitcher.Controls.Add(_btnSwitchApps);
            flowSwitcher.Controls.Add(_btnSwitchTypes);

            // Left Header Info Panel (Docked Fill so subtitle is constrained and never collides with buttons)
            var pnlHeaderText = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };

            var lblHeader = new Label
            {
                Text = "📱  Applications & Ticket Types",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Color.FromArgb(24, 28, 36),
                AutoSize = true,
                UseMnemonic = false,
                Location = new Point(0, 1)
            };

            var lblSub = new Label
            {
                Text = "Manage tracked applications and ticket triage categories.",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(108, 117, 125),
                Location = new Point(2, 31),
                Height = 22,
                AutoEllipsis = true,
                AutoSize = false
            };

            pnlHeaderText.Resize += (s, e) =>
            {
                lblSub.Width = Math.Max(50, pnlHeaderText.Width - 16);
            };

            pnlHeaderText.Controls.Add(lblHeader);
            pnlHeaderText.Controls.Add(lblSub);

            topPanel.Controls.Add(pnlHeaderText);
            topPanel.Controls.Add(flowSwitcher);

            // 2. Master-Detail SplitContainer
            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                BackColor = Color.FromArgb(222, 226, 230),
                SplitterWidth = 6,
                FixedPanel = FixedPanel.Panel2
            };

            _split.SplitterMoved += (s, e) =>
            {
                int totalW = _split.ClientSize.Width > 0 ? _split.ClientSize.Width : _split.Width;
                if (totalW > 200)
                {
                    int rightW = totalW - _split.SplitterDistance - _split.SplitterWidth;
                    if (rightW >= 100 && rightW <= totalW - 100)
                    {
                        _configService.Settings.AppDetailPanelWidth = rightW;
                        _configService.Settings.AppsSplitterDistance = _split.SplitterDistance;
                        _configService.SaveConfig(_configService.Settings);
                        LogService.Info("UI", $"Apps right sidebar width saved: {rightW}px");
                    }
                }
            };

            // Left Holder (Contains both ListViews)
            var leftHolder = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(0)
            };

            // 2a. Apps ListView
            _lvApps = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                GridLines = true,
                BorderStyle = BorderStyle.None,
                BackColor = Color.White,
                Font = new Font("Segoe UI", 9F)
            };
            _lvApps.Columns.Add("App Key", 95);
            _lvApps.Columns.Add("Display Name", 140);
            _lvApps.Columns.Add("Open", 55, HorizontalAlignment.Right);
            _lvApps.Columns.Add("Total", 55, HorizontalAlignment.Right);
            _lvApps.Columns.Add("Description", 200);
            _lvApps.SelectedIndexChanged += OnAppSelectionChanged;
            _lvApps.ColumnWidthChanged += OnLvAppsColumnWidthChanged;
            _lvApps.ColumnClick += OnAppsColumnClick;
            RestoreAppsColumnWidths();

            // 2b. Types ListView
            _lvTypes = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                GridLines = true,
                BorderStyle = BorderStyle.None,
                BackColor = Color.White,
                Font = new Font("Segoe UI", 9F),
                Visible = false
            };
            _lvTypes.Columns.Add("Type Name", 130);
            _lvTypes.Columns.Add("Badge Color", 110);
            _lvTypes.Columns.Add("Tickets Count", 110, HorizontalAlignment.Right);
            _lvTypes.SelectedIndexChanged += OnTypeSelectionChanged;

            leftHolder.Controls.Add(_lvApps);
            leftHolder.Controls.Add(_lvTypes);
            _split.Panel1.Controls.Add(leftHolder);

            // Right Holder (Contains both Editor Cards)
            _pnlRightHolder = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(248, 249, 250),
                Padding = new Padding(20),
                AutoScroll = true
            };

            // 2c. Card: Add / Edit Application
            _cardApp = CreateAppEditorCard();
            // 2d. Card: Add / Edit Ticket Type
            _cardType = CreateTypeEditorCard();
            _cardType.Visible = false;

            _pnlRightHolder.Controls.Add(_cardApp);
            _pnlRightHolder.Controls.Add(_cardType);
            _split.Panel2.Controls.Add(_pnlRightHolder);

            this.Controls.Add(_split);
            this.Controls.Add(topPanel);
        }

        private FlowLayoutPanel CreateAppEditorCard()
        {
            var card = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.White,
                Padding = new Padding(22, 18, 22, 20),
                Margin = new Padding(0)
            };
            card.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(222, 226, 230), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };

            var lblTitle = new Label
            {
                Text = "⚙️  Add or Edit Application",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 35, 45),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 14)
            };
            card.Controls.Add(lblTitle);

            // App Key
            var lblKey = new Label { Text = "App Key (lowercase, e.g. web, api, client) *", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.FromArgb(50, 50, 50), AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
            _txtAppName = new TextBox { Width = 320, Height = 28, Font = new Font("Segoe UI", 9F), Margin = new Padding(0, 0, 0, 10) };
            card.Controls.Add(lblKey);
            card.Controls.Add(_txtAppName);

            // Display Name
            var lblDisp = new Label { Text = "Display Name (e.g. My Web App)", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.FromArgb(50, 50, 50), AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
            _txtAppDisplayName = new TextBox { Width = 320, Height = 28, Font = new Font("Segoe UI", 9F), Margin = new Padding(0, 0, 0, 10) };
            card.Controls.Add(lblDisp);
            card.Controls.Add(_txtAppDisplayName);

            // Badge Color, Live Swatch & Windows Color Picker
            var lblColor = new Label { Text = "Badge Color:", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.FromArgb(50, 50, 50), AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
            card.Controls.Add(lblColor);

            var rowColor = new FlowLayoutPanel
            {
                Width = 320,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 10)
            };

            _pnlAppColorSwatch = new Panel
            {
                Width = 28,
                Height = 28,
                BackColor = ColorTranslator.FromHtml("#0078D7"),
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 6, 0)
            };

            _txtAppColorHex = new TextBox
            {
                Width = 95,
                Height = 28,
                Font = new Font("Segoe UI", 9F),
                Text = "#0078D7",
                CharacterCasing = CharacterCasing.Upper,
                Margin = new Padding(0, 0, 6, 0)
            };

            _btnPickAppColor = new Button
            {
                Text = "🎨 Pick Color...",
                AutoSize = true,
                Height = 28,
                Padding = new Padding(8, 0, 8, 0),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                BackColor = Color.FromArgb(240, 243, 248),
                ForeColor = Color.FromArgb(45, 55, 72),
                Cursor = Cursors.Hand,
                Margin = new Padding(0)
            };
            _btnPickAppColor.FlatAppearance.BorderColor = Color.FromArgb(215, 222, 230);

            Action openAppColorDialog = () =>
            {
                using var dlg = new ColorDialog
                {
                    FullOpen = true,
                    Color = ParseColor(_txtAppColorHex.Text, Color.FromArgb(0, 120, 215))
                };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    string hex = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
                    _txtAppColorHex.Text = hex;
                    _pnlAppColorSwatch.BackColor = dlg.Color;
                }
            };

            _pnlAppColorSwatch.Click += (s, e) => openAppColorDialog();
            _btnPickAppColor.Click += (s, e) => openAppColorDialog();

            _txtAppColorHex.TextChanged += (s, e) =>
            {
                Color c = ParseColor(_txtAppColorHex.Text, Color.Empty);
                if (c != Color.Empty)
                {
                    _pnlAppColorSwatch.BackColor = c;
                }
            };

            rowColor.Controls.Add(_pnlAppColorSwatch);
            rowColor.Controls.Add(_txtAppColorHex);
            rowColor.Controls.Add(_btnPickAppColor);
            card.Controls.Add(rowColor);

            // Description
            var lblDesc = new Label { Text = "Description:", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.FromArgb(50, 50, 50), AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
            _txtAppDesc = new TextBox { Width = 320, Height = 55, Multiline = true, Font = new Font("Segoe UI", 9F), Margin = new Padding(0, 0, 0, 14) };
            card.Controls.Add(lblDesc);
            card.Controls.Add(_txtAppDesc);

            // Action Buttons
            var rowBtns = new FlowLayoutPanel
            {
                Width = 320,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };

            _btnSaveApp = new Button
            {
                Text = "💾 Save App",
                AutoSize = true,
                Height = 34,
                Padding = new Padding(16, 0, 16, 0),
                BackColor = Color.FromArgb(0, 102, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnSaveApp.FlatAppearance.BorderSize = 0;
            _btnSaveApp.Click += OnSaveAppClicked;

            _btnDeleteApp = new Button
            {
                Text = "🗑 Delete",
                AutoSize = true,
                Height = 34,
                Padding = new Padding(14, 0, 14, 0),
                BackColor = Color.FromArgb(240, 242, 245),
                ForeColor = Color.FromArgb(220, 53, 69),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                Margin = new Padding(8, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            _btnDeleteApp.FlatAppearance.BorderColor = Color.FromArgb(215, 222, 230);
            _btnDeleteApp.Click += OnDeleteAppClicked;

            rowBtns.Controls.Add(_btnSaveApp);
            rowBtns.Controls.Add(_btnDeleteApp);
            card.Controls.Add(rowBtns);

            return card;
        }

        private FlowLayoutPanel CreateTypeEditorCard()
        {
            var card = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.White,
                Padding = new Padding(22, 18, 22, 20),
                Margin = new Padding(0)
            };
            card.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(222, 226, 230), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };

            var lblTitle = new Label
            {
                Text = "🏷️  Add or Edit Ticket Type",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 35, 45),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 14)
            };
            card.Controls.Add(lblTitle);

            // Type Name
            var lblName = new Label { Text = "Type Name (e.g. Bug, Feature, Sync, Task, Refactor) *", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.FromArgb(50, 50, 50), AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
            _txtTypeName = new TextBox { Width = 320, Height = 28, Font = new Font("Segoe UI", 9F), Margin = new Padding(0, 0, 0, 10) };
            card.Controls.Add(lblName);
            card.Controls.Add(_txtTypeName);

            // Badge Color, Live Swatch & Windows Color Picker
            var lblColor = new Label { Text = "Badge Color:", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.FromArgb(50, 50, 50), AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
            card.Controls.Add(lblColor);

            var rowColor = new FlowLayoutPanel
            {
                Width = 320,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 14)
            };

            _pnlTypeColorSwatch = new Panel
            {
                Width = 28,
                Height = 28,
                BackColor = ColorTranslator.FromHtml("#D9534F"),
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 6, 0)
            };

            _txtTypeColorHex = new TextBox
            {
                Width = 95,
                Height = 28,
                Font = new Font("Segoe UI", 9F),
                Text = "#D9534F",
                CharacterCasing = CharacterCasing.Upper,
                Margin = new Padding(0, 0, 6, 0)
            };

            _btnPickTypeColor = new Button
            {
                Text = "🎨 Pick Color...",
                AutoSize = true,
                Height = 28,
                Padding = new Padding(8, 0, 8, 0),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                BackColor = Color.FromArgb(240, 243, 248),
                ForeColor = Color.FromArgb(45, 55, 72),
                Cursor = Cursors.Hand,
                Margin = new Padding(0)
            };
            _btnPickTypeColor.FlatAppearance.BorderColor = Color.FromArgb(215, 222, 230);

            Action openTypeColorDialog = () =>
            {
                using var dlg = new ColorDialog
                {
                    FullOpen = true,
                    Color = ParseColor(_txtTypeColorHex.Text, Color.FromArgb(217, 83, 79))
                };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    string hex = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
                    _txtTypeColorHex.Text = hex;
                    _pnlTypeColorSwatch.BackColor = dlg.Color;
                }
            };

            _pnlTypeColorSwatch.Click += (s, e) => openTypeColorDialog();
            _btnPickTypeColor.Click += (s, e) => openTypeColorDialog();

            _txtTypeColorHex.TextChanged += (s, e) =>
            {
                Color c = ParseColor(_txtTypeColorHex.Text, Color.Empty);
                if (c != Color.Empty)
                {
                    _pnlTypeColorSwatch.BackColor = c;
                }
            };

            rowColor.Controls.Add(_pnlTypeColorSwatch);
            rowColor.Controls.Add(_txtTypeColorHex);
            rowColor.Controls.Add(_btnPickTypeColor);
            card.Controls.Add(rowColor);

            // Action Buttons
            var rowBtns = new FlowLayoutPanel
            {
                Width = 320,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };

            _btnSaveType = new Button
            {
                Text = "💾 Save Type",
                AutoSize = true,
                Height = 34,
                Padding = new Padding(16, 0, 16, 0),
                BackColor = Color.FromArgb(0, 102, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnSaveType.FlatAppearance.BorderSize = 0;
            _btnSaveType.Click += OnSaveTypeClicked;

            _btnDeleteType = new Button
            {
                Text = "🗑 Delete",
                AutoSize = true,
                Height = 34,
                Padding = new Padding(14, 0, 14, 0),
                BackColor = Color.FromArgb(240, 242, 245),
                ForeColor = Color.FromArgb(220, 53, 69),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                Margin = new Padding(8, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            _btnDeleteType.FlatAppearance.BorderColor = Color.FromArgb(215, 222, 230);
            _btnDeleteType.Click += OnDeleteTypeClicked;

            rowBtns.Controls.Add(_btnSaveType);
            rowBtns.Controls.Add(_btnDeleteType);
            card.Controls.Add(rowBtns);

            return card;
        }

        private void SwitchSegment(bool showTypes)
        {
            _isShowingTypes = showTypes;

            _btnSwitchApps.BackColor = !showTypes ? Color.FromArgb(0, 102, 204) : Color.FromArgb(245, 247, 250);
            _btnSwitchApps.ForeColor = !showTypes ? Color.White : Color.FromArgb(50, 55, 65);
            _btnSwitchApps.Font = new Font("Segoe UI", 9F, !showTypes ? FontStyle.Bold : FontStyle.Regular);

            _btnSwitchTypes.BackColor = showTypes ? Color.FromArgb(0, 102, 204) : Color.FromArgb(245, 247, 250);
            _btnSwitchTypes.ForeColor = showTypes ? Color.White : Color.FromArgb(50, 55, 65);
            _btnSwitchTypes.Font = new Font("Segoe UI", 9F, showTypes ? FontStyle.Bold : FontStyle.Regular);

            _lvApps.Visible = !showTypes;
            _cardApp.Visible = !showTypes;

            _lvTypes.Visible = showTypes;
            _cardType.Visible = showTypes;

            if (showTypes)
            {
                LoadTypes();
            }
            else
            {
                LoadApps();
            }
        }

        public void LoadData()
        {
            LoadApps();
            LoadTypes();
        }

        public void LoadApps()
        {
            _lvApps.Items.Clear();
            var categories = _dbService.GetAppCategories();

            if (_sortColumnApps >= 0 && _sortStateApps != SortState.Default)
            {
                bool desc = (_sortStateApps == SortState.Descending);
                categories = _sortColumnApps switch
                {
                    0 => desc // App Key
                        ? categories.OrderByDescending(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList()
                        : categories.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList(),

                    1 => desc // Display Name
                        ? categories.OrderByDescending(a => a.DisplayName, StringComparer.OrdinalIgnoreCase).ToList()
                        : categories.OrderBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase).ToList(),

                    2 => desc // Open count
                        ? categories.OrderByDescending(a => a.OpenCount).ThenBy(a => a.Name).ToList()
                        : categories.OrderBy(a => a.OpenCount).ThenBy(a => a.Name).ToList(),

                    3 => desc // Total count
                        ? categories.OrderByDescending(a => a.TotalCount).ThenBy(a => a.Name).ToList()
                        : categories.OrderBy(a => a.TotalCount).ThenBy(a => a.Name).ToList(),

                    4 => desc // Description
                        ? categories.OrderByDescending(a => a.Description, StringComparer.OrdinalIgnoreCase).ToList()
                        : categories.OrderBy(a => a.Description, StringComparer.OrdinalIgnoreCase).ToList(),

                    _ => categories
                };
            }

            foreach (var app in categories)
            {
                var lvi = new ListViewItem(app.Name);
                lvi.UseItemStyleForSubItems = false;

                Color appColor = app.GetColor();
                lvi.SubItems[0].ForeColor = appColor;
                lvi.SubItems[0].Font = new Font(_lvApps.Font, FontStyle.Bold);

                var subDisp = lvi.SubItems.Add(app.DisplayName);
                subDisp.ForeColor = appColor;

                lvi.SubItems.Add(app.OpenCount.ToString());
                lvi.SubItems.Add(app.TotalCount.ToString());
                lvi.SubItems.Add(app.Description);
                lvi.Tag = app;
                _lvApps.Items.Add(lvi);
            }
        }

        public void LoadTypes()
        {
            _lvTypes.Items.Clear();
            var types = _dbService.GetDetailedTicketTypes();

            foreach (var t in types)
            {
                var lvi = new ListViewItem(t.Name);
                lvi.SubItems.Add(t.ColorHex);
                lvi.SubItems.Add(t.TicketCount.ToString());
                lvi.Tag = t;
                _lvTypes.Items.Add(lvi);
            }
        }

        private void OnAppSelectionChanged(object? sender, EventArgs e)
        {
            if (_lvApps.SelectedItems.Count > 0 && _lvApps.SelectedItems[0].Tag is AppCategory app)
            {
                _txtAppName.Text = app.Name;
                _txtAppDisplayName.Text = app.DisplayName;
                _txtAppColorHex.Text = app.ColorHex;
                _pnlAppColorSwatch.BackColor = app.GetColor();
                _txtAppDesc.Text = app.Description;
            }
        }

        private void OnTypeSelectionChanged(object? sender, EventArgs e)
        {
            if (_lvTypes.SelectedItems.Count > 0 && _lvTypes.SelectedItems[0].Tag is TicketTypeCategory t)
            {
                _txtTypeName.Text = t.Name;
                _txtTypeColorHex.Text = t.ColorHex;
                _pnlTypeColorSwatch.BackColor = ParseColor(t.ColorHex, Color.FromArgb(217, 83, 79));
            }
        }

        private void OnSaveAppClicked(object? sender, EventArgs e)
        {
            string name = _txtAppName.Text.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(this, "App key is required.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string disp = string.IsNullOrWhiteSpace(_txtAppDisplayName.Text) ? name : _txtAppDisplayName.Text.Trim();
            string color = string.IsNullOrWhiteSpace(_txtAppColorHex.Text) ? "#0078D7" : _txtAppColorHex.Text.Trim();
            string desc = _txtAppDesc.Text.Trim();

            _dbService.AddAppCategory(name, disp, color, desc);

            if (!_configService.Settings.KnownApps.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                _configService.Settings.KnownApps.Add(name);
                _configService.SaveConfig(_configService.Settings);
            }

            _txtAppName.Text = "";
            _txtAppDisplayName.Text = "";
            _txtAppDesc.Text = "";
            _txtAppColorHex.Text = "#0078D7";

            LoadApps();
            AppsChanged?.Invoke();
        }

        private void OnDeleteAppClicked(object? sender, EventArgs e)
        {
            if (_lvApps.SelectedItems.Count == 0) return;

            var lvi = _lvApps.SelectedItems[0];
            string name = lvi.Text;

            var confirm = MessageBox.Show(this, $"Are you sure you want to delete app '{name}'? Associated tickets will remain.", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm == DialogResult.Yes)
            {
                _dbService.DeleteAppCategory(name);
                _configService.Settings.KnownApps.RemoveAll(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
                _configService.SaveConfig(_configService.Settings);

                _txtAppName.Text = "";
                _txtAppDisplayName.Text = "";
                _txtAppDesc.Text = "";

                LoadApps();
                AppsChanged?.Invoke();
            }
        }

        private void OnSaveTypeClicked(object? sender, EventArgs e)
        {
            string typeName = _txtTypeName.Text.Trim();
            if (string.IsNullOrWhiteSpace(typeName))
            {
                MessageBox.Show(this, "Ticket type name is required.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string color = string.IsNullOrWhiteSpace(_txtTypeColorHex.Text) ? "#0078D7" : _txtTypeColorHex.Text.Trim();

            _dbService.AddTicketType(typeName, color);

            if (!_configService.Settings.KnownTypes.Contains(typeName, StringComparer.OrdinalIgnoreCase))
            {
                _configService.Settings.KnownTypes.Add(typeName);
                _configService.SaveConfig(_configService.Settings);
            }

            _txtTypeName.Text = "";
            _txtTypeColorHex.Text = "#D9534F";

            LoadTypes();
            AppsChanged?.Invoke();
        }

        private void OnDeleteTypeClicked(object? sender, EventArgs e)
        {
            if (_lvTypes.SelectedItems.Count == 0) return;

            var lvi = _lvTypes.SelectedItems[0];
            string typeName = lvi.Text;

            var confirm = MessageBox.Show(this, $"Are you sure you want to delete ticket type '{typeName}'? Existing tickets will retain their type label.", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm == DialogResult.Yes)
            {
                _dbService.DeleteTicketType(typeName);
                _configService.Settings.KnownTypes.RemoveAll(t => string.Equals(t, typeName, StringComparison.OrdinalIgnoreCase));
                _configService.SaveConfig(_configService.Settings);

                _txtTypeName.Text = "";
                _txtTypeColorHex.Text = "#D9534F";

                LoadTypes();
                AppsChanged?.Invoke();
            }
        }

        public void SaveColumnWidths()
        {
            if (_lvApps.Columns.Count >= 5)
            {
                _configService.Settings.AppsColumnWidths["Key"] = _lvApps.Columns[0].Width;
                _configService.Settings.AppsColumnWidths["Name"] = _lvApps.Columns[1].Width;
                _configService.Settings.AppsColumnWidths["Open"] = _lvApps.Columns[2].Width;
                _configService.Settings.AppsColumnWidths["Total"] = _lvApps.Columns[3].Width;
                _configService.Settings.AppsColumnWidths["Desc"] = _lvApps.Columns[4].Width;
            }

            int totalW = _split.ClientSize.Width > 0 ? _split.ClientSize.Width : _split.Width;
            if (totalW > 300)
            {
                int rightW = totalW - _split.SplitterDistance - _split.SplitterWidth;
                if (rightW >= 150 && rightW <= totalW - 150)
                {
                    _configService.Settings.AppDetailPanelWidth = rightW;
                    _configService.Settings.AppsSplitterDistance = _split.SplitterDistance;
                }
            }
        }

        private void OnLvAppsColumnWidthChanged(object? sender, ColumnWidthChangedEventArgs e)
        {
            SaveColumnWidths();
            _configService.SaveConfig(_configService.Settings);
        }

        private void RestoreAppsColumnWidths()
        {
            var dict = _configService.Settings.AppsColumnWidths;
            if (dict == null || dict.Count == 0) return;

            if (dict.TryGetValue("Key", out int wKey) && wKey > 30) _lvApps.Columns[0].Width = wKey;
            if (dict.TryGetValue("Name", out int wName) && wName > 30) _lvApps.Columns[1].Width = wName;
            if (dict.TryGetValue("Open", out int wOpen) && wOpen > 30) _lvApps.Columns[2].Width = wOpen;
            if (dict.TryGetValue("Total", out int wTot) && wTot > 30) _lvApps.Columns[3].Width = wTot;
            if (dict.TryGetValue("Desc", out int wDesc) && wDesc > 30) _lvApps.Columns[4].Width = wDesc;
        }

        private void OnAppsColumnClick(object? sender, ColumnClickEventArgs e)
        {
            int col = e.Column;
            if (col < 0 || col >= AppsColumnTitles.Length) return;

            if (_sortColumnApps != col)
            {
                _sortColumnApps = col;
                _sortStateApps = SortState.Descending;
            }
            else
            {
                _sortStateApps = _sortStateApps switch
                {
                    SortState.Descending => SortState.Ascending,
                    SortState.Ascending => SortState.Default,
                    _ => SortState.Descending
                };

                if (_sortStateApps == SortState.Default)
                {
                    _sortColumnApps = -1;
                }
            }

            UpdateAppsColumnHeaderSortIndicators();
            LoadApps();
        }

        private void UpdateAppsColumnHeaderSortIndicators()
        {
            for (int i = 0; i < AppsColumnTitles.Length && i < _lvApps.Columns.Count; i++)
            {
                if (i == _sortColumnApps)
                {
                    if (_sortStateApps == SortState.Descending)
                    {
                        _lvApps.Columns[i].Text = $"{AppsColumnTitles[i]} ▼";
                    }
                    else if (_sortStateApps == SortState.Ascending)
                    {
                        _lvApps.Columns[i].Text = $"{AppsColumnTitles[i]} ▲";
                    }
                    else
                    {
                        _lvApps.Columns[i].Text = AppsColumnTitles[i];
                    }
                }
                else
                {
                    _lvApps.Columns[i].Text = AppsColumnTitles[i];
                }
            }
        }
        public static Color ParseColor(string? hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            try
            {
                string clean = hex.Trim();
                if (!clean.StartsWith("#") && (clean.Length == 6 || clean.Length == 8 || clean.Length == 3))
                {
                    clean = "#" + clean;
                }
                return ColorTranslator.FromHtml(clean);
            }
            catch
            {
                return fallback;
            }
        }
    }
}
