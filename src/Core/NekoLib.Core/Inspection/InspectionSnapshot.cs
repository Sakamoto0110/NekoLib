using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

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
            Operations = CopyOperations(
                operations ?? throw new ArgumentNullException(nameof(operations)));
            State = CopyState(
                state ?? throw new ArgumentNullException(nameof(state)));
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

        private static IReadOnlyList<InspectionOperation> CopyOperations(
            IReadOnlyList<InspectionOperation> operations)
        {
            var copy = new List<InspectionOperation>(operations.Count);
            for (int i = 0; i < operations.Count; i++)
                copy.Add(operations[i]);

            return new ReadOnlyCollection<InspectionOperation>(copy);
        }

        private static IReadOnlyDictionary<string, object> CopyState(
            IReadOnlyDictionary<string, object> state)
        {
            var copy = new Dictionary<string, object>(state.Count, StringComparer.Ordinal);
            foreach (var pair in state)
                copy[pair.Key] = pair.Value;

            return new ReadOnlyDictionary<string, object>(copy);
        }
    }
}
