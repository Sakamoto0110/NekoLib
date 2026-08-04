using NekoLib.Core.Inspection;
using System;
using System.Linq;
using System.Threading;
using Xunit;

namespace NekoLib.Inspection.Tests.Unit
{
    public sealed class InspectionRuntimeTests
    {
        [Fact]
        public void NullInspection_Operations_DoNotExecuteDelegates()
        {
            var invoked = false;

            NullInspection.Instance.Record("Module", "Operation", () =>
            {
                invoked = true;
                return new object();
            });
            using (NullInspection.Instance.RegisterStateProvider("Module", "state", () =>
            {
                invoked = true;
                return new object();
            })) { }
            using (NullInspection.Instance.RegisterAction("Module", "action", value =>
            {
                invoked = true;
                return value;
            })) { }

            Assert.False(NullInspection.Instance.IsEnabled);
            Assert.False(invoked);
        }

        [Fact]
        public void Record_CapacityExceeded_EvictsOldestAndTracksSequences()
        {
            using var runtime = new InspectionRuntime(new InspectionOptions { Capacity = 2 });

            runtime.Record("Module", "one");
            runtime.Record("Module", "two");
            runtime.Record("Module", "three");

            var operations = runtime.GetOperations();
            var diagnostics = runtime.GetDiagnostics();

            Assert.Equal(new[] { "two", "three" }, operations.Select(x => x.Operation));
            Assert.Equal(new long[] { 2, 3 }, operations.Select(x => x.Sequence));
            Assert.Equal(3, diagnostics.TotalRecorded);
            Assert.Equal(1, diagnostics.EvictedCount);
        }

        [Fact]
        public void Record_PayloadFactoryThrows_CapturesPlaceholder()
        {
            using var runtime = new InspectionRuntime();

            runtime.Record("Module", "operation", () =>
                throw new InvalidOperationException("boom"));

            Assert.Equal("<payload threw: InvalidOperationException>",
                runtime.GetOperations().Single().Payload);
        }

        [Fact]
        public void CaptureSnapshot_ProviderThrows_IsolatesFailure()
        {
            using var runtime = new InspectionRuntime();
            using var good = runtime.RegisterStateProvider("Module", "good", () => 42);
            using var bad = runtime.RegisterStateProvider("Module", "bad", () =>
                throw new InvalidOperationException("boom"));

            var snapshot = runtime.CaptureSnapshot(10, TimeSpan.FromSeconds(1));

            Assert.Equal(42, snapshot.State["Module::good"]);
            Assert.Contains("InvalidOperationException", snapshot.State["Module::bad"].ToString());
        }

        [Fact]
        public void CaptureSnapshot_ProviderExceedsBudget_ReturnsPartialSnapshot()
        {
            using var provider = new BlockingSnapshotProvider();
            using var runtime = new InspectionRuntime();
            using var slow = runtime.RegisterStateProvider("Module", "slow", provider.Capture);

            var snapshot = runtime.CaptureSnapshot(10, TimeSpan.FromMilliseconds(20));
            provider.Release();

            Assert.Equal("<snapshot timed out>", snapshot.State["Module::slow"]);
            Assert.True(provider.WaitForCapture(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public void RegisterStateProvider_Disposed_RemovesProvider()
        {
            using var runtime = new InspectionRuntime();
            var registration = runtime.RegisterStateProvider("Module", "state", () => 1);

            Assert.Equal(new[] { "Module::state" }, runtime.StateKeys());
            registration.Dispose();

            Assert.Empty(runtime.StateKeys());
        }

        [Fact]
        public void RegisterStateProvider_Duplicate_ThrowsWithoutReplacingOwner()
        {
            using var runtime = new InspectionRuntime();
            using var original = runtime.RegisterStateProvider("Module", "state", () => 1);

            Assert.Throws<InvalidOperationException>(() =>
                runtime.RegisterStateProvider("Module", "state", () => 2));

            Assert.Equal(1, runtime.CaptureSnapshot(0, TimeSpan.FromSeconds(1)).State["Module::state"]);
        }

        [Fact]
        public void RegisterAction_InvokeAndDispose_UpdatesActionKeys()
        {
            using var runtime = new InspectionRuntime();
            var registration = runtime.RegisterAction(
                "Module",
                "double",
                value => (int)value * 2);

            Assert.True(runtime.TryInvokeAction("Module", "double", 21, out var result));
            Assert.Equal(42, result);
            Assert.Equal(new[] { "Module::double" }, runtime.ActionKeys());

            registration.Dispose();

            Assert.Empty(runtime.ActionKeys());
            Assert.False(runtime.TryInvokeAction("Module", "double", 1, out _));
        }

        [Fact]
        public void RegisterAction_Duplicate_ThrowsWithoutReplacingOwner()
        {
            using var runtime = new InspectionRuntime();
            using var original = runtime.RegisterAction("Module", "action", value => value);

            Assert.Throws<InvalidOperationException>(() =>
                runtime.RegisterAction("Module", "action", value => null));

            Assert.Equal(1, runtime.GetDiagnostics().ActionCount);
        }

        [Fact]
        public void ClearOperations_MultipleCalls_TracksClearCount()
        {
            using var runtime = new InspectionRuntime();
            runtime.Record("Module", "operation");

            runtime.ClearOperations();
            runtime.ClearOperations();

            Assert.Empty(runtime.GetOperations());
            Assert.Equal(2, runtime.GetDiagnostics().ClearCount);
        }

        [Fact]
        public void Dispose_RegistrationsAndOperations_ClearsStateAndDisablesRuntime()
        {
            var runtime = new InspectionRuntime();
            runtime.Record("Module", "operation");
            runtime.RegisterStateProvider("Module", "state", () => 1);
            runtime.RegisterAction("Module", "action", value => value);

            runtime.Dispose();

            Assert.False(runtime.IsEnabled);
            Assert.Empty(runtime.GetOperations());
            Assert.Empty(runtime.StateKeys());
            Assert.Empty(runtime.ActionKeys());
        }

        [Fact]
        public void EnableGlobal_Dispose_RestoresNullProvider()
        {
            Assert.Same(NullInspection.Instance, InspectionProvider.Current);

            var runtime = InspectionRuntime.EnableGlobal();
            Assert.Same(runtime, InspectionProvider.Current);

            runtime.Dispose();

            Assert.Same(NullInspection.Instance, InspectionProvider.Current);
        }

        [Fact]
        public void EnableGlobal_SecondRuntime_ThrowsAndPreservesOwner()
        {
            using var runtime = InspectionRuntime.EnableGlobal();

            Assert.Throws<InvalidOperationException>(() => InspectionRuntime.EnableGlobal());
            Assert.Same(runtime, InspectionProvider.Current);
        }

        private sealed class BlockingSnapshotProvider : IDisposable
        {
            private readonly ManualResetEventSlim _release = new ManualResetEventSlim(false);
            private readonly ManualResetEventSlim _snapshotCompleted = new ManualResetEventSlim(false);

            public object Capture()
            {
                _release.Wait();
                _snapshotCompleted.Set();
                return 42;
            }

            public void Release()
            {
                _release.Set();
            }

            public bool WaitForCapture(TimeSpan timeout)
            {
                return _snapshotCompleted.Wait(timeout);
            }

            public void Dispose()
            {
                _snapshotCompleted.Dispose();
                _release.Dispose();
            }
        }
    }
}
