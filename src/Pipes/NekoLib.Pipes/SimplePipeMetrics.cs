using System;
using System.Threading;

namespace NekoLib.Pipes
{
    /// <summary>
    /// Provides a thread-safe cumulative in-memory <see cref="IPipeMetrics"/>
    /// implementation. Create a new instance to start a new measurement window.
    /// </summary>
    public sealed class SimplePipeMetrics : IPipeMetrics
    {
        /// <summary>Initializes an empty cumulative metrics collector.</summary>
        public SimplePipeMetrics()
        {
        }

        // ================= SERVER =================

        private long _srvClients;
        private long _srvReq;
        private long _srvOk;
        private long _srvFail;

        private long _srvLastLatency;
        private long _srvMaxLatency;
        private double _srvAvgLatency;
        private double _srvEmaLatency;

        // ================= CLIENT =================

        private long _cliConnAttempts;
        private long _cliConnOk;
        private long _cliConnFail;

        private long _cliReq;
        private long _cliOk;
        private long _cliFail;

        private long _cliLastLatency;
        private long _cliMaxLatency;
        private double _cliAvgLatency;
        private double _cliEmaLatency;

        // ================= EVENTS =================

        private long _evtPublished;
        private long _evtDelivered;
        private long _evtFailed;

        // ================= ERRORS =================

        private long _errors;

        private const double EmaAlpha = 0.2;

        // Latency stats (last/max/avg/ema + sample count) are derived values that
        // need a read-modify-write; guard each channel so concurrent responses
        // don't tear the doubles or race the sample count (audit M1).
        private readonly object _srvLatencyGate = new object();
        private readonly object _cliLatencyGate = new object();
        private long _srvSamples;
        private long _cliSamples;

        // ============================================================
        // SERVER
        // ============================================================

        /// <inheritdoc />
        public void OnServerClientConnected(string pipeName)
            => Interlocked.Increment(ref _srvClients);

        /// <inheritdoc />
        public void OnServerClientDisconnected(string pipeName)
            => Interlocked.Decrement(ref _srvClients);

        /// <inheritdoc />
        public void OnServerRequestReceived(string pipeName, string name)
            => Interlocked.Increment(ref _srvReq);

        /// <inheritdoc />
        public void OnServerResponseSent(
            string pipeName,
            string name,
            bool ok,
            TimeSpan elapsed)
        {
            if (ok)
                Interlocked.Increment(ref _srvOk);
            else
                Interlocked.Increment(ref _srvFail);

            UpdateLatency(
                _srvLatencyGate,
                elapsed,
                ref _srvLastLatency,
                ref _srvMaxLatency,
                ref _srvAvgLatency,
                ref _srvEmaLatency,
                ref _srvSamples);
        }

        /// <inheritdoc />
        public void OnServerEventPublished(
            string pipeName,
            string eventName,
            int subscribers,
            int success,
            int failed)
        {
            Interlocked.Increment(ref _evtPublished);
            Interlocked.Add(ref _evtDelivered, success);
            Interlocked.Add(ref _evtFailed, failed);
        }

        // ============================================================
        // CLIENT
        // ============================================================

        /// <inheritdoc />
        public void OnClientConnect(
            string pipeName,
            TimeSpan elapsed,
            bool ok,
            string? errorCode)
        {
            Interlocked.Increment(ref _cliConnAttempts);

            if (ok)
                Interlocked.Increment(ref _cliConnOk);
            else
                Interlocked.Increment(ref _cliConnFail);
        }

        /// <inheritdoc />
        public void OnClientRequest(string pipeName, string name)
            => Interlocked.Increment(ref _cliReq);

        /// <inheritdoc />
        public void OnClientResponse(
            string pipeName,
            string name,
            bool ok,
            TimeSpan elapsed,
            string? errorCode)
        {
            if (ok)
                Interlocked.Increment(ref _cliOk);
            else
                Interlocked.Increment(ref _cliFail);

            UpdateLatency(
                _cliLatencyGate,
                elapsed,
                ref _cliLastLatency,
                ref _cliMaxLatency,
                ref _cliAvgLatency,
                ref _cliEmaLatency,
                ref _cliSamples);
        }

        // ============================================================
        // ERRORS
        // ============================================================

        /// <inheritdoc />
        public void OnError(string pipeName, string where, Exception ex)
            => Interlocked.Increment(ref _errors);

        // ============================================================
        // LATENCY HELPER
        // ============================================================

        private static void UpdateLatency(
            object gate,
            TimeSpan elapsed,
            ref long last,
            ref long max,
            ref double avg,
            ref double ema,
            ref long samples)
        {
            var ms = (long)elapsed.TotalMilliseconds;

            lock (gate)
            {
                last = ms;
                if (ms > max)
                    max = ms;

                samples++;
                avg = ((avg * (samples - 1)) + ms) / samples;
                ema = (samples == 1) ? ms : (ema * (1 - EmaAlpha)) + (ms * EmaAlpha);
            }
        }

        // ============================================================
        // SNAPSHOT
        // ============================================================

        /// <summary>
        /// Returns a new snapshot of the cumulative counters. Concurrent updates
        /// may advance individual counters while the snapshot is being assembled,
        /// so the result is point-in-time observational evidence rather than a
        /// transactionally consistent protocol state.
        /// </summary>
        /// <returns>A new caller-owned snapshot; collecting it does not reset counters.</returns>
        public PipeMetricsSnapshot Snapshot()
        {
            long srvLast, srvMax; double srvAvg, srvEma;
            lock (_srvLatencyGate)
            {
                srvLast = _srvLastLatency;
                srvMax = _srvMaxLatency;
                srvAvg = _srvAvgLatency;
                srvEma = _srvEmaLatency;
            }

            long cliLast, cliMax; double cliAvg, cliEma;
            lock (_cliLatencyGate)
            {
                cliLast = _cliLastLatency;
                cliMax = _cliMaxLatency;
                cliAvg = _cliAvgLatency;
                cliEma = _cliEmaLatency;
            }

            return new PipeMetricsSnapshot(
                new PipeMetricsSnapshot.ServerMetrics(
                    Interlocked.Read(ref _srvClients),
                    Interlocked.Read(ref _srvReq),
                    Interlocked.Read(ref _srvOk),
                    Interlocked.Read(ref _srvFail),
                    srvLast,
                    srvMax,
                    Math.Round(srvAvg, 2),
                    Math.Round(srvEma, 2)
                ),
                new PipeMetricsSnapshot.ClientMetrics(
                    Interlocked.Read(ref _cliConnAttempts),
                    Interlocked.Read(ref _cliConnOk),
                    Interlocked.Read(ref _cliConnFail),
                    Interlocked.Read(ref _cliReq),
                    Interlocked.Read(ref _cliOk),
                    Interlocked.Read(ref _cliFail),
                    cliLast,
                    cliMax,
                    Math.Round(cliAvg, 2),
                    Math.Round(cliEma, 2)
                ),
                new PipeMetricsSnapshot.EventMetrics(
                    Interlocked.Read(ref _evtPublished),
                    Interlocked.Read(ref _evtDelivered),
                    Interlocked.Read(ref _evtFailed)
                ),
                new PipeMetricsSnapshot.ErrorMetrics(
                    Interlocked.Read(ref _errors)
                )
            );
        }
    }
}
