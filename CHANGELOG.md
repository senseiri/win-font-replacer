# Changelog

## v1.0.3 — 2026-10-31

- 按系统 DPI 计算 LOGFONT 字号（96 DPI 为 -12，144 DPI 为 -18），修复替换后字体变小。
- 每次应用都重新写入正确高度，避免第二次应用继续缩小。
- 更新界面文案和截图。

## v1.0.2 — 2026-10-31

- 修复 `SystemParametersInfo` 调用，应用后立即刷新标题栏、菜单、对话框、状态栏和桌面图标字体。
- 应用 LOGFONT 高度与字符集修正，解决“应用后无效果”。
- 新增 `SPI_SETNONCLIENTMETRICS` / `SPI_SETICONMETRICS` 实时更新。
- 恢复时同步刷新回原始窗口字体。

## v1.0.1 — 2026-10-31

- 修复临时目录权限导致的未处理异常。
- 提权通信目录改为优先使用程序旁的 `WinFontReplacerIpc`。
- 备份目录改为便携的 `WinFontReplacerData`，并支持 `%LOCALAPPDATA%` 回退。
- 增加全局异常日志和更明确的错误提示。

## v1.0.0 — 2026-10-31

- 初始版本：字体选择、搜索、预览。
- 字体映射、FontLink、WindowMetrics 修改。
- 首次应用自动备份、一键恢复、自动回滚。
- 按需 UAC 提权，本地运行，不修改系统字体文件。
