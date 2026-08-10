#nullable enable
using NekoLib.RuntimeTests.Harness.Faults;

namespace NekoLib.Observability.RuntimeTests.LongRunningRecovery.Faults
{
    /// <summary>
    /// The fault kinds this scenario owns. Each names a transition the recovery
    /// rehearsal has to prove at least once.
    /// <para/>
    /// Every one of them is produced by a sink, a state provider or a file this
    /// project owns. None of them is a control surface on Logging, Telemetry or
    /// Inspection, which is what the suite requires: fault injection belongs to
    /// the scenario, never to the library.
    /// </summary>
    internal static class FaultKinds
    {
        public const string LogSinkThrows = "log-sink-throws";
        public const string LogFileLocked = "log-file-locked";
        public const string LogFlushBlocked = "log-flush-blocked";
        public const string TelemetrySinkThrows = "telemetry-sink-throws";
        public const string InspectionProviderThrows = "inspection-provider-throws";
        public const string InspectionProviderTimesOut = "inspection-provider-times-out";
        public const string InspectionGlobalTeardown = "inspection-global-teardown";

        /// <summary>The kinds a rehearsal must cover, in the order they are generated from.</summary>
        public static readonly string[] RecoveryRehearsalSet =
        {
            LogSinkThrows,
            LogFileLocked,
            LogFlushBlocked,
            TelemetrySinkThrows,
            InspectionProviderThrows,
            InspectionProviderTimesOut,
            InspectionGlobalTeardown
        };

        /// <summary>Which capability's result section a fault belongs to.</summary>
        public static string Capability(string kind)
        {
            switch (kind)
            {
                case LogSinkThrows:
                case LogFileLocked:
                case LogFlushBlocked:
                    return Phases.Logging;

                case TelemetrySinkThrows:
                    return Phases.Telemetry;

                default:
                    return Phases.Inspection;
            }
        }
    }

    /// <summary>
    /// The phase names, which are also the result sections.
    /// <para/>
    /// The suite requires each capability to have independent assertions and its
    /// own section "so a shared process does not turn them into one claimed
    /// feature". The phase on every check is what carries that separation into
    /// <c>result.json</c>.
    /// </summary>
    internal static class Phases
    {
        public const string Logging = "logging";
        public const string Telemetry = "telemetry";
        public const string Inspection = "inspection";
        public const string Recovery = "recovery";
        public const string Cleanup = "cleanup";
    }

    /// <summary>
    /// Tells the harness what each of this scenario's fault kinds targets and
    /// what recovery from it looks like.
    /// </summary>
    internal sealed class ObservabilityFaultVocabulary : IFaultVocabulary
    {
        public string DescribeTarget(string kind)
        {
            switch (kind)
            {
                case FaultKinds.LogSinkThrows: return "scenario-owned failing log sink";
                case FaultKinds.LogFileLocked: return "scenario rolling log file";
                case FaultKinds.LogFlushBlocked: return "scenario-owned blocking log sink";
                case FaultKinds.TelemetrySinkThrows: return "scenario-owned telemetry sink";
                case FaultKinds.InspectionProviderThrows: return "scenario-owned throwing state provider";
                case FaultKinds.InspectionProviderTimesOut: return "scenario-owned slow state provider";
                case FaultKinds.InspectionGlobalTeardown: return "process-wide Inspection slot";
                default: return "scenario-owned component";
            }
        }

        /// <summary>How long the fault is left in place, in seconds.</summary>
        public string DescribeParameters(string kind)
        {
            switch (kind)
            {
                case FaultKinds.InspectionGlobalTeardown: return "0";
                case FaultKinds.LogFlushBlocked: return "3";
                default: return "5";
            }
        }

        public string DescribeExpectedRecovery(string kind)
        {
            switch (kind)
            {
                case FaultKinds.LogSinkThrows:
                    return "the failing sink throws on its seeded schedule, every healthy sink still receives every " +
                           "entry, and ordinary logging continues once the sink is disarmed";

                case FaultKinds.LogFileLocked:
                    return "writes to the locked file fail inside the sink and are swallowed by the Logger, other " +
                           "sinks are unaffected, and the file is complete and writable again once the lock is released";

                case FaultKinds.LogFlushBlocked:
                    return "ILogFlusher.Flush(timeout) returns false within its bound rather than hanging, and " +
                           "returns true again once the sink is released";

                case FaultKinds.TelemetrySinkThrows:
                    return "the pipeline keeps recording completed operations, bounded retention is unaffected, and " +
                           "snapshots still read correctly once the sink is disarmed";

                case FaultKinds.InspectionProviderThrows:
                    return "the failing provider's slot carries a thrown marker, healthy providers and recorded " +
                           "operations still appear in the same snapshot, and the provider reports normally once disarmed";

                case FaultKinds.InspectionProviderTimesOut:
                    return "the slow provider's slot carries a timed-out marker within the budget, the rest of the " +
                           "snapshot is complete, and the capture returns inside its timeout";

                case FaultKinds.InspectionGlobalTeardown:
                    return "disposing the global runtime restores the process-wide slot to the null recorder, " +
                           "recording through it is a harmless no-op, and a fresh EnableGlobal succeeds afterwards";

                default:
                    return "unspecified";
            }
        }
    }
}
