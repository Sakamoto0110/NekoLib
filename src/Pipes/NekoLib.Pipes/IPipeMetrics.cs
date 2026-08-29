using System;

namespace NekoLib.Pipes
{
    /// <summary>
    /// Receives synchronous, observational pipe metrics. Transport-owned callback
    /// failures are isolated and never change protocol or delivery outcomes.
    /// Implementations must be safe for concurrent calls.
    /// </summary>
    public interface IPipeMetrics
    {
        // =========================
        // Server
        // =========================

        /// <summary>
        /// Records that a peer connected to a server pipe. An event hub raises this
        /// for each admitted subscriber, so a sink shared by a server and its hub
        /// observes both RPC clients and event subscribers.
        /// </summary>
        /// <param name="pipeName">
        /// Captured endpoint name: the RPC pipe name, or the event pipe name
        /// including its <c>.events</c> suffix.
        /// </param>
        void OnServerClientConnected(string pipeName);

        /// <summary>
        /// Records that a peer disconnected from a server pipe. An event hub raises
        /// this for each removed subscriber.
        /// </summary>
        /// <param name="pipeName">
        /// Captured endpoint name: the RPC pipe name, or the event pipe name
        /// including its <c>.events</c> suffix.
        /// </param>
        void OnServerClientDisconnected(string pipeName);

        /// <summary>Records receipt of an RPC request.</summary>
        /// <param name="pipeName">Captured RPC pipe name.</param>
        /// <param name="name">Operation name received from the request envelope.</param>
        void OnServerRequestReceived(string pipeName, string name);

        /// <summary>
        /// Records a server response attempt and elapsed time from handler
        /// dispatch through the response write attempt.
        /// </summary>
        /// <param name="pipeName">Captured RPC pipe name.</param>
        /// <param name="name">Operation name copied to the response.</param>
        /// <param name="ok">Whether the attempted response envelope represents success.</param>
        /// <param name="elapsed">Elapsed handler and response-write time.</param>
        void OnServerResponseSent(
            string pipeName,
            string name,
            bool ok,
            TimeSpan elapsed);

        /// <summary>
        /// Records terminal delivery totals for one event publication after all
        /// subscribers observed when publishing have succeeded or failed.
        /// </summary>
        /// <param name="pipeName">Captured event pipe name, including the <c>.events</c> suffix.</param>
        /// <param name="eventName">Event name from the envelope.</param>
        /// <param name="subscribers">Subscriber count captured for this publication.</param>
        /// <param name="success">Number of successful subscriber writes.</param>
        /// <param name="failed">Number of dropped, cancelled, disconnected, or failed deliveries.</param>
        void OnServerEventPublished(
            string pipeName,
            string eventName,
            int subscribers,
            int success,
            int failed);

        // =========================
        // Client
        // =========================

        /// <summary>Records one terminal client connection attempt.</summary>
        /// <param name="pipeName">Captured RPC pipe name.</param>
        /// <param name="elapsed">Connection-attempt duration.</param>
        /// <param name="ok">Whether the connection was established.</param>
        /// <param name="errorCode">Implementation diagnostic code, or null for success.</param>
        void OnClientConnect(
            string pipeName,
            TimeSpan elapsed,
            bool ok,
            string? errorCode);

        /// <summary>Records one client request after connection and before frame writing.</summary>
        /// <param name="pipeName">Captured RPC pipe name.</param>
        /// <param name="name">Requested operation name.</param>
        void OnClientRequest(string pipeName, string name);

        /// <summary>
        /// Records one structured client response outcome. Transport and parsing
        /// exceptions are reported through <see cref="OnError"/> instead.
        /// </summary>
        /// <param name="pipeName">Captured RPC pipe name.</param>
        /// <param name="name">Requested operation name.</param>
        /// <param name="ok">Whether the correlated response represents success.</param>
        /// <param name="elapsed">Total elapsed connection, request-write, and response-read time.</param>
        /// <param name="errorCode">Response error code, or null for success.</param>
        void OnClientResponse(
            string pipeName,
            string name,
            bool ok,
            TimeSpan elapsed,
            string? errorCode);

        // =========================
        // Errors
        // =========================

        /// <summary>Records a locally observed transport, framing, or handler error.</summary>
        /// <param name="pipeName">Captured RPC or event pipe name.</param>
        /// <param name="where">Diagnostic location label; it is not a stable wire-code vocabulary.</param>
        /// <param name="ex">Original local exception. It is not serialized to the peer by this callback.</param>
        void OnError(string pipeName, string where, Exception ex);

        // =========================
        // Snapshot
        // =========================

        /// <summary>
        /// Returns a caller-owned metrics snapshot, or null when the implementation
        /// intentionally exposes no snapshot. This call is not failure-isolated by
        /// the transport.
        /// </summary>
        /// <returns>A caller-owned snapshot, or null when snapshots are unsupported.</returns>
        PipeMetricsSnapshot? Snapshot();
    }

    /// <summary>A shared metrics implementation that records no observations.</summary>
    public sealed class NoopPipeMetrics : IPipeMetrics
    {
        /// <summary>Gets the shared no-op instance.</summary>
        public static readonly NoopPipeMetrics Instance = new NoopPipeMetrics();

        /// <summary>Initializes a no-op metrics sink.</summary>
        public NoopPipeMetrics()
        {
        }

        /// <inheritdoc />
        public void OnServerClientConnected(string pipeName) { }
        /// <inheritdoc />
        public void OnServerClientDisconnected(string pipeName) { }
        /// <inheritdoc />
        public void OnServerRequestReceived(string pipeName, string name) { }
        /// <inheritdoc />
        public void OnServerResponseSent(string pipeName, string name, bool ok, TimeSpan elapsed) { }
        /// <inheritdoc />
        public void OnServerEventPublished(string pipeName, string eventName, int subscribers, int success, int failed) { }
        /// <inheritdoc />
        public void OnClientConnect(string pipeName, TimeSpan elapsed, bool ok, string? errorCode) { }
        /// <inheritdoc />
        public void OnClientRequest(string pipeName, string name) { }
        /// <inheritdoc />
        public void OnClientResponse(string pipeName, string name, bool ok, TimeSpan elapsed, string? errorCode) { }
        /// <inheritdoc />
        public void OnError(string pipeName, string where, Exception ex) { }

        /// <inheritdoc />
        PipeMetricsSnapshot? IPipeMetrics.Snapshot() => null;
    }
}

