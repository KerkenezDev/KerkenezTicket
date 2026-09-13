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
    public class CreateTicketView : UserControl
    {
        private class PendingAttachment
        {
            public string FileName { get; set; } = "";
            public long FileSizeBytes { get; set; }
            public string? SourceFilePath { get; set; }
            public byte[]? RawBytes { get; set; }
            public bool IsImage { get; set; }

            public string FormattedFileSize
            {
                get
                {
                    if (FileSizeBytes < 1024) return $"{FileSizeBytes} B";
                    if (FileSizeBytes < 1024 * 1024) return $"{(FileSizeBytes / 1024.0):F1} KB";
                    return $"{(FileSizeBytes / (1024.0 * 1024.0)):F1} MB";
                }
            }
        }

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

        // Attachments
        private readonly List<PendingAttachment> _pendingAttachments = new List<PendingAttachment>();
        private FlowLayoutPanel _pnlAttachmentsList = null!;
        private Label _lblAttachmentsSummary = null!;

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
            var pnlTitle = new Panel { Width = innerWidth, Height = 68, Margin = new Padding(0, 0, 0, 10) };
            var lblT = new Label { Text = "Ticket Title *", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(35, 40, 50), AutoSize = true, Location = new Point(0, 0) };
            _txtTitle = new TextBox { Width = innerWidth, Height = 30, Font = new Font("Segoe UI", 10F), Location = new Point(0, 26) };
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
                Margin = new Padding(0, 0, 0, 6)
            };

            _cboApp = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F), Height = 28 };
            var fieldApp = CreateFormField("Target Application:", "Select which project/app this ticket belongs to", _cboApp, colWidth);

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
                Margin = new Padding(0, 0, 0, 6)
            };

            _cboPriority = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F), Height = 28 };
            _cboPriority.Items.AddRange(new[] { "Low", "Medium", "High", "Urgent" });
            var fieldPriority = CreateFormField("Priority Urgency:", "Severity and triage urgency ranking", _cboPriority, colWidth);

            _cboStatus = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F), Height = 28 };
            _cboStatus.Items.AddRange(new[] { "Backlog", "To Do", "Doing", "Done", "Killed" });
            var fieldStatus = CreateFormField("Initial Workflow Status:", "Initial stage in the triage cycle", _cboStatus, colWidth);

            row2.Controls.Add(fieldPriority);
            row2.Controls.Add(fieldStatus);
            card.Controls.Add(row2);

            // 4. Tags
            var pnlTags = new Panel { Width = innerWidth, Height = 66, Margin = new Padding(0, 0, 0, 10) };
            var lblTags = new Label { Text = "Tags (comma-separated):", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(35, 40, 50), AutoSize = true, Location = new Point(0, 0) };
            _txtTags = new TextBox { Width = innerWidth, Height = 28, Font = new Font("Segoe UI", 9F), PlaceholderText = "e.g. sync, refresh, ui, error", Location = new Point(0, 26) };
            pnlTags.Controls.Add(lblTags);
            pnlTags.Controls.Add(_txtTags);
            card.Controls.Add(pnlTags);

            // 5. Description
            var pnlDesc = new Panel { Width = innerWidth, Height = 170, Margin = new Padding(0, 0, 0, 10) };
            var lblDesc = new Label { Text = "Description:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(35, 40, 50), AutoSize = true, Location = new Point(0, 0) };
            _txtDescription = new TextBox
            {
                Width = innerWidth,
                Height = 132,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Segoe UI", 9.5F),
                Location = new Point(0, 26)
            };
            pnlDesc.Controls.Add(lblDesc);
            pnlDesc.Controls.Add(_txtDescription);
            card.Controls.Add(pnlDesc);

            // 6. Notes
            var pnlNotes = new Panel { Width = innerWidth, Height = 112, Margin = new Padding(0, 0, 0, 16) };
            var lblNotes = new Label { Text = "Initial Work Notes / Links:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(35, 40, 50), AutoSize = true, Location = new Point(0, 0) };
            _txtNotes = new TextBox
            {
                Width = innerWidth,
                Height = 75,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Segoe UI", 9F),
                Location = new Point(0, 26)
            };
            pnlNotes.Controls.Add(lblNotes);
            pnlNotes.Controls.Add(_txtNotes);
            card.Controls.Add(pnlNotes);

            // 7. Attachments (Logs, Screenshots, Files)
            var pnlAtt = new Panel
            {
                Width = innerWidth,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 16)
            };

            var lblAtt = new Label
            {
                Text = "📎  Attachments (Logs, Screenshots, Files):",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(35, 40, 50),
                AutoSize = true,
                Location = new Point(0, 0)
            };

            _lblAttachmentsSummary = new Label
            {
                Text = "0 files attached. Drag & drop files here or use buttons below.",
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(110, 115, 125),
                AutoSize = true,
                Location = new Point(0, 22)
            };

            var attBox = new FlowLayoutPanel
            {
                Width = innerWidth,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.FromArgb(250, 251, 252),
                Padding = new Padding(12),
                Location = new Point(0, 44),
                Margin = new Padding(0, 44, 0, 0)
            };
            attBox.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(220, 226, 235), 1);
                e.Graphics.DrawRectangle(p, 0, 0, attBox.Width - 1, attBox.Height - 1);
            };

            var attToolbar = new FlowLayoutPanel
            {
                Width = innerWidth - 28,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 10)
            };

            var btnAddFiles = new Button
            {
                Text = "📎 Add File(s)...",
                AutoSize = true,
                Height = 30,
                BackColor = Color.FromArgb(240, 243, 248),
                ForeColor = Color.FromArgb(40, 45, 55),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 8, 0)
            };
            btnAddFiles.FlatAppearance.BorderColor = Color.FromArgb(210, 218, 228);
            btnAddFiles.Click += (s, e) => ShowAddFileDialog();

            var btnPasteImg = new Button
            {
                Text = "📋 Paste Screenshot",
                AutoSize = true,
                Height = 30,
                BackColor = Color.FromArgb(240, 243, 248),
                ForeColor = Color.FromArgb(40, 45, 55),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 8, 0)
            };
            btnPasteImg.FlatAppearance.BorderColor = Color.FromArgb(210, 218, 228);
            btnPasteImg.Click += (s, e) => PasteScreenshotFromClipboard();

            attToolbar.Controls.Add(btnAddFiles);
            attToolbar.Controls.Add(btnPasteImg);

            _pnlAttachmentsList = new FlowLayoutPanel
            {
                Width = innerWidth - 28,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0)
            };

            attBox.Controls.Add(attToolbar);
            attBox.Controls.Add(_pnlAttachmentsList);

            // Drag and drop setup on attachment box and whole control
            attBox.AllowDrop = true;
            attBox.DragEnter += OnAttachmentsDragEnter;
            attBox.DragDrop += OnAttachmentsDragDrop;
            this.AllowDrop = true;
            this.DragEnter += OnAttachmentsDragEnter;
            this.DragDrop += OnAttachmentsDragDrop;

            pnlAtt.Controls.Add(lblAtt);
            pnlAtt.Controls.Add(_lblAttachmentsSummary);
            pnlAtt.Controls.Add(attBox);
            card.Controls.Add(pnlAtt);

            // 8. Action Buttons
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
                Height = 84,
                Margin = new Padding(0, 0, 16, 8)
            };

            var lblT = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(35, 40, 50),
                AutoSize = true,
                Location = new Point(0, 0)
            };

            control.Location = new Point(0, 26);
            control.Width = width;

            var lblS = new Label
            {
                Text = subtitle,
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(120, 125, 135),
                AutoSize = true,
                Location = new Point(0, 58)
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

            _pendingAttachments.Clear();
            RefreshPendingAttachmentsList();

            _cboApp.Items.Clear();
            var dbApps = _dbService.GetAllAppNames();
            _configService.SyncKnownApps(dbApps);
            var allApps = dbApps.Union(_configService.Settings.KnownApps, StringComparer.OrdinalIgnoreCase)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var app in allApps)
            {
                _cboApp.Items.Add(app);
            }

            if (!string.IsNullOrWhiteSpace(_configService.Settings.DefaultApp) &&
                _cboApp.Items.Contains(_configService.Settings.DefaultApp))
            {
                _cboApp.SelectedItem = _configService.Settings.DefaultApp;
            }
            else if (_cboApp.Items.Count > 0)
            {
                _cboApp.SelectedIndex = 0;
            }
            else
            {
                _cboApp.SelectedIndex = -1;
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

            string selectedApp = _cboApp.SelectedItem?.ToString() ?? "general";
            if (string.IsNullOrWhiteSpace(selectedApp)) selectedApp = "general";

            _dbService.EnsureAppCategoryExists(selectedApp);
            _configService.SyncKnownApps(new[] { selectedApp });

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

            // Save staged attachments
            foreach (var pending in _pendingAttachments)
            {
                try
                {
                    TicketAttachment? att = null;
                    if (pending.RawBytes != null)
                    {
                        att = AttachmentStorageService.SaveImageBytes(created.Id, pending.RawBytes, pending.FileName);
                    }
                    else if (!string.IsNullOrEmpty(pending.SourceFilePath))
                    {
                        att = AttachmentStorageService.SaveAttachmentFile(created.Id, pending.SourceFilePath, pending.FileName);
                    }

                    if (att != null)
                    {
                        _dbService.AddAttachment(att);
                        created.Attachments.Add(att);
                    }
                }
                catch (Exception ex)
                {
                    LogService.Error("Attachments", $"Failed to save attachment {pending.FileName}: {ex.Message}");
                }
            }

            string attMsg = created.Attachments.Count > 0 ? $" with {created.Attachments.Count} attachment(s)" : "";
            _lblStatusMsg.Text = $"✓ Created {created.FormattedId} successfully{attMsg}!";

            ResetFormDefaults();
            TicketCreated?.Invoke(created);
        }

        private void ShowAddFileDialog()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select Attachments (Logs, Screenshots, Files)",
                Multiselect = true,
                Filter = "All Files (*.*)|*.*|Logs & Text (*.log;*.txt;*.json;*.xml;*.csv)|*.log;*.txt;*.json;*.xml;*.csv|Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp"
            };

            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                foreach (var file in ofd.FileNames)
                {
                    AddPendingFile(file);
                }
            }
        }

        private void PasteScreenshotFromClipboard()
        {
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
                        string name = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                        AddPendingImageBytes(bytes, name);
                        return;
                    }
                }

                MessageBox.Show(this, "No image found in clipboard.\n\nTip: Use Windows Snipping Tool (Win+Shift+S) or copy an image, then click Paste Screenshot.", "No Clipboard Image", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not paste screenshot: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddPendingFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                var fi = new FileInfo(path);
                string ext = fi.Extension.ToLowerInvariant();
                bool isImg = ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp" or ".ico";

                if (_pendingAttachments.Any(p => p.SourceFilePath != null && string.Equals(p.SourceFilePath, path, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                _pendingAttachments.Add(new PendingAttachment
                {
                    FileName = fi.Name,
                    FileSizeBytes = fi.Length,
                    SourceFilePath = path,
                    IsImage = isImg
                });

                RefreshPendingAttachmentsList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to attach file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddPendingImageBytes(byte[] bytes, string name)
        {
            _pendingAttachments.Add(new PendingAttachment
            {
                FileName = name,
                FileSizeBytes = bytes.Length,
                RawBytes = bytes,
                IsImage = true
            });

            RefreshPendingAttachmentsList();
        }

        private void OnAttachmentsDragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void OnAttachmentsDragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null)
                {
                    foreach (var file in files)
                    {
                        AddPendingFile(file);
                    }
                }
            }
        }

        private void RefreshPendingAttachmentsList()
        {
            _pnlAttachmentsList.SuspendLayout();
            _pnlAttachmentsList.Controls.Clear();

            int count = _pendingAttachments.Count;
            if (count == 0)
            {
                _lblAttachmentsSummary.Text = "0 files attached. Drag & drop files here or use buttons below.";
                var lblEmpty = new Label
                {
                    Text = "No attachments staged. Drop log files or screenshots here, or click Add File(s) / Paste Screenshot.",
                    ForeColor = Color.FromArgb(140, 145, 155),
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                    AutoSize = true,
                    Margin = new Padding(2, 6, 0, 4)
                };
                _pnlAttachmentsList.Controls.Add(lblEmpty);
            }
            else
            {
                long totalBytes = _pendingAttachments.Sum(p => p.FileSizeBytes);
                string totalStr = totalBytes < 1024 * 1024 ? $"{(totalBytes / 1024.0):F1} KB" : $"{(totalBytes / (1024.0 * 1024.0)):F1} MB";
                _lblAttachmentsSummary.Text = $"{count} file{(count > 1 ? "s" : "")} staged ({totalStr}).";

                foreach (var item in _pendingAttachments)
                {
                    var chip = CreatePendingChip(item);
                    _pnlAttachmentsList.Controls.Add(chip);
                }
            }

            _pnlAttachmentsList.ResumeLayout();
        }

        private Control CreatePendingChip(PendingAttachment item)
        {
            var pnl = new Panel
            {
                AutoSize = true,
                Height = 32,
                BackColor = Color.White,
                Padding = new Padding(8, 5, 6, 5),
                Margin = new Padding(0, 0, 8, 8)
            };

            pnl.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(218, 224, 233), 1);
                e.Graphics.DrawRectangle(p, 0, 0, pnl.Width - 1, pnl.Height - 1);
            };

            string icon = item.IsImage ? "🖼️" : (item.FileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase) || item.FileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ? "📄" : "📎");

            var lblName = new Label
            {
                Text = $"{icon} {item.FileName} ({item.FormattedFileSize})",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(35, 40, 50),
                AutoSize = true,
                Location = new Point(6, 6)
            };

            var btnRemove = new Button
            {
                Text = "✕",
                Size = new Size(20, 20),
                Location = new Point(lblName.PreferredWidth + 12, 5),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(160, 165, 175),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            btnRemove.FlatAppearance.BorderSize = 0;
            btnRemove.MouseEnter += (s, e) => { btnRemove.ForeColor = Color.FromArgb(220, 53, 69); };
            btnRemove.MouseLeave += (s, e) => { btnRemove.ForeColor = Color.FromArgb(160, 165, 175); };
            btnRemove.Click += (s, e) =>
            {
                _pendingAttachments.Remove(item);
                RefreshPendingAttachmentsList();
            };

            pnl.Controls.Add(lblName);
            pnl.Controls.Add(btnRemove);
            return pnl;
        }
    }
}
