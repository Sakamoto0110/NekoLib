using NekoLib.Core.Telemetry;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace NekoLib.Telemetry
{
    /// <summary>
    /// In-process operation telemetry pipeline. Version 1 intentionally keeps
    /// raw completed operations in bounded memory and does not persist them.
    /// <para/>
    /// Completion is synchronous: <c>Complete</c> retains the operation and then
    /// dispatches it to every sink inline, in registration order, before it
    /// returns. All sinks therefore observe one identical order, which is also
    /// the retained order. A slow sink applies backpressure to every completing
    /// thread.
    /// </summary>
    public sealed class TelemetryPipeline : ITelemetry, ITelemetrySnapshotSource
    {
        private readonly object _dispatchGate = new object();
        private readonly object _recentGate = new object();
        private readonly Queue<TelemetryOperation> _recent;
        private readonly int _capacity;
        private readonly ITelemetrySink[] _sinks;

        /// <summary>
        /// Creates a pipeline with a captured options snapshot and a copied set
        /// of sinks. Null sink elements are ignored, and the pipeline does not
        /// own or dispose accepted sinks.
        /// </summary>
        /// <param name="options">Options to capture, or <c>null</c> for defaults.</param>
        /// <param name="sinks">Sinks invoked synchronously in registration order.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <see cref="TelemetryPipelineOptions.RecentOperationCapacity"/> is less than 1.
        /// </exception>
        public TelemetryPipeline(
            TelemetryPipelineOptions? options = null,
            params ITelemetrySink[]? sinks)
        {
            options = options ?? new TelemetryPipelineOptions();
            if (options.RecentOperationCapacity < 1)
                throw new ArgumentOutOfRangeException(nameof(options.RecentOperationCapacity));

            _capacity = options.RecentOperationCapacity;
            _recent = new Queue<TelemetryOperation>(_capacity);
            _sinks = CopySinks(sinks);
        }

        /// <summary>
        /// Takes the pipeline's own copy of the sink set. A caller that passes an
        /// explicitly constructed array keeps a reference to it, and swapping an
        /// element afterwards must not re-target dispatch. Null elements are
        /// dropped once here rather than being re-checked on every completion.
        /// </summary>
        private static ITelemetrySink[] CopySinks(ITelemetrySink[]? sinks)
        {
            if (sinks == null || sinks.Length == 0)
                return Array.Empty<ITelemetrySink>();

            var accepted = new List<ITelemetrySink>(sinks.Length);
            for (int i = 0; i < sinks.Length; i++)
            {
                var sink = sinks[i];
                if (sink != null)
                    accepted.Add(sink);
            }

            return accepted.Count == 0
                ? Array.Empty<ITelemetrySink>()
                : accepted.ToArray();
        }

        /// <summary>
        /// Starts one caller-owned operation. A blank <paramref name="operationId"/>
        /// is replaced by a generated identifier and a blank
        /// <paramref name="parentOperationId"/> is normalized to <c>null</c>.
        /// Initial dimensions are copied immediately; terminal dimensions
        /// supplied to <c>Complete</c> override them on a key collision.
        /// <para/>
        /// The caller owns the single explicit terminal. An operation that is
        /// never completed is simply never recorded, and the pipeline keeps no
        /// reference to it.
        /// </summary>
        /// <param name="module">Non-blank producer module name.</param>
        /// <param name="name">Non-blank operation name.</param>
        /// <param name="operationId">Caller-supplied operation identifier, or <c>null</c>/blank to generate one.</param>
        /// <param name="parentOperationId">Optional parent correlation identifier; blank values become <c>null</c>.</param>
        /// <param name="dimensions">Optional initial dimensions, copied with ordinal key comparison.</param>
        /// <returns>A caller-owned operation that is retained only after its first completion.</returns>
        /// <exception cref="ArgumentException"><paramref name="module"/> or <paramref name="name"/> is blank.</exception>
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
                // A blank parent is normalized like a blank operation id rather
                // than retained: consumers test the retained value against null
                // to decide whether an operation is a root, and whitespace would
                // read as a correlation link that points nowhere.
                string.IsNullOrWhiteSpace(parentOperationId)
                    ? null
                    : parentOperationId,
                Copy(dimensions));
        }

        /// <summary>
        /// Returns the newest completed operations in completion order, bounded by
        /// <paramref name="maxOperations"/> and by the configured capacity. The
        /// result is a fresh collection over models that never change again.
        /// <para/>
        /// Retention happens before sink dispatch and takes a separate lock, so a
        /// snapshot is never blocked by a slow sink.
        /// </summary>
        /// <param name="maxOperations">Maximum number of newest operations to return.</param>
        /// <returns>A detached collection in completion order, or an empty collection for a non-positive limit.</returns>
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
                    try { _sinks[i].Write(operation); }
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

        private static Dictionary<string, double> CopyMeasurements(
            IReadOnlyDictionary<string, double>? values)
        {
            var result = new Dictionary<string, double>(StringComparer.Ordinal);
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
                TelemetryOperation completed;
                lock (_gate)
                {
                    if (_completed)
                        return;

                    // The caller's payload is materialized before any state is
                    // committed. A malformed dictionary - a null key, a throwing
                    // enumerator - must surface to the caller without destroying
                    // the operation, which would otherwise report itself
                    // completed, never reach a sink, and refuse a corrected retry.
                    var terminalDimensions = Copy(dimensions);
                    var copiedMeasurements = CopyMeasurements(measurements);

                    _completed = true;
                    _watch.Stop();

                    foreach (var pair in terminalDimensions)
                        _dimensions[pair.Key] = pair.Value;

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
