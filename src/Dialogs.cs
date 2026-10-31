using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WinFontReplacer
{
    internal enum DialogKind
    {
        Info,
        Success,
        Warning,
        Error
    }

    internal sealed class MessageDialog : Form
    {
        private readonly string _title;
        private readonly string _message;
        private readonly string _primaryText;
        private readonly string _secondaryText;
        private readonly DialogKind _kind;

        private MessageDialog(
            string title,
            string message,
            string primaryText,
            string secondaryText,
            DialogKind kind)
        {
            _title = title;
            _message = message;
            _primaryText = primaryText;
            _secondaryText = secondaryText;
            _kind = kind;

            BuildInterface();
        }

        public static bool ShowDialog(
            IWin32Window owner,
            DialogKind kind,
            string title,
            string message,
            string primaryText,
            string secondaryText)
        {
            using (MessageDialog dialog = new MessageDialog(
                title,
                message,
                primaryText,
                secondaryText,
                kind))
            {
                return dialog.ShowDialog(owner) == DialogResult.OK;
            }
        }

        private void BuildInterface()
        {
            Text = "Windows 全局字体替换器";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            ClientSize = new Size(500, 350);
            Font = Theme.Font(9f, FontStyle.Regular);
            Icon = Program.AppIcon;

            IconPanel iconPanel = new IconPanel();
            iconPanel.Kind = _kind;
            iconPanel.Location = new Point(26, 26);
            iconPanel.Size = new Size(46, 46);
            Controls.Add(iconPanel);

            Label title = new Label();
            title.Text = _title;
            title.Font = Theme.Font(12.5f, FontStyle.Bold);
            title.ForeColor = Theme.Text;
            title.AutoSize = false;
            title.Location = new Point(90, 25);
            title.Size = new Size(384, 30);
            title.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(title);

            Label message = new Label();
            message.Text = _message;
            message.Font = Theme.Font(9f, FontStyle.Regular);
            message.ForeColor = Theme.TextSoft;
            message.AutoSize = false;
            message.Location = new Point(90, 63);
            message.Size = new Size(384, 194);
            message.TextAlign = ContentAlignment.TopLeft;
            message.UseCompatibleTextRendering = false;
            Controls.Add(message);

            int primaryWidth = string.IsNullOrEmpty(_secondaryText) ? 120 : 112;
            int primaryX = ClientSize.Width - 24 - primaryWidth;

            if (!string.IsNullOrEmpty(_secondaryText))
            {
                AccentButton secondary = new AccentButton();
                secondary.Kind = ButtonKind.Secondary;
                secondary.Text = _secondaryText;
                secondary.Location = new Point(primaryX - 112, 286);
                secondary.Size = new Size(102, 40);
                secondary.DialogResult = DialogResult.Cancel;
                Controls.Add(secondary);
                CancelButton = secondary;
            }

            AccentButton primary = new AccentButton();
            primary.Kind = ButtonKind.Primary;
            primary.Text = _primaryText;
            primary.Location = new Point(primaryX, 286);
            primary.Size = new Size(primaryWidth, 40);
            primary.DialogResult = DialogResult.OK;
            Controls.Add(primary);
            AcceptButton = primary;

            Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen divider = new Pen(Theme.Border))
                {
                    e.Graphics.DrawLine(divider, 24, 270, ClientSize.Width - 24, 270);
                }
            };
        }

        private sealed class IconPanel : Panel
        {
            public DialogKind Kind { get; set; }

            public IconPanel()
            {
                DoubleBuffered = true;
                BackColor = Color.White;
                SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.UserPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw,
                    true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.White);

                Color fill;
                Color ring;
                Color mark;
                switch (Kind)
                {
                    case DialogKind.Success:
                        fill = Color.FromArgb(220, 252, 231);
                        ring = Color.FromArgb(134, 239, 172);
                        mark = Color.FromArgb(5, 150, 105);
                        break;
                    case DialogKind.Warning:
                        fill = Color.FromArgb(254, 243, 199);
                        ring = Color.FromArgb(252, 211, 77);
                        mark = Color.FromArgb(180, 83, 9);
                        break;
                    case DialogKind.Error:
                        fill = Color.FromArgb(254, 226, 226);
                        ring = Color.FromArgb(252, 165, 165);
                        mark = Color.FromArgb(220, 38, 38);
                        break;
                    default:
                        fill = Color.FromArgb(219, 234, 254);
                        ring = Color.FromArgb(147, 197, 253);
                        mark = Color.FromArgb(37, 99, 235);
                        break;
                }

                Rectangle circle = new Rectangle(1, 1, Width - 3, Height - 3);
                using (SolidBrush fillBrush = new SolidBrush(fill))
                {
                    g.FillEllipse(fillBrush, circle);
                }
                using (Pen ringPen = new Pen(ring, 1.5f))
                {
                    g.DrawEllipse(ringPen, circle);
                }

                if (Kind == DialogKind.Success)
                {
                    using (Pen check = new Pen(mark, 3.2f))
                    {
                        check.StartCap = LineCap.Round;
                        check.EndCap = LineCap.Round;
                        g.DrawLines(check, new PointF[]
                        {
                            new PointF(12f, 23f),
                            new PointF(19f, 30f),
                            new PointF(33f, 15f)
                        });
                    }
                }
                else if (Kind == DialogKind.Error)
                {
                    using (Pen cross = new Pen(mark, 3.2f))
                    {
                        cross.StartCap = LineCap.Round;
                        cross.EndCap = LineCap.Round;
                        g.DrawLine(cross, 14, 14, 31, 31);
                        g.DrawLine(cross, 31, 14, 14, 31);
                    }
                }
                else
                {
                    string symbol = Kind == DialogKind.Warning ? "!" : "i";
                    TextRenderer.DrawText(
                        g,
                        symbol,
                        Theme.Font(20f, FontStyle.Bold),
                        new Rectangle(0, 0, Width, Height),
                        mark,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
            }
        }
    }
}
