param(
    [string]$ExePath = (Join-Path $PSScriptRoot '..\dist\WinFontReplacer.exe')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public class WinFontCapture {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int max);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    public static List<IntPtr> VisibleWindows(uint target) {
        var result = new List<IntPtr>();
        EnumWindows(delegate(IntPtr h, IntPtr l) {
            uint pid; GetWindowThreadProcessId(h, out pid);
            if (pid != target || !IsWindowVisible(h)) return true;
            RECT r; GetWindowRect(h, out r);
            if ((r.Right - r.Left) > 200 && (r.Bottom - r.Top) > 150) result.Add(h);
            return true;
        }, IntPtr.Zero);
        return result;
    }

    public static string Title(IntPtr h) {
        var text = new StringBuilder(512);
        GetWindowText(h, text, 512);
        return text.ToString();
    }

    public static bool Save(IntPtr h, string path) {
        RECT r; GetWindowRect(h, out r);
        uint dpi = GetDpiForWindow(h);
        if (dpi < 96) dpi = 96;
        float scale = dpi / 96f;
        int width = (int)Math.Ceiling((r.Right - r.Left) * scale) + 4;
        int height = (int)Math.Ceiling((r.Bottom - r.Top) * scale) + 4;
        using (var bmp = new System.Drawing.Bitmap(width, height)) {
            using (var g = System.Drawing.Graphics.FromImage(bmp)) {
                g.Clear(System.Drawing.Color.White);
                IntPtr hdc = g.GetHdc();
                bool ok = PrintWindow(h, hdc, 2);
                g.ReleaseHdc(hdc);
                if (!ok) return false;
            }
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
        return true;
    }
}
'@ -ReferencedAssemblies System.Drawing

Get-Process WinFontReplacer -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

$p = Start-Process -FilePath (Resolve-Path $ExePath) -PassThru
for ($i = 0; $i -lt 50; $i++) {
    Start-Sleep -Milliseconds 200
    $p.Refresh()
    if ($p.MainWindowHandle -ne 0) { break }
}
if ($p.MainWindowHandle -eq 0) { throw 'Main window was not created.' }
Start-Sleep -Seconds 2

$root = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
$condition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty,
    '应用全局字体')
$button = $null
for ($i = 0; $i -lt 15; $i++) {
    $button = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
    if ($button -ne $null) { break }
    Start-Sleep -Milliseconds 300
}
if ($button -eq $null) { throw 'Apply button was not found.' }

[WinFontCapture]::PostMessage(
    [IntPtr]$button.Current.NativeWindowHandle,
    0x00F5,
    [IntPtr]::Zero,
    [IntPtr]::Zero) | Out-Null
Start-Sleep -Seconds 1

$windows = [WinFontCapture]::VisibleWindows([uint32]$p.Id)
$dialog = $windows | Where-Object { $_ -ne $p.MainWindowHandle } | Select-Object -Last 1
if ($dialog -eq $null) { throw 'Confirmation dialog was not found.' }

$assets = Join-Path (Split-Path -Parent $PSScriptRoot) 'assets'
New-Item -ItemType Directory -Force -Path $assets | Out-Null
if (-not [WinFontCapture]::Save($dialog, (Join-Path $assets 'screenshot-dialog.png'))) {
    throw 'PrintWindow failed for dialog.'
}

[WinFontCapture]::PostMessage($dialog, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
Start-Sleep -Milliseconds 700

if (-not [WinFontCapture]::Save($p.MainWindowHandle, (Join-Path $assets 'screenshot-main.png'))) {
    throw 'PrintWindow failed for main window.'
}

$p.CloseMainWindow() | Out-Null
Start-Sleep -Milliseconds 500
if (-not $p.HasExited) { $p.Kill() }

Write-Host 'Captured main window and confirmation dialog.'
