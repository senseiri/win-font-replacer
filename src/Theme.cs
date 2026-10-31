using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WinFontReplacer
{
    internal static class Theme
    {
        public static readonly Color Background = Color.FromArgb(245, 247, 251);
        public static readonly Color Card = Color.White;
        public static readonly Color Border = Color.FromArgb(226, 232, 240);
        public static readonly Color BorderStrong = Color.FromArgb(203, 213, 225);
        public static readonly Color Text = Color.FromArgb(15, 23, 42);
        public static readonly Color TextSoft = Color.FromArgb(51, 65, 85);
        public static readonly Color Muted = Color.FromArgb(100, 116, 139);
        public static readonly Color MutedLight = Color.FromArgb(148, 163, 184);
        public static readonly Color Accent = Color.FromArgb(37, 99, 235);
        public static readonly Color AccentHover = Color.FromArgb(29, 78, 216);
        public static readonly Color AccentPressed = Color.FromArgb(30, 64, 175);
        public static readonly Color AccentSoft = Color.FromArgb(239, 246, 255);
        public static readonly Color Success = Color.FromArgb(5, 150, 105);
        public static readonly Color SuccessSoft = Color.FromArgb(236, 253, 245);
        public static readonly Color Warning = Color.FromArgb(217, 119, 6);
        public static readonly Color Danger = Color.FromArgb(220, 38, 38);
        public static readonly Color SidebarTop = Color.FromArgb(24, 33, 49);
        public static readonly Color SidebarBottom = Color.FromArgb(12, 18, 29);

        private static readonly Dictionary<string, Font> FontCache = new Dictionary<string, Font>();

        public static Font Font(float size, FontStyle style)
        {
            string key = size.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "|" + (int)style;
            lock (FontCache)
            {
                Font cached;
                if (FontCache.TryGetValue(key, out cached))
                {
                    return cached;
                }

                Font created = new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Point);
                FontCache[key] = created;
                return created;
            }
        }

        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return path;
            }

            int d = Math.Max(1, radius * 2);
            if (d > bounds.Width)
            {
                d = bounds.Width;
            }
            if (d > bounds.Height)
            {
                d = bounds.Height;
            }

            Rectangle arc = new Rectangle(bounds.X, bounds.Y, d, d);
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - d;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - d;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.X;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static string Ellipsis(string text, Font font, int width)
        {
            if (string.IsNullOrEmpty(text) || width <= 0)
            {
                return text;
            }

            if (TextRenderer.MeasureText(text, font).Width <= width)
            {
                return text;
            }

            string value = text;
            while (value.Length > 1 && TextRenderer.MeasureText(value + "…", font).Width > width)
            {
                value = value.Substring(0, value.Length - 1);
            }
            return value + "…";
        }
    }

    internal sealed class CardPanel : Panel
    {
        public int CornerRadius { get; set; }
        public Color CardColor { get; set; }
        public Color BorderColor { get; set; }

        public CardPanel()
        {
            CornerRadius = 12;
            CardColor = Theme.Card;
            BorderColor = Theme.Border;
            BackColor = Theme.Card;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color parentColor = Parent != null ? Parent.BackColor : Theme.Background;
            using (SolidBrush background = new SolidBrush(parentColor))
            {
                e.Graphics.FillRectangle(background, ClientRectangle);
            }

            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.RoundedRect(bounds, CornerRadius))
            {
                using (SolidBrush fill = new SolidBrush(CardColor))
                {
                    e.Graphics.FillPath(fill, path);
                }
                using (Pen border = new Pen(BorderColor))
                {
                    e.Graphics.DrawPath(border, path);
                }
            }

            base.OnPaint(e);
        }
    }

    internal enum ButtonKind
    {
        Primary,
        Secondary
    }

    internal sealed class AccentButton : Button
    {
        private bool _hover;
        private bool _pressed;

        public int CornerRadius { get; set; }
        public ButtonKind Kind { get; set; }

        public AccentButton()
        {
            CornerRadius = 9;
            Kind = ButtonKind.Primary;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Font = Theme.Font(9.5f, FontStyle.Bold);
            Cursor = Cursors.Hand;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            _pressed = true;
            Invalidate();
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color parentColor = Parent != null ? Parent.BackColor : Theme.Background;
            using (SolidBrush background = new SolidBrush(parentColor))
            {
                g.FillRectangle(background, ClientRectangle);
            }

            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fillColor;
            Color textColor;
            Color borderColor = Color.Transparent;

            if (!Enabled)
            {
                fillColor = Kind == ButtonKind.Primary ? Color.FromArgb(203, 213, 225) : Color.White;
                textColor = Kind == ButtonKind.Primary ? Color.White : Theme.MutedLight;
                borderColor = Kind == ButtonKind.Primary ? Color.Transparent : Theme.Border;
            }
            else if (Kind == ButtonKind.Primary)
            {
                fillColor = _pressed ? Theme.AccentPressed : (_hover ? Theme.AccentHover : Theme.Accent);
                textColor = Color.White;
            }
            else
            {
                fillColor = _pressed ? Color.FromArgb(241, 245, 249) : (_hover ? Color.FromArgb(248, 250, 252) : Color.White);
                textColor = Theme.TextSoft;
                borderColor = _hover ? Theme.BorderStrong : Theme.Border;
            }

            using (GraphicsPath path = Theme.RoundedRect(bounds, CornerRadius))
            {
                using (SolidBrush fill = new SolidBrush(fillColor))
                {
                    g.FillPath(fill, path);
                }
                if (borderColor != Color.Transparent)
                {
                    using (Pen border = new Pen(borderColor))
                    {
                        g.DrawPath(border, path);
                    }
                }
            }

            if (Focused && Enabled)
            {
                Rectangle focus = new Rectangle(3, 3, Width - 7, Height - 7);
                using (GraphicsPath path = Theme.RoundedRect(focus, Math.Max(2, CornerRadius - 2)))
                using (Pen pen = new Pen(Color.FromArgb(100, 255, 255, 255)))
                {
                    pen.DashStyle = DashStyle.Dot;
                    g.DrawPath(pen, path);
                }
            }

            TextFormatFlags flags =
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine |
                TextFormatFlags.NoPrefix |
                TextFormatFlags.EndEllipsis;

            Rectangle textBounds = new Rectangle(8, 0, Width - 16, Height);
            TextRenderer.DrawText(g, Text, Font, textBounds, textColor, flags);
        }
    }

    internal sealed class SearchBox : Panel
    {
        private readonly TextBox _input;
        private bool _focused;

        public TextBox Input
        {
            get { return _input; }
        }

        public string CueText { get; set; }

        public SearchBox()
        {
            CueText = "搜索字体";
            BackColor = Theme.Card;
            Height = 38;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            _input = new TextBox();
            _input.BorderStyle = BorderStyle.None;
            _input.Font = Theme.Font(9.5f, FontStyle.Regular);
            _input.ForeColor = Theme.Text;
            _input.BackColor = Theme.Card;
            _input.Location = new Point(35, 9);
            _input.Width = Width - 48;
            _input.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            _input.Enter += delegate { _focused = true; Invalidate(); };
            _input.Leave += delegate { _focused = false; Invalidate(); };
            _input.HandleCreated += delegate
            {
                NativeMethods.SendMessage(_input.Handle, NativeMethods.EM_SETCUEBANNER, new IntPtr(1), CueText);
            };
            Controls.Add(_input);
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            if (_input != null)
            {
                _input.Location = new Point(35, Math.Max(6, (Height - _input.Height) / 2));
                _input.Width = Math.Max(20, Width - 48);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color parentColor = Parent != null ? Parent.BackColor : Theme.Card;
            using (SolidBrush background = new SolidBrush(parentColor))
            {
                e.Graphics.FillRectangle(background, ClientRectangle);
            }

            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.RoundedRect(bounds, 8))
            {
                using (SolidBrush fill = new SolidBrush(Theme.Card))
                {
                    e.Graphics.FillPath(fill, path);
                }
                Color border = _focused ? Theme.Accent : Theme.Border;
                using (Pen pen = new Pen(border))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            }

            using (Pen pen = new Pen(_focused ? Theme.Accent : Theme.Muted, 1.6f))
            {
                e.Graphics.DrawEllipse(pen, 13, 12, 8, 8);
                e.Graphics.DrawLine(pen, 20, 19, 24, 23);
            }
        }
    }
}
