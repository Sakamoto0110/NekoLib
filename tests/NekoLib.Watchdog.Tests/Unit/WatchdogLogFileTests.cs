using System.IO;
using NekoLib.Watchdog.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Watchdog.Tests.Unit
{
    /// <summary>
    /// Unit tests for <see cref="WatchdogLogFile.Append"/> — the bounded,
    /// single-backup-rotation file writer that finally honors
    /// <c>WatchdogOptions.MaxLogBytes</c> (audit finding M3: the cap existed but
    /// the runtime appended without limit). These pin the rotation contract:
    /// the active file stays under the cap, exactly one ".1" backup is kept, and
    /// a non-positive cap disables rotation entirely.
    /// </summary>
    public class WatchdogLogFileTests
    {
        [Fact]
        public void Append_CreatesFile_WhenMissing()
        {
            using var ws = new TempWorkspace();
            var log = ws.Path("watchdog.log");

            WatchdogLogFile.Append(log, "hello", maxBytes: 0);

            Assert.True(File.Exists(log));
            Assert.Contains("hello", File.ReadAllText(log));
        }

        [Fact]
        public void Append_AddsTrailingNewline()
        {
            using var ws = new TempWorkspace();
            var log = ws.Path("watchdog.log");

            WatchdogLogFile.Append(log, "line-a", maxBytes: 0);
            WatchdogLogFile.Append(log, "line-b", maxBytes: 0);

            Assert.Equal(new[] { "line-a", "line-b" }, File.ReadAllLines(log));
        }

        [Fact]
        public void Append_DoesNotRotate_WhenUnderLimit()
        {
            using var ws = new TempWorkspace();
            var log = ws.Path("watchdog.log");

            for (int i = 0; i < 20; i++)
                WatchdogLogFile.Append(log, "x", maxBytes: 1024 * 1024);

            Assert.False(File.Exists(log + ".1"));
        }

        [Fact]
        public void Append_Rotates_WhenExceedingLimit_AndBoundsActiveFile()
        {
            using var ws = new TempWorkspace();
            var log = ws.Path("watchdog.log");

            // ~12 bytes per line ("0123456789" + CRLF); cap at 50 forces rotation.
            const long cap = 50;
            for (int i = 0; i < 40; i++)
                WatchdogLogFile.Append(log, "0123456789", cap);

            Assert.True(File.Exists(log + ".1"));

            // Active file never carries more than the cap plus one in-flight line.
            long active = new FileInfo(log).Length;
            Assert.True(active <= cap + 12, "active file size " + active + " exceeded cap+line");
        }

        [Fact]
        public void Append_KeepsOnlySingleBackup_AcrossManyRotations()
        {
            using var ws = new TempWorkspace();
            var log = ws.Path("watchdog.log");

            for (int i = 0; i < 200; i++)
                WatchdogLogFile.Append(log, "0123456789", maxBytes: 40);

            // Exactly one rolling backup — no ".2", ".3", etc.
            var siblings = Directory.GetFiles(ws.Root, "watchdog.log*");
            Assert.Equal(2, siblings.Length); // watchdog.log + watchdog.log.1
            Assert.True(File.Exists(log + ".1"));
            Assert.False(File.Exists(log + ".2"));
        }

        [Fact]
        public void Append_ReturnsTrue_OnRotation()
        {
            using var ws = new TempWorkspace();
            var log = ws.Path("watchdog.log");

            Assert.False(WatchdogLogFile.Append(log, "0123456789", maxBytes: 40)); // first write, file fresh
            // Keep appending until a rotation is reported.
            bool sawRotation = false;
            for (int i = 0; i < 20 && !sawRotation; i++)
                sawRotation = WatchdogLogFile.Append(log, "0123456789", maxBytes: 40);

            Assert.True(sawRotation);
        }

        [Fact]
        public void Append_ZeroMaxBytes_DisablesRotation()
        {
            using var ws = new TempWorkspace();
            var log = ws.Path("watchdog.log");

            for (int i = 0; i < 500; i++)
                WatchdogLogFile.Append(log, "0123456789", maxBytes: 0);

            Assert.False(File.Exists(log + ".1"));
        }

        [Fact]
        public void Append_NullOrEmptyPath_IsNoOp()
        {
            Assert.False(WatchdogLogFile.Append(null, "x", 100));
            Assert.False(WatchdogLogFile.Append("", "x", 100));
        }
    }
}
