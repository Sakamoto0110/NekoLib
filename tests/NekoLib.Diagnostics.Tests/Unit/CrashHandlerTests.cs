using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using NekoLib.Core.Inspection;
using NekoLib.Core.Logging;
using NekoLib.Core.Telemetry;
using Xunit;

namespace NekoLib.Diagnostics.Tests.Unit
{
    public sealed class CrashHandlerTests
    {
        [Fact]
        public void CrashDetectedSubscriberFailure_DoesNotPreventCrashArtifacts()
        {
            var root = Path.Combine(Path.GetTempPath(), "nekolib-diagnostics-test-" + Guid.NewGuid().ToString("N"));

            try
            {
                var handler = new CrashHandler(
                    new CrashHandlerOptions
                    {
                        CrashRootDirectory = root,
                        DumpLevel = CrashDumpLevel.None
                    });

                string crashTextPath = null;
                handler.CrashDetected += (s, e) => throw new InvalidOperationException("bad subscriber");
                handler.CrashBundleWritten += (s, e) => crashTextPath = e.CrashTextPath;

                InvokeHandleCrash(handler);

                Assert.False(string.IsNullOrWhiteSpace(crashTextPath));
                Assert.True(File.Exists(crashTextPath));
                Assert.Contains("unit-test", File.ReadAllText(crashTextPath));
            }
            finally
            {
                try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
            }
        }

        [Fact]
        public void HandleCrash_WithSuppliedSources_RecordsFlushesAndWritesRedactedEvidence()
        {
            var root = NewTempRoot();
            try
            {
                var logger = new CapturingLogger();
                var handler = new CrashHandler(new CrashHandlerOptions
                {
                    CrashRootDirectory = root,
                    DumpLevel = CrashDumpLevel.None,
                    Logger = logger,
                    EvidenceCollectionTimeout = TimeSpan.FromSeconds(2),
                    TelemetrySnapshotSource = new TelemetrySource(),
                    InspectionSnapshotSource = new InspectionSource(),
                    Redact = line => line.Replace("secret", "[redacted]")
                });

                string crashTextPath = null;
                handler.CrashBundleWritten += (s, e) => crashTextPath = e.CrashTextPath;

                InvokeHandleCrash(handler, "secret-source", "secret-failure");

                Assert.Equal(new[] { "log", "flush", "snapshot" }, logger.Calls.ToArray());
                Assert.Equal(LogLevel.Fatal, logger.Entries[0].Level);

                var report = File.ReadAllText(crashTextPath);
                Assert.Contains("---- Recent logs ----", report);
                Assert.Contains("---- Recent telemetry ----", report);
                Assert.Contains("---- Inspection snapshot ----", report);
                Assert.Contains("Navigation/page_switch", report);
                Assert.Contains("state page=Catalog", report);
                Assert.Contains("[redacted]-source", report);
                Assert.DoesNotContain("secret", report);
            }
            finally
            {
                TryDelete(root);
            }
        }

        [Fact]
        public void HandleCrash_WhenContributorsFailOrTimeout_WritesBoundedPartialBundle()
        {
            var root = NewTempRoot();
            try
            {
                var handler = new CrashHandler(new CrashHandlerOptions
                {
                    CrashRootDirectory = root,
                    DumpLevel = CrashDumpLevel.None,
                    EvidenceCollectionTimeout = TimeSpan.FromMilliseconds(50),
                    LogSnapshotSource = new StaticLogSource(),
                    TelemetrySnapshotSource = new SlowTelemetrySource(),
                    InspectionSnapshotSource = new ThrowingInspectionSource()
                });

                string crashTextPath = null;
                handler.CrashBundleWritten += (s, e) => crashTextPath = e.CrashTextPath;
                var watch = Stopwatch.StartNew();

                InvokeHandleCrash(handler);

                watch.Stop();
                Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1));
                var report = File.ReadAllText(crashTextPath);
                Assert.Contains("surviving log", report);
                Assert.Contains("<contributor timed out>", report);
                Assert.Contains("<contributor failed: InvalidOperationException>", report);
                Assert.Contains("==== END ====", report);
            }
            finally
            {
                TryDelete(root);
            }
        }

        [Fact]
        public void HandleCrash_WhenRedactorTimesOut_DoesNotPersistUnredactedEvidence()
        {
            var root = NewTempRoot();
            try
            {
                var handler = new CrashHandler(new CrashHandlerOptions
                {
                    CrashRootDirectory = root,
                    DumpLevel = CrashDumpLevel.None,
                    EvidenceCollectionTimeout = TimeSpan.FromMilliseconds(40),
                    Redact = line =>
                    {
                        Thread.Sleep(500);
                        return line;
                    }
                });

                string crashTextPath = null;
                handler.CrashBundleWritten += (s, e) => crashTextPath = e.CrashTextPath;
                var watch = Stopwatch.StartNew();

                InvokeHandleCrash(handler, "secret-source", "secret-failure");

                watch.Stop();
                Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1));
                var report = File.ReadAllText(crashTextPath);
                Assert.DoesNotContain("secret", report);
                Assert.Contains("<redaction", report);
            }
            finally
            {
                TryDelete(root);
            }
        }

        [Fact]
        public void HandleCrash_WithExternalNotifierOutsideWatchdog_InvokesAfterArtifacts()
        {
            var root = NewTempRoot();
            var previous = Environment.GetEnvironmentVariable("NEKO_UNDER_WATCHDOG");

            try
            {
                Environment.SetEnvironmentVariable("NEKO_UNDER_WATCHDOG", null);
                var calls = new List<string>();
                var handler = new CrashHandler(new CrashHandlerOptions
                {
                    CrashRootDirectory = root,
                    DumpLevel = CrashDumpLevel.None,
                    ExternalNotifier = _ => calls.Add("notify")
                });

                handler.CrashBundleWritten += (_, __) => calls.Add("bundle");

                InvokeHandleCrash(handler);

                Assert.Equal(new[] { "bundle", "notify" }, calls.ToArray());
            }
            finally
            {
                Environment.SetEnvironmentVariable("NEKO_UNDER_WATCHDOG", previous);
                TryDelete(root);
            }
        }

        [Fact]
        public void HandleCrash_WithCompatibilityNotificationGateDisabled_DoesNotNotify()
        {
            var notified = false;
            var options = new CrashHandlerOptions
            {
                WriteCrashFolder = false,
                DumpLevel = CrashDumpLevel.None,
                ExternalNotifier = _ => notified = true
            };

#pragma warning disable CS0618
            options.NotifyWatchdog = false;
#pragma warning restore CS0618

            InvokeHandleCrash(new CrashHandler(options));

            Assert.False(notified);
        }

        private static void InvokeHandleCrash(
            CrashHandler handler,
            string source = "unit-test",
            string message = "unit-test")
        {
            var method = typeof(CrashHandler).GetMethod(
                "HandleCrash",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method);

            method.Invoke(handler, new object[]
            {
                source,
                new InvalidOperationException(message),
                false
            });
        }

        private static string NewTempRoot()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "nekolib-diagnostics-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void TryDelete(string root)
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); }
            catch { }
        }

        private sealed class CapturingLogger : ILogger, ILogFlusher, ILogSnapshotSource
        {
            public List<string> Calls { get; } = new List<string>();
            public List<LogEntry> Entries { get; } = new List<LogEntry>();

            public void Log(LogLevel level, string message, Exception exception = null, string category = null)
            {
                Calls.Add("log");
                Entries.Add(new LogEntry(DateTime.UtcNow, level, message, exception, category));
            }

            public bool Flush(TimeSpan timeout)
            {
                Calls.Add("flush");
                return true;
            }

            public IReadOnlyList<LogEntry> GetRecentEntries(int maxEntries)
            {
                Calls.Add("snapshot");
                return Entries.ToArray();
            }
        }

        private sealed class StaticLogSource : ILogSnapshotSource
        {
            public IReadOnlyList<LogEntry> GetRecentEntries(int maxEntries)
                => new[] { new LogEntry(DateTime.UtcNow, LogLevel.Info, "surviving log") };
        }

        private sealed class TelemetrySource : ITelemetrySnapshotSource
        {
            public IReadOnlyList<TelemetryOperation> GetRecentOperations(int maxOperations)
                => new[]
                {
                    new TelemetryOperation(
                        DateTime.UtcNow,
                        "Navigation",
                        "page_switch",
                        "operation-1",
                        null,
                        TelemetryOutcome.Succeeded,
                        TimeSpan.FromMilliseconds(10))
                };
        }

        private sealed class SlowTelemetrySource : ITelemetrySnapshotSource
        {
            public IReadOnlyList<TelemetryOperation> GetRecentOperations(int maxOperations)
            {
                Thread.Sleep(500);
                return new TelemetryOperation[0];
            }
        }

        private sealed class InspectionSource : IInspectionSnapshotSource
        {
            public InspectionSnapshot CaptureSnapshot(int maxOperations, TimeSpan timeout)
                => new InspectionSnapshot(
                    DateTime.UtcNow,
                    new[]
                    {
                        new InspectionOperation(1, DateTime.UtcNow, "Navigation", "page_ready", null)
                    },
                    new Dictionary<string, object> { ["page"] = "Catalog" },
                    10,
                    1,
                    0);
        }

        private sealed class ThrowingInspectionSource : IInspectionSnapshotSource
        {
            public InspectionSnapshot CaptureSnapshot(int maxOperations, TimeSpan timeout)
                => throw new InvalidOperationException("inspection unavailable");
        }
    }
}
