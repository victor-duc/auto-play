using System.ComponentModel;
using System.Runtime.InteropServices;
using AutoPlay.Core.Abstractions;
using static AutoPlay.Windows.Interop.NativeMethods;

namespace AutoPlay.Windows.Power;

/// <summary>
/// Keeps the system awake and the display on with a power request. Unlike SetThreadExecutionState,
/// a power request is not tied to the calling thread, which suits asynchronous code.
/// </summary>
public sealed class WindowsPowerManager : IPowerManager
{
    private const string Reason = "AutoPlay is running a sequence.";

    public IDisposable PreventSleep()
    {
        var reason = Marshal.StringToHGlobalUni(Reason);
        try
        {
            var context = new REASON_CONTEXT
            {
                Version = POWER_REQUEST_CONTEXT_VERSION,
                Flags = POWER_REQUEST_CONTEXT_SIMPLE_STRING,
                SimpleReasonString = reason,
            };

            var request = PowerCreateRequest(ref context);
            if (request == -1)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            var handle = new PowerRequest(request);
            handle.Set(POWER_REQUEST_TYPE.PowerRequestSystemRequired);
            handle.Set(POWER_REQUEST_TYPE.PowerRequestDisplayRequired);
            return handle;
        }
        finally
        {
            Marshal.FreeHGlobal(reason);
        }
    }

    private sealed class PowerRequest(nint handle) : IDisposable
    {
        private readonly List<POWER_REQUEST_TYPE> _activeTypes = [];
        private bool _disposed;

        public void Set(POWER_REQUEST_TYPE type)
        {
            if (!PowerSetRequest(handle, type))
            {
                var error = Marshal.GetLastPInvokeError();
                Dispose();
                throw new Win32Exception(error);
            }

            _activeTypes.Add(type);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var type in _activeTypes)
            {
                PowerClearRequest(handle, type);
            }

            CloseHandle(handle);
        }
    }
}
