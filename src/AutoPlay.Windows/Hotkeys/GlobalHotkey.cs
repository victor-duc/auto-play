using System.ComponentModel;
using System.Runtime.InteropServices;
using static AutoPlay.Windows.Interop.NativeMethods;

namespace AutoPlay.Windows.Hotkeys;

/// <summary>
/// A system-wide hotkey registered on a window. The owner must forward the window messages to
/// <see cref="ProcessMessage"/> (e.g. from a WPF HwndSource hook).
/// </summary>
public sealed class GlobalHotkey : IDisposable
{
    private static int s_nextId = 0x1000;

    private readonly nint _windowHandle;
    private readonly int _id;
    private bool _disposed;

    /// <param name="windowHandle">The window receiving WM_HOTKEY.</param>
    /// <param name="virtualKey">Virtual-key code, e.g. 0x7B for F12.</param>
    /// <param name="modifiers">MOD_* flags (Alt = 1, Control = 2, Shift = 4, Win = 8).</param>
    public GlobalHotkey(nint windowHandle, uint virtualKey, uint modifiers = 0)
    {
        _windowHandle = windowHandle;
        _id = Interlocked.Increment(ref s_nextId);
        if (!RegisterHotKey(windowHandle, _id, modifiers | MOD_NOREPEAT, virtualKey))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "The hotkey is already used by another application.");
        }
    }

    public event EventHandler? Pressed;

    /// <summary>Returns true if the message was this hotkey and has been handled.</summary>
    public bool ProcessMessage(int message, nint wParam)
    {
        if (message != WM_HOTKEY || wParam != _id)
        {
            return false;
        }

        Pressed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UnregisterHotKey(_windowHandle, _id);
    }
}
