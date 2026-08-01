using NekoLib.Core.Telemetry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    }
}
