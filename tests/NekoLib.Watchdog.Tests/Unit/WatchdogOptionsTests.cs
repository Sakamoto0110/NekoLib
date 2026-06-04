using System;
using System.IO;
using System.Text.RegularExpressions;
using NekoLib.Watchdog.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Watchdog.Tests.Unit
{
    /// <summary>
    /// Unit tests for <see cref="WatchdogOptions.Normalize()"/> — the single
    /// validate-and-fill-defaults entry point the runtime calls at construction.
    /// These pin the policy decisions made there: required-field validation,
    /// the SHA1-derived pipe identity, working-directory defaulting, restart-delay
    /// clamping, log-path defaulting, and eager crash/update directory creation.
    ///
    /// <para>
    /// Several findings from the first-pass audit live in this surface — most
    /// notably that <c>Normalize()</c> unconditionally overwrites any caller-set
    /// <see cref="WatchdogOptions.PipeName"/>. The relevant test below pins that
    /// as current (if surprising) behavior so a future fix is a deliberate change.
    /// </para>
    /// </summary>
    public class WatchdogOptionsTests
    {
        private static readonly Regex PipeNamePattern =
            new Regex(@"^NekoLib\.Watchdog\.[0-9A-F]{16}$", RegexOptions.Compiled);

        // -----------------------------------------------------------------
        // Required-field validation
        // -----------------------------------------------------------------

        [Fact]
        public void Normalize_NullTargetPath_Throws()
        {
            var o = new WatchdogOptions { TargetPath = null };

            Assert.Throws<InvalidOperationException>(() => o.Normalize());
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Normalize_BlankTargetPath_Throws(string blank)
        {
            var o = new WatchdogOptions { TargetPath = blank };

            Assert.Throws<InvalidOperationException>(() => o.Normalize());
        }

        [Fact]
        public void Normalize_MissingTargetFile_ThrowsFileNotFound()
        {
            using var ws = new TempWorkspace();
            var ghost = ws.Path("does-not-exist.exe");

            var o = new WatchdogOptions { TargetPath = ghost };

            Assert.Throws<FileNotFoundException>(() => o.Normalize());
        }

        // -----------------------------------------------------------------
        // Pipe identity (SHA1 of full lowercased path, first 16 hex chars)
        // -----------------------------------------------------------------

        [Fact]
        public void Normalize_ValidTarget_DerivesPipeNameFromHash()
        {
            using var ws = new TempWorkspace();
            var target = ws.WriteFile("app.exe");

            var o = new WatchdogOptions { TargetPath = target };
            o.Normalize();

            Assert.Matches(PipeNamePattern, o.PipeName);
        }

        [Fact]
        public void Normalize_SameTarget_ProducesDeterministicPipeName()
        {
            using var ws = new TempWorkspace();
            var target = ws.WriteFile("app.exe");

            var a = new WatchdogOptions { TargetPath = target };
            var b = new WatchdogOptions { TargetPath = target };
            a.Normalize();
            b.Normalize();

            Assert.Equal(a.PipeName, b.PipeName);
        }

        [Fact]
        public void Normalize_DifferentTargets_ProduceDifferentPipeNames()
        {
            using var ws = new TempWorkspace();
            var first = ws.WriteFile("one.exe");
            var second = ws.WriteFile("two.exe");

            var a = new WatchdogOptions { TargetPath = first };
            var b = new WatchdogOptions { TargetPath = second };
            a.Normalize();
            b.Normalize();

            Assert.NotEqual(a.PipeName, b.PipeName);
        }

        [Fact]
        public void Normalize_OverwritesCallerSuppliedPipeName_WithHash()
        {
            // Pins current behavior: Normalize() ignores an explicit PipeName and
            // replaces it with the hash-derived identity (audit finding — kept as a
            // regression lock, not an endorsement).
            using var ws = new TempWorkspace();
            var target = ws.WriteFile("app.exe");

            var o = new WatchdogOptions
            {
                TargetPath = target,
                PipeName = "my.custom.pipe"
            };
            o.Normalize();

            Assert.NotEqual("my.custom.pipe", o.PipeName);
            Assert.Matches(PipeNamePattern, o.PipeName);
        }

        // -----------------------------------------------------------------
        // TargetPath / WorkingDirectory resolution
        // -----------------------------------------------------------------

        [Fact]
        public void Normalize_ResolvesTargetPathToFullPath()
        {
            using var ws = new TempWorkspace();
            var target = ws.WriteFile("app.exe");

            var o = new WatchdogOptions { TargetPath = target };
            o.Normalize();

            Assert.Equal(System.IO.Path.GetFullPath(target), o.TargetPath);
        }

        [Fact]
        public void Normalize_DefaultsWorkingDirectory_ToTargetDirectory()
        {
            using var ws = new TempWorkspace();
            var target = ws.WriteFile("nested\\app.exe");

            var o = new WatchdogOptions { TargetPath = target };
            o.Normalize();

            Assert.Equal(System.IO.Path.GetDirectoryName(o.TargetPath), o.WorkingDirectory);
        }

        [Fact]
        public void Normalize_RespectsExplicitWorkingDirectory()
        {
            using var ws = new TempWorkspace();
            var target = ws.WriteFile("app.exe");
            var work = ws.CreateDir("work");

            var o = new WatchdogOptions { TargetPath = target, WorkingDirectory = work };
            o.Normalize();

            Assert.Equal(System.IO.Path.GetFullPath(work), o.WorkingDirectory);
        }

        // -----------------------------------------------------------------
        // Restart-delay clamping (floor of 200 ms)
        // -----------------------------------------------------------------

        [Theory]
        [InlineData(0, 200)]
        [InlineData(50, 200)]
        [InlineData(199, 200)]
        [InlineData(200, 200)]
        [InlineData(5000, 5000)]
        public void Normalize_ClampsRestartDelayToFloor(int input, int expected)
        {
            using var ws = new TempWorkspace();
            var target = ws.WriteFile("app.exe");

            var o = new WatchdogOptions { TargetPath = target, RestartDelayMs = input };
            o.Normalize();

            Assert.Equal(expected, o.RestartDelayMs);
        }

        // -----------------------------------------------------------------
        // Log-path defaulting
        // -----------------------------------------------------------------

        [Fact]
        public void Normalize_DefaultsLogPath_WhenFileLoggingEnabled()
        {
            using var ws = new TempWorkspace();
            var target = ws.WriteFile("app.exe");

            var o = new WatchdogOptions { TargetPath = target, EnableFileLogging = true };
            o.Normalize();

            Assert.Equal(
                System.IO.Path.Combine(o.WorkingDirectory, "watchdog.log"),
                o.LogPath);
        }

        [Fact]
        public void Normalize_LeavesLogPathUnset_WhenFileLoggingDisabled()
        {
            using var ws = new TempWorkspace();
            var target = ws.WriteFile("app.exe");

            var o = new WatchdogOptions
            {
                TargetPath = target,
                EnableFileLogging = false,
                LogPath = null
            };
            o.Normalize();

            Assert.True(string.IsNullOrWhiteSpace(o.LogPath));
        }

        // -----------------------------------------------------------------
        // Crash / update directory defaulting + eager creation
        // -----------------------------------------------------------------

        [Fact]
        public void Normalize_DefaultsCrashRoots_UnderWorkingDirectory()
        {
            using var ws = new TempWorkspace();
            var target = ws.WriteFile("app.exe");

            var o = new WatchdogOptions { TargetPath = target };
            o.Normalize();

            Assert.Equal(
                System.IO.Path.Combine(o.WorkingDirectory, "crash", "pending"),
                o.PendingCrashRoot);
            Assert.Equal(
                System.IO.Path.Combine(o.WorkingDirectory, "crash", "bundles"),
                o.BundleRoot);
            Assert.Equal(
                System.IO.Path.Combine(o.WorkingDirectory, "updates"),
                o.UpdateStagingRoot);
        }

        [Fact]
        public void Normalize_CreatesCrashAndUpdateDirectories()
        {
            using var ws = new TempWorkspace();
            var target = ws.WriteFile("app.exe");

            var o = new WatchdogOptions { TargetPath = target };
            o.Normalize();

            Assert.True(Directory.Exists(o.PendingCrashRoot));
            Assert.True(Directory.Exists(o.BundleRoot));
            Assert.True(Directory.Exists(o.UpdateStagingRoot));
        }

        // -----------------------------------------------------------------
        // Control hotkey defaults (Ctrl+Alt+P / R / Q)
        // -----------------------------------------------------------------

        [Fact]
        public void Defaults_EnableHotkeys_WithCtrlAltPRQ()
        {
            var o = new WatchdogOptions();

            Assert.True(o.EnableHotkeys);

            uint ctrlAlt = WatchdogHotkeys.MOD_CONTROL | WatchdogHotkeys.MOD_ALT;

            Assert.Equal(ctrlAlt, o.PauseHotkey.Modifiers);
            Assert.Equal(WatchdogHotkeys.VK_P, o.PauseHotkey.VirtualKey);

            Assert.Equal(ctrlAlt, o.ResumeHotkey.Modifiers);
            Assert.Equal(WatchdogHotkeys.VK_R, o.ResumeHotkey.VirtualKey);

            Assert.Equal(ctrlAlt, o.StopHotkey.Modifiers);
            Assert.Equal(WatchdogHotkeys.VK_Q, o.StopHotkey.VirtualKey);
        }
    }
}
