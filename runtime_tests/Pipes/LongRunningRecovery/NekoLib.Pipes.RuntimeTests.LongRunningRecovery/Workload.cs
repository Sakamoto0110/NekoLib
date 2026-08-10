#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Pipes;
using NekoLib.RuntimeTests.Harness.Reporting;

namespace NekoLib.Pipes.RuntimeTests.LongRunningRecovery
{
    /// <summary>What every phase needs, in one place.</summary>
    internal sealed class PhaseContext
    {
        public CheckRunner Runner = null!;
        public RunArtifacts Artifacts = null!;
        public WorkloadCounters Counters = null!;
        public ResourceSampler Sampler = null!;
        public ScenarioSamples Samples = null!;
        public string PipeName = string.Empty;
        public int Seed;
        public CancellationToken Ct;

        /// <summary>
        /// Serialises assertions against faults, for the same reason every other
        /// Phase E scenario does: an assertion made while a scheduled fault has
        /// the server killed is measuring the fault.
        /// </summary>
        public readonly SemaphoreSlim ExclusiveAccess = new SemaphoreSlim(1, 1);

        public async Task ExclusiveAsync(Func<Task> work)
        {
            await ExclusiveAccess.WaitAsync(Ct).ConfigureAwait(false);
            try { await work().ConfigureAwait(false); }
            finally { ExclusiveAccess.Release(); }
        }

        public PipeClient NewClient(
            TimeSpan? requestTimeout = null,
            int maxMessageBytes = 64 * 1024)
        {
            return new PipeClient(new PipeClientOptions
            {
                PipeName = PipeName,
                ConnectTimeout = TimeSpan.FromSeconds(5),
                RequestTimeout = requestTimeout ?? TimeSpan.FromSeconds(10),
                MaxMessageBytes = maxMessageBytes
            });
        }

        public async Task<PipeMessage> SendAsync(string name, object? payload, PipeClient? client = null)
        {
            Samples.RequestsSent.Increment();

            if (client != null) return await client.SendAsync(name, payload, Ct).ConfigureAwait(false);

            using (PipeClient owned = NewClient())
                return await owned.SendAsync(name, payload, Ct).ConfigureAwait(false);
        }

        public static async Task<Exception?> CaptureAsync(Func<Task> call)
        {
            try { await call().ConfigureAwait(false); return null; }
            catch (Exception ex) { return ex; }
        }

        public static Task CompletedTask
        {
            get
            {
#if NET6_0_OR_GREATER
                return Task.CompletedTask;
#else
                return Task.FromResult(0);
#endif
            }
        }
    }

    /// <summary>
    /// Request and response behaviour across a real process boundary.
    /// </summary>
    internal static class RequestMatrix
    {
        private const string Phase = Phases.Request;

        public static async Task RunAsync(PhaseContext context)
        {
            await PayloadSizes(context).ConfigureAwait(false);
            await ConcurrentCorrelation(context).ConfigureAwait(false);
            await ErrorContracts(context).ConfigureAwait(false);
            await TimeoutThenClean(context).ConfigureAwait(false);
            await ReconnectCycles(context).ConfigureAwait(false);
        }

        private static Task PayloadSizes(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "payload-sizes",
                "small, medium and near-limit payloads round-trip byte-for-byte",
                async check =>
                {
                    // 64 KiB is the configured frame limit, so 60 KiB is "near"
                    // without being over: the over-limit case is its own check.
                    int[] sizes = { 32, 4 * 1024, 60 * 1024 };

                    foreach (int size in sizes)
                    {
                        string marker = "size" + size.ToString(CultureInfo.InvariantCulture);
                        string payload = Payload.Make(marker, size);

                        PipeMessage response = await context.SendAsync(Ops.Echo, payload).ConfigureAwait(false);

                        check.That(response.Ok, "a " + size + "-byte echo failed: " + Payload.Describe(response));
                        check.Equal(size, Payload.Length(response), "echoed bytes for a " + size + "-byte payload");
                        check.Equal(payload, Payload.Text(response), "echoed content for a " + size + "-byte payload");

                        context.Counters.Success();
                    }

                    check.Note("round-tripped " + sizes.Length + " payload sizes up to 60 KiB against a 64 KiB frame limit");
                });
        }

        private static Task ConcurrentCorrelation(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "concurrent-correlation",
                "concurrent clients each receive their own response and never another's",
                async check =>
                {
                    const int clients = 8;
                    const int each = 12;

                    Task<int>[] running = new Task<int>[clients];

                    for (int c = 0; c < clients; c++)
                    {
                        int id = c;
                        running[c] = Task.Run(async () =>
                        {
                            int wrong = 0;

                            using (PipeClient client = context.NewClient())
                            {
                                for (int i = 0; i < each; i++)
                                {
                                    string marker = "w" + id + "-" + i;
                                    PipeMessage response = await context
                                        .SendAsync(Ops.Echo, Payload.Make(marker, 256), client)
                                        .ConfigureAwait(false);

                                    string? text = Payload.Text(response);
                                    if (!response.Ok || text == null ||
                                        !text.StartsWith(marker, StringComparison.Ordinal))
                                    {
                                        wrong++;
                                    }
                                    else context.Counters.Success();
                                }
                            }

                            return wrong;
                        }, context.Ct);
                    }

                    int[] wrongPerClient = await Task.WhenAll(running).ConfigureAwait(false);

                    int total = 0;
                    foreach (int wrong in wrongPerClient) total += wrong;

                    check.Equal(0, total, "responses that went to the wrong caller");
                    check.Note(clients + " concurrent clients exchanged " + (clients * each) +
                               " requests with no crossed response");
                });
        }

        private static Task ErrorContracts(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "error-contracts",
                "an unmapped operation and a throwing handler each return their documented error code",
                async check =>
                {
                    PipeMessage missing = await context.SendAsync(Ops.Missing, "x").ConfigureAwait(false);
                    check.That(!missing.Ok, "an unmapped operation reported success");
                    check.That(missing.Error != null, "an unmapped operation returned no error");
                    check.Equal("not_found", missing.Error!.Code, "error code for an unmapped operation");
                    context.Counters.ExpectedFailure();

                    PipeMessage boom = await context.SendAsync(Ops.Boom, "x").ConfigureAwait(false);
                    check.That(!boom.Ok, "a throwing handler reported success");
                    check.That(boom.Error != null, "a throwing handler returned no error");
                    check.Equal("exception", boom.Error!.Code, "error code for a throwing handler");
                    context.Counters.ExpectedFailure();

                    // The connection must remain usable after both.
                    PipeMessage after = await context.SendAsync(Ops.Echo, "after-errors").ConfigureAwait(false);
                    check.That(after.Ok, "an ordinary request after two error contracts failed");
                    context.Counters.Success();

                    check.Note("not_found and exception both returned an error response rather than dropping the connection");
                });
        }

        private static Task TimeoutThenClean(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "timeout-does-not-corrupt-the-next-response",
                "a timed-out request fails within its bound and the next response is its own",
                async check =>
                {
                    System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
                    Exception? failure;

                    using (PipeClient impatient = context.NewClient(TimeSpan.FromMilliseconds(400)))
                    {
                        failure = await PhaseContext.CaptureAsync(() =>
                            context.SendAsync(Ops.Slow, "3000", impatient)).ConfigureAwait(false);
                    }

                    clock.Stop();

                    check.That(failure != null, "a request that outlived its timeout reported success");
                    check.That(clock.Elapsed < TimeSpan.FromSeconds(10),
                        "the timeout took " + clock.ElapsedMilliseconds + "ms against a 400ms budget");

                    check.Note("timeout surfaced as " + failure!.GetType().Name + " after " +
                               clock.ElapsedMilliseconds + "ms");

                    context.Counters.ExpectedFailure();

                    // The abandoned response must not be delivered to the next
                    // caller. A fresh connection asks for something unmistakable.
                    string marker = "after-timeout-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                    PipeMessage next = await context.SendAsync(Ops.Echo, marker).ConfigureAwait(false);

                    check.That(next.Ok, "the request after a timeout failed");
                    check.Equal(marker, Payload.Text(next), "the response after a timeout");
                    context.Counters.Success();
                });
        }

        private static Task ReconnectCycles(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "client-reconnect-cycles",
                "repeated connect, request and disconnect cycles leave the server serving",
                async check =>
                {
                    const int cycles = 40;

                    for (int i = 0; i < cycles; i++)
                    {
                        using (PipeClient client = context.NewClient())
                        {
                            PipeMessage response = await context
                                .SendAsync(Ops.Echo, "cycle-" + i, client).ConfigureAwait(false);

                            check.That(response.Ok, "cycle " + i + " failed: " + Payload.Describe(response));
                            context.Counters.Success();
                        }
                    }

                    check.Note(cycles + " client-initiated connect/request/disconnect cycles all succeeded");
                });
        }
    }

    /// <summary>Event hub behaviour: churn, ordering, and the bounded queue.</summary>
    internal static class EventMatrix
    {
        private const string Phase = Phases.Events;

        public static async Task RunAsync(PhaseContext context)
        {
            await OrderedDelivery(context).ConfigureAwait(false);
            await SubscriberChurn(context).ConfigureAwait(false);
        }

        private static Task OrderedDelivery(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "ordered-delivery",
                "a subscriber receives published events in publication order",
                async check =>
                {
                    const int events = 40;
                    List<int> received = new List<int>();
                    ManualResetEventSlim connected = new ManualResetEventSlim(false);

                    using (PipeEventClient subscriber = new PipeEventClient(context.PipeName))
                    {
                        subscriber.OnConnected += () => connected.Set();
                        subscriber.OnEvent += message =>
                        {
                            string? text = Payload.Text(message);
                            int value;
                            if (text != null &&
                                int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                            {
                                lock (received) received.Add(value);
                                context.Samples.EventsReceived.Increment();
                            }
                        };

                        subscriber.Start();

                        check.That(connected.Wait(TimeSpan.FromSeconds(10)),
                            "the subscriber never connected to " + Endpoint.EventsFor(context.PipeName));

                        context.Samples.Subscribers.Set(1);

                        PipeMessage published = await context
                            .SendAsync(Ops.Publish, events.ToString(CultureInfo.InvariantCulture))
                            .ConfigureAwait(false);

                        check.That(published.Ok, "the publish request failed: " + Payload.Describe(published));
                        context.Counters.Success();

                        // Bounded wait: the hub is asynchronous, so a fixed sleep
                        // would be a race dressed as a delay.
                        DateTime deadline = DateTime.UtcNow.AddSeconds(15);
                        while (DateTime.UtcNow < deadline)
                        {
                            lock (received) if (received.Count >= events) break;
                            await Task.Delay(50, context.Ct).ConfigureAwait(false);
                        }

                        int[] seen;
                        lock (received) seen = received.ToArray();

                        check.That(seen.Length > 0, "no event reached the subscriber at all");

                        for (int i = 1; i < seen.Length; i++)
                        {
                            check.That(seen[i] > seen[i - 1],
                                "events arrived out of order at index " + i + ": " +
                                seen[i - 1] + " then " + seen[i]);
                        }

                        check.Note(seen.Length + " of " + events + " events observed, in publication order" +
                                   (seen.Length < events
                                       ? " (the remainder had not arrived inside the bounded wait)"
                                       : string.Empty));
                    }

                    context.Samples.Subscribers.Set(0);
                });
        }

        private static Task SubscriberChurn(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "subscriber-churn",
                "repeated subscriber connect and disconnect leaves the hub serving",
                async check =>
                {
                    const int cycles = 10;
                    int connects = 0;

                    for (int i = 0; i < cycles; i++)
                    {
                        ManualResetEventSlim connected = new ManualResetEventSlim(false);

                        using (PipeEventClient subscriber = new PipeEventClient(context.PipeName))
                        {
                            subscriber.AutoReconnect = false;
                            subscriber.OnConnected += () => connected.Set();
                            subscriber.Start();

                            if (connected.Wait(TimeSpan.FromSeconds(10))) connects++;
                        }

                        await Task.Delay(20, context.Ct).ConfigureAwait(false);
                    }

                    check.Equal(cycles, connects, "subscriber connections across churn cycles");

                    // The hub must still serve an ordinary request afterwards.
                    PipeMessage stats = await context.SendAsync(Ops.Stats, null).ConfigureAwait(false);
                    check.That(stats.Ok, "the server stopped answering after subscriber churn");
                    context.Counters.Success();

                    check.Note(cycles + " connect/disconnect cycles; the hub reported " +
                               Payload.Text(stats) + " afterwards");
                });
        }
    }

    /// <summary>Frame limits and peers that do not follow the protocol.</summary>
    internal static class ProtocolMatrix
    {
        private const string Phase = Phases.Protocol;

        public static async Task RunAsync(PhaseContext context)
        {
            await OversizeRequest(context).ConfigureAwait(false);
            await OversizeResponse(context).ConfigureAwait(false);
            await MalformedPeer(context).ConfigureAwait(false);
        }

        private static Task OversizeRequest(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "oversize-request-refused",
                "a request beyond the frame limit is refused locally and the connection stays usable",
                async check =>
                {
                    Exception? failure;

                    using (PipeClient client = context.NewClient())
                    {
                        failure = await PhaseContext.CaptureAsync(() =>
                            context.SendAsync(Ops.Echo, Payload.Make("huge", 200 * 1024), client))
                            .ConfigureAwait(false);

                        check.That(failure != null, "an over-limit request was accepted");
                        check.Note("over-limit request surfaced as " + failure!.GetType().Name + ": " +
                                   Payload.Flatten(failure.Message));

                        context.Counters.ExpectedFailure();
                    }

                    PipeMessage after = await context.SendAsync(Ops.Echo, "after-oversize").ConfigureAwait(false);
                    check.That(after.Ok, "an ordinary request after an over-limit one failed");
                    context.Counters.Success();
                });
        }

        private static Task OversizeResponse(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "oversize-response-reported",
                "a response beyond the frame limit returns the documented protocol error",
                async check =>
                {
                    PipeMessage response = await context
                        .SendAsync(Ops.Fat, (200 * 1024).ToString(CultureInfo.InvariantCulture))
                        .ConfigureAwait(false);

                    check.That(!response.Ok, "an over-limit response was delivered");
                    check.That(response.Error != null, "an over-limit response carried no error");
                    check.Equal("response_too_large", response.Error!.Code, "error code for an over-limit response");
                    context.Counters.ExpectedFailure();

                    PipeMessage after = await context.SendAsync(Ops.Echo, "after-fat").ConfigureAwait(false);
                    check.That(after.Ok, "an ordinary request after an over-limit response failed");
                    context.Counters.Success();

                    check.Note("the server reported response_too_large rather than truncating or dropping");
                });
        }

        private static Task MalformedPeer(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "malformed-peer-does-not-disturb-the-server",
                "a raw peer that lies about a frame or closes silently is dropped without affecting other clients",
                async check =>
                {
                    string truncated = RawPeer.TruncatedFrame(context.PipeName, 4096, 16);
                    check.Note("raw peer: " + truncated);
                    context.Counters.ExpectedFailure();

                    string silent = RawPeer.SilentClose(context.PipeName);
                    check.Note("raw peer: " + silent);
                    context.Counters.ExpectedFailure();

                    // The whole point: the server survived both.
                    for (int i = 0; i < 3; i++)
                    {
                        PipeMessage response = await context
                            .SendAsync(Ops.Echo, "after-malformed-" + i).ConfigureAwait(false);

                        check.That(response.Ok,
                            "request " + i + " after a malformed peer failed: " + Payload.Describe(response));

                        context.Counters.Success();
                    }

                    check.Note("three ordinary requests succeeded after both malformed peers");
                });
        }
    }

    /// <summary>Disposal, endpoint release and rebinding.</summary>
    internal static class LifecycleMatrix
    {
        private const string Phase = Phases.Lifecycle;

        public static Task RunAsync(PhaseContext context) => DisposeUnderLoad(context);

        private static Task DisposeUnderLoad(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "dispose-and-rebind-a-private-endpoint",
                "a server disposed while requests are in flight releases its name, which can then be bound again",
                async check =>
                {
                    // A private endpoint, so the shared server the other phases
                    // depend on is never disturbed by this one.
                    string name = context.PipeName + ".life";

                    check.That(!Endpoint.IsBound(name), "the private endpoint was already bound before the check");

                    PipeServerOptions options = new PipeServerOptions
                    {
                        PipeName = name,
                        MaxClients = 8,
                        EnableEvents = false,
                        MaxMessageBytes = 32 * 1024
                    };

                    using (PipeServer server = new PipeServer(options))
                    {
                        server.Map(Ops.Slow, async (message, ct) =>
                        {
                            await Task.Delay(300, ct).ConfigureAwait(false);
                            return new PipeMessage { Id = message.Id, Type = "response", Name = message.Name, Ok = true };
                        });

                        server.Start();
                        check.That(Endpoint.IsBound(name), "the server did not bind " + name);

                        // Admit work, then dispose underneath it.
                        Task<Exception?> inFlight = Task.Run(async () =>
                        {
                            using (PipeClient client = new PipeClient(new PipeClientOptions
                            {
                                PipeName = name,
                                ConnectTimeout = TimeSpan.FromSeconds(5),
                                RequestTimeout = TimeSpan.FromSeconds(5)
                            }))
                            {
                                return await PhaseContext.CaptureAsync(() =>
                                    client.SendAsync(Ops.Slow, "300", context.Ct)).ConfigureAwait(false);
                            }
                        }, context.Ct);

                        await Task.Delay(120, context.Ct).ConfigureAwait(false);

                        Exception? disposal = await PhaseContext.CaptureAsync(() =>
                        {
                            server.Dispose();
                            return PhaseContext.CompletedTask;
                        }).ConfigureAwait(false);

                        check.That(disposal == null,
                            "disposing under load threw " + (disposal == null ? string.Empty : disposal.GetType().Name));

                        Exception? admitted = await inFlight.ConfigureAwait(false);
                        check.Note("the in-flight request ended as " +
                                   (admitted == null ? "a completed response" : admitted.GetType().Name));

                        context.Counters.ExpectedFailure();
                    }

                    // Releasing the name is what makes a restart possible, and is
                    // the assertion a restart really rests on.
                    DateTime deadline = DateTime.UtcNow.AddSeconds(10);
                    while (Endpoint.IsBound(name) && DateTime.UtcNow < deadline)
                        await Task.Delay(100, context.Ct).ConfigureAwait(false);

                    check.That(!Endpoint.IsBound(name), "the endpoint was still bound 10s after disposal");

                    using (PipeServer replacement = new PipeServer(new PipeServerOptions
                    {
                        PipeName = name,
                        MaxClients = 8,
                        EnableEvents = false,
                        MaxMessageBytes = 32 * 1024
                    }))
                    {
                        replacement.Map(Ops.Echo, (message, ct) =>
                            Task.FromResult(new PipeMessage
                            {
                                Id = message.Id,
                                Type = "response",
                                Name = message.Name,
                                Ok = true
                            }));

                        Exception? rebind = await PhaseContext.CaptureAsync(() =>
                        {
                            replacement.Start();
                            return PhaseContext.CompletedTask;
                        }).ConfigureAwait(false);

                        check.That(rebind == null,
                            "rebinding the released name threw " +
                            (rebind == null ? string.Empty : rebind.GetType().Name));

                        using (PipeClient client = new PipeClient(new PipeClientOptions
                        {
                            PipeName = name,
                            ConnectTimeout = TimeSpan.FromSeconds(5),
                            RequestTimeout = TimeSpan.FromSeconds(5)
                        }))
                        {
                            PipeMessage response = await client
                                .SendAsync(Ops.Echo, "rebound", context.Ct).ConfigureAwait(false);

                            check.That(response.Ok, "the rebound server did not answer");
                            context.Counters.Success();
                        }
                    }

                    check.Note("disposed under load, released the name, rebound it and served a request on it");
                });
        }
    }
}
