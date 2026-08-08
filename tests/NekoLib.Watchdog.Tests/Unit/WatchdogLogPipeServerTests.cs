using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading;
using Xunit;

namespace NekoLib.Watchdog.Tests.Unit
{
    public sealed class WatchdogLogPipeServerTests
    {
        [Fact]
        public void Dispose_PendingAccept_UnblocksAndJoinsThreads()
        {
            var name = "neko.watchdog.log.test." + Guid.NewGuid().ToString("N");

#pragma warning disable CS0618
            var server = new WatchdogLogPipeServer(name);
#pragma warning restore CS0618
            server.Start();
            Assert.True(WaitUntil(() => server.HasPendingAccept));

            var sw = Stopwatch.StartNew();
            server.Dispose();
            sw.Stop();

            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3));
            Assert.False(server.IsAcceptThreadAlive);
            Assert.False(server.IsDispatchThreadAlive);
            Assert.False(server.HasPendingAccept);
        }

        [Fact]
        public void Dispose_ConnectedNonReadingClient_UnblocksDispatchAndJoinsThreads()
        {
            var name = "neko.watchdog.log.test." + Guid.NewGuid().ToString("N");

#pragma warning disable CS0618
            var server = new WatchdogLogPipeServer(name);
#pragma warning restore CS0618
            server.Start();

            using (var client = new NamedPipeClientStream(
                ".",
                name,
                PipeDirection.In,
                PipeOptions.Asynchronous))
            {
                client.Connect(3000);
                Assert.True(WaitUntil(() => server.ConnectedClientCount == 1));

                var largeLine = new string('x', 512 * 1024);
                for (var i = 0; i < 16; i++)
                    server.Enqueue(largeLine);

                var sw = Stopwatch.StartNew();
                server.Dispose();
                sw.Stop();

                Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3));
                Assert.False(server.IsAcceptThreadAlive);
                Assert.False(server.IsDispatchThreadAlive);
            }
        }

        private static bool WaitUntil(Func<bool> condition, int timeoutMs = 5000)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (condition())
                    return true;
                Thread.Sleep(20);
            }

            return condition();
        }
    }
}
