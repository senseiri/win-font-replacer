using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace WinFontReplacer
{
    internal sealed class PreviewPanel : Panel
    {
        private string _familyName;

        public string FamilyName
        {
            get { return _familyName; }
            set
            {
                _familyName = value;
                Invalidate();
            }
        }

        public PreviewPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
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
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            using (SolidBrush fill = new SolidBrush(Theme.Card))
            {
                g.FillRectangle(fill, ClientRectangle);
            }

            if (string.IsNullOrEmpty(_familyName))
            {
                TextRenderer.DrawText(
                    g,
                    "请选择一种字体",
                    Theme.Font(9f, FontStyle.Regular),
                    new Rectangle(2, 2, Width - 4, 50),
                    Theme.MutedLight,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                return;
            }

            DrawSamples(g);
        }

        private void DrawSamples(Graphics g)
        {
            Color primary = Theme.Text;
            Color secondary = Theme.TextSoft;
            Color muted = Theme.Muted;

            DrawLine(g, "永字八法", 28f, 0, primary, FontStyle.Regular, 28);
            DrawLine(g, "天地玄黄，宇宙洪荒", 17f, 44, secondary, FontStyle.Regular, 28);
            DrawLine(g, "Aa Bb Cc · 0123", 14.5f, 76, secondary, FontStyle.Regular, 28);
            DrawLine(g, "The quick brown fox jumps over the lazy dog.", 10.5f, 103, muted, FontStyle.Regular, 26);

            using (Pen line = new Pen(Theme.Border))
            {
                g.DrawLine(line, 0, 137, Width, 137);
            }

            DrawLine(g, "中文字形  /  Latin glyphs  /  1234567890", 8.6f, 148, Theme.MutedLight, FontStyle.Regular, 26);
            DrawLine(g, "字体渲染预览", 8.6f, 171, Theme.MutedLight, FontStyle.Regular, 26);
        }

        private void DrawLine(
            Graphics g,
            string text,
            float sizePx,
            int y,
            Color color,
            FontStyle style,
            int height)
        {
            try
            {
                using (Font font = new Font(_familyName, sizePx, style, GraphicsUnit.Pixel))
                using (SolidBrush brush = new SolidBrush(color))
                using (StringFormat format = new StringFormat(StringFormatFlags.NoWrap))
                {
                    format.Trimming = StringTrimming.EllipsisCharacter;
                    g.DrawString(text, font, brush, new RectangleF(0, y, Width, height), format);
                }
            }
            catch
            {
                TextRenderer.DrawText(
                    g,
                    text,
                    Theme.Font(9f, FontStyle.Regular),
                    new Rectangle(0, y, Width, height),
                    color,
                    TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }
        }
    }
}
