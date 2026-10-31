using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace WinFontReplacer
{
    internal sealed class MainForm : Form
    {
        private readonly SidebarPanel _sidebar;
        private readonly SearchBox _search;
        private readonly ListBox _fontList;
        private readonly PreviewPanel _preview;
        private readonly Label _selectedName;
        private readonly Label _fileInfo;
        private readonly CheckBox _metricsCheck;
        private readonly CheckBox _fontLinkCheck;
        private readonly AccentButton _applyButton;
        private readonly AccentButton _restoreButton;
        private readonly Label _statusLabel;
        private readonly AdminBadge _adminBadge;
        private readonly ToolTip _toolTip;

        private List<FontListItem> _allFonts;
        private List<FontListItem> _visibleFonts;
        private string _selectedFamily;
        private int _hoverIndex;
        private bool _busy;

        public MainForm()
        {
            Text = "Windows 全局字体替换器";
            ClientSize = new Size(1000, 620);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(16, 12);
            BackColor = Theme.Background;
            Font = Theme.Font(9f, FontStyle.Regular);
            Icon = Program.AppIcon;
            DoubleBuffered = true;
            KeyPreview = true;

            _toolTip = new ToolTip();
            _toolTip.AutoPopDelay = 12000;
            _toolTip.InitialDelay = 500;
            _toolTip.ReshowDelay = 100;

            _sidebar = new SidebarPanel();
            _sidebar.Location = new Point(0, 0);
            _sidebar.Size = new Size(250, 620);
            _sidebar.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            Controls.Add(_sidebar);

            Label title = MakeLabel(
                "选择全局字体",
                Theme.Font(17f, FontStyle.Bold),
                Theme.Text,
                new Point(274, 19),
                new Size(440, 32));
            Controls.Add(title);

            Label subtitle = MakeLabel(
                "刷新标题栏、菜单、对话框等传统窗口字体，随时一键恢复",
                Theme.Font(8.7f, FontStyle.Regular),
                Theme.Muted,
                new Point(274, 52),
                new Size(540, 22));
            Controls.Add(subtitle);

            _adminBadge = new AdminBadge();
            _adminBadge.Location = new Point(872, 29);
            _adminBadge.Size = new Size(104, 24);
            _adminBadge.IsAdmin = Program.IsAdministrator();
            _toolTip.SetToolTip(
                _adminBadge,
                _adminBadge.IsAdmin
                    ? "当前以管理员权限运行，可直接应用和恢复。"
                    : "浏览字体不需要管理员权限；点击应用或恢复时才会弹出 UAC。");
            Controls.Add(_adminBadge);

            CardPanel fontCard = new CardPanel();
            fontCard.Location = new Point(274, 88);
            fontCard.Size = new Size(398, 320);
            Controls.Add(fontCard);

            _search = new SearchBox();
            _search.Location = new Point(12, 14);
            _search.Size = new Size(374, 38);
            _search.CueText = "搜索字体名称";
            _search.Input.TextChanged += delegate { ApplyFilter(); };
            fontCard.Controls.Add(_search);

            _fontList = new ListBox();
            _fontList.Location = new Point(10, 60);
            _fontList.Size = new Size(378, 246);
            _fontList.BorderStyle = BorderStyle.None;
            _fontList.BackColor = Theme.Card;
            _fontList.DrawMode = DrawMode.OwnerDrawFixed;
            _fontList.ItemHeight = 46;
            _fontList.IntegralHeight = false;
            _fontList.ScrollAlwaysVisible = false;
            _fontList.DrawItem += FontListDrawItem;
            _fontList.SelectedIndexChanged += FontListSelectedIndexChanged;
            _fontList.MouseMove += FontListMouseMove;
            _fontList.MouseLeave += FontListMouseLeave;
            fontCard.Controls.Add(_fontList);

            CardPanel previewCard = new CardPanel();
            previewCard.Location = new Point(688, 88);
            previewCard.Size = new Size(288, 320);
            Controls.Add(previewCard);

            Label previewTitle = MakeLabel(
                "实时预览",
                Theme.Font(9f, FontStyle.Bold),
                Theme.Text,
                new Point(16, 14),
                new Size(120, 20));
            previewTitle.BackColor = Theme.Card;
            previewCard.Controls.Add(previewTitle);

            _preview = new PreviewPanel();
            _preview.Location = new Point(16, 40);
            _preview.Size = new Size(256, 206);
            previewCard.Controls.Add(_preview);

            _selectedName = MakeLabel(
                "—",
                Theme.Font(10.5f, FontStyle.Bold),
                Theme.Text,
                new Point(16, 254),
                new Size(256, 24));
            _selectedName.AutoEllipsis = true;
            _selectedName.BackColor = Theme.Card;
            previewCard.Controls.Add(_selectedName);

            _fileInfo = MakeLabel(
                "字体文件：—",
                Theme.Font(7.7f, FontStyle.Regular),
                Theme.MutedLight,
                new Point(16, 279),
                new Size(256, 20));
            _fileInfo.AutoEllipsis = true;
            _fileInfo.BackColor = Theme.Card;
            previewCard.Controls.Add(_fileInfo);

            CardPanel optionsCard = new CardPanel();
            optionsCard.Location = new Point(274, 420);
            optionsCard.Size = new Size(702, 92);
            Controls.Add(optionsCard);

            Label optionsTitle = MakeLabel(
                "应用选项",
                Theme.Font(9f, FontStyle.Bold),
                Theme.Text,
                new Point(16, 12),
                new Size(100, 20));
            optionsTitle.BackColor = Theme.Card;
            optionsCard.Controls.Add(optionsTitle);

            _metricsCheck = new CheckBox();
            _metricsCheck.Text = "同时修改标题栏、菜单和对话框字体";
            _metricsCheck.Checked = true;
            _metricsCheck.Location = new Point(16, 36);
            _metricsCheck.Size = new Size(330, 24);
            _metricsCheck.Font = Theme.Font(8.7f, FontStyle.Regular);
            _metricsCheck.ForeColor = Theme.TextSoft;
            _metricsCheck.BackColor = Theme.Card;
            _metricsCheck.FlatStyle = FlatStyle.Flat;
            _toolTip.SetToolTip(
                _metricsCheck,
                "写入当前账户的 CaptionFont、MenuFont、MessageFont 等窗口字体设置。");
            optionsCard.Controls.Add(_metricsCheck);

            _fontLinkCheck = new CheckBox();
            _fontLinkCheck.Text = "启用字体链接，自动补齐缺失字形（推荐）";
            _fontLinkCheck.Checked = true;
            _fontLinkCheck.Location = new Point(16, 60);
            _fontLinkCheck.Size = new Size(390, 24);
            _fontLinkCheck.Font = Theme.Font(8.7f, FontStyle.Regular);
            _fontLinkCheck.ForeColor = Theme.TextSoft;
            _fontLinkCheck.BackColor = Theme.Card;
            _fontLinkCheck.FlatStyle = FlatStyle.Flat;
            _toolTip.SetToolTip(
                _fontLinkCheck,
                "当某些程序请求 Segoe UI 等字体时，让 Windows 优先使用目标字体补齐缺字。");
            optionsCard.Controls.Add(_fontLinkCheck);

            Label optionsHint = MakeLabel(
                "立即刷新传统窗口字体\n不替换系统字体文件",
                Theme.Font(7.7f, FontStyle.Regular),
                Theme.MutedLight,
                new Point(464, 40),
                new Size(218, 36));
            optionsHint.TextAlign = ContentAlignment.MiddleRight;
            optionsHint.BackColor = Theme.Card;
            optionsCard.Controls.Add(optionsHint);

            Panel divider = new Panel();
            divider.Location = new Point(274, 528);
            divider.Size = new Size(702, 1);
            divider.BackColor = Theme.Border;
            Controls.Add(divider);

            _statusLabel = MakeLabel(
                "正在检查字体备份…",
                Theme.Font(7.9f, FontStyle.Regular),
                Theme.Muted,
                new Point(274, 540),
                new Size(390, 24));
            _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(_statusLabel);

            _restoreButton = new AccentButton();
            _restoreButton.Kind = ButtonKind.Secondary;
            _restoreButton.Text = "一键恢复";
            _restoreButton.Location = new Point(676, 532);
            _restoreButton.Size = new Size(136, 42);
            _restoreButton.Click += delegate { RestoreClicked(); };
            _toolTip.SetToolTip(
                _restoreButton,
                "回到首次使用本工具前的字体设置。首次应用后自动生成备份。");
            Controls.Add(_restoreButton);

            _applyButton = new AccentButton();
            _applyButton.Kind = ButtonKind.Primary;
            _applyButton.Text = "应用全局字体";
            _applyButton.Location = new Point(824, 532);
            _applyButton.Size = new Size(152, 42);
            _applyButton.Click += delegate { ApplyClicked(); };
            _toolTip.SetToolTip(
                _applyButton,
                "通过字体映射和窗口字体设置生效，不修改系统字体文件。Windows 11 部分 DirectWrite 界面可能仍使用系统自带字体。");
            Controls.Add(_applyButton);

            Label footerNote = MakeLabel(
                "标题栏/菜单会立即刷新；其他程序建议重启或注销。",
                Theme.Font(7.6f, FontStyle.Regular),
                Theme.MutedLight,
                new Point(400, 585),
                new Size(576, 20));
            Controls.Add(footerNote);

            LinkLabel backupLink = new LinkLabel();
            backupLink.Text = "打开备份目录";
            backupLink.Font = Theme.Font(7.9f, FontStyle.Regular);
            backupLink.LinkColor = Theme.Accent;
            backupLink.ActiveLinkColor = Theme.AccentPressed;
            backupLink.BackColor = Theme.Background;
            backupLink.Location = new Point(274, 583);
            backupLink.Size = new Size(120, 20);
            backupLink.LinkClicked += delegate { OpenBackupFolder(); };
            Controls.Add(backupLink);

            Shown += delegate
            {
                LoadFonts();
                RefreshStatus();
            };
            FormClosing += MainFormClosing;
        }

        private void MainFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_busy)
            {
                e.Cancel = true;
            }
        }

        private static Label MakeLabel(string text, Font font, Color color, Point location, Size size)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = Theme.Background;
            label.Location = location;
            label.Size = size;
            label.AutoSize = false;
            label.UseCompatibleTextRendering = false;
            return label;
        }

        private void LoadFonts()
        {
            Cursor previous = Cursor;
            Cursor = Cursors.WaitCursor;
            try
            {
                _allFonts = FontCatalog.Load();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                _allFonts = new List<FontListItem>();
                _visibleFonts = new List<FontListItem>();
                MessageDialog.ShowDialog(
                    this,
                    DialogKind.Error,
                    "无法读取字体列表",
                    "读取已安装字体失败。" + ex.Message,
                    "知道了",
                    null);
            }
            finally
            {
                Cursor = previous;
            }
        }

        private void ApplyFilter()
        {
            string query = _search == null ? string.Empty : _search.Input.Text.Trim();
            List<FontListItem> filtered = new List<FontListItem>();
            if (_allFonts != null)
            {
                foreach (FontListItem item in _allFonts)
                {
                    if (query.Length == 0 ||
                        item.DisplayName.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                        item.FamilyName.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)
                    {
                        filtered.Add(item);
                    }
                }
            }

            _visibleFonts = filtered;
            _fontList.BeginUpdate();
            try
            {
                _fontList.Items.Clear();
                foreach (FontListItem item in filtered)
                {
                    _fontList.Items.Add(item);
                }
            }
            finally
            {
                _fontList.EndUpdate();
            }

            if (filtered.Count == 0)
            {
                _selectedFamily = null;
                _preview.FamilyName = null;
                _selectedName.Text = "没有匹配的字体";
                _fileInfo.Text = "字体文件：—";
                _applyButton.Enabled = false;
                return;
            }

            _applyButton.Enabled = !_busy;
            string wanted = _selectedFamily;
            int index = 0;
            if (!string.IsNullOrEmpty(wanted))
            {
                for (int i = 0; i < filtered.Count; i++)
                {
                    if (string.Equals(filtered[i].FamilyName, wanted, StringComparison.OrdinalIgnoreCase))
                    {
                        index = i;
                        break;
                    }
                }
            }
            else
            {
                index = PreferredInitialIndex(filtered);
            }

            _fontList.SelectedIndex = index;
            FontListSelectedIndexChanged(null, EventArgs.Empty);
        }

        private int PreferredInitialIndex(List<FontListItem> fonts)
        {
            string applied = BackupStore.CurrentSubstitution();
            if (!string.IsNullOrEmpty(applied))
            {
                for (int i = 0; i < fonts.Count; i++)
                {
                    if (string.Equals(fonts[i].FamilyName, applied, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }

            string[] preferred = new string[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI" };
            foreach (string name in preferred)
            {
                for (int i = 0; i < fonts.Count; i++)
                {
                    if (string.Equals(fonts[i].FamilyName, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }
            return 0;
        }

        private void FontListSelectedIndexChanged(object sender, EventArgs e)
        {
            FontListItem item = _fontList.SelectedItem as FontListItem;
            if (item == null)
            {
                return;
            }

            _selectedFamily = item.FamilyName;
            _preview.FamilyName = item.FamilyName;
            _selectedName.Text = item.DisplayName;
            _fileInfo.Text = "字体文件：正在读取…";

            string family = item.FamilyName;
            ThreadPool.QueueUserWorkItem(delegate
            {
                FontFileInfo info = null;
                try
                {
                    info = FontFileIndex.Find(family);
                }
                catch
                {
                }

                try
                {
                    if (IsDisposed)
                    {
                        return;
                    }
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (string.Equals(_selectedFamily, family, StringComparison.OrdinalIgnoreCase))
                        {
                            _fileInfo.Text = info == null
                                ? "字体文件：注册表未记录"
                                : "字体文件：" + info.FileName;
                        }
                    });
                }
                catch
                {
                }
            });
        }

        private void FontListDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _fontList.Items.Count)
            {
                return;
            }

            FontListItem item = _fontList.Items[e.Index] as FontListItem;
            if (item == null)
            {
                return;
            }

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            bool hover = e.Index == _hoverIndex;
            Color background = selected
                ? Theme.AccentSoft
                : (hover ? Color.FromArgb(248, 250, 252) : Theme.Card);

            using (SolidBrush fill = new SolidBrush(background))
            {
                e.Graphics.FillRectangle(fill, e.Bounds);
            }

            if (selected)
            {
                using (SolidBrush accent = new SolidBrush(Theme.Accent))
                {
                    e.Graphics.FillRectangle(accent, e.Bounds.X, e.Bounds.Y + 8, 3, e.Bounds.Height - 16);
                }
            }

            Font previewFont = null;
            bool disposeFont = false;
            try
            {
                previewFont = new Font(item.FamilyName, 11f, FontStyle.Regular);
                disposeFont = true;
            }
            catch
            {
                previewFont = Theme.Font(9f, FontStyle.Regular);
            }

            Rectangle textBounds = new Rectangle(
                e.Bounds.X + 16,
                e.Bounds.Y + 5,
                e.Bounds.Width - 62,
                24);
            TextRenderer.DrawText(
                e.Graphics,
                item.DisplayName,
                previewFont,
                textBounds,
                selected ? Theme.AccentPressed : Theme.Text,
                TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

            TextRenderer.DrawText(
                e.Graphics,
                item.Subtitle,
                Theme.Font(7.4f, FontStyle.Regular),
                new Rectangle(e.Bounds.X + 16, e.Bounds.Y + 27, e.Bounds.Width - 62, 16),
                Theme.MutedLight,
                TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

            if (selected)
            {
                int x = e.Bounds.Right - 30;
                int y = e.Bounds.Y + 16;
                using (SolidBrush circle = new SolidBrush(Theme.Accent))
                {
                    e.Graphics.FillEllipse(circle, x, y, 14, 14);
                }
                using (Pen check = new Pen(Color.White, 1.8f))
                {
                    check.StartCap = LineCap.Round;
                    check.EndCap = LineCap.Round;
                    e.Graphics.DrawLines(check, new PointF[]
                    {
                        new PointF(x + 3.5f, y + 7f),
                        new PointF(x + 6f, y + 9.5f),
                        new PointF(x + 10.5f, y + 4.5f)
                    });
                }
            }

            if (disposeFont && previewFont != null)
            {
                previewFont.Dispose();
            }
        }

        private void FontListMouseMove(object sender, MouseEventArgs e)
        {
            int index = _fontList.IndexFromPoint(e.Location);
            if (index != _hoverIndex)
            {
                _hoverIndex = index;
                _fontList.Invalidate();
            }
        }

        private void FontListMouseLeave(object sender, EventArgs e)
        {
            _hoverIndex = -1;
            _fontList.Invalidate();
        }

        private void ApplyClicked()
        {
            if (_busy || string.IsNullOrEmpty(_selectedFamily))
            {
                return;
            }

            string message =
                "将把 Windows 界面字体替换为：\n“" + _selectedFamily + "”\n\n" +
                "会修改字体映射和当前账户的窗口字体。\n" +
                "首次应用自动备份，之后可随时一键恢复。\n\n" +
                "应用后建议注销并重新登录。";

            bool confirmed = MessageDialog.ShowDialog(
                this,
                DialogKind.Warning,
                "应用全局字体",
                message,
                "应用字体",
                "取消");
            if (!confirmed)
            {
                return;
            }

            OperationRequest request = new OperationRequest();
            request.Operation = "Apply";
            request.FontFamily = _selectedFamily;
            request.ModifyWindowMetrics = _metricsCheck.Checked;
            request.EnableFontLink = _fontLinkCheck.Checked;

            SetBusy(true, "正在应用字体，请在 UAC 窗口中确认…");
            OperationResult result = null;
            try
            {
                result = ElevationService.Execute(request);
            }
            finally
            {
                SetBusy(false, null);
            }

            HandleOperationResult(result, true);
        }

        private void RestoreClicked()
        {
            if (_busy)
            {
                return;
            }

            if (!BackupStore.HasBaseline)
            {
                MessageDialog.ShowDialog(
                    this,
                    DialogKind.Info,
                    "还没有可恢复的备份",
                    "首次应用字体会自动保存系统原始字体设置，之后才能使用一键恢复。",
                    "知道了",
                    null);
                return;
            }

            bool confirmed = MessageDialog.ShowDialog(
                this,
                DialogKind.Warning,
                "一键恢复原始字体",
                "将把字体映射、窗口字体和字体链接恢复到首次使用本工具前的状态。\n\n" +
                "此操作不会删除或修改任何字体文件。恢复后建议注销并重新登录。",
                "恢复原始字体",
                "取消");
            if (!confirmed)
            {
                return;
            }

            OperationRequest request = new OperationRequest();
            request.Operation = "Restore";

            SetBusy(true, "正在恢复原始字体，请在 UAC 窗口中确认…");
            OperationResult result = null;
            try
            {
                result = ElevationService.Execute(request);
            }
            finally
            {
                SetBusy(false, null);
            }

            HandleOperationResult(result, false);
        }

        private void HandleOperationResult(OperationResult result, bool applied)
        {
            RefreshStatus();

            if (result == null)
            {
                MessageDialog.ShowDialog(
                    this,
                    DialogKind.Error,
                    "操作没有返回结果",
                    "操作进程没有返回结果，请检查系统日志或重试。",
                    "知道了",
                    null);
                return;
            }

            if (result.Canceled)
            {
                _statusLabel.Text = "已取消管理员授权，未做任何修改。";
                return;
            }

            if (!result.Success)
            {
                string message = result.Message;
                if (string.IsNullOrEmpty(message))
                {
                    message = "操作失败。";
                }
                if (!string.IsNullOrEmpty(result.LogPath))
                {
                    message += "\n\n日志：" + result.LogPath;
                }

                MessageDialog.ShowDialog(
                    this,
                    DialogKind.Error,
                    applied ? "应用失败" : "恢复失败",
                    message,
                    "知道了",
                    null);
                return;
            }

            string successMessage = applied
                ? "已把传统窗口字体切换为：\n“" + result.TargetFont + "”\n\n" +
                  "标题栏、菜单、对话框等已刷新。部分程序需要重启，注销后重新登录可让所有程序生效。"
                : "字体映射和窗口字体已恢复到首次使用本工具前的状态。\n\n" +
                  "传统窗口字体已刷新。部分程序需要重启，注销后重新登录可让所有程序恢复。";

            bool restart = MessageDialog.ShowDialog(
                this,
                DialogKind.Success,
                applied ? "字体应用成功" : "恢复完成",
                successMessage,
                "重启资源管理器",
                "稍后");
            if (restart)
            {
                RestartExplorer();
            }
        }

        private void RefreshStatus()
        {
            bool hasBaseline = BackupStore.HasBaseline;
            string applied = BackupStore.CurrentSubstitution();
            bool isApplied = !string.IsNullOrEmpty(applied) &&
                !string.Equals(applied, "Segoe UI", StringComparison.OrdinalIgnoreCase);

            StateInfo state = BackupStore.LoadState();
            if (!isApplied && state != null &&
                string.Equals(state.LastAction, "Applied", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(state.TargetFont, "Segoe UI", StringComparison.OrdinalIgnoreCase))
            {
                applied = state.TargetFont;
                isApplied = true;
            }

            _sidebar.IsAdmin = Program.IsAdministrator();
            _sidebar.HasBaseline = hasBaseline;
            _sidebar.IsApplied = isApplied;
            _sidebar.AppliedFont = applied;

            if (hasBaseline)
            {
                _statusLabel.Text = isApplied
                    ? "已应用：" + applied + " · 原始字体备份已就绪"
                    : "原始字体备份已就绪 · 可随时一键恢复";
            }
            else
            {
                _statusLabel.Text = "首次应用会自动创建系统原始字体备份";
            }

            _restoreButton.Enabled = hasBaseline && !_busy;
            _applyButton.Enabled = !_busy && _visibleFonts != null && _visibleFonts.Count > 0;
        }

        private void SetBusy(bool busy, string status)
        {
            _busy = busy;
            _applyButton.Enabled = !busy && _visibleFonts != null && _visibleFonts.Count > 0;
            _restoreButton.Enabled = !busy && BackupStore.HasBaseline;
            _fontList.Enabled = !busy;
            _search.Enabled = !busy;
            _metricsCheck.Enabled = !busy;
            _fontLinkCheck.Enabled = !busy;
            UseWaitCursor = busy;

            if (!string.IsNullOrEmpty(status))
            {
                _statusLabel.Text = status;
            }

            if (busy)
            {
                _applyButton.Text = "正在处理…";
            }
            else
            {
                _applyButton.Text = "应用全局字体";
            }

            Application.DoEvents();
        }

        private void RestartExplorer()
        {
            Cursor previous = Cursor;
            Cursor = Cursors.WaitCursor;
            try
            {
                foreach (Process process in Process.GetProcessesByName("explorer"))
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit(3000);
                    }
                    catch
                    {
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }

                Thread.Sleep(900);
                if (Process.GetProcessesByName("explorer").Length == 0)
                {
                    Process.Start("explorer.exe");
                }
                _statusLabel.Text = "资源管理器已重启，字体设置已重新加载。";
            }
            catch (Exception ex)
            {
                MessageDialog.ShowDialog(
                    this,
                    DialogKind.Error,
                    "重启资源管理器失败",
                    "请手动注销并重新登录，或按 Ctrl+Shift+Esc 打开任务管理器，重新启动 explorer.exe。\n\n" +
                    ex.Message,
                    "知道了",
                    null);
            }
            finally
            {
                Cursor = previous;
            }
        }

        private void OpenBackupFolder()
        {
            try
            {
                if (!Directory.Exists(BackupStore.BaseDirectory))
                {
                    MessageDialog.ShowDialog(
                        this,
                        DialogKind.Info,
                        "备份目录尚未创建",
                        "首次应用字体会自动创建备份目录。",
                        "知道了",
                        null);
                    return;
                }

                Process.Start("explorer.exe", "\"" + BackupStore.BaseDirectory + "\"");
            }
            catch (Exception ex)
            {
                MessageDialog.ShowDialog(
                    this,
                    DialogKind.Error,
                    "无法打开备份目录",
                    ex.Message,
                    "知道了",
                    null);
            }
        }

        private sealed class AdminBadge : Control
        {
            private bool _isAdmin;

            public bool IsAdmin
            {
                get { return _isAdmin; }
                set { _isAdmin = value; Invalidate(); }
            }

            public AdminBadge()
            {
                DoubleBuffered = true;
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
                using (SolidBrush parent = new SolidBrush(Theme.Background))
                {
                    g.FillRectangle(parent, ClientRectangle);
                }

                Color fill = _isAdmin ? Theme.SuccessSoft : Color.FromArgb(241, 245, 249);
                Color border = _isAdmin ? Color.FromArgb(167, 243, 208) : Theme.Border;
                Color text = _isAdmin ? Theme.Success : Theme.Muted;
                Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
                using (GraphicsPath path = Theme.RoundedRect(bounds, 11))
                {
                    using (SolidBrush fillBrush = new SolidBrush(fill))
                    {
                        g.FillPath(fillBrush, path);
                    }
                    using (Pen borderPen = new Pen(border))
                    {
                        g.DrawPath(borderPen, path);
                    }
                }

                using (SolidBrush dot = new SolidBrush(_isAdmin ? Theme.Success : Theme.MutedLight))
                {
                    g.FillEllipse(dot, 11, 9, 6, 6);
                }

                TextRenderer.DrawText(
                    g,
                    _isAdmin ? "管理员模式" : "按需提权",
                    Theme.Font(7.6f, FontStyle.Bold),
                    new Rectangle(22, 0, Width - 26, Height),
                    text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
        }
    }
}
