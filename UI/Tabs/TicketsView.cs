using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using KerkenezTicket.Models;
using KerkenezTicket.Services;

namespace KerkenezTicket.UI.Tabs
{
    public class TicketsView : UserControl
    {
        private readonly ConfigService _configService;
        private readonly TicketDatabaseService _dbService;

        // Top Action Bar Controls
        private Panel _topPanel = null!;
        private ComboBox _cboAppFilter = null!;
        private ComboBox _cboTypeFilter = null!;
        private ComboBox _cboStatusFilter = null!;
        private TextBox _txtSearch = null!;
        private Button _btnNewTicket = null!;
        private Button _btnRefresh = null!;

        // Filter State
        private TicketStatus? _selectedStatusFilter = null;
        private string _selectedAppFilter = "all";
        private string _selectedTypeFilter = "all";

        // Master-Detail Split
        private SplitContainer _split = null!;
        private ListView _lvTickets = null!;
        private ImageList _spacerImageList = null!;

        // Right Detail Panel
        private Panel _pnlDetailScroll = null!;
        private FlowLayoutPanel _detailFlow = null!;

        // Card 1: Header & Quick Actions
        private Panel _cardHeader = null!;
        private Label _lblDetailId = null!;
        private Label _lblStatusBadge = null!;
        private Label _lblPriorityBadge = null!;
        private Label _lblAppBadge = null!;
        private Label _lblTypeBadge = null!;
        private Label _lblDetailTitle = null!;
        private FlowLayoutPanel _pnlActionButtons = null!;
        private Button _btnActionDoing = null!;
        private Button _btnActionDone = null!;
        private Button _btnActionTodo = null!;
        private Button _btnActionBacklog = null!;
        private Button _btnActionKill = null!;
        private Button _btnActionEdit = null!;
        private Button _btnActionDelete = null!;
        private Label _lblMetaDates = null!;
        private Label _lblMetaTags = null!;

        // Card 2: Description
        private Panel _cardDescription = null!;
        private TextBox _txtDetailDescription = null!;

        // Card 3: Notes & Quick Note Logger
        private Panel _cardNotes = null!;
        private TextBox _txtDetailNotes = null!;
        private TextBox _txtNewNote = null!;
        private Button _btnAddNote = null!;

        private TicketItem? _currentTicket;
        private List<TicketItem> _loadedTickets = new List<TicketItem>();

        public event Action? CreateTicketRequested;
        public event Action<string, string>? StatusUpdated;

        public TicketsView(ConfigService configService, TicketDatabaseService dbService)
        {
            _configService = configService;
            _dbService = dbService;

            InitializeComponent();
            RefreshAll();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(248, 249, 250);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // ==================== 1. Top Action Toolbar ====================
            _topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.White,
                Padding = new Padding(16, 14, 16, 12)
            };
            _topPanel.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(222, 226, 230), 1);
                e.Graphics.DrawLine(p, 0, _topPanel.Height - 1, _topPanel.Width, _topPanel.Height - 1);
            };

            // Right Action Controls Flow (Docked Right, declared first so it reserves right boundary)
            var flowRight = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };

            _btnNewTicket = new Button
            {
                Text = "+ New Ticket",
                AutoSize = true,
                Height = 32,
                Padding = new Padding(14, 0, 14, 0),
                BackColor = Color.FromArgb(0, 102, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnNewTicket.FlatAppearance.BorderSize = 0;
            _btnNewTicket.Click += (s, e) => CreateTicketRequested?.Invoke();

            _btnRefresh = new Button
            {
                Text = "🔄 Refresh",
                AutoSize = true,
                Height = 32,
                Padding = new Padding(10, 0, 10, 0),
                BackColor = Color.FromArgb(245, 247, 250),
                ForeColor = Color.FromArgb(45, 55, 72),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 0)
            };
            _btnRefresh.FlatAppearance.BorderColor = Color.FromArgb(215, 222, 230);
            _btnRefresh.Click += (s, e) =>
            {
                LogService.Info("Tickets", "Refreshed tickets list from database.");
                RefreshAll();
            };

            flowRight.Controls.Add(_btnNewTicket);
            flowRight.Controls.Add(_btnRefresh);

            // Left Filter Controls Flow (Docked Fill to occupy remaining width and never clip into flowRight)
            var flowLeft = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false,
                Margin = new Padding(0)
            };

            // App Filter
            var lblApp = new Label { Text = "App:", AutoSize = true, Margin = new Padding(0, 6, 4, 0), Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(70, 75, 85) };
            _cboAppFilter = new ComboBox { Width = 135, Height = 28, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F), Margin = new Padding(0, 1, 14, 0) };
            _cboAppFilter.SelectedIndexChanged += (s, e) =>
            {
                _selectedAppFilter = _cboAppFilter.SelectedItem?.ToString() ?? "all";
                ApplyFilters();
            };
            flowLeft.Controls.Add(lblApp);
            flowLeft.Controls.Add(_cboAppFilter);

            // Type Filter
            var lblType = new Label { Text = "Type:", AutoSize = true, Margin = new Padding(0, 6, 4, 0), Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(70, 75, 85) };
            _cboTypeFilter = new ComboBox { Width = 105, Height = 28, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F), Margin = new Padding(0, 1, 14, 0) };
            _cboTypeFilter.SelectedIndexChanged += (s, e) =>
            {
                _selectedTypeFilter = _cboTypeFilter.SelectedItem?.ToString() ?? "all";
                ApplyFilters();
            };
            flowLeft.Controls.Add(lblType);
            flowLeft.Controls.Add(_cboTypeFilter);

            // Status Filter Dropdown
            var lblStatus = new Label { Text = "Status:", AutoSize = true, Margin = new Padding(0, 6, 4, 0), Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(70, 75, 85) };
            _cboStatusFilter = new ComboBox { Width = 138, Height = 28, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F), Margin = new Padding(0, 1, 14, 0) };
            _cboStatusFilter.Items.AddRange(new object[] { "All Statuses", "Backlog", "To Do", "Doing", "Done", "Killed" });
            _cboStatusFilter.SelectedIndex = 0;
            _cboStatusFilter.SelectedIndexChanged += (s, e) =>
            {
                _selectedStatusFilter = _cboStatusFilter.SelectedIndex switch
                {
                    1 => TicketStatus.Backlog,
                    2 => TicketStatus.Todo,
                    3 => TicketStatus.Doing,
                    4 => TicketStatus.Done,
                    5 => TicketStatus.Killed,
                    _ => null
                };
                LogService.Info("Tickets", $"Filtered status: {_cboStatusFilter.SelectedItem}");
                ApplyFilters();
            };
            flowLeft.Controls.Add(lblStatus);
            flowLeft.Controls.Add(_cboStatusFilter);

            // Search Box
            _txtSearch = new TextBox
            {
                Width = 170,
                Height = 28,
                Font = new Font("Segoe UI", 9F),
                PlaceholderText = "🔍 Search tickets...",
                Margin = new Padding(4, 1, 0, 0)
            };
            _txtSearch.TextChanged += (s, e) => ApplyFilters();
            flowLeft.Controls.Add(_txtSearch);

            // Add right panel first, then fill left panel
            _topPanel.Controls.Add(flowLeft);
            _topPanel.Controls.Add(flowRight);

            // ==================== 2. Master-Detail SplitContainer ====================
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
                        _configService.Settings.TicketDetailPanelWidth = rightW;
                        _configService.Settings.TicketsSplitterDistance = _split.SplitterDistance;
                        _configService.SaveConfig(_configService.Settings);
                        LogService.Info("UI", $"Right sidebar width saved: {rightW}px");
                    }
                }
            };

            // Master ListView (Panel1)
            var pnlListHolder = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(0)
            };

            // Spacer ImageList for comfortable touchable row height (28px)
            _spacerImageList = new ImageList { ImageSize = new Size(1, 26) };

            _lvTickets = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                GridLines = true,
                BorderStyle = BorderStyle.None,
                BackColor = Color.White,
                Font = new Font("Segoe UI", 9F),
                ShowGroups = true,
                SmallImageList = _spacerImageList
            };
            _lvTickets.Columns.Add("ID", 65);
            _lvTickets.Columns.Add("Priority", 75);
            _lvTickets.Columns.Add("App", 75);
            _lvTickets.Columns.Add("Type", 80);
            _lvTickets.Columns.Add("Title", 220);
            _lvTickets.Columns.Add("Updated", 75);
            _lvTickets.SelectedIndexChanged += OnTicketSelectionChanged;
            _lvTickets.ColumnWidthChanged += OnLvTicketsColumnWidthChanged;
            RestoreTicketColumnWidths();

            pnlListHolder.Controls.Add(_lvTickets);
            _split.Panel1.Controls.Add(pnlListHolder);

            // Detail Reader (Panel2)
            InitializeDetailPanel();
            _split.Panel2.Controls.Add(_pnlDetailScroll);

            this.Controls.Add(_split);
            this.Controls.Add(_topPanel);
        }


        private void InitializeDetailPanel()
        {
            _pnlDetailScroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(248, 249, 250),
                Padding = new Padding(16, 14, 16, 20)
            };

            _detailFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0)
            };

            // ==================== Card 1: Header & Quick Actions ====================
            _cardHeader = CreateCardContainer();

            // Top Badges Row
            var rowBadges = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 8)
            };

            _lblDetailId = CreatePillBadge("KT-0", Color.FromArgb(0, 102, 204), Color.FromArgb(235, 245, 255));
            _lblStatusBadge = CreatePillBadge("To Do", Color.FromArgb(70, 130, 180), Color.FromArgb(235, 243, 250));
            _lblPriorityBadge = CreatePillBadge("Medium", Color.FromArgb(0, 120, 215), Color.FromArgb(235, 243, 250));
            _lblAppBadge = CreatePillBadge("general", Color.FromArgb(100, 100, 100), Color.FromArgb(245, 245, 245));
            _lblTypeBadge = CreatePillBadge("Bug", Color.FromArgb(217, 83, 79), Color.FromArgb(254, 242, 242));

            rowBadges.Controls.Add(_lblDetailId);
            rowBadges.Controls.Add(_lblStatusBadge);
            rowBadges.Controls.Add(_lblPriorityBadge);
            rowBadges.Controls.Add(_lblAppBadge);
            rowBadges.Controls.Add(_lblTypeBadge);
            _cardHeader.Controls.Add(rowBadges);

            // Title Label
            _lblDetailTitle = new Label
            {
                Text = "Select a ticket from the list",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Color.FromArgb(17, 24, 39),
                AutoSize = true,
                MaximumSize = new Size(580, 0),
                Margin = new Padding(0, 0, 0, 12)
            };
            _cardHeader.Controls.Add(_lblDetailTitle);

            // Quick Workflow Action Buttons Strip
            _pnlActionButtons = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 12)
            };

            _btnActionDoing = CreateActionButton("▶  Start Doing", Color.FromArgb(220, 130, 20), (s, e) => ChangeStatus(TicketStatus.Doing));
            _btnActionDone = CreateActionButton("✓  Mark Done", Color.FromArgb(34, 134, 58), (s, e) => ChangeStatus(TicketStatus.Done));
            _btnActionTodo = CreateActionButton("↩  Move to Todo", Color.FromArgb(70, 130, 180), (s, e) => ChangeStatus(TicketStatus.Todo));
            _btnActionBacklog = CreateActionButton("📋  Move to Backlog", Color.FromArgb(114, 9, 183), (s, e) => ChangeStatus(TicketStatus.Backlog));
            _btnActionKill = CreateActionButton("✕  Kill Ticket", Color.FromArgb(108, 117, 125), (s, e) => ChangeStatus(TicketStatus.Killed));

            _btnActionEdit = CreateOutlineButton("✏️ Edit", (s, e) => OnEditClicked());
            _btnActionDelete = CreateOutlineButton("🗑 Delete", (s, e) => OnDeleteClicked());
            _btnActionDelete.ForeColor = Color.FromArgb(220, 53, 69);

            _pnlActionButtons.Controls.Add(_btnActionDoing);
            _pnlActionButtons.Controls.Add(_btnActionDone);
            _pnlActionButtons.Controls.Add(_btnActionTodo);
            _pnlActionButtons.Controls.Add(_btnActionBacklog);
            _pnlActionButtons.Controls.Add(_btnActionKill);
            _pnlActionButtons.Controls.Add(_btnActionEdit);
            _pnlActionButtons.Controls.Add(_btnActionDelete);
            _cardHeader.Controls.Add(_pnlActionButtons);

            // Metadata info
            _lblMetaDates = new Label
            {
                Text = "Created: -   •   Updated: -",
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(115, 125, 135),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            _lblMetaTags = new Label
            {
                Text = "Tags: (none)",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(75, 85, 99),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };

            _cardHeader.Controls.Add(_lblMetaDates);
            _cardHeader.Controls.Add(_lblMetaTags);
            _detailFlow.Controls.Add(_cardHeader);

            // ==================== Card 2: Description ====================
            _cardDescription = CreateCardContainer();
            _cardDescription.Controls.Add(CreateSectionHeader("📝  Description"));

            _txtDetailDescription = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.FromArgb(250, 251, 252),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5F),
                Height = 140,
                Width = 560,
                ScrollBars = ScrollBars.Vertical,
                Margin = new Padding(0)
            };
            _cardDescription.Controls.Add(_txtDetailDescription);
            _detailFlow.Controls.Add(_cardDescription);

            // ==================== Card 3: Notes & Activity ====================
            _cardNotes = CreateCardContainer();
            _cardNotes.Controls.Add(CreateSectionHeader("📋  Work Notes & History"));

            _txtDetailNotes = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.FromArgb(250, 251, 252),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9F),
                Height = 90,
                Width = 560,
                ScrollBars = ScrollBars.Vertical,
                Margin = new Padding(0, 0, 0, 10)
            };
            _cardNotes.Controls.Add(_txtDetailNotes);

            // Quick Note Adding Row
            var rowAddNote = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };

            _txtNewNote = new TextBox
            {
                Width = 460,
                Height = 28,
                Font = new Font("Segoe UI", 9F),
                PlaceholderText = "Add a quick update note to this ticket..."
            };
            _txtNewNote.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    OnAddNoteClicked();
                }
            };

            _btnAddNote = new Button
            {
                Text = "+ Log Note",
                Width = 90,
                Height = 28,
                BackColor = Color.FromArgb(0, 102, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(8, 0, 0, 0)
            };
            _btnAddNote.FlatAppearance.BorderSize = 0;
            _btnAddNote.Click += (s, e) => OnAddNoteClicked();

            rowAddNote.Controls.Add(_txtNewNote);
            rowAddNote.Controls.Add(_btnAddNote);
            _cardNotes.Controls.Add(rowAddNote);

            _detailFlow.Controls.Add(_cardNotes);

            _pnlDetailScroll.Controls.Add(_detailFlow);

            // Responsive Width Adjustment
            _pnlDetailScroll.Resize += (s, e) =>
            {
                int cardW = Math.Max(320, _pnlDetailScroll.Width - 36);
                _cardHeader.Width = cardW;
                _cardDescription.Width = cardW;
                _cardNotes.Width = cardW;
                _lblDetailTitle.MaximumSize = new Size(cardW - 32, 0);
                _txtDetailDescription.Width = cardW - 32;
                _txtDetailNotes.Width = cardW - 32;
                _txtNewNote.Width = Math.Max(150, cardW - 132);
            };
        }

        private static Panel CreateCardContainer()
        {
            var pnl = new FlowLayoutPanel
            {
                Width = 580,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.White,
                Padding = new Padding(16, 14, 16, 16),
                Margin = new Padding(0, 0, 0, 14)
            };

            pnl.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(226, 232, 240), 1);
                e.Graphics.DrawRectangle(p, 0, 0, pnl.Width - 1, pnl.Height - 1);
            };

            return pnl;
        }

        private static Label CreateSectionHeader(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(31, 41, 55),
                Margin = new Padding(0, 0, 0, 8)
            };
        }

        private static Label CreatePillBadge(string text, Color foreColor, Color backColor)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = foreColor,
                BackColor = backColor,
                Padding = new Padding(7, 3, 7, 3),
                Margin = new Padding(0, 0, 6, 0)
            };
        }

        private static Button CreateActionButton(string text, Color backColor, EventHandler onClick)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                Height = 30,
                Padding = new Padding(12, 0, 12, 0),
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 6, 4)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += onClick;
            return btn;
        }

        private static Button CreateOutlineButton(string text, EventHandler onClick)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                Height = 30,
                Padding = new Padding(10, 0, 10, 0),
                BackColor = Color.FromArgb(245, 247, 250),
                ForeColor = Color.FromArgb(55, 65, 81),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 6, 4)
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(215, 222, 230);
            btn.Click += onClick;
            return btn;
        }

        public void RefreshAll()
        {
            PopulateDropdowns();
            ApplyFilters();
        }

        private void PopulateDropdowns()
        {
            string currApp = _selectedAppFilter;
            _cboAppFilter.Items.Clear();
            _cboAppFilter.Items.Add("All Apps");

            var dbApps = _dbService.GetAllAppNames();
            _configService.SyncKnownApps(dbApps);
            var allApps = dbApps.Union(_configService.Settings.KnownApps, StringComparer.OrdinalIgnoreCase)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var app in allApps)
            {
                _cboAppFilter.Items.Add(app);
            }
            int appIdx = _cboAppFilter.Items.IndexOf(currApp);
            _cboAppFilter.SelectedIndex = appIdx >= 0 ? appIdx : 0;

            string currType = _selectedTypeFilter;
            _cboTypeFilter.Items.Clear();
            _cboTypeFilter.Items.Add("All Types");
            foreach (var t in _configService.Settings.KnownTypes)
            {
                _cboTypeFilter.Items.Add(t);
            }
            int typeIdx = _cboTypeFilter.Items.IndexOf(currType);
            _cboTypeFilter.SelectedIndex = typeIdx >= 0 ? typeIdx : 0;
        }

        public void ApplyFilters()
        {
            string? app = _selectedAppFilter.Equals("all apps", StringComparison.OrdinalIgnoreCase) || _selectedAppFilter.Equals("all", StringComparison.OrdinalIgnoreCase) ? null : _selectedAppFilter;
            string? type = _selectedTypeFilter.Equals("all types", StringComparison.OrdinalIgnoreCase) || _selectedTypeFilter.Equals("all", StringComparison.OrdinalIgnoreCase) ? null : _selectedTypeFilter;
            string? search = string.IsNullOrWhiteSpace(_txtSearch.Text) ? null : _txtSearch.Text;

            _loadedTickets = _dbService.GetFilteredTickets(app, type, _selectedStatusFilter, search);

            _lvTickets.BeginUpdate();
            _lvTickets.Items.Clear();
            _lvTickets.Groups.Clear();

            string grouping = _configService.Settings.ListViewGrouping;

            // Grouping Setup
            var groupMap = new Dictionary<string, ListViewGroup>();
            if (string.Equals(grouping, "Status", StringComparison.OrdinalIgnoreCase))
            {
                groupMap["doing"] = new ListViewGroup("doing", "🟡  In Progress / Doing");
                groupMap["todo"] = new ListViewGroup("todo", "🔵  To Do");
                groupMap["backlog"] = new ListViewGroup("backlog", "🟣  Backlog");
                groupMap["done"] = new ListViewGroup("done", "🟢  Completed / Done");
                groupMap["killed"] = new ListViewGroup("killed", "⚫  Killed / Cancelled");

                _lvTickets.Groups.Add(groupMap["doing"]);
                _lvTickets.Groups.Add(groupMap["todo"]);
                _lvTickets.Groups.Add(groupMap["backlog"]);
                _lvTickets.Groups.Add(groupMap["done"]);
                _lvTickets.Groups.Add(groupMap["killed"]);
            }
            else if (string.Equals(grouping, "App", StringComparison.OrdinalIgnoreCase))
            {
                var dbApps = _dbService.GetAllAppNames();
                var allApps = dbApps.Union(_configService.Settings.KnownApps, StringComparer.OrdinalIgnoreCase)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);

                foreach (var a in allApps)
                {
                    var grp = new ListViewGroup(a, $"📱  {a}");
                    groupMap[a.ToLowerInvariant()] = grp;
                    _lvTickets.Groups.Add(grp);
                }
            }

            foreach (var ticket in _loadedTickets)
            {
                var lvi = new ListViewItem(ticket.FormattedId);
                lvi.SubItems.Add(ticket.Priority.ToDisplayName());
                lvi.SubItems.Add(ticket.App);
                lvi.SubItems.Add(ticket.TicketType);
                lvi.SubItems.Add(ticket.Title);
                lvi.SubItems.Add(ticket.UpdatedAt.ToString("MM-dd HH:mm"));
                lvi.Tag = ticket;

                // Priority coloring
                switch (ticket.Priority)
                {
                    case TicketPriority.Urgent:
                        lvi.ForeColor = Color.FromArgb(220, 38, 38);
                        break;
                    case TicketPriority.High:
                        lvi.ForeColor = Color.FromArgb(217, 119, 6);
                        break;
                    case TicketPriority.Medium:
                        lvi.ForeColor = Color.FromArgb(37, 99, 235);
                        break;
                    case TicketPriority.Low:
                        lvi.ForeColor = Color.FromArgb(107, 114, 128);
                        break;
                }

                // Assign to group if enabled
                if (groupMap.Count > 0)
                {
                    string key = string.Equals(grouping, "Status", StringComparison.OrdinalIgnoreCase)
                        ? ticket.Status.ToKey()
                        : (ticket.App ?? "general").ToLowerInvariant();

                    if (groupMap.TryGetValue(key, out var grp))
                    {
                        lvi.Group = grp;
                    }
                    else if (string.Equals(grouping, "App", StringComparison.OrdinalIgnoreCase))
                    {
                        var newGrp = new ListViewGroup(key, $"📱  {key}");
                        groupMap[key] = newGrp;
                        _lvTickets.Groups.Add(newGrp);
                        lvi.Group = newGrp;
                    }
                }

                _lvTickets.Items.Add(lvi);
            }

            _lvTickets.EndUpdate();

            // Reselect ticket:
            // 1. If currently inspecting a ticket, reselect it
            // 2. Otherwise restore LastSelectedTicketId if stored in config
            // 3. Otherwise default to first available ticket
            string targetId = _currentTicket?.Id ?? _configService.Settings.LastSelectedTicketId;

            ListViewItem? match = null;
            if (!string.IsNullOrEmpty(targetId))
            {
                match = _lvTickets.Items.Cast<ListViewItem>().FirstOrDefault(i => (i.Tag as TicketItem)?.Id == targetId);
            }

            if (match != null)
            {
                match.Selected = true;
                match.EnsureVisible();
            }
            else if (_lvTickets.Items.Count > 0)
            {
                _lvTickets.Items[0].Selected = true;
            }
            else
            {
                ShowDetail(null);
            }

            // Update bottom status strip metrics
            var stats = _dbService.GetStatistics(app);
            string statusMsg = $"Showing {_loadedTickets.Count} tickets";
            string metrics = $"Total: {stats.Total} | Backlog: {stats.Backlog} | Todo: {stats.Todo} | Doing: {stats.Doing} | Done: {stats.Done} | Killed: {stats.Killed}";
            StatusUpdated?.Invoke(statusMsg, metrics);
        }

        private void OnTicketSelectionChanged(object? sender, EventArgs e)
        {
            if (_lvTickets.SelectedItems.Count > 0)
            {
                var ticket = _lvTickets.SelectedItems[0].Tag as TicketItem;
                ShowDetail(ticket);
                if (ticket != null)
                {
                    _configService.Settings.LastSelectedTicketId = ticket.Id;
                    _configService.SaveConfig(_configService.Settings);
                    LogService.Info("Tickets", $"Selected ticket {ticket.FormattedId}: \"{ticket.Title}\" ({ticket.App}/{ticket.TicketType})");
                }
            }
            else
            {
                ShowDetail(null);
            }
        }

        public void SelectTicketById(string ticketId)
        {
            ApplyFilters();
            foreach (ListViewItem item in _lvTickets.Items)
            {
                if (item.Tag is TicketItem t && string.Equals(t.Id, ticketId, StringComparison.OrdinalIgnoreCase))
                {
                    item.Selected = true;
                    item.EnsureVisible();
                    break;
                }
            }
        }

        private void ShowDetail(TicketItem? ticket)
        {
            _currentTicket = ticket;
            if (ticket == null)
            {
                _lblDetailId.Text = "No Ticket";
                _lblDetailTitle.Text = "Select a ticket from the list to view its full details";
                _lblStatusBadge.Visible = false;
                _lblPriorityBadge.Visible = false;
                _lblAppBadge.Visible = false;
                _lblTypeBadge.Visible = false;
                _pnlActionButtons.Visible = false;
                _lblMetaDates.Text = "";
                _lblMetaTags.Text = "";
                _txtDetailDescription.Text = "";
                _txtDetailNotes.Text = "";
                return;
            }

            _lblDetailId.Text = ticket.FormattedId;
            _lblDetailTitle.Text = ticket.Title;

            // Badges
            _lblStatusBadge.Visible = true;
            _lblStatusBadge.Text = ticket.Status.ToDisplayName();
            _lblStatusBadge.ForeColor = ticket.Status.GetBadgeColor();
            _lblStatusBadge.BackColor = ticket.Status.GetBadgeBgColor();

            _lblPriorityBadge.Visible = true;
            _lblPriorityBadge.Text = ticket.Priority.ToDisplayName();
            _lblPriorityBadge.ForeColor = ticket.Priority.GetBadgeColor();
            _lblPriorityBadge.BackColor = ticket.Priority.GetBadgeBgColor();

            _lblAppBadge.Visible = true;
            _lblAppBadge.Text = ticket.App;
            _lblAppBadge.ForeColor = Color.FromArgb(0, 102, 204);
            _lblAppBadge.BackColor = Color.FromArgb(235, 245, 255);

            _lblTypeBadge.Visible = true;
            _lblTypeBadge.Text = ticket.TicketType;
            _lblTypeBadge.ForeColor = TicketTypeHelper.GetTypeColor(ticket.TicketType);
            _lblTypeBadge.BackColor = Color.FromArgb(248, 250, 252);

            // Action Buttons
            _pnlActionButtons.Visible = true;
            _btnActionDoing.Visible = (ticket.Status != TicketStatus.Doing);
            _btnActionDone.Visible = (ticket.Status != TicketStatus.Done);
            _btnActionTodo.Visible = (ticket.Status != TicketStatus.Todo);
            _btnActionBacklog.Visible = (ticket.Status != TicketStatus.Backlog);
            _btnActionKill.Visible = (ticket.Status != TicketStatus.Killed);

            // Dates & Tags
            string dates = $"Created: {ticket.CreatedAt:yyyy-MM-dd HH:mm} UTC   •   Updated: {ticket.UpdatedAt:yyyy-MM-dd HH:mm} UTC";
            if (ticket.CompletedAt.HasValue) dates += $"   •   Completed: {ticket.CompletedAt.Value:yyyy-MM-dd HH:mm} UTC";
            if (ticket.KilledAt.HasValue) dates += $"   •   Killed: {ticket.KilledAt.Value:yyyy-MM-dd HH:mm} UTC";
            _lblMetaDates.Text = dates;

            _lblMetaTags.Text = ticket.Tags != null && ticket.Tags.Count > 0 ? $"Tags: {string.Join(", ", ticket.Tags)}" : "Tags: (none)";

            _txtDetailDescription.Text = string.IsNullOrWhiteSpace(ticket.Description) ? "(No detailed description provided)" : ticket.Description;
            _txtDetailNotes.Text = string.IsNullOrWhiteSpace(ticket.Notes) ? "(No work notes logged)" : ticket.Notes;
            _txtNewNote.Text = "";
        }

        private void ChangeStatus(TicketStatus newStatus)
        {
            if (_currentTicket == null) return;
            var oldStatus = _currentTicket.Status;
            _currentTicket.Status = newStatus;
            _dbService.UpdateTicket(_currentTicket);
            LogService.Success("Tickets", $"Updated status for {_currentTicket.FormattedId} from {oldStatus} to {newStatus}.");
            ApplyFilters();
            SelectTicketById(_currentTicket.Id);
        }

        private void OnAddNoteClicked()
        {
            if (_currentTicket == null) return;
            string note = _txtNewNote.Text.Trim();
            if (string.IsNullOrWhiteSpace(note)) return;

            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
            string entry = $"[{timestamp}]: {note}";

            if (string.IsNullOrWhiteSpace(_currentTicket.Notes))
            {
                _currentTicket.Notes = entry;
            }
            else
            {
                _currentTicket.Notes = entry + "\r\n" + _currentTicket.Notes;
            }

            _dbService.UpdateTicket(_currentTicket);
            LogService.Info("Tickets", $"Added work note to {_currentTicket.FormattedId}.");
            _txtNewNote.Text = "";
            ShowDetail(_currentTicket);
        }

        private void OnEditClicked()
        {
            if (_currentTicket == null) return;

            using var dlg = new TicketEditDialog(_currentTicket, _configService, _dbService);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _dbService.UpdateTicket(dlg.UpdatedTicket);
                _currentTicket = dlg.UpdatedTicket;
                LogService.Success("Tickets", $"Updated ticket {_currentTicket.FormattedId}: \"{_currentTicket.Title}\"");
                ApplyFilters();
                SelectTicketById(_currentTicket.Id);
            }
        }

        private void OnDeleteClicked()
        {
            if (_currentTicket == null) return;

            if (_configService.Settings.ConfirmBeforeDelete)
            {
                var confirm = MessageBox.Show(this, $"Are you sure you want to permanently delete ticket {_currentTicket.FormattedId}?\n\"{_currentTicket.Title}\"", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes) return;
            }

            string delId = _currentTicket.FormattedId;
            _dbService.DeleteTicket(_currentTicket.Id);
            LogService.Warn("Tickets", $"Permanently deleted ticket {delId}.");
            _currentTicket = null;
            ApplyFilters();
        }

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

                int savedW = _configService.Settings.TicketDetailPanelWidth;
                int rightW;

                if (savedW >= 150 && savedW <= totalW - 150)
                {
                    rightW = savedW;
                }
                else
                {
                    // Screen-proportional initial size: ~40% of available content width (clamped 380..520)
                    rightW = Math.Clamp((int)(totalW * 0.40), 380, Math.Min(520, totalW - 200));
                    if (rightW < 150) rightW = Math.Max(100, totalW / 2);
                    _configService.Settings.TicketDetailPanelWidth = rightW;
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
                _configService.Settings.TicketsSplitterDistance = _split.SplitterDistance;
                LogService.Info("UI", $"Right sidebar width restored: {rightW}px");
            }
            catch (Exception ex)
            {
                LogService.Warn("UI", $"Could not apply right sidebar width: {ex.Message}");
            }
        }

        public void SaveColumnWidths()
        {
            if (_lvTickets.Columns.Count >= 6)
            {
                _configService.Settings.TicketsColumnWidths["ID"] = _lvTickets.Columns[0].Width;
                _configService.Settings.TicketsColumnWidths["Priority"] = _lvTickets.Columns[1].Width;
                _configService.Settings.TicketsColumnWidths["App"] = _lvTickets.Columns[2].Width;
                _configService.Settings.TicketsColumnWidths["Type"] = _lvTickets.Columns[3].Width;
                _configService.Settings.TicketsColumnWidths["Title"] = _lvTickets.Columns[4].Width;
                _configService.Settings.TicketsColumnWidths["Updated"] = _lvTickets.Columns[5].Width;
            }

            int totalW = _split.ClientSize.Width > 0 ? _split.ClientSize.Width : _split.Width;
            if (totalW > 300)
            {
                int rightW = totalW - _split.SplitterDistance - _split.SplitterWidth;
                if (rightW >= 150 && rightW <= totalW - 150)
                {
                    _configService.Settings.TicketDetailPanelWidth = rightW;
                    _configService.Settings.TicketsSplitterDistance = _split.SplitterDistance;
                }
            }
        }

        private void OnLvTicketsColumnWidthChanged(object? sender, ColumnWidthChangedEventArgs e)
        {
            SaveColumnWidths();
            _configService.SaveConfig(_configService.Settings);
        }

        private void RestoreTicketColumnWidths()
        {
            var dict = _configService.Settings.TicketsColumnWidths;
            if (dict == null || dict.Count == 0) return;

            if (dict.TryGetValue("ID", out int wId) && wId > 30) _lvTickets.Columns[0].Width = wId;
            if (dict.TryGetValue("Priority", out int wPri) && wPri > 30) _lvTickets.Columns[1].Width = wPri;
            if (dict.TryGetValue("App", out int wApp) && wApp > 30) _lvTickets.Columns[2].Width = wApp;
            if (dict.TryGetValue("Type", out int wType) && wType > 30) _lvTickets.Columns[3].Width = wType;
            if (dict.TryGetValue("Title", out int wTitle) && wTitle > 30) _lvTickets.Columns[4].Width = wTitle;
            if (dict.TryGetValue("Updated", out int wUpd) && wUpd > 30) _lvTickets.Columns[5].Width = wUpd;
        }
    }
}
