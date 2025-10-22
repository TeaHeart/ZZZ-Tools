namespace ZZZScanner.Helpers;

using System.Diagnostics;
using System.Drawing;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

public class WindowHelper
{
    private readonly HWND _hWnd;
    private readonly Rectangle _windowRect;

    public WindowHelper(string processName)
    {
        if (!PInvoke.SetProcessDPIAware())
        {
            throw new InvalidOperationException("设置DPI感应失败");
        }

        var hWnd = GetWindowByProcessName(processName);
        if (hWnd == IntPtr.Zero)
        {
            throw new ArgumentException($"未找到进程的窗口 {processName}", nameof(processName));
        }

        _hWnd = (HWND)hWnd;

        SetVisible();

        _windowRect = GetClientRect(_hWnd);
    }

    public void SetVisible(bool setForeground = false)
    {
        if (!PInvoke.ShowWindow(_hWnd, SHOW_WINDOW_CMD.SW_RESTORE))
        {
            throw new InvalidOperationException("窗口恢复失败");
        }

        if (setForeground && !PInvoke.SetForegroundWindow(_hWnd))
        {
            throw new InvalidOperationException("窗口前置失败");
        }
    }

    public void LeftClick(float fx, float fy, int duration = 0)
    {
        var (x, y) = ToAbsolute(fx, fy);
        PInvoke.SetCursorPos(x, y);
        PInvoke.mouse_event(MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
        if (duration > 0)
        {
            Thread.Sleep(duration);
        }
        PInvoke.mouse_event(MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
    }

    public void MouseWheel(int delta)
    {
        PInvoke.mouse_event(MOUSE_EVENT_FLAGS.MOUSEEVENTF_WHEEL, 0, 0, delta, 0);
    }

    public Bitmap GetImage(float fx, float fy, float fw, float fh)
    {
        var (x, y, w, h) = ToAbsolute(fx, fy, fw, fh);
        var image = new Bitmap(w, h);
        using var graph = Graphics.FromImage(image);
        graph.CopyFromScreen(x, y, 0, 0, image.Size);
        return image;
    }

    public Color GetPixel(float fx, float fy)
    {
        var (x, y) = ToAbsolute(fx, fy);
        using var image = new Bitmap(1, 1);
        using var graph = Graphics.FromImage(image);
        graph.CopyFromScreen(x, y, 0, 0, image.Size);
        return image.GetPixel(0, 0);
    }

    public (int X, int Y) ToAbsolute(float fx, float fy, bool clientToScreen = true)
    {
        var x = (int)(fx * _windowRect.Width);
        var y = (int)(fy * _windowRect.Height);
        return clientToScreen ? (_windowRect.X + x, _windowRect.Y + y) : (x, y);
    }

    public (int X, int Y, int Width, int Height) ToAbsolute(float fx, float fy, float fw, float fh, bool clientToScreen = true)
    {
        var (x, y) = ToAbsolute(fx, fy, clientToScreen);
        var w = (int)(fw * _windowRect.Width);
        var h = (int)(fh * _windowRect.Height);
        return (x, y, w, h);
    }

    private static Rectangle GetClientRect(HWND hWnd)
    {
        if (!PInvoke.GetClientRect(hWnd, out RECT lpRect))
        {
            throw new InvalidOperationException("无法获取客户端尺寸");
        }

        Rectangle rect = lpRect;
        var point = rect.Location;
        if (!PInvoke.ClientToScreen(hWnd, ref point))
        {
            throw new InvalidOperationException("无法转换坐标");
        }

        rect.Location = point;
        return rect;
    }

    private static IntPtr GetWindowByProcessName(string processName)
    {
        var processes = Process.GetProcesses();
        var hWnd = IntPtr.Zero;

        foreach (var item in processes)
        {
            if (hWnd == IntPtr.Zero)
            {
                if (string.Equals(item.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
                {
                    if (item.MainWindowHandle != IntPtr.Zero)
                    {
                        hWnd = item.MainWindowHandle;
                    }
                }
            }

            item.Dispose();
        }

        return hWnd;
    }
}
