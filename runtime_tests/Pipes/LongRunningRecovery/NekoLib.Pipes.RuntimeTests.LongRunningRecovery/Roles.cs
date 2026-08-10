#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Pipes;
using NekoLib.RuntimeTests.Harness;

namespace NekoLib.Pipes.RuntimeTests.LongRunningRecovery
{
    /// <summary>
    /// The server child: a real process hosting a real <see cref="PipeServer"/>.
    /// <para/>
    /// It maps the operations the controller's checks need and nothing else. In
    /// particular it exposes no way to make the server misbehave on demand: the
    /// suite forbids a privileged control plane, and the faults this scenario
    /// injects are things done *to* this process from outside - killing it,
    /// closing a peer mid-frame - or slow handlers it was configured with at
    /// startup.
    /// </summary>
    internal static class ServerRole
    {
        public static int Run(ScenarioOptions options, CancellationToken ct)
        {
            if (options.PipeName.Length == 0)
            {
                Console.Error.WriteLine("E3-PIPE server: --pipe is required");
                return ExitCodes.Usage;
            }

            SimplePipeMetrics metrics = new SimplePipeMetrics();
            PipeServerOptions serverOptions = new PipeServerOptions
            {
                PipeName = options.PipeName,
                MaxClients = 16,
                EnableEvents = true,
                MaxEventSubscribers = 8,

                // Deliberately small, so a slow subscriber reaches the bound in
                // seconds rather than in hours. The mechanics are what is being
                // proved; production capacity is a different claim.
                EventSubscriberQueueCapacity = 8,
                EventQueueOverflowPolicy = PipeEventQueueOverflowPolicy.DropNewest,
                MaxMessageBytes = 64 * 1024,
                Metrics = metrics
            };

            using (ManualResetEventSlim shutdown = new ManualResetEventSlim(false))
            using (PipeServer server = new PipeServer(serverOptions))
            {
                Map(server, shutdown);

                try
                {
                    server.Start();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("E3-PIPE server: could not bind '" + options.PipeName + "': " + ex.Message);
                    return ExitCodes.PrerequisiteMissing;
                }

                Console.Out.WriteLine("server ready on " + options.PipeName);
                Console.Out.Flush();

                // Ends on a shutdown request, on its own deadline, or when the
                // controller cancels. Being killed outright is also expected -
                // that is one of the faults - and leaves nothing to clean up
                // beyond the endpoint the OS releases with the process.
                DateTime deadline = DateTime.UtcNow + options.ChildDuration;
                while (!shutdown.IsSet && !ct.IsCancellationRequested && DateTime.UtcNow < deadline)
                    shutdown.Wait(TimeSpan.FromMilliseconds(200));

                WriteResult(options, metrics);
                return ExitCodes.Success;
            }
        }

        private static void Map(PipeServer server, ManualResetEventSlim shutdown)
        {
            server.Map(Ops.Echo, (message, ct) =>
                Task.FromResult(Reply(message, Payload.Text(message) ?? string.Empty)));

            server.Map(Ops.Slow, async (message, ct) =>
            {
                int milliseconds = ReadInt(Payload.Text(message), 250);
                await Task.Delay(milliseconds, ct).ConfigureAwait(false);
                return Reply(message, "slept " + milliseconds.ToString(CultureInfo.InvariantCulture));
            });

            server.Map(Ops.Boom, (message, ct) =>
                throw new InvalidOperationException("the scenario's handler failed on purpose"));

            server.Map(Ops.Fat, (message, ct) =>
                Task.FromResult(Reply(message, Payload.Make("fat", ReadInt(Payload.Text(message), 1024)))));

            server.Map(Ops.Publish, async (message, ct) =>
            {
                int count = ReadInt(Payload.Text(message), 10);
                PipeEventHub? hub = server.Events;

                if (hub != null)
                {
                    for (int i = 1; i <= count; i++)
                    {
                        // Sequence numbers are the whole point: retained-event
                        // ordering cannot be asserted without them.
                        await hub.PublishAsync("tick", i.ToString(CultureInfo.InvariantCulture), ct)
                            .ConfigureAwait(false);
                    }
                }

                return Reply(message, "published " + count.ToString(CultureInfo.InvariantCulture));
            });

            server.Map(Ops.Stats, (message, ct) =>
            {
                PipeEventHub? hub = server.Events;
                string text = "subscribers=" +
                              (hub == null ? "0" : hub.SubscriberCount.ToString(CultureInfo.InvariantCulture));

                return Task.FromResult(Reply(message, text));
            });

            server.Map(Ops.Shutdown, (message, ct) =>
            {
                shutdown.Set();
                return Task.FromResult(Reply(message, "shutting down"));
            });
        }

        private static PipeMessage Reply(PipeMessage request, string payload)
        {
            PipeMessage response = new PipeMessage
            {
                Id = request.Id,
                Type = "response",
                Name = request.Name,
                Ok = true
            };

            SetPayload(response, payload);
            return response;
        }

        /// <summary>
        /// Sets a response payload, in the one place the target difference has
        /// to be handled: the module stores <c>JsonElement</c> on modern .NET
        /// and <c>JToken</c> on net481.
        /// </summary>
        private static void SetPayload(PipeMessage message, string payload)
        {
#if NET9_0_OR_GREATER
            using (System.Text.Json.JsonDocument document =
                   System.Text.Json.JsonDocument.Parse(
                       System.Text.Json.JsonSerializer.Serialize(payload)))
            {
                message.Data = document.RootElement.Clone();
            }
#else
            message.Data = new Newtonsoft.Json.Linq.JValue(payload);
#endif
        }

        private static int ReadInt(string? text, int fallback)
        {
            int value;
            return text != null &&
                   int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                ? value
                : fallback;
        }

        private static void WriteResult(ScenarioOptions options, SimplePipeMetrics metrics)
        {
            if (options.ChildResultPath.Length == 0) return;

            PipeMetricsSnapshot snapshot = metrics.Snapshot();
            JsonWriter json = new JsonWriter();
            json.Object(null, () =>
            {
                json.Prop("role", "server");
                json.Prop("pipeName", options.PipeName);
                json.Prop("requests", snapshot.Server.Requests);
                json.Prop("success", snapshot.Server.Success);
                json.Prop("failures", snapshot.Server.Failures);
                json.Prop("connectedClients", snapshot.Server.ConnectedClients);
            });

            SafeWrite(options.ChildResultPath, json.ToString());
        }

        internal static void SafeWrite(string path, string content)
        {
            try { File.WriteAllText(path, content, new UTF8Encoding(false)); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// The client child: a real process driving sustained request/response load.
    /// <para/>
    /// It asserts nothing. Its job is to be traffic and to report what it saw,
    /// so the controller can assert on the aggregate; putting checks here would
    /// scatter the verdict across processes and make a killed child look like a
    /// failed assertion.
    /// </summary>
    internal static class ClientRole
    {
        public static int Run(ScenarioOptions options, CancellationToken ct)
        {
            if (options.PipeName.Length == 0)
            {
                Console.Error.WriteLine("E3-PIPE client: --pipe is required");
                return ExitCodes.Usage;
            }

            long sent = 0;
            long ok = 0;
            long failed = 0;
            long mismatched = 0;

            DateTime deadline = DateTime.UtcNow + options.ChildDuration;
            int[] sizes = { 64, 4 * 1024, 32 * 1024 };
            int index = 0;

            while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
            {
                int size = sizes[index++ % sizes.Length];
                string marker = "c" + Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture) +
                                "-" + sent.ToString(CultureInfo.InvariantCulture);
                string payload = Payload.Make(marker, size);

                try
                {
                    using (PipeClient client = new PipeClient(new PipeClientOptions
                    {
                        PipeName = options.PipeName,
                        ConnectTimeout = TimeSpan.FromSeconds(5),
                        RequestTimeout = TimeSpan.FromSeconds(10),
                        MaxMessageBytes = 64 * 1024
                    }))
                    {
                        sent++;
                        PipeMessage response = client
                            .SendAsync(Ops.Echo, payload, ct).GetAwaiter().GetResult();

                        if (!response.Ok) failed++;
                        else
                        {
                            ok++;

                            // Correlation: the response must carry back this
                            // request's own marker, not another client's.
                            string? text = Payload.Text(response);
                            if (text == null || !text.StartsWith(marker, StringComparison.Ordinal))
                                mismatched++;
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception)
                {
                    // Expected while a fault has the server away. The controller
                    // owns the judgement about whether that was acceptable.
                    failed++;
                    try { Thread.Sleep(100); } catch (ThreadInterruptedException) { }
                }
            }

            if (options.ChildResultPath.Length > 0)
            {
                JsonWriter json = new JsonWriter();
                json.Object(null, () =>
                {
                    json.Prop("role", "client");
                    json.Prop("pipeName", options.PipeName);
                    json.Prop("sent", sent);
                    json.Prop("ok", ok);
                    json.Prop("failed", failed);
                    json.Prop("mismatchedCorrelation", mismatched);
                });

                ServerRole.SafeWrite(options.ChildResultPath, json.ToString());
            }

            // A client that never correlated a response wrongly is the only
            // hard failure it can report on its own.
            return mismatched == 0 ? ExitCodes.Success : ExitCodes.CheckFailed;
        }
    }
}
