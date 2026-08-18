using System;
using System.Runtime.InteropServices;

namespace NekoLib.Diagnostics.Windows
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

        [DllImport("kernel32.dll")]
        private static extern ErrorModes GetErrorMode();

        /// <summary>
        /// Suppresses the interactive Windows error UI for the rest of the process
        /// lifetime. The effect is process-wide, is not nestable, and cannot be
        /// restored. It hides the error UI only; it does not stop WER from generating
        /// or queueing a report.
        /// </summary>
        public static void Enable()
        {
            try
            {
                // Merge rather than replace: the host and other components may have
                // set flags this library did not, and discarding them would silently
                // change process behaviour that was not ours to change.
                SetErrorMode(
                    GetErrorMode() |
                    ErrorModes.SEM_FAILCRITICALERRORS |
                    ErrorModes.SEM_NOGPFAULTERRORBOX |
                    ErrorModes.SEM_NOOPENFILEERRORBOX);
            }
            catch { }
        }
    }
}
