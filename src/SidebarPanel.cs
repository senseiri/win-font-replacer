using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WinFontReplacer
{
    internal sealed class SidebarPanel : Panel
    {
        private bool _isAdmin;
        private bool _isApplied;
        private bool _hasBaseline;
        private string _appliedFont;
        private string _lastAction;

        public bool IsAdmin
        {
            get { return _isAdmin; }
            set { _isAdmin = value; Invalidate(); }
        }

        public bool IsApplied
        {
            get { return _isApplied; }
            set { _isApplied = value; Invalidate(); }
        }

        public bool HasBaseline
        {
            get { return _hasBaseline; }
            set { _hasBaseline = value; Invalidate(); }
        }

        public string AppliedFont
        {
            get { return _appliedFont; }
            set { _appliedFont = value; Invalidate(); }
        }

        public string LastAction
        {
            get { return _lastAction; }
            set { _lastAction = value; Invalidate(); }
        }

        public SidebarPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Theme.SidebarTop;
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
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (LinearGradientBrush background = new LinearGradientBrush(
                ClientRectangle,
                Theme.SidebarTop,
                Theme.SidebarBottom,
                90f))
            {
                g.FillRectangle(background, ClientRectangle);
            }

            DrawDecorations(g);
            DrawBrand(g);
            DrawSteps(g);
            DrawStatus(g);
            DrawFooter(g);
        }

        private void DrawDecorations(Graphics g)
        {
            using (Pen pen = new Pen(Color.FromArgb(13, 255, 255, 255), 1f))
            {
                g.DrawEllipse(pen, Width - 92, -76, 190, 190);
                g.DrawEllipse(pen, Width - 54, -38, 114, 114);
                g.DrawEllipse(pen, -80, Height - 118, 190, 190);
            }
        }

        private void DrawBrand(Graphics g)
        {
            Rectangle logo = new Rectangle(22, 27, 44, 44);
            using (GraphicsPath path = Theme.RoundedRect(logo, 12))
            {
                using (LinearGradientBrush fill = new LinearGradientBrush(
                    logo,
                    Color.FromArgb(96, 165, 250),
                    Color.FromArgb(37, 99, 235),
                    45f))
                {
                    g.FillPath(fill, path);
                }
                using (Pen shine = new Pen(Color.FromArgb(70, 255, 255, 255)))
                {
                    g.DrawPath(shine, path);
                }
            }

            TextRenderer.DrawText(
                g,
                "Aa",
                Theme.Font(16f, FontStyle.Bold),
                new Rectangle(22, 36, 44, 27),
                Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            TextRenderer.DrawText(
                g,
                "字体替换器",
                Theme.Font(13.5f, FontStyle.Bold),
                new Point(78, 31),
                Color.White,
                TextFormatFlags.NoPrefix);

            TextRenderer.DrawText(
                g,
                "GLOBAL FONT TOOL",
                Theme.Font(6.5f, FontStyle.Bold),
                new Point(79, 56),
                Color.FromArgb(125, 148, 177),
                TextFormatFlags.NoPrefix);
        }

        private void DrawSteps(Graphics g)
        {
            TextRenderer.DrawText(
                g,
                "工作流程",
                Theme.Font(7.5f, FontStyle.Bold),
                new Point(24, 106),
                Color.FromArgb(116, 139, 170),
                TextFormatFlags.NoPrefix);

            DrawStep(g, 0, 137, "选择字体", "从已安装字体中挑选");
            DrawStep(g, 1, 192, "应用并注销", "写入映射与传统窗口字体");
            DrawStep(g, 2, 247, "随时一键恢复", "回到首次使用前的状态");
        }

        private void DrawStep(Graphics g, int index, int y, string title, string subtitle)
        {
            bool active = index == 0;
            Rectangle circle = new Rectangle(24, y, 23, 23);

            using (SolidBrush fill = new SolidBrush(active ? Theme.Accent : Color.FromArgb(36, 47, 65)))
            {
                g.FillEllipse(fill, circle);
            }
            if (!active)
            {
                using (Pen border = new Pen(Color.FromArgb(71, 85, 105)))
                {
                    g.DrawEllipse(border, circle);
                }
            }

            TextRenderer.DrawText(
                g,
                (index + 1).ToString(),
                Theme.Font(8f, FontStyle.Bold),
                circle,
                active ? Color.White : Color.FromArgb(148, 163, 184),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            if (index < 2)
            {
                using (Pen line = new Pen(Color.FromArgb(45, 58, 78)))
                {
                    g.DrawLine(line, 35, y + 24, 35, y + 47);
                }
            }

            TextRenderer.DrawText(
                g,
                title,
                Theme.Font(9.2f, FontStyle.Bold),
                new Point(58, y),
                active ? Color.White : Color.FromArgb(203, 213, 225),
                TextFormatFlags.NoPrefix);

            TextRenderer.DrawText(
                g,
                subtitle,
                Theme.Font(7.4f, FontStyle.Regular),
                new Point(58, y + 19),
                Color.FromArgb(118, 139, 168),
                TextFormatFlags.NoPrefix);
        }

        private void DrawStatus(Graphics g)
        {
            Rectangle card = new Rectangle(18, 378, Width - 36, 134);
            using (GraphicsPath path = Theme.RoundedRect(card, 12))
            {
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(24, 255, 255, 255)))
                {
                    g.FillPath(fill, path);
                }
                using (Pen border = new Pen(Color.FromArgb(30, 255, 255, 255)))
                {
                    g.DrawPath(border, path);
                }
            }

            TextRenderer.DrawText(
                g,
                "当前状态",
                Theme.Font(7.5f, FontStyle.Bold),
                new Point(34, 392),
                Color.FromArgb(130, 151, 180),
                TextFormatFlags.NoPrefix);

            Color dotColor;
            string headline;
            if (_isApplied)
            {
                dotColor = Theme.Success;
                headline = "已应用全局字体";
            }
            else if (_hasBaseline)
            {
                dotColor = Color.FromArgb(148, 163, 184);
                headline = "系统原始状态";
            }
            else
            {
                dotColor = Theme.Warning;
                headline = "尚未建立备份";
            }

            using (SolidBrush dot = new SolidBrush(dotColor))
            {
                g.FillEllipse(dot, 35, 419, 8, 8);
            }

            TextRenderer.DrawText(
                g,
                headline,
                Theme.Font(9.2f, FontStyle.Bold),
                new Point(50, 412),
                Color.White,
                TextFormatFlags.NoPrefix);

            string detail;
            if (_isApplied && !string.IsNullOrEmpty(_appliedFont))
            {
                detail = "目标：" + _appliedFont;
            }
            else if (!_hasBaseline)
            {
                detail = "首次应用时会自动备份";
            }
            else
            {
                detail = "原始字体设置已备份";
            }

            detail = Theme.Ellipsis(detail, Theme.Font(7.6f, FontStyle.Regular), Width - 68);

            TextRenderer.DrawText(
                g,
                detail,
                Theme.Font(7.6f, FontStyle.Regular),
                new Point(34, 440),
                Color.FromArgb(165, 183, 207),
                TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

            string backupLine = _hasBaseline ? "●  一键恢复可用" : "○  等待首次备份";
            TextRenderer.DrawText(
                g,
                backupLine,
                Theme.Font(7.4f, FontStyle.Regular),
                new Point(34, 468),
                _hasBaseline ? Color.FromArgb(110, 231, 183) : Color.FromArgb(130, 151, 180),
                TextFormatFlags.NoPrefix);
        }

        private void DrawFooter(Graphics g)
        {
            using (Pen divider = new Pen(Color.FromArgb(34, 255, 255, 255)))
            {
                g.DrawLine(divider, 24, 535, Width - 24, 535);
            }

            TextRenderer.DrawText(
                g,
                "安全可逆 · 不修改系统字体文件",
                Theme.Font(7.3f, FontStyle.Regular),
                new Point(24, 550),
                Color.FromArgb(116, 139, 170),
                TextFormatFlags.NoPrefix);

            string admin = _isAdmin ? "管理员权限已启用" : "标准权限 · 应用时请求 UAC";
            TextRenderer.DrawText(
                g,
                admin,
                Theme.Font(7.3f, FontStyle.Regular),
                new Point(24, 569),
                Color.FromArgb(116, 139, 170),
                TextFormatFlags.NoPrefix);

            TextRenderer.DrawText(
                g,
                "v1.0.3",
                Theme.Font(7.2f, FontStyle.Regular),
                new Point(24, 590),
                Color.FromArgb(82, 101, 128),
                TextFormatFlags.NoPrefix);

            TextRenderer.DrawText(
                g,
                "WinFontReplacer",
                Theme.Font(7.2f, FontStyle.Bold),
                new Point(78, 590),
                Color.FromArgb(82, 101, 128),
                TextFormatFlags.NoPrefix);
        }
    }
}
