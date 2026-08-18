using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#if NET9
using System.Text.Json;
#else
using Newtonsoft.Json;
#endif

namespace NekoLib.Watchdog
{
    internal enum CrashBundleOutcome
    {
        NoPendingCrash,
        Complete,
        Partial,
        Failed
    }

    internal sealed class CrashBundleResult
    {
        public CrashBundleResult(
            CrashBundleOutcome outcome,
            string? bundleId,
            IReadOnlyList<string> failures)
        {
            Outcome = outcome;
            BundleId = bundleId;
            Failures = failures;
        }

        public CrashBundleOutcome Outcome { get; }
        public string? BundleId { get; }
        public IReadOnlyList<string> Failures { get; }
    }

    internal static class CrashBundler
    {
        public static CrashBundleResult TryFinalizeLatestCrashBundle(
            CrashBundlerOptions? options,
            string restartReason,
            long restartCount,
            Action<string>? log = null)
        {
            if (options == null)
                return Failed(null, "options", log);

            string? bundleId = null;
            try
            {
                Directory.CreateDirectory(options.PendingCrashRoot);
                Directory.CreateDirectory(options.BundleRoot);

                var pending = new DirectoryInfo(options.PendingCrashRoot)
                    .GetDirectories("crash-*")
                    .OrderByDescending(directory => directory.CreationTimeUtc)
                    .FirstOrDefault();

                if (pending == null)
                {
                    return new CrashBundleResult(
                        CrashBundleOutcome.NoPendingCrash,
                        null,
                        new string[0]);
                }

                bundleId = "bundle-" + DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss-fffZ");
                var bundleDirectory = Path.Combine(options.BundleRoot, bundleId);
                Directory.CreateDirectory(bundleDirectory);
                CopyDirectory(pending.FullName, bundleDirectory);

                var failures = new List<string>();

                TryOptional("watchdog_status", failures, () =>
                {
                    var status = options.GetWatchdogStatus?.Invoke();
                    if (!string.IsNullOrWhiteSpace(status))
                    {
                        File.WriteAllText(
                            Path.Combine(bundleDirectory, "watchdog-status.txt"),
                            status);
                    }
                });

                var watchdogLogPath = options.WatchdogLogPath;
                if (options.CopyWatchdogLogTail &&
                    !string.IsNullOrWhiteSpace(watchdogLogPath) &&
                    File.Exists(watchdogLogPath))
                {
                    TryOptional("watchdog_log_tail", failures, () =>
                        TailFileLines(
                            watchdogLogPath!,
                            Path.Combine(bundleDirectory, "watchdog.log.tail"),
                            options.TailLines));
                }

                if (options.EnableManifests)
                {
                    TryOptional("manifest", failures, () =>
                        WriteManifest(
                            options,
                            bundleDirectory,
                            bundleId,
                            restartReason,
                            restartCount,
                            failures));
                }

                TryOptional("pending_cleanup", failures, () => pending.Delete(true));
                if (!EnforceMaxBundles(options.BundleRoot, options.MaxBundles))
                    failures.Add("bundle_retention");

                var outcome = failures.Count == 0
                    ? CrashBundleOutcome.Complete
                    : CrashBundleOutcome.Partial;
                SafeLog(
                    log,
                    outcome == CrashBundleOutcome.Complete
                        ? "[bundler] finalized " + bundleId
                        : "[bundler] partial " + bundleId + " (" + string.Join(",", failures) + ")");

                return new CrashBundleResult(outcome, bundleId, failures.ToArray());
            }
            catch (Exception ex)
            {
                return Failed(bundleId, ex.GetType().Name + ": " + ex.Message, log);
            }
        }

        private static CrashBundleResult Failed(
            string? bundleId,
            string failure,
            Action<string>? log)
        {
            SafeLog(log, "[bundler] failed " + failure);
            return new CrashBundleResult(
                CrashBundleOutcome.Failed,
                bundleId,
                new[] { failure });
        }

        private static void TryOptional(
            string name,
            ICollection<string> failures,
            Action action)
        {
            try
            {
                action();
            }
            catch
            {
                failures.Add(name);
            }
        }

        private static void SafeLog(Action<string>? log, string message)
        {
            try
            {
                log?.Invoke(message);
            }
            catch
            {
                // Reporting is never allowed to replace the finalization outcome.
            }
        }

        private static void WriteManifest(
            CrashBundlerOptions options,
            string bundleDirectory,
            string bundleId,
            string restartReason,
            long restartCount,
            ICollection<string> failures)
        {
            var files = Directory.GetFiles(bundleDirectory)
                .Select(path => new FileInfo(path))
                .OrderBy(file => file.Name)
                .ToList();
            var manifestFiles = new List<Dictionary<string, object?>>(files.Count);

            foreach (var file in files)
            {
                var item = new Dictionary<string, object?>
                {
                    { "path", file.Name },
                    { "size", file.Length }
                };
                if (options.EnableChecksums)
                    item.Add("sha256", CrashChecksums.Sha256Hex(file.FullName));
                manifestFiles.Add(item);
            }

            var appVersion = SafeCall(
                options.GetAppVersion,
                "application_version",
                failures);
            var watchdogVersion = SafeCall(
                options.GetWatchdogVersion,
                "watchdog_version",
                failures);
            var manifest = new Dictionary<string, object?>
            {
                { "schemaVersion", 1 },
                { "bundleId", bundleId },
                { "timestampUtc", DateTime.UtcNow.ToString("O") },
                { "application", new Dictionary<string, object?> { { "version", appVersion } } },
                {
                    "watchdog",
                    new Dictionary<string, object?>
                    {
                        { "version", watchdogVersion },
                        { "restartReason", restartReason },
                        { "restartCount", restartCount }
                    }
                },
                { "checksums", options.EnableChecksums },
                { "files", manifestFiles }
            };

#if NET9
            var json = JsonSerializer.Serialize(
                manifest,
                new JsonSerializerOptions { WriteIndented = true });
#else
            var json = JsonConvert.SerializeObject(manifest, Formatting.Indented);
#endif
            File.WriteAllText(Path.Combine(bundleDirectory, "manifest.json"), json);
        }

        private static string? SafeCall(
            Func<string?>? callback,
            string failureName,
            ICollection<string> failures)
        {
            if (callback == null)
                return null;

            try
            {
                return callback();
            }
            catch
            {
                failures.Add(failureName);
                return null;
            }
        }

        private static bool EnforceMaxBundles(string bundleRoot, int maxBundles)
        {
            if (maxBundles <= 0)
                return true;

            try
            {
                var directories = new DirectoryInfo(bundleRoot)
                    .GetDirectories("bundle-*")
                    .OrderByDescending(directory => directory.CreationTimeUtc)
                    .ToList();
                var complete = true;
                for (var index = maxBundles; index < directories.Count; index++)
                {
                    try
                    {
                        directories[index].Delete(true);
                    }
                    catch
                    {
                        complete = false;
                    }
                }

                return complete;
            }
            catch
            {
                return false;
            }
        }

        private static void CopyDirectory(string sourceDirectory, string targetDirectory)
        {
            Directory.CreateDirectory(targetDirectory);
            foreach (var file in Directory.GetFiles(sourceDirectory))
            {
                File.Copy(
                    file,
                    Path.Combine(targetDirectory, Path.GetFileName(file)),
                    true);
            }

            foreach (var directory in Directory.GetDirectories(sourceDirectory))
            {
                CopyDirectory(
                    directory,
                    Path.Combine(targetDirectory, Path.GetFileName(directory)));
            }
        }

        private static void TailFileLines(string source, string target, int lines)
        {
            if (lines <= 0)
                return;

            var tail = new Queue<string>(lines);
            foreach (var line in File.ReadLines(source))
            {
                if (tail.Count == lines)
                    tail.Dequeue();
                tail.Enqueue(line);
            }

            File.WriteAllLines(target, tail);
        }
    }
}
