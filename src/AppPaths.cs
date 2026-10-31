using System;
using System.IO;

namespace WinFontReplacer
{
    internal static class AppPaths
    {
        private static readonly object SyncRoot = new object();
        private static string _dataDirectory;
        private static string _ipcDirectory;

        public static string ApplicationDirectory
        {
            get
            {
                string directory = AppDomain.CurrentDomain.BaseDirectory;
                if (string.IsNullOrEmpty(directory))
                {
                    directory = Environment.CurrentDirectory;
                }
                return Path.GetFullPath(directory);
            }
        }

        public static string DataDirectory
        {
            get
            {
                if (_dataDirectory != null)
                {
                    return _dataDirectory;
                }

                lock (SyncRoot)
                {
                    if (_dataDirectory == null)
                    {
                        _dataDirectory = ResolveDataDirectory();
                    }
                    return _dataDirectory;
                }
            }
        }

        public static string IpcDirectory
        {
            get
            {
                if (_ipcDirectory != null)
                {
                    return _ipcDirectory;
                }

                lock (SyncRoot)
                {
                    if (_ipcDirectory == null)
                    {
                        _ipcDirectory = ResolveIpcDirectory();
                    }
                    return _ipcDirectory;
                }
            }
        }

        private static string ResolveDataDirectory()
        {
            string common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string programData = string.IsNullOrEmpty(common)
                ? null
                : Path.Combine(common, "WinFontReplacer");

            // Keep using an existing system-wide backup if an earlier version created one.
            if (programData != null &&
                File.Exists(Path.Combine(programData, "baseline.xml")))
            {
                return programData;
            }

            string portable = Path.Combine(ApplicationDirectory, "WinFontReplacerData");
            if (CanWrite(portable))
            {
                return portable;
            }

            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(local))
            {
                string localData = Path.Combine(local, "WinFontReplacer");
                if (CanWrite(localData))
                {
                    return localData;
                }
            }

            if (programData != null && CanWrite(programData))
            {
                return programData;
            }

            string temp = Path.Combine(Path.GetTempPath(), "WinFontReplacer");
            if (CanWrite(temp))
            {
                return temp;
            }

            return portable;
        }

        private static string ResolveIpcDirectory()
        {
            string portable = Path.Combine(ApplicationDirectory, "WinFontReplacerIpc");
            if (CanWrite(portable))
            {
                return portable;
            }

            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(local))
            {
                string localIpc = Path.Combine(local, "WinFontReplacer", "ipc");
                if (CanWrite(localIpc))
                {
                    return localIpc;
                }
            }

            string temp = Path.Combine(Path.GetTempPath(), "WinFontReplacer", "ipc");
            if (CanWrite(temp))
            {
                return temp;
            }

            return portable;
        }

        public static bool CanWrite(string directory)
        {
            if (string.IsNullOrEmpty(directory))
            {
                return false;
            }

            try
            {
                Directory.CreateDirectory(directory);
                string probe = Path.Combine(
                    directory,
                    ".write-test-" + Guid.NewGuid().ToString("N") + ".tmp");
                using (FileStream stream = new FileStream(
                    probe,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    1,
                    FileOptions.None))
                {
                    stream.WriteByte(0);
                }
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
