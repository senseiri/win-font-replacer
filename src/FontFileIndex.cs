using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace WinFontReplacer
{
    internal sealed class FontFileInfo
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string FamilyName { get; set; }
        public string FullName { get; set; }
        public bool IsCollection { get; set; }
    }

    internal static class FontFileIndex
    {
        private static readonly object SyncRoot = new object();
        private static Dictionary<string, FontFileInfo> _index;

        public static FontFileInfo Find(string familyName)
        {
            if (string.IsNullOrEmpty(familyName))
            {
                return null;
            }

            EnsureBuilt();

            FontFileInfo info;
            if (_index.TryGetValue(familyName, out info))
            {
                return info;
            }
            return null;
        }

        public static string GetFontLinkEntry(string familyName)
        {
            FontFileInfo info = Find(familyName);
            if (info == null)
            {
                return null;
            }

            string fileName = info.FilePath;
            if (info.FilePath.StartsWith(FontsDirectory, StringComparison.OrdinalIgnoreCase))
            {
                fileName = Path.GetFileName(info.FilePath);
            }

            if (info.FamilyName.IndexOf(",", StringComparison.Ordinal) >= 0)
            {
                return null;
            }

            return fileName + "," + info.FamilyName;
        }

        private static string FontsDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "Fonts");
            }
        }

        private static void EnsureBuilt()
        {
            if (_index != null)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (_index != null)
                {
                    return;
                }

                Dictionary<string, FontFileInfo> map =
                    new Dictionary<string, FontFileInfo>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (KeyValuePair<string, string> pair in ReadFontRegistry())
                {
                    string path = ResolveFontPath(pair.Value);
                    if (path == null || !File.Exists(path) || !files.Add(path))
                    {
                        continue;
                    }

                    List<FontFileInfo> infos = ParseFile(path);
                    foreach (FontFileInfo info in infos)
                    {
                        AddFamily(map, info.FamilyName, info);
                        AddFamily(map, info.FullName, info);
                    }

                    foreach (string fallbackName in RegistryFamilyNames(pair.Key))
                    {
                        FontFileInfo preferred = infos.Count > 0 ? infos[0] : null;
                        if (preferred != null)
                        {
                            AddFamily(map, fallbackName, preferred);
                        }
                    }
                }

                _index = map;
            }
        }

        private static void AddFamily(
            Dictionary<string, FontFileInfo> map,
            string name,
            FontFileInfo info)
        {
            if (string.IsNullOrEmpty(name) || name.Length < 1 || info == null)
            {
                return;
            }

            if (!map.ContainsKey(name))
            {
                map[name] = info;
            }
        }

        private static IEnumerable<KeyValuePair<string, string>> ReadFontRegistry()
        {
            List<KeyValuePair<string, string>> values = new List<KeyValuePair<string, string>>();
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = baseKey.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts",
                    false))
                {
                    ReadFontRegistryKey(key, values);
                }
            }
            catch
            {
                // FontLink is optional; a failure here must not block applying substitutions.
            }

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts",
                    false))
                {
                    ReadFontRegistryKey(key, values);
                }
            }
            catch
            {
            }

            return values;
        }

        private static void ReadFontRegistryKey(
            RegistryKey key,
            List<KeyValuePair<string, string>> values)
        {
            if (key == null)
            {
                return;
            }

            foreach (string name in key.GetValueNames())
            {
                string value = key.GetValue(name, null) as string;
                if (!string.IsNullOrEmpty(value))
                {
                    values.Add(new KeyValuePair<string, string>(name, value));
                }
            }
        }

        private static string ResolveFontPath(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            value = Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
            if (Path.IsPathRooted(value))
            {
                return File.Exists(value) ? value : null;
            }

            string path = Path.Combine(FontsDirectory, value);
            if (File.Exists(path))
            {
                return path;
            }

            string perUser = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "Windows",
                "Fonts",
                value);
            return File.Exists(perUser) ? perUser : null;
        }

        private static IEnumerable<string> RegistryFamilyNames(string valueName)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(valueName))
            {
                return result;
            }

            string value = valueName;
            int suffix = value.IndexOf(" (", StringComparison.Ordinal);
            if (suffix > 0)
            {
                value = value.Substring(0, suffix);
            }

            foreach (string part in value.Split(new string[] { " & " }, StringSplitOptions.RemoveEmptyEntries))
            {
                string name = part.Trim();
                if (name.Length > 0)
                {
                    result.Add(name);
                }
            }
            return result;
        }

        private static List<FontFileInfo> ParseFile(string path)
        {
            List<FontFileInfo> result = new List<FontFileInfo>();
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    if (stream.Length < 12)
                    {
                        return result;
                    }

                    uint tag = ReadUInt32(reader);
                    List<long> offsets = new List<long>();
                    bool collection = false;

                    if (tag == 0x74746366) // 'ttcf'
                    {
                        collection = true;
                        ReadUInt32(reader); // version
                        uint count = ReadUInt32(reader);
                        if (count == 0 || count > 512)
                        {
                            return result;
                        }
                        for (uint i = 0; i < count; i++)
                        {
                            offsets.Add(ReadUInt32(reader));
                        }
                    }
                    else
                    {
                        offsets.Add(0);
                    }

                    foreach (long offset in offsets)
                    {
                        FontFileInfo info = ParseFace(stream, offset, path, collection || offsets.Count > 1);
                        if (info != null)
                        {
                            result.Add(info);
                        }
                    }
                }
            }
            catch
            {
                // Ignore malformed or unsupported font files.
            }
            return result;
        }

        private static FontFileInfo ParseFace(FileStream stream, long offset, string path, bool collection)
        {
            if (offset < 0 || offset + 12 > stream.Length)
            {
                return null;
            }

            stream.Position = offset;
            using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                uint version = ReadUInt32(reader);
                if (version != 0x00010000 && version != 0x4F54544F && version != 0x74727565)
                {
                    return null;
                }

                ushort tableCount = ReadUInt16(reader);
                reader.ReadUInt16();
                reader.ReadUInt16();
                reader.ReadUInt16();
                if (tableCount == 0 || tableCount > 512)
                {
                    return null;
                }

                long nameOffset = -1;
                long nameLength = 0;
                for (int i = 0; i < tableCount; i++)
                {
                    uint tableTag = ReadUInt32(reader);
                    reader.ReadUInt32();
                    uint tableOffset = ReadUInt32(reader);
                    uint tableLength = ReadUInt32(reader);
                    if (tableTag == 0x6E616D65) // 'name'
                    {
                        nameOffset = tableOffset;
                        nameLength = tableLength;
                    }
                }

                if (nameOffset < 0 || nameOffset + nameLength > stream.Length || nameLength < 6)
                {
                    return null;
                }

                stream.Position = nameOffset;
                ushort format = ReadUInt16(reader);
                ushort count = ReadUInt16(reader);
                ushort stringOffset = ReadUInt16(reader);
                if (format > 1 || count > 4096)
                {
                    return null;
                }

                Dictionary<ushort, NamedString> names = new Dictionary<ushort, NamedString>();
                for (int i = 0; i < count; i++)
                {
                    ushort platform = ReadUInt16(reader);
                    ushort encoding = ReadUInt16(reader);
                    ushort language = ReadUInt16(reader);
                    ushort nameId = ReadUInt16(reader);
                    ushort length = ReadUInt16(reader);
                    ushort stringOffsetInTable = ReadUInt16(reader);

                    long start = nameOffset + stringOffset + stringOffsetInTable;
                    if (start < 0 || start + length > stream.Length)
                    {
                        continue;
                    }

                    long returnPosition = stream.Position;
                    stream.Position = start;
                    byte[] bytes = reader.ReadBytes(length);
                    stream.Position = returnPosition;

                    string value = DecodeName(bytes, platform, encoding);
                    if (string.IsNullOrEmpty(value))
                    {
                        continue;
                    }

                    NamedString existing;
                    int rank = LanguageRank(language);
                    if (!names.TryGetValue(nameId, out existing) || rank > existing.Rank)
                    {
                        names[nameId] = new NamedString { Value = value.Trim(), Rank = rank };
                    }
                }

                NamedString familyName = GetName(names, 16);
                if (familyName == null)
                {
                    familyName = GetName(names, 1);
                }
                if (familyName == null || string.IsNullOrEmpty(familyName.Value))
                {
                    return null;
                }

                NamedString fullName = GetName(names, 4);

                return new FontFileInfo
                {
                    FilePath = path,
                    FileName = Path.GetFileName(path),
                    FamilyName = familyName.Value,
                    FullName = fullName != null ? fullName.Value : familyName.Value,
                    IsCollection = collection
                };
            }
        }

        private static NamedString GetName(Dictionary<ushort, NamedString> names, ushort id)
        {
            NamedString value;
            return names.TryGetValue(id, out value) ? value : null;
        }

        private static int LanguageRank(ushort language)
        {
            if (language == 0x0409)
            {
                return 4;
            }
            if (language == 0x0804)
            {
                return 3;
            }
            if (language == 0x0404)
            {
                return 2;
            }
            return 1;
        }

        private static string DecodeName(byte[] bytes, ushort platform, ushort encoding)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return null;
            }

            try
            {
                if (platform == 0 || platform == 3)
                {
                    if (bytes.Length % 2 == 1)
                    {
                        Array.Resize(ref bytes, bytes.Length - 1);
                    }
                    return Encoding.BigEndianUnicode.GetString(bytes);
                }

                if (platform == 1)
                {
                    return Encoding.GetEncoding(10000).GetString(bytes);
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        private static ushort ReadUInt16(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(2);
            if (bytes.Length != 2)
            {
                throw new EndOfStreamException();
            }
            return (ushort)((bytes[0] << 8) | bytes[1]);
        }

        private static uint ReadUInt32(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            if (bytes.Length != 4)
            {
                throw new EndOfStreamException();
            }
            return ((uint)bytes[0] << 24) |
                   ((uint)bytes[1] << 16) |
                   ((uint)bytes[2] << 8) |
                   bytes[3];
        }

        private sealed class NamedString
        {
            public string Value;
            public int Rank;
        }
    }
}
