using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NekoLib.Core.Inspection
{
    /// <summary>Represents a structurally read-only inspection snapshot.</summary>
    /// <remarks>
    /// Operations and state are copied and read-only-wrapped. Operation payloads
    /// and state values remain shallow application references.
    /// </remarks>
    public sealed class InspectionSnapshot
    {
        /// <summary>Initializes an inspection snapshot.</summary>
        /// <param name="capturedUtc">
        /// Caller-supplied capture timestamp. The constructor does not validate or
        /// rewrite <see cref="DateTime.Kind"/>.
        /// </param>
        /// <param name="operations">Non-null ordered operation collection to copy.</param>
        /// <param name="state">Non-null state dictionary to copy with ordinal keys.</param>
        /// <param name="capacity">Caller-supplied retention capacity.</param>
        /// <param name="totalRecorded">Caller-supplied lifetime recorded count.</param>
        /// <param name="evictedCount">Caller-supplied lifetime eviction count.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="operations"/> or <paramref name="state"/> is null.
        /// </exception>
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

        /// <summary>Gets the caller-supplied capture timestamp.</summary>
        public DateTime CapturedUtc { get; }

        /// <summary>Gets the read-only outer snapshot of operations in supplied order.</summary>
        public IReadOnlyList<InspectionOperation> Operations { get; }

        /// <summary>Gets the read-only outer snapshot of state values.</summary>
        public IReadOnlyDictionary<string, object> State { get; }

        /// <summary>Gets the reported operation-retention capacity.</summary>
        public int Capacity { get; }

        /// <summary>Gets the reported lifetime number of recorded operations.</summary>
        public long TotalRecorded { get; }

        /// <summary>Gets the reported lifetime number of capacity evictions.</summary>
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
