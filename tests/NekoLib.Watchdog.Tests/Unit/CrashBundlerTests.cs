using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NekoLib.Watchdog.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Watchdog.Tests.Unit
{
    /// <summary>
    /// Unit tests for <see cref="CrashBundler.TryFinalizeLatestCrashBundle"/>.
    /// The bundler is pure filesystem orchestration — it promotes the newest
    /// pending <c>crash-*</c> folder into a final <c>bundle-*</c>, copies the
    /// app's artifacts, optionally appends a watchdog status snapshot + log tail,
    /// writes a manifest (with optional SHA256 checksums), deletes the pending
    /// folder, and enforces a hard <c>MaxBundles</c> retention cap.
    ///
    /// <para>
    /// These run entirely against a <see cref="TempWorkspace"/>; the checksum
    /// assertions also indirectly cover the internal <c>CrashChecksums</c> helper
    /// (which is not otherwise reachable from the test assembly).
    /// </para>
    /// </summary>
    public class CrashBundlerTests
    {
        private static CrashBundlerOptions OptionsFor(TempWorkspace ws)
        {
            return new CrashBundlerOptions
            {
                PendingCrashRoot = ws.Path("crash", "pending"),
                BundleRoot = ws.Path("crash", "bundles"),
                EnableManifests = true,
                EnableChecksums = true,
                CopyWatchdogLogTail = false,
                MaxBundles = 10
            };
        }

        private static string[] BundleDirs(CrashBundlerOptions o)
        {
            if (!Directory.Exists(o.BundleRoot))
                return Array.Empty<string>();

            return Directory.GetDirectories(o.BundleRoot, "bundle-*");
        }

        private static string SingleBundle(CrashBundlerOptions o)
        {
            var dirs = BundleDirs(o);
            Assert.Single(dirs);
            return dirs[0];
        }

        private static string Sha256Hex(string content)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(content));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        // -----------------------------------------------------------------
        // Guard clauses
        // -----------------------------------------------------------------

        [Fact]
        public void NullOptions_DoesNotThrow()
        {
            var ex = Record.Exception(() =>
                CrashBundler.TryFinalizeLatestCrashBundle(null, "reason", 0));

            Assert.Null(ex);
        }

        [Fact]
        public void NoPendingFolder_CreatesNoBundle()
        {
            using var ws = new TempWorkspace();
            var o = OptionsFor(ws);

            CrashBundler.TryFinalizeLatestCrashBundle(o, "reason", 0);

            Assert.Empty(BundleDirs(o));
        }

        // -----------------------------------------------------------------
        // Happy path: copy + promote + delete pending
        // -----------------------------------------------------------------

        [Fact]
        public void PendingFolder_PromotedToBundle_WithCopiedArtifacts()
        {
            using var ws = new TempWorkspace();
            var o = OptionsFor(ws);
            ws.WriteFile(System.IO.Path.Combine("crash", "pending", "crash-001", "dump.txt"), "boom");

            CrashBundler.TryFinalizeLatestCrashBundle(o, "reason", 0);

            var bundle = SingleBundle(o);
            Assert.True(File.Exists(System.IO.Path.Combine(bundle, "dump.txt")));
            Assert.Equal("boom", File.ReadAllText(System.IO.Path.Combine(bundle, "dump.txt")));
        }

        [Fact]
        public void Finalize_DeletesPendingFolder()
        {
            using var ws = new TempWorkspace();
            var o = OptionsFor(ws);
            var pending = ws.Path("crash", "pending", "crash-001");
            ws.WriteFile(System.IO.Path.Combine("crash", "pending", "crash-001", "dump.txt"), "boom");

            CrashBundler.TryFinalizeLatestCrashBundle(o, "reason", 0);

            Assert.False(Directory.Exists(pending));
        }

        [Fact]
        public void Finalize_PicksNewestPendingFolder()
        {
            using var ws = new TempWorkspace();
            var o = OptionsFor(ws);

            ws.WriteFile(System.IO.Path.Combine("crash", "pending", "crash-old", "marker.txt"), "OLD");
            var older = ws.Path("crash", "pending", "crash-old");
            Directory.SetCreationTimeUtc(older, DateTime.UtcNow.AddMinutes(-30));

            ws.WriteFile(System.IO.Path.Combine("crash", "pending", "crash-new", "marker.txt"), "NEW");
            var newer = ws.Path("crash", "pending", "crash-new");
            Directory.SetCreationTimeUtc(newer, DateTime.UtcNow);

            CrashBundler.TryFinalizeLatestCrashBundle(o, "reason", 0);

            var bundle = SingleBundle(o);
            Assert.Equal("NEW", File.ReadAllText(System.IO.Path.Combine(bundle, "marker.txt")));
        }

        // -----------------------------------------------------------------
        // Manifest
        // -----------------------------------------------------------------

        [Fact]
        public void Manifest_Written_WhenEnabled()
        {
            using var ws = new TempWorkspace();
            var o = OptionsFor(ws);
            ws.WriteFile(System.IO.Path.Combine("crash", "pending", "crash-001", "dump.txt"), "boom");

            CrashBundler.TryFinalizeLatestCrashBundle(o, "crash-loop", 7);

            var manifest = File.ReadAllText(System.IO.Path.Combine(SingleBundle(o), "manifest.json"));
            Assert.Contains("\"schemaVersion\": 1", manifest);
            Assert.Contains("\"restartReason\": \"crash-loop\"", manifest);
            Assert.Contains("\"restartCount\": 7", manifest);
        }

        [Fact]
        public void Manifest_NotWritten_WhenDisabled()
        {
            using var ws = new TempWorkspace();
            var o = OptionsFor(ws);
            o.EnableManifests = false;
            ws.WriteFile(System.IO.Path.Combine("crash", "pending", "crash-001", "dump.txt"), "boom");

            CrashBundler.TryFinalizeLatestCrashBundle(o, "reason", 0);

            Assert.False(File.Exists(System.IO.Path.Combine(SingleBundle(o), "manifest.json")));
        }

        [Fact]
        public void Manifest_IncludesChecksum_MatchingFileContent()
        {
            using var ws = new TempWorkspace();
            var o = OptionsFor(ws);
            o.EnableChecksums = true;
            ws.WriteFile(System.IO.Path.Combine("crash", "pending", "crash-001", "dump.txt"), "boom");

            CrashBundler.TryFinalizeLatestCrashBundle(o, "reason", 0);

            var manifest = File.ReadAllText(System.IO.Path.Combine(SingleBundle(o), "manifest.json"));
            Assert.Contains("\"checksums\": true", manifest);
            Assert.Contains(Sha256Hex("boom"), manifest);
        }

        [Fact]
        public void Manifest_OmitsChecksums_WhenDisabled()
        {
            using var ws = new TempWorkspace();
            var o = OptionsFor(ws);
            o.EnableChecksums = false;
            ws.WriteFile(System.IO.Path.Combine("crash", "pending", "crash-001", "dump.txt"), "boom");

            CrashBundler.TryFinalizeLatestCrashBundle(o, "reason", 0);

            var manifest = File.ReadAllText(System.IO.Path.Combine(SingleBundle(o), "manifest.json"));
            Assert.Contains("\"checksums\": false", manifest);
            Assert.DoesNotContain("\"sha256\"", manifest);
        }

        // -----------------------------------------------------------------
        // Status snapshot + log tail
        // -----------------------------------------------------------------

        [Fact]
        public void WatchdogStatus_WrittenFromCallback()
        {
            using var ws = new TempWorkspace();
            var o = OptionsFor(ws);
            o.GetWatchdogStatus = () => "STATE=running pid=4242";
            ws.WriteFile(System.IO.Path.Combine("crash", "pending", "crash-001", "dump.txt"), "boom");

            CrashBundler.TryFinalizeLatestCrashBundle(o, "reason", 0);

            var statusPath = System.IO.Path.Combine(SingleBundle(o), "watchdog-status.txt");
            Assert.True(File.Exists(statusPath));
            Assert.Equal("STATE=running pid=4242", File.ReadAllText(statusPath));
        }

        [Fact]
        public void LogTail_CopiesLastNLines_WhenEnabled()
        {
            using var ws = new TempWorkspace();
            var o = OptionsFor(ws);

            var logPath = ws.WriteFile(
                "watchdog.log",
                string.Join(Environment.NewLine, Enumerable.Range(1, 10).Select(i => "line" + i)));

            o.CopyWatchdogLogTail = true;
            o.WatchdogLogPath = logPath;
            o.TailLines = 3;

            ws.WriteFile(System.IO.Path.Combine("crash", "pending", "crash-001", "dump.txt"), "boom");

            CrashBundler.TryFinalizeLatestCrashBundle(o, "reason", 0);

            var tailPath = System.IO.Path.Combine(SingleBundle(o), "watchdog.log.tail");
            Assert.True(File.Exists(tailPath));

            var tail = File.ReadAllLines(tailPath);
            Assert.Equal(new[] { "line8", "line9", "line10" }, tail);
        }

        // -----------------------------------------------------------------
        // Retention (MaxBundles)
        // -----------------------------------------------------------------

        [Fact]
        public void EnforceMaxBundles_DeletesOldest_KeepsNewest()
        {
            using var ws = new TempWorkspace();
            var o = OptionsFor(ws);
            o.MaxBundles = 2;

            // Three pre-existing bundles with distinct, increasing creation times.
            var oldest = ws.CreateDir(System.IO.Path.Combine("crash", "bundles", "bundle-old-1"));
            var middle = ws.CreateDir(System.IO.Path.Combine("crash", "bundles", "bundle-old-2"));
            var newest = ws.CreateDir(System.IO.Path.Combine("crash", "bundles", "bundle-old-3"));
            Directory.SetCreationTimeUtc(oldest, DateTime.UtcNow.AddMinutes(-30));
            Directory.SetCreationTimeUtc(middle, DateTime.UtcNow.AddMinutes(-20));
            Directory.SetCreationTimeUtc(newest, DateTime.UtcNow.AddMinutes(-10));

            // A pending crash drives one fresh bundle (creation time ~ now).
            ws.WriteFile(System.IO.Path.Combine("crash", "pending", "crash-001", "dump.txt"), "boom");

            CrashBundler.TryFinalizeLatestCrashBundle(o, "reason", 0);

            var remaining = BundleDirs(o);
            Assert.Equal(2, remaining.Length);

            // The two pre-existing oldest are gone; the most recent pre-existing survives.
            Assert.False(Directory.Exists(oldest));
            Assert.False(Directory.Exists(middle));
            Assert.True(Directory.Exists(newest));
        }

        [Fact]
        public void EnforceMaxBundles_Zero_KeepsAll()
        {
            using var ws = new TempWorkspace();
            var o = OptionsFor(ws);
            o.MaxBundles = 0; // disabled

            ws.CreateDir(System.IO.Path.Combine("crash", "bundles", "bundle-old-1"));
            ws.CreateDir(System.IO.Path.Combine("crash", "bundles", "bundle-old-2"));
            ws.WriteFile(System.IO.Path.Combine("crash", "pending", "crash-001", "dump.txt"), "boom");

            CrashBundler.TryFinalizeLatestCrashBundle(o, "reason", 0);

            // 2 pre-existing + 1 freshly promoted, none pruned.
            Assert.Equal(3, BundleDirs(o).Length);
        }
    }
}
