using System.ComponentModel;
using System.Runtime.InteropServices;
using AutoPlay.Core.Geometry;
using static AutoPlay.Windows.Interop.NativeMethods;

namespace AutoPlay.Windows.Windowing;

/// <summary>Reads and sets window bounds in physical screen pixels, independently of the DPI scaling.</summary>
public static class WindowGeometry
{
    public static PixelRect GetBounds(nint windowHandle)
    {
        if (!GetWindowRect(windowHandle, out var rect))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        return new PixelRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    public static void SetBounds(nint windowHandle, PixelRect bounds)
    {
        if (!SetWindowPos(windowHandle, 0, bounds.Left, bounds.Top, bounds.Width, bounds.Height, SWP_NOZORDER | SWP_NOACTIVATE))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    /// <summary>Returns the work area (screen minus taskbar) of the monitor that shows most of <paramref name="area"/>.</summary>
    public static unsafe PixelRect GetWorkArea(PixelRect area)
    {
        var rect = new RECT { Left = area.Left, Top = area.Top, Right = area.Right, Bottom = area.Bottom };
        var monitor = MonitorFromRect(rect, MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
        if (!GetMonitorInfo(monitor, ref info))
        {
            throw new InvalidOperationException("Unable to read the monitor information.");
        }

        var work = info.rcWork;
        return new PixelRect(work.Left, work.Top, work.Right - work.Left, work.Bottom - work.Top);
    }

    /// <summary>
    /// Excludes the window from screen captures (Windows 10 2004 and later), so that it never disturbs
    /// image matching. Returns false if the system does not support it.
    /// </summary>
    public static bool TryExcludeFromCapture(nint windowHandle) =>
        SetWindowDisplayAffinity(windowHandle, WDA_EXCLUDEFROMCAPTURE);
}
