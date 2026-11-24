using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

// Placement belongs to the qualification executable, never to the package.
internal static class QualificationWindowPlacement
{
    internal static int X(int fallback) => Coordinate("NEKOLIB_QUALIFICATION_WINDOW_X", fallback);
    internal static int Y(int fallback) => Coordinate("NEKOLIB_QUALIFICATION_WINDOW_Y", fallback);

    private static int Coordinate(string name, int fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrEmpty(value) ? fallback : int.Parse(value, CultureInfo.InvariantCulture);
    }

    internal static void Apply(IntPtr handle)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NEKOLIB_QUALIFICATION_WINDOW_X")) &&
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NEKOLIB_QUALIFICATION_WINDOW_Y"))) return;
        var x = X(0);
        var y = Y(0);
        // Native coordinates avoid WPF DIP / WinForms autoscaling differences.
        if (!SetWindowPos(handle, IntPtr.Zero, x, y, 0, 0, 0x0015) || !GetWindowRect(handle, out var rect))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        if (rect.Left != x || rect.Top != y)
            throw new InvalidOperationException("Qualification window did not reach its requested coordinates.");
        var directory = Environment.GetEnvironmentVariable("NEKOLIB_QUALIFICATION_WINDOW_EVIDENCE");
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, Process.GetCurrentProcess().Id + "-" + handle.ToInt64() + ".txt"),
                "x=" + rect.Left + ";y=" + rect.Top + ";width=" + (rect.Right - rect.Left) + ";height=" + (rect.Bottom - rect.Top));
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { internal int Left, Top, Right, Bottom; }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr handle, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr handle, out Rect rect);
}
