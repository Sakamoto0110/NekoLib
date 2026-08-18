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
                    EvidenceCollectionTimeout = TimeSpan.FromMilliseconds(250),
                    LogSnapshotSource = new StaticLogSource(),
                    TelemetrySnapshotSource = new SlowTelemetrySource(),
                    InspectionSnapshotSource = new ThrowingInspectionSource()
                });

                string crashTextPath = null;
                handler.CrashBundleWritten += (s, e) => crashTextPath = e.CrashTextPath;
                var watch = Stopwatch.StartNew();

                InvokeHandleCrash(handler);

                watch.Stop();
                Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2));
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
        public void HandleCrash_WithoutExternalNotifier_DoesNotNotify()
        {
            var raised = 0;
            var options = new CrashHandlerOptions
            {
                WriteCrashFolder = false,
                DumpLevel = CrashDumpLevel.None,
                ExternalNotifier = null
            };

            var handler = new CrashHandler(options);
            handler.CrashDetected += (_, __) => raised++;

            InvokeHandleCrash(handler);

            Assert.Equal(1, raised);
        }

        [Fact]
        public void HandleCrash_WhenBundleCannotBeWritten_RaisesFailedInsteadOfWritten()
        {
            var blocker = Path.Combine(
                Path.GetTempPath(),
                "nekolib-diagnostics-blocker-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(blocker, "not a directory");

            try
            {
                var handler = new CrashHandler(new CrashHandlerOptions
                {
                    CrashRootDirectory = Path.Combine(blocker, "crash"),
                    DumpLevel = CrashDumpLevel.None
                });

                var events = new List<string>();
                CrashBundleFailedEventArgs failure = null;
                handler.CrashDetected += (_, __) => events.Add("detected");
                handler.CrashBundleWritten += (_, __) => events.Add("written");
                handler.CrashBundleFailed += (_, e) => { events.Add("failed"); failure = e; };

                InvokeHandleCrash(handler);

                Assert.Equal(new[] { "detected", "failed" }, events.ToArray());
                Assert.NotNull(failure);
                Assert.False(string.IsNullOrWhiteSpace(failure.Reason));
                Assert.False(string.IsNullOrWhiteSpace(failure.BundleDirectory));
            }
            finally
            {
                try { File.Delete(blocker); } catch { }
            }
        }

        [Fact]
        public void HandleCrash_WhenFlusherConsumesItsWholeBudget_ReportsItsResultNotATimeout()
        {
            var root = NewTempRoot();
            try
            {
                var handler = new CrashHandler(new CrashHandlerOptions
                {
                    CrashRootDirectory = root,
                    DumpLevel = CrashDumpLevel.None,
                    EvidenceCollectionTimeout = TimeSpan.FromMilliseconds(200),
                    LogFlusher = new BudgetConsumingFlusher()
                });

                string crashTextPath = null;
                handler.CrashBundleWritten += (s, e) => crashTextPath = e.CrashTextPath;

                InvokeHandleCrash(handler);

                var report = File.ReadAllText(crashTextPath);
                Assert.Contains("Logging flush: did not complete within its budget.", report);
                Assert.DoesNotContain("Logging flush: timed out", report);
            }
            finally
            {
                TryDelete(root);
            }
        }

        [Fact]
        public void HandleCrash_WithRedactor_RunsOneBoundedBatchPerBlock()
        {
            var root = NewTempRoot();
            try
            {
                var threads = new HashSet<int>();
                var handler = new CrashHandler(new CrashHandlerOptions
                {
                    CrashRootDirectory = root,
                    DumpLevel = CrashDumpLevel.None,
                    EvidenceCollectionTimeout = TimeSpan.FromSeconds(2),
                    Redact = line =>
                    {
                        lock (threads) threads.Add(Thread.CurrentThread.ManagedThreadId);
                        return line;
                    }
                });

                handler.CrashBundleWritten += (s, e) => { };

                InvokeHandleCrash(handler);

                // The crash-text block is redacted by a single contributor rather
                // than one dedicated thread per line.
                Assert.Single(threads);
            }
            finally
            {
                TryDelete(root);
            }
        }

        [Fact]
        public void HandleCrash_WhenSourceIgnoresItsLimit_TruncatesLocally()
        {
            var root = NewTempRoot();
            try
            {
                var handler = new CrashHandler(new CrashHandlerOptions
                {
                    CrashRootDirectory = root,
                    DumpLevel = CrashDumpLevel.None,
                    EvidenceCollectionTimeout = TimeSpan.FromSeconds(5),
                    MaxRecentLogEntries = 5,
                    LogSnapshotSource = new LimitIgnoringLogSource(50)
                });

                string crashTextPath = null;
                handler.CrashBundleWritten += (s, e) => crashTextPath = e.CrashTextPath;

                InvokeHandleCrash(handler);

                var report = File.ReadAllText(crashTextPath);
                Assert.Contains("entry-4", report);
                Assert.DoesNotContain("entry-5", report);
                Assert.Contains("<truncated at 5 log entries>", report);
            }
            finally
            {
                TryDelete(root);
            }
        }

        [Fact]
        public void HandleCrash_WhenOneRecordThrows_KeepsTheRestOfTheSection()
        {
            var root = NewTempRoot();
            try
            {
                var handler = new CrashHandler(new CrashHandlerOptions
                {
                    CrashRootDirectory = root,
                    DumpLevel = CrashDumpLevel.None,
                    EvidenceCollectionTimeout = TimeSpan.FromSeconds(5),
                    LogSnapshotSource = new PoisonedLogSource()
                });

                string crashTextPath = null;
                handler.CrashBundleWritten += (s, e) => crashTextPath = e.CrashTextPath;

                InvokeHandleCrash(handler);

                var report = File.ReadAllText(crashTextPath);
                Assert.Contains("good-entry-1", report);
                Assert.Contains("good-entry-2", report);
                Assert.Contains("<ToString threw: NotSupportedException>", report);
                Assert.DoesNotContain("<contributor failed", report);
            }
            finally
            {
                TryDelete(root);
            }
        }

        [Fact]
        public void HandleCrash_WithTailFilesSharingAName_PreservesBothAndRecordsTheCollision()
        {
            var root = NewTempRoot();
            try
            {
                var first = Path.Combine(root, "a");
                var second = Path.Combine(root, "b");
                Directory.CreateDirectory(first);
                Directory.CreateDirectory(second);

                var firstLog = Path.Combine(first, "app.log");
                var secondLog = Path.Combine(second, "app.log");
                File.WriteAllText(firstLog, "from-a");
                File.WriteAllText(secondLog, "from-b");

                var handler = new CrashHandler(new CrashHandlerOptions
                {
                    CrashRootDirectory = Path.Combine(root, "crash"),
                    DumpLevel = CrashDumpLevel.None,
                    EvidenceCollectionTimeout = TimeSpan.FromSeconds(5),
                    TailFiles = new List<string> { firstLog, secondLog }
                });

                string bundleDirectory = null;
                handler.CrashBundleWritten += (s, e) => bundleDirectory = e.BundleDirectory;

                InvokeHandleCrash(handler);

                Assert.Equal("from-a", File.ReadAllText(Path.Combine(bundleDirectory, "app.log")).Trim());
                Assert.Equal("from-b", File.ReadAllText(Path.Combine(bundleDirectory, "app-2.log")).Trim());
                Assert.Contains(
                    "File tail app.log: name collision, written as app-2.log.",
                    File.ReadAllText(Path.Combine(bundleDirectory, "crash.txt")));
            }
            finally
            {
                TryDelete(root);
            }
        }

        [Fact]
        public void Constructor_CapturesOptions_SoLaterMutationIsIgnored()
        {
            var root = NewTempRoot();
            try
            {
                var options = new CrashHandlerOptions
                {
                    CrashRootDirectory = root,
                    DumpLevel = CrashDumpLevel.None,
                    TailFiles = new List<string>()
                };

                var handler = new CrashHandler(options);

                var stray = Path.Combine(root, "stray.log");
                File.WriteAllText(stray, "stray");

                options.CrashRootDirectory = null;
                options.MaxEvidenceLineLength = 1;
                options.TailFiles.Add(stray);

                string bundleDirectory = null;
                handler.CrashBundleWritten += (s, e) => bundleDirectory = e.BundleDirectory;
                handler.CrashBundleFailed += (s, e) => Assert.Fail("bundle should still be written");

                InvokeHandleCrash(handler, "captured-source");

                Assert.NotNull(bundleDirectory);
                var report = File.ReadAllText(Path.Combine(bundleDirectory, "crash.txt"));
                Assert.Contains("Source: captured-source", report);
                Assert.False(File.Exists(Path.Combine(bundleDirectory, "stray.log")));
            }
            finally
            {
                TryDelete(root);
            }
        }

        [Fact]
        public void Dispose_IsTerminal_AndInstallAfterwardsThrows()
        {
            var handler = new CrashHandler(new CrashHandlerOptions
            {
                WriteCrashFolder = false,
                DumpLevel = CrashDumpLevel.None
            });

            var dispatches = 0;
            handler.CrashDetected += (_, __) => dispatches++;

            handler.Install();
            CrashHandler.ReportExternalCrash("unit-test", new InvalidOperationException("a"), false);
            Assert.Equal(1, dispatches);

            handler.Dispose();
            handler.Dispose();

            CrashHandler.ReportExternalCrash("unit-test", new InvalidOperationException("b"), false);
            Assert.Equal(1, dispatches);

            Assert.Throws<ObjectDisposedException>(() => handler.Install());
            CrashHandler.ReportExternalCrash("unit-test", new InvalidOperationException("c"), false);
            Assert.Equal(1, dispatches);
        }

        [Fact]
        public void Dispose_WhenLastHandlerIsRemoved_RemovesTheProcessWideHooks()
        {
            var installedField = typeof(CrashHandler).GetField(
                "_globalHandlersInstalled",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(installedField);

            var handler = new CrashHandler(new CrashHandlerOptions
            {
                WriteCrashFolder = false,
                DumpLevel = CrashDumpLevel.None
            });

            handler.Install();
            Assert.True((bool)installedField.GetValue(null));

            handler.Dispose();
            Assert.False((bool)installedField.GetValue(null));

            // A later handler re-arms the process-wide hooks.
            var second = new CrashHandler(new CrashHandlerOptions
            {
                WriteCrashFolder = false,
                DumpLevel = CrashDumpLevel.None
            });

            try
            {
                second.Install();
                Assert.True((bool)installedField.GetValue(null));
            }
            finally
            {
                second.Dispose();
            }

            Assert.False((bool)installedField.GetValue(null));
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

        private sealed class BudgetConsumingFlusher : ILogFlusher
        {
            // Exactly what NekoLib.Logging.Logger.Flush does when its budget expires:
            // consume the supplied budget, then report that it did not complete.
            public bool Flush(TimeSpan timeout)
            {
                Thread.Sleep(timeout);
                return false;
            }
        }

        private sealed class LimitIgnoringLogSource : ILogSnapshotSource
        {
            private readonly int _count;

            public LimitIgnoringLogSource(int count) => _count = count;

            public IReadOnlyList<LogEntry> GetRecentEntries(int maxEntries)
            {
                var entries = new LogEntry[_count];
                for (int i = 0; i < entries.Length; i++)
                    entries[i] = new LogEntry(DateTime.UtcNow, LogLevel.Info, "entry-" + i);
                return entries;
            }
        }

        private sealed class PoisonedToStringException : Exception
        {
            public override string ToString() => throw new NotSupportedException("poison");
        }

        private sealed class PoisonedLogSource : ILogSnapshotSource
        {
            public IReadOnlyList<LogEntry> GetRecentEntries(int maxEntries) => new[]
            {
                new LogEntry(DateTime.UtcNow, LogLevel.Info, "good-entry-1"),
                new LogEntry(DateTime.UtcNow, LogLevel.Error, "poisoned", new PoisonedToStringException()),
                new LogEntry(DateTime.UtcNow, LogLevel.Info, "good-entry-2")
            };
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
                Thread.Sleep(2000);
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
