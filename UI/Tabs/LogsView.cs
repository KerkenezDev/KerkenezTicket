using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using KerkenezTicket.Services;

namespace KerkenezTicket.UI.Tabs
{
    public class LogsView : UserControl
    {
        private RichTextBox _rtbLog = null!;
        private Label _lblStatus = null!;
        private CheckBox _chkAutoScroll = null!;
        private Button _btnCopy = null!;
        private Button _btnClear = null!;
        private Button _btnExport = null!;

        public LogsView()
        {
            InitializeComponent();
            LoadExistingLogs();
            LogService.LogAdded += OnLogAdded;
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(248, 249, 250);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // 1. Top Panel
            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 54,
                BackColor = Color.White,
                Padding = new Padding(20, 10, 20, 10)
            };
            topPanel.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(222, 226, 230), 1);
                e.Graphics.DrawLine(p, 0, topPanel.Height - 1, topPanel.Width, topPanel.Height - 1);
            };

            var lblTitle = new Label
            {
                Text = "📋  Live Activity & System Logs",
                Font = new Font("Segoe UI", 13.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(24, 28, 36),
                AutoSize = true,
                Location = new Point(16, 6)
            };

            _lblStatus = new Label
            {
                Text = "● Live Stream",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(46, 160, 67),
                AutoSize = true,
                Location = new Point(275, 12)
            };

            var flowActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };

            _chkAutoScroll = new CheckBox
            {
                Text = "Auto-scroll",
                Checked = true,
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(70, 75, 85),
                Margin = new Padding(0, 7, 12, 0)
            };

            _btnCopy = new Button
            {
                Text = "📋  Copy All",
                AutoSize = true,
                Height = 28,
                Padding = new Padding(10, 0, 10, 0),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                BackColor = Color.FromArgb(245, 247, 250),
                ForeColor = Color.FromArgb(45, 55, 72),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 2, 6, 0)
            };
            _btnCopy.FlatAppearance.BorderColor = Color.FromArgb(215, 222, 230);
            _btnCopy.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(_rtbLog.Text))
                {
                    Clipboard.SetText(_rtbLog.Text);
                    MessageBox.Show(this, "Logs copied to clipboard.", "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            _btnExport = new Button
            {
                Text = "💾  Save Log File",
                AutoSize = true,
                Height = 28,
                Padding = new Padding(10, 0, 10, 0),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                BackColor = Color.FromArgb(245, 247, 250),
                ForeColor = Color.FromArgb(45, 55, 72),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 2, 6, 0)
            };
            _btnExport.FlatAppearance.BorderColor = Color.FromArgb(215, 222, 230);
            _btnExport.Click += (s, e) => ExportLogToFile();

            _btnClear = new Button
            {
                Text = "🧹  Clear",
                AutoSize = true,
                Height = 28,
                Padding = new Padding(10, 0, 10, 0),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F),
                BackColor = Color.FromArgb(245, 247, 250),
                ForeColor = Color.FromArgb(45, 55, 72),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 2, 0, 0)
            };
            _btnClear.FlatAppearance.BorderColor = Color.FromArgb(215, 222, 230);
            _btnClear.Click += (s, e) =>
            {
                LogService.Clear();
                _rtbLog.Clear();
            };

            flowActions.Controls.Add(_chkAutoScroll);
            flowActions.Controls.Add(_btnCopy);
            flowActions.Controls.Add(_btnExport);
            flowActions.Controls.Add(_btnClear);

            topPanel.Controls.Add(lblTitle);
            topPanel.Controls.Add(_lblStatus);
            topPanel.Controls.Add(flowActions);

            // 2. Terminal Log Box
            var pnlContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                BackColor = Color.FromArgb(240, 242, 245)
            };

            _rtbLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(24, 27, 33),
                ForeColor = Color.FromArgb(230, 237, 243),
                Font = new Font("Cascadia Code", 9F, FontStyle.Regular),
                BorderStyle = BorderStyle.None,
                WordWrap = false,
                DetectUrls = false
            };

            pnlContainer.Controls.Add(_rtbLog);

            this.Controls.Add(pnlContainer);
            this.Controls.Add(topPanel);
        }

        private void LoadExistingLogs()
        {
            var list = LogService.GetRecentLogs();
            _rtbLog.Clear();
            foreach (var item in list)
            {
                AppendEntry(item);
            }
        }

        private void OnLogAdded(LogEntry entry)
        {
            if (this.IsDisposed) return;

            if (this.InvokeRequired)
            {
                try
                {
                    this.BeginInvoke(new Action<LogEntry>(OnLogAdded), entry);
                }
                catch { }
                return;
            }

            AppendEntry(entry);
        }

        private void AppendEntry(LogEntry entry)
        {
            _rtbLog.SelectionStart = _rtbLog.TextLength;
            _rtbLog.SelectionLength = 0;

            // Timestamp [HH:mm:ss]
            _rtbLog.SelectionColor = Color.FromArgb(139, 148, 158);
            _rtbLog.AppendText($"[{entry.Timestamp:HH:mm:ss}] ");

            // Level [LEVEL]
            Color lvlColor = entry.Level switch
            {
                "SUCCESS" => Color.FromArgb(63, 185, 80),
                "WARN" => Color.FromArgb(210, 153, 34),
                "ERROR" => Color.FromArgb(248, 81, 73),
                _ => Color.FromArgb(88, 166, 255)
            };
            _rtbLog.SelectionColor = lvlColor;
            _rtbLog.AppendText($"[{entry.Level.PadRight(5)}] ");

            // Category [Category]
            _rtbLog.SelectionColor = Color.FromArgb(165, 214, 255);
            _rtbLog.AppendText($"[{entry.Category}] ");

            // Message
            _rtbLog.SelectionColor = Color.FromArgb(230, 237, 243);
            _rtbLog.AppendText($"{entry.Message}\r\n");

            if (_chkAutoScroll.Checked)
            {
                _rtbLog.SelectionStart = _rtbLog.TextLength;
                _rtbLog.ScrollToCaret();
            }
        }

        private void ExportLogToFile()
        {
            using var sfd = new SaveFileDialog
            {
                Title = "Save Application Log",
                Filter = "Log File (*.log)|*.log|Text File (*.txt)|*.txt|All Files (*.*)|*.*",
                FileName = $"kticket_log_{DateTime.Now:yyyyMMdd_HHmmss}.log"
            };

            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    File.WriteAllText(sfd.FileName, _rtbLog.Text, Encoding.UTF8);
                    MessageBox.Show(this, $"Log exported successfully:\n{sfd.FileName}", "Exported", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Failed to write log file:\n{ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
