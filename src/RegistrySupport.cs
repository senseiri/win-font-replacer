using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Win32;

namespace WinFontReplacer
{
    internal enum RegRoot
    {
        LocalMachine,
        CurrentUser
    }

    internal sealed class RegistryRef
    {
        public RegistryRef(RegRoot root, string key, string name, bool is64Bit)
        {
            Root = root;
            Key = key;
            Name = name;
            Is64Bit = is64Bit;
        }

        public RegRoot Root { get; private set; }
        public string Key { get; private set; }
        public string Name { get; private set; }
        public bool Is64Bit { get; private set; }
    }

    internal sealed class RegistryValueSnapshot
    {
        public RegRoot Root { get; set; }
        public string Key { get; set; }
        public string Name { get; set; }
        public bool Is64Bit { get; set; }
        public bool Exists { get; set; }
        public RegistryValueKind Kind { get; set; }
        public string StringValue { get; set; }
        public byte[] BinaryValue { get; set; }
        public string[] Lines { get; set; }
        public long NumberValue { get; set; }
    }

    internal sealed class FontBackup
    {
        public int Version { get; set; }
        public string Kind { get; set; }
        public string TargetFont { get; set; }
        public DateTime CreatedUtc { get; set; }
        public List<RegistryValueSnapshot> Values { get; set; }

        public FontBackup()
        {
            Version = 1;
            Kind = "baseline";
            TargetFont = string.Empty;
            CreatedUtc = DateTime.UtcNow;
            Values = new List<RegistryValueSnapshot>();
        }
    }

    internal static class RegistryAccess
    {
        public static RegistryValueSnapshot Capture(RegistryRef reference)
        {
            RegistryValueSnapshot snapshot = new RegistryValueSnapshot
            {
                Root = reference.Root,
                Key = reference.Key,
                Name = reference.Name,
                Is64Bit = reference.Is64Bit,
                Exists = false,
                Kind = RegistryValueKind.None
            };

            using (RegistryKey key = OpenKey(reference, false, false))
            {
                if (key == null || !ValueExists(key, reference.Name))
                {
                    return snapshot;
                }

                object value = key.GetValue(
                    reference.Name,
                    null,
                    RegistryValueOptions.DoNotExpandEnvironmentNames);
                RegistryValueKind kind = key.GetValueKind(reference.Name);
                snapshot.Exists = true;
                snapshot.Kind = kind;

                switch (kind)
                {
                    case RegistryValueKind.String:
                    case RegistryValueKind.ExpandString:
                        snapshot.StringValue = value as string;
                        break;
                    case RegistryValueKind.Binary:
                        snapshot.BinaryValue = value as byte[];
                        break;
                    case RegistryValueKind.MultiString:
                        snapshot.Lines = value as string[];
                        break;
                    case RegistryValueKind.DWord:
                        snapshot.NumberValue = value is int ? (int)value : 0;
                        break;
                    case RegistryValueKind.QWord:
                        snapshot.NumberValue = value is long ? (long)value : 0;
                        break;
                    default:
                        snapshot.StringValue = value == null ? null : value.ToString();
                        break;
                }
            }

            return snapshot;
        }

        public static void Restore(RegistryValueSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            if (!snapshot.Exists)
            {
                using (RegistryKey key = OpenKey(
                    new RegistryRef(snapshot.Root, snapshot.Key, snapshot.Name, snapshot.Is64Bit),
                    true,
                    false))
                {
                    if (key != null)
                    {
                        key.DeleteValue(snapshot.Name, false);
                    }
                }
                return;
            }

            using (RegistryKey key = OpenKey(
                new RegistryRef(snapshot.Root, snapshot.Key, snapshot.Name, snapshot.Is64Bit),
                true,
                true))
            {
                object value = ValueFromSnapshot(snapshot);
                if (snapshot.Kind == RegistryValueKind.Unknown ||
                    snapshot.Kind == RegistryValueKind.None)
                {
                    key.SetValue(snapshot.Name, value);
                }
                else
                {
                    key.SetValue(snapshot.Name, value, snapshot.Kind);
                }
            }
        }

        private static object ValueFromSnapshot(RegistryValueSnapshot snapshot)
        {
            switch (snapshot.Kind)
            {
                case RegistryValueKind.String:
                case RegistryValueKind.ExpandString:
                    return snapshot.StringValue ?? string.Empty;
                case RegistryValueKind.Binary:
                    return snapshot.BinaryValue ?? new byte[0];
                case RegistryValueKind.MultiString:
                    return snapshot.Lines ?? new string[0];
                case RegistryValueKind.DWord:
                    return unchecked((int)snapshot.NumberValue);
                case RegistryValueKind.QWord:
                    return snapshot.NumberValue;
                default:
                    return snapshot.StringValue ?? string.Empty;
            }
        }

        public static RegistryKey OpenKey(RegistryRef reference, bool writable, bool create)
        {
            RegistryHive hive = reference.Root == RegRoot.LocalMachine
                ? RegistryHive.LocalMachine
                : RegistryHive.CurrentUser;
            RegistryView view = reference.Is64Bit ? RegistryView.Registry64 : RegistryView.Default;

            RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
            try
            {
                if (create)
                {
                    return baseKey.CreateSubKey(reference.Key);
                }
                return baseKey.OpenSubKey(reference.Key, writable);
            }
            finally
            {
                baseKey.Dispose();
            }
        }

        private static bool ValueExists(RegistryKey key, string name)
        {
            foreach (string valueName in key.GetValueNames())
            {
                if (string.Equals(valueName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public static string ReadString(RegistryRef reference)
        {
            using (RegistryKey key = OpenKey(reference, false, false))
            {
                if (key == null)
                {
                    return null;
                }
                return key.GetValue(reference.Name, null) as string;
            }
        }
    }

    internal static class BackupFile
    {
        public static void Save(string path, FontBackup backup)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temp = path + ".tmp";
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.Encoding = new UTF8Encoding(false);
            settings.CloseOutput = true;

            using (XmlWriter writer = XmlWriter.Create(temp, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("fontBackup");
                writer.WriteAttributeString("version", backup.Version.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("kind", backup.Kind ?? string.Empty);
                writer.WriteAttributeString("target", backup.TargetFont ?? string.Empty);
                writer.WriteAttributeString("createdUtc", backup.CreatedUtc.ToString("o", CultureInfo.InvariantCulture));

                foreach (RegistryValueSnapshot value in backup.Values)
                {
                    writer.WriteStartElement("value");
                    writer.WriteAttributeString("root", value.Root.ToString());
                    writer.WriteAttributeString("key", value.Key ?? string.Empty);
                    writer.WriteAttributeString("name", value.Name ?? string.Empty);
                    writer.WriteAttributeString("exists", value.Exists ? "true" : "false");
                    writer.WriteAttributeString("kind", value.Kind.ToString());
                    writer.WriteAttributeString("view", value.Is64Bit ? "64" : "32");

                    if (value.Exists)
                    {
                        switch (value.Kind)
                        {
                            case RegistryValueKind.String:
                            case RegistryValueKind.ExpandString:
                                writer.WriteElementString("string", value.StringValue ?? string.Empty);
                                break;
                            case RegistryValueKind.Binary:
                                writer.WriteElementString(
                                    "binary",
                                    Convert.ToBase64String(value.BinaryValue ?? new byte[0]));
                                break;
                            case RegistryValueKind.MultiString:
                                writer.WriteStartElement("lines");
                                if (value.Lines != null)
                                {
                                    foreach (string line in value.Lines)
                                    {
                                        writer.WriteElementString("line", line ?? string.Empty);
                                    }
                                }
                                writer.WriteEndElement();
                                break;
                            case RegistryValueKind.DWord:
                            case RegistryValueKind.QWord:
                                writer.WriteElementString(
                                    "number",
                                    value.NumberValue.ToString(CultureInfo.InvariantCulture));
                                break;
                        }
                    }

                    writer.WriteEndElement();
                }

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

        public static FontBackup Load(string path)
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
            if (root == null || root.Name.LocalName != "fontBackup")
            {
                throw new InvalidDataException("备份文件格式不正确。");
            }

            FontBackup backup = new FontBackup();
            backup.Version = ParseInt(root.Attribute("version"), 1);
            backup.Kind = (string)root.Attribute("kind") ?? "baseline";
            backup.TargetFont = (string)root.Attribute("target") ?? string.Empty;

            DateTime created;
            if (DateTime.TryParse(
                (string)root.Attribute("createdUtc"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out created))
            {
                backup.CreatedUtc = created;
            }

            foreach (XElement element in root.Elements("value"))
            {
                RegistryValueSnapshot value = new RegistryValueSnapshot();
                string rootName = (string)element.Attribute("root") ?? "LocalMachine";
                value.Root = string.Equals(rootName, "CurrentUser", StringComparison.OrdinalIgnoreCase)
                    ? RegRoot.CurrentUser
                    : RegRoot.LocalMachine;
                value.Key = (string)element.Attribute("key") ?? string.Empty;
                value.Name = (string)element.Attribute("name") ?? string.Empty;
                value.Exists = string.Equals(
                    (string)element.Attribute("exists"),
                    "true",
                    StringComparison.OrdinalIgnoreCase);
                value.Is64Bit = string.Equals(
                    (string)element.Attribute("view"),
                    "64",
                    StringComparison.OrdinalIgnoreCase);

                RegistryValueKind kind;
                if (!Enum.TryParse((string)element.Attribute("kind"), out kind))
                {
                    kind = RegistryValueKind.None;
                }
                value.Kind = kind;

                XElement stringElement = element.Element("string");
                if (stringElement != null)
                {
                    value.StringValue = stringElement.Value;
                }

                XElement binaryElement = element.Element("binary");
                if (binaryElement != null)
                {
                    value.BinaryValue = Convert.FromBase64String(binaryElement.Value);
                }

                XElement linesElement = element.Element("lines");
                if (linesElement != null)
                {
                    List<string> lines = new List<string>();
                    foreach (XElement line in linesElement.Elements("line"))
                    {
                        lines.Add(line.Value);
                    }
                    value.Lines = lines.ToArray();
                }

                XElement numberElement = element.Element("number");
                if (numberElement != null)
                {
                    long number;
                    if (long.TryParse(
                        numberElement.Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out number))
                    {
                        value.NumberValue = number;
                    }
                }

                backup.Values.Add(value);
            }

            if (backup.Values.Count == 0)
            {
                throw new InvalidDataException("备份文件中没有可恢复的数据。");
            }

            return backup;
        }

        private static int ParseInt(XAttribute attribute, int fallback)
        {
            int value;
            if (attribute != null && int.TryParse(attribute.Value, out value))
            {
                return value;
            }
            return fallback;
        }
    }
}
