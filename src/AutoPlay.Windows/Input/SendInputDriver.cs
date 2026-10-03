using System.ComponentModel;
using System.Runtime.InteropServices;
using AutoPlay.Core.Abstractions;
using AutoPlay.Core.Geometry;
using static AutoPlay.Windows.Interop.NativeMethods;

namespace AutoPlay.Windows.Input;

/// <summary>Clicks by moving the real cursor and injecting mouse button events with SendInput.</summary>
public sealed class SendInputDriver : IInputDriver
{
    private static readonly TimeSpan PressDuration = TimeSpan.FromMilliseconds(60);

    public void Click(PixelPoint point)
    {
        if (!SetCursorPos(point.X, point.Y))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        Send(MOUSEEVENTF_LEFTDOWN);
        Thread.Sleep(PressDuration);
        Send(MOUSEEVENTF_LEFTUP);
    }

    public PixelPoint GetCursorPosition()
    {
        if (!GetCursorPos(out var point))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        return new PixelPoint(point.X, point.Y);
    }

    private static void Send(uint flags)
    {
        INPUT[] inputs = [new INPUT { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = flags } }];
        if (SendInput(1, inputs, Marshal.SizeOf<INPUT>()) != 1)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }
}
