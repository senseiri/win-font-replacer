# Security

## Reporting a vulnerability

请通过 GitHub 仓库的 **Security → Report a vulnerability** 私下报告，不要在公开 Issue 中粘贴完整的注册表快照、日志或系统隐私信息。

## Security model

- 程序只修改 `FontSubstitutes`、`FontLink\SystemLink` 和当前用户 `WindowMetrics`。
- 不修改 `C:\Windows\Fonts` 下的字体文件。
- 不联网、不上传字体、不收集遥测。
- 应用/恢复时才请求管理员权限。
- 备份和日志默认保存在程序旁的 `WinFontReplacerData`，或在目录只读时使用 `%LOCALAPPDATA%\WinFontReplacer`。

请在提交 Issue 前删除日志中的用户名、计算机名和个人路径。
