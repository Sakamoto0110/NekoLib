using NekoLib.Core.Telemetry;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace NekoLib.Telemetry
{
    /// <summary>
    /// In-process operation telemetry pipeline. Version 1 intentionally keeps
    /// raw completed operations in bounded memory and does not persist them.
    /// </summary>
    public sealed class TelemetryPipeline : ITelemetry, ITelemetrySnapshotSource
    {
        private readonly object _dispatchGate = new object();
        private readonly object _recentGate = new object();
        private readonly Queue<TelemetryOperation> _recent;
        private readonly int _capacity;
        private readonly ITelemetrySink[] _sinks;

        public TelemetryPipeline(
            TelemetryPipelineOptions? options = null,
            params ITelemetrySink[]? sinks)
        {
            options = options ?? new TelemetryPipelineOptions();
            if (options.RecentOperationCapacity < 1)
                throw new ArgumentOutOfRangeException(nameof(options.RecentOperationCapacity));

            _capacity = options.RecentOperationCapacity;
            _recent = new Queue<TelemetryOperation>(_capacity);
            _sinks = sinks ?? Array.Empty<ITelemetrySink>();
        }

        public ITelemetryOperation StartOperation(
            string module,
            string name,
            string? operationId = null,
            string? parentOperationId = null,
            IReadOnlyDictionary<string, object>? dimensions = null)
        {
            if (string.IsNullOrWhiteSpace(module))
                throw new ArgumentException("Module is required.", nameof(module));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name is required.", nameof(name));

            return new OperationScope(
                this,
                module,
                name,
                string.IsNullOrWhiteSpace(operationId)
                    ? Guid.NewGuid().ToString("N")
                    : operationId!,
                parentOperationId,
                Copy(dimensions));
        }

        public IReadOnlyList<TelemetryOperation> GetRecentOperations(int maxOperations)
        {
            if (maxOperations <= 0)
                return Array.Empty<TelemetryOperation>();

            lock (_recentGate)
            {
                var all = _recent.ToArray();
                var take = Math.Min(maxOperations, all.Length);
                var result = new TelemetryOperation[take];
                Array.Copy(all, all.Length - take, result, 0, take);
                return result;
            }
        }

        private void Record(TelemetryOperation operation)
        {
            lock (_dispatchGate)
            {
                lock (_recentGate)
                {
                    _recent.Enqueue(operation);
                    while (_recent.Count > _capacity)
                        _recent.Dequeue();
                }

                for (int i = 0; i < _sinks.Length; i++)
                {
                    try { _sinks[i]?.Write(operation); }
                    catch { /* telemetry must never break feature behavior */ }
                }
            }
        }

        private static Dictionary<string, object> Copy(
            IReadOnlyDictionary<string, object>? values)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            if (values == null)
                return result;

            foreach (var pair in values)
                result[pair.Key] = pair.Value;
            return result;
        }

        private sealed class OperationScope : ITelemetryOperation
        {
            private readonly object _gate = new object();
            private readonly TelemetryPipeline _owner;
            private readonly DateTime _startedUtc = DateTime.UtcNow;
            private readonly Stopwatch _watch = Stopwatch.StartNew();
            private readonly string _module;
            private readonly string _name;
            private readonly string? _parentOperationId;
            private readonly Dictionary<string, object> _dimensions;
            private readonly List<TelemetryCheckpoint> _checkpoints =
                new List<TelemetryCheckpoint>();
            private bool _completed;

            public OperationScope(
                TelemetryPipeline owner,
                string module,
                string name,
                string operationId,
                string? parentOperationId,
                Dictionary<string, object> dimensions)
            {
                _owner = owner;
                _module = module;
                _name = name;
                OperationId = operationId;
                _parentOperationId = parentOperationId;
                _dimensions = dimensions;
            }

            public string OperationId { get; }

            public bool IsCompleted
            {
                get { lock (_gate) return _completed; }
            }

            public TimeSpan Checkpoint(
                string name,
                IReadOnlyDictionary<string, object>? dimensions = null)
            {
                if (string.IsNullOrWhiteSpace(name))
                    throw new ArgumentException("Checkpoint name is required.", nameof(name));

                lock (_gate)
                {
                    if (_completed)
                        return _watch.Elapsed;

                    var elapsed = _watch.Elapsed;
                    _checkpoints.Add(new TelemetryCheckpoint(
                        name,
                        elapsed,
                        Copy(dimensions)));
                    return elapsed;
                }
            }

            public void Complete(
                TelemetryOutcome outcome,
                IReadOnlyDictionary<string, object>? dimensions = null,
                IReadOnlyDictionary<string, double>? measurements = null)
            {
                TelemetryOperation? completed;
                lock (_gate)
                {
                    if (_completed)
                        return;

                    _completed = true;
                    _watch.Stop();

                    foreach (var pair in Copy(dimensions))
                        _dimensions[pair.Key] = pair.Value;

                    var copiedMeasurements = new Dictionary<string, double>(StringComparer.Ordinal);
                    if (measurements != null)
                    {
                        foreach (var pair in measurements)
                            copiedMeasurements[pair.Key] = pair.Value;
                    }

                    completed = new TelemetryOperation(
                        _startedUtc,
                        _module,
                        _name,
                        OperationId,
                        _parentOperationId,
                        outcome,
                        _watch.Elapsed,
                        _checkpoints.ToArray(),
                        new Dictionary<string, object>(_dimensions, StringComparer.Ordinal),
                        copiedMeasurements);
                }

                _owner.Record(completed);
            }
        }
    }
}
