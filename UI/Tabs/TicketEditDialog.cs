using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using KerkenezTicket.Models;
using KerkenezTicket.Services;

namespace KerkenezTicket.UI.Tabs
{
    public class TicketEditDialog : Form
    {
        private class EditAttachmentItem
        {
            public TicketAttachment? ExistingAttachment { get; set; }
            public string FileName { get; set; } = "";
            public long FileSizeBytes { get; set; }
            public string? SourceFilePath { get; set; }
            public byte[]? RawBytes { get; set; }
            public bool IsNew => ExistingAttachment == null;

            public string FormattedSize
            {
                get
                {
                    if (FileSizeBytes < 1024) return $"{FileSizeBytes} B";
                    if (FileSizeBytes < 1024 * 1024) return $"{(FileSizeBytes / 1024.0):F1} KB";
                    return $"{(FileSizeBytes / (1024.0 * 1024.0)):F1} MB";
                }
            }
        }

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

        // Attachments
        private readonly List<EditAttachmentItem> _editAttachments = new List<EditAttachmentItem>();
        private readonly List<TicketAttachment> _removedExisting = new List<TicketAttachment>();
        private ListView _lvAttachments = null!;
        private Button _btnAddAttachment = null!;
        private Button _btnPasteScreenshot = null!;
        private Button _btnOpenAttachment = null!;
        private Button _btnRemoveAttachment = null!;

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
            this.Size = new Size(680, 780);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(248, 249, 250);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.AutoScroll = true;

            int pad = 20;
            int labelW = 105;
            int inputW = 505;
            int y = 18;
            int lineH = 36;

            // 1. Title
            var lblTitle = new Label { Text = "Title *", Location = new Point(pad, y + 4), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _txtTitle = new TextBox { Location = new Point(pad + labelW, y), Width = inputW, Height = 28, Font = new Font("Segoe UI", 9.5F) };
            this.Controls.Add(lblTitle);
            this.Controls.Add(_txtTitle);
            y += lineH;

            // 2. App & Type (side-by-side)
            var lblApp = new Label { Text = "App", Location = new Point(pad, y + 4), AutoSize = true };
            _cboApp = new ComboBox { Location = new Point(pad + labelW, y), Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
            var lblType = new Label { Text = "Type", Location = new Point(pad + labelW + 215, y + 4), AutoSize = true };
            _cboType = new ComboBox { Location = new Point(pad + labelW + 265, y), Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
            this.Controls.Add(lblApp);
            this.Controls.Add(_cboApp);
            this.Controls.Add(lblType);
            this.Controls.Add(_cboType);
            y += lineH;

            // 3. Priority & Status (side-by-side)
            var lblPriority = new Label { Text = "Priority", Location = new Point(pad, y + 4), AutoSize = true };
            _cboPriority = new ComboBox { Location = new Point(pad + labelW, y), Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
            var lblStatus = new Label { Text = "Status", Location = new Point(pad + labelW + 215, y + 4), AutoSize = true };
            _cboStatus = new ComboBox { Location = new Point(pad + labelW + 265, y), Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
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
                Height = 115,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };
            this.Controls.Add(lblDesc);
            this.Controls.Add(_txtDescription);
            y += 125;

            // 6. Notes / History
            var lblNotes = new Label { Text = "Work Notes", Location = new Point(pad, y + 4), AutoSize = true };
            _txtNotes = new TextBox
            {
                Location = new Point(pad + labelW, y),
                Width = inputW,
                Height = 80,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };
            this.Controls.Add(lblNotes);
            this.Controls.Add(_txtNotes);
            y += 90;

            // 7. Attachments
            var lblAttachments = new Label { Text = "Attachments", Location = new Point(pad, y + 4), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            this.Controls.Add(lblAttachments);

            var pnlAttHolder = new Panel
            {
                Location = new Point(pad + labelW, y),
                Width = inputW,
                Height = 160
            };

            var attToolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 32,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };

            _btnAddAttachment = new Button
            {
                Text = "📎 Add File(s)...",
                AutoSize = true,
                Height = 28,
                BackColor = Color.FromArgb(240, 243, 248),
                ForeColor = Color.FromArgb(40, 45, 55),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 6, 0)
            };
            _btnAddAttachment.FlatAppearance.BorderColor = Color.FromArgb(215, 222, 230);
            _btnAddAttachment.Click += (s, e) => ShowAddFileDialog();

            _btnPasteScreenshot = new Button
            {
                Text = "📋 Paste Screenshot",
                AutoSize = true,
                Height = 28,
                BackColor = Color.FromArgb(240, 243, 248),
                ForeColor = Color.FromArgb(40, 45, 55),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 6, 0)
            };
            _btnPasteScreenshot.FlatAppearance.BorderColor = Color.FromArgb(215, 222, 230);
            _btnPasteScreenshot.Click += (s, e) => PasteScreenshotFromClipboard();

            _btnOpenAttachment = new Button
            {
                Text = "🔍 Open",
                AutoSize = true,
                Height = 28,
                BackColor = Color.FromArgb(240, 243, 248),
                ForeColor = Color.FromArgb(40, 45, 55),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 6, 0)
            };
            _btnOpenAttachment.FlatAppearance.BorderColor = Color.FromArgb(215, 222, 230);
            _btnOpenAttachment.Click += (s, e) => OpenSelectedAttachment();

            _btnRemoveAttachment = new Button
            {
                Text = "🗑 Remove",
                AutoSize = true,
                Height = 28,
                BackColor = Color.FromArgb(240, 243, 248),
                ForeColor = Color.FromArgb(220, 53, 69),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 6, 0)
            };
            _btnRemoveAttachment.FlatAppearance.BorderColor = Color.FromArgb(215, 222, 230);
            _btnRemoveAttachment.Click += (s, e) => RemoveSelectedAttachment();

            attToolbar.Controls.Add(_btnAddAttachment);
            attToolbar.Controls.Add(_btnPasteScreenshot);
            attToolbar.Controls.Add(_btnOpenAttachment);
            attToolbar.Controls.Add(_btnRemoveAttachment);

            _lvAttachments = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 8.5F)
            };
            _lvAttachments.Columns.Add("File Name", 270);
            _lvAttachments.Columns.Add("Size", 85);
            _lvAttachments.Columns.Add("Status", 110);
            _lvAttachments.DoubleClick += (s, e) => OpenSelectedAttachment();

            // Drag and drop onto dialog / listview
            _lvAttachments.AllowDrop = true;
            _lvAttachments.DragEnter += OnDragEnter;
            _lvAttachments.DragDrop += OnDragDrop;
            this.AllowDrop = true;
            this.DragEnter += OnDragEnter;
            this.DragDrop += OnDragDrop;

            pnlAttHolder.Controls.Add(_lvAttachments);
            pnlAttHolder.Controls.Add(attToolbar);
            this.Controls.Add(pnlAttHolder);
            y += 175;

            // Bottom Buttons
            _btnSave = new Button
            {
                Text = "Save Changes",
                Location = new Point(pad + labelW + inputW - 230, y),
                Size = new Size(125, 34),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.Click += OnSaveClicked;

            _btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(pad + labelW + inputW - 95, y),
                Size = new Size(95, 34),
                BackColor = Color.FromArgb(235, 238, 242),
                ForeColor = Color.FromArgb(50, 50, 50),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
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

            // Attachments
            if (_dbService != null)
            {
                _ticket.Attachments = _dbService.GetAttachmentsForTicket(_ticket.Id);
            }

            _editAttachments.Clear();
            _removedExisting.Clear();

            if (_ticket.Attachments != null)
            {
                foreach (var att in _ticket.Attachments)
                {
                    _editAttachments.Add(new EditAttachmentItem
                    {
                        ExistingAttachment = att,
                        FileName = att.FileName,
                        FileSizeBytes = att.FileSizeBytes
                    });
                }
            }

            RefreshAttachmentsListView();
        }

        private void RefreshAttachmentsListView()
        {
            _lvAttachments.BeginUpdate();
            _lvAttachments.Items.Clear();

            foreach (var item in _editAttachments)
            {
                string icon = item.FileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                              item.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                              item.FileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ? "🖼️ " :
                              (item.FileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase) ||
                               item.FileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ? "📄 " : "📎 ");

                var lvi = new ListViewItem(icon + item.FileName);
                lvi.SubItems.Add(item.FormattedSize);
                lvi.SubItems.Add(item.IsNew ? "Newly Added" : "Existing");
                lvi.Tag = item;

                if (item.IsNew)
                {
                    lvi.ForeColor = Color.FromArgb(0, 102, 204);
                }

                _lvAttachments.Items.Add(lvi);
            }

            _lvAttachments.EndUpdate();
        }

        private void ShowAddFileDialog()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select Files to Attach",
                Multiselect = true,
                Filter = "All Files (*.*)|*.*|Logs & Text (*.log;*.txt;*.json;*.xml;*.csv)|*.log;*.txt;*.json;*.xml;*.csv|Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp"
            };

            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                foreach (var file in ofd.FileNames)
                {
                    AddAttachmentFile(file);
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

                        _editAttachments.Add(new EditAttachmentItem
                        {
                            FileName = name,
                            FileSizeBytes = bytes.Length,
                            RawBytes = bytes
                        });

                        RefreshAttachmentsListView();
                        return;
                    }
                }

                MessageBox.Show(this, "No image found in clipboard.\n\nUse Windows Snipping Tool (Win+Shift+S) or Copy an image first.", "No Image", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to paste screenshot: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddAttachmentFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                var fi = new FileInfo(path);

                if (_editAttachments.Any(a => string.Equals(a.SourceFilePath, path, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                _editAttachments.Add(new EditAttachmentItem
                {
                    FileName = fi.Name,
                    FileSizeBytes = fi.Length,
                    SourceFilePath = path
                });

                RefreshAttachmentsListView();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not attach file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenSelectedAttachment()
        {
            if (_lvAttachments.SelectedItems.Count == 0) return;
            var item = _lvAttachments.SelectedItems[0].Tag as EditAttachmentItem;
            if (item == null) return;

            try
            {
                if (item.ExistingAttachment != null)
                {
                    AttachmentStorageService.OpenAttachment(item.ExistingAttachment);
                }
                else if (!string.IsNullOrEmpty(item.SourceFilePath) && File.Exists(item.SourceFilePath))
                {
                    Process.Start(new ProcessStartInfo { FileName = item.SourceFilePath, UseShellExecute = true });
                }
                else if (item.RawBytes != null)
                {
                    string tempPath = Path.Combine(Path.GetTempPath(), item.FileName);
                    File.WriteAllBytes(tempPath, item.RawBytes);
                    Process.Start(new ProcessStartInfo { FileName = tempPath, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to open attachment: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RemoveSelectedAttachment()
        {
            if (_lvAttachments.SelectedItems.Count == 0) return;
            var item = _lvAttachments.SelectedItems[0].Tag as EditAttachmentItem;
            if (item == null) return;

            if (item.ExistingAttachment != null)
            {
                _removedExisting.Add(item.ExistingAttachment);
            }

            _editAttachments.Remove(item);
            RefreshAttachmentsListView();
        }

        private void OnDragEnter(object? sender, DragEventArgs e)
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

        private void OnDragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null)
                {
                    foreach (var file in files)
                    {
                        AddAttachmentFile(file);
                    }
                }
            }
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

            // 1. Delete removed attachments
            if (_dbService != null)
            {
                foreach (var removed in _removedExisting)
                {
                    _dbService.DeleteAttachment(removed.Id);
                }

                // 2. Save newly added attachments
                foreach (var item in _editAttachments.Where(a => a.IsNew))
                {
                    try
                    {
                        TicketAttachment? att = null;
                        if (item.RawBytes != null)
                        {
                            att = AttachmentStorageService.SaveImageBytes(_ticket.Id, item.RawBytes, item.FileName);
                        }
                        else if (!string.IsNullOrEmpty(item.SourceFilePath))
                        {
                            att = AttachmentStorageService.SaveAttachmentFile(_ticket.Id, item.SourceFilePath, item.FileName);
                        }

                        if (att != null)
                        {
                            _dbService.AddAttachment(att);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogService.Error("Attachments", $"Failed to save new attachment {item.FileName}: {ex.Message}");
                    }
                }

                // 3. Refresh ticket attachments from DB
                _ticket.Attachments = _dbService.GetAttachmentsForTicket(_ticket.Id);
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
