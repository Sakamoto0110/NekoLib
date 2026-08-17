namespace NekoLib.Inspection
{
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

        public bool IsEnabled { get; }
        public int Capacity { get; }
        public int RetainedCount { get; }
        public long TotalRecorded { get; }
        public long EvictedCount { get; }
        public long ClearCount { get; }
        public long? OldestSequence { get; }
        public long? NewestSequence { get; }
        public int ProviderCount { get; }
        [System.Obsolete("Experimental API NEKOEXP0001: compatibility is not guaranteed.", error: false)]
        public int ActionCount => _actionCount;
    }
}
