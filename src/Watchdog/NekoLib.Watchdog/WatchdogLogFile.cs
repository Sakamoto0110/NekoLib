using System;
using System.IO;
using System.Text;

namespace NekoLib.Watchdog
{
    /// <summary>
    /// Bounded append-only text log with single-backup rotation.
    ///
    /// <para>
    /// The watchdog's file log previously grew without limit — <c>WatchdogOptions.MaxLogBytes</c>
    /// was defined but never honored. This helper enforces that cap: when an append would
    /// push the active file past <paramref name="maxBytes"/>, the current file is rolled to
    /// "<c>&lt;path&gt;.1</c>" (replacing any previous backup) and a fresh active file is started.
    /// Total on-disk usage is therefore bounded to roughly 2 × <paramref name="maxBytes"/>.
    /// </para>
    ///
    /// <para>
    /// Callers are responsible for serializing concurrent writes to the same path; the
    /// watchdog runtime does this under its own log lock. A single oversized line is still
    /// written in full (lines are never split) after rotation.
    /// </para>
    /// </summary>
    internal static class WatchdogLogFile
    {
        /// <summary>
        /// Appends <paramref name="line"/> (with a trailing newline) to <paramref name="path"/>,
        /// rotating first if the file would exceed <paramref name="maxBytes"/>.
        /// A <paramref name="maxBytes"/> of 0 or less disables rotation (unbounded growth).
        /// </summary>
        /// <returns><c>true</c> if a rotation occurred during this call.</returns>
        public static bool Append(string path, string line, long maxBytes)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            var payload = (line ?? string.Empty) + Environment.NewLine;
            bool rotated = false;

            if (maxBytes > 0 && File.Exists(path))
            {
                long current = new FileInfo(path).Length;
                long incoming = Encoding.UTF8.GetByteCount(payload);

                if (current > 0 && current + incoming > maxBytes)
                {
                    var backup = path + ".1";

                    try { if (File.Exists(backup)) File.Delete(backup); }
                    catch { /* best-effort: a stuck backup must not block logging */ }

                    try
                    {
                        File.Move(path, backup);   // active file becomes the single rolling backup
                        rotated = true;
                    }
                    catch
                    {
                        // If the move fails (file locked, etc.) fall through and keep appending
                        // to the existing file rather than dropping the line.
                    }
                }
            }

            File.AppendAllText(path, payload);
            return rotated;
        }
    }
}
