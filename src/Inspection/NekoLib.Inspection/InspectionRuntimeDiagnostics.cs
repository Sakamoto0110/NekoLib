namespace NekoLib.Inspection
{
    public sealed class InspectionRuntimeDiagnostics
    {
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
            ActionCount = actionCount;
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
        public int ActionCount { get; }
    }
}
