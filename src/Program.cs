using System;
using System.Drawing;
using System.IO;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace WinFontReplacer
{
    internal static class Program
    {
        private static bool? _isAdministrator;

        public static Icon AppIcon { get; private set; }

        [STAThread]
        private static void Main(string[] args)
        {
            if (args != null && args.Length >= 2 &&
                string.Equals(args[0], "--execute", StringComparison.OrdinalIgnoreCase))
            {
                ExecuteRequest(args[1]);
                return;
            }

            if (args != null && args.Length >= 2 &&
                string.Equals(args[0], "--self-test", StringComparison.OrdinalIgnoreCase))
            {
                int exitCode = SelfTest.Run(args[1]);
                Environment.Exit(exitCode);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainException;

            try
            {
                AppIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                AppIcon = null;
            }

            bool createdNew;
            using (Mutex mutex = new Mutex(true, @"Local\WinFontReplacer.UI", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show(
                        "字体替换器已经在运行。",
                        "Windows 全局字体替换器",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                Application.Run(new MainForm());
                GC.KeepAlive(mutex);
            }
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            string logPath = TryWriteCrashLog("ui", e.Exception);
            try
            {
                MessageBox.Show(
                    "程序遇到一个未处理的错误，操作可能没有完成。\n\n" +
                    e.Exception.Message +
                    "\n\n日志：" + logPath,
                    "Windows 全局字体替换器",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch
            {
            }
        }

        private static void OnDomainException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception exception = e.ExceptionObject as Exception;
            TryWriteCrashLog("domain", exception);
        }

        private static string TryWriteCrashLog(string kind, Exception exception)
        {
            try
            {
                string directory = Path.Combine(AppPaths.DataDirectory, "logs");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(
                    directory,
                    "crash-" + kind + "-" +
                    DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".log");
                string text = DateTime.Now.ToString("o") + Environment.NewLine +
                    (exception == null ? "Unknown exception" : exception.ToString()) +
                    Environment.NewLine;
                File.WriteAllText(path, text);
                return path;
            }
            catch
            {
                return "(无法写入日志文件)";
            }
        }

        public static bool IsAdministrator()
        {
            if (_isAdministrator.HasValue)
            {
                return _isAdministrator.Value;
            }

            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    _isAdministrator = principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                _isAdministrator = false;
            }

            return _isAdministrator.Value;
        }

        private static void ExecuteRequest(string requestPath)
        {
            int exitCode = 1;
            OperationRequest request = null;
            try
            {
                request = OperationRequest.Load(requestPath);
                OperationResult result = OperationEngine.Execute(request);
                if (!string.IsNullOrEmpty(request.ResultPath))
                {
                    result.Save(request.ResultPath);
                }
                exitCode = result.Success ? 0 : 1;
            }
            catch (Exception ex)
            {
                try
                {
                    if (request != null && !string.IsNullOrEmpty(request.ResultPath))
                    {
                        OperationResult result = new OperationResult();
                        result.Success = false;
                        result.Message = "管理员进程发生异常：" + ex.Message;
                        result.Detail = ex.ToString();
                        result.Save(request.ResultPath);
                    }
                    else
                    {
                        string errorDirectory = Path.Combine(
                            Path.GetTempPath(),
                            "WinFontReplacer");
                        Directory.CreateDirectory(errorDirectory);
                        File.WriteAllText(
                            Path.Combine(errorDirectory, "elevated-error.log"),
                            ex.ToString());
                    }
                }
                catch
                {
                }
            }

            Environment.Exit(exitCode);
        }
    }
}
