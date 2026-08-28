using System;

namespace NekoLib.Core.Inspection
{
    /// <summary>Represents one structurally read-only recorded inspection operation.</summary>
    /// <remarks>
    /// The payload is retained as a shallow reference. Core does not serialize,
    /// deep-clone, redact, truncate, or validate application payloads.
    /// </remarks>
    public sealed class InspectionOperation
    {
        /// <summary>Initializes a recorded inspection operation.</summary>
        /// <param name="sequence">Caller-supplied ordering sequence.</param>
        /// <param name="timestampUtc">
        /// Caller-supplied timestamp. The constructor does not validate or rewrite
        /// <see cref="DateTime.Kind"/>.
        /// </param>
        /// <param name="module">Non-null logical module identity.</param>
        /// <param name="operation">Non-null operation name.</param>
        /// <param name="payload">Optional shallow payload reference.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="module"/> or <paramref name="operation"/> is null.
        /// </exception>
        public InspectionOperation(
            long sequence,
            DateTime timestampUtc,
            string module,
            string operation,
            object? payload)
        {
            Sequence = sequence;
            TimestampUtc = timestampUtc;
            Module = module ?? throw new ArgumentNullException(nameof(module));
            Operation = operation ?? throw new ArgumentNullException(nameof(operation));
            Payload = payload;
        }

        /// <summary>Gets the caller-supplied ordering sequence.</summary>
        public long Sequence { get; }

        /// <summary>Gets the caller-supplied timestamp.</summary>
        public DateTime TimestampUtc { get; }

        /// <summary>Gets the logical module identity.</summary>
        public string Module { get; }

        /// <summary>Gets the operation name.</summary>
        public string Operation { get; }

        /// <summary>Gets the retained optional payload reference.</summary>
        public object? Payload { get; }

        /// <summary>Formats the timestamp, module, operation, and optional payload.</summary>
        /// <returns>
        /// A human-readable representation. If payload formatting throws, the text
        /// contains only the exception type marker rather than propagating the fault.
        /// </returns>
        /// <remarks>The returned text can contain sensitive payload data.</remarks>
        public override string ToString()
        {
            var text = $"[{TimestampUtc:O}] {Module}/{Operation}";
            if (Payload == null)
                return text;

            try
            {
                return text + " | " + (Payload.ToString() ?? string.Empty);
            }
            catch (Exception ex)
            {
                return text + " | <payload ToString threw: " + ex.GetType().Name + ">";
            }
        }
    }
}
