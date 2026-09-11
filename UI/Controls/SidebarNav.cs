using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace KerkenezTicket.UI.Controls
{
    public class SidebarNav : Panel
    {
        public const int ExpandedWidth = 168;
        public const int CollapsedWidth = 60;

        public event EventHandler<int>? TabChanged;
        public event EventHandler<bool>? CollapsedChanged;
        public event EventHandler? LiveLogsClicked;

        private readonly string[] _tabTitles = new[]
        {
            "Tickets",
            "New Ticket",
            "Apps & Types",
            "Settings",
            "Live Logs"
        };

        private readonly string[] _tabIcons = new[]
        {
            "\uE8A5", // Tickets / tasks
            "\uE710", // Plus / New
            "\uE71D", // Apps / categories
            "\uE713", // Settings gear
            "\uE753"  // Cloud icon (matching KerkenezMail)
        };

        private static string? _iconFontFamily;
        private static string GetIconFontFamily()
        {
            if (_iconFontFamily != null) return _iconFontFamily;

            try
            {
                using var installedFonts = new System.Drawing.Text.InstalledFontCollection();
                var set = new System.Collections.Generic.HashSet<string>(installedFonts.Families.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
                if (set.Contains("Segoe Fluent Icons")) return _iconFontFamily = "Segoe Fluent Icons";
                if (set.Contains("Segoe MDL2 Assets")) return _iconFontFamily = "Segoe MDL2 Assets";
            }
            catch { }

            return _iconFontFamily = "Segoe UI Symbol";
        }

        private int _selectedIndex = 0;
        private int _hoveredIndex = -1;
        private bool _isToggleHovered = false;
        private bool _isCollapsed = false;

        private readonly System.Windows.Forms.Timer _animTimer;
        private int _startWidth;
        private int _targetWidth;
        private int _animFrame = 0;
        private const int TotalAnimFrames = 6;

        private readonly ToolTip _toolTip;
        private string _currentToolTipText = "";

        private readonly Color _bgColor = Color.FromArgb(240, 242, 245);
        private readonly Color _activeBgColor = Color.FromArgb(255, 255, 255);
        private readonly Color _hoverBgColor = Color.FromArgb(230, 233, 238);
        private readonly Color _textColor = Color.FromArgb(50, 54, 62);
        private readonly Color _activeTextColor = Color.FromArgb(0, 102, 204);
        private readonly Color _borderColor = Color.FromArgb(218, 222, 228);

        private float CurrentScale => (this.DeviceDpi > 0 ? this.DeviceDpi : 96f) / 96f;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_selectedIndex != value && (value == -1 || (value >= 0 && value < _tabTitles.Length)))
                {
                    _selectedIndex = value;
                    Invalidate();
                    if (_selectedIndex >= 0)
                    {
                        TabChanged?.Invoke(this, _selectedIndex);
                        if (_selectedIndex == 4)
                        {
                            LiveLogsClicked?.Invoke(this, EventArgs.Empty);
                        }
                    }
                }
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsCollapsed
        {
            get => _isCollapsed;
            set
            {
                if (_isCollapsed != value)
                {
                    _isCollapsed = value;
                    StartCollapseAnimation();
                    CollapsedChanged?.Invoke(this, _isCollapsed);
                }
            }
        }

        public SidebarNav()
        {
            this.Dock = DockStyle.Left;
            float scale = CurrentScale;
            this.Width = _isCollapsed ? (int)(CollapsedWidth * scale) : (int)(ExpandedWidth * scale);
            this.BackColor = _bgColor;
            this.DoubleBuffered = true;

            _toolTip = new ToolTip
            {
                InitialDelay = 250,
                ReshowDelay = 100,
                AutoPopDelay = 3000
            };

            _animTimer = new System.Windows.Forms.Timer { Interval = 15 };
            _animTimer.Tick += OnAnimTick;

            this.MouseMove += OnMouseMoveHandler;
            this.MouseLeave += OnMouseLeaveHandler;
            this.MouseClick += OnMouseClickHandler;
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            float scale = CurrentScale;
            this.Width = _isCollapsed ? (int)(CollapsedWidth * scale) : (int)(ExpandedWidth * scale);
            Invalidate();
        }

        private void StartCollapseAnimation()
        {
            float scale = CurrentScale;
            _animTimer.Stop();
            _startWidth = this.Width;
            _targetWidth = _isCollapsed ? (int)(CollapsedWidth * scale) : (int)(ExpandedWidth * scale);
            _animFrame = 0;
            _animTimer.Start();
        }

        private void OnAnimTick(object? sender, EventArgs e)
        {
            _animFrame++;
            float progress = (float)_animFrame / TotalAnimFrames;
            progress = (float)Math.Sin(progress * Math.PI / 2);

            this.Width = (int)(_startWidth + (_targetWidth - _startWidth) * progress);

            if (_animFrame >= TotalAnimFrames)
            {
                _animTimer.Stop();
                this.Width = _targetWidth;
            }
            Invalidate();
        }

        private Rectangle GetToggleRect()
        {
            float scale = CurrentScale;
            int sz = (int)(28 * scale);
            int headerH = (int)(56 * scale);
            bool isWide = !_isCollapsed && this.Width >= (int)(110 * scale);
            if (isWide)
            {
                return new Rectangle(this.Width - sz - (int)(10 * scale), (headerH - sz) / 2, sz, sz);
            }
            else
            {
                return new Rectangle((this.Width - sz) / 2, (headerH - sz) / 2, sz, sz);
            }
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            Invalidate();
        }

        private Rectangle GetTabRect(int index)
        {
            float scale = CurrentScale;
            int itemHeight = (int)(44 * scale);
            int marginX = _isCollapsed ? (int)(6 * scale) : (int)(10 * scale);
            int width = this.Width - (marginX * 2);

            if (index == 4) // Live Logs - sits at the bottom of the left menu
            {
                int marginY = (int)(12 * scale);
                int y = this.Height - itemHeight - marginY;
                int topLimit = (int)(62 * scale) + (4 * (itemHeight + (int)(4 * scale))) + (int)(16 * scale);
                if (y < topLimit) y = topLimit;
                return new Rectangle(marginX, y, width, itemHeight);
            }

            int topOffset = (int)(62 * scale);
            return new Rectangle(marginX, topOffset + (index * (itemHeight + (int)(4 * scale))), width, itemHeight);
        }

        private void OnMouseMoveHandler(object? sender, MouseEventArgs e)
        {
            var toggleRect = GetToggleRect();
            bool toggleHover = toggleRect.Contains(e.Location);
            if (toggleHover != _isToggleHovered)
            {
                _isToggleHovered = toggleHover;
                Invalidate();
            }

            int hovered = -1;
            for (int i = 0; i < _tabTitles.Length; i++)
            {
                if (GetTabRect(i).Contains(e.Location))
                {
                    hovered = i;
                    break;
                }
            }

            if (hovered != _hoveredIndex)
            {
                _hoveredIndex = hovered;
                Invalidate();

                if (_isCollapsed && _hoveredIndex >= 0)
                {
                    string text = _tabTitles[_hoveredIndex];
                    if (_currentToolTipText != text)
                    {
                        _currentToolTipText = text;
                        _toolTip.SetToolTip(this, text);
                    }
                }
                else
                {
                    _currentToolTipText = "";
                    _toolTip.RemoveAll();
                }
            }
        }

        private void OnMouseLeaveHandler(object? sender, EventArgs e)
        {
            _hoveredIndex = -1;
            _isToggleHovered = false;
            _currentToolTipText = "";
            _toolTip.RemoveAll();
            Invalidate();
        }

        private void OnMouseClickHandler(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            if (GetToggleRect().Contains(e.Location))
            {
                IsCollapsed = !IsCollapsed;
                return;
            }

            for (int i = 0; i < _tabTitles.Length; i++)
            {
                if (GetTabRect(i).Contains(e.Location))
                {
                    SelectedIndex = i;
                    return;
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // 1. Right Border
            using (var borderPen = new Pen(_borderColor))
            {
                g.DrawLine(borderPen, this.Width - 1, 0, this.Width - 1, this.Height);
            }

            // 2. Toggle Button (Fluent hamburger icon)
            var toggleRect = GetToggleRect();
            if (_isToggleHovered)
            {
                using var hoverBrush = new SolidBrush(Color.FromArgb(222, 226, 232));
                using var p = GetRoundedRect(toggleRect, 4);
                g.FillPath(hoverBrush, p);
            }

            string iconFamily = GetIconFontFamily();
            using (var iconFont = new Font(iconFamily, 11F, FontStyle.Regular))
            using (var iconBrush = new SolidBrush(_textColor))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString("\uE700", iconFont, iconBrush, toggleRect, sf);
            }

            float scale = CurrentScale;
            bool isWide = !_isCollapsed && this.Width >= (int)(110 * scale);

            // 2b. App Branding if expanded (styled exactly like KerkenezMail)
            if (isWide)
            {
                int textMaxWidth = toggleRect.Left - (int)(18 * scale);
                if (textMaxWidth > 20)
                {
                    using var titleFont = new Font("Segoe UI", 10.5F, FontStyle.Bold);
                    using var subFont = new Font("Segoe UI", 8F, FontStyle.Regular);
                    using var titleBrush = new SolidBrush(Color.FromArgb(25, 25, 25));
                    using var subBrush = new SolidBrush(Color.FromArgb(115, 120, 130));

                    var sfTitle = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                    var titleRect = new Rectangle((int)(14 * scale), (int)(10 * scale), textMaxWidth, (int)(20 * scale));
                    var subRect = new Rectangle((int)(14 * scale), (int)(31 * scale), textMaxWidth, (int)(16 * scale));

                    g.DrawString("Kerkenez", titleFont, titleBrush, titleRect, sfTitle);
                    g.DrawString("Ticket", subFont, subBrush, subRect, sfTitle);
                }
            }

            // 3. Separator line above bottom tab
            var bottomTabRect = GetTabRect(4);
            using (var sepPen = new Pen(_borderColor, 1))
            {
                int sepY = bottomTabRect.Top - (int)(8 * scale);
                int sepMargin = isWide ? (int)(12 * scale) : (int)(8 * scale);
                g.DrawLine(sepPen, sepMargin, sepY, this.Width - sepMargin, sepY);
            }

            // 4. Tab Items
            for (int i = 0; i < _tabTitles.Length; i++)
            {
                var rect = GetTabRect(i);
                bool isSelected = (i == _selectedIndex);
                bool isHovered = (i == _hoveredIndex);

                // Background
                if (isSelected)
                {
                    using var activeBrush = new SolidBrush(_activeBgColor);
                    using var path = GetRoundedRect(rect, 6);
                    g.FillPath(activeBrush, path);

                    // Left indicator pill
                    int indX = rect.X + 2;
                    int indW = 4;
                    int indH = rect.Height - 12;
                    int indY = rect.Y + 6;
                    using var indBrush = new SolidBrush(_activeTextColor);
                    using var indPath = GetRoundedRect(new Rectangle(indX, indY, indW, indH), 2);
                    g.FillPath(indBrush, indPath);
                }
                else if (isHovered)
                {
                    using var hBrush = new SolidBrush(_hoverBgColor);
                    using var path = GetRoundedRect(rect, 6);
                    g.FillPath(hBrush, path);
                }

                // Icon
                int iconSize = (int)(24 * scale);
                int iconX = _isCollapsed ? rect.X + (rect.Width - iconSize) / 2 : rect.X + (int)(14 * scale);
                int iconY = rect.Y + (rect.Height - iconSize) / 2;
                var iconRect = new Rectangle(iconX, iconY, iconSize, iconSize);

                using (var iconFont = new Font(iconFamily, 12F))
                using (var brush = new SolidBrush(isSelected ? _activeTextColor : _textColor))
                {
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    g.DrawString(_tabIcons[i], iconFont, brush, iconRect, sf);
                }

                // Text (if expanded)
                if (isWide)
                {
                    int textX = iconX + iconSize + (int)(10 * scale);
                    int textW = rect.Right - textX - (int)(8 * scale);
                    var textRect = new Rectangle(textX, rect.Y, textW, rect.Height);

                    using var textFont = new Font("Segoe UI", 9.5F, isSelected ? FontStyle.Bold : FontStyle.Regular);
                    using var textBrush = new SolidBrush(isSelected ? _activeTextColor : _textColor);
                    var textSf = new StringFormat
                    {
                        Alignment = StringAlignment.Near,
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter,
                        FormatFlags = StringFormatFlags.NoWrap
                    };
                    g.DrawString(_tabTitles[i], textFont, textBrush, textRect, textSf);
                }
            }
        }

        private static GraphicsPath GetRoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            var path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
