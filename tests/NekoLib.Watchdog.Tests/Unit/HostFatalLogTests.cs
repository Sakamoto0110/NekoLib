using System;
using System.IO;
using NekoLib.Watchdog.Host;
using NekoLib.Watchdog.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Watchdog.Tests.Unit
{
    public sealed class HostFatalLogTests
    {
        [Fact]
        public void TryWrite_FixedContext_WritesUtcAndProcessIdentity()
        {
            using var workspace = new TempWorkspace();
            var path = workspace.Path("logs", "host-fatal.log");
            var utc = new DateTime(2026, 8, 20, 12, 34, 56, DateTimeKind.Utc);

            HostFatalLog.TryWrite(
                new InvalidOperationException("startup failed"),
                path,
                utc,
                4321);

            var text = File.ReadAllText(path);
            Assert.Contains("2026-08-20T12:34:56.0000000Z", text);
            Assert.Contains("pid=4321", text);
            Assert.Contains("startup failed", text);
        }

        [Fact]
        public void TryWrite_AppendWouldExceedBound_RotatesOneBackup()
        {
            using var workspace = new TempWorkspace();
            var path = workspace.WriteFile(
                "host-fatal.log",
                new string('a', checked((int)HostFatalLog.MaxBytes - 32)));
            File.WriteAllText(path + ".1", "old backup");

            HostFatalLog.TryWrite(
                new InvalidOperationException("new failure"),
                path,
                DateTime.UtcNow,
                1);

            Assert.True(File.Exists(path + ".1"));
            Assert.Contains("new failure", File.ReadAllText(path));
            Assert.True(new FileInfo(path).Length <= HostFatalLog.MaxBytes);
            Assert.True(new FileInfo(path + ".1").Length <= HostFatalLog.MaxBytes);
        }

        [Fact]
        public void TryWrite_OversizedException_BoundsActiveFile()
        {
            using var workspace = new TempWorkspace();
            var path = workspace.Path("host-fatal.log");
            var message = new string('x', checked((int)HostFatalLog.MaxBytes * 2));

            HostFatalLog.TryWrite(
                new InvalidOperationException(message),
                path,
                DateTime.UtcNow,
                1);

            Assert.True(new FileInfo(path).Length <= HostFatalLog.MaxBytes);
        }

        [Fact]
        public void TryWrite_InvalidPath_DoesNotEscape()
        {
            HostFatalLog.TryWrite(
                new InvalidOperationException("failure"),
                "invalid\0path",
                DateTime.UtcNow,
                1);
        }

        [Fact]
        public void FatalLogPath_IsPerUserLocalApplicationData()
        {
            var expectedRoot = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            var path = HostFatalLog.GetDefaultPath();

            Assert.StartsWith(expectedRoot, path, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(
                Path.Combine("NekoLib", "Watchdog", "watchdog-host-fatal.log"),
                path,
                StringComparison.OrdinalIgnoreCase);
            Assert.Equal(
                WatchdogBootstrap.GetHostFatalLogPath(),
                path,
                ignoreCase: true);
        }
    }
}
