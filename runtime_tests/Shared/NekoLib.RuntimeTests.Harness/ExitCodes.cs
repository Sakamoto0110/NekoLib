namespace NekoLib.RuntimeTests.Harness
{
    /// <summary>
    /// The scenario's whole result is its exit code. Nothing here requires a
    /// person to read free-form output to decide whether a run passed.
    /// <para/>
    /// The codes separate the four things an unattended campaign has to tell
    /// apart: the library behaved wrongly, the run never got far enough to
    /// judge, the machine was not ready, and the scenario itself broke.
    /// </summary>
    public static class ExitCodes
    {
        /// <summary>Every selected check passed and cleanup reconciled.</summary>
        public const int Success = 0;

        /// <summary>Command line could not be understood.</summary>
        public const int Usage = 2;

        /// <summary>
        /// A prerequisite the scenario does not own was missing or unusable:
        /// an external service, a device, a credential variable, a writable
        /// artifact directory. This is an environment result, never a product
        /// finding.
        /// </summary>
        public const int PrerequisiteMissing = 3;

        /// <summary>At least one check asserted a wrong observable outcome.</summary>
        public const int CheckFailed = 4;

        /// <summary>A bounded wait expired: no progress, or a fault never took effect.</summary>
        public const int Timeout = 5;

        /// <summary>
        /// Reconciliation failed: a resource the scenario created or adopted was
        /// left behind, or an environment it changed was not restored.
        /// </summary>
        public const int CleanupFailed = 6;

        /// <summary>The scenario threw where it has no expectation. A scenario defect until proven otherwise.</summary>
        public const int Unexpected = 7;

        /// <summary>Ctrl+C or process termination; bounded cleanup ran and a partial summary was written.</summary>
        public const int Interrupted = 8;
    }
}
