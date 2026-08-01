using System;

namespace NekoLib.Core.Inspection
{
    public sealed class InspectionOperation
    {
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

        public long Sequence { get; }
        public DateTime TimestampUtc { get; }
        public string Module { get; }
        public string Operation { get; }
        public object? Payload { get; }

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
