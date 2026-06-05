using System;
using System.Threading.Tasks;
using Xunit;

namespace NekoLib.Pipes.Tests.Unit
{
    /// <summary>
    /// Pure tests for <see cref="SimplePipeMetrics"/> — counter accumulation and
    /// the latency math (last / max / running average / EMA) exposed via
    /// <see cref="PipeMetricsSnapshot"/>. Single-threaded, so the latency values
    /// are deterministic (the concurrent-update race noted as audit M1 is a
    /// separate concern and not exercised here).
    /// </summary>
    public class SimplePipeMetricsTests
    {
        [Fact]
        public void ServerRequestAndResponse_AccumulateCounts()
        {
            var m = new SimplePipeMetrics();

            m.OnServerRequestReceived("p", "cmd");
            m.OnServerResponseSent("p", "cmd", ok: true, elapsed: TimeSpan.FromMilliseconds(10));
            m.OnServerResponseSent("p", "cmd", ok: false, elapsed: TimeSpan.FromMilliseconds(20));

            var s = m.Snapshot().Server;

            Assert.Equal(1L, s.Requests);
            Assert.Equal(1L, s.Success);
            Assert.Equal(1L, s.Failures);
        }

        [Fact]
        public void ServerClientConnectDisconnect_TracksLiveCount()
        {
            var m = new SimplePipeMetrics();

            m.OnServerClientConnected("p");
            m.OnServerClientConnected("p");
            m.OnServerClientDisconnected("p");

            Assert.Equal(1L, m.Snapshot().Server.ConnectedClients);
        }

        [Fact]
        public void Latency_TracksLastMaxAverageAndEma()
        {
            var m = new SimplePipeMetrics();

            m.OnServerResponseSent("p", "cmd", true, TimeSpan.FromMilliseconds(10));
            m.OnServerResponseSent("p", "cmd", true, TimeSpan.FromMilliseconds(50));

            var s = m.Snapshot().Server;

            Assert.Equal(50L, s.LastLatencyMs);
            Assert.Equal(50L, s.MaxLatencyMs);
            Assert.Equal(30.0, s.AverageLatencyMs);          // (10 + 50) / 2
            Assert.Equal(18.0, s.EmaLatencyMs);              // 10, then 10*0.8 + 50*0.2
        }

        [Fact]
        public void ClientConnect_CountsAttemptsSuccessAndFailure()
        {
            var m = new SimplePipeMetrics();

            m.OnClientConnect("p", TimeSpan.Zero, ok: true, errorCode: null);
            m.OnClientConnect("p", TimeSpan.Zero, ok: false, errorCode: "connect_failed");

            var c = m.Snapshot().Client;

            Assert.Equal(2L, c.ConnectAttempts);
            Assert.Equal(1L, c.ConnectSuccess);
            Assert.Equal(1L, c.ConnectFailures);
        }

        [Fact]
        public void EventsPublished_AccumulateDeliveredAndFailed()
        {
            var m = new SimplePipeMetrics();

            m.OnServerEventPublished("p", "e", subscribers: 3, success: 2, failed: 1);

            var e = m.Snapshot().Events;

            Assert.Equal(1L, e.Published);
            Assert.Equal(2L, e.Delivered);
            Assert.Equal(1L, e.Failed);
        }

        [Fact]
        public void OnError_IncrementsErrorTotal()
        {
            var m = new SimplePipeMetrics();

            m.OnError("p", "accept_loop", new InvalidOperationException());
            m.OnError("p", "dispatch", new Exception());

            Assert.Equal(2L, m.Snapshot().Errors.Total);
        }

        [Fact]
        public void ConcurrentResponses_ProduceExactCounts_AndConsistentMax()
        {
            // Thread-safety lock for audit M1: under parallel load the counters must
            // not lose updates and the derived latency stats must not tear/crash.
            var m = new SimplePipeMetrics();
            const int threads = 8;
            const int perThread = 1000;

            Parallel.For(0, threads, _ =>
            {
                for (int i = 0; i < perThread; i++)
                    m.OnServerResponseSent("p", "cmd", ok: true, elapsed: TimeSpan.FromMilliseconds(i % 100));
            });

            var s = m.Snapshot().Server;

            Assert.Equal((long)(threads * perThread), s.Success);
            Assert.Equal(0L, s.Failures);
            Assert.Equal(99L, s.MaxLatencyMs);                 // max of (i % 100)
            Assert.InRange(s.AverageLatencyMs, 0.0, 99.0);     // finite, untorn
        }

        [Fact]
        public void NoopMetrics_SnapshotIsNull()
        {
            // Pins the current contract (audit L5 suggests an empty snapshot instead).
            IPipeMetrics noop = NoopPipeMetrics.Instance;
            Assert.Null(noop.Snapshot());
        }
    }
}
