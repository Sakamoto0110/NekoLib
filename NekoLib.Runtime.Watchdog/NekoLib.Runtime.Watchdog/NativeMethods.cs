using System;
using System.Runtime.InteropServices;

namespace NekoLib.Runtime.Watchdog
{
    internal static class NativeMethods
    {
        internal const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern bool TranslateMessage([In] ref MSG lpMsg);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern IntPtr DispatchMessage([In] ref MSG lpMsg);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern void PostQuitMessage(int nExitCode);

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }
    }
}
