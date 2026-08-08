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

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var proc = Process.GetCurrentProcess();

                    var info = new MINIDUMP_EXCEPTION_INFORMATION
                    {
                        ThreadId = GetCurrentThreadId(),
                        ExceptionPointers = Marshal.GetExceptionPointers(),
                        ClientPointers = false
                    };

                    var dumpType = Map(level);

                    return MiniDumpWriteDump(
                        proc.Handle,
                        (uint)proc.Id,
                        fs.SafeFileHandle.DangerousGetHandle(),
                        dumpType,
                        ref info,
                        IntPtr.Zero,
                        IntPtr.Zero);
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
