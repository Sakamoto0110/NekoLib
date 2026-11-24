using System;
using System.Runtime.InteropServices;

namespace NekoLib.Watchdog
{
    internal enum UiWindowState { NoWindow, Responding, Unresponsive, Unavailable }

    internal readonly struct UiWindowSample
    {
        internal UiWindowSample(UiWindowState state, long window = 0, int error = 0)
        { State = state; Window = window; Error = error; }
        internal UiWindowState State { get; }
        internal long Window { get; }
        internal int Error { get; }
    }

    // A native, external probe: no application worker or IPC heartbeat can make
    // a blocked window thread answer this message. The cached HWND survives
    // Windows hiding the original window behind a hung-application ghost.
    internal sealed class UiWindowProbe
    {
        private int _pid;
        private IntPtr _window;
        internal const int TimeoutMs = 100;

        internal UiWindowSample Check(int pid)
        {
            if (_pid != pid) { _pid = pid; _window = IntPtr.Zero; }
            if (!BelongsToTarget(_window, pid))
            {
                _window = IntPtr.Zero;
                SetLastError(0);
                var enumerated = EnumWindows((window, unused) =>
                {
                    if (!IsWindowVisible(window) || !BelongsToTarget(window, pid)) return true;
                    _window = window;
                    return false;
                }, IntPtr.Zero);
                if (_window == IntPtr.Zero && !enumerated)
                    return new UiWindowSample(UiWindowState.Unavailable, error: Marshal.GetLastWin32Error());
            }
            if (_window == IntPtr.Zero) return new UiWindowSample(UiWindowState.NoWindow);

            SetLastError(0);
            var acknowledged = SendMessageTimeoutW(_window, 0 /* WM_NULL */, UIntPtr.Zero,
                IntPtr.Zero, 0x0001 | 0x0002 | 0x0020, TimeoutMs, out _);
            var error = Marshal.GetLastWin32Error();
            if (!BelongsToTarget(_window, pid))
            {
                _window = IntPtr.Zero;
                return new UiWindowSample(UiWindowState.NoWindow);
            }
            if (acknowledged != IntPtr.Zero)
                return new UiWindowSample(UiWindowState.Responding, _window.ToInt64());
            // No acknowledgement with an intact target HWND is a failed probe.
            // Explicit access/interop failures are not evidence of a UI hang.
            return new UiWindowSample(error == 0 || error == 1460
                ? UiWindowState.Unresponsive : UiWindowState.Unavailable, _window.ToInt64(), error);
        }

        private static bool BelongsToTarget(IntPtr window, int pid)
            => window != IntPtr.Zero && IsWindow(window) &&
               GetWindowThreadProcessId(window, out var owner) != 0 && owner == (uint)pid;

        private delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("user32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern IntPtr SendMessageTimeoutW(IntPtr window, uint message, UIntPtr wParam,
            IntPtr lParam, uint flags, uint timeout, out UIntPtr result);
        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern void SetLastError(uint error);
    }
}
