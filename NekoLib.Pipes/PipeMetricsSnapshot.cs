using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NekoLib.Pipes
{
    public sealed class PipeMetricsSnapshot
    {
        public ServerMetrics Server { get; private set; }
        public ClientMetrics Client { get; private set; }
        public EventMetrics Events { get; private set; }
        public ErrorMetrics Errors { get; private set; }

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

        public sealed class ServerMetrics
        {
            public long ConnectedClients { get; private set; }

            public long Requests { get; private set; }
            public long Success { get; private set; }
            public long Failures { get; private set; }

            public long LastLatencyMs { get; private set; }
            public long MaxLatencyMs { get; private set; }
            public double AverageLatencyMs { get; private set; }
            public double EmaLatencyMs { get; private set; }

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

        public sealed class ClientMetrics
        {
            public long ConnectAttempts { get; private set; }
            public long ConnectSuccess { get; private set; }
            public long ConnectFailures { get; private set; }

            public long Requests { get; private set; }
            public long Success { get; private set; }
            public long Failures { get; private set; }

            public long LastLatencyMs { get; private set; }
            public long MaxLatencyMs { get; private set; }
            public double AverageLatencyMs { get; private set; }
            public double EmaLatencyMs { get; private set; }

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

        public sealed class EventMetrics
        {
            public long Published { get; private set; }
            public long Delivered { get; private set; }
            public long Failed { get; private set; }

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

        public sealed class ErrorMetrics
        {
            public long Total { get; private set; }

            public ErrorMetrics(long total)
            {
                Total = total;
            }
        }
    }
}

