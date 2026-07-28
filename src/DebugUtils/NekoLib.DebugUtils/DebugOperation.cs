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
        /// <summary>
        /// Monotonically increasing sequence assigned by the owning
        /// <see cref="DebugUtilsRuntime"/>. A value of zero identifies an
        /// operation created through the legacy public constructor.
        /// </summary>
        public long Sequence { get; }

        public DateTime TimestampUtc { get; }
        public string Module { get; }
        public string Operation { get; }

        /// <summary>
        /// The captured payload, or <c>null</c> when the caller supplied none.
        /// May be a diagnostic placeholder string if the payload delegate threw.
        /// </summary>
        public object? Payload { get; }

        public DebugOperation(DateTime timestampUtc, string module, string operation, object? payload)
            : this(0, timestampUtc, module, operation, payload)
        {
        }

        public DebugOperation(
            long sequence,
            DateTime timestampUtc,
            string module,
            string operation,
            object? payload)
        {
            Sequence = sequence;
            TimestampUtc = timestampUtc;
            Module = module;
            Operation = operation;
            Payload = payload;
        }

        public override string ToString()
        {
            var text = $"[{TimestampUtc:O}] {Module}/{Operation}";
            if (Payload == null)
                return text;

            string payloadText;
            try
            {
                payloadText = Payload.ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                payloadText = "<payload ToString threw: " + ex.GetType().Name + ">";
            }

            return text + " | " + payloadText;
        }
    }
}
