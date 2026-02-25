using System;

namespace NekoLib.Pipes
{
    public interface IPipeMetrics
    {
        // =========================
        // Server
        // =========================

        void OnServerClientConnected(string pipeName);
        void OnServerClientDisconnected(string pipeName);

        void OnServerRequestReceived(string pipeName, string name);

        void OnServerResponseSent(
            string pipeName,
            string name,
            bool ok,
            TimeSpan elapsed);

        void OnServerEventPublished(
            string pipeName,
            string eventName,
            int subscribers,
            int success,
            int failed);

        // =========================
        // Client
        // =========================

        void OnClientConnect(
            string pipeName,
            TimeSpan elapsed,
            bool ok,
            string errorCode);

        void OnClientRequest(string pipeName, string name);

        void OnClientResponse(
            string pipeName,
            string name,
            bool ok,
            TimeSpan elapsed,
            string errorCode);

        // =========================
        // Errors
        // =========================

        void OnError(string pipeName, string where, Exception ex);

        // =========================
        // Snapshot
        // =========================

        PipeMetricsSnapshot Snapshot();
    }

    public sealed class NoopPipeMetrics : IPipeMetrics
    {
        public static readonly NoopPipeMetrics Instance = new NoopPipeMetrics();

        public void OnServerClientConnected(string pipeName) { }
        public void OnServerClientDisconnected(string pipeName) { }
        public void OnServerRequestReceived(string pipeName, string name) { }
        public void OnServerResponseSent(string pipeName, string name, bool ok, TimeSpan elapsed) { }
        public void OnServerEventPublished(string pipeName, string eventName, int subscribers, int success, int failed) { }
        public void OnClientConnect(string pipeName, TimeSpan elapsed, bool ok, string errorCode) { }
        public void OnClientRequest(string pipeName, string name) { }
        public void OnClientResponse(string pipeName, string name, bool ok, TimeSpan elapsed, string errorCode) { }
        public void OnError(string pipeName, string where, Exception ex) { }

       

        PipeMetricsSnapshot IPipeMetrics.Snapshot() => null;
    }
}

