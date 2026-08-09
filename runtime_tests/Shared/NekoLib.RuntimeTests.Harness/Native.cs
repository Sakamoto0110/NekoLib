#nullable enable
using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace NekoLib.RuntimeTests.Harness
{
    /// <summary>
    /// The two Windows facts the environment record needs that the BCL will not
    /// answer truthfully on both target families.
    /// <para/>
    /// <c>Environment.OSVersion</c> lies on <c>net481</c> without a compatibility
    /// manifest — it reports Windows 8 on Windows 11 — and installed physical
    /// memory has no portable API before .NET 5. Both are read from the OS
    /// directly so the same number appears in both builds' evidence.
    /// </summary>
    public static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatusEx
        {
            public uint Length;
            public uint MemoryLoad;
            public ulong TotalPhysical;
            public ulong AvailablePhysical;
            public ulong TotalPageFile;
            public ulong AvailablePageFile;
            public ulong TotalVirtual;
            public ulong AvailableVirtual;
            public ulong AvailableExtendedVirtual;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OsVersionInfoEx
        {
            public uint OSVersionInfoSize;
            public uint MajorVersion;
            public uint MinorVersion;
            public uint BuildNumber;
            public uint PlatformId;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string CSDVersion;

            public ushort ServicePackMajor;
            public ushort ServicePackMinor;
            public ushort SuiteMask;
            public byte ProductType;
            public byte Reserved;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

        [DllImport("ntdll.dll")]
        private static extern int RtlGetVersion(ref OsVersionInfoEx versionInfo);

        /// <summary>Installed physical memory in bytes, or zero when the call fails.</summary>
        public static ulong InstalledPhysicalMemoryBytes()
        {
            MemoryStatusEx status = new MemoryStatusEx();
            status.Length = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));

            try
            {
                return GlobalMemoryStatusEx(ref status) ? status.TotalPhysical : 0UL;
            }
            catch (DllNotFoundException)
            {
                return 0UL;
            }
            catch (EntryPointNotFoundException)
            {
                return 0UL;
            }
        }

        /// <summary>The real Windows version, manifest or not.</summary>
        public static string WindowsVersion()
        {
            OsVersionInfoEx info = new OsVersionInfoEx();
            info.OSVersionInfoSize = (uint)Marshal.SizeOf(typeof(OsVersionInfoEx));

            try
            {
                if (RtlGetVersion(ref info) == 0)
                {
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}.{1}.{2}",
                        info.MajorVersion,
                        info.MinorVersion,
                        info.BuildNumber);
                }
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }

            return Environment.OSVersion.Version.ToString() + " (reported by the BCL; may be manifest-limited)";
        }
    }
}
