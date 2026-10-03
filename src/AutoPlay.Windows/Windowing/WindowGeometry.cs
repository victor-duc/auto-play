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
}
