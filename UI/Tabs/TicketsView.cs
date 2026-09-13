using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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

        // Column Sorting State
        private enum SortState
        {
            Default = 0,
            Descending = 1,
            Ascending = 2
        }

        private int _sortColumn = -1;
        private SortState _sortState = SortState.Default;

        private static readonly string[] TicketColumnTitles = new[]
        {
            "ID", "Priority", "App", "Type", "Title", "Updated"
        };

        // Master-Detail Split
        private SplitContainer _split = null!;
        private ListView _lvTickets = null!;
        private ImageList _spacerImageList = null!;

        // Right Detail Panel
        private Panel _pnlDetailScroll = null!;
        private FlowLayoutPanel _detailFlow = null!;

        // Right Batch Panel
        private Panel _pnlBatchScroll = null!;
        private FlowLayoutPanel _batchFlow = null!;
        private Panel _cardBatchHeader = null!;
        private Label _lblBatchTitle = null!;
        private FlowLayoutPanel _pnlBatchStatBadges = null!;
        private Button _btnBatchDeselectAll = null!;
        private Panel _cardBatchStatus = null!;
        private Panel _cardBatchPriority = null!;
        private Panel _cardBatchExport = null!;
        private ComboBox _cboBatchExportFormat = null!;
        private Label _lblBatchExportDest = null!;
        private Button _btnBatchChangeFolder = null!;
        private Button _btnBatchExport = null!;
        private string? _batchExportFolderOverride = null;
        private Panel _cardBatchPreview = null!;
        private Panel _pnlBatchPreviewContainer = null!;
        private FlowLayoutPanel _pnlBatchPreviewList = null!;
        private Panel _cardBatchDanger = null!;
        private Button _btnBatchDelete = null!;
        private List<TicketItem> _currentBatchTickets = new List<TicketItem>();
        private readonly System.Windows.Forms.Timer _selectionDebounceTimer;

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
        private Button _btnActionExport = null!;
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

        // Card 4: Attachments
        private Panel _cardAttachments = null!;
        private FlowLayoutPanel _rowAttHeader = null!;
        private Label _lblDetailAttachmentsHeader = null!;
        private FlowLayoutPanel _pnlDetailAttachmentsList = null!;
        private Button _btnDetailAddAtt = null!;
        private Button _btnDetailPasteScreenshot = null!;

        private TicketItem? _currentTicket;
        private List<TicketItem> _loadedTickets = new List<TicketItem>();
        private Dictionary<string, string> _appDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, Color> _appColorCache = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);

        public event Action? CreateTicketRequested;
        public event Action<string, string>? StatusUpdated;

        public TicketsView(ConfigService configService, TicketDatabaseService dbService)
        {
            _configService = configService;
            _dbService = dbService;

            _selectionDebounceTimer = new System.Windows.Forms.Timer { Interval = 20 };
            _selectionDebounceTimer.Tick += (s, e) =>
            {
                _selectionDebounceTimer.Stop();
                ApplySelectionChange();
            };

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
                UpdateDetailLayout();
            };
            _split.Resize += (s, e) => UpdateDetailLayout();

            // Master ListView (Panel1)
            var pnlListHolder = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(0)
            };

            // Spacer ImageList for comfortable touchable row height (28px) and app color vertical strips
            _spacerImageList = new ImageList
            {
                ImageSize = new Size(6, 22),
                ColorDepth = ColorDepth.Depth32Bit
            };

            _lvTickets = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = true,
                HideSelection = false,
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
            _lvTickets.ColumnClick += OnTicketColumnClick;
            _lvTickets.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.A)
                {
                    e.SuppressKeyPress = true;
                    _lvTickets.BeginUpdate();
                    foreach (ListViewItem item in _lvTickets.Items)
                    {
                        item.Selected = true;
                    }
                    _lvTickets.EndUpdate();
                }
            };
            EnableDoubleBuffer(_lvTickets);
            RestoreTicketColumnWidths();

            pnlListHolder.Controls.Add(_lvTickets);
            _split.Panel1.Controls.Add(pnlListHolder);

            // Detail & Batch Panels (Panel2)
            InitializeDetailPanel();
            InitializeBatchPanel();
            EnableDoubleBuffer(_pnlDetailScroll);
            EnableDoubleBuffer(_pnlBatchScroll);
            _split.Panel2.Controls.Add(_pnlDetailScroll);
            _split.Panel2.Controls.Add(_pnlBatchScroll);

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

            _btnActionExport = CreateOutlineButton("📤 Export...", (s, e) => OnExportSingleTicketClicked());
            _btnActionEdit = CreateOutlineButton("✏️ Edit", (s, e) => OnEditClicked());
            _btnActionDelete = CreateOutlineButton("🗑 Delete", (s, e) => OnDeleteClicked());
            _btnActionDelete.ForeColor = Color.FromArgb(220, 53, 69);

            _pnlActionButtons.Controls.Add(_btnActionDoing);
            _pnlActionButtons.Controls.Add(_btnActionDone);
            _pnlActionButtons.Controls.Add(_btnActionTodo);
            _pnlActionButtons.Controls.Add(_btnActionBacklog);
            _pnlActionButtons.Controls.Add(_btnActionKill);
            _pnlActionButtons.Controls.Add(_btnActionExport);
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
                Font = new Font("Segoe UI", 9F),
                PlaceholderText = "Add a quick update note to this ticket...",
                Margin = new Padding(0)
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
                Text = "+ Log",
                Width = 92,
                Height = _txtNewNote.PreferredHeight > 0 ? _txtNewNote.PreferredHeight : 30,
                BackColor = Color.FromArgb(0, 102, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(8, 0, 0, 0)
            };
            _btnAddNote.FlatAppearance.BorderSize = 0;
            _btnAddNote.Click += (s, e) => OnAddNoteClicked();

            rowAddNote.Controls.Add(_txtNewNote);
            rowAddNote.Controls.Add(_btnAddNote);
            _cardNotes.Controls.Add(rowAddNote);

            _detailFlow.Controls.Add(_cardNotes);

            // ==================== Card 4: Attachments ====================
            _cardAttachments = CreateCardContainer();

            _rowAttHeader = new FlowLayoutPanel
            {
                Width = 560,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 10)
            };

            _lblDetailAttachmentsHeader = CreateSectionHeader("📎  Attachments");
            _lblDetailAttachmentsHeader.Margin = new Padding(0, 4, 12, 0);

            _btnDetailAddAtt = CreateOutlineButton("📎 Add File...", (s, e) => OnDetailAddAttachmentClicked());
            _btnDetailAddAtt.Margin = new Padding(0, 0, 4, 0);

            _btnDetailPasteScreenshot = CreateOutlineButton("📋", (s, e) => OnDetailPasteScreenshotClicked());
            _btnDetailPasteScreenshot.Size = new Size(32, 30);
            _btnDetailPasteScreenshot.Padding = new Padding(0);
            _btnDetailPasteScreenshot.Font = new Font("Segoe UI", 9.5F);
            _btnDetailPasteScreenshot.Margin = new Padding(0);

            var tipAttHeader = new ToolTip();
            tipAttHeader.SetToolTip(_btnDetailAddAtt, "Select file from disk to attach to this ticket");
            tipAttHeader.SetToolTip(_btnDetailPasteScreenshot, "Paste screenshot from clipboard (Ctrl+V)");

            _rowAttHeader.Controls.Add(_lblDetailAttachmentsHeader);
            _rowAttHeader.Controls.Add(_btnDetailAddAtt);
            _rowAttHeader.Controls.Add(_btnDetailPasteScreenshot);
            _cardAttachments.Controls.Add(_rowAttHeader);

            _pnlDetailAttachmentsList = new FlowLayoutPanel
            {
                Width = 560,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0)
            };

            _cardAttachments.AllowDrop = true;
            _cardAttachments.DragEnter += OnDetailDragEnter;
            _cardAttachments.DragDrop += OnDetailDragDrop;

            _cardAttachments.Controls.Add(_pnlDetailAttachmentsList);
            _detailFlow.Controls.Add(_cardAttachments);

            _pnlDetailScroll.Controls.Add(_detailFlow);

            // Responsive Width Adjustment
            _pnlDetailScroll.Resize += (s, e) => UpdateDetailLayout();
        }

        private void UpdateDetailLayout()
        {
            if (_pnlDetailScroll == null || _cardHeader == null) return;

            int scrollW = _pnlDetailScroll.ClientSize.Width > 0 ? _pnlDetailScroll.ClientSize.Width : _pnlDetailScroll.Width;
            if (_split?.Panel2 != null && _split.Panel2.ClientSize.Width > 0)
            {
                int p2W = _split.Panel2.ClientSize.Width;
                if (p2W > scrollW) scrollW = p2W;
            }
            int cardW = Math.Max(320, scrollW - 36);
            int innerW = Math.Max(240, cardW - 32);

            _cardHeader.Width = cardW;
            _cardDescription.Width = cardW;
            _cardNotes.Width = cardW;
            _cardAttachments.Width = cardW;

            _cardHeader.MinimumSize = new Size(cardW, 0);
            _cardDescription.MinimumSize = new Size(cardW, 0);
            _cardNotes.MinimumSize = new Size(cardW, 0);
            _cardAttachments.MinimumSize = new Size(cardW, 0);

            _lblDetailTitle.MaximumSize = new Size(innerW, 0);
            _txtDetailDescription.Width = innerW;
            _txtDetailNotes.Width = innerW;
            _pnlDetailAttachmentsList.Width = innerW;
            _pnlDetailAttachmentsList.MinimumSize = new Size(innerW, 0);
            _txtNewNote.Width = Math.Max(150, cardW - 132);
            if (_txtNewNote.PreferredHeight > 0)
            {
                _btnAddNote.Height = _txtNewNote.PreferredHeight;
            }

            if (_rowAttHeader != null)
            {
                _rowAttHeader.Width = innerW;
            }

            foreach (Control c in _pnlDetailAttachmentsList.Controls)
            {
                c.Width = innerW;
                c.MinimumSize = new Size(innerW, c.Height);
            }
        }

        private void InitializeBatchPanel()
        {
            _pnlBatchScroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(248, 249, 250),
                Padding = new Padding(16, 14, 16, 20),
                Visible = false
            };

            _batchFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0)
            };

            // ==================== Card 1: Batch Header ====================
            _cardBatchHeader = CreateCardContainer();

            var rowBadges = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 8)
            };
            var badgeBatch = CreatePillBadge("⚡ BATCH SELECTION", Color.FromArgb(88, 28, 135), Color.FromArgb(243, 232, 255));
            rowBadges.Controls.Add(badgeBatch);

            _btnBatchDeselectAll = CreateOutlineButton("✕ Clear Selection", (s, e) =>
            {
                _lvTickets.SelectedIndices.Clear();
            });
            _btnBatchDeselectAll.Margin = new Padding(8, 0, 0, 0);
            rowBadges.Controls.Add(_btnBatchDeselectAll);

            _cardBatchHeader.Controls.Add(rowBadges);

            _lblBatchTitle = new Label
            {
                Text = "0 Tickets Selected",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Color.FromArgb(17, 24, 39),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 6)
            };
            _cardBatchHeader.Controls.Add(_lblBatchTitle);

            var lblSubtitle = new Label
            {
                Text = "Choose a batch action below to update, export, or mass delete all selected tickets at once.",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10)
            };
            _cardBatchHeader.Controls.Add(lblSubtitle);

            _pnlBatchStatBadges = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            _cardBatchHeader.Controls.Add(_pnlBatchStatBadges);

            _batchFlow.Controls.Add(_cardBatchHeader);

            // ==================== Card 2: Batch Status Changes ====================
            _cardBatchStatus = CreateCardContainer();
            _cardBatchStatus.Controls.Add(CreateSectionHeader("🔄  Batch Status Change"));

            var lblStatusDesc = new Label
            {
                Text = "Update the workflow state of all selected tickets simultaneously:",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8)
            };
            _cardBatchStatus.Controls.Add(lblStatusDesc);

            var rowStatusButtons = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0)
            };

            rowStatusButtons.Controls.Add(CreateActionButton("▶  Doing", Color.FromArgb(220, 130, 20), (s, e) => ExecuteBatchStatusChange(TicketStatus.Doing)));
            rowStatusButtons.Controls.Add(CreateActionButton("✓  Done", Color.FromArgb(34, 134, 58), (s, e) => ExecuteBatchStatusChange(TicketStatus.Done)));
            rowStatusButtons.Controls.Add(CreateActionButton("↩  To Do", Color.FromArgb(70, 130, 180), (s, e) => ExecuteBatchStatusChange(TicketStatus.Todo)));
            rowStatusButtons.Controls.Add(CreateActionButton("📋  Backlog", Color.FromArgb(114, 9, 183), (s, e) => ExecuteBatchStatusChange(TicketStatus.Backlog)));
            rowStatusButtons.Controls.Add(CreateActionButton("✕  Killed", Color.FromArgb(108, 117, 125), (s, e) => ExecuteBatchStatusChange(TicketStatus.Killed)));

            _cardBatchStatus.Controls.Add(rowStatusButtons);
            _batchFlow.Controls.Add(_cardBatchStatus);

            // ==================== Card 3: Batch Priority Changes ====================
            _cardBatchPriority = CreateCardContainer();
            _cardBatchPriority.Controls.Add(CreateSectionHeader("⚡  Batch Priority Change"));

            var lblPriorityDesc = new Label
            {
                Text = "Change the urgency and priority level of all selected tickets:",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8)
            };
            _cardBatchPriority.Controls.Add(lblPriorityDesc);

            var rowPriorityButtons = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0)
            };

            rowPriorityButtons.Controls.Add(CreateActionButton("🔴 Urgent", Color.FromArgb(220, 53, 69), (s, e) => ExecuteBatchPriorityChange(TicketPriority.Urgent)));
            rowPriorityButtons.Controls.Add(CreateActionButton("🟠 High", Color.FromArgb(230, 126, 34), (s, e) => ExecuteBatchPriorityChange(TicketPriority.High)));
            rowPriorityButtons.Controls.Add(CreateActionButton("🔵 Medium", Color.FromArgb(0, 120, 215), (s, e) => ExecuteBatchPriorityChange(TicketPriority.Medium)));
            rowPriorityButtons.Controls.Add(CreateActionButton("⚪ Low", Color.FromArgb(108, 117, 125), (s, e) => ExecuteBatchPriorityChange(TicketPriority.Low)));

            _cardBatchPriority.Controls.Add(rowPriorityButtons);
            _batchFlow.Controls.Add(_cardBatchPriority);

            // ==================== Card 4: Batch Export ====================
            _cardBatchExport = CreateCardContainer();
            _cardBatchExport.Controls.Add(CreateSectionHeader("📤  Batch Export"));

            var lblExportDesc = new Label
            {
                Text = "Export all selected tickets with full markdown and attachments into a folder or zip:",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8)
            };
            _cardBatchExport.Controls.Add(lblExportDesc);

            var rowFormat = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 8)
            };
            var lblFormat = new Label
            {
                Text = "Format:",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(55, 65, 81),
                Margin = new Padding(0, 4, 8, 0)
            };
            _cboBatchExportFormat = new ComboBox
            {
                Width = 280,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0)
            };
            _cboBatchExportFormat.Items.AddRange(new object[]
            {
                "📦 Compressed ZIP Archive (.zip)",
                "📁 Ticket Folders (each ticket in subfolder)",
                "📝 Markdown Files Folder (.md + attachments)"
            });
            string defFormat = _configService.Settings.DefaultExportFormat ?? "Zip";
            _cboBatchExportFormat.SelectedIndex = defFormat.Equals("Folder", StringComparison.OrdinalIgnoreCase) ? 1
                : (defFormat.Equals("Markdown", StringComparison.OrdinalIgnoreCase) || defFormat.Equals("md", StringComparison.OrdinalIgnoreCase)) ? 2 : 0;

            rowFormat.Controls.Add(lblFormat);
            rowFormat.Controls.Add(_cboBatchExportFormat);
            _cardBatchExport.Controls.Add(rowFormat);

            var rowDest = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 10)
            };
            _lblBatchExportDest = new Label
            {
                Text = $"📁 Destination: {_configService.GetExportDirectory()}",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(75, 85, 99),
                AutoSize = true,
                AutoEllipsis = true,
                MaximumSize = new Size(380, 0),
                Margin = new Padding(0, 4, 8, 0)
            };
            _btnBatchChangeFolder = CreateOutlineButton("Change...", (s, e) =>
            {
                using var fbd = new FolderBrowserDialog();
                fbd.Description = "Select Destination Folder for Batch Export";
                fbd.SelectedPath = _batchExportFolderOverride ?? _configService.GetExportDirectory();
                if (fbd.ShowDialog(this) == DialogResult.OK && Directory.Exists(fbd.SelectedPath))
                {
                    _batchExportFolderOverride = fbd.SelectedPath;
                    _lblBatchExportDest.Text = $"📁 Destination: {_batchExportFolderOverride}";
                }
            });
            rowDest.Controls.Add(_lblBatchExportDest);
            rowDest.Controls.Add(_btnBatchChangeFolder);
            _cardBatchExport.Controls.Add(rowDest);

            _btnBatchExport = new Button
            {
                Text = "📤 Export Selected Tickets",
                AutoSize = true,
                Height = 34,
                Padding = new Padding(14, 0, 14, 0),
                BackColor = Color.FromArgb(0, 102, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0)
            };
            _btnBatchExport.FlatAppearance.BorderSize = 0;
            _btnBatchExport.Click += (s, e) => ExecuteBatchExport();
            _cardBatchExport.Controls.Add(_btnBatchExport);

            _batchFlow.Controls.Add(_cardBatchExport);

            // ==================== Card 5: Selected Tickets Preview List ====================
            _cardBatchPreview = CreateCardContainer();
            _cardBatchPreview.Controls.Add(CreateSectionHeader("📋  Selected Tickets Preview"));

            _pnlBatchPreviewContainer = new Panel
            {
                Width = 560,
                Height = 120,
                AutoScroll = true,
                BorderStyle = BorderStyle.None,
                Margin = new Padding(0)
            };

            _pnlBatchPreviewList = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0)
            };
            _pnlBatchPreviewContainer.Controls.Add(_pnlBatchPreviewList);
            _cardBatchPreview.Controls.Add(_pnlBatchPreviewContainer);
            _batchFlow.Controls.Add(_cardBatchPreview);

            // ==================== Card 6: Danger Zone / Mass Deletion ====================
            _cardBatchDanger = CreateCardContainer();
            _cardBatchDanger.Controls.Add(CreateSectionHeader("⚠️  Danger Zone"));

            var lblDangerDesc = new Label
            {
                Text = "Permanently delete all selected tickets, work notes, and stored attachment files. This cannot be undone.",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(185, 28, 28),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10)
            };
            _cardBatchDanger.Controls.Add(lblDangerDesc);

            _btnBatchDelete = new Button
            {
                Text = "🗑 Delete Selected Tickets Permanently",
                AutoSize = true,
                Height = 34,
                Padding = new Padding(14, 0, 14, 0),
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0)
            };
            _btnBatchDelete.FlatAppearance.BorderSize = 0;
            _btnBatchDelete.Click += (s, e) => ExecuteBatchDelete();
            _cardBatchDanger.Controls.Add(_btnBatchDelete);

            _batchFlow.Controls.Add(_cardBatchDanger);

            _pnlBatchScroll.Controls.Add(_batchFlow);

            // Responsive Width Adjustment
            _pnlBatchScroll.Resize += (s, e) =>
            {
                int cardW = Math.Max(320, _pnlBatchScroll.Width - 36);
                _cardBatchHeader.Width = cardW;
                _cardBatchStatus.Width = cardW;
                _cardBatchPriority.Width = cardW;
                _cardBatchExport.Width = cardW;
                _cardBatchPreview.Width = cardW;
                _cardBatchDanger.Width = cardW;

                _pnlBatchPreviewContainer.Width = cardW - 32;
                _pnlBatchPreviewList.Width = cardW - 32;
                foreach (Control c in _pnlBatchPreviewList.Controls)
                {
                    c.Width = cardW - 32;
                    c.Invalidate();
                }
            };
        }

        private Control CreateBatchPreviewRow(TicketItem ticket, int width)
        {
            var pnl = new Panel
            {
                Width = width,
                Height = 34,
                BackColor = Color.FromArgb(250, 252, 255),
                Margin = new Padding(0, 0, 0, 4)
            };
            EnableDoubleBuffer(pnl);

            Color appColor = GetAppColor(ticket.App);
            string appDisp = GetAppDisplayName(ticket.App);
            string priDisp = ticket.Priority.ToDisplayName();
            Color priColor = ticket.Priority.GetBadgeColor();
            Color priBg = ticket.Priority.GetBadgeBgColor();
            Color appReadable = EnsureReadableColor(appColor);
            Color appBg = Color.FromArgb(240, 243, 248);

            var btnRemove = new Button
            {
                Text = "✕",
                Width = 24,
                Height = 24,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(140, 150, 160),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                Margin = new Padding(0, 5, 4, 0)
            };
            btnRemove.FlatAppearance.BorderSize = 0;
            btnRemove.Click += (s, e) =>
            {
                var lvi = _lvTickets.Items.Cast<ListViewItem>().FirstOrDefault(i => (i.Tag as TicketItem)?.Id == ticket.Id);
                if (lvi != null)
                {
                    lvi.Selected = false;
                }
            };
            pnl.Controls.Add(btnRemove);

            pnl.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // Border
                using var pBorder = new Pen(Color.FromArgb(226, 232, 240), 1);
                g.DrawRectangle(pBorder, 0, 0, pnl.Width - 1, pnl.Height - 1);

                // Left app color strip
                using var bStrip = new SolidBrush(appColor);
                g.FillRectangle(bStrip, 0, 0, 4, pnl.Height);

                int curX = 10;
                int y = 6;
                int h = 20;

                // 1. Draw ID Badge
                curX = DrawPreviewPill(g, ticket.FormattedId, curX, y, h, Color.FromArgb(0, 102, 204), Color.FromArgb(235, 245, 255), true);

                // 2. Draw Priority Badge
                curX = DrawPreviewPill(g, priDisp, curX + 4, y, h, priColor, priBg, true);

                // 3. Draw App Badge
                curX = DrawPreviewPill(g, appDisp, curX + 4, y, h, appReadable, appBg, false);

                // 4. Draw Title
                int availW = Math.Max(20, pnl.Width - btnRemove.Width - curX - 10);
                var titleRect = new Rectangle(curX + 6, y, availW, h);
                using var titleFont = new Font("Segoe UI", 9F);
                TextRenderer.DrawText(g, ticket.Title, titleFont, titleRect, Color.FromArgb(31, 41, 55),
                    TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            };

            return pnl;
        }

        private static int DrawPreviewPill(Graphics g, string text, int x, int y, int h, Color foreColor, Color backColor, bool bold)
        {
            using var font = new Font("Segoe UI", 8F, bold ? FontStyle.Bold : FontStyle.Regular);
            var size = TextRenderer.MeasureText(text, font);
            int pillW = size.Width + 10;
            var rect = new Rectangle(x, y, pillW, h);

            using var brush = new SolidBrush(backColor);
            g.FillRectangle(brush, rect);

            TextRenderer.DrawText(g, text, font, rect, foreColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            return x + pillW;
        }

        private void ShowBatchActions()
        {
            _currentBatchTickets = _lvTickets.SelectedItems
                .Cast<ListViewItem>()
                .Select(i => i.Tag as TicketItem)
                .Where(t => t != null)
                .Cast<TicketItem>()
                .ToList();

            if (_currentBatchTickets.Count <= 1)
            {
                ApplySelectionChange();
                return;
            }

            _lblBatchTitle.Text = $"📋 {_currentBatchTickets.Count} Tickets Selected";

            // Status Breakdown & App Breakdown Badges
            _pnlBatchStatBadges.SuspendLayout();
            _pnlBatchStatBadges.Controls.Clear();
            var statusCounts = _currentBatchTickets.GroupBy(t => t.Status);
            foreach (var grp in statusCounts)
            {
                var badge = CreatePillBadge($"{grp.Count()} {grp.Key.ToDisplayName()}", grp.Key.GetBadgeColor(), grp.Key.GetBadgeBgColor());
                _pnlBatchStatBadges.Controls.Add(badge);
            }

            var appCounts = _currentBatchTickets.GroupBy(t => t.App ?? "general");
            if (appCounts.Count() > 1 || statusCounts.Count() <= 2)
            {
                foreach (var grp in appCounts.Take(4))
                {
                    string disp = GetAppDisplayName(grp.Key);
                    Color col = GetAppColor(grp.Key);
                    var badge = CreatePillBadge($"{grp.Count()} {disp}", EnsureReadableColor(col), Color.FromArgb(240, 243, 248));
                    _pnlBatchStatBadges.Controls.Add(badge);
                }
            }
            _pnlBatchStatBadges.ResumeLayout(true);

            _btnBatchExport.Text = $"📤 Export {_currentBatchTickets.Count} Selected Tickets";
            _btnBatchDelete.Text = $"🗑 Delete {_currentBatchTickets.Count} Tickets Permanently";

            string exportDir = _batchExportFolderOverride ?? _configService.GetExportDirectory();
            _lblBatchExportDest.Text = $"📁 Destination: {exportDir}";

            // Rebuild preview rows (fast, capped at max 8 items)
            int cardW = Math.Max(320, _pnlBatchScroll.Width - 36);
            int itemW = Math.Max(240, cardW - 32);

            _pnlBatchPreviewList.SuspendLayout();
            _pnlBatchPreviewList.Controls.Clear();

            int displayLimit = 8;
            var displayTickets = _currentBatchTickets.Take(displayLimit).ToList();

            foreach (var ticket in displayTickets)
            {
                _pnlBatchPreviewList.Controls.Add(CreateBatchPreviewRow(ticket, itemW));
            }

            if (_currentBatchTickets.Count > displayLimit)
            {
                int remaining = _currentBatchTickets.Count - displayLimit;
                var lblMore = new Label
                {
                    Text = $"+ {remaining} more tickets selected",
                    Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                    ForeColor = Color.FromArgb(100, 116, 139),
                    AutoSize = true,
                    Margin = new Padding(4, 4, 0, 2)
                };
                _pnlBatchPreviewList.Controls.Add(lblMore);
            }

            _pnlBatchPreviewList.ResumeLayout(true);

            int calculatedH = Math.Clamp(displayTickets.Count * 38 + (_currentBatchTickets.Count > displayLimit ? 34 : 10), 72, 340);
            _pnlBatchPreviewContainer.Height = calculatedH;
        }

        private void ExecuteBatchStatusChange(TicketStatus newStatus)
        {
            if (_currentBatchTickets.Count == 0) return;

            var ids = _currentBatchTickets.Select(t => t.Id).ToList();
            int count = _dbService.UpdateTicketsStatus(ids, newStatus);
            LogService.Success("Tickets", $"Batch updated {count} tickets to {newStatus.ToDisplayName()}.");

            ApplyFilters(ids);
        }

        private void ExecuteBatchPriorityChange(TicketPriority newPriority)
        {
            if (_currentBatchTickets.Count == 0) return;

            var ids = _currentBatchTickets.Select(t => t.Id).ToList();
            int count = _dbService.UpdateTicketsPriority(ids, newPriority);
            LogService.Success("Tickets", $"Batch updated {count} tickets to priority {newPriority.ToDisplayName()}.");

            ApplyFilters(ids);
        }

        private void ExecuteBatchExport()
        {
            if (_currentBatchTickets.Count == 0) return;

            ExportFormat format = _cboBatchExportFormat.SelectedIndex switch
            {
                0 => ExportFormat.Zip,
                1 => ExportFormat.Folder,
                2 => ExportFormat.Markdown,
                _ => ExportFormat.Zip
            };

            // Ensure attachments populated
            var attMap = _dbService.GetAllAttachmentsGrouped();
            foreach (var t in _currentBatchTickets)
            {
                if (attMap.TryGetValue(t.Id, out var atts))
                {
                    t.Attachments = atts;
                }
            }

            string exportDir = _batchExportFolderOverride ?? _configService.GetExportDirectory();
            string batchName = $"Tickets_Batch_{DateTime.Now:yyyyMMdd_HHmmss}";

            try
            {
                string resultPath = TicketExportService.ExportBatchTickets(_currentBatchTickets, exportDir, format, batchName);
                LogService.Success("Export", $"Batch exported {_currentBatchTickets.Count} tickets to: {resultPath}");

                var res = MessageBox.Show(this,
                    $"Successfully exported {_currentBatchTickets.Count} tickets to:\n\n{resultPath}\n\nWould you like to open or reveal the exported item in Windows Explorer?",
                    "Batch Export Successful",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (res == DialogResult.Yes)
                {
                    TicketExportService.RevealInExplorer(resultPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Batch export failed: {ex.Message}", "Batch Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                LogService.Error("Export", $"Failed to batch export tickets: {ex.Message}");
            }
        }

        private void ExecuteBatchDelete()
        {
            if (_currentBatchTickets.Count == 0) return;

            var confirm = MessageBox.Show(this,
                $"Are you sure you want to permanently delete {_currentBatchTickets.Count} selected tickets and all their associated attachments?\n\nThis action cannot be undone.",
                "Confirm Mass Deletion",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            var ids = _currentBatchTickets.Select(t => t.Id).ToList();
            int count = _dbService.DeleteTickets(ids);
            LogService.Warn("Tickets", $"Permanently mass deleted {count} tickets.");

            _currentTicket = null;
            ApplyFilters();
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

        public void ApplyFilters(IEnumerable<string>? preserveSelectionIds = null)
        {
            string? app = _selectedAppFilter.Equals("all apps", StringComparison.OrdinalIgnoreCase) || _selectedAppFilter.Equals("all", StringComparison.OrdinalIgnoreCase) ? null : _selectedAppFilter;
            string? type = _selectedTypeFilter.Equals("all types", StringComparison.OrdinalIgnoreCase) || _selectedTypeFilter.Equals("all", StringComparison.OrdinalIgnoreCase) ? null : _selectedTypeFilter;
            string? search = string.IsNullOrWhiteSpace(_txtSearch.Text) ? null : _txtSearch.Text;

            _appDisplayNames = _dbService.GetAppDisplayNameMap();
            var colorMap = _dbService.GetAppColorMap();
            _appColorCache.Clear();
            foreach (var kvp in colorMap)
            {
                _appColorCache[kvp.Key] = ParseColor(kvp.Value, Color.FromArgb(0, 120, 215));
            }
            EnsureAppStripImages();

            _loadedTickets = _dbService.GetFilteredTickets(app, type, _selectedStatusFilter, search);

            if (_sortColumn >= 0 && _sortState != SortState.Default)
            {
                bool desc = (_sortState == SortState.Descending);
                _loadedTickets = _sortColumn switch
                {
                    0 => desc // ID
                        ? _loadedTickets.OrderByDescending(t => t.TicketNumber).ThenByDescending(t => t.Id).ToList()
                        : _loadedTickets.OrderBy(t => t.TicketNumber).ThenBy(t => t.Id).ToList(),

                    1 => desc // Priority (Urgent > High > Medium > Low)
                        ? _loadedTickets.OrderByDescending(t => (int)t.Priority).ThenByDescending(t => t.TicketNumber).ToList()
                        : _loadedTickets.OrderBy(t => (int)t.Priority).ThenBy(t => t.TicketNumber).ToList(),

                    2 => desc // App
                        ? _loadedTickets.OrderByDescending(t => GetAppDisplayName(t.App), StringComparer.OrdinalIgnoreCase).ThenByDescending(t => t.TicketNumber).ToList()
                        : _loadedTickets.OrderBy(t => GetAppDisplayName(t.App), StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TicketNumber).ToList(),

                    3 => desc // Type
                        ? _loadedTickets.OrderByDescending(t => t.TicketType, StringComparer.OrdinalIgnoreCase).ThenByDescending(t => t.TicketNumber).ToList()
                        : _loadedTickets.OrderBy(t => t.TicketType, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TicketNumber).ToList(),

                    4 => desc // Title
                        ? _loadedTickets.OrderByDescending(t => t.Title, StringComparer.OrdinalIgnoreCase).ThenByDescending(t => t.TicketNumber).ToList()
                        : _loadedTickets.OrderBy(t => t.Title, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.TicketNumber).ToList(),

                    5 => desc // Updated
                        ? _loadedTickets.OrderByDescending(t => t.UpdatedAt).ThenByDescending(t => t.TicketNumber).ToList()
                        : _loadedTickets.OrderBy(t => t.UpdatedAt).ThenBy(t => t.TicketNumber).ToList(),

                    _ => _loadedTickets
                };
            }

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
                    .Where(x => !string.IsNullOrWhiteSpace(x));

                var orderedApps = (_sortColumn == 2 && _sortState == SortState.Descending)
                    ? allApps.OrderByDescending(x => GetAppDisplayName(x), StringComparer.OrdinalIgnoreCase)
                    : allApps.OrderBy(x => GetAppDisplayName(x), StringComparer.OrdinalIgnoreCase);

                foreach (var a in orderedApps)
                {
                    string disp = GetAppDisplayName(a);
                    var grp = new ListViewGroup(a, $"📱  {disp}");
                    groupMap[a.ToLowerInvariant()] = grp;
                    _lvTickets.Groups.Add(grp);
                }
            }

            foreach (var ticket in _loadedTickets)
            {
                var lvi = new ListViewItem(ticket.FormattedId);
                lvi.UseItemStyleForSubItems = false;

                string appKey = string.IsNullOrWhiteSpace(ticket.App) ? "general" : ticket.App;
                string appKeyLower = appKey.ToLowerInvariant();
                lvi.ImageKey = _spacerImageList.Images.ContainsKey(appKeyLower) ? appKeyLower : "default";

                Color appColor = GetAppColor(ticket.App);
                Color readableAppColor = EnsureReadableColor(appColor);
                Color priorityColor = ticket.Priority.GetBadgeColor();

                // SubItem 0: ID
                lvi.SubItems[0].ForeColor = Color.FromArgb(30, 41, 59);
                lvi.SubItems[0].Font = new Font(_lvTickets.Font, FontStyle.Bold);

                // SubItem 1: Priority
                var subPri = lvi.SubItems.Add(ticket.Priority.ToDisplayName());
                subPri.ForeColor = priorityColor;
                subPri.Font = new Font(_lvTickets.Font, FontStyle.Bold);

                // SubItem 2: App (styled in app's signature color)
                var subApp = lvi.SubItems.Add(GetAppDisplayName(ticket.App));
                subApp.ForeColor = readableAppColor;
                subApp.Font = new Font(_lvTickets.Font, FontStyle.Bold);

                // SubItem 3: Type (styled in type color)
                var subType = lvi.SubItems.Add(ticket.TicketType);
                subType.ForeColor = TicketTypeHelper.GetTypeColor(ticket.TicketType);

                // SubItem 4: Title
                string titleDisplay = ticket.AttachmentCount > 0 ? $"📎 {ticket.Title}" : ticket.Title;
                var subTitle = lvi.SubItems.Add(titleDisplay);
                subTitle.ForeColor = Color.FromArgb(17, 24, 39);

                // SubItem 5: Updated
                var subDate = lvi.SubItems.Add(ticket.UpdatedAt.ToString("MM-dd HH:mm"));
                subDate.ForeColor = Color.FromArgb(100, 116, 139);

                lvi.Tag = ticket;

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
                        string disp = GetAppDisplayName(key);
                        var newGrp = new ListViewGroup(key, $"📱  {disp}");
                        groupMap[key] = newGrp;
                        _lvTickets.Groups.Add(newGrp);
                        lvi.Group = newGrp;
                    }
                }

                _lvTickets.Items.Add(lvi);
            }

            _lvTickets.EndUpdate();

            var preserveList = preserveSelectionIds?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            if (preserveList != null && preserveList.Count > 0)
            {
                var idSet = new HashSet<string>(preserveList, StringComparer.OrdinalIgnoreCase);
                _lvTickets.BeginUpdate();
                bool first = true;
                foreach (ListViewItem item in _lvTickets.Items)
                {
                    if (item.Tag is TicketItem t && idSet.Contains(t.Id))
                    {
                        item.Selected = true;
                        if (first)
                        {
                            item.EnsureVisible();
                            first = false;
                        }
                    }
                }
                _lvTickets.EndUpdate();
            }
            else
            {
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
            }

            // Immediately apply selection change after filter reload
            _selectionDebounceTimer.Stop();
            ApplySelectionChange();

            // Update bottom status strip metrics
            var stats = _dbService.GetStatistics(app);
            string statusMsg = $"Showing {_loadedTickets.Count} tickets";
            string metrics = $"Total: {stats.Total} | Backlog: {stats.Backlog} | Todo: {stats.Todo} | Doing: {stats.Doing} | Done: {stats.Done} | Killed: {stats.Killed}";
            StatusUpdated?.Invoke(statusMsg, metrics);
        }

        private void OnTicketSelectionChanged(object? sender, EventArgs e)
        {
            _selectionDebounceTimer.Stop();
            _selectionDebounceTimer.Start();
        }

        private void ApplySelectionChange()
        {
            if (this.IsDisposed || _lvTickets.IsDisposed) return;

            int count = _lvTickets.SelectedItems.Count;
            if (count > 1)
            {
                if (!_pnlBatchScroll.Visible)
                {
                    _pnlDetailScroll.Visible = false;
                    _pnlBatchScroll.Visible = true;
                    _pnlBatchScroll.BringToFront();
                }
                ShowBatchActions();
            }
            else if (count == 1)
            {
                if (!_pnlDetailScroll.Visible)
                {
                    _pnlBatchScroll.Visible = false;
                    _pnlDetailScroll.Visible = true;
                    _pnlDetailScroll.BringToFront();
                }
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
                if (!_pnlDetailScroll.Visible)
                {
                    _pnlBatchScroll.Visible = false;
                    _pnlDetailScroll.Visible = true;
                    _pnlDetailScroll.BringToFront();
                }
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
                _cardAttachments.Visible = false;
                return;
            }

            _cardAttachments.Visible = true;
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

            Color appColor = GetAppColor(ticket.App);
            Color readableAppColor = EnsureReadableColor(appColor);
            _lblAppBadge.Visible = true;
            _lblAppBadge.Text = GetAppDisplayName(ticket.App);
            _lblAppBadge.ForeColor = readableAppColor;
            _lblAppBadge.BackColor = Color.FromArgb(
                Math.Clamp((int)(255 * 0.88 + appColor.R * 0.12), 0, 255),
                Math.Clamp((int)(255 * 0.88 + appColor.G * 0.12), 0, 255),
                Math.Clamp((int)(255 * 0.88 + appColor.B * 0.12), 0, 255)
            );

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

            RenderDetailAttachments();
        }

        private void RenderDetailAttachments()
        {
            if (_currentTicket == null) return;

            _currentTicket.Attachments = _dbService.GetAttachmentsForTicket(_currentTicket.Id);
            int count = _currentTicket.AttachmentCount;
            _lblDetailAttachmentsHeader.Text = $"📎  Attachments ({count})";

            _pnlDetailAttachmentsList.SuspendLayout();
            _pnlDetailAttachmentsList.Controls.Clear();

            int scrollW = _pnlDetailScroll.ClientSize.Width > 0 ? _pnlDetailScroll.ClientSize.Width : _pnlDetailScroll.Width;
            if (_split?.Panel2 != null && _split.Panel2.ClientSize.Width > 0)
            {
                int p2W = _split.Panel2.ClientSize.Width;
                if (p2W > scrollW) scrollW = p2W;
            }
            int cardW = Math.Max(320, scrollW - 36);
            int cardInnerW = Math.Max(240, cardW - 32);

            if (count == 0)
            {
                var lblEmpty = new Label
                {
                    Text = "No attachments for this ticket. Drag & drop logs or screenshots here, or click Add File / 📋 Paste.",
                    ForeColor = Color.FromArgb(130, 140, 150),
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                    AutoSize = true,
                    Margin = new Padding(0, 4, 0, 4)
                };
                _pnlDetailAttachmentsList.Controls.Add(lblEmpty);
            }
            else
            {
                foreach (var att in _currentTicket.Attachments)
                {
                    var attRow = CreateAttachmentDetailRow(att, cardInnerW);
                    _pnlDetailAttachmentsList.Controls.Add(attRow);
                }
            }

            _pnlDetailAttachmentsList.ResumeLayout(true);
            UpdateDetailLayout();
        }

        private Control CreateAttachmentDetailRow(TicketAttachment att, int width)
        {
            var pnl = new Panel
            {
                Width = width,
                Height = 64,
                BackColor = Color.FromArgb(250, 251, 253),
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(10, 6, 10, 6)
            };
            EnableDoubleBuffer(pnl);

            pnl.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(226, 232, 240), 1);
                e.Graphics.DrawRectangle(p, 0, 0, pnl.Width - 1, pnl.Height - 1);
            };
            pnl.Resize += (s, e) => pnl.Invalidate();

            var btnOpen = new Button
            {
                Text = "Open",
                AutoSize = true,
                Padding = new Padding(14, 0, 14, 0),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 102, 204),
                BackColor = Color.FromArgb(240, 246, 255),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 6, 0)
            };
            btnOpen.FlatAppearance.BorderColor = Color.FromArgb(200, 220, 245);
            btnOpen.Click += (s, e) => AttachmentStorageService.OpenAttachment(att);

            int btnH = Math.Max(32, btnOpen.PreferredSize.Height);
            int topPad = Math.Max(0, (52 - btnH) / 2);

            // Actions panel docked Right with comfortable padding so no buttons are clipped
            var pnlActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0),
                Padding = new Padding(8, topPad, 4, 0)
            };

            var btnFolder = new Button
            {
                Text = "📁",
                Size = new Size(btnH, btnH),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.FromArgb(70, 80, 95),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 6, 0)
            };
            btnFolder.FlatAppearance.BorderSize = 0;
            btnFolder.FlatAppearance.MouseOverBackColor = Color.FromArgb(235, 240, 248);
            btnFolder.Click += (s, e) => AttachmentStorageService.ShowInExplorer(att);

            var btnDel = new Button
            {
                Text = "🗑",
                Size = new Size(btnH, btnH),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.FromArgb(220, 53, 69),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 4, 0)
            };
            btnDel.FlatAppearance.BorderSize = 0;
            btnDel.FlatAppearance.MouseOverBackColor = Color.FromArgb(254, 238, 240);
            btnDel.Click += (s, e) =>
            {
                var confirm = MessageBox.Show(this, $"Delete attachment \"{att.FileName}\"?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm == DialogResult.Yes)
                {
                    _dbService.DeleteAttachment(att.Id);
                    RenderDetailAttachments();
                    ApplyFilters();
                }
            };

            var tip = new ToolTip();
            tip.SetToolTip(btnOpen, "Open attachment in default application");
            tip.SetToolTip(btnFolder, "Show in File Explorer");
            tip.SetToolTip(btnDel, "Delete attachment");

            pnlActions.Controls.Add(btnOpen);
            pnlActions.Controls.Add(btnFolder);
            pnlActions.Controls.Add(btnDel);

            // Icon docked Left
            string icon = att.IsImage ? "🖼️" : (att.IsLogOrText ? "📄" : "📎");
            var lblIcon = new Label
            {
                Text = icon,
                Font = new Font("Segoe UI", 14F),
                Dock = DockStyle.Left,
                Width = 32,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0)
            };

            // Middle text panel filling remaining width with ellipsis
            var pnlText = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8, 4, 8, 4),
                Margin = new Padding(0)
            };

            var lblName = new Label
            {
                Text = att.FileName,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 102, 204),
                Dock = DockStyle.Top,
                Height = 22,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Cursor = Cursors.Hand
            };
            lblName.Click += (s, e) => AttachmentStorageService.OpenAttachment(att);

            var lblMeta = new Label
            {
                Text = $"{att.FormattedFileSize}  •  {att.CreatedAt:yyyy-MM-dd HH:mm} UTC",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(115, 125, 135),
                Dock = DockStyle.Bottom,
                Height = 20,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };

            pnlText.Controls.Add(lblName);
            pnlText.Controls.Add(lblMeta);

            pnl.Controls.Add(pnlText);
            pnl.Controls.Add(lblIcon);
            pnl.Controls.Add(pnlActions);
            pnlText.BringToFront();

            return pnl;
        }

        private void OnDetailAddAttachmentClicked()
        {
            if (_currentTicket == null) return;

            using var ofd = new OpenFileDialog
            {
                Title = $"Attach Files to {_currentTicket.FormattedId}",
                Multiselect = true,
                Filter = "All Files (*.*)|*.*|Logs & Text (*.log;*.txt;*.json;*.xml;*.csv)|*.log;*.txt;*.json;*.xml;*.csv|Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp"
            };

            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                foreach (var file in ofd.FileNames)
                {
                    try
                    {
                        var att = AttachmentStorageService.SaveAttachmentFile(_currentTicket.Id, file);
                        _dbService.AddAttachment(att);
                        LogService.Success("Attachments", $"Attached {att.FileName} to {_currentTicket.FormattedId}");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, $"Failed to attach {Path.GetFileName(file)}: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                RenderDetailAttachments();
                ApplyFilters();
            }
        }

        private void OnDetailPasteScreenshotClicked()
        {
            if (_currentTicket == null) return;

            try
            {
                if (Clipboard.ContainsImage())
                {
                    using var img = Clipboard.GetImage();
                    if (img != null)
                    {
                        using var ms = new MemoryStream();
                        img.Save(ms, ImageFormat.Png);
                        byte[] bytes = ms.ToArray();
                        var att = AttachmentStorageService.SaveImageBytes(_currentTicket.Id, bytes);
                        _dbService.AddAttachment(att);
                        LogService.Success("Attachments", $"Pasted screenshot {att.FileName} into {_currentTicket.FormattedId}");
                        RenderDetailAttachments();
                        ApplyFilters();
                        return;
                    }
                }

                MessageBox.Show(this, "No image found in clipboard.\n\nTip: Use Windows Snipping Tool (Win+Shift+S) or copy an image first.", "No Clipboard Image", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not paste screenshot: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnDetailDragEnter(object? sender, DragEventArgs e)
        {
            if (_currentTicket != null && e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void OnDetailDragDrop(object? sender, DragEventArgs e)
        {
            if (_currentTicket == null) return;
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null)
                {
                    foreach (var file in files)
                    {
                        try
                        {
                            var att = AttachmentStorageService.SaveAttachmentFile(_currentTicket.Id, file);
                            _dbService.AddAttachment(att);
                            LogService.Success("Attachments", $"Attached {att.FileName} to {_currentTicket.FormattedId}");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, $"Failed to attach {Path.GetFileName(file)}: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    RenderDetailAttachments();
                    ApplyFilters();
                }
            }
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
            this.BeginInvoke(new Action(() =>
            {
                ApplySavedSplitterDistance();
                UpdateDetailLayout();
            }));
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
                UpdateDetailLayout();
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

        private void OnTicketColumnClick(object? sender, ColumnClickEventArgs e)
        {
            int col = e.Column;
            if (col < 0 || col >= TicketColumnTitles.Length) return;

            if (_sortColumn != col)
            {
                _sortColumn = col;
                _sortState = SortState.Descending;
            }
            else
            {
                _sortState = _sortState switch
                {
                    SortState.Descending => SortState.Ascending,
                    SortState.Ascending => SortState.Default,
                    _ => SortState.Descending
                };

                if (_sortState == SortState.Default)
                {
                    _sortColumn = -1;
                }
            }

            UpdateColumnHeaderSortIndicators();
            ApplyFilters();
        }

        private void UpdateColumnHeaderSortIndicators()
        {
            for (int i = 0; i < TicketColumnTitles.Length && i < _lvTickets.Columns.Count; i++)
            {
                if (i == _sortColumn)
                {
                    if (_sortState == SortState.Descending)
                    {
                        _lvTickets.Columns[i].Text = $"{TicketColumnTitles[i]} ▼";
                    }
                    else if (_sortState == SortState.Ascending)
                    {
                        _lvTickets.Columns[i].Text = $"{TicketColumnTitles[i]} ▲";
                    }
                    else
                    {
                        _lvTickets.Columns[i].Text = TicketColumnTitles[i];
                    }
                }
                else
                {
                    _lvTickets.Columns[i].Text = TicketColumnTitles[i];
                }
            }
        }

        private void OnExportSingleTicketClicked()
        {
            if (_currentTicket == null) return;

            var menu = new ContextMenuStrip();
            string defaultFormatStr = _configService.Settings.DefaultExportFormat ?? "Zip";
            ExportFormat defaultFormat = ParseExportFormat(defaultFormatStr);

            var itemDefault = new ToolStripMenuItem($"⚡ Quick Export ({defaultFormatStr})", null, (s, e) =>
            {
                DoExportSingleTicket(_currentTicket, defaultFormat);
            })
            {
                Font = new Font(menu.Font, FontStyle.Bold)
            };
            menu.Items.Add(itemDefault);
            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add(new ToolStripMenuItem("📦 Export as Zip Archive (.zip)", null, (s, e) =>
            {
                DoExportSingleTicket(_currentTicket, ExportFormat.Zip);
            }));

            menu.Items.Add(new ToolStripMenuItem("📁 Export as Folder Directory", null, (s, e) =>
            {
                DoExportSingleTicket(_currentTicket, ExportFormat.Folder);
            }));

            menu.Items.Add(new ToolStripMenuItem("📄 Export as Single Markdown File (.md)", null, (s, e) =>
            {
                DoExportSingleTicket(_currentTicket, ExportFormat.Markdown);
            }));

            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add(new ToolStripMenuItem("🌐 Export All Tickets with Index...", null, (s, e) =>
            {
                var allTickets = _dbService.GetAllTickets();
                DoExportBatchTickets(allTickets, "All");
            }));

            menu.Items.Add(new ToolStripMenuItem("📂 Open Export Destination Folder", null, (s, e) =>
            {
                string dir = _configService.GetExportDirectory();
                TicketExportService.RevealInExplorer(dir);
            }));

            menu.Show(_btnActionExport, new Point(0, _btnActionExport.Height + 2));
        }

        private void DoExportSingleTicket(TicketItem ticket, ExportFormat format)
        {
            try
            {
                // Ensure ticket has latest attachments
                ticket.Attachments = _dbService.GetAttachmentsForTicket(ticket.Id);

                string exportDir = _configService.GetExportDirectory();
                string resultPath = TicketExportService.ExportTicket(ticket, exportDir, format);
                LogService.Success("Export", $"Exported ticket {ticket.FormattedId} to: {resultPath}");

                var res = MessageBox.Show(this,
                    $"Ticket {ticket.FormattedId} successfully exported to:\n\n{resultPath}\n\nWould you like to open or reveal the exported item in Windows Explorer?",
                    "Export Successful",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (res == DialogResult.Yes)
                {
                    TicketExportService.RevealInExplorer(resultPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Export failed: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                LogService.Error("Export", $"Failed to export ticket {ticket.FormattedId}: {ex.Message}");
            }
        }

        private void DoExportBatchTickets(List<TicketItem> tickets, string batchSuffix)
        {
            if (tickets.Count == 0)
            {
                MessageBox.Show(this, "There are no tickets to export.", "Export Tickets", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                // Ensure all tickets have attachments populated
                var attMap = _dbService.GetAllAttachmentsGrouped();
                foreach (var t in tickets)
                {
                    if (attMap.TryGetValue(t.Id, out var atts))
                    {
                        t.Attachments = atts;
                    }
                }

                string exportDir = _configService.GetExportDirectory();
                var defaultFormat = ParseExportFormat(_configService.Settings.DefaultExportFormat);
                string batchName = $"KT_Batch_{batchSuffix}_{DateTime.Now:yyyyMMdd_HHmmss}";

                string resultPath = TicketExportService.ExportBatchTickets(tickets, exportDir, defaultFormat, batchName);
                LogService.Success("Export", $"Batch exported {tickets.Count} tickets to: {resultPath}");

                var res = MessageBox.Show(this,
                    $"Successfully exported {tickets.Count} tickets with INDEX.md catalog to:\n\n{resultPath}\n\nWould you like to open or reveal the exported item in Windows Explorer?",
                    "Batch Export Successful",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (res == DialogResult.Yes)
                {
                    TicketExportService.RevealInExplorer(resultPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Batch export failed: {ex.Message}", "Batch Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                LogService.Error("Export", $"Failed to batch export tickets: {ex.Message}");
            }
        }

        private static ExportFormat ParseExportFormat(string? format)
        {
            if (string.IsNullOrWhiteSpace(format)) return ExportFormat.Zip;
            if (string.Equals(format, "Folder", StringComparison.OrdinalIgnoreCase)) return ExportFormat.Folder;
            if (string.Equals(format, "Markdown", StringComparison.OrdinalIgnoreCase) || string.Equals(format, "md", StringComparison.OrdinalIgnoreCase)) return ExportFormat.Markdown;
            return ExportFormat.Zip;
        }

        private string GetAppDisplayName(string? appKey)
        {
            if (string.IsNullOrWhiteSpace(appKey)) return "general";
            if (_appDisplayNames.TryGetValue(appKey, out string? disp) && !string.IsNullOrWhiteSpace(disp))
            {
                return disp;
            }
            return appKey;
        }

        private Color GetAppColor(string? appKey)
        {
            if (string.IsNullOrWhiteSpace(appKey)) appKey = "general";
            if (_appColorCache.TryGetValue(appKey, out var color))
            {
                return color;
            }

            string hex = _dbService.GetAppColorHex(appKey);
            Color parsed = ParseColor(hex, Color.FromArgb(0, 120, 215));
            _appColorCache[appKey] = parsed;
            return parsed;
        }

        private void EnsureAppStripImages()
        {
            if (_spacerImageList == null) return;

            _spacerImageList.Images.Clear();
            _spacerImageList.Images.Add("default", CreateColorStripBitmap(Color.FromArgb(160, 174, 192)));

            foreach (var kvp in _appColorCache)
            {
                string key = kvp.Key.ToLowerInvariant();
                _spacerImageList.Images.Add(key, CreateColorStripBitmap(kvp.Value));
            }
        }

        private static Bitmap CreateColorStripBitmap(Color color)
        {
            int w = 6;
            int h = 22;
            var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            using var brush = new SolidBrush(color);
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            int x = 1;
            int y = 1;
            int diameter = 4;
            int height = h - 2;

            path.AddArc(x, y, diameter, diameter, 180, 180);
            path.AddArc(x, y + height - diameter, diameter, diameter, 0, 180);
            path.CloseFigure();

            g.FillPath(brush, path);
            return bmp;
        }

        public static Color EnsureReadableColor(Color c)
        {
            double luminance = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
            if (luminance > 0.65)
            {
                double factor = 0.65 / luminance;
                return Color.FromArgb(
                    Math.Clamp((int)(c.R * factor), 0, 255),
                    Math.Clamp((int)(c.G * factor), 0, 255),
                    Math.Clamp((int)(c.B * factor), 0, 255)
                );
            }
            return c;
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

        private static void EnableDoubleBuffer(Control control)
        {
            try
            {
                typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.SetValue(control, true, null);
            }
            catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _selectionDebounceTimer.Stop();
                _selectionDebounceTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
