using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
namespace NekoLib.Pipes
{
    

    public sealed class PipeClient : IDisposable
#if NET9
    , IAsyncDisposable
#endif
    {
        private readonly PipeClientOptions _o;
        private readonly IPipeMetrics _metrics;

        public PipeClient(PipeClientOptions options)
        {
            _o = options ?? throw new ArgumentNullException(nameof(options));
            _metrics = options.Metrics ?? NoopPipeMetrics.Instance;
        }

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
                    _o.PipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);

                await ConnectAsync(pipe, cancellationToken).ConfigureAwait(false);

                _metrics.OnClientRequest(_o.PipeName, name);

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(_o.RequestTimeout);

#if NET9
            await PipeFraming.WriteAsync(pipe, request, timeoutCts.Token).ConfigureAwait(false);
            var response = await PipeFraming.ReadAsync(pipe, timeoutCts.Token).ConfigureAwait(false);
#else
                await PipeFraming.WriteAsync(pipe, request).ConfigureAwait(false);
                var response = await PipeFraming.ReadAsync(pipe).ConfigureAwait(false);
#endif

                ValidateCorrelation(request, response);

                swTotal.Stop();
                _metrics.OnClientResponse(_o.PipeName, name, response.Ok, swTotal.Elapsed,
                    response.Ok ? null : response.Error?.Code);

                return response;
            }
            catch (Exception ex)
            {
                swTotal.Stop();
                _metrics.OnError(_o.PipeName, "client_send", ex);
                throw;
            }
            finally
            {
                try { pipe?.Dispose(); } catch { }
            }
        }

        private async Task ConnectAsync(NamedPipeClientStream pipe, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();

            try
            {
#if NET9
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectCts.CancelAfter(_o.ConnectTimeout);
            await pipe.ConnectAsync(connectCts.Token).ConfigureAwait(false);
#else
                await Task.Run(() =>
                {
                    pipe.Connect((int)_o.ConnectTimeout.TotalMilliseconds);
                }).ConfigureAwait(false);
#endif

                sw.Stop();
                _metrics.OnClientConnect(_o.PipeName, sw.Elapsed, true, null);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _metrics.OnClientConnect(_o.PipeName, sw.Elapsed, false, "connect_failed");
                _metrics.OnError(_o.PipeName, "client_connect", ex);
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

        public void Dispose()
        {
            // currently stateless (per-call connection)
            // reserved for future persistent connection model
        }

#if NET9
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
#endif
    }


}


