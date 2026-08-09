#nullable enable
using NekoLib.RuntimeTests.Harness.Faults;

namespace NekoLib.Data.RuntimeTests.SqlServer.Faults
{
    /// <summary>
    /// The fault kinds this scenario owns. Each one names a transition the
    /// recovery rehearsal has to prove at least once.
    /// <para/>
    /// This vocabulary stays with the scenario. The harness places faults in
    /// time and hashes the plan; what a fault means, what it targets, and what
    /// counts as recovery are knowledge only the owning scenario has.
    /// </summary>
    internal static class FaultKinds
    {
        public const string ConnectWhileServerDown = "connect-while-server-down";
        public const string TransportLossDuringCommand = "transport-loss-during-command";
        public const string TransportLossDuringTransaction = "transport-loss-during-transaction";
        public const string TransportLossDuringStream = "transport-loss-during-stream";
        public const string StalePooledConnection = "stale-pooled-connection";
        public const string ContainerRestart = "container-restart";
        public const string SchemaRecreation = "schema-recreation";

        /// <summary>The kinds a rehearsal must cover, in the order they are generated from.</summary>
        public static readonly string[] RecoveryRehearsalSet =
        {
            ConnectWhileServerDown,
            TransportLossDuringCommand,
            TransportLossDuringTransaction,
            TransportLossDuringStream,
            StalePooledConnection,
            ContainerRestart,
            SchemaRecreation
        };

        /// <summary>True when acting on the fault requires stopping the adopted container.</summary>
        public static bool NeedsContainerControl(string kind)
        {
            return kind == ConnectWhileServerDown
                || kind == TransportLossDuringCommand
                || kind == TransportLossDuringTransaction
                || kind == TransportLossDuringStream
                || kind == StalePooledConnection
                || kind == ContainerRestart;
        }
    }

    /// <summary>
    /// Tells the harness what each of this scenario's fault kinds targets and
    /// what recovery from it looks like.
    /// </summary>
    internal sealed class SqlServerFaultVocabulary : IFaultVocabulary
    {
        private readonly string _containerName;

        public SqlServerFaultVocabulary(string containerName)
        {
            _containerName = containerName;
        }

        public string DescribeTarget(string kind) =>
            FaultKinds.NeedsContainerControl(kind) ? _containerName : "scenario-database";

        public string DescribeParameters(string kind) =>
            kind == FaultKinds.ContainerRestart ? "0" : "5";

        public string DescribeExpectedRecovery(string kind)
        {
            switch (kind)
            {
                case FaultKinds.ConnectWhileServerDown:
                    return "the open attempt fails with a provider connection error and no session is left behind; " +
                           "ordinary work succeeds once the server is back";
                case FaultKinds.TransportLossDuringCommand:
                    return "the in-flight command fails with a provider transport error, the connection is disposed, " +
                           "and a fresh command succeeds after recovery";
                case FaultKinds.TransportLossDuringTransaction:
                    return "the transaction does not commit, the session disposes without throwing, " +
                           "and the database shows no partial effect after recovery";
                case FaultKinds.TransportLossDuringStream:
                    return "the stream reports exactly one failed terminal outcome and releases its reader and connection";
                case FaultKinds.StalePooledConnection:
                    return "a pooled handle from before the interruption fails or is discarded, and the scenario's own " +
                           "bounded retry reaches a working connection";
                case FaultKinds.ContainerRestart:
                    return "the server returns, the scenario database is reachable again, and ordinary commands succeed";
                case FaultKinds.SchemaRecreation:
                    return "the schema is recreated deterministically and ordinary commands succeed against it";
                default:
                    return "unspecified";
            }
        }
    }
}
