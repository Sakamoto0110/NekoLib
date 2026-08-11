#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Devices.Core.Abstractions;
using NekoLib.Devices.Core.Engine;
using NekoLib.Devices.Core.Protocols;
using NekoLib.Devices.Core.Transport;
using NekoLib.RuntimeTests.Harness.Reporting;

namespace NekoLib.Devices.RuntimeTests.Com0Com
{
    /// <summary>
    /// One open transport under test, with the sample column that says how many
    /// are open right now.
    /// <para/>
    /// Almost every check opens its own and closes it, because "repeated open,
    /// request, close and reopen cycles" is itself one of the workloads. Keeping
    /// a single long-lived transport would have made that impossible to
    /// exercise and would have hidden exactly the handle leak the soak looks
    /// for.
    /// </summary>
    internal sealed class Subject : IDisposable
    {
        private readonly ScenarioSamples _samples;
        private bool _counted = true;

        public Subject(SerialCommTransport transport, ScenarioSamples samples)
        {
            Transport = transport;
            _samples = samples;
        }

        public SerialCommTransport Transport { get; }

        /// <summary>
        /// Stops counting this transport as open without disposing it, for the
        /// one check that deliberately destroys its own subject.
        /// </summary>
        public void Release()
        {
            if (!_counted) return;
            _counted = false;
            _samples.SubjectTransportsAlive.Add(-1);
        }

        public void Dispose()
        {
            Release();
            try { Transport.Dispose(); } catch (Exception) { }
        }
    }

    /// <summary>What every phase needs, in one place.</summary>
    internal sealed class PhaseContext
    {
        public CheckRunner Runner = null!;
        public RunArtifacts Artifacts = null!;
        public WorkloadCounters Counters = null!;
        public ResourceSampler Sampler = null!;
        public ScenarioSamples Samples = null!;
        public OwnedPeer PeerA = null!;
        public OwnedPeer PeerB = null!;
        public string PcbAPort = string.Empty;
        public string PcbBPort = string.Empty;
        public int Seed;
        public CancellationToken Ct;

        /// <summary>
        /// Serialises assertions against faults, for the same reason every other
        /// Phase E scenario does: an assertion made while a scheduled fault has
        /// the peer silent is measuring the fault.
        /// </summary>
        public readonly SemaphoreSlim ExclusiveAccess = new SemaphoreSlim(1, 1);

        public async Task ExclusiveAsync(Func<Task> work)
        {
            await ExclusiveAccess.WaitAsync(Ct).ConfigureAwait(false);
            try { await work().ConfigureAwait(false); }
            finally { ExclusiveAccess.Release(); }
        }

        public static SerialConfig Config(
            string portName,
            int readTimeout = SerialPort.InfiniteTimeout,
            string newLine = PcbA.NewLine)
        {
            return new SerialConfig
            {
                BaudRate = 115200,
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                DtrEnable = true,
                RtsEnable = true,
                ReadTimeout = readTimeout,
                WriteTimeout = 2000,
                NewLine = newLine,
                PortName = portName
            };
        }

        /// <summary>
        /// Opens a fresh transport and discards whatever the pair still held.
        /// <para/>
        /// The drain is not housekeeping: a com0com pair keeps bytes the peer
        /// wrote while nobody was reading, so without it a check would inherit
        /// the previous one's late response and fail for a reason that has
        /// nothing to do with what it asserts.
        /// </summary>
        public async Task<Subject> OpenAsync(string portName, SerialConfig? config = null)
        {
            SerialCommTransport transport = new SerialCommTransport(portName);

            try
            {
                transport.Configure(config ?? Config(portName));
                await transport.Open(Ct).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // A failed open still leaves a SerialPort behind, and over a
                // four-hour soak a recurring transient failure would accumulate
                // exactly the handle growth this run exists to detect.
                try { transport.Dispose(); } catch (Exception) { }
                throw;
            }

            Samples.SubjectTransportsAlive.Increment();
            Subject subject = new Subject(transport, Samples);

            await DrainAsync(transport).ConfigureAwait(false);
            return subject;
        }

        public async Task DrainAsync(SerialCommTransport transport)
        {
            byte[]? leftover = await transport.ReadAll(80, 20, Ct).ConfigureAwait(false);
            if (leftover != null) Artifacts.Event("drain", json =>
            {
                json.Prop("port", transport.PortName);
                json.Prop("bytes", leftover.Length);
            });
        }

        /// <summary>Clears every armed fault and both peers' buffers.</summary>
        public void ResetPeers()
        {
            PeerA.Reset();
            PeerB.Reset();
            RefreshPeerSamples();
        }

        public void RefreshPeerSamples()
        {
            Samples.PeerResponses.Set(PeerA.Responses + PeerB.Responses);
            Samples.PeerBytesRead.Set(PeerA.BytesRead + PeerB.BytesRead);
            Samples.PeerRestarts.Set(PeerA.Restarts + PeerB.Restarts);
            Samples.PeerPortsOpen.Set((PeerA.IsOpen ? 1 : 0) + (PeerB.IsOpen ? 1 : 0));
        }

        /// <summary>Writes a PCB-A command and reads one line back.</summary>
        public async Task<string?> ExchangeLineAsync(
            SerialCommTransport transport,
            string command,
            int timeoutMilliseconds)
        {
            Samples.RequestsSent.Increment();
            byte[] frame = PcbA.Latin1.GetBytes(command);
            await transport.Write(frame, 0, frame.Length, Ct).ConfigureAwait(false);

            string? line = await transport.ReadLine(timeoutMilliseconds, Ct).ConfigureAwait(false);
            if (line == null) Samples.RequestsFailed.Increment();
            return line;
        }

        /// <summary>Writes a PCB-A command and reads every byte that arrives.</summary>
        public async Task<byte[]?> ExchangeBytesAsync(
            SerialCommTransport transport,
            string command,
            int timeoutMilliseconds,
            int quietMilliseconds = 50)
        {
            Samples.RequestsSent.Increment();
            byte[] frame = PcbA.Latin1.GetBytes(command);
            await transport.Write(frame, 0, frame.Length, Ct).ConfigureAwait(false);

            byte[]? bytes = await transport
                .ReadAll(timeoutMilliseconds, quietMilliseconds, Ct).ConfigureAwait(false);

            if (bytes == null) Samples.RequestsFailed.Increment();
            return bytes;
        }

        /// <summary>Runs one PCB-B exchange through a fresh engine.</summary>
        public Task<HardwareResponse> SendPcbBAsync(
            SerialCommTransport transport,
            byte sequence,
            int timeoutMilliseconds)
        {
            return SendPcbBAsync(NewEngine(transport), sequence, timeoutMilliseconds);
        }

        public async Task<HardwareResponse> SendPcbBAsync(
            HardwareEngine engine,
            byte sequence,
            int timeoutMilliseconds)
        {
            HardwareOperation operation = new HardwareOperation { Operation = "PING" };
            operation.Args["RawBytes"] = PcbB.EncodeRequest(sequence, PcbB.CommandPing);

            Samples.RequestsSent.Increment();
            HardwareResponse response = await engine
                .SendAsync(operation, timeoutMilliseconds, Ct).ConfigureAwait(false);

            if (!response.Success) Samples.RequestsFailed.Increment();
            return response;
        }

        public HardwareEngine NewEngine(SerialCommTransport transport) =>
            new HardwareEngine(transport, new ProtocolRaw(Config(transport.PortName)));

        /// <summary>A token that cannot collide with another exchange's.</summary>
        public static string NewToken(string prefix) =>
            prefix + Guid.NewGuid().ToString("N").Substring(0, 8);

        public static async Task<Exception?> CaptureAsync(Func<Task> call)
        {
            try { await call().ConfigureAwait(false); return null; }
            catch (Exception ex) { return ex; }
        }

        /// <summary>Awaits a task, or gives up, so no check can hang the run.</summary>
        public static async Task<bool> WithinAsync(Task work, TimeSpan bound)
        {
            Task completed = await Task.WhenAny(work, Task.Delay(bound)).ConfigureAwait(false);
            return ReferenceEquals(completed, work);
        }

        public static string Flatten(string? text) =>
            (text ?? string.Empty).Replace("\r", "\\r").Replace("\n", "\\n").Trim();

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
    /// Open/close lifetime, timeouts, cancellation, endpoint switching,
    /// serialization, and what a late response can and cannot do.
    /// </summary>
    internal static class TransportMatrix
    {
        private const string Phase = Phases.Transport;

        public static async Task RunAsync(PhaseContext context)
        {
            await OpenCloseReopenCycles(context).ConfigureAwait(false);
            await FiniteTimeoutThenSuccess(context).ConfigureAwait(false);
            await CancellationThenSuccess(context).ConfigureAwait(false);
            await EndpointSwitching(context).ConfigureAwait(false);
            await ConcurrentCallersAreSerialized(context).ConfigureAwait(false);
            await LateResponseAfterReopen(context).ConfigureAwait(false);
            await LateResponseWithoutReopen(context).ConfigureAwait(false);
        }

        private static Task OpenCloseReopenCycles(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "open-request-close-reopen-cycles",
                "repeated open, request, close and reopen cycles all serve, and Close really closes",
                async check =>
                {
                    const int cycles = 8;
                    context.ResetPeers();

                    using (Subject subject = await context.OpenAsync(context.PcbAPort).ConfigureAwait(false))
                    {
                        for (int i = 0; i < cycles; i++)
                        {
                            string token = PhaseContext.NewToken("cycle" + i + "-");
                            string? line = await context
                                .ExchangeLineAsync(subject.Transport, PcbA.EchoRequest(token), 1500)
                                .ConfigureAwait(false);

                            check.Equal("OK " + token + ";", line, "the echo response on cycle " + i);
                            context.Counters.Success();

                            await subject.Transport.Close().ConfigureAwait(false);
                            check.That(!subject.Transport.IsOpen, "the transport stayed open after Close on cycle " + i);

                            await subject.Transport.Open(context.Ct).ConfigureAwait(false);
                            check.That(subject.Transport.IsOpen, "the transport did not reopen on cycle " + i);
                        }

                        await subject.Transport.Close().ConfigureAwait(false);
                    }

                    check.Note(cycles + " open/request/close/reopen cycles on the same transport instance");
                });
        }

        private static Task FiniteTimeoutThenSuccess(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "finite-timeout-then-success",
                "every read returns its documented no-data result inside its bound, and the next request succeeds",
                async check =>
                {
                    context.ResetPeers();
                    context.PeerA.Silence(true);

                    using (Subject subject = await context.OpenAsync(context.PcbAPort).ConfigureAwait(false))
                    {
                        Stopwatch clock = Stopwatch.StartNew();

                        await subject.Transport
                            .Write(PcbA.Latin1.GetBytes(PcbA.Ping), 0, PcbA.Ping.Length, context.Ct)
                            .ConfigureAwait(false);

                        string? line = await subject.Transport.ReadLine(400, context.Ct).ConfigureAwait(false);
                        byte[]? exact = await subject.Transport.ReadExact(4, 400, context.Ct).ConfigureAwait(false);
                        byte[]? all = await subject.Transport.ReadAll(400, 50, context.Ct).ConfigureAwait(false);

                        clock.Stop();

                        check.That(line == null, "ReadLine returned '" + PhaseContext.Flatten(line) +
                                                 "' from a peer that answered nothing");
                        check.That(exact == null, "ReadExact returned bytes from a peer that answered nothing");
                        check.That(all == null, "ReadAll returned bytes from a peer that answered nothing");

                        check.That(clock.Elapsed < TimeSpan.FromSeconds(6),
                            "three 400ms reads took " + clock.ElapsedMilliseconds + "ms in total");

                        context.Counters.ExpectedFailure();

                        context.PeerA.Silence(false);

                        string token = PhaseContext.NewToken("after-timeout-");
                        string? next = await context
                            .ExchangeLineAsync(subject.Transport, PcbA.EchoRequest(token), 1500)
                            .ConfigureAwait(false);

                        check.Equal("OK " + token + ";", next, "the response after three timed-out reads");
                        context.Counters.Success();

                        await subject.Transport.Close().ConfigureAwait(false);
                    }

                    check.Note("timeouts returned null rather than throwing, which is the transport's documented " +
                               "no-data contract");
                });
        }

        private static Task CancellationThenSuccess(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "cancellation-then-success",
                "a pending read observes cancellation under both infinite and finite configured port timeouts",
                async check =>
                {
                    context.ResetPeers();
                    context.PeerA.Silence(true);

                    int[] configured = { SerialPort.InfiniteTimeout, 50 };

                    foreach (int readTimeout in configured)
                    {
                        SerialConfig config = PhaseContext.Config(context.PcbAPort, readTimeout);

                        using (Subject subject = await context.OpenAsync(context.PcbAPort, config)
                                   .ConfigureAwait(false))
                        {
                            check.Equal(readTimeout, subject.Transport.PortInfo.ReadTimeout,
                                "the configured port read timeout");

                            using (CancellationTokenSource cancellation = new CancellationTokenSource(150))
                            {
                                Stopwatch clock = Stopwatch.StartNew();

                                Exception? failure = await PhaseContext.CaptureAsync(() =>
                                    subject.Transport.ReadLine(8000, cancellation.Token)).ConfigureAwait(false);

                                clock.Stop();

                                check.That(failure is OperationCanceledException,
                                    "cancellation with a configured timeout of " + readTimeout +
                                    " surfaced as " + (failure == null ? "success" : failure.GetType().Name));

                                check.That(clock.Elapsed < TimeSpan.FromSeconds(5),
                                    "cancellation took " + clock.ElapsedMilliseconds +
                                    "ms against an 8000ms method timeout");

                                context.Counters.Cancellation();
                            }

                            context.PeerA.Silence(false);

                            string token = PhaseContext.NewToken("after-cancel-");
                            string? next = await context
                                .ExchangeLineAsync(subject.Transport, PcbA.EchoRequest(token), 1500)
                                .ConfigureAwait(false);

                            check.Equal("OK " + token + ";", next,
                                "the response after a cancelled read with a configured timeout of " + readTimeout);

                            context.Counters.Success();
                            context.PeerA.Silence(true);

                            await subject.Transport.Close().ConfigureAwait(false);
                        }
                    }

                    context.PeerA.Silence(false);
                    check.Note("the method timeout and the token stay authoritative whether the configured " +
                               "SerialPort timeout is infinite or finite");
                });
        }

        private static Task EndpointSwitching(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "endpoint-switching-allow-and-reject",
                "switching endpoints is refused while open and accepted after close, as documented",
                async check =>
                {
                    context.ResetPeers();

                    using (Subject subject = await context.OpenAsync(context.PcbAPort).ConfigureAwait(false))
                    {
                        SerialCommTransport transport = subject.Transport;

                        Exception? reopen = await PhaseContext.CaptureAsync(() =>
                            transport.Open(context.PcbBPort, context.Ct)).ConfigureAwait(false);

                        check.That(reopen is InvalidOperationException,
                            "Open(otherPort) while open surfaced as " +
                            (reopen == null ? "success" : reopen.GetType().Name));

                        Exception? reconfigure = await PhaseContext.CaptureAsync(() =>
                        {
                            transport.Configure(PhaseContext.Config(context.PcbBPort));
                            return PhaseContext.CompletedTask;
                        }).ConfigureAwait(false);

                        check.That(reconfigure is InvalidOperationException,
                            "Configure(otherPort) while open surfaced as " +
                            (reconfigure == null ? "success" : reconfigure.GetType().Name));

                        Exception? rename = await PhaseContext.CaptureAsync(() =>
                        {
                            transport.PortName = context.PcbBPort;
                            return PhaseContext.CompletedTask;
                        }).ConfigureAwait(false);

                        check.That(rename is InvalidOperationException,
                            "the PortName setter while open surfaced as " +
                            (rename == null ? "success" : rename.GetType().Name));

                        check.Equal(context.PcbAPort, transport.PortName,
                            "the endpoint after three rejected switches");

                        context.Counters.ExpectedFailure();

                        // Still usable: a refused switch must not have disturbed
                        // the endpoint that was already open.
                        string token = PhaseContext.NewToken("after-reject-");
                        string? line = await context
                            .ExchangeLineAsync(transport, PcbA.EchoRequest(token), 1500).ConfigureAwait(false);

                        check.Equal("OK " + token + ";", line, "the response after three rejected switches");
                        context.Counters.Success();

                        await transport.Close().ConfigureAwait(false);

                        // After Close the same instance may take the other pair.
                        Exception? switched = await PhaseContext.CaptureAsync(() =>
                            transport.Open(context.PcbBPort, context.Ct)).ConfigureAwait(false);

                        check.That(switched == null,
                            "Open(otherPort) after Close surfaced as " +
                            (switched == null ? "success" : switched.GetType().Name));

                        check.Equal(context.PcbBPort, transport.PortName, "the endpoint after a permitted switch");

                        await context.DrainAsync(transport).ConfigureAwait(false);

                        byte sequence = 0x51;
                        HardwareResponse response = await context
                            .SendPcbBAsync(transport, sequence, 1500).ConfigureAwait(false);

                        check.That(response.Success, "the switched endpoint did not answer: " + response.Status);
                        check.Equal(null, PcbB.Validate(response.RawBytes, sequence),
                            "the frame from the switched endpoint");

                        context.Counters.Success();

                        await transport.Close().ConfigureAwait(false);
                    }

                    check.Note("the transport refuses a switch while open through all three entry points - " +
                               "Open(port), Configure(cfg) and the PortName setter - and accepts one after Close");
                });
        }

        private static Task ConcurrentCallersAreSerialized(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "concurrent-callers-are-serialized",
                "concurrent engine operations each receive their own frame and never another caller's",
                async check =>
                {
                    const int callers = 6;
                    context.ResetPeers();

                    using (Subject subject = await context.OpenAsync(context.PcbBPort).ConfigureAwait(false))
                    {
                        // One engine, deliberately: the serialization being
                        // proved is HardwareEngine's operation gate, which is
                        // what makes a whole request/response pair atomic.
                        HardwareEngine engine = context.NewEngine(subject.Transport);
                        Task<string?>[] running = new Task<string?>[callers];

                        for (int i = 0; i < callers; i++)
                        {
                            byte sequence = (byte)(0x60 + i);
                            running[i] = Task.Run(async () =>
                            {
                                HardwareResponse response = await context
                                    .SendPcbBAsync(engine, sequence, 3000).ConfigureAwait(false);

                                if (!response.Success) return "operation failed: " + response.Status;
                                return PcbB.Validate(response.RawBytes, sequence);
                            }, context.Ct);
                        }

                        string?[] problems = await Task.WhenAll(running).ConfigureAwait(false);

                        int wrong = 0;
                        for (int i = 0; i < problems.Length; i++)
                        {
                            if (problems[i] == null) { context.Counters.Success(); continue; }

                            wrong++;
                            check.Note("caller " + i + ": " + problems[i]);
                        }

                        check.Equal(0, wrong, "concurrent callers that received the wrong frame or none");

                        await subject.Transport.Close().ConfigureAwait(false);
                    }

                    check.Note(callers + " concurrent HardwareEngine operations, each identified by its own " +
                               "sequence byte, completed without crossing. The transport's own gate serialises " +
                               "individual calls, not request/response pairs: a caller composing Write and Read " +
                               "itself still owns that pairing");
                });
        }

        private static Task LateResponseAfterReopen(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "late-response-does-not-survive-a-reopen",
                "a response abandoned by a timed-out read is gone after the port is reopened",
                async check =>
                {
                    context.ResetPeers();
                    context.PeerA.Delay(TimeSpan.FromMilliseconds(900));

                    string abandoned = PhaseContext.NewToken("abandoned-");

                    using (Subject subject = await context.OpenAsync(context.PcbAPort).ConfigureAwait(false))
                    {
                        string? timedOut = await context
                            .ExchangeLineAsync(subject.Transport, PcbA.EchoRequest(abandoned), 250)
                            .ConfigureAwait(false);

                        check.That(timedOut == null,
                            "a 250ms read against a 900ms peer returned '" + PhaseContext.Flatten(timedOut) + "'");

                        context.Counters.ExpectedFailure();

                        context.PeerA.Delay(TimeSpan.Zero);

                        // Waited for with the port still open, so the abandoned
                        // response really is sitting in the driver's buffer when
                        // the reopen happens. Closing first would have let
                        // com0com discard it and the check would have proved
                        // nothing.
                        await Task.Delay(1200, context.Ct).ConfigureAwait(false);

                        await subject.Transport.Close().ConfigureAwait(false);
                        await subject.Transport.Open(context.Ct).ConfigureAwait(false);

                        string fresh = PhaseContext.NewToken("fresh-");
                        string? line = await context
                            .ExchangeLineAsync(subject.Transport, PcbA.EchoRequest(fresh), 2000)
                            .ConfigureAwait(false);

                        check.Equal("OK " + fresh + ";", line, "the response after a reopen");
                        check.That(line == null || line.IndexOf(abandoned, StringComparison.Ordinal) < 0,
                            "the abandoned response survived a close and reopen");

                        context.Counters.Success();
                        await subject.Transport.Close().ConfigureAwait(false);
                    }
                });
        }

        private static Task LateResponseWithoutReopen(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "late-response-stays-attributable",
                "without a reopen the late bytes are still intact and attributable, never mixed into one frame",
                async check =>
                {
                    // The honest form of "a timed-out operation's response is not
                    // consumed by the next one". A serial port is a byte stream
                    // with no correlation of its own, so the late bytes are
                    // simply still there; asserting they vanish would be
                    // asserting a contract the transport does not offer. What can
                    // be asserted is that nothing is corrupted or interleaved,
                    // and that every byte belongs to one identifiable exchange.
                    context.ResetPeers();
                    context.PeerA.Delay(TimeSpan.FromMilliseconds(700));

                    string abandoned = PhaseContext.NewToken("late-");
                    string fresh = PhaseContext.NewToken("next-");

                    using (Subject subject = await context.OpenAsync(context.PcbAPort).ConfigureAwait(false))
                    {
                        string? timedOut = await context
                            .ExchangeLineAsync(subject.Transport, PcbA.EchoRequest(abandoned), 200)
                            .ConfigureAwait(false);

                        check.That(timedOut == null, "the deliberately short read did not time out");
                        context.Counters.ExpectedFailure();

                        context.PeerA.Delay(TimeSpan.Zero);
                        await Task.Delay(900, context.Ct).ConfigureAwait(false);

                        byte[]? bytes = await context
                            .ExchangeBytesAsync(subject.Transport, PcbA.EchoRequest(fresh), 2000)
                            .ConfigureAwait(false);

                        check.That(bytes != null, "the next exchange received nothing at all");

                        string text = PcbA.Latin1.GetString(bytes!);
                        string lateResponse = PcbA.EchoResponse(abandoned);
                        string freshResponse = PcbA.EchoResponse(fresh);

                        bool onlyFresh = string.Equals(text, freshResponse, StringComparison.Ordinal);
                        bool onlyLate = string.Equals(text, lateResponse, StringComparison.Ordinal);
                        bool bothInOrder = string.Equals(text, lateResponse + freshResponse, StringComparison.Ordinal);

                        check.That(onlyFresh || onlyLate || bothInOrder,
                            "the next read returned bytes belonging to neither exchange intact: " +
                            PhaseContext.Flatten(text));

                        check.Note(onlyFresh
                            ? "the late response was no longer buffered; the next read saw only its own"
                            : onlyLate
                                ? "the next read consumed the late response and its own had not arrived yet"
                                : "the next read saw both responses, in order, each intact");

                        context.Counters.Success();
                        await subject.Transport.Close().ConfigureAwait(false);
                    }
                });
        }
    }

    /// <summary>
    /// Both wire protocols, chunked delivery, and configuration parity.
    /// </summary>
    internal static class ProtocolMatrix
    {
        private const string Phase = Phases.Protocol;

        public static async Task RunAsync(PhaseContext context)
        {
            await TextFraming(context).ConfigureAwait(false);
            await BinaryFraming(context).ConfigureAwait(false);
            await ChunksWithinTheQuietPeriod(context).ConfigureAwait(false);
            await ChunksBeyondTheQuietPeriod(context).ConfigureAwait(false);
            await ConfigurationParity(context).ConfigureAwait(false);
            await EncodingParity(context).ConfigureAwait(false);
        }

        private static Task TextFraming(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "pcb-a-text-framing",
                "ReadLine, ReadExact and ReadAll each return the text response the way they document",
                async check =>
                {
                    context.ResetPeers();

                    using (Subject subject = await context.OpenAsync(context.PcbAPort).ConfigureAwait(false))
                    {
                        SerialCommTransport transport = subject.Transport;

                        string? line = await context
                            .ExchangeLineAsync(transport, PcbA.Ping, 1500).ConfigureAwait(false);

                        check.Equal("OK PONG;", line, "ReadLine strips the terminator and returns the frame");
                        context.Counters.Success();

                        context.Samples.RequestsSent.Increment();
                        byte[] ping = PcbA.Latin1.GetBytes(PcbA.Ping);
                        await transport.Write(ping, 0, ping.Length, context.Ct).ConfigureAwait(false);

                        byte[]? exact = await transport
                            .ReadExact(PcbA.Pong.Length, 1500, context.Ct).ConfigureAwait(false);

                        check.That(exact != null, "ReadExact returned nothing for a " + PcbA.Pong.Length + "-byte reply");
                        check.Equal(PcbA.Pong, PcbA.Latin1.GetString(exact!),
                            "ReadExact preserves the terminator and every byte");
                        context.Counters.Success();

                        byte[]? all = await context
                            .ExchangeBytesAsync(transport, PcbA.Identify, 1500).ConfigureAwait(false);

                        check.That(all != null, "ReadAll returned nothing for the identify reply");
                        check.Equal(PcbA.Identity, PcbA.Latin1.GetString(all!),
                            "ReadAll returns the whole reply once the line goes quiet");
                        context.Counters.Success();

                        await transport.Close().ConfigureAwait(false);
                    }
                });
        }

        private static Task BinaryFraming(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "pcb-b-binary-framing-and-crc",
                "a binary exchange returns its own sequence, the PONG opcode and a valid CRC-16/CCITT-FALSE",
                async check =>
                {
                    context.ResetPeers();

                    using (Subject subject = await context.OpenAsync(context.PcbBPort).ConfigureAwait(false))
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            byte sequence = (byte)(0x2A + i);
                            HardwareResponse response = await context
                                .SendPcbBAsync(subject.Transport, sequence, 1500).ConfigureAwait(false);

                            check.That(response.Success, "the binary exchange failed: " + response.Status);
                            check.Equal(null, PcbB.Validate(response.RawBytes, sequence),
                                "frame " + i + " (" + PcbB.Hex(response.RawBytes) + ")");

                            context.Counters.Success();
                        }

                        await subject.Transport.Close().ConfigureAwait(false);
                    }

                    check.Note("four exchanges, each with its own sequence byte, all validated independently of " +
                               "the code that produced them");
                });
        }

        private static Task ChunksWithinTheQuietPeriod(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "partial-chunks-inside-the-quiet-period-are-reassembled",
                "a reply delivered in pieces closer together than the quiet period arrives whole",
                async check =>
                {
                    context.ResetPeers();
                    context.PeerA.Chunk(3, TimeSpan.FromMilliseconds(10));

                    using (Subject subject = await context.OpenAsync(context.PcbAPort).ConfigureAwait(false))
                    {
                        string token = PhaseContext.NewToken("chunked-");
                        byte[]? bytes = await context
                            .ExchangeBytesAsync(subject.Transport, PcbA.EchoRequest(token), 3000, 50)
                            .ConfigureAwait(false);

                        check.That(bytes != null, "a chunked reply produced no bytes at all");
                        check.Equal(PcbA.EchoResponse(token), PcbA.Latin1.GetString(bytes!),
                            "the reassembled reply");

                        context.Counters.Success();
                        await subject.Transport.Close().ConfigureAwait(false);
                    }

                    context.PeerA.Chunk(0, TimeSpan.Zero);
                    check.Note("3-byte pieces 10ms apart against a 50ms quiet period");
                });
        }

        private static Task ChunksBeyondTheQuietPeriod(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "a-gap-beyond-the-quiet-period-ends-the-read",
                "ReadAll returns what it has when the line goes quiet, which is a prefix rather than a truncation",
                async check =>
                {
                    // The documented contract is "until a quiet period or timeout
                    // occurs", so a slow peer ending a read early is correct
                    // behaviour rather than a defect. It is asserted because it
                    // is what a caller has to design around, and because the
                    // remainder is exactly the stale-byte problem the reopen
                    // check exists for.
                    const int chunkBytes = 3;
                    const int chunkGapMilliseconds = 300;

                    context.ResetPeers();
                    context.PeerA.Chunk(chunkBytes, TimeSpan.FromMilliseconds(chunkGapMilliseconds));

                    string token = PhaseContext.NewToken("slow-");
                    string whole = PcbA.EchoResponse(token);

                    using (Subject subject = await context.OpenAsync(context.PcbAPort).ConfigureAwait(false))
                    {
                        byte[]? first = await context
                            .ExchangeBytesAsync(subject.Transport, PcbA.EchoRequest(token), 3000, 50)
                            .ConfigureAwait(false);

                        check.That(first != null, "the first read of a slow reply produced nothing");
                        string prefix = PcbA.Latin1.GetString(first!);

                        check.That(prefix.Length < whole.Length,
                            "a 300ms gap against a 50ms quiet period still returned the whole " +
                            whole.Length + "-byte reply");

                        check.That(whole.StartsWith(prefix, StringComparison.Ordinal),
                            "what arrived first was not a prefix of the reply: " + PhaseContext.Flatten(prefix));

                        context.Counters.ExpectedFailure();
                        check.Note("read returned " + prefix.Length + " of " + whole.Length + " bytes");

                        context.PeerA.Chunk(0, TimeSpan.Zero);

                        // The rest of the slow reply is still on its way, so it
                        // is waited out with the port open and then thrown away
                        // by the reopen. That is the recovery a caller has to
                        // perform, and the check performs it rather than
                        // pretending the remainder does not exist.
                        int chunkGapCount = (whole.Length - 1) / chunkBytes;
                        int settleMilliseconds = (chunkGapCount + 1) * chunkGapMilliseconds;
                        await Task.Delay(settleMilliseconds, context.Ct).ConfigureAwait(false);

                        await subject.Transport.Close().ConfigureAwait(false);
                        await subject.Transport.Open(context.Ct).ConfigureAwait(false);
                        await context.DrainAsync(subject.Transport).ConfigureAwait(false);

                        string fresh = PhaseContext.NewToken("after-slow-");
                        string? line = await context
                            .ExchangeLineAsync(subject.Transport, PcbA.EchoRequest(fresh), 3000)
                            .ConfigureAwait(false);

                        check.Equal("OK " + fresh + ";", line, "the response after a reopen");
                        context.Counters.Success();

                        await subject.Transport.Close().ConfigureAwait(false);
                    }
                });
        }

        private static Task ConfigurationParity(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "configuration-parity",
                "Configure applies and reports every serial field, including RTS/CTS combinations",
                async check =>
                {
                    context.ResetPeers();

                    // Applied and read back without opening, deliberately: a
                    // handshake nobody is asserting on the far end can block a
                    // write for as long as the write timeout, and a check that
                    // can hang is worse than one that proves slightly less. One
                    // configuration below is opened and used as well.
                    SerialConfig[] applied =
                    {
                        Field(context.PcbAPort, 9600, 7, StopBits.Two, Parity.Even, Handshake.None, false, false),
                        Field(context.PcbAPort, 19200, 8, StopBits.One, Parity.Odd, Handshake.None, true, false),
                        Field(context.PcbAPort, 115200, 8, StopBits.One, Parity.None, Handshake.XOnXOff, false, true),
                        Field(context.PcbAPort, 57600, 5, StopBits.OnePointFive, Parity.Mark, Handshake.None, true, true)
                    };

                    foreach (SerialConfig config in applied)
                    {
                        using (SerialCommTransport transport = new SerialCommTransport(context.PcbAPort))
                        {
                            transport.Configure(config);
                            SerialConfig readBack = transport.PortInfo;

                            check.Equal(config.BaudRate, readBack.BaudRate, "baud rate");
                            check.Equal(config.DataBits, readBack.DataBits, "data bits");
                            check.Equal((long)config.StopBits, (long)readBack.StopBits, "stop bits");
                            check.Equal((long)config.Parity, (long)readBack.Parity, "parity");
                            check.Equal((long)config.Handshake, (long)readBack.Handshake, "handshake");
                            check.That(config.DtrEnable == readBack.DtrEnable, "DTR was not applied");
                            check.That(config.RtsEnable == readBack.RtsEnable, "RTS was not applied");
                            check.Equal(config.NewLine, readBack.NewLine, "newline");
                            check.Equal(config.ReadTimeout, readBack.ReadTimeout, "read timeout");
                            check.Equal(config.WriteTimeout, readBack.WriteTimeout, "write timeout");
                        }
                    }

                    context.Counters.Success();

                    // The first runtime execution refuted the build-time
                    // assumption that these combinations would be rejected.
                    // Assert the observed contract directly on both targets:
                    // Configure accepts the snapshot and PortInfo reports the
                    // same handshake and RTS values while the port is closed.
                    Handshake[] rtsHandshakes = { Handshake.RequestToSend, Handshake.RequestToSendXOnXOff };
                    bool[] rtsValues = { true, false };

                    foreach (Handshake handshake in rtsHandshakes)
                    {
                        foreach (bool rts in rtsValues)
                        {
                            using (SerialCommTransport transport = new SerialCommTransport(context.PcbAPort))
                            {
                                SerialConfig rtsConfig = Field(
                                    context.PcbAPort, 115200, 8, StopBits.One, Parity.None, handshake, true, rts);

                                transport.Configure(rtsConfig);
                                SerialConfig readBack = transport.PortInfo;

                                check.Equal((long)handshake, (long)readBack.Handshake,
                                    handshake + " handshake read-back with RtsEnable=" + rts);
                                check.That(readBack.RtsEnable == rts,
                                    handshake + " changed RtsEnable=" + rts + " during read-back");
                            }
                        }
                    }

                    check.Note("RequestToSend and RequestToSendXOnXOff accepted both RtsEnable values and " +
                               "reported them unchanged. This is configuration-snapshot evidence only; the " +
                               "scenario does not claim that a virtual peer enforces hardware flow control");

                    // One configuration is also opened and used, so the matrix is
                    // not only about fields being copied onto an object.
                    SerialConfig live = Field(
                        context.PcbAPort, 9600, 8, StopBits.One, Parity.None, Handshake.None, true, true);

                    using (Subject subject = await context.OpenAsync(context.PcbAPort, live).ConfigureAwait(false))
                    {
                        SerialConfig open = subject.Transport.PortInfo;
                        check.Equal(9600, open.BaudRate, "the baud rate of an open port");

                        string token = PhaseContext.NewToken("cfg-");
                        string? line = await context
                            .ExchangeLineAsync(subject.Transport, PcbA.EchoRequest(token), 2000)
                            .ConfigureAwait(false);

                        check.Equal("OK " + token + ";", line,
                            "a round trip with the subject at 9600 and the peer still at 115200");

                        context.Counters.Success();
                        await subject.Transport.Close().ConfigureAwait(false);
                    }

                    check.Note("the round trip above succeeded with the two ends configured at different line " +
                               "rates, which is the honest limit of this check: com0com is a virtual pair and " +
                               "does not emulate baud, framing or electrical behaviour. What is proved is that " +
                               "Configure applies and reports every field, not that any of them reached a UART");
                });
        }

        private static SerialConfig Field(
            string portName,
            int baud,
            int dataBits,
            StopBits stopBits,
            Parity parity,
            Handshake handshake,
            bool dtr,
            bool rts)
        {
            return new SerialConfig
            {
                BaudRate = baud,
                DataBits = dataBits,
                StopBits = stopBits,
                Parity = parity,
                Handshake = handshake,
                DtrEnable = dtr,
                RtsEnable = rts,
                ReadTimeout = SerialPort.InfiniteTimeout,
                WriteTimeout = 2000,
                NewLine = PcbA.NewLine,
                PortName = portName
            };
        }

        private static Task EncodingParity(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "encoding-parity",
                "Write(string) is ASCII by contract, and a byte payload carries any single-byte encoding intact",
                async check =>
                {
                    context.ResetPeers();

                    using (Subject subject = await context.OpenAsync(context.PcbAPort).ConfigureAwait(false))
                    {
                        SerialCommTransport transport = subject.Transport;

                        // A Latin-1 token, sent as bytes, must come back byte for
                        // byte. This is the supported way to speak a non-ASCII
                        // single-byte encoding.
                        string latin = "AC" + (char)0xC7 + (char)0xE3 + "O";
                        string request = PcbA.EchoRequest(latin);

                        context.Samples.RequestsSent.Increment();
                        byte[] frame = PcbA.Latin1.GetBytes(request);
                        await transport.Write(frame, 0, frame.Length, context.Ct).ConfigureAwait(false);

                        byte[]? bytes = await transport.ReadAll(2000, 50, context.Ct).ConfigureAwait(false);
                        check.That(bytes != null, "the Latin-1 echo produced no bytes");
                        check.Equal(PcbA.EchoResponse(latin), PcbA.Latin1.GetString(bytes!),
                            "a Latin-1 payload sent as bytes");

                        context.Counters.Success();

                        // The same token through Write(string) must not survive:
                        // that overload is documented ASCII, so the two non-ASCII
                        // characters become '?' on the wire and the peer echoes
                        // the substitution back.
                        context.Samples.RequestsSent.Increment();
                        await transport.Write(request, context.Ct).ConfigureAwait(false);

                        byte[]? viaAscii = await transport.ReadAll(2000, 50, context.Ct).ConfigureAwait(false);
                        check.That(viaAscii != null, "the ASCII-coerced echo produced no bytes");

                        string coerced = PcbA.Latin1.GetString(viaAscii!);
                        check.Equal(PcbA.EchoResponse("AC??O"), coerced,
                            "Write(string) coerces to ASCII, replacing what it cannot represent");

                        context.Counters.ExpectedFailure();
                        await transport.Close().ConfigureAwait(false);
                    }

                    check.Note("Write(string) uses Encoding.ASCII unconditionally; this is the documented " +
                               "behaviour rather than a finding, and the byte overload is the way to send " +
                               "anything else");
                });
        }
    }

    /// <summary>Disposal under load, idempotence, and use after disposal.</summary>
    internal static class LifecycleMatrix
    {
        private const string Phase = Phases.Lifecycle;

        public static Task RunAsync(PhaseContext context) => DisposeUnderLoad(context);

        private static Task DisposeUnderLoad(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "dispose-during-an-active-operation",
                "disposal with a read in flight returns, ends that read, is idempotent, and refuses later use",
                async check =>
                {
                    context.ResetPeers();
                    context.PeerA.Silence(true);

                    Subject subject = await context.OpenAsync(context.PcbAPort).ConfigureAwait(false);
                    SerialCommTransport transport = subject.Transport;

                    // A read that will not finish on its own inside the window.
                    Task<Exception?> pending = Task.Run(() => PhaseContext.CaptureAsync(() =>
                        transport.ReadAll(20000, 200, CancellationToken.None)), context.Ct);

                    await Task.Delay(300, context.Ct).ConfigureAwait(false);

                    subject.Release();
                    Task disposal = Task.Run(() => transport.Dispose());

                    check.That(await PhaseContext.WithinAsync(disposal, TimeSpan.FromSeconds(15)).ConfigureAwait(false),
                        "Dispose did not return within 15s while a read was in flight");

                    check.That(await PhaseContext.WithinAsync(pending, TimeSpan.FromSeconds(15)).ConfigureAwait(false),
                        "the in-flight read had not ended 15s after disposal");

                    if (pending.IsCompleted)
                    {
                        Exception? outcome = await pending.ConfigureAwait(false);
                        check.Note("the in-flight read ended as " +
                                   (outcome == null ? "a completed result" : outcome.GetType().Name));
                    }

                    context.Counters.ExpectedFailure();

                    Exception? again = await PhaseContext.CaptureAsync(() =>
                    {
                        transport.Dispose();
                        return PhaseContext.CompletedTask;
                    }).ConfigureAwait(false);

                    check.That(again == null,
                        "a second Dispose threw " + (again == null ? string.Empty : again.GetType().Name));

                    Exception? close = await PhaseContext.CaptureAsync(() => transport.Close()).ConfigureAwait(false);
                    check.That(close is ObjectDisposedException,
                        "Close after Dispose surfaced as " + (close == null ? "success" : close.GetType().Name));

                    Exception? reopen = await PhaseContext.CaptureAsync(() =>
                        transport.Open(context.Ct)).ConfigureAwait(false);
                    check.That(reopen is ObjectDisposedException,
                        "Open after Dispose surfaced as " + (reopen == null ? "success" : reopen.GetType().Name));

                    context.PeerA.Silence(false);

                    // The endpoint itself must be free again, which is what makes
                    // the next cycle possible at all.
                    using (Subject replacement = await context.OpenAsync(context.PcbAPort).ConfigureAwait(false))
                    {
                        string token = PhaseContext.NewToken("after-dispose-");
                        string? line = await context
                            .ExchangeLineAsync(replacement.Transport, PcbA.EchoRequest(token), 2000)
                            .ConfigureAwait(false);

                        check.Equal("OK " + token + ";", line,
                            "the response from a transport opened on the disposed one's port");

                        context.Counters.Success();
                        await replacement.Transport.Close().ConfigureAwait(false);
                    }

                    check.Note("the disposed transport released its port, so a replacement bound the same endpoint");
                });
        }
    }
}
