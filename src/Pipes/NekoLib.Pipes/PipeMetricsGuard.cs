using System;

namespace NekoLib.Pipes
{
    internal static class PipeMetricsGuard
    {
        public static IPipeMetrics Protect(IPipeMetrics metrics)
        {
            if (metrics == null)
                throw new ArgumentNullException(nameof(metrics));

            return metrics is GuardedPipeMetrics ? metrics : new GuardedPipeMetrics(metrics);
        }

        private sealed class GuardedPipeMetrics : IPipeMetrics
        {
            private readonly IPipeMetrics _inner;

            public GuardedPipeMetrics(IPipeMetrics inner)
            {
                _inner = inner;
            }

            public void OnServerClientConnected(string pipeName)
                => Invoke(() => _inner.OnServerClientConnected(pipeName));

            public void OnServerClientDisconnected(string pipeName)
                => Invoke(() => _inner.OnServerClientDisconnected(pipeName));

            public void OnServerRequestReceived(string pipeName, string name)
                => Invoke(() => _inner.OnServerRequestReceived(pipeName, name));

            public void OnServerResponseSent(
                string pipeName,
                string name,
                bool ok,
                TimeSpan elapsed)
                => Invoke(() => _inner.OnServerResponseSent(pipeName, name, ok, elapsed));

            public void OnServerEventPublished(
                string pipeName,
                string eventName,
                int subscribers,
                int success,
                int failed)
                => Invoke(() => _inner.OnServerEventPublished(
                    pipeName,
                    eventName,
                    subscribers,
                    success,
                    failed));

            public void OnClientConnect(
                string pipeName,
                TimeSpan elapsed,
                bool ok,
                string? errorCode)
                => Invoke(() => _inner.OnClientConnect(pipeName, elapsed, ok, errorCode));

            public void OnClientRequest(string pipeName, string name)
                => Invoke(() => _inner.OnClientRequest(pipeName, name));

            public void OnClientResponse(
                string pipeName,
                string name,
                bool ok,
                TimeSpan elapsed,
                string? errorCode)
                => Invoke(() => _inner.OnClientResponse(pipeName, name, ok, elapsed, errorCode));

            public void OnError(string pipeName, string where, Exception ex)
                => Invoke(() => _inner.OnError(pipeName, where, ex));

            public PipeMetricsSnapshot? Snapshot()
                => _inner.Snapshot();

            private static void Invoke(Action callback)
            {
                try
                {
                    callback();
                }
                catch
                {
                    // Metrics are observational. A sink failure must never change
                    // transport, protocol, lifecycle, or delivery outcomes.
                }
            }
        }
    }
}
