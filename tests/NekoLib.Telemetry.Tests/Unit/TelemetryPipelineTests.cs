using NekoLib.Core.Telemetry;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NekoLib.Telemetry.Tests.Unit
{
    public sealed class TelemetryPipelineTests
    {
        [Fact]
        public void StartOperation_ExplicitCorrelation_PreservesIdentifiers()
        {
            var pipeline = new TelemetryPipeline();
            var operation = pipeline.StartOperation(
                "Navigation",
                "page_switch",
                "operation-id",
                "parent-id");

            operation.Complete(TelemetryOutcome.Succeeded);

            var snapshot = pipeline.GetRecentOperations(10).Single();
            Assert.Equal("operation-id", snapshot.OperationId);
            Assert.Equal("parent-id", snapshot.ParentOperationId);
        }

        [Fact]
        public void Checkpoint_MultipleCalls_UsesMonotonicElapsedValues()
        {
            var pipeline = new TelemetryPipeline();
            var operation = pipeline.StartOperation("Navigation", "page_switch");

            operation.Checkpoint("started");
            Thread.Sleep(5);
            operation.Checkpoint("authenticated");
            operation.Complete(TelemetryOutcome.Succeeded);

            var snapshot = pipeline.GetRecentOperations(1).Single();
            Assert.True(snapshot.Checkpoints[1].Elapsed >= snapshot.Checkpoints[0].Elapsed);
            Assert.True(snapshot.Duration >= snapshot.Checkpoints[1].Elapsed);
        }

        [Fact]
        public void Complete_CalledTwice_RecordsOneTerminalOutcome()
        {
            var pipeline = new TelemetryPipeline();
            var operation = pipeline.StartOperation("Navigation", "page_switch");

            operation.Complete(TelemetryOutcome.Failed);
            operation.Complete(TelemetryOutcome.Succeeded);

            var snapshot = pipeline.GetRecentOperations(10);
            Assert.Single(snapshot);
            Assert.Equal(TelemetryOutcome.Failed, snapshot[0].Outcome);
            Assert.True(operation.IsCompleted);
        }

        [Fact]
        public void Complete_DimensionsAndMeasurements_PreservesValues()
        {
            var pipeline = new TelemetryPipeline();
            var operation = pipeline.StartOperation(
                "Navigation",
                "page_switch",
                dimensions: new Dictionary<string, object> { ["target"] = "Catalog" });

            operation.Complete(
                TelemetryOutcome.Succeeded,
                new Dictionary<string, object> { ["ready"] = true },
                new Dictionary<string, double> { ["page_switch.total_ms"] = 12.5 });

            var snapshot = pipeline.GetRecentOperations(1).Single();
            Assert.Equal("Catalog", snapshot.Dimensions["target"]);
            Assert.Equal(true, snapshot.Dimensions["ready"]);
            Assert.Equal(12.5, snapshot.Measurements["page_switch.total_ms"]);
        }

        [Fact]
        public void Complete_CapacityExceeded_RetainsNewestOperations()
        {
            var pipeline = new TelemetryPipeline(new TelemetryPipelineOptions
            {
                RecentOperationCapacity = 2
            });

            Complete(pipeline, "one");
            Complete(pipeline, "two");
            Complete(pipeline, "three");

            Assert.Equal(new[] { "two", "three" },
                pipeline.GetRecentOperations(10).Select(x => x.Name));
        }

        [Fact]
        public void Complete_SinkThrows_LaterSinkStillReceivesOperation()
        {
            var sink = new RecordingSink();
            var pipeline = new TelemetryPipeline(
                null,
                new ThrowingSink(),
                sink);

            Complete(pipeline, "operation");

            Assert.Single(sink.Operations);
        }

        [Fact]
        public void Constructor_SinkArrayMutatedAfterConstruction_DoesNotRetargetDispatch()
        {
            var supplied = new RecordingSink();
            var replacement = new RecordingSink();
            var sinks = new ITelemetrySink[] { supplied };
            var pipeline = new TelemetryPipeline(null, sinks);

            sinks[0] = replacement;
            Complete(pipeline, "after the caller swapped its own array");

            Assert.Single(supplied.Operations);
            Assert.Empty(replacement.Operations);
        }

        [Fact]
        public void Constructor_NullSinkArrayAndElements_AreTolerated()
        {
            var real = new RecordingSink();

            var withNullArray = new TelemetryPipeline(null, null);
            Complete(withNullArray, "no sinks");
            Assert.Single(withNullArray.GetRecentOperations(10));

            var withNullElements = new TelemetryPipeline(
                null,
                new ITelemetrySink[] { null, real, null });
            Complete(withNullElements, "past the null elements");

            Assert.Single(real.Operations);
        }

        [Fact]
        public void Complete_TerminalDimensionsThrow_LeavesOperationCompletable()
        {
            var pipeline = new TelemetryPipeline();
            var operation = pipeline.StartOperation("Navigation", "page_switch", "op-1");

            Assert.Throws<ArgumentNullException>(
                () => operation.Complete(TelemetryOutcome.Succeeded, new NullKeyDictionary()));

            Assert.False(operation.IsCompleted);
            Assert.Empty(pipeline.GetRecentOperations(10));

            operation.Complete(TelemetryOutcome.Failed);

            var snapshot = pipeline.GetRecentOperations(10).Single();
            Assert.Equal("op-1", snapshot.OperationId);
            Assert.Equal(TelemetryOutcome.Failed, snapshot.Outcome);
        }

        [Fact]
        public void StartOperation_DimensionsThrow_CreatesNoOperation()
        {
            var pipeline = new TelemetryPipeline();

            Assert.Throws<ArgumentNullException>(
                () => pipeline.StartOperation("Navigation", "page_switch", dimensions: new NullKeyDictionary()));

            Assert.Empty(pipeline.GetRecentOperations(10));
        }

        [Fact]
        public void StartOperation_BlankParent_IsNormalizedToNull()
        {
            var pipeline = new TelemetryPipeline();
            pipeline.StartOperation("Navigation", "page_switch", "op-1", "   ")
                .Complete(TelemetryOutcome.Succeeded);

            Assert.Null(pipeline.GetRecentOperations(1).Single().ParentOperationId);
        }

        [Fact]
        public void StartOperation_BlankOperationId_IsReplacedByAGeneratedIdentifier()
        {
            var pipeline = new TelemetryPipeline();

            var generated = pipeline.StartOperation("Navigation", "page_switch", "   ").OperationId;

            Assert.False(string.IsNullOrWhiteSpace(generated));
            Assert.NotEqual("   ", generated);
        }

        [Fact]
        public void StartOperation_BlankModuleOrName_Throws()
        {
            var pipeline = new TelemetryPipeline();

            Assert.Throws<ArgumentException>(() => pipeline.StartOperation("  ", "page_switch"));
            Assert.Throws<ArgumentException>(() => pipeline.StartOperation("Navigation", "  "));
        }

        [Fact]
        public void Complete_TerminalDimensionCollision_OverridesTheInitialValue()
        {
            var pipeline = new TelemetryPipeline();
            var operation = pipeline.StartOperation(
                "Navigation",
                "page_switch",
                dimensions: new Dictionary<string, object>
                {
                    ["shared"] = "initial",
                    ["initial-only"] = 1
                });

            operation.Complete(
                TelemetryOutcome.Succeeded,
                new Dictionary<string, object> { ["shared"] = "terminal" });

            var snapshot = pipeline.GetRecentOperations(1).Single();
            Assert.Equal("terminal", snapshot.Dimensions["shared"]);
            Assert.Equal(1, snapshot.Dimensions["initial-only"]);
        }

        [Fact]
        public void Checkpoint_AfterCompletion_IsIgnoredAndReturnsTheFinalDuration()
        {
            var pipeline = new TelemetryPipeline();
            var operation = pipeline.StartOperation("Navigation", "page_switch");

            operation.Checkpoint("started");
            operation.Complete(TelemetryOutcome.Succeeded);
            var late = operation.Checkpoint("too-late");

            var snapshot = pipeline.GetRecentOperations(1).Single();
            Assert.Single(snapshot.Checkpoints);
            Assert.Equal(snapshot.Duration, late);
        }

        [Fact]
        public async Task Complete_ConcurrentTerminals_RecordExactlyOneOperation()
        {
            for (int round = 0; round < 50; round++)
            {
                var pipeline = new TelemetryPipeline();
                var operation = pipeline.StartOperation("Navigation", "race", "race-" + round);

                using (var start = new ManualResetEventSlim(false))
                {
                    var first = Task.Run(() =>
                    {
                        start.Wait();
                        operation.Complete(TelemetryOutcome.Succeeded);
                    });
                    var second = Task.Run(() =>
                    {
                        start.Wait();
                        operation.Complete(TelemetryOutcome.Failed);
                    });

                    start.Set();
                    await Task.WhenAll(first, second);
                }

                Assert.Single(pipeline.GetRecentOperations(10));
            }
        }

        [Fact]
        public async Task Complete_ConcurrentWriters_GiveEverySinkTheRetainedOrder()
        {
            const int writers = 4;
            const int perWriter = 250;
            const int total = writers * perWriter;

            var first = new RecordingSink();
            var second = new RecordingSink();
            var pipeline = new TelemetryPipeline(
                new TelemetryPipelineOptions { RecentOperationCapacity = total },
                first,
                second);

            var running = new Task[writers];
            for (int w = 0; w < writers; w++)
            {
                int id = w;
                running[w] = Task.Run(() =>
                {
                    for (int i = 0; i < perWriter; i++)
                        pipeline.StartOperation("Test", "n", "w" + id + "-" + i)
                            .Complete(TelemetryOutcome.Succeeded);
                });
            }

            await Task.WhenAll(running);

            var retained = pipeline.GetRecentOperations(int.MaxValue);
            Assert.Equal(total, first.Operations.Count);
            Assert.Equal(total, second.Operations.Count);
            Assert.Equal(total, retained.Count);

            for (int i = 0; i < total; i++)
            {
                Assert.Same(first.Operations[i], second.Operations[i]);
                Assert.Same(first.Operations[i], retained[i]);
            }
        }

        [Fact]
        public void Complete_RetentionPrecedesSinkDispatch()
        {
            var visibility = new SnapshotVisibilitySink();
            var pipeline = new TelemetryPipeline(null, visibility);
            visibility.Pipeline = pipeline;

            Complete(pipeline, "visible");

            Assert.Equal(1, visibility.RetainedWhileWriting);
        }

        [Fact]
        public void GetRecentOperations_NonPositiveOrOversizedLimits_StayBounded()
        {
            var pipeline = new TelemetryPipeline(new TelemetryPipelineOptions
            {
                RecentOperationCapacity = 2
            });

            Complete(pipeline, "one");
            Complete(pipeline, "two");
            Complete(pipeline, "three");

            Assert.Empty(pipeline.GetRecentOperations(0));
            Assert.Empty(pipeline.GetRecentOperations(-5));
            Assert.Single(pipeline.GetRecentOperations(1));
            Assert.Equal(2, pipeline.GetRecentOperations(int.MaxValue).Count);
        }

        [Fact]
        public void TelemetryPipelineOptions_Defaults_AreTheSupportedContract()
            => Assert.Equal(1024, new TelemetryPipelineOptions().RecentOperationCapacity);

        [Fact]
        public void Constructor_OptionsMutatedAfterConstruction_DoNotAffectThePipeline()
        {
            var options = new TelemetryPipelineOptions { RecentOperationCapacity = 2 };
            var pipeline = new TelemetryPipeline(options);

            options.RecentOperationCapacity = 100;
            for (int i = 0; i < 5; i++)
                Complete(pipeline, "n" + i);

            Assert.Equal(2, pipeline.GetRecentOperations(100).Count);
        }

        [Fact]
        public void Constructor_CapacityBelowOne_Throws()
            => Assert.Throws<ArgumentOutOfRangeException>(
                () => new TelemetryPipeline(new TelemetryPipelineOptions
                {
                    RecentOperationCapacity = 0
                }));

        private static void Complete(TelemetryPipeline pipeline, string name)
        {
            var operation = pipeline.StartOperation("Test", name);
            operation.Complete(TelemetryOutcome.Succeeded);
        }

        private sealed class ThrowingSink : ITelemetrySink
        {
            public void Write(TelemetryOperation operation)
                => throw new InvalidOperationException("sink");
        }

        private sealed class RecordingSink : ITelemetrySink
        {
            public List<TelemetryOperation> Operations { get; } =
                new List<TelemetryOperation>();

            public void Write(TelemetryOperation operation)
                => Operations.Add(operation);
        }

        private sealed class SnapshotVisibilitySink : ITelemetrySink
        {
            public TelemetryPipeline Pipeline { get; set; }
            public int RetainedWhileWriting { get; private set; }

            public void Write(TelemetryOperation operation)
                => RetainedWhileWriting = Pipeline.GetRecentOperations(10).Count;
        }

        /// <summary>
        /// A caller-supplied dictionary whose enumeration yields a null key. The
        /// pipeline copies dimensions into a <see cref="Dictionary{TKey,TValue}"/>,
        /// which rejects that key.
        /// </summary>
        private sealed class NullKeyDictionary : IReadOnlyDictionary<string, object>
        {
            public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
            {
                yield return new KeyValuePair<string, object>(null, "value");
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            public int Count => 1;
            public bool ContainsKey(string key) => false;
            public bool TryGetValue(string key, out object value) { value = null; return false; }
            public object this[string key] => null;
            public IEnumerable<string> Keys { get { yield return null; } }
            public IEnumerable<object> Values { get { yield return "value"; } }
        }
    }
}
