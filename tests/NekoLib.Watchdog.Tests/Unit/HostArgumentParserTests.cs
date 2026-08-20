using System;
using System.Collections.Generic;
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

            var options = HostArgumentParser.Parse(Args(
                "--target", target,
                "--workdir", workdir,
                "--args", targetArguments,
                "--attach-pid", "1234",
                "--attach-token", "token-1234"));

            Assert.Equal(Path.GetFullPath(target), options.TargetPath);
            Assert.Equal(Path.GetFullPath(workdir), options.WorkingDirectory);
            Assert.Equal(targetArguments, options.TargetArguments);
            Assert.Equal(1234, options.InitialProcessId);
            Assert.Equal("token-1234", options.AttachToken);
        }

        [Fact]
        public void Parse_WithoutWorkdir_PreservesRuntimeDefault()
        {
            using var workspace = new TempWorkspace();
            var target = workspace.WriteFile("app.exe");

            var options = HostArgumentParser.Parse(Args("--target", target));

            Assert.Equal(string.Empty, options.WorkingDirectory);
        }

        [Fact]
        public void Parse_MissingWorkdir_Throws()
        {
            using var workspace = new TempWorkspace();
            var target = workspace.WriteFile("app.exe");
            var missing = workspace.Path("missing");

            var error = Assert.Throws<DirectoryNotFoundException>(() =>
                HostArgumentParser.Parse(Args(
                    "--target", target,
                    "--workdir", missing)));

            Assert.Contains(Path.GetFullPath(missing), error.Message);
        }

        [Fact]
        public void Parse_FileAsWorkdir_Throws()
        {
            using var workspace = new TempWorkspace();
            var target = workspace.WriteFile("app.exe");
            var file = workspace.WriteFile("not-a-directory.txt");

            var error = Assert.Throws<ArgumentException>(() =>
                HostArgumentParser.Parse(Args(
                    "--target", target,
                    "--workdir", file)));

            Assert.Contains("directory", error.Message);
        }

        [Fact]
        public void Parse_MissingProtocolVersion_Throws()
        {
            using var workspace = new TempWorkspace();
            var target = workspace.WriteFile("app.exe");

            var error = Assert.Throws<ArgumentException>(() =>
                HostArgumentParser.Parse(new[] { "--target", target }));

            Assert.Contains("--protocol-version", error.Message);
        }

        [Fact]
        public void Parse_UnsupportedProtocolVersion_Throws()
        {
            using var workspace = new TempWorkspace();
            var target = workspace.WriteFile("app.exe");

            var error = Assert.Throws<NotSupportedException>(() =>
                HostArgumentParser.Parse(new[]
                {
                    "--protocol-version", "999",
                    "--target", target
                }));

            Assert.Contains("999", error.Message);
            Assert.Contains(WatchdogBootstrap.HostProtocolVersion, error.Message);
        }

        [Fact]
        public void Parse_AttachPidWithoutToken_Throws()
        {
            using var workspace = new TempWorkspace();
            var target = workspace.WriteFile("app.exe");

            var error = Assert.Throws<ArgumentException>(() =>
                HostArgumentParser.Parse(Args(
                    "--target", target,
                    "--attach-pid", "1234")));

            Assert.Contains("--attach-token", error.Message);
        }

        [Fact]
        public void Parse_AttachTokenWithoutPid_Throws()
        {
            using var workspace = new TempWorkspace();
            var target = workspace.WriteFile("app.exe");

            var error = Assert.Throws<ArgumentException>(() =>
                HostArgumentParser.Parse(Args(
                    "--target", target,
                    "--attach-token", "token")));

            Assert.Contains("--attach-pid", error.Message);
        }

        [Fact]
        public void Parse_AttachPidWithoutValue_Throws()
        {
            using var workspace = new TempWorkspace();
            var target = workspace.WriteFile("app.exe");

            var error = Assert.Throws<ArgumentException>(() =>
                HostArgumentParser.Parse(Args(
                    "--target", target,
                    "--attach-pid")));

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
                HostArgumentParser.Parse(Args(
                    "--target", target,
                    "--attach-pid", value,
                    "--attach-token", "token")));
        }

        [Fact]
        public void Parse_UnknownArgument_Throws()
        {
            using var workspace = new TempWorkspace();
            var target = workspace.WriteFile("app.exe");

            var error = Assert.Throws<ArgumentException>(() =>
                HostArgumentParser.Parse(Args(
                    "--target", target,
                    "--typo", "value")));

            Assert.Contains("Unknown", error.Message);
            Assert.Contains("--typo", error.Message);
        }

        [Fact]
        public void Parse_DuplicateArgument_Throws()
        {
            using var workspace = new TempWorkspace();
            var target = workspace.WriteFile("app.exe");

            var error = Assert.Throws<ArgumentException>(() =>
                HostArgumentParser.Parse(Args(
                    "--target", target,
                    "--target", target)));

            Assert.Contains("Duplicate", error.Message);
            Assert.Contains("--target", error.Message);
        }

        private static string[] Args(params string[] arguments)
        {
            var result = new List<string>
            {
                "--protocol-version",
                WatchdogBootstrap.HostProtocolVersion
            };
            result.AddRange(arguments);
            return result.ToArray();
        }
    }
}
