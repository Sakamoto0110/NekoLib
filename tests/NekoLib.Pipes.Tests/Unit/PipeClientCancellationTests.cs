using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Pipes.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Pipes.Tests.Unit
{
    public sealed class PipeClientCancellationTests
    {
        [Fact]
        public async Task SendAsync_CancelledDuringConnect_ReturnsPromptly()
        {
            var client = new PipeClient(new PipeClientOptions
            {
                PipeName = PipeTestUtil.UniqueName(),
                ConnectTimeout = TimeSpan.FromSeconds(10),
                RequestTimeout = TimeSpan.FromSeconds(10)
            });
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.CancelAfter(150);
                var stopwatch = Stopwatch.StartNew();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => client.SendAsync("unavailable", null, cancellation.Token));

                stopwatch.Stop();
                Assert.True(
                    stopwatch.Elapsed < TimeSpan.FromSeconds(3),
                    "connect cancellation took " + stopwatch.Elapsed);
            }
        }
    }
}
