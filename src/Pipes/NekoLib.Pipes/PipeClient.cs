using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace NekoLib.Pipes
{
    /// <summary>
    /// Sends independent request/response calls. Each call owns its pipe stream;
    /// the client itself owns no persistent transport and requires no disposal.
    /// </summary>
    public sealed class PipeClient
    {
        private readonly string _pipeName;
        private readonly TimeSpan _connectTimeout;
        private readonly TimeSpan _requestTimeout;
        private readonly int _maxMessageBytes;
        private readonly IPipeMetrics _metrics;

        /// <summary>
        /// Initializes a stateless RPC client and captures a validated snapshot of
        /// the supplied options. The options and metrics sink remain caller-owned.
        /// </summary>
        /// <param name="options">Client configuration to validate and capture.</param>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException">The configured pipe name is blank.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// A timeout or maximum message size is outside its supported positive bound.
        /// </exception>
        public PipeClient(PipeClientOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            _pipeName = PipeConfiguration.RequirePipeName(
                options.PipeName,
                nameof(PipeClientOptions.PipeName));
            _connectTimeout = PipeConfiguration.RequirePositiveTimeout(
                options.ConnectTimeout,
                nameof(PipeClientOptions.ConnectTimeout));
            _requestTimeout = PipeConfiguration.RequirePositiveTimeout(
                options.RequestTimeout,
                nameof(PipeClientOptions.RequestTimeout));
            _maxMessageBytes = PipeConfiguration.RequirePositive(
                options.MaxMessageBytes,
                nameof(PipeClientOptions.MaxMessageBytes));
            _metrics = PipeMetricsGuard.Protect(options.Metrics ?? NoopPipeMetrics.Instance);
        }

        /// <summary>
        /// Sends one request over a new owned pipe connection, validates the
        /// correlated response, and closes the connection before completion.
        /// Concurrent calls are independent.
        /// </summary>
        /// <param name="name">Nonblank mapped operation name.</param>
        /// <param name="payload">
        /// Optional caller payload serialized immediately into the target-specific
        /// JSON DOM. Credentials or secrets in this object are sent to the peer.
        /// </param>
        /// <param name="cancellationToken">Token governing connection, request writing, and response reading.</param>
        /// <returns>
        /// The correlated response. A clean close before a response frame is
        /// represented by an unsuccessful <see cref="PipeErrorCodes.ConnectionClosed"/> response.
        /// </returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
        /// <exception cref="OperationCanceledException">
        /// The caller token, connection timeout, or request timeout ends the operation.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The request exceeds the configured frame limit, or the response has a
        /// mismatched ID or a type other than <c>res</c>.
        /// </exception>
        /// <remarks>
        /// Serialization, connection, write, framing, parsing, and truncated-frame
        /// failures propagate. The request timeout starts only after connection.
        /// </remarks>
        public async Task<PipeMessage> SendAsync(
            string name,
            object? payload = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Command name required.", nameof(name));

            var request = CreateRequest(name, payload);
            var swTotal = Stopwatch.StartNew();
            NamedPipeClientStream? pipe = null;

            try
            {
                pipe = new NamedPipeClientStream(
                    ".",
                    _pipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);

                await ConnectAsync(pipe, cancellationToken).ConfigureAwait(false);
                _metrics.OnClientRequest(_pipeName, name);

                using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken))
                {
                    timeoutCts.CancelAfter(_requestTimeout);
                    await PipeFraming.WriteAsync(
                        pipe,
                        request,
                        timeoutCts.Token,
                        _maxMessageBytes).ConfigureAwait(false);
                    var response = await PipeFraming.TryReadAsync(
                        pipe,
                        timeoutCts.Token,
                        _maxMessageBytes).ConfigureAwait(false)
                        ?? ConnectionClosedResponse(request);

                    ValidateCorrelation(request, response);

                    swTotal.Stop();
                    _metrics.OnClientResponse(
                        _pipeName,
                        name,
                        response.Ok,
                        swTotal.Elapsed,
                        response.Ok ? null : response.Error?.Code);

                    return response;
                }
            }
            catch (Exception ex)
            {
                swTotal.Stop();
                _metrics.OnError(_pipeName, "client_send", ex);
                throw;
            }
            finally
            {
                try { pipe?.Dispose(); } catch { }
            }
        }

        private async Task ConnectAsync(NamedPipeClientStream pipe, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken))
                {
                    connectCts.CancelAfter(_connectTimeout);
#if NET9
                    await pipe.ConnectAsync(connectCts.Token).ConfigureAwait(false);
#else
                    var connectTask = Task.Run(
                        () => pipe.Connect(PipeConfiguration.ToTimeoutMilliseconds(_connectTimeout)));
                    await PipeTaskCancellation.WithCancellation(
                        connectTask,
                        connectCts.Token).ConfigureAwait(false);
#endif
                }

                sw.Stop();
                _metrics.OnClientConnect(_pipeName, sw.Elapsed, true, null);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _metrics.OnClientConnect(_pipeName, sw.Elapsed, false, "connect_failed");
                _metrics.OnError(_pipeName, "client_connect", ex);
                throw;
            }
        }

        private static void ValidateCorrelation(PipeMessage request, PipeMessage response)
        {
            if (response.Id != request.Id)
                throw new InvalidOperationException("Pipe correlation mismatch.");

            if (response.Type != "res")
                throw new InvalidOperationException("Invalid pipe response type.");
        }

        private static PipeMessage ConnectionClosedResponse(PipeMessage request)
        {
            return new PipeMessage
            {
                Id = request.Id,
                Type = "res",
                Name = request.Name,
                Ok = false,
                Error = new PipeError
                {
                    Code = PipeErrorCodes.ConnectionClosed,
                    Message = "The pipe closed before a response frame was received."
                }
            };
        }

        private static PipeMessage CreateRequest(string name, object? payload)
        {
#if NET9
            return new PipeMessage
            {
                Id = Guid.NewGuid(),
                Type = "req",
                Name = name,
                Ok = true,
                Data = payload == null
                    ? null
                    : System.Text.Json.JsonSerializer.SerializeToElement(payload)
            };
#else
            return new PipeMessage
            {
                Id = Guid.NewGuid(),
                Type = "req",
                Name = name,
                Ok = true,
                Data = payload == null
                    ? null
                    : Newtonsoft.Json.Linq.JToken.FromObject(payload)
            };
#endif
        }
    }
}
