using System;
using System.Runtime.InteropServices;

namespace NekoLib.Runtime.Diagnostics
{
    /// <summary>
    /// Best-effort suppression of Windows error UI (WER dialogs / critical error popups).
    /// Call as early as possible in Main().
    /// </summary>
    public static class CrashSuppressor
    {
        [Flags]
        private enum ErrorModes : uint
        {
            SYSTEM_DEFAULT = 0x0,
            SEM_FAILCRITICALERRORS = 0x0001,
            SEM_NOGPFAULTERRORBOX = 0x0002,
            SEM_NOALIGNMENTFAULTEXCEPT = 0x0004,
            SEM_NOOPENFILEERRORBOX = 0x8000
        }

        [DllImport("kernel32.dll")]
        private static extern ErrorModes SetErrorMode(ErrorModes uMode);

        public static void Enable()
        {
            try
            {
                SetErrorMode(
                    ErrorModes.SEM_FAILCRITICALERRORS |
                    ErrorModes.SEM_NOGPFAULTERRORBOX |
                    ErrorModes.SEM_NOOPENFILEERRORBOX);
            }
            catch { }
        }
    }
}
