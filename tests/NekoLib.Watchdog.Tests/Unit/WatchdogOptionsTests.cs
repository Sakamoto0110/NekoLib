using System;
using System.IO;
using System.Text.RegularExpressions;
using NekoLib.Core.Logging;
using NekoLib.Watchdog.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Watchdog.Tests.Unit
{
    public sealed class WatchdogOptionsTests
    {
        private static readonly Regex PipeNamePattern =
            new Regex(@"^NekoLib\.Watchdog\.[0-9A-F]{16}$", RegexOptions.Compiled);

        [Fact]
        public void Capture_NullTargetPath_Throws()
        {
            var options = new WatchdogOptions { TargetPath = null };

            Assert.Throws<InvalidOperationException>(
                () => WatchdogRuntimeOptions.Capture(options));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Capture_BlankTargetPath_Throws(string blank)
        {
            var options = new WatchdogOptions { TargetPath = blank };

            Assert.Throws<InvalidOperationException>(
                () => WatchdogRuntimeOptions.Capture(options));
        }

        [Fact]
        public void Capture_MissingTargetFile_ThrowsFileNotFound()
        {
            using var workspace = new TempWorkspace();
            var options = new WatchdogOptions
            {
                TargetPath = workspace.Path("does-not-exist.exe")
            };

            Assert.Throws<FileNotFoundException>(
                () => WatchdogRuntimeOptions.Capture(options));
        }

        [Fact]
        public void Capture_AttachTokenWithoutInitialProcessId_Throws()
        {
            using var workspace = new TempWorkspace();
            var options = new WatchdogOptions
            {
                TargetPath = workspace.WriteFile("app.exe"),
                AttachToken = "orphan-token"
            };

            var error = Assert.Throws<InvalidOperationException>(
                () => WatchdogRuntimeOptions.Capture(options));

            Assert.Contains("InitialProcessId", error.Message);
        }

        [Fact]
        public void Capture_ValidTarget_DerivesDeterministicPipeName()
        {
            using var workspace = new TempWorkspace();
            var target = workspace.WriteFile("app.exe");

            var first = WatchdogRuntimeOptions.Capture(
                new WatchdogOptions { TargetPath = target });
            var second = WatchdogRuntimeOptions.Capture(
                new WatchdogOptions { TargetPath = target });

            Assert.Matches(PipeNamePattern, first.PipeName);
            Assert.Equal(first.PipeName, second.PipeName);
            Assert.Equal(
                WatchdogController.ResolvePipeNameForTarget(target),
                first.PipeName);
        }

        [Fact]
        public void Capture_DifferentTargets_ProduceDifferentPipeNames()
        {
            using var workspace = new TempWorkspace();

            var first = WatchdogRuntimeOptions.Capture(new WatchdogOptions
            {
                TargetPath = workspace.WriteFile("one.exe")
            });
            var second = WatchdogRuntimeOptions.Capture(new WatchdogOptions
            {
                TargetPath = workspace.WriteFile("two.exe")
            });

            Assert.NotEqual(first.PipeName, second.PipeName);
        }

        [Fact]
        public void Constructor_DoesNotMutateCallerOptions()
        {
            using var workspace = new TempWorkspace();
            var target = workspace.WriteFile("app.exe");
            var options = new WatchdogOptions
            {
                TargetPath = target,
                WorkingDirectory = null,
                LogPath = null,
                PendingCrashRoot = null,
                BundleRoot = null,
                MonitorPollMs = 1,
                RestartDelayMs = 2,
                ForceKillTimeoutMs = 3,
                MaxLogBytes = 4
            };

            using var runtime = new WatchdogRuntime(options);

            Assert.Equal(target, options.TargetPath);
            Assert.Null(options.WorkingDirectory);
            Assert.Null(options.LogPath);
            Assert.Null(options.PendingCrashRoot);
            Assert.Null(options.BundleRoot);
            Assert.Equal(1, options.MonitorPollMs);
            Assert.Equal(2, options.RestartDelayMs);
            Assert.Equal(3, options.ForceKillTimeoutMs);
            Assert.Equal(4, options.MaxLogBytes);
        }

        [Fact]
        public void Capture_DefaultsPathsAndClampsBounds()
        {
            using var workspace = new TempWorkspace();
            var target = workspace.WriteFile(Path.Combine("nested", "app.exe"));

            var captured = WatchdogRuntimeOptions.Capture(new WatchdogOptions
            {
                TargetPath = target,
                MonitorPollMs = 1,
                RestartDelayMs = 2,
                GracefulKillTimeoutMs = -1,
                ForceKillTimeoutMs = 3,
                MaxLogBytes = 4
            });

            var workingDirectory = Path.GetDirectoryName(Path.GetFullPath(target));
            Assert.Equal(workingDirectory, captured.WorkingDirectory);
            Assert.Equal(Path.Combine(workingDirectory, "watchdog.log"), captured.LogPath);
            Assert.Equal(Path.Combine(workingDirectory, "crash", "pending"), captured.PendingCrashRoot);
            Assert.Equal(Path.Combine(workingDirectory, "crash", "bundles"), captured.BundleRoot);
            Assert.Equal(50, captured.MonitorPollMs);
            Assert.Equal(200, captured.RestartDelayMs);
            Assert.Equal(0, captured.GracefulKillTimeoutMs);
            Assert.Equal(100, captured.ForceKillTimeoutMs);
            Assert.Equal(64 * 1024, captured.MaxLogBytes);
        }

        [Fact]
        public void Capture_CreatesCrashAndLogDirectoriesOnly()
        {
            using var workspace = new TempWorkspace();
            var target = workspace.WriteFile("app.exe");
            var workingDirectory = workspace.CreateDir("work");
            var updateDirectory = Path.Combine(workingDirectory, "updates");

            var captured = WatchdogRuntimeOptions.Capture(new WatchdogOptions
            {
                TargetPath = target,
                WorkingDirectory = workingDirectory
            });

            Assert.True(Directory.Exists(captured.PendingCrashRoot));
            Assert.True(Directory.Exists(captured.BundleRoot));
            Assert.True(Directory.Exists(Path.GetDirectoryName(captured.LogPath)));
            Assert.False(Directory.Exists(updateDirectory));
        }

        [Fact]
        public void Capture_FileLoggingDisabled_LeavesLogPathNull()
        {
            using var workspace = new TempWorkspace();

            var captured = WatchdogRuntimeOptions.Capture(new WatchdogOptions
            {
                TargetPath = workspace.WriteFile("app.exe"),
                EnableFileLogging = false
            });

            Assert.Null(captured.LogPath);
        }

        [Fact]
        public void Capture_CopiesSinkArrayWithoutTakingSinkOwnership()
        {
            using var workspace = new TempWorkspace();
            var first = new CountingSink();
            var second = new CountingSink();
            var supplied = new ILogSink[] { first };
            var options = new WatchdogOptions
            {
                TargetPath = workspace.WriteFile("app.exe"),
                LogSinks = supplied
            };

            using var runtime = new WatchdogRuntime(options);
            supplied[0] = second;
            options.LogSinks = new ILogSink[] { second };

            Assert.Same(first, runtime.CapturedOptions.LogSinks[0]);
        }

        [Fact]
        public void Constructor_CapturesValuesAgainstLaterMutation()
        {
            using var workspace = new TempWorkspace();
            var target = workspace.WriteFile("app.exe");
            var options = new WatchdogOptions
            {
                TargetPath = target,
                EnableHotkeys = false,
                RestartDelayMs = 450
            };

            using var runtime = new WatchdogRuntime(options);
            options.TargetPath = workspace.WriteFile("other.exe");
            options.EnableHotkeys = true;
            options.RestartDelayMs = 900;

            Assert.Equal(Path.GetFullPath(target), runtime.CapturedOptions.TargetPath);
            Assert.False(runtime.CapturedOptions.EnableHotkeys);
            Assert.Equal(450, runtime.CapturedOptions.RestartDelayMs);
        }

        [Fact]
        public void EnableHotkeys_DefaultsToTrue()
        {
            Assert.True(new WatchdogOptions().EnableHotkeys);
        }

        private sealed class CountingSink : ILogSink
        {
            public void Write(LogEntry entry)
            {
            }
        }
    }
}
