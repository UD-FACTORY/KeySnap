Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;

public class WinCap {
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static void Capture(IntPtr hWnd, string filePath) {
        ShowWindow(hWnd, 9); // SW_RESTORE
        SetForegroundWindow(hWnd);
        System.Threading.Thread.Sleep(300);
        RECT rect;
        GetWindowRect(hWnd, out rect);
        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0) return;
        using (Bitmap bmp = new Bitmap(width, height)) {
            using (Graphics g = Graphics.FromImage(bmp)) {
                g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height));
            }
            bmp.Save(filePath, ImageFormat.Png);
        }
    }
}
"@ -ReferencedAssemblies System.Drawing

Stop-Process -Name KeySnap -Force -ErrorAction SilentlyContinue
Start-Process "d:\Code\QuickReplace\dist\KeySnap.exe"
Start-Sleep -Seconds 2

$proc = Get-Process KeySnap | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($proc) {
    $hwnd = $proc.MainWindowHandle
    # 1. 단축어 관리
    [WinCap]::Capture($hwnd, "d:\Code\QuickReplace\docs\screenshots\01_shortcuts.png")
    Write-Output "Captured 01_shortcuts.png"

    # Tab navigation via keys: Alt + Down or Click
    [System.Windows.Forms.SendKeys]::SendWait("^{TAB}")
    Start-Sleep -Milliseconds 400
}
