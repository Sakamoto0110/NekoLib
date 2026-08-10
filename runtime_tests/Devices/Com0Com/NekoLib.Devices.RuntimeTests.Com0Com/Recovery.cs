#nullable enable
using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using NekoLib.Devices.Core.Abstractions;
using NekoLib.Devices.Core.Transport;
using NekoLib.RuntimeTests.Harness.Faults;

namespace NekoLib.Devices.RuntimeTests.Com0Com
{
    /// <summary>
    /// Dispatches the persisted seeded schedule and proves each fault's expected
    /// terminal and its recovery.
    /// <para/>
    /// Every fault is a switch on the scenario's own peer. Nothing here calls
    /// into <c>NekoLib.Devices</c> to make it misbehave, because a module with a
    /// fault switch is a module that ships a back door, and a scenario that
    /// exercised one would be testing the back door rather than the product.
    /// </summary>
    internal static class RecoveryMatrix
    {
        private const string Phase = Phases.Recovery;

        public static async Task RunAsync(PhaseContext context, FaultSchedule schedule, DateTime startedUtc)
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

                await context.ExclusiveAsync(() => DispatchAsync(context, planned)).ConfigureAwait(false);

                context.RefreshPeerSamples();
                context.Sampler.Take(Phase, "post-recovery");
            }
        }

        private static Task DispatchAsync(PhaseContext context, FaultEvent planned)
        {
            switch (planned.Kind)
            {
                case FaultKinds.PeerDelay: return Delay(context, planned);
                case FaultKinds.PeerSilence: return Silence(context, planned);
                case FaultKinds.PeerMalformed: return Malformed(context, planned);
                case FaultKinds.PeerDisconnect: return Disconnect(context, planned);
                case FaultKinds.PeerRestart: return Restart(context, planned);

                default:
                    context.Runner.Skip(Phase, planned.Kind, planned.ExpectedRecovery,
                        "no handler is registered for this fault kind");
                    return PhaseContext.CompletedTask;
            }
        }

        private static Task Delay(PhaseContext context, FaultEvent planned)
        {
            return context.Runner.RunAsync(Phase, planned.Kind, planned.ExpectedRecovery,
                async check =>
                {
                    context.ResetPeers();
                    context.PeerA.Delay(TimeSpan.FromSeconds(2));

                    using (Subject subject = await context.OpenAsync(context.PcbAPort).ConfigureAwait(false))
                    {
                        Stopwatch clock = Stopwatch.StartNew();

                        string late = PhaseContext.NewToken("fault-late-");
                        string? timedOut = await context
                            .ExchangeLineAsync(subject.Transport, PcbA.EchoRequest(late), 400)
                            .ConfigureAwait(false);

                        clock.Stop();

                        check.That(timedOut == null,
                            "a 400ms read against a 2s peer returned '" + PhaseContext.Flatten(timedOut) + "'");

                        check.That(clock.Elapsed < TimeSpan.FromSeconds(5),
                            "the read took " + clock.ElapsedMilliseconds + "ms against a 400ms budget");

                        check.Note("timed out after " + clock.ElapsedMilliseconds + "ms rather than waiting for the peer");
                        context.Counters.ExpectedFailure();

                        context.PeerA.Delay(TimeSpan.Zero);

                        // The late reply lands while the port is still open, so
                        // the reopen is what actually clears it.
                        await Task.Delay(2500, context.Ct).ConfigureAwait(false);
                        await subject.Transport.Close().ConfigureAwait(false);
                        await subject.Transport.Open(context.Ct).ConfigureAwait(false);

                        string fresh = PhaseContext.NewToken("fault-fresh-");
                        string? line = await context
                            .ExchangeLineAsync(subject.Transport, PcbA.EchoRequest(fresh), 3000)
                            .ConfigureAwait(false);

                        check.Equal("OK " + fresh + ";", line, "the response after the delay was cleared");
                        context.Counters.Success();

                        await subject.Transport.Close().ConfigureAwait(false);
                    }
                });
        }

        private static Task Silence(PhaseContext context, FaultEvent planned)
        {
            return context.Runner.RunAsync(Phase, planned.Kind, planned.ExpectedRecovery,
                async check =>
                {
                    context.ResetPeers();
                    context.PeerA.Silence(true);

                    using (Subject subject = await context.OpenAsync(context.PcbAPort).ConfigureAwait(false))
                    {
                        // Repeated, because the suite asks for repeated silence
                        // rather than one occurrence, and because a transport
                        // that recovers once and then wedges would pass a single
                        // pass.
                        for (int i = 0; i < 3; i++)
                        {
                            byte[]? nothing = await context
                                .ExchangeBytesAsync(subject.Transport, PcbA.Ping, 400)
                                .ConfigureAwait(false);

                            check.That(nothing == null,
                                "attempt " + i + " received " + PcbB.Hex(nothing) + " from a silent peer");

                            context.Counters.ExpectedFailure();
                        }

                        context.PeerA.Silence(false);

                        string token = PhaseContext.NewToken("after-silence-");
                        string? line = await context
                            .ExchangeLineAsync(subject.Transport, PcbA.EchoRequest(token), 3000)
                            .ConfigureAwait(false);

                        check.Equal("OK " + token + ";", line, "the response after the peer started answering again");
                        context.Counters.Success();

                        await subject.Transport.Close().ConfigureAwait(false);
                    }

                    check.Note("a silent peer produced the documented no-data result three times and then the " +
                               "same transport served an ordinary request");
                });
        }

        private static Task Malformed(PhaseContext context, FaultEvent planned)
        {
            return context.Runner.RunAsync(Phase, planned.Kind, planned.ExpectedRecovery,
                async check =>
                {
                    context.ResetPeers();
                    context.PeerB.Malform(true);

                    using (Subject subject = await context.OpenAsync(context.PcbBPort).ConfigureAwait(false))
                    {
                        const byte sequence = 0x71;

                        HardwareResponse corrupt = await context
                            .SendPcbBAsync(subject.Transport, sequence, 1500).ConfigureAwait(false);

                        // The transport's job is to deliver what was on the wire.
                        // Repairing or hiding a bad frame would be the real
                        // defect, so the assertion is that the bytes arrived and
                        // that this scenario's own validator rejected them.
                        check.That(corrupt.Success,
                            "the malformed frame did not reach the caller at all: " + corrupt.Status);

                        check.That(corrupt.RawBytes != null && corrupt.RawBytes.Length == PcbB.FrameLength,
                            "the malformed frame arrived as " + PcbB.Hex(corrupt.RawBytes));

                        string? rejection = PcbB.Validate(corrupt.RawBytes, sequence);
                        check.That(rejection != null,
                            "the deliberately corrupted frame passed validation: " + PcbB.Hex(corrupt.RawBytes));

                        check.Note("rejected as: " + rejection);
                        context.Counters.ExpectedFailure();

                        context.PeerB.Malform(false);

                        const byte next = 0x72;
                        HardwareResponse good = await context
                            .SendPcbBAsync(subject.Transport, next, 1500).ConfigureAwait(false);

                        check.That(good.Success, "the exchange after a malformed frame failed: " + good.Status);
                        check.Equal(null, PcbB.Validate(good.RawBytes, next),
                            "the frame after a malformed one (" + PcbB.Hex(good.RawBytes) + ")");

                        context.Counters.Success();
                        await subject.Transport.Close().ConfigureAwait(false);
                    }
                });
        }

        private static Task Disconnect(PhaseContext context, FaultEvent planned)
        {
            return context.Runner.RunAsync(Phase, planned.Kind, planned.ExpectedRecovery,
                async check =>
                {
                    context.ResetPeers();

                    using (Subject subject = await context.OpenAsync(context.PcbAPort).ConfigureAwait(false))
                    {
                        string token = PhaseContext.NewToken("before-loss-");
                        string? before = await context
                            .ExchangeLineAsync(subject.Transport, PcbA.EchoRequest(token), 2000)
                            .ConfigureAwait(false);

                        check.Equal("OK " + token + ";", before, "the response before the far end went away");
                        context.Counters.Success();

                        context.PeerA.Disconnect();
                        check.That(!context.PeerA.IsOpen, "the peer port did not close");
                        context.RefreshPeerSamples();

                        Stopwatch clock = Stopwatch.StartNew();

                        // The terminal is captured rather than prescribed. A pair
                        // whose far end has gone can surface as no data, as a
                        // write failure, or as neither, and which one it is
                        // depends on the virtual driver rather than on NekoLib.
                        // What must hold is that it ends, and ends quickly.
                        Exception? failure = null;
                        string? during = null;

                        failure = await PhaseContext.CaptureAsync(async () =>
                        {
                            during = await context
                                .ExchangeLineAsync(subject.Transport, PcbA.Ping, 800)
                                .ConfigureAwait(false);
                        }).ConfigureAwait(false);

                        clock.Stop();

                        check.That(clock.Elapsed < TimeSpan.FromSeconds(10),
                            "an exchange against a departed peer took " + clock.ElapsedMilliseconds + "ms");

                        check.That(failure != null || during == null,
                            "an exchange against a departed peer returned '" + PhaseContext.Flatten(during) + "'");

                        check.Note("the terminal while the far end was gone: " +
                                   (failure == null ? "no data, no exception" : failure.GetType().Name) +
                                   " after " + clock.ElapsedMilliseconds + "ms");

                        context.Counters.ExpectedFailure();

                        context.PeerA.Reconnect();
                        check.That(context.PeerA.IsOpen, "the peer port did not come back");
                        context.RefreshPeerSamples();

                        // The same endpoint, deliberately: reconnecting on a
                        // different port would prove nothing about recovery.
                        await subject.Transport.Close().ConfigureAwait(false);
                        await subject.Transport.Open(context.Ct).ConfigureAwait(false);
                        await context.DrainAsync(subject.Transport).ConfigureAwait(false);

                        check.Equal(context.PcbAPort, subject.Transport.PortName,
                            "the endpoint used for recovery");

                        string recovered = PhaseContext.NewToken("after-loss-");
                        string? after = await context
                            .ExchangeLineAsync(subject.Transport, PcbA.EchoRequest(recovered), 3000)
                            .ConfigureAwait(false);

                        check.Equal("OK " + recovered + ";", after,
                            "the response after the far end came back on the same pair");

                        context.Counters.Success();
                        await subject.Transport.Close().ConfigureAwait(false);
                    }
                });
        }

        private static Task Restart(PhaseContext context, FaultEvent planned)
        {
            return context.Runner.RunAsync(Phase, planned.Kind, planned.ExpectedRecovery,
                async check =>
                {
                    context.ResetPeers();
                    long before = context.PeerA.Restarts;

                    using (Subject subject = await context.OpenAsync(context.PcbAPort).ConfigureAwait(false))
                    {
                        // Two restarts, because "repeated" is what the suite
                        // asks for and because a handle leaked on the first one
                        // would stop the second from opening at all.
                        for (int i = 0; i < 2; i++)
                        {
                            context.PeerA.Disconnect();
                            await Task.Delay(200, context.Ct).ConfigureAwait(false);
                            context.PeerA.Reconnect();

                            check.That(context.PeerA.IsOpen, "the peer did not come back after restart " + i);

                            string token = PhaseContext.NewToken("restart" + i + "-");

                            // First without touching the caller's own port: if
                            // that works, the endpoint survived the far end
                            // restarting, which is the stronger outcome.
                            string? held = await context
                                .ExchangeLineAsync(subject.Transport, PcbA.EchoRequest(token), 1500)
                                .ConfigureAwait(false);

                            if (string.Equals(held, "OK " + token + ";", StringComparison.Ordinal))
                            {
                                check.Note("restart " + i + ": the caller's port kept working without reopening");
                                context.Counters.Success();
                                continue;
                            }

                            context.Counters.ExpectedFailure();

                            await subject.Transport.Close().ConfigureAwait(false);
                            await subject.Transport.Open(context.Ct).ConfigureAwait(false);
                            await context.DrainAsync(subject.Transport).ConfigureAwait(false);

                            string retry = PhaseContext.NewToken("restart" + i + "-retry-");
                            string? line = await context
                                .ExchangeLineAsync(subject.Transport, PcbA.EchoRequest(retry), 3000)
                                .ConfigureAwait(false);

                            check.Equal("OK " + retry + ";", line,
                                "the response after restart " + i + " and a reopen");

                            check.Note("restart " + i + ": the caller had to reopen its own port before the pair " +
                                       "served again");

                            context.Counters.Success();
                        }

                        await subject.Transport.Close().ConfigureAwait(false);
                    }

                    context.RefreshPeerSamples();
                    check.Equal(before + 2, context.PeerA.Restarts, "peer restarts recorded by this fault");
                });
        }
    }
}
