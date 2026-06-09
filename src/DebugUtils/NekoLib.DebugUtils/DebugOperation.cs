using System;

namespace NekoLib.DebugUtils
{
    /// <summary>
    /// Immutable record of a single observed operation captured by
    /// <see cref="DebugUtilsRuntime"/>. Not a <c>record</c> type so it compiles on
    /// net481 without the IsExternalInit shim.
    /// </summary>
    public sealed class DebugOperation
    {
        public DateTime TimestampUtc { get; }
        public string Module { get; }
        public string Operation { get; }

        /// <summary>
        /// The captured payload, or <c>null</c> when the caller supplied none.
        /// May be a diagnostic placeholder string if the payload delegate threw.
        /// </summary>
        public object? Payload { get; }

        public DebugOperation(DateTime timestampUtc, string module, string operation, object? payload)
        {
            TimestampUtc = timestampUtc;
            Module = module;
            Operation = operation;
            Payload = payload;
        }

        public override string ToString()
            => $"[{TimestampUtc:O}] {Module}/{Operation}"
               + (Payload != null ? $" | {Payload}" : string.Empty);
    }
}
