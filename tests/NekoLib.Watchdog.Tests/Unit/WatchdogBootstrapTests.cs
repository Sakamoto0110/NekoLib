using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Pipes;
using NekoLib.Watchdog;
using Xunit;

#if NET481
using Newtonsoft.Json.Linq;
#else
using System.Text.Json;
#endif

namespace NekoLib.Watchdog.Tests.Unit
{
    public sealed class WatchdogBootstrapTests
    {
        public WatchdogBootstrapTests()
        {
            // net481 PipeServer/PipeClient adapt blocking named-pipe APIs through
            // pool work items. xUnit also schedules tests on that pool, so keep
            // enough workers available for the in-process client/server fixture.
            ThreadPool.GetMinThreads(out var workers, out var completions);
            ThreadPool.SetMinThreads(Math.Max(workers, 32), completions);
        }

        [Fact]
        public void EnsureStarted_UnderWatchdog_ReturnsBeforeStartingHost()
        {
            var name = WatchdogBootstrap.UnderWatchdogEnvironmentVariable;
            var previous = Environment.GetEnvironmentVariable(name);
            try
            {
                Environment.SetEnvironmentVariable(name, "1");

                // Null would normally fail validation. Returning proves the
                // recursion guard runs before host/path/argument work.
                WatchdogBootstrap.EnsureStarted(null, 1);
            }
            finally
            {
                Environment.SetEnvironmentVariable(name, previous);
            }
        }

        [Fact]
        public void EnsureStarted_UnderWatchdogWithEmptyValue_ReturnsBeforeStartingHost()
        {
            var name = WatchdogBootstrap.UnderWatchdogEnvironmentVariable;
            var previous = Environment.GetEnvironmentVariable(name);
            try
            {
                Assert.True(SetEnvironmentVariable(name, string.Empty));
                Assert.True(Environment.GetEnvironmentVariables().Contains(name));

                WatchdogBootstrap.EnsureStarted(null, 1);
            }
            finally
            {
                if (previous == null)
                    SetEnvironmentVariable(name, null);
                else
                    SetEnvironmentVariable(name, previous);
            }
        }

        [Fact]
        public void EnsureStarted_ExistingTarget_RequiresCurrentAttachmentIdentity()
        {
            string targetPath;
            int currentPid;
            using (var current = Process.GetCurrentProcess())
            {
                targetPath = current.MainModule.FileName;
                currentPid = current.Id;
            }

            var otherPid = currentPid == int.MaxValue
                ? currentPid - 1
                : currentPid + 1;

#if NET481
            // The net481 PipeServer adapts blocking pipe calls through the
            // ThreadPool; hosting both ends inside the xUnit worker can make a
            // sub-second preflight inherently scheduler-dependent. Exercise the
            // shared production identity decision directly here. The live
            // attach-status transport is covered by WaitForAttachment below and
            // by WatchdogAttachTests.
            WatchdogBootstrap.ValidateAttachedProcessId(currentPid, currentPid);
            var identityError = Assert.Throws<InvalidOperationException>(
                () => WatchdogBootstrap.ValidateAttachedProcessId(
                    otherPid,
                    currentPid));
            Assert.Contains(otherPid.ToString(), identityError.Message);
            Assert.Contains(currentPid.ToString(), identityError.Message);
#else
            var mode = 0;
            var pipeName = WatchdogController.ResolvePipeNameForTarget(targetPath);
            using (var server = new PipeServer(new PipeServerOptions
            {
                PipeName = pipeName
            }))
            {
                server.Map(
                    WatchdogCommands.Ping,
                    (request, cancellationToken) => Task.FromResult(Pong()));
                server.Map(
                    WatchdogCommands.ProtocolVersion,
                    (request, cancellationToken) => Task.FromResult(
                        StringResponse(WatchdogBootstrap.HostProtocolVersion)));
                server.Map(
                    WatchdogCommands.AttachStatus,
                    (request, cancellationToken) =>
                    {
                        if (mode == 2)
                            return Task.FromResult(ErrorResponse(
                                "attach_not_requested"));

                        var pid = mode == 0 ? currentPid : otherPid;
                        return Task.FromResult(StringResponse(
                            WatchdogBootstrap.FormatAttachmentStatus(
                                pid,
                                "existing-token")));
                    });
                server.Start();
                Thread.Sleep(100);

                WatchdogBootstrap.EnsureStarted(Array.Empty<string>(), 1000);

                mode = 1;
                var wrongPidError = Assert.Throws<InvalidOperationException>(
                    () => WatchdogBootstrap.EnsureStarted(
                        Array.Empty<string>(),
                        1000));

                Assert.Contains(otherPid.ToString(), wrongPidError.Message);
                Assert.Contains(currentPid.ToString(), wrongPidError.Message);

                mode = 2;
                var missingIdentityError = Assert.Throws<InvalidOperationException>(
                    () => WatchdogBootstrap.EnsureStarted(
                        Array.Empty<string>(),
                        1000));

                Assert.Contains(
                    "could not confirm supervision",
                    missingIdentityError.Message);
            }
#endif
        }

        [Fact]
        public void BuildCommandLine_SpacesQuotesAndTrailingSlashes_RoundTrip()
        {
            var expected = new[]
            {
                "plain",
                "two words",
                "say \"hello\"",
                @"C:\folder with spaces\",
                ""
            };

            var commandLine = WatchdogBootstrap.BuildCommandLine(expected);
            var actual = ParseWindowsCommandLine(commandLine);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void WaitForAttachment_MissingPipe_TimesOutWithinBound()
        {
            var elapsed = Stopwatch.StartNew();

            var attached = WatchdogBootstrap.WaitForAttachment(
                "NekoLib.Watchdog.Missing." + Guid.NewGuid().ToString("N"),
                1234,
                "token",
                250);

            Assert.False(attached);
            Assert.InRange(elapsed.ElapsedMilliseconds, 50, 1500);
        }

        [Fact]
        public void WaitForAttachment_SlowResponse_UsesOneTotalTimeoutBudget()
        {
            var pipeName =
                "NekoLib.Watchdog.Slow." + Guid.NewGuid().ToString("N");
            using (var server = new PipeServer(new PipeServerOptions
            {
                PipeName = pipeName
            }))
            {
                server.Map(
                    WatchdogCommands.ProtocolVersion,
                    (request, cancellationToken) => Task.FromResult(
                        StringResponse(WatchdogBootstrap.HostProtocolVersion)));
                server.Map(
                    WatchdogCommands.AttachStatus,
                    async (request, cancellationToken) =>
                    {
                        await Task.Delay(1000);
                        return StringResponse(
                            WatchdogBootstrap.FormatAttachmentStatus(
                                1234,
                                "token"));
                    });
                server.Start();

                var elapsed = Stopwatch.StartNew();
                var attached = WatchdogBootstrap.WaitForAttachment(
                    pipeName,
                    1234,
                    "token",
                    150);

                Assert.False(attached);
                Assert.InRange(elapsed.ElapsedMilliseconds, 50, 1500);
            }
        }

        [Fact]
        public void WaitForAttachment_IncompatibleProtocol_ThrowsClearMismatch()
        {
            var pipeName =
                "NekoLib.Watchdog.Protocol." + Guid.NewGuid().ToString("N");
            using (var server = new PipeServer(new PipeServerOptions
            {
                PipeName = pipeName
            }))
            {
                server.Map(
                    WatchdogCommands.ProtocolVersion,
                    (request, cancellationToken) => Task.FromResult(
                        StringResponse("0")));
                server.Start();
                Thread.Sleep(250);

                var error = Assert.Throws<InvalidOperationException>(() =>
                    WatchdogBootstrap.WaitForAttachment(
                        pipeName,
                        1234,
                        "token",
                        5000));

                Assert.Contains("incompatible protocol", error.Message);
                Assert.Contains("version '0'", error.Message);
                Assert.Contains(
                    WatchdogBootstrap.HostProtocolVersion,
                    error.Message);
            }
        }

        [Fact]
        public void FormatAttachmentStatus_UsesVersionedIdentity()
        {
            Assert.Equal(
                "attached:v" +
                WatchdogBootstrap.HostProtocolVersion +
                ":1234:token",
                WatchdogBootstrap.FormatAttachmentStatus(1234, "token"));
        }

        private static string[] ParseWindowsCommandLine(string commandLine)
        {
            var argv = CommandLineToArgvW(commandLine, out var count);
            if (argv == IntPtr.Zero)
                throw new InvalidOperationException("CommandLineToArgvW failed.");

            try
            {
                var result = new List<string>(count);
                for (int i = 0; i < count; i++)
                {
                    var value = Marshal.ReadIntPtr(argv, i * IntPtr.Size);
                    result.Add(Marshal.PtrToStringUni(value));
                }
                return result.ToArray();
            }
            finally
            {
                LocalFree(argv);
            }
        }

        private static PipeMessage Pong()
            => StringResponse("pong");

        private static PipeMessage StringResponse(string value)
        {
#if NET481
            return new PipeMessage
            {
                Ok = true,
                Data = JToken.FromObject(value)
            };
#else
            return new PipeMessage
            {
                Ok = true,
                Data = JsonSerializer.SerializeToElement(value)
            };
#endif
        }

        private static PipeMessage ErrorResponse(string code)
            => new PipeMessage
            {
                Ok = false,
                Error = new PipeError
                {
                    Code = code,
                    Message = code
                }
            };

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern IntPtr CommandLineToArgvW(
            [MarshalAs(UnmanagedType.LPWStr)] string commandLine,
            out int argumentCount);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);

        [DllImport(
            "kernel32.dll",
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetEnvironmentVariable(
            string name,
            string value);
    }
}
