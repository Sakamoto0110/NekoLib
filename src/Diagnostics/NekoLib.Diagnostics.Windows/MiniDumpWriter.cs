using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace NekoLib.Diagnostics.Windows
{
    internal static class MiniDumpWriter
    {
        [Flags]
        private enum MiniDumpType : uint
        {
            MiniDumpNormal = 0x00000000,
            MiniDumpWithDataSegs = 0x00000001,
            MiniDumpWithFullMemory = 0x00000002,
            MiniDumpWithHandleData = 0x00000004,
            MiniDumpWithThreadInfo = 0x00001000,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINIDUMP_EXCEPTION_INFORMATION
        {
            public uint ThreadId;
            public IntPtr ExceptionPointers;
            [MarshalAs(UnmanagedType.Bool)]
            public bool ClientPointers;
        }

        [DllImport("dbghelp.dll", SetLastError = true)]
        private static extern bool MiniDumpWriteDump(
            IntPtr hProcess,
            uint processId,
            IntPtr hFile,
            MiniDumpType dumpType,
            ref MINIDUMP_EXCEPTION_INFORMATION exceptionParam,
            IntPtr userStreamParam,
            IntPtr callbackParam);

        /// <summary>
        /// Same export, called with a NULL exception parameter. Used when no native
        /// exception is in flight on the calling thread, so the dump does not claim
        /// an exception context that does not exist.
        /// </summary>
        [DllImport("dbghelp.dll", EntryPoint = "MiniDumpWriteDump", SetLastError = true)]
        private static extern bool MiniDumpWriteDumpWithoutException(
            IntPtr hProcess,
            uint processId,
            IntPtr hFile,
            MiniDumpType dumpType,
            IntPtr exceptionParam,
            IntPtr userStreamParam,
            IntPtr callbackParam);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        private static MiniDumpType Map(CrashDumpLevel level)
        {
            switch (level)
            {
                case CrashDumpLevel.MiniDumpNormal: return MiniDumpType.MiniDumpNormal;
                case CrashDumpLevel.WithDataSegs: return MiniDumpType.MiniDumpWithDataSegs;
                case CrashDumpLevel.WithHandleData: return MiniDumpType.MiniDumpWithHandleData;
                case CrashDumpLevel.WithThreadInfo: return MiniDumpType.MiniDumpWithThreadInfo;
                case CrashDumpLevel.WithFullMemory: return MiniDumpType.MiniDumpWithFullMemory;
                default: return MiniDumpType.MiniDumpNormal;
            }
        }

        public static bool TryWrite(string filePath, CrashDumpLevel level)
        {
            if (level == CrashDumpLevel.None) return false;

            bool written = false;

            try
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    written = WriteDump(fs, Map(level));
                }
            }
            catch
            {
                written = false;
            }

            if (!written)
            {
                // FileMode.Create already created - and possibly partly filled - the
                // file. Leaving it behind would put something named crash.dmp next to
                // crash.txt while the bundle reports that no dump was written.
                TryDeleteIncompleteDump(filePath);
            }

            return written;
        }

        private static bool WriteDump(FileStream target, MiniDumpType dumpType)
        {
            var proc = Process.GetCurrentProcess();
            var handle = target.SafeFileHandle.DangerousGetHandle();
            var exceptionPointers = Marshal.GetExceptionPointers();

            if (exceptionPointers == IntPtr.Zero)
            {
                // NekoLib.Diagnostics runs the dump writer on its own contributor
                // thread, so there is usually no exception in flight here. Passing a
                // structure that names this thread and a null pointer would label the
                // dump with a bystander thread; a dump with no exception stream is
                // truthful instead.
                return MiniDumpWriteDumpWithoutException(
                    proc.Handle,
                    (uint)proc.Id,
                    handle,
                    dumpType,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero);
            }

            var info = new MINIDUMP_EXCEPTION_INFORMATION
            {
                ThreadId = GetCurrentThreadId(),
                ExceptionPointers = exceptionPointers,
                ClientPointers = false
            };

            return MiniDumpWriteDump(
                proc.Handle,
                (uint)proc.Id,
                handle,
                dumpType,
                ref info,
                IntPtr.Zero,
                IntPtr.Zero);
        }

        private static void TryDeleteIncompleteDump(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch
            {
            }
        }
    }
}
