using System;
using System.Threading;

namespace NekoLib.Pipes
{
    public class SimplePipeMetrics : IPipeMetrics
    {
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

        // ============================================================
        // SERVER
        // ============================================================

        public void OnServerClientConnected(string pipeName)
            => Interlocked.Increment(ref _srvClients);

        public void OnServerClientDisconnected(string pipeName)
            => Interlocked.Decrement(ref _srvClients);

        public void OnServerRequestReceived(string pipeName, string name)
            => Interlocked.Increment(ref _srvReq);

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
                elapsed,
                ref _srvLastLatency,
                ref _srvMaxLatency,
                ref _srvAvgLatency,
                ref _srvEmaLatency,
                _srvOk + _srvFail);
        }

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

        public void OnClientConnect(
            string pipeName,
            TimeSpan elapsed,
            bool ok,
            string errorCode)
        {
            Interlocked.Increment(ref _cliConnAttempts);

            if (ok)
                Interlocked.Increment(ref _cliConnOk);
            else
                Interlocked.Increment(ref _cliConnFail);
        }

        public void OnClientRequest(string pipeName, string name)
            => Interlocked.Increment(ref _cliReq);

        public void OnClientResponse(
            string pipeName,
            string name,
            bool ok,
            TimeSpan elapsed,
            string errorCode)
        {
            if (ok)
                Interlocked.Increment(ref _cliOk);
            else
                Interlocked.Increment(ref _cliFail);

            UpdateLatency(
                elapsed,
                ref _cliLastLatency,
                ref _cliMaxLatency,
                ref _cliAvgLatency,
                ref _cliEmaLatency,
                _cliOk + _cliFail);
        }

        // ============================================================
        // ERRORS
        // ============================================================

        public void OnError(string pipeName, string where, Exception ex)
            => Interlocked.Increment(ref _errors);

        // ============================================================
        // LATENCY HELPER
        // ============================================================

        private void UpdateLatency(
            TimeSpan elapsed,
            ref long last,
            ref long max,
            ref double avg,
            ref double ema,
            long count)
        {
            var ms = (long)elapsed.TotalMilliseconds;

            Interlocked.Exchange(ref last, ms);

            long currentMax;
            do
            {
                currentMax = max;
                if (ms <= currentMax)
                    break;
            }
            while (Interlocked.CompareExchange(ref max, ms, currentMax) != currentMax);

            if (count > 0)
                avg = ((avg * (count - 1)) + ms) / count;

            if (ema == 0)
                ema = ms;
            else
                ema = (ema * (1 - EmaAlpha)) + (ms * EmaAlpha);
        }

        // ============================================================
        // SNAPSHOT
        // ============================================================

        public PipeMetricsSnapshot Snapshot()
        {
            return new PipeMetricsSnapshot(
                new PipeMetricsSnapshot.ServerMetrics(
                    Interlocked.Read(ref _srvClients),
                    Interlocked.Read(ref _srvReq),
                    Interlocked.Read(ref _srvOk),
                    Interlocked.Read(ref _srvFail),
                    Interlocked.Read(ref _srvLastLatency),
                    Interlocked.Read(ref _srvMaxLatency),
                    Math.Round(_srvAvgLatency, 2),
                    Math.Round(_srvEmaLatency, 2)
                ),
                new PipeMetricsSnapshot.ClientMetrics(
                    Interlocked.Read(ref _cliConnAttempts),
                    Interlocked.Read(ref _cliConnOk),
                    Interlocked.Read(ref _cliConnFail),
                    Interlocked.Read(ref _cliReq),
                    Interlocked.Read(ref _cliOk),
                    Interlocked.Read(ref _cliFail),
                    Interlocked.Read(ref _cliLastLatency),
                    Interlocked.Read(ref _cliMaxLatency),
                    Math.Round(_cliAvgLatency, 2),
                    Math.Round(_cliEmaLatency, 2)
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
