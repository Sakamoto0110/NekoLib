namespace NekoLib.Inspection
{
    /// <summary>
    /// Immutable best-effort diagnostic snapshot of one inspection runtime.
    /// Operation and registry groups are captured under separate locks and are
    /// not one globally atomic instant.
    /// </summary>
    public sealed class InspectionRuntimeDiagnostics
    {
        private readonly int _actionCount;

        internal InspectionRuntimeDiagnostics(
            bool isEnabled,
            int capacity,
            int retainedCount,
            long totalRecorded,
            long evictedCount,
            long clearCount,
            long? oldestSequence,
            long? newestSequence,
            int providerCount,
            int actionCount)
        {
            IsEnabled = isEnabled;
            Capacity = capacity;
            RetainedCount = retainedCount;
            TotalRecorded = totalRecorded;
            EvictedCount = evictedCount;
            ClearCount = clearCount;
            OldestSequence = oldestSequence;
            NewestSequence = newestSequence;
            ProviderCount = providerCount;
            _actionCount = actionCount;
        }

        /// <summary>Gets whether the runtime was enabled when this snapshot was captured.</summary>
        public bool IsEnabled { get; }
        /// <summary>Gets the configured retained-operation capacity.</summary>
        public int Capacity { get; }
        /// <summary>Gets the number of operations retained at capture time.</summary>
        public int RetainedCount { get; }
        /// <summary>Gets the lifetime number of operations recorded by the runtime.</summary>
        public long TotalRecorded { get; }
        /// <summary>Gets the lifetime number of operations evicted by the capacity bound.</summary>
        public long EvictedCount { get; }
        /// <summary>Gets the lifetime number of enabled clear requests.</summary>
        public long ClearCount { get; }
        /// <summary>Gets the oldest retained sequence, or <c>null</c> when no operation is retained.</summary>
        public long? OldestSequence { get; }
        /// <summary>Gets the newest retained sequence, or <c>null</c> when no operation is retained.</summary>
        public long? NewestSequence { get; }
        /// <summary>Gets the number of registered state providers.</summary>
        public int ProviderCount { get; }
        /// <summary>Gets the number of registered experimental actions.</summary>
        [System.Obsolete("Experimental API NEKOEXP0001: compatibility is not guaranteed.", error: false)]
        public int ActionCount => _actionCount;
    }
}
