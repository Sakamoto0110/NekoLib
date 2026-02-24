using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NekoLib.Runtime.Watchdog
{
    /// <summary>
    /// Watchdog-side bundler:
    /// - Moves/copies newest pending app crash folder (created by the app) into a final bundle
    /// - Adds watchdog status snapshot and watchdog log tail
    /// - Writes manifest.json
    /// - Optionally includes checksums (auto-disabled if CrashChecksums.cs is removed)
    /// - Enforces MaxBundles
    /// </summary>
    public static class CrashBundler
    {
        public static void TryFinalizeLatestCrashBundle(
            CrashBundlerOptions o,
            string restartReason,
            long restartCount,
            Action<string> log = null)
        {
            if (o == null) return;

            try
            {
                Directory.CreateDirectory(o.PendingCrashRoot);
                Directory.CreateDirectory(o.BundleRoot);

                // Find newest pending crash folder: crash-YYYY...
                var pending = new DirectoryInfo(o.PendingCrashRoot)
                    .GetDirectories("crash-*")
                    .OrderByDescending(d => d.CreationTimeUtc)
                    .FirstOrDefault();

                if (pending == null) return;

                var bundleId = "bundle-" + DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss-fffZ");
                var bundleDir = Path.Combine(o.BundleRoot, bundleId);
                Directory.CreateDirectory(bundleDir);

                // Copy app crash artifacts
                CopyDirectory(pending.FullName, bundleDir);

                // Add watchdog status snapshot
                try
                {
                    var status = o.GetWatchdogStatus != null ? o.GetWatchdogStatus() : null;
                    if (!string.IsNullOrWhiteSpace(status))
                        File.WriteAllText(Path.Combine(bundleDir, "watchdog-status.txt"), status);
                }
                catch { }

                // Add watchdog log tail
                if (o.CopyWatchdogLogTail && !string.IsNullOrWhiteSpace(o.WatchdogLogPath) && File.Exists(o.WatchdogLogPath))
                {
                    try
                    {
                        TailFileLines(o.WatchdogLogPath, Path.Combine(bundleDir, "watchdog.log.tail"), o.TailLines);
                    }
                    catch { }
                }

                // Manifest (+ optional checksums)
                if (o.EnableManifests)
                {
                    try
                    {
                        WriteManifest(o, bundleDir, bundleId, restartReason, restartCount);
                    }
                    catch { }
                }

                // After successful copy, you can delete pending folder (or keep it if you want)
                try { pending.Delete(true); } catch { }

                // Enforce MaxBundles (hard limit)
                EnforceMaxBundles(o.BundleRoot, o.MaxBundles);

                log?.Invoke("[bundler] finalized " + bundleId);
            }
            catch (Exception ex)
            {
                log?.Invoke("[bundler] error " + ex.Message);
            }
        }

        private static void WriteManifest(CrashBundlerOptions o, string bundleDir, string bundleId, string restartReason, long restartCount)
        {
            var files = Directory.GetFiles(bundleDir)
                .Select(p => new FileInfo(p))
                .OrderBy(f => f.Name)
                .ToList();

            bool checksumsEnabled = o.EnableChecksums;

            var sb = new StringBuilder(16 * 1024);
            sb.AppendLine("{");
            sb.AppendLine("  \"schemaVersion\": 1,");
            sb.AppendLine("  \"bundleId\": " + Json(bundleId) + ",");
            sb.AppendLine("  \"timestampUtc\": " + Json(DateTime.UtcNow.ToString("O")) + ",");
            sb.AppendLine();

            sb.AppendLine("  \"application\": {");
            sb.AppendLine("    \"version\": " + Json(SafeCall(o.GetAppVersion)) + "");
            sb.AppendLine("  },");
            sb.AppendLine();

            sb.AppendLine("  \"watchdog\": {");
            sb.AppendLine("    \"version\": " + Json(SafeCall(o.GetWatchdogVersion)) + ",");
            sb.AppendLine("    \"restartReason\": " + Json(restartReason) + ",");
            sb.AppendLine("    \"restartCount\": " + restartCount);
            sb.AppendLine("  },");
            sb.AppendLine();

            sb.AppendLine("  \"checksums\": " + (checksumsEnabled ? "true" : "false") + ",");
            sb.AppendLine("  \"files\": [");

            for (int i = 0; i < files.Count; i++)
            {
                var f = files[i];
                var rel = f.Name;

                string sha = null;
                if (checksumsEnabled)
                    sha = CrashChecksums.Sha256Hex(f.FullName);

                sb.AppendLine("    {");
                sb.AppendLine("      \"path\": " + Json(rel) + ",");
                sb.AppendLine("      \"size\": " + f.Length + (checksumsEnabled ? "," : ""));
                if (checksumsEnabled)
                    sb.AppendLine("      \"sha256\": " + Json(sha));
                sb.Append("    }");
                if (i != files.Count - 1) sb.Append(",");
                sb.AppendLine();
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");

            File.WriteAllText(Path.Combine(bundleDir, "manifest.json"), sb.ToString());
        }

        private static string SafeCall(Func<string> f)
        {
            try { return f != null ? f() : null; } catch { return null; }
        }

        private static string Json(string s)
        {
            if (string.IsNullOrEmpty(s)) return "null";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static void EnforceMaxBundles(string bundleRoot, int maxBundles)
        {
            if (maxBundles <= 0) return;

            try
            {
                var dirs = new DirectoryInfo(bundleRoot)
                    .GetDirectories("bundle-*")
                    .OrderByDescending(d => d.CreationTimeUtc)
                    .ToList();

                for (int i = maxBundles; i < dirs.Count; i++)
                {
                    try { dirs[i].Delete(true); } catch { }
                }
            }
            catch { }
        }

        private static void CopyDirectory(string srcDir, string dstDir)
        {
            Directory.CreateDirectory(dstDir);

            foreach (var file in Directory.GetFiles(srcDir))
            {
                var dst = Path.Combine(dstDir, Path.GetFileName(file));
                File.Copy(file, dst, true);
            }

            foreach (var dir in Directory.GetDirectories(srcDir))
            {
                var name = Path.GetFileName(dir);
                CopyDirectory(dir, Path.Combine(dstDir, name));
            }
        }

        private static void TailFileLines(string src, string dst, int lines)
        {
            if (lines <= 0) return;

            var q = new Queue<string>(lines);
            foreach (var line in File.ReadLines(src))
            {
                if (q.Count == lines) q.Dequeue();
                q.Enqueue(line);
            }
            File.WriteAllLines(dst, q);
        }
    }
}
