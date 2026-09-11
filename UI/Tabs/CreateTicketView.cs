using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using KerkenezTicket.Models;
using KerkenezTicket.Services;

namespace KerkenezTicket.UI.Tabs
{
    public class CreateTicketView : UserControl
    {
        private readonly ConfigService _configService;
        private readonly TicketDatabaseService _dbService;

        private TextBox _txtTitle = null!;
        private ComboBox _cboApp = null!;
        private ComboBox _cboType = null!;
        private ComboBox _cboPriority = null!;
        private ComboBox _cboStatus = null!;
        private TextBox _txtTags = null!;
        private TextBox _txtDescription = null!;
        private TextBox _txtNotes = null!;
        private Button _btnCreate = null!;
        private Button _btnClear = null!;
        private Label _lblStatusMsg = null!;

        public event Action<TicketItem>? TicketCreated;

        private const int ContentWidth = 780;

        public CreateTicketView(ConfigService configService, TicketDatabaseService dbService)
        {
            _configService = configService;
            _dbService = dbService;

            InitializeComponent();
            ResetFormDefaults();
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
                Height = 78,
                Margin = new Padding(0, 0, 0, 14)
            };

            var lblTitle = new Label
            {
                Text = "🎫  Create New Ticket",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.FromArgb(24, 28, 36),
                AutoSize = true,
                Location = new Point(0, 0)
            };

            var lblSubtitle = new Label
            {
                Text = "Create an internal ticket. Data is stored at %APPDATA%\\Kerkenez\\ticket\\tickets.db and protected by Windows DPAPI.",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(108, 117, 125),
                AutoSize = true,
                Location = new Point(2, 40)
            };

            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(lblSubtitle);
            mainFlow.Controls.Add(headerPanel);

            // Main Form Card
            var card = CreateCardPanel(ContentWidth);
            int innerWidth = ContentWidth - 48; // accounting for 24px padding on each side
            int colWidth = (innerWidth - 20) / 2; // two columns

            // 1. Title
            var pnlTitle = new Panel { Width = innerWidth, Height = 64, Margin = new Padding(0, 0, 0, 8) };
            var lblT = new Label { Text = "Ticket Title *", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(35, 40, 50), AutoSize = true, Location = new Point(0, 0) };
            _txtTitle = new TextBox { Width = innerWidth, Height = 30, Font = new Font("Segoe UI", 10F), Location = new Point(0, 22) };
            pnlTitle.Controls.Add(lblT);
            pnlTitle.Controls.Add(_txtTitle);
            card.Controls.Add(pnlTitle);

            // 2. Row 1: Target App & Ticket Type
            var row1 = new FlowLayoutPanel
            {
                Width = innerWidth,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 4)
            };

            _cboApp = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Font = new Font("Segoe UI", 9F), Height = 28 };
            var fieldApp = CreateFormField("Target Application:", "Select or enter which project/app this ticket belongs to", _cboApp, colWidth);

            _cboType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F), Height = 28 };
            var fieldType = CreateFormField("Ticket Type:", "Bug report, feature request, sync task, etc.", _cboType, colWidth);

            row1.Controls.Add(fieldApp);
            row1.Controls.Add(fieldType);
            card.Controls.Add(row1);

            // 3. Row 2: Priority & Workflow Status
            var row2 = new FlowLayoutPanel
            {
                Width = innerWidth,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 4)
            };

            _cboPriority = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F), Height = 28 };
            _cboPriority.Items.AddRange(new[] { "Low", "Medium", "High", "Urgent" });
            var fieldPriority = CreateFormField("Priority Urgency:", "Severity and triage urgency ranking", _cboPriority, colWidth);

            _cboStatus = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F), Height = 28 };
            _cboStatus.Items.AddRange(new[] { "To Do", "Doing", "Done", "Killed" });
            var fieldStatus = CreateFormField("Initial Workflow Status:", "Initial stage in the triage cycle", _cboStatus, colWidth);

            row2.Controls.Add(fieldPriority);
            row2.Controls.Add(fieldStatus);
            card.Controls.Add(row2);

            // 4. Tags
            var pnlTags = new Panel { Width = innerWidth, Height = 64, Margin = new Padding(0, 0, 0, 8) };
            var lblTags = new Label { Text = "Tags (comma-separated):", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(35, 40, 50), AutoSize = true, Location = new Point(0, 0) };
            _txtTags = new TextBox { Width = innerWidth, Height = 28, Font = new Font("Segoe UI", 9F), PlaceholderText = "e.g. sync, refresh, ui, error", Location = new Point(0, 22) };
            pnlTags.Controls.Add(lblTags);
            pnlTags.Controls.Add(_txtTags);
            card.Controls.Add(pnlTags);

            // 5. Description
            var pnlDesc = new Panel { Width = innerWidth, Height = 162, Margin = new Padding(0, 0, 0, 8) };
            var lblDesc = new Label { Text = "Description:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(35, 40, 50), AutoSize = true, Location = new Point(0, 0) };
            _txtDescription = new TextBox
            {
                Width = innerWidth,
                Height = 132,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Segoe UI", 9.5F),
                Location = new Point(0, 22)
            };
            pnlDesc.Controls.Add(lblDesc);
            pnlDesc.Controls.Add(_txtDescription);
            card.Controls.Add(pnlDesc);

            // 6. Notes
            var pnlNotes = new Panel { Width = innerWidth, Height = 105, Margin = new Padding(0, 0, 0, 16) };
            var lblNotes = new Label { Text = "Initial Work Notes / Links:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(35, 40, 50), AutoSize = true, Location = new Point(0, 0) };
            _txtNotes = new TextBox
            {
                Width = innerWidth,
                Height = 75,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Segoe UI", 9F),
                Location = new Point(0, 22)
            };
            pnlNotes.Controls.Add(lblNotes);
            pnlNotes.Controls.Add(_txtNotes);
            card.Controls.Add(pnlNotes);

            // 7. Action Buttons
            var rowBtns = new FlowLayoutPanel
            {
                Width = innerWidth,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 4, 0, 0)
            };

            _btnCreate = new Button
            {
                Text = "➕  Create Ticket",
                AutoSize = true,
                Height = 38,
                Padding = new Padding(20, 0, 20, 0),
                BackColor = Color.FromArgb(0, 102, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnCreate.FlatAppearance.BorderSize = 0;
            _btnCreate.Click += OnCreateTicketClicked;

            _btnClear = new Button
            {
                Text = "Clear Form",
                AutoSize = true,
                Height = 38,
                Padding = new Padding(16, 0, 16, 0),
                BackColor = Color.FromArgb(240, 242, 245),
                ForeColor = Color.FromArgb(50, 55, 65),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(12, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            _btnClear.FlatAppearance.BorderColor = Color.FromArgb(215, 222, 230);
            _btnClear.Click += (s, e) => ResetFormDefaults();

            _lblStatusMsg = new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(40, 167, 69),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(16, 10, 0, 0)
            };

            rowBtns.Controls.Add(_btnCreate);
            rowBtns.Controls.Add(_btnClear);
            rowBtns.Controls.Add(_lblStatusMsg);
            card.Controls.Add(rowBtns);

            mainFlow.Controls.Add(card);
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
                Padding = new Padding(24, 20, 24, 24),
                Margin = new Padding(0, 0, 0, 24)
            };

            pnl.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(222, 226, 230), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, pnl.Width - 1, pnl.Height - 1);
            };

            return pnl;
        }

        private static Panel CreateFormField(string title, string subtitle, Control control, int width)
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

        public void ResetFormDefaults()
        {
            _txtTitle.Text = "";
            _txtDescription.Text = "";
            _txtNotes.Text = "";
            _txtTags.Text = "";
            _lblStatusMsg.Text = "";

            _cboApp.Items.Clear();
            foreach (var app in _configService.Settings.KnownApps)
            {
                _cboApp.Items.Add(app);
            }
            if (!string.IsNullOrWhiteSpace(_configService.Settings.DefaultApp))
            {
                _cboApp.Text = _configService.Settings.DefaultApp;
            }
            else if (_cboApp.Items.Count > 0)
            {
                _cboApp.SelectedIndex = 0;
            }
            else
            {
                _cboApp.Text = "";
            }

            _cboType.Items.Clear();
            foreach (var t in _configService.Settings.KnownTypes)
            {
                _cboType.Items.Add(t);
            }
            _cboType.SelectedItem = _configService.Settings.DefaultType;
            if (_cboType.SelectedIndex < 0 && _cboType.Items.Count > 0) _cboType.SelectedIndex = 0;

            _cboPriority.SelectedItem = _configService.Settings.DefaultPriority;
            if (_cboPriority.SelectedIndex < 0) _cboPriority.SelectedItem = "Medium";

            _cboStatus.SelectedItem = "To Do";
        }

        private void OnCreateTicketClicked(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_txtTitle.Text))
            {
                MessageBox.Show(this, "Ticket title is required.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtTitle.Focus();
                return;
            }

            string selectedApp = !string.IsNullOrWhiteSpace(_cboApp.Text)
                ? _cboApp.Text.Trim()
                : (_cboApp.SelectedItem?.ToString() ?? "general");
            if (string.IsNullOrWhiteSpace(selectedApp)) selectedApp = "general";

            var ticket = new TicketItem
            {
                Title = _txtTitle.Text.Trim(),
                App = selectedApp,
                TicketType = _cboType.SelectedItem?.ToString() ?? "Bug",
                Priority = TicketPriorityExtensions.ParsePriority(_cboPriority.SelectedItem?.ToString()),
                Status = TicketStatusExtensions.ParseStatus(_cboStatus.SelectedItem?.ToString()),
                Description = _txtDescription.Text.Trim(),
                Notes = _txtNotes.Text.Trim(),
                Tags = _txtTags.Text.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToList()
            };

            var created = _dbService.AddTicket(ticket);
            _lblStatusMsg.Text = $"✓ Created {created.FormattedId} successfully!";

            ResetFormDefaults();
            TicketCreated?.Invoke(created);
        }
    }
}
