using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Win32;

namespace WinFontReplacer
{
    internal static class ProtectedValues
    {
        public const string FontSubstitutesKey =
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\FontSubstitutes";
        public const string FontLinkKey =
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\FontLink\SystemLink";
        public const string WindowMetricsKey =
            @"Control Panel\Desktop\WindowMetrics";

        public static readonly string[] SubstitutionNames = new string[]
        {
            "Segoe UI",
            "Microsoft YaHei UI",
            "Microsoft YaHei",
            "MS Shell Dlg",
            "MS Shell Dlg 2"
        };

        public static readonly string[] FontLinkNames = new string[]
        {
            "Segoe UI",
            "Microsoft YaHei UI",
            "MS Shell Dlg"
        };

        public static readonly string[] MetricNames = new string[]
        {
            "CaptionFont",
            "MenuFont",
            "MessageFont",
            "StatusFont",
            "SmCaptionFont",
            "IconFont"
        };

        public static List<RegistryRef> AllRefs()
        {
            List<RegistryRef> refs = new List<RegistryRef>();
            foreach (string name in SubstitutionNames)
            {
                refs.Add(new RegistryRef(RegRoot.LocalMachine, FontSubstitutesKey, name, true));
            }
            foreach (string name in FontLinkNames)
            {
                refs.Add(new RegistryRef(RegRoot.LocalMachine, FontLinkKey, name, true));
            }
            foreach (string name in MetricNames)
            {
                refs.Add(new RegistryRef(RegRoot.CurrentUser, WindowMetricsKey, name, false));
            }
            return refs;
        }
    }

    internal sealed class StateInfo
    {
        public string LastAction { get; set; }
        public string TargetFont { get; set; }
        public DateTime ChangedUtc { get; set; }
    }

    internal static class BackupStore
    {
        private static string _directoryOverride;

        public static string BaseDirectory
        {
            get
            {
                return string.IsNullOrEmpty(_directoryOverride)
                    ? AppPaths.DataDirectory
                    : _directoryOverride;
            }
        }

        public static void SetDirectoryOverride(string directory)
        {
            _directoryOverride = string.IsNullOrEmpty(directory) ? null : directory;
        }

        public static string BaselinePath
        {
            get { return Path.Combine(BaseDirectory, "baseline.xml"); }
        }

        public static string StatePath
        {
            get { return Path.Combine(BaseDirectory, "state.xml"); }
        }

        public static string BackupsDirectory
        {
            get { return Path.Combine(BaseDirectory, "backups"); }
        }

        public static string LogsDirectory
        {
            get { return Path.Combine(BaseDirectory, "logs"); }
        }

        public static bool HasBaseline
        {
            get
            {
                try
                {
                    FileInfo info = new FileInfo(BaselinePath);
                    return info.Exists && info.Length > 0;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static void EnsureDirectories()
        {
            Directory.CreateDirectory(BaseDirectory);
            Directory.CreateDirectory(BackupsDirectory);
            Directory.CreateDirectory(LogsDirectory);
        }

        public static FontBackup Capture(string kind, string target)
        {
            FontBackup backup = new FontBackup();
            backup.Kind = kind;
            backup.TargetFont = target ?? string.Empty;
            backup.CreatedUtc = DateTime.UtcNow;

            foreach (RegistryRef reference in ProtectedValues.AllRefs())
            {
                backup.Values.Add(RegistryAccess.Capture(reference));
            }
            return backup;
        }

        public static void EnsureBaseline(OperationLog log)
        {
            EnsureDirectories();
            if (File.Exists(BaselinePath))
            {
                return;
            }

            log.Write("Creating baseline snapshot.");
            FontBackup baseline = Capture("baseline", string.Empty);
            BackupFile.Save(BaselinePath, baseline);
            log.Write("Baseline saved: " + BaselinePath);
            ExportEmergencyRegistryBackups(log);
        }

        public static string SaveSnapshot(FontBackup backup, string prefix)
        {
            EnsureDirectories();
            string name = prefix + "-" +
                DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) +
                ".xml";
            string path = Path.Combine(BackupsDirectory, name);
            BackupFile.Save(path, backup);
            return path;
        }

        public static FontBackup LoadBaseline()
        {
            if (!File.Exists(BaselinePath))
            {
                throw new FileNotFoundException("未找到原始字体备份，请先应用一次字体。", BaselinePath);
            }
            return BackupFile.Load(BaselinePath);
        }

        public static void RestoreValues(FontBackup backup)
        {
            foreach (RegistryValueSnapshot value in backup.Values)
            {
                RegistryAccess.Restore(value);
            }
        }

        public static void SaveState(string action, string targetFont)
        {
            EnsureDirectories();
            StateInfo state = new StateInfo
            {
                LastAction = action,
                TargetFont = targetFont ?? string.Empty,
                ChangedUtc = DateTime.UtcNow
            };

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.Encoding = new UTF8Encoding(false);
            settings.CloseOutput = true;

            string temp = StatePath + ".tmp";
            using (XmlWriter writer = XmlWriter.Create(temp, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("state");
                writer.WriteAttributeString("action", state.LastAction ?? string.Empty);
                writer.WriteAttributeString("target", state.TargetFont ?? string.Empty);
                writer.WriteAttributeString(
                    "changedUtc",
                    state.ChangedUtc.ToString("o", CultureInfo.InvariantCulture));
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            if (File.Exists(StatePath))
            {
                File.Replace(temp, StatePath, null);
            }
            else
            {
                File.Move(temp, StatePath);
            }
        }

        public static StateInfo LoadState()
        {
            try
            {
                if (!File.Exists(StatePath))
                {
                    return null;
                }

                XmlReaderSettings settings = new XmlReaderSettings();
                settings.DtdProcessing = DtdProcessing.Prohibit;
                settings.XmlResolver = null;

                XDocument document;
                using (XmlReader reader = XmlReader.Create(StatePath, settings))
                {
                    document = XDocument.Load(reader);
                }

                XElement root = document.Root;
                if (root == null)
                {
                    return null;
                }

                DateTime changed;
                if (!DateTime.TryParse(
                    (string)root.Attribute("changedUtc"),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out changed))
                {
                    changed = DateTime.MinValue;
                }

                return new StateInfo
                {
                    LastAction = (string)root.Attribute("action") ?? string.Empty,
                    TargetFont = (string)root.Attribute("target") ?? string.Empty,
                    ChangedUtc = changed
                };
            }
            catch
            {
                return null;
            }
        }

        public static string CurrentSubstitution()
        {
            try
            {
                return RegistryAccess.ReadString(new RegistryRef(
                    RegRoot.LocalMachine,
                    ProtectedValues.FontSubstitutesKey,
                    "Segoe UI",
                    true));
            }
            catch
            {
                return null;
            }
        }

        private static void ExportEmergencyRegistryBackups(OperationLog log)
        {
            ExportKey(
                @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\FontSubstitutes",
                Path.Combine(BaseDirectory, "emergency-fontsubstitutes.reg"),
                log);
            ExportKey(
                @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\FontLink\SystemLink",
                Path.Combine(BaseDirectory, "emergency-fontlink.reg"),
                log);
            ExportKey(
                @"HKCU\Control Panel\Desktop\WindowMetrics",
                Path.Combine(BaseDirectory, "emergency-windowmetrics.reg"),
                log);
        }

        private static void ExportKey(string key, string path, OperationLog log)
        {
            try
            {
                if (File.Exists(path))
                {
                    return;
                }

                string reg = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "System32",
                    "reg.exe");
                if (!File.Exists(reg))
                {
                    reg = "reg.exe";
                }

                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = reg;
                info.Arguments = "export \"" + key + "\" \"" + path + "\" /y";
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.WindowStyle = ProcessWindowStyle.Hidden;

                using (Process process = Process.Start(info))
                {
                    if (process != null)
                    {
                        process.WaitForExit(10000);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Write("Emergency .reg export failed: " + ex.Message);
            }
        }
    }

    internal sealed class OperationLog : IDisposable
    {
        private readonly object _sync = new object();
        private StreamWriter _writer;

        public string Path { get; private set; }

        public OperationLog()
        {
            try
            {
                BackupStore.EnsureDirectories();
                Path = System.IO.Path.Combine(
                    BackupStore.LogsDirectory,
                    "operation-" +
                    DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) +
                    ".log");
                _writer = new StreamWriter(Path, true, new UTF8Encoding(false));
                _writer.AutoFlush = true;
            }
            catch
            {
                _writer = null;
                Path = string.Empty;
            }
        }

        public void Write(string message)
        {
            if (_writer == null)
            {
                return;
            }

            lock (_sync)
            {
                try
                {
                    _writer.WriteLine(
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) +
                        "  " + message);
                }
                catch
                {
                    // Logging must never break an operation.
                }
            }
        }

        public void Write(string format, params object[] args)
        {
            Write(string.Format(CultureInfo.InvariantCulture, format, args));
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_writer != null)
                {
                    try
                    {
                        _writer.Dispose();
                    }
                    catch
                    {
                    }
                    _writer = null;
                }
            }
        }
    }
}
