using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace WinFontReplacer
{
    internal static class SelfTest
    {
        public static int Run(string resultPath)
        {
            List<string> lines = new List<string>();
            bool failed = false;
            string resultDirectory = Path.GetDirectoryName(Path.GetFullPath(resultPath));
            if (string.IsNullOrEmpty(resultDirectory))
            {
                resultDirectory = Environment.CurrentDirectory;
            }
            string tempRoot = Path.Combine(
                resultDirectory,
                "selftest-temp",
                Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(tempRoot);

                RunTest(lines, ref failed, "FontCatalog", delegate
                {
                    List<FontListItem> fonts = FontCatalog.Load();
                    if (fonts.Count < 20)
                    {
                        throw new Exception("Installed font count is unexpectedly low: " + fonts.Count);
                    }
                    if (!fonts.Exists(delegate(FontListItem item)
                    {
                        return string.Equals(item.FamilyName, "Segoe UI", StringComparison.OrdinalIgnoreCase);
                    }))
                    {
                        throw new Exception("Segoe UI was not found in the installed font list.");
                    }
                });

                RunTest(lines, ref failed, "FontFileIndex", delegate
                {
                    FontFileInfo info = FontFileIndex.Find("Microsoft YaHei UI");
                    if (info == null)
                    {
                        info = FontFileIndex.Find("Segoe UI");
                    }
                    if (info == null)
                    {
                        throw new Exception("Could not resolve a known system font file.");
                    }
                    if (!File.Exists(info.FilePath))
                    {
                        throw new Exception("Resolved font file does not exist: " + info.FilePath);
                    }
                    string link = FontFileIndex.GetFontLinkEntry(info.FamilyName);
                    if (string.IsNullOrEmpty(link))
                    {
                        throw new Exception("FontLink entry was empty.");
                    }
                });

                RunTest(lines, ref failed, "LogFont", delegate
                {
                    byte[] font = LogFontHelper.CreateDefault();
                    if (font.Length != 92)
                    {
                        throw new Exception("LOGFONT length was not 92.");
                    }
                    font = LogFontHelper.WithFaceName(font, "Microsoft YaHei UI");
                    if (!string.Equals(
                        LogFontHelper.GetFaceName(font),
                        "Microsoft YaHei UI",
                        StringComparison.Ordinal))
                    {
                        throw new Exception("Face name round-trip failed.");
                    }
                    byte[] longName = LogFontHelper.WithFaceName(font, new string('A', 40));
                    if (LogFontHelper.GetFaceName(longName).Length != 31)
                    {
                        throw new Exception("Face name was not truncated to 31 characters.");
                    }
                });

                RunTest(lines, ref failed, "BackupXml", delegate
                {
                    FontBackup backup = new FontBackup();
                    backup.Kind = "selftest";
                    backup.TargetFont = "Test Font, Inc.";
                    backup.Values.Add(new RegistryValueSnapshot
                    {
                        Root = RegRoot.LocalMachine,
                        Key = "Software\\WinFontReplacer\\Test",
                        Name = "StringValue",
                        Is64Bit = true,
                        Exists = true,
                        Kind = RegistryValueKind.String,
                        StringValue = "hello"
                    });
                    backup.Values.Add(new RegistryValueSnapshot
                    {
                        Root = RegRoot.CurrentUser,
                        Key = "Control Panel\\Desktop\\WindowMetrics",
                        Name = "CaptionFont",
                        Is64Bit = false,
                        Exists = true,
                        Kind = RegistryValueKind.Binary,
                        BinaryValue = LogFontHelper.WithFaceName(null, "Test Font")
                    });
                    backup.Values.Add(new RegistryValueSnapshot
                    {
                        Root = RegRoot.LocalMachine,
                        Key = "Software\\WinFontReplacer\\Test",
                        Name = "Lines",
                        Is64Bit = true,
                        Exists = true,
                        Kind = RegistryValueKind.MultiString,
                        Lines = new string[] { "C:\\Windows\\Fonts\\a.ttf,Font A", "C:\\Windows\\Fonts\\b.ttc,Font B" }
                    });
                    backup.Values.Add(new RegistryValueSnapshot
                    {
                        Root = RegRoot.LocalMachine,
                        Key = "Software\\WinFontReplacer\\Test",
                        Name = "Missing",
                        Is64Bit = true,
                        Exists = false,
                        Kind = RegistryValueKind.None
                    });

                    string path = Path.Combine(tempRoot, "backup-test.xml");
                    BackupFile.Save(path, backup);
                    FontBackup loaded = BackupFile.Load(path);
                    if (loaded.Values.Count != 4)
                    {
                        throw new Exception("Backup value count mismatch.");
                    }
                    if (loaded.Values[0].StringValue != "hello")
                    {
                        throw new Exception("String value did not round-trip.");
                    }
                    if (loaded.Values[1].BinaryValue == null || loaded.Values[1].BinaryValue.Length != 92)
                    {
                        throw new Exception("Binary value did not round-trip.");
                    }
                    if (loaded.Values[2].Lines == null || loaded.Values[2].Lines.Length != 2)
                    {
                        throw new Exception("Multi-string value did not round-trip.");
                    }
                    if (loaded.Values[3].Exists)
                    {
                        throw new Exception("Missing value unexpectedly exists.");
                    }
                });

                RunTest(lines, ref failed, "OperationRequest", delegate
                {
                    OperationRequest request = new OperationRequest();
                    request.Operation = "Apply";
                    request.FontFamily = "Microsoft YaHei UI";
                    request.ModifyWindowMetrics = true;
                    request.EnableFontLink = false;
                    request.ResultPath = Path.Combine(tempRoot, "result&test.xml");
                    string path = Path.Combine(tempRoot, "request-test.xml");
                    request.Save(path);
                    OperationRequest loaded = OperationRequest.Load(path);
                    if (loaded.Operation != "Apply" ||
                        loaded.FontFamily != "Microsoft YaHei UI" ||
                        !loaded.ModifyWindowMetrics ||
                        loaded.EnableFontLink ||
                        loaded.ResultPath != request.ResultPath)
                    {
                        throw new Exception("Operation request did not round-trip.");
                    }
                });

                RunTest(lines, ref failed, "RegistrySnapshot", delegate
                {
                    string testKey = "Software\\WinFontReplacerSelfTest\\" + Guid.NewGuid().ToString("N");
                    try
                    {
                        RegistryKey created;
                        try
                        {
                            created = Registry.CurrentUser.CreateSubKey(testKey);
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            throw new SkipTestException(
                                "Environment does not allow HKCU test writes: " + ex.Message);
                        }

                        using (created)
                        {
                            created.SetValue("A", "original", RegistryValueKind.String);
                            created.SetValue("B", new byte[] { 1, 2, 3, 4 }, RegistryValueKind.Binary);
                            created.SetValue("C", new string[] { "x", "y" }, RegistryValueKind.MultiString);
                            created.SetValue("D", 42, RegistryValueKind.DWord);
                        }

                        RegistryRef refA = new RegistryRef(RegRoot.CurrentUser, testKey, "A", false);
                        RegistryRef refB = new RegistryRef(RegRoot.CurrentUser, testKey, "B", false);
                        RegistryRef refC = new RegistryRef(RegRoot.CurrentUser, testKey, "C", false);
                        RegistryRef refD = new RegistryRef(RegRoot.CurrentUser, testKey, "D", false);
                        RegistryRef refE = new RegistryRef(RegRoot.CurrentUser, testKey, "E", false);

                        List<RegistryValueSnapshot> snapshot = new List<RegistryValueSnapshot>();
                        snapshot.Add(RegistryAccess.Capture(refA));
                        snapshot.Add(RegistryAccess.Capture(refB));
                        snapshot.Add(RegistryAccess.Capture(refC));
                        snapshot.Add(RegistryAccess.Capture(refD));
                        snapshot.Add(RegistryAccess.Capture(refE));

                        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(testKey, true))
                        {
                            key.SetValue("A", "changed", RegistryValueKind.String);
                            key.SetValue("B", new byte[] { 9, 9 }, RegistryValueKind.Binary);
                            key.DeleteValue("C", false);
                            key.SetValue("E", "new", RegistryValueKind.String);
                        }

                        foreach (RegistryValueSnapshot value in snapshot)
                        {
                            RegistryAccess.Restore(value);
                        }

                        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(testKey, false))
                        {
                            if ((string)key.GetValue("A") != "original")
                            {
                                throw new Exception("String value was not restored.");
                            }
                            byte[] b = key.GetValue("B") as byte[];
                            if (b == null || b.Length != 4 || b[0] != 1 || b[3] != 4)
                            {
                                throw new Exception("Binary value was not restored.");
                            }
                            string[] c = key.GetValue("C") as string[];
                            if (c == null || c.Length != 2 || c[0] != "x")
                            {
                                throw new Exception("Multi-string value was not restored.");
                            }
                            if ((int)key.GetValue("D") != 42)
                            {
                                throw new Exception("DWORD value was not restored.");
                            }
                            if (key.GetValue("E") != null)
                            {
                                throw new Exception("Value that did not exist before was not removed.");
                            }
                        }
                    }
                    finally
                    {
                        try
                        {
                            Registry.CurrentUser.DeleteSubKeyTree(testKey, false);
                        }
                        catch
                        {
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                failed = true;
                lines.Add("[FAIL] SelfTest harness: " + ex);
            }
            finally
            {
                try
                {
                    Directory.Delete(tempRoot, true);
                }
                catch
                {
                }
            }

            lines.Add("RESULT: " + (failed ? "FAIL" : "PASS"));
            string text = string.Join(Environment.NewLine, lines.ToArray()) + Environment.NewLine;
            try
            {
                string directory = Path.GetDirectoryName(resultPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText(resultPath, text, new UTF8Encoding(false));
            }
            catch
            {
            }

            return failed ? 1 : 0;
        }

        private static void RunTest(
            List<string> lines,
            ref bool failed,
            string name,
            Action test)
        {
            try
            {
                test();
                lines.Add("[PASS] " + name);
            }
            catch (SkipTestException ex)
            {
                lines.Add("[SKIP] " + name + ": " + ex.Message);
            }
            catch (Exception ex)
            {
                failed = true;
                lines.Add("[FAIL] " + name + ": " + ex.Message);
            }
        }

        private sealed class SkipTestException : Exception
        {
            public SkipTestException(string message)
                : base(message)
            {
            }
        }
    }
}
