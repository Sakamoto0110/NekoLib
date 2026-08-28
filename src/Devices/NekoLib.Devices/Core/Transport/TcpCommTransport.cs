using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NekoLib.Devices.Core.Transport
{
    /// <summary>
    /// Byte-stream transport backed by a TCP client.
    /// Endpoints use <c>tcp://host:port</c>; <c>host:port</c> is also accepted.
    /// </summary>
    public sealed class TcpCommTransport : StreamCommTransport
    {
        /// <summary>
        /// Maximum time allowed for establishing a TCP connection.
        /// The default is 5000 milliseconds. Zero disables the client-side
        /// connect timeout and negative values are rejected when opening.
        /// </summary>
        public int ConnectTimeout { get; set; } = 5000;

        /// <summary>Creates a TCP transport without a configured endpoint.</summary>
        public TcpCommTransport()
        {
        }

        /// <summary>Creates a TCP transport for an endpoint.</summary>
        /// <param name="endpoint"><c>tcp://host:port</c> or <c>host:port</c>.</param>
        public TcpCommTransport(string endpoint)
            : base(endpoint)
        {
        }

        /// <summary>Creates a TCP transport for a host and port.</summary>
        /// <param name="host">Non-blank host name or address.</param>
        /// <param name="port">Port from 1 through 65535.</param>
        public TcpCommTransport(string host, int port)
            : base(BuildEndpoint(host, port))
        {
        }

        /// <inheritdoc/>
        protected override string TransportName => "TCP";

        /// <inheritdoc/>
        protected override string NormalizeEndpoint(string endpoint)
        {
            return ParseEndpoint(endpoint).Canonical;
        }

        /// <inheritdoc/>
        protected override async Task<Stream> ConnectStream(
            string normalizedEndpoint,
            CancellationToken ct)
        {
            if(ConnectTimeout < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(ConnectTimeout),
                    "ConnectTimeout cannot be negative.");

            var parsed = ParseEndpoint(normalizedEndpoint);
            var client = new TcpClient();
            var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            if(ConnectTimeout > 0)
                timeoutCts.CancelAfter(ConnectTimeout);

            using(var registration = timeoutCts.Token.Register(client.Close))
            {
                try
                {
                    await client.ConnectAsync(parsed.Host, parsed.Port).ConfigureAwait(false);
                    ct.ThrowIfCancellationRequested();

                    if(timeoutCts.IsCancellationRequested)
                        throw new TimeoutException(
                            $"TCP connection to '{parsed.Canonical}' timed out after {ConnectTimeout}ms.");

                    client.NoDelay = true;
                    return client.GetStream();
                }
                catch(Exception) when(ct.IsCancellationRequested)
                {
                    client.Dispose();
                    throw new OperationCanceledException(ct);
                }
                catch(Exception ex) when(
                    timeoutCts.IsCancellationRequested &&
                    !ct.IsCancellationRequested &&
                    !(ex is TimeoutException))
                {
                    client.Dispose();
                    throw new TimeoutException(
                        $"TCP connection to '{parsed.Canonical}' timed out after {ConnectTimeout}ms.",
                        ex);
                }
                catch
                {
                    client.Dispose();
                    throw;
                }
                finally
                {
                    timeoutCts.Dispose();
                }
            }
        }

        private static string BuildEndpoint(string host, int port)
        {
            if(string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("Host is required.", nameof(host));

            if(port < 1 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");

            return $"tcp://{host}:{port}";
        }

        private static TcpEndpoint ParseEndpoint(string endpoint)
        {
            if(string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("TCP endpoint is required.", nameof(endpoint));

            string candidate = endpoint.Trim();
            if(!candidate.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase))
                candidate = "tcp://" + candidate;

            if(!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
               !string.Equals(uri.Scheme, "tcp", StringComparison.OrdinalIgnoreCase) ||
               string.IsNullOrWhiteSpace(uri.Host) ||
               uri.Port < 1 ||
               uri.Port > 65535 ||
               (!string.IsNullOrEmpty(uri.AbsolutePath) && uri.AbsolutePath != "/"))
            {
                throw new ArgumentException(
                    "TCP endpoint must use the form 'tcp://host:port' or 'host:port'.",
                    nameof(endpoint));
            }

            return new TcpEndpoint(uri.Host, uri.Port);
        }

        private sealed class TcpEndpoint
        {
            public TcpEndpoint(string host, int port)
            {
                Host = host;
                Port = port;
                Canonical = $"tcp://{host}:{port}";
            }

            public string Host { get; }

            public int Port { get; }

            public string Canonical { get; }
        }
    }
}
