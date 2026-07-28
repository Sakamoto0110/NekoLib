namespace NekoLib.DebugUtils
{
    /// <summary>
    /// Immutable scalar snapshot of a <see cref="DebugUtilsRuntime"/>.
    /// It deliberately carries no recorded payloads or registered delegates.
    /// </summary>
    public sealed class DebugUtilsRuntimeDiagnostics
    {
        internal DebugUtilsRuntimeDiagnostics(
            bool isEnabled,
            int capacity,
            int retainedCount,
            long totalRecorded,
            long evictedCount,
            long clearCount,
            long? oldestSequence,
            long? newestSequence,
            int stateProviderCount,
            int commandCount)
        {
            IsEnabled = isEnabled;
            Capacity = capacity;
            RetainedCount = retainedCount;
            TotalRecorded = totalRecorded;
            EvictedCount = evictedCount;
            ClearCount = clearCount;
            OldestSequence = oldestSequence;
            NewestSequence = newestSequence;
            ProviderCount = stateProviderCount;
            CommandCount = commandCount;
        }

        public bool IsEnabled { get; }
        public int Capacity { get; }
        public int RetainedCount { get; }
        public long TotalRecorded { get; }
        public long EvictedCount { get; }
        public long ClearCount { get; }
        public long? OldestSequence { get; }
        public long? NewestSequence { get; }
        public int ProviderCount { get; }
        public int CommandCount { get; }
    }
}
