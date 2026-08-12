#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using NekoLib.RuntimeTests.Harness;
using NekoLib.RuntimeTests.Harness.Faults;
using NekoLib.RuntimeTests.Harness.Reporting;

namespace NekoLib.Pipes.RuntimeTests.LongRunningRecovery
{
    /// <summary>Which of the three processes this one is.</summary>
    internal enum ScenarioRole
    {
        Controller = 0,
        Server = 1,
        Client = 2
    }

    /// <summary>
    /// What this scenario adds to the suite's shared command line.
    /// <para/>
    /// <c>--role</c> is the only structural addition: the controller re-launches
    /// this same executable as a server and as clients, so the three processes
    /// the suite asks for are three real processes without three projects.
    /// </summary>
    internal sealed class ScenarioOptions : ScenarioOptionsBase
    {
        public ScenarioRole Role = ScenarioRole.Controller;

        /// <summary>Set by the controller on a child; children never invent a name.</summary>
        public string PipeName = string.Empty;

        /// <summary>How many client children the controller starts for the load phases.</summary>
        public int Clients = 3;

        /// <summary>Child-only: how long to keep working before exiting.</summary>
        public TimeSpan ChildDuration = TimeSpan.FromMinutes(1);

        /// <summary>Child-only: where to write its result document.</summary>
        public string ChildResultPath = string.Empty;

        /// <summary>
        /// Child-only: a file whose appearance asks this child to stop working
        /// and write its result.
        /// <para/>
        /// A client outlives the controller's own run on purpose, so the
        /// controller needs a way to end it that still lets it report. Forcing it
        /// would leave no document at all, which is precisely the state that made
        /// <c>client-children-correlation</c> read nothing on 2026-08-12.
        /// </summary>
        public string ChildStopFile = string.Empty;

        public TimeSpan SmokeDuration = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Runs the isolated terminal-classification contracts and exits. Opens
        /// no pipe and starts no process, so it is not runtime evidence.
        /// </summary>
        public bool ContractsOnly;

        public static readonly TimeSpan MinimumSpecifiedSmoke = TimeSpan.FromMinutes(15);

        public override string ScenarioId => "E3-PIPE";

        protected override string CampaignPrefix => "e3pipe";

        /// <summary>
        /// Identifies the schedule generator and is covered by the schedule
        /// hash, so it is this scenario's own string and nobody else's.
        /// </summary>
        public const string ScheduleGeneratorVersion = "e3pipe-schedule-1";

        protected override IEnumerable<string> ScenarioUsage() => new[]
        {
            "  --smoke-duration <d>        smoke window, default 15m (the suite specifies 15-30m)",
            "  --clients <n>               client child processes, default 3",
            "  --contracts                 run the isolated terminal-classification contracts and exit",
            string.Empty,
            "Child roles are started by the controller and are not meant to be run by hand:",
            "  --role server|client        run as a child process",
            "  --pipe <name>               the endpoint the controller allocated",
            "  --child-duration <d>        how long the child works",
            "  --child-result <file>       where the child writes its result",
            "  --child-stop-file <file>    asks the child to stop and report when it appears"
        };

        protected override bool TryParseScenarioOption(
            string[] args,
            ref int index,
            string option,
            out string diagnostic)
        {
            diagnostic = string.Empty;

            switch (option)
            {
                case "--role":
                    if (!TryTakeValue(args, ref index, "--role", out string role, out diagnostic)) return false;
                    switch (role.ToLowerInvariant())
                    {
                        case "controller": Role = ScenarioRole.Controller; return true;
                        case "server": Role = ScenarioRole.Server; return true;
                        case "client": Role = ScenarioRole.Client; return true;
                        default:
                            diagnostic = "--role expects controller, server or client, got '" + role + "'";
                            return false;
                    }

                case "--pipe":
                    if (!TryTakeValue(args, ref index, "--pipe", out PipeName, out diagnostic)) return false;
                    return true;

                case "--child-result":
                    if (!TryTakeValue(args, ref index, "--child-result", out ChildResultPath, out diagnostic))
                        return false;
                    return true;

                case "--child-stop-file":
                    if (!TryTakeValue(args, ref index, "--child-stop-file", out ChildStopFile, out diagnostic))
                        return false;
                    return true;

                case "--child-duration":
                    if (!TryTakeValue(args, ref index, "--child-duration", out string childDuration, out diagnostic))
                        return false;
                    return TryParseDuration(childDuration, out ChildDuration, out diagnostic);

                case "--smoke-duration":
                    if (!TryTakeValue(args, ref index, "--smoke-duration", out string smoke, out diagnostic))
                        return false;
                    return TryParseDuration(smoke, out SmokeDuration, out diagnostic);

                case "--contracts":
                    ContractsOnly = true;
                    return true;

                case "--clients":
                    if (!TryTakeValue(args, ref index, "--clients", out string clients, out diagnostic)) return false;
                    if (!int.TryParse(clients, NumberStyles.Integer, CultureInfo.InvariantCulture, out Clients) ||
                        Clients < 1)
                    {
                        diagnostic = "--clients expects a positive integer, got '" + clients + "'";
                        return false;
                    }
                    return true;

                default:
                    return false;
            }
        }

        public static string UsageText() =>
            new ScenarioOptions().Usage(
                "E3-PIPE - NekoLib.Pipes across real processes, under load, failure and recovery");
    }

    /// <summary>
    /// The fault kinds this scenario owns. Each names a transition the recovery
    /// rehearsal has to prove at least once.
    /// <para/>
    /// Every one is produced by a process the controller started, a handler this
    /// project wrote, or a raw pipe peer it opened itself. None is a control
    /// surface on <c>NekoLib.Pipes</c>: the suite forbids a privileged control
    /// plane, and a scenario that added one would be testing its own back door.
    /// </summary>
    internal static class FaultKinds
    {
        public const string KillServer = "kill-server-process";
        public const string KillClient = "kill-client-process";
        public const string RawPeerMidFrame = "raw-peer-closes-mid-frame";
        public const string HandlerDelay = "handler-delay-forces-timeout";
        public const string SlowSubscriber = "slow-subscriber-overflows-queue";
        public const string DisposeUnderLoad = "dispose-while-requests-admitted";

        public static readonly string[] RecoveryRehearsalSet =
        {
            KillServer,
            KillClient,
            RawPeerMidFrame,
            HandlerDelay,
            SlowSubscriber,
            DisposeUnderLoad
        };
    }

    internal static class Phases
    {
        public const string Request = "request";
        public const string Events = "events";
        public const string Protocol = "protocol";
        public const string Lifecycle = "lifecycle";
        public const string Recovery = "recovery";
    }

    /// <summary>
    /// Tells the harness what each fault kind targets and what recovery looks
    /// like.
    /// <para/>
    /// <b>Nothing here may name a per-run resource.</b> The target string is
    /// covered by the schedule hash, and this scenario's endpoint is derived
    /// from the campaign id, which carries the target framework and a
    /// millisecond timestamp. An earlier version interpolated the pipe name and
    /// the same seed then produced a different hash on every single run — the
    /// determinism the schedule exists to provide, silently gone. A target
    /// describes the *class* of resource; the instance belongs in
    /// <c>environment.json</c>, which records the endpoint for the run.
    /// </summary>
    internal sealed class PipeFaultVocabulary : IFaultVocabulary
    {
        public string DescribeTarget(string kind)
        {
            switch (kind)
            {
                case FaultKinds.KillServer: return "controller-owned server process";
                case FaultKinds.KillClient: return "controller-owned client process";
                case FaultKinds.RawPeerMidFrame: return "scenario-owned raw pipe peer";
                case FaultKinds.HandlerDelay: return "scenario-owned slow handler";
                case FaultKinds.SlowSubscriber: return "scenario-owned event subscriber";
                default: return "scenario-owned client and server objects";
            }
        }

        public string DescribeParameters(string kind) =>
            kind == FaultKinds.KillServer ? "5" : "2";

        public string DescribeExpectedRecovery(string kind)
        {
            switch (kind)
            {
                case FaultKinds.KillServer:
                    return "in-flight requests fail rather than hang, the endpoint is released, a replacement " +
                           "server binds the same name, and a fresh request succeeds";
                case FaultKinds.KillClient:
                    return "the server sheds the connection, its connected-client count returns to the survivors, " +
                           "and the remaining clients keep completing requests";
                case FaultKinds.RawPeerMidFrame:
                    return "a peer that closes mid-frame is dropped without disturbing the server, and the next " +
                           "ordinary request succeeds on a new connection";
                case FaultKinds.HandlerDelay:
                    return "the request times out on the client within its bound, and the next request on a fresh " +
                           "connection returns its own response rather than the abandoned one";
                case FaultKinds.SlowSubscriber:
                    return "the bounded queue overflows under the configured policy - dropped and counted, or the " +
                           "subscriber disconnected - while other subscribers keep receiving in order";
                default:
                    return "disposal while work is admitted completes without throwing, releases the endpoint, and " +
                           "the name can be bound again afterwards";
            }
        }
    }

    /// <summary>The versions only this scenario can name.</summary>
    internal static class ScenarioFacts
    {
        public static string PipesVersion =>
            RuntimeFacts.DescribeAssembly("NekoLib.Pipes", typeof(global::NekoLib.Pipes.PipeServer));

        public static string ScenarioVersion => RuntimeFacts.AssemblyVersion(typeof(ScenarioFacts));
    }

    /// <summary>A counter that is cheap to read from the sampling path.</summary>
    internal sealed class Counter
    {
        private long _value;

        public long Value => System.Threading.Interlocked.Read(ref _value);
        public void Increment() => System.Threading.Interlocked.Increment(ref _value);
        public void Add(long amount) => System.Threading.Interlocked.Add(ref _value, amount);
        public void Set(long value) => System.Threading.Interlocked.Exchange(ref _value, value);
    }

    /// <summary>
    /// The sample columns only this scenario can fill.
    /// <para/>
    /// The suite asks every sample to carry "active/retained item counts for
    /// bounded components" and "queue depth or a truthful unavailable marker".
    /// Here that is connections, in-flight requests, subscribers and dropped
    /// events - and the child processes, because a scenario whose children
    /// outlive it has leaked the thing that matters most.
    /// </summary>
    internal sealed class ScenarioSamples : IScenarioSamples
    {
        private static readonly string[] Columns =
        {
            "requests_sent",
            "requests_failed",
            "events_received",
            "events_dropped",
            "child_processes",
            "server_connected_clients",
            "subscribers"
        };

        public static IReadOnlyList<string> ColumnNamesForHeader => Columns;

        public IReadOnlyList<string> ColumnNames => Columns;

        public readonly Counter RequestsSent = new Counter();
        public readonly Counter RequestsFailed = new Counter();
        public readonly Counter EventsReceived = new Counter();
        public readonly Counter EventsDropped = new Counter();
        public readonly Counter ChildProcesses = new Counter();
        public readonly Counter ServerConnectedClients = new Counter();
        public readonly Counter Subscribers = new Counter();

        public long[] Read() => new[]
        {
            RequestsSent.Value,
            RequestsFailed.Value,
            EventsReceived.Value,
            EventsDropped.Value,
            ChildProcesses.Value,
            ServerConnectedClients.Value,
            Subscribers.Value
        };
    }

    internal sealed class PipeSummary : IScenarioSummary
    {
        private readonly string _pipeName;
        private readonly IReadOnlyList<string> _boundaries;

        public PipeSummary(string pipeName, IReadOnlyList<string> boundaries)
        {
            _pipeName = pipeName;
            _boundaries = boundaries;
        }

        public IReadOnlyList<KeyValuePair<string, string>> Facts => new[]
        {
            new KeyValuePair<string, string>("Pipes", ScenarioFacts.PipesVersion),
            new KeyValuePair<string, string>("Endpoint", _pipeName)
        };

        public void WriteJson(JsonWriter json)
        {
            json.Prop("pipes", ScenarioFacts.PipesVersion);
            json.Prop("pipeName", _pipeName);
            json.Array("claimBoundaries", () =>
            {
                foreach (string boundary in _boundaries) json.Item(boundary);
            });
        }
    }
}
