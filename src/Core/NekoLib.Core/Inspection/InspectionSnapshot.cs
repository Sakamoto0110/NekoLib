using System;
using System.Collections.Generic;

namespace NekoLib.Core.Inspection
{
    public sealed class InspectionSnapshot
    {
        public InspectionSnapshot(
            DateTime capturedUtc,
            IReadOnlyList<InspectionOperation> operations,
            IReadOnlyDictionary<string, object> state,
            int capacity,
            long totalRecorded,
            long evictedCount)
        {
            CapturedUtc = capturedUtc;
            Operations = operations ?? throw new ArgumentNullException(nameof(operations));
            State = state ?? throw new ArgumentNullException(nameof(state));
            Capacity = capacity;
            TotalRecorded = totalRecorded;
            EvictedCount = evictedCount;
        }

        public DateTime CapturedUtc { get; }
        public IReadOnlyList<InspectionOperation> Operations { get; }
        public IReadOnlyDictionary<string, object> State { get; }
        public int Capacity { get; }
        public long TotalRecorded { get; }
        public long EvictedCount { get; }
    }
}
