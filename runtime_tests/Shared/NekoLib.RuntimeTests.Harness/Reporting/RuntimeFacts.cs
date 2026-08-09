#nullable enable
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace NekoLib.RuntimeTests.Harness.Reporting
{
    /// <summary>
    /// Everything about this process and this checkout that the evidence record
    /// has to name.
    /// <para/>
    /// A result that says "SQL Server passed" is worth nothing without the
    /// combination it passed in. Two of these values decide whether a later run
    /// is comparing like with like at all: the repository commit with its dirty
    /// flag, and the exact provider package version.
    /// </summary>
    public static class RuntimeFacts
    {
        public static string TargetFrameworkMoniker =>
#if NET481
            "net481";
#else
            "net9.0";
#endif

        public static string RuntimeDescription
        {
            get
            {
#if NET481
                return ".NET Framework, CLR " + Environment.Version +
                       " (target " + TargetFrameworkMoniker + ")";
#else
                return System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription +
                       " (target " + TargetFrameworkMoniker + ")";
#endif
            }
        }

        public static string ProcessArchitecture =>
            Environment.Is64BitProcess ? "x64" : "x86";

        public static string MachineArchitecture =>
            Environment.Is64BitOperatingSystem ? "x64" : "x86";

        public static int LogicalProcessorCount => Environment.ProcessorCount;

        public static ulong InstalledMemoryBytes => Native.InstalledPhysicalMemoryBytes();

        public static string WindowsVersion => Native.WindowsVersion();

        /// <summary>
        /// Describes the assembly a type came from, read from what is loaded
        /// rather than from a project file, so it reports what actually ran.
        /// <para/>
        /// Scenarios use this to name their provider package and the module
        /// under test. The harness deliberately does not know which those are:
        /// it references nothing but the BCL, and a shared component that had
        /// to be edited every time a scenario chose a different dependency
        /// would not be shared at all.
        /// </summary>
        public static string DescribeAssembly(string label, Type type)
        {
            Assembly assembly = type.Assembly;
            AssemblyInformationalVersionAttribute? informational =
                (AssemblyInformationalVersionAttribute?)Attribute.GetCustomAttribute(
                    assembly, typeof(AssemblyInformationalVersionAttribute));

            string version = informational?.InformationalVersion
                             ?? assembly.GetName().Version?.ToString()
                             ?? "unknown";

            return label + " " + version;
        }

        /// <summary>
        /// The version of the assembly the supplied type lives in.
        /// <para/>
        /// Takes a type rather than reading its own assembly: after this code
        /// moved into a shared harness, "my assembly" stopped meaning "the
        /// scenario" and a self-read would have quietly recorded the harness
        /// version in every scenario's environment file.
        /// </summary>
        public static string AssemblyVersion(Type type) =>
            type.Assembly.GetName().Version?.ToString() ?? "unknown";

        /// <summary>
        /// The repository commit and whether the tree was dirty when the run
        /// started. A dirty tree does not invalidate a run, but a result that
        /// hides it cannot be reproduced.
        /// </summary>
        public static void ReadRepository(out string commit, out bool dirty, out string diagnostic)
        {
            commit = "unknown";
            dirty = false;
            diagnostic = string.Empty;

            string? root = FindRepositoryRoot();
            if (root == null)
            {
                diagnostic = "no repository root found above " + AppContext.BaseDirectory;
                return;
            }

            try
            {
                ProcessResult head = ProcessRunner.Run(
                    "git", "-C \"" + root + "\" rev-parse HEAD", TimeSpan.FromSeconds(30));

                if (head.Succeeded && head.Trimmed.Length > 0)
                    commit = head.Trimmed;
                else
                    diagnostic = "git rev-parse failed: " + head.Diagnostic;

                ProcessResult status = ProcessRunner.Run(
                    "git", "-C \"" + root + "\" status --porcelain", TimeSpan.FromSeconds(60));

                if (status.Succeeded)
                    dirty = status.Trimmed.Length > 0;
                else if (diagnostic.Length == 0)
                    diagnostic = "git status failed: " + status.Diagnostic;
            }
            catch (Exception ex)
            {
                diagnostic = "git is not usable from this process: " + ex.GetType().Name;
            }
        }

        public static string? FindRepositoryRoot()
        {
            DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
                    return directory.FullName;

                directory = directory.Parent;
            }

            return null;
        }

        public static string DescribeBytes(ulong bytes)
        {
            if (bytes == 0) return "unknown";

            double gigabytes = bytes / (1024.0 * 1024.0 * 1024.0);
            return gigabytes.ToString("F1", CultureInfo.InvariantCulture) + " GiB";
        }

        /// <summary>Current process resource counters, sampled cheaply.</summary>
        public static void SampleProcess(
            out long privateBytes,
            out long managedHeapBytes,
            out int threadCount,
            out int handleCount)
        {
            using (Process process = Process.GetCurrentProcess())
            {
                process.Refresh();
                privateBytes = process.PrivateMemorySize64;
                threadCount = process.Threads.Count;
                handleCount = process.HandleCount;
            }

            managedHeapBytes = GC.GetTotalMemory(false);
        }
    }
}
