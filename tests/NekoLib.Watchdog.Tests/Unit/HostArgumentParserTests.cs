using System;
using System.IO;
using NekoLib.Watchdog.Host;
using NekoLib.Watchdog.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Watchdog.Tests.Unit
{
    public sealed class HostArgumentParserTests
    {
        [Fact]
        public void Parse_TargetWorkdirArgumentsAndAttachPid_PreservesValues()
        {
            using var workspace = new TempWorkspace();
            var target = workspace.WriteFile("app folder\\sample app.exe");
            var workdir = workspace.CreateDir("working folder");
            var targetArguments = "--name \"A B\" --value \"quoted \\\"text\\\"\"";

            var options = HostArgumentParser.Parse(new[]
            {
                "--target", target,
                "--workdir", workdir,
                "--args", targetArguments,
                "--attach-pid", "1234",
                "--attach-token", "token-1234"
            });

            Assert.Equal(Path.GetFullPath(target), options.TargetPath);
            Assert.Equal(Path.GetFullPath(workdir), options.WorkingDirectory);
            Assert.Equal(targetArguments, options.TargetArguments);
            Assert.Equal(1234, options.InitialProcessId);
            Assert.Equal("token-1234", options.AttachToken);
        }

        [Fact]
        public void Parse_AttachPidWithoutToken_Throws()
        {
            using var workspace = new TempWorkspace();
            var target = workspace.WriteFile("app.exe");

            var error = Assert.Throws<ArgumentException>(() =>
                HostArgumentParser.Parse(new[]
                {
                    "--target", target,
                    "--attach-pid", "1234"
                }));

            Assert.Contains("--attach-token", error.Message);
        }

        [Fact]
        public void Parse_AttachTokenWithoutPid_Throws()
        {
            using var workspace = new TempWorkspace();
            var target = workspace.WriteFile("app.exe");

            var error = Assert.Throws<ArgumentException>(() =>
                HostArgumentParser.Parse(new[]
                {
                    "--target", target,
                    "--attach-token", "token"
                }));

            Assert.Contains("--attach-pid", error.Message);
        }

        [Fact]
        public void Parse_AttachPidWithoutValue_Throws()
        {
            using var workspace = new TempWorkspace();
            var target = workspace.WriteFile("app.exe");

            var error = Assert.Throws<ArgumentException>(() =>
                HostArgumentParser.Parse(new[]
                {
                    "--target", target,
                    "--attach-pid"
                }));

            Assert.Contains("requires a value", error.Message);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("not-a-pid")]
        public void Parse_InvalidAttachPid_Throws(string value)
        {
            using var workspace = new TempWorkspace();
            var target = workspace.WriteFile("app.exe");

            Assert.Throws<ArgumentException>(() =>
                HostArgumentParser.Parse(new[]
                {
                    "--target", target,
                    "--attach-pid", value,
                    "--attach-token", "token"
                }));
        }

        [Fact]
        public void Parse_UnknownArgument_Throws()
        {
            using var workspace = new TempWorkspace();
            var target = workspace.WriteFile("app.exe");

            var error = Assert.Throws<ArgumentException>(() =>
                HostArgumentParser.Parse(new[]
                {
                    "--target", target,
                    "--typo", "value"
                }));

            Assert.Contains("Unknown", error.Message);
            Assert.Contains("--typo", error.Message);
        }

        [Fact]
        public void Parse_DuplicateArgument_Throws()
        {
            using var workspace = new TempWorkspace();
            var target = workspace.WriteFile("app.exe");

            var error = Assert.Throws<ArgumentException>(() =>
                HostArgumentParser.Parse(new[]
                {
                    "--target", target,
                    "--target", target
                }));

            Assert.Contains("Duplicate", error.Message);
            Assert.Contains("--target", error.Message);
        }
    }
}
