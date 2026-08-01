using NekoLib.Core.Logging;
using NekoLib.Logging.Sinks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;

namespace NekoLib.Logging.Tests.Unit
{
    public sealed class LoggerTests
    {
        [Fact]
        public void Log_BelowMinimumLevel_DoesNotDispatchOrRetain()
        {
            var sink = new RecordingSink("sink", new List<string>());
            using var logger = new Logger(LogLevel.Info, sink);

            logger.Debug("ignored");

            Assert.Empty(sink.Entries);
            Assert.Empty(logger.GetRecentEntries(10));
        }

        [Fact]
        public void Log_MultipleSinks_DispatchesInRegistrationOrder()
        {
            var order = new List<string>();
            var first = new RecordingSink("first", order);
            var second = new RecordingSink("second", order);
            using var logger = new Logger(LogLevel.Trace, first, second);

            logger.Info("accepted", "Navigation");

            Assert.Equal(new[] { "first", "second" }, order);
            Assert.Equal("Navigation", first.Entries.Single().Category);
        }

        [Fact]
        public void Log_SinkThrows_LaterSinkStillReceivesEntry()
        {
            var later = new RecordingSink("later", new List<string>());
            using var logger = new Logger(LogLevel.Trace, new ThrowingSink(), later);

            logger.Error("failure", new InvalidOperationException("boom"));

            Assert.Single(later.Entries);
        }

        [Fact]
        public void GetRecentEntries_CapacityExceeded_ReturnsNewestOldestFirst()
        {
            using var logger = new Logger(new LoggerOptions
            {
                MinimumLevel = LogLevel.Trace,
                RecentEntryCapacity = 2
            });

            logger.Info("one");
            logger.Info("two");
            logger.Info("three");

            Assert.Equal(new[] { "two", "three" },
                logger.GetRecentEntries(10).Select(x => x.Message));
        }

        [Fact]
        public void Flush_FlushableSinks_InvokesEverySink()
        {
            var first = new FlushSink();
            var second = new FlushSink();
            using var logger = new Logger(LogLevel.Trace, first, second);

            Assert.True(logger.Flush(TimeSpan.FromSeconds(1)));
            Assert.Equal(1, first.FlushCount);
            Assert.Equal(1, second.FlushCount);
        }

        [Fact]
        public void Flush_SinkExceedsBudget_ReturnsFalse()
        {
            using var logger = new Logger(LogLevel.Trace, new SlowFlushSink());

            Assert.False(logger.Flush(TimeSpan.FromMilliseconds(20)));
        }

        [Fact]
        public void RollingFileSink_Info_WritesWithoutDiagnosticsOrInspection()
        {
            var root = NewRoot();
            try
            {
                var path = Path.Combine(root, "application.log");
                var sink = CreateFileSink(path, 4096, 2);
                using var logger = new Logger(LogLevel.Info, sink);

                logger.Info("ordinary-info", "Application");

                Assert.Contains("ordinary-info", File.ReadAllText(path));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Fact]
        public void RollingFileSink_MaximumExceeded_RotatesAndRetainsConfiguredCount()
        {
            var root = NewRoot();
            try
            {
                var path = Path.Combine(root, "application.log");
                var sink = CreateFileSink(path, 1024, 2);
                var payload = new string('x', 700);

                for (int i = 0; i < 6; i++)
                {
                    sink.Write(new LogEntry(
                        DateTime.UtcNow,
                        LogLevel.Info,
                        i + "-" + payload));
                }

                Assert.True(File.Exists(path));
                Assert.True(File.Exists(path + ".1"));
                Assert.True(File.Exists(path + ".2"));
                Assert.False(File.Exists(path + ".3"));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Fact]
        public void RollingFileSink_PathIsDirectory_ThrowsWriteFailure()
        {
            var root = NewRoot();
            try
            {
                var sink = CreateFileSink(root, 4096, 2);

                Assert.ThrowsAny<Exception>(() => sink.Write(new LogEntry(
                    DateTime.UtcNow,
                    LogLevel.Info,
                    "cannot-write")));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        private static RollingFileLogSink CreateFileSink(
            string path,
            long maximumBytes,
            int retainedFiles)
            => new RollingFileLogSink(new RollingFileLogSinkOptions
            {
                FilePath = path,
                MaximumFileBytes = maximumBytes,
                RetainedFileCount = retainedFiles
            });

        private static string NewRoot()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "nekolib-logging-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void DeleteRoot(string root)
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); }
            catch { }
        }

        private sealed class RecordingSink : ILogSink
        {
            private readonly string _name;
            private readonly List<string> _order;

            public RecordingSink(string name, List<string> order)
            {
                _name = name;
                _order = order;
            }

            public List<LogEntry> Entries { get; } = new List<LogEntry>();

            public void Write(LogEntry entry)
            {
                _order.Add(_name);
                Entries.Add(entry);
            }
        }

        private sealed class ThrowingSink : ILogSink
        {
            public void Write(LogEntry entry) => throw new InvalidOperationException("sink");
        }

        private sealed class FlushSink : IFlushableLogSink
        {
            public int FlushCount { get; private set; }
            public void Write(LogEntry entry) { }
            public void Flush() => FlushCount++;
        }

        private sealed class SlowFlushSink : IFlushableLogSink
        {
            public void Write(LogEntry entry) { }
            public void Flush() => Thread.Sleep(250);
        }
    }
}
