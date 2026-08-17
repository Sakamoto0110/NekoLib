using NekoLib.Core.Inspection;
using NekoLib.Core.Logging;
using NekoLib.Core.Telemetry;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NekoLib.Core.Tests.Unit
{
    public sealed class CapabilityContractTests
    {
        [Fact]
        public void LogEntry_Construction_PreservesStructuredValues()
        {
            var timestamp = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
            var exception = new InvalidOperationException("boom");

            var entry = new LogEntry(
                timestamp,
                LogLevel.Error,
                "failed",
                exception,
                "Navigation");

            Assert.Equal(timestamp, entry.TimestampUtc);
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.Equal("Navigation", entry.Category);
            Assert.Equal("failed", entry.Message);
            Assert.Same(exception, entry.Exception);
        }

        [Fact]
        public void LogEntry_BoxedAsObject_UsesStructuredFormatting()
        {
            object entry = new LogEntry(
                new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LogLevel.Info,
                "ready",
                category: "Application");

            var formatted = entry.ToString();

            Assert.Contains("Info", formatted);
            Assert.Contains("[Application]", formatted);
            Assert.Contains("ready", formatted);
        }

        [Fact]
        public void TelemetryOperation_Type_IsIndependentFromLogEntry()
        {
            Assert.False(typeof(LogEntry).IsAssignableFrom(typeof(TelemetryOperation)));
        }

        [Fact]
        public void TelemetryOperation_Construction_PreservesOperationModel()
        {
            var checkpoint = new TelemetryCheckpoint("authenticated", TimeSpan.FromMilliseconds(4));
            var operation = new TelemetryOperation(
                DateTime.UtcNow,
                "Navigation",
                "page_switch",
                "operation",
                "parent",
                TelemetryOutcome.Succeeded,
                TimeSpan.FromMilliseconds(10),
                new[] { checkpoint },
                new Dictionary<string, object> { ["target"] = "Catalog" },
                new Dictionary<string, double> { ["page_switch.total_ms"] = 10 });

            Assert.Equal("Navigation", operation.Module);
            Assert.Equal("operation", operation.OperationId);
            Assert.Equal("parent", operation.ParentOperationId);
            Assert.Same(checkpoint, operation.Checkpoints.Single());
        }

        [Fact]
        public void TelemetryCheckpoint_SourceDimensionsMutated_PreservesOuterSnapshot()
        {
            var dimensions = new Dictionary<string, object>
            {
                ["value"] = 1
            };
            var checkpoint = new TelemetryCheckpoint(
                "ready",
                TimeSpan.FromMilliseconds(2),
                dimensions);

            dimensions["value"] = 2;
            dimensions["added"] = true;

            Assert.Equal(1, checkpoint.Dimensions["value"]);
            Assert.False(checkpoint.Dimensions.ContainsKey("added"));
            var exposed = Assert.IsAssignableFrom<IDictionary<string, object>>(
                checkpoint.Dimensions);
            Assert.Throws<NotSupportedException>(() => exposed["value"] = 3);
        }

        [Fact]
        public void TelemetryOperation_SourceCollectionsMutated_PreservesOuterSnapshotAndShallowValues()
        {
            var checkpoint = new TelemetryCheckpoint(
                "ready",
                TimeSpan.FromMilliseconds(2));
            var replacement = new TelemetryCheckpoint(
                "replacement",
                TimeSpan.FromMilliseconds(3));
            var payload = new object();
            var checkpoints = new List<TelemetryCheckpoint> { checkpoint };
            var dimensions = new Dictionary<string, object> { ["payload"] = payload };
            var measurements = new Dictionary<string, double> { ["total_ms"] = 4 };

            var operation = new TelemetryOperation(
                DateTime.UtcNow,
                "Core",
                "contract",
                "operation",
                null,
                TelemetryOutcome.Succeeded,
                TimeSpan.FromMilliseconds(4),
                checkpoints,
                dimensions,
                measurements);

            checkpoints[0] = replacement;
            dimensions["payload"] = new object();
            measurements["total_ms"] = 8;

            Assert.Same(checkpoint, operation.Checkpoints.Single());
            Assert.Same(payload, operation.Dimensions["payload"]);
            Assert.Equal(4, operation.Measurements["total_ms"]);
            var exposedCheckpoints = Assert.IsAssignableFrom<IList<TelemetryCheckpoint>>(
                operation.Checkpoints);
            var exposedDimensions = Assert.IsAssignableFrom<IDictionary<string, object>>(
                operation.Dimensions);
            var exposedMeasurements = Assert.IsAssignableFrom<IDictionary<string, double>>(
                operation.Measurements);
            Assert.Throws<NotSupportedException>(() => exposedCheckpoints[0] = replacement);
            Assert.Throws<NotSupportedException>(() => exposedDimensions["payload"] = new object());
            Assert.Throws<NotSupportedException>(() => exposedMeasurements["total_ms"] = 8);
        }

        [Fact]
        public void TelemetryModels_EmptyCollections_RejectMutationWithoutSharedContamination()
        {
            var checkpoint = new TelemetryCheckpoint("ready", TimeSpan.Zero);
            var operation = new TelemetryOperation(
                DateTime.UtcNow,
                "Core",
                "contract",
                "operation",
                null,
                TelemetryOutcome.Succeeded,
                TimeSpan.Zero);

            var checkpointDimensions = Assert.IsAssignableFrom<IDictionary<string, object>>(
                checkpoint.Dimensions);
            var operationCheckpoints = Assert.IsAssignableFrom<IList<TelemetryCheckpoint>>(
                operation.Checkpoints);
            var operationDimensions = Assert.IsAssignableFrom<IDictionary<string, object>>(
                operation.Dimensions);
            var operationMeasurements = Assert.IsAssignableFrom<IDictionary<string, double>>(
                operation.Measurements);

            Assert.Throws<NotSupportedException>(() => checkpointDimensions.Add("value", 1));
            Assert.Throws<NotSupportedException>(() => operationCheckpoints.Add(checkpoint));
            Assert.Throws<NotSupportedException>(() => operationDimensions.Add("value", 1));
            Assert.Throws<NotSupportedException>(() => operationMeasurements.Add("value", 1));
            Assert.Empty(new TelemetryCheckpoint("later", TimeSpan.Zero).Dimensions);
        }

        [Fact]
        public void InspectionSnapshot_SourceCollectionsMutated_PreservesOuterSnapshotAndShallowValues()
        {
            var payload = new object();
            var operation = new InspectionOperation(
                1,
                DateTime.UtcNow,
                "Core",
                "contract",
                null);
            var replacement = new InspectionOperation(
                2,
                DateTime.UtcNow,
                "Core",
                "replacement",
                null);
            var operations = new List<InspectionOperation> { operation };
            var state = new Dictionary<string, object> { ["payload"] = payload };

            var snapshot = new InspectionSnapshot(
                DateTime.UtcNow,
                operations,
                state,
                10,
                1,
                0);

            operations[0] = replacement;
            state["payload"] = new object();

            Assert.Same(operation, snapshot.Operations.Single());
            Assert.Same(payload, snapshot.State["payload"]);
            var exposedOperations = Assert.IsAssignableFrom<IList<InspectionOperation>>(
                snapshot.Operations);
            var exposedState = Assert.IsAssignableFrom<IDictionary<string, object>>(
                snapshot.State);
            Assert.Throws<NotSupportedException>(() => exposedOperations[0] = replacement);
            Assert.Throws<NotSupportedException>(() => exposedState["payload"] = new object());
        }

        [Fact]
        public void InspectionSnapshotSource_Surface_DoesNotExposeActions()
        {
            var methodNames = typeof(IInspectionSnapshotSource)
                .GetMethods()
                .Select(method => method.Name)
                .ToArray();

            Assert.Equal(new[] { "CaptureSnapshot" }, methodNames);
        }

        [Fact]
        public void RegisterAction_CompiledContract_IsExplicitlyExperimental()
        {
            var method = typeof(IInspectionRecorder).GetMethod("RegisterAction");
            var attribute = Assert.IsType<ObsoleteAttribute>(
                Attribute.GetCustomAttribute(method, typeof(ObsoleteAttribute)));

            Assert.Equal(
                "Experimental API NEKOEXP0001: compatibility is not guaranteed.",
                attribute.Message);
            Assert.False(attribute.IsError);
        }

        [Fact]
        public void InspectionProvider_InstallationDisposed_RestoresNullWithoutOwningRecorder()
        {
            Assert.Same(NullInspection.Instance, InspectionProvider.Current);
            var recorder = new EnabledInspectionRecorder();
            var installation = InspectionProvider.Install(recorder);

            try
            {
                Assert.Same(recorder, InspectionProvider.Current);
                Assert.Throws<InvalidOperationException>(() =>
                    InspectionProvider.Install(new EnabledInspectionRecorder()));
            }
            finally
            {
                installation.Dispose();
            }

            installation.Dispose();
            Assert.Same(NullInspection.Instance, InspectionProvider.Current);
            Assert.False(recorder.DisposeCalled);
            recorder.Dispose();
            Assert.True(recorder.DisposeCalled);
        }

        [Fact]
        public void NullImplementations_DefaultOperations_AreSafe()
        {
            NullLogger.Instance.Info("ignored");
            var telemetry = NullTelemetry.Instance.StartOperation("Test", "ignored");
            telemetry.Checkpoint("checkpoint");
            telemetry.Complete(TelemetryOutcome.Succeeded);
            NullInspection.Instance.Record("Test", "ignored", () =>
                throw new InvalidOperationException("must not execute"));

            Assert.Empty(NullLogger.Instance.GetRecentEntries(1));
            Assert.Empty(NullTelemetry.Instance.GetRecentOperations(1));
            Assert.False(NullInspection.Instance.IsEnabled);
        }

        [Fact]
        public void NullImplementations_RepeatedAccess_ReturnsSharedCompletedDefaults()
        {
            var firstOperation = NullTelemetry.Instance.StartOperation("Test", "first");
            var secondOperation = NullTelemetry.Instance.StartOperation("Test", "second");
            var stateRegistration = NullInspection.Instance.RegisterStateProvider(
                "Test",
                "state",
                () => throw new InvalidOperationException("must not execute"));

            Assert.Same(NullLogger.Instance, NullLogger.Instance);
            Assert.Same(NullTelemetry.Instance, NullTelemetry.Instance);
            Assert.Same(NullInspection.Instance, NullInspection.Instance);
            Assert.Same(firstOperation, secondOperation);
            Assert.True(firstOperation.IsCompleted);
            Assert.Equal(string.Empty, firstOperation.OperationId);
            Assert.Same(NekoLib.Core.Disposable.Empty, stateRegistration);

            stateRegistration.Dispose();
            stateRegistration.Dispose();
            NekoLib.Core.Disposable.Empty.Dispose();
        }

        private sealed class EnabledInspectionRecorder : IInspectionRecorder, IDisposable
        {
            public bool IsEnabled => !DisposeCalled;
            public bool DisposeCalled { get; private set; }

            public void Record(string module, string operation, Func<object> payload = null)
            {
            }

            public IDisposable RegisterStateProvider(
                string module,
                string key,
                Func<object> snapshot)
                => NekoLib.Core.Disposable.Empty;

#pragma warning disable CS0618 // Test implementation of experimental contract.
            public IDisposable RegisterAction(
                string module,
                string name,
                Func<object, object> action)
                => NekoLib.Core.Disposable.Empty;
#pragma warning restore CS0618

            public void Dispose()
            {
                DisposeCalled = true;
            }
        }
    }
}
