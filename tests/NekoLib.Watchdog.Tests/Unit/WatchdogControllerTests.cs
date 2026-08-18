using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using NekoLib.Pipes;
using Xunit;

#if NET9_0_OR_GREATER
using System.Text.Json;
#else
using Newtonsoft.Json.Linq;
#endif

namespace NekoLib.Watchdog.Tests.Unit
{
    public sealed class WatchdogControllerTests
    {
        [Fact]
        public void MutatingCommands_AcceptedResponses_ReturnTrue()
        {
            using var server = StartControllerServer(
                (WatchdogCommands.Pause, "paused"),
                (WatchdogCommands.Resume, "running"),
                (WatchdogCommands.Restart, "restarting"),
                (WatchdogCommands.Stop, "stopped"));

            Assert.True(WatchdogController.Pause());
            Assert.True(WatchdogController.Resume());
            Assert.True(WatchdogController.Restart());
            Assert.True(WatchdogController.Stop());
        }

        [Fact]
        public void MutatingCommands_ProtocolErrors_ReturnFalse()
        {
            using var server = StartErrorServer(
                WatchdogCommands.Pause,
                WatchdogCommands.Resume,
                WatchdogCommands.Restart,
                WatchdogCommands.Stop);

            Assert.False(WatchdogController.Pause());
            Assert.False(WatchdogController.Resume());
            Assert.False(WatchdogController.Restart());
            Assert.False(WatchdogController.Stop());
        }

        [Fact]
        public void MutatingCommand_NoServer_ReturnsFalse()
        {
            Assert.False(WatchdogController.Pause());
        }

        [Fact]
        public void SubscribeLogs_ReplayUsesSerializerNeutralMetadataJson()
        {
            using var server = NewServer();
            server.Map(
                WatchdogCommands.LogHistory,
                (request, cancellationToken) => Task.FromResult(Ok(new[]
                {
                    new
                    {
                        tsUnixMs = 42L,
                        level = "info",
                        msg = "first",
                        meta = (object)new { value = 7 },
                        line = (string)null
                    },
                    new
                    {
                        tsUnixMs = 43L,
                        level = "warn",
                        msg = "second",
                        meta = (object)null,
                        line = "line"
                    }
                })));
            server.Map(
                WatchdogCommands.Ping,
                (request, cancellationToken) => Task.FromResult(Ok("pong")));
            server.Start();
            Assert.True(WaitUntil(WatchdogController.Ping));
            var received = new List<WatchdogController.LogEvent>();

            using var subscription = WatchdogController.SubscribeLogs(received.Add);

            Assert.Equal(2, received.Count);
            Assert.Equal("{\"value\":7}", received[0].MetaJson);
            Assert.Null(received[1].MetaJson);
            Assert.Null(received[0].Line);
        }

        [Fact]
        public void SubscribeLogs_ThrowingReplayCallback_IsolatedPerEntry()
        {
            using var server = NewServer();
            server.Map(
                WatchdogCommands.LogHistory,
                (request, cancellationToken) => Task.FromResult(Ok(new[]
                {
                    new { tsUnixMs = 1L, msg = "first" },
                    new { tsUnixMs = 2L, msg = "second" }
                })));
            server.Map(
                WatchdogCommands.Ping,
                (request, cancellationToken) => Task.FromResult(Ok("pong")));
            server.Start();
            Assert.True(WaitUntil(WatchdogController.Ping));
            var calls = 0;

            using var subscription = WatchdogController.SubscribeLogs(value =>
            {
                calls++;
                throw new InvalidOperationException("subscriber");
            });

            Assert.Equal(2, calls);
        }

        private static PipeServer StartControllerServer(
            params (string Command, string Response)[] handlers)
        {
            var server = NewServer();
            foreach (var handler in handlers)
            {
                server.Map(
                    handler.Command,
                    (request, cancellationToken) =>
                        Task.FromResult(Ok(handler.Response)));
            }

            server.Start();
            return server;
        }

        private static PipeServer StartErrorServer(params string[] commands)
        {
            var server = NewServer();
            foreach (var command in commands)
            {
                server.Map(
                    command,
                    (request, cancellationToken) => Task.FromResult(new PipeMessage
                    {
                        Ok = false,
                        Error = new PipeError
                        {
                            Code = "rejected",
                            Message = "Rejected by test server."
                        }
                    }));
            }

            server.Start();
            return server;
        }

        private static PipeServer NewServer()
        {
            using var process = Process.GetCurrentProcess();
            var targetPath = process.MainModule.FileName;
            return new PipeServer(new PipeServerOptions
            {
                PipeName = WatchdogController.ResolvePipeNameForTarget(targetPath),
                AccessPolicy = PipeAccessPolicy.CurrentUserOnly,
                EnableEvents = true
            });
        }

        private static PipeMessage Ok(string value)
        {
            return Ok((object)value);
        }

        private static PipeMessage Ok(object value)
        {
#if NET9_0_OR_GREATER
            return new PipeMessage
            {
                Ok = true,
                Data = JsonSerializer.SerializeToElement(value)
            };
#else
            return new PipeMessage
            {
                Ok = true,
                Data = JToken.FromObject(value)
            };
#endif
        }

        private static bool WaitUntil(Func<bool> condition)
        {
            var timeout = Stopwatch.StartNew();
            while (timeout.Elapsed < TimeSpan.FromSeconds(5))
            {
                if (condition())
                    return true;
                System.Threading.Thread.Sleep(25);
            }

            return condition();
        }
    }
}
