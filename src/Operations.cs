using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Win32;

namespace WinFontReplacer
{
    internal sealed class OperationRequest
    {
        public string Operation { get; set; }
        public string FontFamily { get; set; }
        public bool ModifyWindowMetrics { get; set; }
        public bool EnableFontLink { get; set; }
        public string ResultPath { get; set; }
        public string DataDirectory { get; set; }

        public OperationRequest()
        {
            Operation = "Apply";
            FontFamily = string.Empty;
            ModifyWindowMetrics = true;
            EnableFontLink = true;
            DataDirectory = string.Empty;
        }

        public void Save(string path)
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.Encoding = new UTF8Encoding(false);
            settings.CloseOutput = true;

            using (XmlWriter writer = XmlWriter.Create(path, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("operation");
                writer.WriteAttributeString("name", Operation ?? string.Empty);
                writer.WriteAttributeString("font", FontFamily ?? string.Empty);
                writer.WriteAttributeString("modifyWindowMetrics", ModifyWindowMetrics ? "true" : "false");
                writer.WriteAttributeString("enableFontLink", EnableFontLink ? "true" : "false");
                writer.WriteAttributeString("result", ResultPath ?? string.Empty);
                writer.WriteAttributeString("data", DataDirectory ?? string.Empty);
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        public static OperationRequest Load(string path)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.DtdProcessing = DtdProcessing.Prohibit;
            settings.XmlResolver = null;

            XDocument document;
            using (XmlReader reader = XmlReader.Create(path, settings))
            {
                document = XDocument.Load(reader);
            }

            XElement root = document.Root;
            if (root == null || root.Name.LocalName != "operation")
            {
                throw new InvalidDataException("操作请求格式不正确。");
            }

            OperationRequest request = new OperationRequest();
            request.Operation = (string)root.Attribute("name") ?? "Apply";
            request.FontFamily = (string)root.Attribute("font") ?? string.Empty;
            request.ModifyWindowMetrics = ReadBool(root.Attribute("modifyWindowMetrics"), true);
            request.EnableFontLink = ReadBool(root.Attribute("enableFontLink"), true);
            request.ResultPath = (string)root.Attribute("result") ?? string.Empty;
            request.DataDirectory = (string)root.Attribute("data") ?? string.Empty;
            return request;
        }

        private static bool ReadBool(XAttribute attribute, bool fallback)
        {
            if (attribute == null)
            {
                return fallback;
            }
            return string.Equals(attribute.Value, "true", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal sealed class OperationResult
    {
        public bool Success { get; set; }
        public bool Canceled { get; set; }
        public string Message { get; set; }
        public string Detail { get; set; }
        public string LogPath { get; set; }
        public string TargetFont { get; set; }
        public bool RestartRecommended { get; set; }

        public OperationResult()
        {
            Message = string.Empty;
            Detail = string.Empty;
            LogPath = string.Empty;
            TargetFont = string.Empty;
        }

        public void Save(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.Encoding = new UTF8Encoding(false);
            settings.CloseOutput = true;

            string temp = path + ".tmp";
            using (XmlWriter writer = XmlWriter.Create(temp, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("operationResult");
                writer.WriteAttributeString("success", Success ? "true" : "false");
                writer.WriteAttributeString("canceled", Canceled ? "true" : "false");
                writer.WriteAttributeString("message", Message ?? string.Empty);
                writer.WriteAttributeString("logPath", LogPath ?? string.Empty);
                writer.WriteAttributeString("target", TargetFont ?? string.Empty);
                writer.WriteAttributeString("restart", RestartRecommended ? "true" : "false");
                writer.WriteElementString("detail", Detail ?? string.Empty);
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            if (File.Exists(path))
            {
                File.Replace(temp, path, null);
            }
            else
            {
                File.Move(temp, path);
            }
        }

        public static OperationResult Load(string path)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.DtdProcessing = DtdProcessing.Prohibit;
            settings.XmlResolver = null;

            XDocument document;
            using (XmlReader reader = XmlReader.Create(path, settings))
            {
                document = XDocument.Load(reader);
            }

            XElement root = document.Root;
            if (root == null || root.Name.LocalName != "operationResult")
            {
                throw new InvalidDataException("操作结果格式不正确。");
            }

            OperationResult result = new OperationResult();
            result.Success = ReadBool(root.Attribute("success"), false);
            result.Canceled = ReadBool(root.Attribute("canceled"), false);
            result.Message = (string)root.Attribute("message") ?? string.Empty;
            result.LogPath = (string)root.Attribute("logPath") ?? string.Empty;
            result.TargetFont = (string)root.Attribute("target") ?? string.Empty;
            result.RestartRecommended = ReadBool(root.Attribute("restart"), false);
            XElement detail = root.Element("detail");
            result.Detail = detail == null ? string.Empty : detail.Value;
            return result;
        }

        private static bool ReadBool(XAttribute attribute, bool fallback)
        {
            if (attribute == null)
            {
                return fallback;
            }
            return string.Equals(attribute.Value, "true", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal static class OperationEngine
    {
        private const string MutexName = @"Global\WinFontReplacer.Operation";

        public static OperationResult Execute(OperationRequest request)
        {
            if (!string.IsNullOrEmpty(request.DataDirectory))
            {
                BackupStore.SetDirectoryOverride(request.DataDirectory);
            }

            using (OperationLog log = new OperationLog())
            {
                OperationResult result = new OperationResult();
                result.LogPath = log.Path;
                result.TargetFont = request.FontFamily ?? string.Empty;

                try
                {
                    log.Write("Operation requested: " + request.Operation +
                        "; font=" + (request.FontFamily ?? string.Empty) +
                        "; metrics=" + request.ModifyWindowMetrics +
                        "; fontLink=" + request.EnableFontLink);

                    using (Mutex mutex = new Mutex(false, MutexName))
                    {
                        bool acquired = false;
                        try
                        {
                            try
                            {
                                acquired = mutex.WaitOne(TimeSpan.FromSeconds(8));
                            }
                            catch (AbandonedMutexException)
                            {
                                acquired = true;
                            }

                            if (!acquired)
                            {
                                throw new InvalidOperationException("另一个字体操作正在进行，请稍后重试。");
                            }

                            if (string.Equals(request.Operation, "Restore", StringComparison.OrdinalIgnoreCase))
                            {
                                result = RestoreInternal(log);
                            }
                            else if (string.Equals(request.Operation, "Apply", StringComparison.OrdinalIgnoreCase))
                            {
                                result = ApplyInternal(request, log);
                            }
                            else
                            {
                                throw new InvalidOperationException("未知操作：" + request.Operation);
                            }

                            result.LogPath = log.Path;
                        }
                        finally
                        {
                            if (acquired)
                            {
                                mutex.ReleaseMutex();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Write("Operation failed: " + ex);
                    result.Success = false;
                    result.Detail = ex.ToString();
                    result.Message = ex.Message;
                }

                return result;
            }
        }

        private static OperationResult ApplyInternal(OperationRequest request, OperationLog log)
        {
            string family = (request.FontFamily ?? string.Empty).Trim();
            ValidateFontFamily(family);

            BackupStore.EnsureBaseline(log);

            FontBackup before = BackupStore.Capture("pre-apply", family);
            string beforePath = BackupStore.SaveSnapshot(before, "before-apply");
            log.Write("Pre-apply snapshot: " + beforePath);

            try
            {
                ApplySubstitutions(family, log);
                if (request.EnableFontLink)
                {
                    ApplyFontLink(family, log);
                }
                if (request.ModifyWindowMetrics)
                {
                    ApplyWindowMetrics(family, log);
                }

                BroadcastSettings(log);
                Verify(family, request.ModifyWindowMetrics, request.EnableFontLink, log);
                BackupStore.SaveState("Applied", family);

                OperationResult result = new OperationResult();
                result.Success = true;
                result.TargetFont = family;
                result.RestartRecommended = true;
                result.Message = "已应用字体：“" + family + "”。";
                return result;
            }
            catch (Exception ex)
            {
                log.Write("Apply failed, rolling back: " + ex);
                try
                {
                    BackupStore.RestoreValues(before);
                    BroadcastSettings(log);
                    log.Write("Automatic rollback completed.");
                }
                catch (Exception rollback)
                {
                    log.Write("Automatic rollback FAILED: " + rollback);
                    throw new InvalidOperationException(
                        "应用失败，并且自动回滚未完成。请打开备份目录，使用 emergency-*.reg 文件手动恢复。原因：" +
                        ex.Message,
                        ex);
                }

                throw new InvalidOperationException(
                    "应用失败，已自动回滚到操作前状态。" + ex.Message,
                    ex);
            }
        }

        private static OperationResult RestoreInternal(OperationLog log)
        {
            FontBackup baseline = BackupStore.LoadBaseline();
            log.Write("Loaded baseline: " + BackupStore.BaselinePath +
                " (" + baseline.Values.Count + " values)");

            FontBackup before = BackupStore.Capture("pre-restore", string.Empty);
            string beforePath = BackupStore.SaveSnapshot(before, "before-restore");
            log.Write("Pre-restore snapshot: " + beforePath);

            try
            {
                BackupStore.RestoreValues(baseline);
                ApplyLiveMetrics(log);
                // SPI_SETNONCLIENTMETRICS may rewrite heights; put the exact
                // baseline values back so future restores remain deterministic.
                BackupStore.RestoreValues(baseline);
                BroadcastSettings(log);
                BackupStore.SaveState("Restored", string.Empty);

                OperationResult result = new OperationResult();
                result.Success = true;
                result.RestartRecommended = true;
                result.Message = "已恢复到首次使用本工具前的字体设置。";
                return result;
            }
            catch (Exception ex)
            {
                log.Write("Restore failed, rolling back: " + ex);
                try
                {
                    BackupStore.RestoreValues(before);
                    BroadcastSettings(log);
                    log.Write("Automatic rollback completed.");
                }
                catch (Exception rollback)
                {
                    log.Write("Automatic rollback FAILED: " + rollback);
                    throw new InvalidOperationException(
                        "恢复失败，并且自动回滚未完成。请使用备份目录中的文件手动恢复。原因：" +
                        ex.Message,
                        ex);
                }

                throw new InvalidOperationException(
                    "恢复失败，已自动回滚到操作前状态。" + ex.Message,
                    ex);
            }
        }

        private static void ValidateFontFamily(string family)
        {
            if (string.IsNullOrEmpty(family))
            {
                throw new ArgumentException("请先选择一种字体。");
            }
            if (family.Length > 128)
            {
                throw new ArgumentException("字体名称过长。");
            }
            foreach (char c in family)
            {
                if (char.IsControl(c))
                {
                    throw new ArgumentException("字体名称包含无效字符。");
                }
            }
            if (!FontCatalog.Exists(family))
            {
                throw new ArgumentException("系统中找不到字体：“" + family + "”。");
            }
        }

        private static void ApplySubstitutions(string family, OperationLog log)
        {
            using (RegistryKey key = RegistryAccess.OpenKey(
                new RegistryRef(RegRoot.LocalMachine, ProtectedValues.FontSubstitutesKey, string.Empty, true),
                true,
                true))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("无法打开字体替换注册表项。");
                }

                foreach (string name in ProtectedValues.SubstitutionNames)
                {
                    if (string.Equals(name, family, StringComparison.OrdinalIgnoreCase))
                    {
                        if (key.GetValue(name) != null)
                        {
                            key.DeleteValue(name, false);
                            log.Write("FontSubstitutes: removed self/legacy mapping for " + name);
                        }
                        else
                        {
                            log.Write("Skipping self-substitution: " + name);
                        }
                        continue;
                    }

                    key.SetValue(name, family, RegistryValueKind.String);
                    log.Write("FontSubstitutes: " + name + " -> " + family);
                }
            }
        }

        private static void ApplyFontLink(string family, OperationLog log)
        {
            string entry = FontFileIndex.GetFontLinkEntry(family);
            if (string.IsNullOrEmpty(entry))
            {
                log.Write("Font file not resolved; FontLink skipped for " + family);
                return;
            }

            using (RegistryKey key = RegistryAccess.OpenKey(
                new RegistryRef(RegRoot.LocalMachine, ProtectedValues.FontLinkKey, string.Empty, true),
                true,
                true))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("无法打开字体链接注册表项。");
                }

                foreach (string name in ProtectedValues.FontLinkNames)
                {
                    string[] existing = key.GetValue(
                        name,
                        null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames) as string[];
                    string firstLine = entry;
                    if (existing != null)
                    {
                        foreach (string line in existing)
                        {
                            if (!string.IsNullOrEmpty(line) && IsSameFontLink(line, entry))
                            {
                                firstLine = line;
                                break;
                            }
                        }
                    }

                    List<string> lines = new List<string>();
                    lines.Add(firstLine);

                    if (existing != null)
                    {
                        foreach (string line in existing)
                        {
                            if (!string.IsNullOrEmpty(line) &&
                                !IsSameFontLink(line, entry))
                            {
                                lines.Add(line);
                            }
                        }
                    }

                    key.SetValue(name, lines.ToArray(), RegistryValueKind.MultiString);
                    log.Write("FontLink: " + name + " += " + firstLine);
                }
            }
        }

        private static void ApplyWindowMetrics(string family, OperationLog log)
        {
            using (RegistryKey key = RegistryAccess.OpenKey(
                new RegistryRef(RegRoot.CurrentUser, ProtectedValues.WindowMetricsKey, string.Empty, false),
                true,
                true))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("无法打开当前用户的窗口字体注册表项。");
                }

                foreach (string name in ProtectedValues.MetricNames)
                {
                    byte[] current = key.GetValue(
                        name,
                        null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames) as byte[];
                    string oldFace = LogFontHelper.GetFaceName(current);
                    int height = DefaultHeight(name);
                    byte[] updated = LogFontHelper.WithFaceName(current, family, height);
                    key.SetValue(name, updated, RegistryValueKind.Binary);
                    log.Write("WindowMetrics: " + name + " " + oldFace + " -> " + family);
                }
            }

            ApplyLiveMetrics(log);
        }

        private static int DefaultHeight(string metricName)
        {
            int points = string.Equals(
                metricName,
                "SmCaptionFont",
                StringComparison.OrdinalIgnoreCase) ? 8 : 9;
            int dpi = GetSystemUiDpi();
            int height = -(points * dpi / 72);
            if (height > -9)
            {
                height = -9;
            }
            return height;
        }

        private static int GetSystemUiDpi()
        {
            try
            {
                IntPtr shell = NativeMethods.GetShellWindow();
                if (shell != IntPtr.Zero)
                {
                    uint dpi = NativeMethods.GetDpiForWindow(shell);
                    if (dpi >= 96 && dpi <= 480)
                    {
                        return (int)dpi;
                    }
                }

                IntPtr desktop = NativeMethods.GetDesktopWindow();
                if (desktop != IntPtr.Zero)
                {
                    uint dpi = NativeMethods.GetDpiForWindow(desktop);
                    if (dpi >= 96 && dpi <= 480)
                    {
                        return (int)dpi;
                    }
                }
            }
            catch
            {
            }
            return 96;
        }

        private static void ApplyLiveMetrics(OperationLog log)
        {
            try
            {
                NativeMethods.NONCLIENTMETRICS metrics = new NativeMethods.NONCLIENTMETRICS();
                metrics.cbSize = Marshal.SizeOf(typeof(NativeMethods.NONCLIENTMETRICS));
                if (!NativeMethods.SystemParametersInfo(
                    NativeMethods.SPI_GETNONCLIENTMETRICS,
                    0,
                    ref metrics,
                    0))
                {
                    log.Write("SPI_GETNONCLIENTMETRICS failed: " +
                        Marshal.GetLastWin32Error());
                    return;
                }

                using (RegistryKey key = RegistryAccess.OpenKey(
                    new RegistryRef(RegRoot.CurrentUser, ProtectedValues.WindowMetricsKey, string.Empty, false),
                    false,
                    false))
                {
                    if (key == null)
                    {
                        log.Write("WindowMetrics key disappeared; live metrics skipped.");
                        return;
                    }

                    metrics.lfCaptionFont = ReadLogFont(key, "CaptionFont", DefaultHeight("CaptionFont"));
                    metrics.lfSmCaptionFont = ReadLogFont(key, "SmCaptionFont", DefaultHeight("SmCaptionFont"));
                    metrics.lfMenuFont = ReadLogFont(key, "MenuFont", DefaultHeight("MenuFont"));
                    metrics.lfStatusFont = ReadLogFont(key, "StatusFont", DefaultHeight("StatusFont"));
                    metrics.lfMessageFont = ReadLogFont(key, "MessageFont", DefaultHeight("MessageFont"));
                }

                bool applied = NativeMethods.SystemParametersInfo(
                    NativeMethods.SPI_SETNONCLIENTMETRICS,
                    metrics.cbSize,
                    ref metrics,
                    NativeMethods.SPIF_UPDATEINIFILE | NativeMethods.SPIF_SENDCHANGE);
                log.Write("SPI_SETNONCLIENTMETRICS: " + applied);

                NativeMethods.ICONMETRICS iconMetrics = new NativeMethods.ICONMETRICS();
                iconMetrics.cbSize = Marshal.SizeOf(typeof(NativeMethods.ICONMETRICS));
                if (NativeMethods.SystemParametersInfo(
                    NativeMethods.SPI_GETICONMETRICS,
                    0,
                    ref iconMetrics,
                    0))
                {
                    using (RegistryKey key = RegistryAccess.OpenKey(
                        new RegistryRef(RegRoot.CurrentUser, ProtectedValues.WindowMetricsKey, string.Empty, false),
                        false,
                        false))
                    {
                        if (key != null)
                        {
                            iconMetrics.lfFont = ReadLogFont(key, "IconFont", DefaultHeight("IconFont"));
                        }
                    }

                    bool iconApplied = NativeMethods.SystemParametersInfo(
                        NativeMethods.SPI_SETICONMETRICS,
                        iconMetrics.cbSize,
                        ref iconMetrics,
                        NativeMethods.SPIF_UPDATEINIFILE | NativeMethods.SPIF_SENDCHANGE);
                    log.Write("SPI_SETICONMETRICS: " + iconApplied);
                }
            }
            catch (Exception ex)
            {
                log.Write("Live window metrics update failed (non-fatal): " + ex.Message);
            }
        }

        private static NativeMethods.LOGFONT ReadLogFont(
            RegistryKey key,
            string name,
            int defaultHeight)
        {
            byte[] data = key.GetValue(
                name,
                null,
                RegistryValueOptions.DoNotExpandEnvironmentNames) as byte[];
            if (data == null || data.Length != LogFontHelper.Size)
            {
                data = LogFontHelper.CreateDefault(defaultHeight);
            }

            byte[] normalized = LogFontHelper.WithFaceName(
                data,
                LogFontHelper.GetFaceName(data),
                defaultHeight);

            GCHandle handle = GCHandle.Alloc(normalized, GCHandleType.Pinned);
            try
            {
                return (NativeMethods.LOGFONT)Marshal.PtrToStructure(
                    handle.AddrOfPinnedObject(),
                    typeof(NativeMethods.LOGFONT));
            }
            finally
            {
                handle.Free();
            }
        }

        private static bool IsSameFontLink(string existingLine, string newEntry)
        {
            string[] existing = existingLine.Split(',');
            string[] wanted = newEntry.Split(',');
            if (existing.Length == 0 || wanted.Length == 0)
            {
                return false;
            }

            string existingFile = Path.GetFileName(existing[0].Trim());
            string wantedFile = Path.GetFileName(wanted[0].Trim());
            if (!string.Equals(existingFile, wantedFile, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (wanted.Length >= 2)
            {
                return existing.Length >= 2 &&
                    string.Equals(existing[1].Trim(), wanted[1].Trim(), StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        private static void Verify(
            string family,
            bool verifyMetrics,
            bool verifyFontLink,
            OperationLog log)
        {
            string substitution = BackupStore.CurrentSubstitution();
            bool self = string.Equals(family, "Segoe UI", StringComparison.OrdinalIgnoreCase);
            if (!self && !string.Equals(substitution, family, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("字体替换写入验证失败。");
            }

            if (verifyMetrics)
            {
                using (RegistryKey key = RegistryAccess.OpenKey(
                    new RegistryRef(RegRoot.CurrentUser, ProtectedValues.WindowMetricsKey, string.Empty, false),
                    false,
                    false))
                {
                    byte[] value = key == null ? null : key.GetValue("MessageFont") as byte[];
                    string face = LogFontHelper.GetFaceName(value);
                    string expected = family.Length > 31 ? family.Substring(0, 31) : family;
                    if (!string.Equals(face, expected, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("窗口字体写入验证失败。");
                    }
                }
            }

            if (verifyFontLink)
            {
                string entry = FontFileIndex.GetFontLinkEntry(family);
                if (!string.IsNullOrEmpty(entry))
                {
                    using (RegistryKey key = RegistryAccess.OpenKey(
                        new RegistryRef(RegRoot.LocalMachine, ProtectedValues.FontLinkKey, string.Empty, true),
                        false,
                        false))
                    {
                        string[] lines = key == null
                            ? null
                            : key.GetValue("Segoe UI") as string[];
                        if (lines == null || lines.Length == 0 ||
                            !IsSameFontLink(lines[0], entry))
                        {
                            throw new InvalidOperationException("字体链接写入验证失败。");
                        }
                    }
                }
            }

            log.Write("Verification passed.");
        }

        private static void BroadcastSettings(OperationLog log)
        {
            try
            {
                IntPtr result;
                NativeMethods.SendMessageTimeout(
                    new IntPtr(NativeMethods.HWND_BROADCAST),
                    (uint)NativeMethods.WM_SETTINGCHANGE,
                    IntPtr.Zero,
                    "WindowMetrics",
                    (uint)NativeMethods.SMTO_ABORTIFHUNG,
                    1500,
                    out result);
                NativeMethods.SendMessageTimeout(
                    new IntPtr(NativeMethods.HWND_BROADCAST),
                    (uint)NativeMethods.WM_SETTINGCHANGE,
                    IntPtr.Zero,
                    "Fonts",
                    (uint)NativeMethods.SMTO_ABORTIFHUNG,
                    1500,
                    out result);
                log.Write("Broadcast WM_SETTINGCHANGE completed.");
            }
            catch (Exception ex)
            {
                log.Write("Broadcast failed (non-fatal): " + ex.Message);
            }
        }
    }

    internal static class ElevationService
    {
        public static OperationResult Execute(OperationRequest request)
        {
            if (Program.IsAdministrator())
            {
                return OperationEngine.Execute(request);
            }

            string ipcDirectory = AppPaths.IpcDirectory;
            try
            {
                Directory.CreateDirectory(ipcDirectory);
            }
            catch (Exception ex)
            {
                return Failure(
                    "无法创建临时通信目录：" + ipcDirectory + "。请检查该目录的写入权限。",
                    ex.ToString());
            }

            string id = Guid.NewGuid().ToString("N");
            string requestPath = Path.Combine(ipcDirectory, "request-" + id + ".xml");
            string resultPath = Path.Combine(ipcDirectory, "result-" + id + ".xml");
            request.ResultPath = resultPath;
            request.DataDirectory = BackupStore.BaseDirectory;

            try
            {
                try
                {
                    request.Save(requestPath);
                }
                catch (Exception ex)
                {
                    return Failure(
                        "无法创建提权请求文件：" + requestPath + "。请检查目录权限。",
                        ex.ToString());
                }

                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = System.Windows.Forms.Application.ExecutablePath;
                info.Arguments = "--execute \"" + requestPath + "\"";
                info.UseShellExecute = true;
                info.Verb = "runas";
                info.WorkingDirectory = AppPaths.ApplicationDirectory;
                info.WindowStyle = ProcessWindowStyle.Hidden;

                try
                {
                    using (Process process = Process.Start(info))
                    {
                        if (process == null)
                        {
                            return Failure("无法启动管理员进程。");
                        }

                        if (!process.WaitForExit(180000))
                        {
                            try
                            {
                                process.Kill();
                            }
                            catch
                            {
                            }
                            return Failure("操作超时，未读取到结果。");
                        }
                    }
                }
                catch (Win32Exception ex)
                {
                    if (ex.NativeErrorCode == 1223)
                    {
                        OperationResult canceled = new OperationResult();
                        canceled.Success = false;
                        canceled.Canceled = true;
                        canceled.Message = "已取消管理员授权，未做任何修改。";
                        return canceled;
                    }
                    return Failure("无法请求管理员权限：" + ex.Message, ex.ToString());
                }

                if (!File.Exists(resultPath))
                {
                    return Failure("操作进程没有返回结果。可能被安全软件拦截或异常退出。");
                }

                try
                {
                    return OperationResult.Load(resultPath);
                }
                catch (Exception ex)
                {
                    return Failure("读取操作结果失败：" + ex.Message, ex.ToString());
                }
            }
            finally
            {
                TryDelete(requestPath);
                TryDelete(resultPath);
            }
        }

        private static OperationResult Failure(string message)
        {
            return Failure(message, string.Empty);
        }

        private static OperationResult Failure(string message, string detail)
        {
            OperationResult result = new OperationResult();
            result.Success = false;
            result.Message = message;
            result.Detail = detail;
            return result;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }
}
