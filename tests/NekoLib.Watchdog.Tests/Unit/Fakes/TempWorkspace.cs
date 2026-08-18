using System;
using System.IO;

namespace NekoLib.Watchdog.Tests.Unit.Fakes
{
    /// <summary>
    /// A throwaway directory under the OS temp path, deleted (best-effort) on
    /// <see cref="Dispose"/>. Watchdog configuration capture and
    /// <c>CrashBundler</c> both touch the real filesystem — they create crash /
    /// bundle / update directories and copy files around — so the tests that
    /// exercise them need a sandbox they own and can clean up.
    /// </summary>
    internal sealed class TempWorkspace : IDisposable
    {
        public string Root { get; }

        public TempWorkspace()
        {
            Root = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "NekoWdgTests_" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(Root);
        }

        /// <summary>Absolute path under the workspace (does not create it).</summary>
        public string Path(params string[] parts)
        {
            var p = Root;
            foreach (var part in parts)
                p = System.IO.Path.Combine(p, part);
            return p;
        }

        /// <summary>Creates a file with <paramref name="content"/> and returns its full path.</summary>
        public string WriteFile(string relativePath, string content = "")
        {
            var full = Path(relativePath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full));
            File.WriteAllText(full, content);
            return full;
        }

        /// <summary>Creates a directory and returns its full path.</summary>
        public string CreateDir(string relativePath)
        {
            var full = Path(relativePath);
            Directory.CreateDirectory(full);
            return full;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                    Directory.Delete(Root, recursive: true);
            }
            catch
            {
                // best-effort: a leaked temp dir must never fail a test run
            }
        }
    }
}
