using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace NekoLib.Devices.Core.Transport
{
    /// <summary>
    /// Byte-stream transport backed by a named-pipe client.
    /// Accepts a bare pipe name, <c>\\.\pipe\name</c>, or the emulator's
    /// <c>pipe://\\.\pipe\name</c> endpoint form.
    /// </summary>
    public sealed class NamedPipeCommTransport : StreamCommTransport
    {
        /// <summary>
        /// Maximum time allowed for establishing a pipe connection.
        /// Zero disables the client-side connect timeout.
        /// </summary>
        public int ConnectTimeout { get; set; } = 5000;

        /// <summary>Creates a named-pipe transport without a configured endpoint.</summary>
        public NamedPipeCommTransport()
        {
        }

        /// <summary>Creates a named-pipe transport for a pipe name or endpoint.</summary>
        public NamedPipeCommTransport(string pipeNameOrEndpoint)
            : base(pipeNameOrEndpoint)
        {
        }

        /// <summary>Creates a named-pipe transport for a server and pipe name.</summary>
        public NamedPipeCommTransport(string serverName, string pipeName)
            : base(BuildEndpoint(serverName, pipeName))
        {
        }

        /// <inheritdoc/>
        protected override string TransportName => "NamedPipe";

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
            var pipe = new NamedPipeClientStream(
                parsed.ServerName,
                parsed.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough);
            int effectiveTimeout = ConnectTimeout == 0
                ? Timeout.Infinite
                : ConnectTimeout;

            using(var registration = ct.Register(pipe.Dispose))
            {
                try
                {
                    await Task.Run(
                        () => pipe.Connect(effectiveTimeout),
                        CancellationToken.None).ConfigureAwait(false);
                    ct.ThrowIfCancellationRequested();
                    return pipe;
                }
                catch(Exception) when(ct.IsCancellationRequested)
                {
                    pipe.Dispose();
                    throw new OperationCanceledException(ct);
                }
                catch
                {
                    pipe.Dispose();
                    throw;
                }
            }
        }

        private static string BuildEndpoint(string serverName, string pipeName)
        {
            if(string.IsNullOrWhiteSpace(serverName))
                throw new ArgumentException("Server name is required.", nameof(serverName));

            if(string.IsNullOrWhiteSpace(pipeName))
                throw new ArgumentException("Pipe name is required.", nameof(pipeName));

            return $@"\\{serverName}\pipe\{pipeName}";
        }

        private static PipeEndpoint ParseEndpoint(string endpoint)
        {
            if(string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("Named-pipe endpoint is required.", nameof(endpoint));

            string candidate = endpoint.Trim();
            const string scheme = "pipe://";
            if(candidate.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
                candidate = candidate.Substring(scheme.Length);

            if(!candidate.StartsWith(@"\\", StringComparison.Ordinal))
                return new PipeEndpoint(".", ValidatePipeName(candidate, nameof(endpoint)));

            string withoutPrefix = candidate.Substring(2);
            int serverSeparator = withoutPrefix.IndexOf('\\');
            if(serverSeparator <= 0)
                throw InvalidEndpoint(nameof(endpoint));

            string server = withoutPrefix.Substring(0, serverSeparator);
            string remainder = withoutPrefix.Substring(serverSeparator + 1);
            const string pipePrefix = @"pipe\";
            if(!remainder.StartsWith(pipePrefix, StringComparison.OrdinalIgnoreCase))
                throw InvalidEndpoint(nameof(endpoint));

            string pipeName = remainder.Substring(pipePrefix.Length);
            return new PipeEndpoint(server, ValidatePipeName(pipeName, nameof(endpoint)));
        }

        private static string ValidatePipeName(string pipeName, string paramName)
        {
            if(string.IsNullOrWhiteSpace(pipeName))
                throw InvalidEndpoint(paramName);

            return pipeName;
        }

        private static ArgumentException InvalidEndpoint(string paramName)
        {
            return new ArgumentException(
                @"Named-pipe endpoint must be a pipe name or use the form '\\server\pipe\name'.",
                paramName);
        }

        private sealed class PipeEndpoint
        {
            public PipeEndpoint(string serverName, string pipeName)
            {
                ServerName = serverName;
                PipeName = pipeName;
                Canonical = $@"\\{serverName}\pipe\{pipeName}";
            }

            public string ServerName { get; }

            public string PipeName { get; }

            public string Canonical { get; }
        }
    }
}
