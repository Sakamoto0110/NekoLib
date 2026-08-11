#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Pipes;
using NekoLib.RuntimeTests.Harness.Faults;

namespace NekoLib.Pipes.RuntimeTests.LongRunningRecovery
{
    /// <summary>
    /// What the fault dispatcher is allowed to do to the processes the
    /// controller owns.
    /// <para/>
    /// Narrow on purpose. A fault may end a child this controller started and
    /// ask for a replacement; it may not touch anything else on the machine, and
    /// it has no way to reach inside a running server.
    /// </summary>
    internal interface IProcessControl
    {
        ChildProcess? Server { get; }
        IReadOnlyList<ChildProcess> Clients { get; }

        /// <summary>Starts a fresh server child on the same endpoint.</summary>
        ChildProcess RestartServer();

        /// <summary>Ends one client child, returning what happened.</summary>
        bool KillOneClient(out string diagnostic);

        /// <summary>Waits until the endpoint is bound, or gives up.</summary>
        Task<bool> WaitForServerAsync(TimeSpan timeout);
    }

    /// <summary>
    /// The event-hub overflow behaviour, both policies.
    /// <para/>
    /// These run against hubs the controller owns on private endpoints rather
    /// than against the server child, because the policy is fixed when a hub is
    /// constructed. Asking the child to change it at runtime would be the
    /// privileged control plane the suite forbids; standing up a hub with the
    /// other policy is the same code path and costs nothing.
    /// </summary>
    internal static class OverflowMatrix
    {
        private const string Phase = Phases.Events;

        public static async Task RunAsync(PhaseContext context)
        {
            await Policy(context, PipeEventQueueOverflowPolicy.DropNewest).ConfigureAwait(false);
            await Policy(context, PipeEventQueueOverflowPolicy.DisconnectSubscriber).ConfigureAwait(false);
        }

        private static Task Policy(PhaseContext context, PipeEventQueueOverflowPolicy policy)
        {
            bool dropping = policy == PipeEventQueueOverflowPolicy.DropNewest;
            string name = dropping ? "overflow-drop-newest" : "overflow-disconnect-subscriber";

            return context.Runner.RunAsync(Phase, name,
                dropping
                    ? "a slow subscriber's overflow is dropped and counted, and it stays connected"
                    : "a slow subscriber that overflows is disconnected, and the count reflects it",
                async check =>
                {
                    const int capacity = 4;
                    const int published = 200;

                    string endpoint = context.PipeName + (dropping ? ".ovd" : ".ovx");
                    SimplePipeMetrics metrics = new SimplePipeMetrics();

                    using (PipeEventHub hub = new PipeEventHub(
                        endpoint,
                        maxSubscribers: 4,
                        accessPolicy: PipeAccessPolicy.PlatformDefault,
                        subscriberQueueCapacity: capacity,
                        overflowPolicy: policy,
                        metrics: metrics))
                    {
                        hub.Start();

                        // A subscriber that connects and then never reads. The
                        // pipe buffer plus the bounded queue fill, and the hub
                        // has to decide what to do about it.
                        using (System.IO.Pipes.NamedPipeClientStream lazy =
                               new System.IO.Pipes.NamedPipeClientStream(
                                   ".", Endpoint.EventsFor(endpoint), System.IO.Pipes.PipeDirection.InOut))
                        {
                            lazy.Connect(5000);

                            DateTime seen = DateTime.UtcNow.AddSeconds(5);
                            while (hub.SubscriberCount == 0 && DateTime.UtcNow < seen)
                                await Task.Delay(50, context.Ct).ConfigureAwait(false);

                            check.Equal(1, hub.SubscriberCount, "subscribers after the lazy peer connected");

                            for (int i = 1; i <= published; i++)
                            {
                                await hub.PublishAsync("tick", i.ToString(CultureInfo.InvariantCulture), context.Ct)
                                    .ConfigureAwait(false);
                            }

                            // Give the hub a bounded moment to act on the overflow.
                            DateTime settle = DateTime.UtcNow.AddSeconds(5);
                            while (DateTime.UtcNow < settle)
                            {
                                if (!dropping && hub.SubscriberCount == 0) break;
                                await Task.Delay(50, context.Ct).ConfigureAwait(false);
                            }

                            PipeMetricsSnapshot snapshot = metrics.Snapshot();
                            long failed = snapshot.Events.Failed;

                            context.Samples.EventsDropped.Set(failed);

                            check.That(snapshot.Events.Published > 0, "the hub reported no publish at all");
                            check.That(failed > 0,
                                "publishing " + published + " events into a queue of " + capacity +
                                " to a subscriber that never reads reported no failed delivery");

                            check.Note("published=" + snapshot.Events.Published +
                                       " delivered=" + snapshot.Events.Delivered +
                                       " failed=" + failed +
                                       " (failed is the truthful dropped-event count: the hub completes an " +
                                       "overflowed delivery as unsuccessful)");

                            if (dropping)
                            {
                                check.Equal(1, hub.SubscriberCount,
                                    "subscribers after overflow under DropNewest; it must stay connected");
                            }
                            else
                            {
                                check.Equal(0, hub.SubscriberCount,
                                    "subscribers after overflow under DisconnectSubscriber; it must be dropped");
                            }

                            context.Counters.ExpectedFailure();
                        }

                        // The hub must still serve after all of that.
                        Exception? afterwards = await PhaseContext.CaptureAsync(() =>
                            hub.PublishAsync("tick", "after", context.Ct)).ConfigureAwait(false);

                        check.That(afterwards == null,
                            "publishing after an overflow threw " +
                            (afterwards == null ? string.Empty : afterwards.GetType().Name));

                        context.Counters.Success();
                    }
                });
        }
    }

    /// <summary>
    /// Cancellation and server-initiated disconnect, which the timeout check
    /// does not cover.
    /// </summary>
    internal static class CancellationMatrix
    {
        public static async Task RunAsync(PhaseContext context)
        {
            await TokenCancellation(context).ConfigureAwait(false);
            await ServerInitiatedDisconnect(context).ConfigureAwait(false);
        }

        private static Task TokenCancellation(PhaseContext context)
        {
            return context.Runner.RunAsync(Phases.Request, "token-cancellation",
                "a request cancelled by its token ends promptly and leaves the next request clean",
                async check =>
                {
                    // Distinct from the timeout check: there the client's own
                    // deadline expires; here the caller withdraws while the
                    // request is demonstrably still running on the server.
                    using (CancellationTokenSource cancellation = new CancellationTokenSource())
                    using (PipeClient client = context.NewClient(TimeSpan.FromSeconds(30)))
                    {
                        Task<PipeMessage> pending = client.SendAsync(Ops.Slow, "5000", cancellation.Token);

                        await Task.Delay(300, context.Ct).ConfigureAwait(false);

                        System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
                        cancellation.Cancel();

                        Exception? failure = await PhaseContext.CaptureAsync(() => pending).ConfigureAwait(false);
                        clock.Stop();

                        check.That(failure != null, "a cancelled request reported success");
                        check.That(clock.Elapsed < TimeSpan.FromSeconds(15),
                            "cancellation took " + clock.ElapsedMilliseconds + "ms to surface");

                        check.Note("cancellation surfaced as " + failure!.GetType().Name + " after " +
                                   clock.ElapsedMilliseconds + "ms");

                        context.Counters.Cancellation();
                    }

                    string marker = "after-cancel-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                    PipeMessage next = await context.SendAsync(Ops.Echo, marker).ConfigureAwait(false);

                    check.That(next.Ok, "the request after a cancellation failed");
                    check.Equal(marker, Payload.Text(next), "the response after a cancellation");
                    context.Counters.Success();
                });
        }

        private static Task ServerInitiatedDisconnect(PhaseContext context)
        {
            return context.Runner.RunAsync(Phases.Lifecycle, "server-initiated-disconnect",
                "a server that goes away drops its clients, and a replacement serves them again",
                async check =>
                {
                    string endpoint = context.PipeName + ".sid";

                    using (PipeServer server = new PipeServer(new PipeServerOptions
                    {
                        PipeName = endpoint,
                        MaxClients = 4,
                        EnableEvents = false,
                        MaxMessageBytes = 32 * 1024
                    }))
                    {
                        server.Map(Ops.Echo, (message, ct) => Task.FromResult(
                            new PipeMessage { Id = message.Id, Type = "response", Name = message.Name, Ok = true }));

                        server.Start();

                        using (PipeClient client = new PipeClient(new PipeClientOptions
                        {
                            PipeName = endpoint,
                            ConnectTimeout = TimeSpan.FromSeconds(5),
                            RequestTimeout = TimeSpan.FromSeconds(5)
                        }))
                        {
                            PipeMessage before = await client.SendAsync(Ops.Echo, "before", context.Ct)
                                .ConfigureAwait(false);

                            check.That(before.Ok, "the private server did not answer before disconnecting");
                            context.Counters.Success();

                            server.Dispose();

                            // The disconnect is the server's decision, and the
                            // client must learn about it by failing rather than
                            // by hanging.
                            Exception? after = await PhaseContext.CaptureAsync(() =>
                                client.SendAsync(Ops.Echo, "after", context.Ct)).ConfigureAwait(false);

                            check.That(after != null, "a request after the server disconnected reported success");
                            check.Note("server-initiated disconnect surfaced as " + after!.GetType().Name);
                            context.Counters.ExpectedFailure();
                        }
                    }

                    await WaitUnbound(context, endpoint).ConfigureAwait(false);
                    check.That(!Endpoint.IsBound(endpoint), "the endpoint stayed bound after the server went away");
                });
        }

        internal static async Task WaitUnbound(PhaseContext context, string endpoint)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (Endpoint.IsBound(endpoint) && DateTime.UtcNow < deadline)
                await Task.Delay(100, context.Ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Dispatches the persisted seeded schedule and proves each fault's expected
    /// terminal and recovery.
    /// </summary>
    internal static class RecoveryMatrix
    {
        private const string Phase = Phases.Recovery;

        public static async Task RunAsync(
            PhaseContext context,
            IProcessControl control,
            FaultSchedule schedule,
            DateTime startedUtc)
        {
            if (schedule.Events.Count == 0)
            {
                context.Artifacts.Out("recovery no faults planned for this mode");
                return;
            }

            foreach (FaultEvent planned in schedule.Events)
            {
                TimeSpan wait = startedUtc.AddSeconds(planned.OffsetSeconds) - DateTime.UtcNow;

                if (wait > TimeSpan.Zero)
                {
                    context.Artifacts.Out(
                        "recovery waiting " + ((int)wait.TotalSeconds).ToString(CultureInfo.InvariantCulture) +
                        "s for " + planned.Kind);

                    await Task.Delay(wait, context.Ct).ConfigureAwait(false);
                }

                context.Sampler.Take(Phase, "pre-fault");

                context.Artifacts.Event("fault", json =>
                {
                    json.Prop("id", planned.Id);
                    json.Prop("kind", planned.Kind);
                    json.Prop("target", planned.Target);
                    json.Prop("plannedOffsetSeconds", planned.OffsetSeconds);
                    json.Prop("actualUtc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                    json.Prop("expectedRecovery", planned.ExpectedRecovery);
                });

                await context.ExclusiveAsync(() => DispatchAsync(context, control, planned)).ConfigureAwait(false);

                context.Sampler.Take(Phase, "post-recovery");
            }
        }

        private static Task DispatchAsync(PhaseContext context, IProcessControl control, FaultEvent planned)
        {
            switch (planned.Kind)
            {
                case FaultKinds.KillServer: return KillServer(context, control, planned);
                case FaultKinds.KillClient: return KillClient(context, control, planned);
                case FaultKinds.RawPeerMidFrame: return RawPeerMidFrame(context, planned);
                case FaultKinds.HandlerDelay: return HandlerDelay(context, planned);
                case FaultKinds.SlowSubscriber: return SlowSubscriber(context, planned);
                case FaultKinds.DisposeUnderLoad: return DisposeUnderLoad(context, planned);

                default:
                    context.Runner.Skip(Phase, planned.Kind, planned.ExpectedRecovery,
                        "no handler is registered for this fault kind");
                    return PhaseContext.CompletedTask;
            }
        }

        private static Task KillServer(PhaseContext context, IProcessControl control, FaultEvent planned)
        {
            return context.Runner.RunAsync(Phase, planned.Kind, planned.ExpectedRecovery,
                async check =>
                {
                    ChildProcess? server = control.Server;
                    check.That(server != null, "there was no server child to kill");

                    // A request in flight when the server dies must fail rather
                    // than hang: a hang is what would turn a killed worker into
                    // a stalled campaign.
                    Task<Exception?> inFlight = Task.Run(async () =>
                    {
                        using (PipeClient client = context.NewClient(TimeSpan.FromSeconds(20)))
                        {
                            return await PhaseContext.CaptureAsync(() =>
                                client.SendAsync(Ops.Slow, "4000", context.Ct)).ConfigureAwait(false);
                        }
                    }, context.Ct);

                    await Task.Delay(400, context.Ct).ConfigureAwait(false);

                    string diagnostic;
                    check.That(server!.Kill(out diagnostic), "the server child could not be ended: " + diagnostic);
                    check.Note("server pid " + server.Id + " " + diagnostic);

                    System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
                    Exception? admitted = await inFlight.ConfigureAwait(false);
                    clock.Stop();

                    check.That(admitted != null, "an in-flight request survived the server being killed");
                    check.That(clock.Elapsed < TimeSpan.FromSeconds(30),
                        "the in-flight request took " + clock.Elapsed.TotalSeconds.ToString("F0") +
                        "s to fail after the server died");

                    check.Note("the in-flight request failed as " + admitted!.GetType().Name +
                               " " + clock.ElapsedMilliseconds + "ms after the kill");

                    context.Counters.ExpectedFailure();

                    // The endpoint must be released by the dead process, or a
                    // replacement could never bind it.
                    await CancellationMatrix.WaitUnbound(context, context.PipeName).ConfigureAwait(false);
                    check.That(!Endpoint.IsBound(context.PipeName),
                        "the endpoint was still bound after the server process died");

                    control.RestartServer();
                    check.That(await control.WaitForServerAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false),
                        "a replacement server did not bind " + context.PipeName);

                    PipeMessage probe = await context.SendAsync(Ops.Echo, "after-server-restart")
                        .ConfigureAwait(false);

                    check.That(probe.Ok, "the replacement server did not answer: " + Payload.Describe(probe));
                    context.Counters.Success();
                });
        }

        private static Task KillClient(PhaseContext context, IProcessControl control, FaultEvent planned)
        {
            return context.Runner.RunAsync(Phase, planned.Kind, planned.ExpectedRecovery,
                async check =>
                {
                    if (control.Clients.Count == 0)
                    {
                        context.Runner.Skip(Phase, planned.Kind + "-skipped", planned.ExpectedRecovery,
                            "this mode started no client children, so there was none to kill");
                        return;
                    }

                    string diagnostic;
                    bool killed = control.KillOneClient(out diagnostic);
                    check.That(killed, "a client child could not be ended: " + diagnostic);
                    check.Note("client " + diagnostic);
                    context.Counters.ExpectedFailure();

                    // The server must shed the connection and keep serving. The
                    // proof that matters is that it still answers.
                    for (int i = 0; i < 3; i++)
                    {
                        PipeMessage probe = await context.SendAsync(Ops.Echo, "after-client-kill-" + i)
                            .ConfigureAwait(false);

                        check.That(probe.Ok,
                            "request " + i + " after a client was killed failed: " + Payload.Describe(probe));

                        context.Counters.Success();
                    }

                    check.Note("the server kept answering after losing a client process abruptly");
                });
        }

        private static Task RawPeerMidFrame(PhaseContext context, FaultEvent planned)
        {
            return context.Runner.RunAsync(Phase, planned.Kind, planned.ExpectedRecovery,
                async check =>
                {
                    check.Note("raw peer: " + RawPeer.TruncatedFrame(context.PipeName, 8192, 32));
                    check.Note("raw peer: " + RawPeer.SilentClose(context.PipeName));
                    context.Counters.ExpectedFailure();

                    for (int i = 0; i < 3; i++)
                    {
                        PipeMessage probe = await context.SendAsync(Ops.Echo, "after-raw-peer-" + i)
                            .ConfigureAwait(false);

                        check.That(probe.Ok, "request " + i + " after a raw peer failed");
                        context.Counters.Success();
                    }
                });
        }

        private static Task HandlerDelay(PhaseContext context, FaultEvent planned)
        {
            return context.Runner.RunAsync(Phase, planned.Kind, planned.ExpectedRecovery,
                async check =>
                {
                    System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
                    Exception? failure;

                    using (PipeClient impatient = context.NewClient(TimeSpan.FromMilliseconds(500)))
                    {
                        failure = await PhaseContext.CaptureAsync(() =>
                            impatient.SendAsync(Ops.Slow, "4000", context.Ct)).ConfigureAwait(false);
                    }

                    clock.Stop();

                    check.That(failure != null, "a request against a delayed handler reported success");
                    check.That(clock.Elapsed < TimeSpan.FromSeconds(15),
                        "the timeout took " + clock.ElapsedMilliseconds + "ms against a 500ms budget");

                    check.Note("timed out as " + failure!.GetType().Name + " after " + clock.ElapsedMilliseconds + "ms");
                    context.Counters.ExpectedFailure();

                    string marker = "after-delay-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                    PipeMessage next = await context.SendAsync(Ops.Echo, marker).ConfigureAwait(false);

                    check.That(next.Ok, "the request after a forced timeout failed");
                    check.Equal(marker, Payload.Text(next),
                        "the response after a forced timeout - it must not be the abandoned one");

                    context.Counters.Success();
                });
        }

        private static Task SlowSubscriber(PhaseContext context, FaultEvent planned)
        {
            return context.Runner.RunAsync(Phase, planned.Kind, planned.ExpectedRecovery,
                async check =>
                {
                    // Against the shared server's hub, which is configured with
                    // DropNewest: a subscriber that never reads must not stop
                    // the server from answering ordinary requests.
                    using (System.IO.Pipes.NamedPipeClientStream lazy =
                           new System.IO.Pipes.NamedPipeClientStream(
                               ".", Endpoint.EventsFor(context.PipeName), System.IO.Pipes.PipeDirection.InOut))
                    {
                        lazy.Connect(5000);

                        PipeMessage published = await context
                            .SendAsync(Ops.Publish, "300").ConfigureAwait(false);

                        check.That(published.Ok, "publishing past a slow subscriber failed: " +
                                                 Payload.Describe(published));

                        context.Counters.ExpectedFailure();

                        PipeMessage probe = await context.SendAsync(Ops.Echo, "during-slow-subscriber")
                            .ConfigureAwait(false);

                        check.That(probe.Ok, "the server stopped answering while a subscriber was stalled");
                        context.Counters.Success();
                    }

                    PipeMessage after = await context.SendAsync(Ops.Echo, "after-slow-subscriber")
                        .ConfigureAwait(false);

                    check.That(after.Ok, "the server did not recover after the slow subscriber left");
                    context.Counters.Success();

                    check.Note("a subscriber that never drained did not block publishing or ordinary requests");
                });
        }

        private static Task DisposeUnderLoad(PhaseContext context, FaultEvent planned)
        {
            return context.Runner.RunAsync(Phase, planned.Kind, planned.ExpectedRecovery,
                async check =>
                {
                    // A private endpoint: disposing the shared server here would
                    // take the rest of the schedule with it.
                    string endpoint = context.PipeName + ".dul";

                    using (PipeServer server = new PipeServer(new PipeServerOptions
                    {
                        PipeName = endpoint,
                        MaxClients = 8,
                        EnableEvents = true,
                        MaxEventSubscribers = 4,
                        MaxMessageBytes = 32 * 1024
                    }))
                    {
                        server.Map(Ops.Slow, async (message, ct) =>
                        {
                            await Task.Delay(400, ct).ConfigureAwait(false);
                            return new PipeMessage { Id = message.Id, Type = "response", Name = message.Name, Ok = true };
                        });

                        server.Start();

                        Task<Exception?> admitted = Task.Run(async () =>
                        {
                            using (PipeClient client = new PipeClient(new PipeClientOptions
                            {
                                PipeName = endpoint,
                                ConnectTimeout = TimeSpan.FromSeconds(5),
                                RequestTimeout = TimeSpan.FromSeconds(5)
                            }))
                            {
                                return await PhaseContext.CaptureAsync(() =>
                                    client.SendAsync(Ops.Slow, "400", context.Ct)).ConfigureAwait(false);
                            }
                        }, context.Ct);

                        // A subscriber is attached too, so disposal happens with
                        // a connection, a request and a subscriber all live.
                        using (PipeEventClient subscriber = new PipeEventClient(endpoint))
                        {
                            subscriber.AutoReconnect = false;
                            subscriber.Start();

                            await Task.Delay(200, context.Ct).ConfigureAwait(false);

                            Exception? disposal = await PhaseContext.CaptureAsync(() =>
                            {
                                server.Dispose();
                                return PhaseContext.CompletedTask;
                            }).ConfigureAwait(false);

                            check.That(disposal == null,
                                "disposing with a request and a subscriber live threw " +
                                (disposal == null ? string.Empty : disposal.GetType().Name));
                        }

                        Exception? outcome = await admitted.ConfigureAwait(false);
                        check.Note("the admitted request ended as " +
                                   (outcome == null ? "a completed response" : outcome.GetType().Name));

                        context.Counters.ExpectedFailure();
                    }

                    await CancellationMatrix.WaitUnbound(context, endpoint).ConfigureAwait(false);
                    check.That(!Endpoint.IsBound(endpoint), "the private endpoint stayed bound after disposal");

                    // The shared server is untouched by all of that.
                    PipeMessage probe = await context.SendAsync(Ops.Echo, "after-dispose-under-load")
                        .ConfigureAwait(false);

                    check.That(probe.Ok, "the shared server was disturbed by a private server's disposal");
                    context.Counters.Success();
                });
        }
    }
}
