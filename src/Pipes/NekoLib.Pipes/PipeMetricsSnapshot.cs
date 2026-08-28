using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NekoLib.Pipes
{
    /// <summary>
    /// Groups caller-owned server, client, event, and error observations returned
    /// by a pipe metrics implementation.
    /// </summary>
    public sealed class PipeMetricsSnapshot
    {
        /// <summary>Gets the supplied server metrics snapshot.</summary>
        public ServerMetrics Server { get; private set; }

        /// <summary>Gets the supplied client metrics snapshot.</summary>
        public ClientMetrics Client { get; private set; }

        /// <summary>Gets the supplied event-delivery metrics snapshot.</summary>
        public EventMetrics Events { get; private set; }

        /// <summary>Gets the supplied locally observed error metrics snapshot.</summary>
        public ErrorMetrics Errors { get; private set; }

        /// <summary>
        /// Initializes an aggregate snapshot from the supplied component snapshots.
        /// Arguments are stored as supplied and are not copied or validated.
        /// </summary>
        /// <param name="server">Server metrics component.</param>
        /// <param name="client">Client metrics component.</param>
        /// <param name="events">Event-delivery metrics component.</param>
        /// <param name="errors">Locally observed error metrics component.</param>
        public PipeMetricsSnapshot(
            ServerMetrics server,
            ClientMetrics client,
            EventMetrics events,
            ErrorMetrics errors)
        {
            Server = server;
            Client = client;
            Events = events;
            Errors = errors;
        }

        // ============================================================
        // Server Metrics
        // ============================================================

        /// <summary>Represents cumulative RPC-server observations.</summary>
        public sealed class ServerMetrics
        {
            /// <summary>Gets the live connected-client count at capture time.</summary>
            public long ConnectedClients { get; private set; }

            /// <summary>Gets the cumulative number of received request envelopes.</summary>
            public long Requests { get; private set; }

            /// <summary>Gets the cumulative number of successful response attempts.</summary>
            public long Success { get; private set; }

            /// <summary>Gets the cumulative number of unsuccessful response attempts.</summary>
            public long Failures { get; private set; }

            /// <summary>Gets the most recently recorded response-attempt latency in whole milliseconds.</summary>
            public long LastLatencyMs { get; private set; }

            /// <summary>Gets the maximum recorded response-attempt latency in whole milliseconds.</summary>
            public long MaxLatencyMs { get; private set; }

            /// <summary>Gets the arithmetic mean response-attempt latency in milliseconds.</summary>
            public double AverageLatencyMs { get; private set; }

            /// <summary>Gets the exponential moving average response-attempt latency in milliseconds.</summary>
            public double EmaLatencyMs { get; private set; }

            /// <summary>Initializes a server metrics snapshot from supplied cumulative values.</summary>
            /// <param name="connectedClients">Live connected-client count.</param>
            /// <param name="requests">Cumulative received-request count.</param>
            /// <param name="success">Cumulative successful response-attempt count.</param>
            /// <param name="failures">Cumulative unsuccessful response-attempt count.</param>
            /// <param name="lastLatencyMs">Most recent response-attempt latency in whole milliseconds.</param>
            /// <param name="maxLatencyMs">Maximum response-attempt latency in whole milliseconds.</param>
            /// <param name="averageLatencyMs">Arithmetic mean response-attempt latency in milliseconds.</param>
            /// <param name="emaLatencyMs">Exponential moving average response-attempt latency in milliseconds.</param>
            public ServerMetrics(
                long connectedClients,
                long requests,
                long success,
                long failures,
                long lastLatencyMs,
                long maxLatencyMs,
                double averageLatencyMs,
                double emaLatencyMs)
            {
                ConnectedClients = connectedClients;
                Requests = requests;
                Success = success;
                Failures = failures;
                LastLatencyMs = lastLatencyMs;
                MaxLatencyMs = maxLatencyMs;
                AverageLatencyMs = averageLatencyMs;
                EmaLatencyMs = emaLatencyMs;
            }
        }

        // ============================================================
        // Client Metrics
        // ============================================================

        /// <summary>Represents cumulative RPC-client observations.</summary>
        public sealed class ClientMetrics
        {
            /// <summary>Gets the cumulative number of connection attempts.</summary>
            public long ConnectAttempts { get; private set; }

            /// <summary>Gets the cumulative number of successful connection attempts.</summary>
            public long ConnectSuccess { get; private set; }

            /// <summary>Gets the cumulative number of failed connection attempts.</summary>
            public long ConnectFailures { get; private set; }

            /// <summary>Gets the cumulative number of requests dispatched after connection.</summary>
            public long Requests { get; private set; }

            /// <summary>Gets the cumulative number of successful correlated responses.</summary>
            public long Success { get; private set; }

            /// <summary>Gets the cumulative number of unsuccessful correlated responses.</summary>
            public long Failures { get; private set; }

            /// <summary>Gets the most recently recorded total request latency in whole milliseconds.</summary>
            public long LastLatencyMs { get; private set; }

            /// <summary>Gets the maximum recorded total request latency in whole milliseconds.</summary>
            public long MaxLatencyMs { get; private set; }

            /// <summary>Gets the arithmetic mean total request latency in milliseconds.</summary>
            public double AverageLatencyMs { get; private set; }

            /// <summary>Gets the exponential moving average total request latency in milliseconds.</summary>
            public double EmaLatencyMs { get; private set; }

            /// <summary>Initializes a client metrics snapshot from supplied cumulative values.</summary>
            /// <param name="connectAttempts">Cumulative connection-attempt count.</param>
            /// <param name="connectSuccess">Cumulative successful connection-attempt count.</param>
            /// <param name="connectFailures">Cumulative failed connection-attempt count.</param>
            /// <param name="requests">Cumulative dispatched-request count.</param>
            /// <param name="success">Cumulative successful correlated-response count.</param>
            /// <param name="failures">Cumulative unsuccessful correlated-response count.</param>
            /// <param name="lastLatencyMs">Most recent total request latency in whole milliseconds.</param>
            /// <param name="maxLatencyMs">Maximum total request latency in whole milliseconds.</param>
            /// <param name="averageLatencyMs">Arithmetic mean total request latency in milliseconds.</param>
            /// <param name="emaLatencyMs">Exponential moving average total request latency in milliseconds.</param>
            public ClientMetrics(
                long connectAttempts,
                long connectSuccess,
                long connectFailures,
                long requests,
                long success,
                long failures,
                long lastLatencyMs,
                long maxLatencyMs,
                double averageLatencyMs,
                double emaLatencyMs)
            {
                ConnectAttempts = connectAttempts;
                ConnectSuccess = connectSuccess;
                ConnectFailures = connectFailures;
                Requests = requests;
                Success = success;
                Failures = failures;
                LastLatencyMs = lastLatencyMs;
                MaxLatencyMs = maxLatencyMs;
                AverageLatencyMs = averageLatencyMs;
                EmaLatencyMs = emaLatencyMs;
            }
        }

        // ============================================================
        // Event Metrics
        // ============================================================

        /// <summary>Represents cumulative event-publication and subscriber-delivery observations.</summary>
        public sealed class EventMetrics
        {
            /// <summary>Gets the number of publications whose subscriber deliveries reached terminal outcomes.</summary>
            public long Published { get; private set; }

            /// <summary>Gets the cumulative number of successful subscriber deliveries.</summary>
            public long Delivered { get; private set; }

            /// <summary>Gets the cumulative number of failed, dropped, or cancelled subscriber deliveries.</summary>
            public long Failed { get; private set; }

            /// <summary>Initializes an event metrics snapshot from supplied cumulative values.</summary>
            /// <param name="published">Terminal publication count.</param>
            /// <param name="delivered">Successful subscriber-delivery count.</param>
            /// <param name="failed">Failed, dropped, or cancelled subscriber-delivery count.</param>
            public EventMetrics(
                long published,
                long delivered,
                long failed)
            {
                Published = published;
                Delivered = delivered;
                Failed = failed;
            }
        }

        // ============================================================
        // Error Metrics
        // ============================================================

        /// <summary>Represents cumulative locally observed error callbacks.</summary>
        public sealed class ErrorMetrics
        {
            /// <summary>Gets the cumulative error callback count.</summary>
            public long Total { get; private set; }

            /// <summary>Initializes an error metrics snapshot.</summary>
            /// <param name="total">Cumulative error callback count.</param>
            public ErrorMetrics(long total)
            {
                Total = total;
            }
        }
    }
}

