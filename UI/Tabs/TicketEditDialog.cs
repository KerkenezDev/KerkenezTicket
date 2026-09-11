using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using KerkenezTicket.Models;
using KerkenezTicket.Services;

namespace KerkenezTicket.UI.Tabs
{
    public class TicketEditDialog : Form
    {
        private readonly TicketItem _ticket;
        private readonly ConfigService _configService;
        private readonly TicketDatabaseService? _dbService;

        private TextBox _txtTitle = null!;
        private ComboBox _cboApp = null!;
        private ComboBox _cboType = null!;
        private ComboBox _cboPriority = null!;
        private ComboBox _cboStatus = null!;
        private TextBox _txtTags = null!;
        private TextBox _txtDescription = null!;
        private TextBox _txtNotes = null!;
        private Button _btnSave = null!;
        private Button _btnCancel = null!;

        public TicketItem UpdatedTicket => _ticket;

        public TicketEditDialog(TicketItem ticket, ConfigService configService, TicketDatabaseService? dbService = null)
        {
            _ticket = ticket.Clone();
            _configService = configService;
            _dbService = dbService;

            InitializeComponent();
            PopulateData();
        }

        private void InitializeComponent()
        {
            this.Text = $"Edit Ticket {_ticket.FormattedId}";
            this.Size = new Size(620, 680);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(248, 249, 250);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            int pad = 20;
            int labelW = 100;
            int inputW = 450;
            int y = 20;
            int lineH = 38;

            // 1. Title
            var lblTitle = new Label { Text = "Title *", Location = new Point(pad, y + 4), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _txtTitle = new TextBox { Location = new Point(pad + labelW, y), Width = inputW, Height = 28 };
            this.Controls.Add(lblTitle);
            this.Controls.Add(_txtTitle);
            y += lineH;

            // 2. App & Type (side-by-side)
            var lblApp = new Label { Text = "App", Location = new Point(pad, y + 4), AutoSize = true };
            _cboApp = new ComboBox { Location = new Point(pad + labelW, y), Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
            var lblType = new Label { Text = "Type", Location = new Point(pad + labelW + 195, y + 4), AutoSize = true };
            _cboType = new ComboBox { Location = new Point(pad + labelW + 265, y), Width = 185, DropDownStyle = ComboBoxStyle.DropDownList };
            this.Controls.Add(lblApp);
            this.Controls.Add(_cboApp);
            this.Controls.Add(lblType);
            this.Controls.Add(_cboType);
            y += lineH;

            // 3. Priority & Status (side-by-side)
            var lblPriority = new Label { Text = "Priority", Location = new Point(pad, y + 4), AutoSize = true };
            _cboPriority = new ComboBox { Location = new Point(pad + labelW, y), Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
            var lblStatus = new Label { Text = "Status", Location = new Point(pad + labelW + 195, y + 4), AutoSize = true };
            _cboStatus = new ComboBox { Location = new Point(pad + labelW + 265, y), Width = 185, DropDownStyle = ComboBoxStyle.DropDownList };
            this.Controls.Add(lblPriority);
            this.Controls.Add(_cboPriority);
            this.Controls.Add(lblStatus);
            this.Controls.Add(_cboStatus);
            y += lineH;

            // 4. Tags
            var lblTags = new Label { Text = "Tags", Location = new Point(pad, y + 4), AutoSize = true };
            _txtTags = new TextBox { Location = new Point(pad + labelW, y), Width = inputW };
            this.Controls.Add(lblTags);
            this.Controls.Add(_txtTags);
            y += lineH;

            // 5. Description
            var lblDesc = new Label { Text = "Description", Location = new Point(pad, y + 4), AutoSize = true };
            _txtDescription = new TextBox
            {
                Location = new Point(pad + labelW, y),
                Width = inputW,
                Height = 160,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };
            this.Controls.Add(lblDesc);
            this.Controls.Add(_txtDescription);
            y += 175;

            // 6. Notes / History
            var lblNotes = new Label { Text = "Work Notes", Location = new Point(pad, y + 4), AutoSize = true };
            _txtNotes = new TextBox
            {
                Location = new Point(pad + labelW, y),
                Width = inputW,
                Height = 110,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };
            this.Controls.Add(lblNotes);
            this.Controls.Add(_txtNotes);
            y += 125;

            // Bottom Buttons
            _btnSave = new Button
            {
                Text = "Save Changes",
                Location = new Point(this.Width - 250, y),
                Size = new Size(110, 32),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.Click += OnSaveClicked;

            _btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(this.Width - 130, y),
                Size = new Size(90, 32),
                BackColor = Color.FromArgb(235, 238, 242),
                ForeColor = Color.FromArgb(50, 50, 50),
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.Cancel
            };
            _btnCancel.FlatAppearance.BorderSize = 0;

            this.Controls.Add(_btnSave);
            this.Controls.Add(_btnCancel);
            this.AcceptButton = _btnSave;
            this.CancelButton = _btnCancel;
        }

        private void PopulateData()
        {
            _txtTitle.Text = _ticket.Title;
            _txtDescription.Text = _ticket.Description;
            _txtNotes.Text = _ticket.Notes;
            _txtTags.Text = _ticket.TagsSummary;

            // Apps
            var appsList = _dbService != null ? _dbService.GetAllAppNames() : _configService.Settings.KnownApps;
            var allApps = appsList.Union(_configService.Settings.KnownApps, StringComparer.OrdinalIgnoreCase)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);

            foreach (var app in allApps)
            {
                _cboApp.Items.Add(app);
            }
            if (!_cboApp.Items.Contains(_ticket.App)) _cboApp.Items.Add(_ticket.App);
            _cboApp.SelectedItem = _ticket.App;

            // Types
            foreach (var t in _configService.Settings.KnownTypes)
            {
                _cboType.Items.Add(t);
            }
            if (!_cboType.Items.Contains(_ticket.TicketType)) _cboType.Items.Add(_ticket.TicketType);
            _cboType.SelectedItem = _ticket.TicketType;

            // Priority
            _cboPriority.Items.Add("Low");
            _cboPriority.Items.Add("Medium");
            _cboPriority.Items.Add("High");
            _cboPriority.Items.Add("Urgent");
            _cboPriority.SelectedItem = _ticket.Priority.ToDisplayName();

            // Status
            _cboStatus.Items.Add("Backlog");
            _cboStatus.Items.Add("To Do");
            _cboStatus.Items.Add("Doing");
            _cboStatus.Items.Add("Done");
            _cboStatus.Items.Add("Killed");
            _cboStatus.SelectedItem = _ticket.Status.ToDisplayName();
        }

        private void OnSaveClicked(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_txtTitle.Text))
            {
                MessageBox.Show(this, "Title cannot be empty.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _ticket.Title = _txtTitle.Text.Trim();
            string editApp = _cboApp.SelectedItem?.ToString() ?? "general";
            if (string.IsNullOrWhiteSpace(editApp)) editApp = "general";
            _ticket.App = editApp;

            _dbService?.EnsureAppCategoryExists(editApp);
            _configService.SyncKnownApps(new[] { editApp });
            _ticket.TicketType = _cboType.SelectedItem?.ToString() ?? "Bug";
            _ticket.Priority = TicketPriorityExtensions.ParsePriority(_cboPriority.SelectedItem?.ToString());
            _ticket.Status = TicketStatusExtensions.ParseStatus(_cboStatus.SelectedItem?.ToString());
            _ticket.Description = _txtDescription.Text.Trim();
            _ticket.Notes = _txtNotes.Text.Trim();
            _ticket.Tags = _txtTags.Text.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
