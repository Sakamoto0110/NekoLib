using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace NekoLib.Watchdog.Host
{
    internal static class HostFatalLog
    {
        internal const long MaxBytes = 256 * 1024;

        public static void TryWrite(Exception exception)
        {
            try
            {
                int processId;
                using (var process = Process.GetCurrentProcess())
                    processId = process.Id;

                TryWrite(
                    exception,
                    GetDefaultPath(),
                    DateTime.UtcNow,
                    processId);
            }
            catch
            {
                // Fatal reporting must never replace the original startup failure.
            }
        }

        internal static string GetDefaultPath()
            => Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "NekoLib",
                "Watchdog",
                "watchdog-host-fatal.log");

        internal static void TryWrite(
            Exception exception,
            string path,
            DateTime utcNow,
            int processId)
        {
            try
            {
                if (exception == null)
                    throw new ArgumentNullException(nameof(exception));
                if (string.IsNullOrWhiteSpace(path))
                    throw new ArgumentException("Fatal log path is required.", nameof(path));

                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                var entry = string.Format(
                    CultureInfo.InvariantCulture,
                    "[{0:O}] pid={1} {2}",
                    utcNow.ToUniversalTime(),
                    processId,
                    exception);
                entry = BoundEntry(entry);

                RotateIfRequired(path, Encoding.UTF8.GetByteCount(
                    entry + Environment.NewLine));
                File.AppendAllText(path, entry + Environment.NewLine);
            }
            catch
            {
                // Fatal reporting must never replace the original startup failure.
            }
        }

        private static string BoundEntry(string entry)
        {
            var maxCharacters = checked((int)(MaxBytes / 4)) -
                Environment.NewLine.Length;
            return entry.Length <= maxCharacters
                ? entry
                : entry.Substring(0, maxCharacters);
        }

        private static void RotateIfRequired(string path, int incomingBytes)
        {
            if (!File.Exists(path))
                return;

            var currentBytes = new FileInfo(path).Length;
            if (currentBytes + incomingBytes <= MaxBytes)
                return;

            var backup = path + ".1";
            if (File.Exists(backup))
                File.Delete(backup);

            if (currentBytes <= MaxBytes)
            {
                File.Move(path, backup);
                return;
            }

            File.Delete(path);
        }
    }
}
