using NekoLib.Core.Inspection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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
#pragma warning disable CS0618 // Deliberate coverage of the experimental Core action boundary.
            using (NullInspection.Instance.RegisterAction("Module", "action", value =>
            {
                invoked = true;
                return value;
            })) { }
#pragma warning restore CS0618

            Assert.False(NullInspection.Instance.IsEnabled);
            Assert.False(invoked);
        }

        [Fact]
        public void Constructor_DefaultOptions_UsesDocumentedCapacity()
        {
            using var runtime = new InspectionRuntime();

            Assert.Equal(1024, runtime.GetDiagnostics().Capacity);
        }

        [Fact]
        public void Constructor_CapacityBelowOne_ThrowsWithCapacityParameter()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                new InspectionRuntime(new InspectionOptions { Capacity = 0 }));

            Assert.Equal(nameof(InspectionOptions.Capacity), exception.ParamName);
        }

        [Fact]
        public void Constructor_OptionsChangedAfterConstruction_KeepsOriginalCapacity()
        {
            var options = new InspectionOptions { Capacity = 2 };
            using var runtime = new InspectionRuntime(options);

            options.Capacity = 10;
            runtime.Record("Module", "one");
            runtime.Record("Module", "two");
            runtime.Record("Module", "three");

            Assert.Equal(2, runtime.GetDiagnostics().Capacity);
            Assert.Equal(2, runtime.GetOperations().Count);
        }

        [Fact]
        public void Record_InvalidIdentifiers_ThrowWithTheirParameterNames()
        {
            using var runtime = new InspectionRuntime();

            Assert.Equal("module", Assert.Throws<ArgumentNullException>(() =>
                runtime.Record(null, "operation")).ParamName);
            Assert.Equal("module", Assert.Throws<ArgumentException>(() =>
                runtime.Record(" ", "operation")).ParamName);
            Assert.Equal("module", Assert.Throws<ArgumentException>(() =>
                runtime.Record("Module::Nested", "operation")).ParamName);
            Assert.Equal("operation", Assert.Throws<ArgumentNullException>(() =>
                runtime.Record("Module", null)).ParamName);
            Assert.Equal("operation", Assert.Throws<ArgumentException>(() =>
                runtime.Record("Module", " ")).ParamName);
        }

        [Fact]
        public void Record_Disposed_DoesNotEvaluatePayload()
        {
            var invoked = false;
            var runtime = new InspectionRuntime();
            runtime.Dispose();

            runtime.Record("Module", "operation", () =>
            {
                invoked = true;
                return 42;
            });

            Assert.False(invoked);
        }

        [Fact]
        public void Record_NullAndObjectPayloads_PreserveShallowValues()
        {
            var payload = new object();
            using var runtime = new InspectionRuntime();

            runtime.Record("Module", "null");
            runtime.Record("Module", "object", () => payload);

            var operations = runtime.GetOperations();
            Assert.Null(operations[0].Payload);
            Assert.Same(payload, operations[1].Payload);
        }

        [Fact]
        public async Task Record_PayloadCompletesLater_CommitsByCompletionSequence()
        {
            using var payloadStarted = new ManualResetEventSlim(false);
            using var releasePayload = new ManualResetEventSlim(false);
            using var runtime = new InspectionRuntime();

            var slowRecord = Task.Run(() => runtime.Record("Module", "slow", () =>
            {
                payloadStarted.Set();
                releasePayload.Wait();
                return "slow";
            }));

            try
            {
                await AssertSignaledAsync(payloadStarted);
                runtime.Record("Module", "fast", () => "fast");
            }
            finally
            {
                releasePayload.Set();
                await slowRecord;
            }

            var operations = runtime.GetOperations();
            Assert.Equal(new[] { "fast", "slow" }, operations.Select(x => x.Operation));
            Assert.Equal(new long[] { 1, 2 }, operations.Select(x => x.Sequence));
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
        public void CaptureSnapshot_NullProviderResult_UsesNullMarker()
        {
            using var runtime = new InspectionRuntime();
            using var registration = runtime.RegisterStateProvider("Module", "state", () => null);

            var snapshot = runtime.CaptureSnapshot(0, TimeSpan.FromSeconds(1));

            Assert.Equal("<null>", snapshot.State["Module::state"]);
        }

        [Fact]
        public void CaptureSnapshot_ProvidersAndKeys_UseRegistrationOrder()
        {
            var invocations = new List<string>();
            using var runtime = new InspectionRuntime();
            using var first = runtime.RegisterStateProvider("Module", "zeta", () =>
            {
                invocations.Add("zeta");
                return 1;
            });
            using var second = runtime.RegisterStateProvider("Module", "alpha", () =>
            {
                invocations.Add("alpha");
                return 2;
            });

            var snapshot = runtime.CaptureSnapshot(0, TimeSpan.FromSeconds(1));

            Assert.Equal(new[] { "Module::zeta", "Module::alpha" }, runtime.StateKeys());
            Assert.Equal(new[] { "zeta", "alpha" }, invocations);
            Assert.Equal(1, snapshot.State["Module::zeta"]);
            Assert.Equal(2, snapshot.State["Module::alpha"]);
        }

        [Fact]
        public void CaptureSnapshot_RepeatedTimeouts_ShareOutstandingProviderInvocation()
        {
            using var provider = new CountingBlockingSnapshotProvider();
            using var runtime = new InspectionRuntime();
            using var registration = runtime.RegisterStateProvider("Module", "slow", provider.Capture);

            var first = runtime.CaptureSnapshot(0, TimeSpan.FromMilliseconds(50));
            Assert.True(provider.WaitForCaptureStarted(TimeSpan.FromSeconds(5)));
            var second = runtime.CaptureSnapshot(0, TimeSpan.FromMilliseconds(50));

            Assert.Equal("<snapshot timed out>", first.State["Module::slow"]);
            Assert.Equal("<snapshot timed out>", second.State["Module::slow"]);
            Assert.Equal(1, provider.InvocationCount);

            provider.Release();
            Assert.True(provider.WaitForCaptureCompleted(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public void CaptureSnapshot_SharedBudgetExpires_SkipsLaterProviders()
        {
            var skippedInvocations = 0;
            using var provider = new CountingBlockingSnapshotProvider();
            using var runtime = new InspectionRuntime();
            using var slow = runtime.RegisterStateProvider("Module", "slow", provider.Capture);
            using var skipped = runtime.RegisterStateProvider("Module", "skipped", () =>
            {
                Interlocked.Increment(ref skippedInvocations);
                return 2;
            });

            var snapshot = runtime.CaptureSnapshot(0, TimeSpan.FromMilliseconds(50));

            Assert.Equal("<snapshot timed out>", snapshot.State["Module::slow"]);
            Assert.Equal("<snapshot timed out>", snapshot.State["Module::skipped"]);
            Assert.Equal(0, Volatile.Read(ref skippedInvocations));

            provider.Release();
            Assert.True(provider.WaitForCaptureCompleted(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public void CaptureSnapshot_LateProviderFailure_DoesNotPoisonLaterCapture()
        {
            using var provider = new FirstInvocationBlockingFailureProvider();
            using var runtime = new InspectionRuntime();
            using var registration = runtime.RegisterStateProvider("Module", "state", provider.Capture);

            var timedOut = runtime.CaptureSnapshot(0, TimeSpan.FromMilliseconds(50));
            Assert.True(provider.WaitForCaptureStarted(TimeSpan.FromSeconds(5)));
            provider.ReleaseFailure();
            Assert.True(provider.WaitForFailureCompleted(TimeSpan.FromSeconds(5)));

            runtime.CaptureSnapshot(0, TimeSpan.FromSeconds(1));
            var recovered = runtime.CaptureSnapshot(0, TimeSpan.FromSeconds(1));

            Assert.Equal("<snapshot timed out>", timedOut.State["Module::state"]);
            Assert.Equal(42, recovered.State["Module::state"]);
            Assert.InRange(provider.InvocationCount, 2, 3);
        }

        [Fact]
        public async Task CaptureSnapshot_ProviderCompletesAfterCopy_UsesOriginalOperationsAndCompletionTime()
        {
            using var provider = new TimestampBlockingSnapshotProvider();
            using var runtime = new InspectionRuntime();
            runtime.Record("Module", "before");
            using var registration = runtime.RegisterStateProvider("Module", "state", provider.Capture);

            var captureTask = Task.Run(() =>
                runtime.CaptureSnapshot(10, TimeSpan.FromSeconds(5)));

            try
            {
                await AssertSignaledAsync(provider.CaptureStarted);
                runtime.Record("Module", "after");
            }
            finally
            {
                provider.Release();
            }

            var snapshot = await captureTask;
            Assert.Equal(new[] { "before" }, snapshot.Operations.Select(x => x.Operation));
            Assert.True(snapshot.CapturedUtc >= provider.CompletedUtc);
        }

        [Fact]
        public async Task CaptureState_BlockingProvider_WaitsWithoutSnapshotBudget()
        {
            using var provider = new CountingBlockingSnapshotProvider();
            using var runtime = new InspectionRuntime();
            using var registration = runtime.RegisterStateProvider("Module", "state", provider.Capture);

            var captureTask = Task.Run(() => runtime.CaptureState());

            try
            {
                await AssertSignaledAsync(provider.CaptureStarted);
                Assert.False(captureTask.IsCompleted);
            }
            finally
            {
                provider.Release();
            }

            var state = await captureTask;
            Assert.Equal(42, state["Module::state"]);
        }

        [Fact]
        public void CaptureSnapshot_InvalidArguments_ThrowWithParameterNames()
        {
            using var runtime = new InspectionRuntime();

            Assert.Equal("maxOperations", Assert.Throws<ArgumentOutOfRangeException>(() =>
                runtime.CaptureSnapshot(-1, TimeSpan.Zero)).ParamName);
            Assert.Equal("timeout", Assert.Throws<ArgumentOutOfRangeException>(() =>
                runtime.CaptureSnapshot(0, TimeSpan.FromTicks(-1))).ParamName);
        }

        [Fact]
        public void CaptureSnapshot_MaxOperations_ReturnsNewestTail()
        {
            using var runtime = new InspectionRuntime();
            runtime.Record("Module", "one");
            runtime.Record("Module", "two");
            runtime.Record("Module", "three");

            var snapshot = runtime.CaptureSnapshot(2, TimeSpan.Zero);

            Assert.Equal(new[] { "two", "three" }, snapshot.Operations.Select(x => x.Operation));
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
        public void RegisterStateProvider_InvalidIdentity_ThrowsWithParameterName()
        {
            using var runtime = new InspectionRuntime();

            Assert.Equal("module", Assert.Throws<ArgumentException>(() =>
                runtime.RegisterStateProvider(" ", "state", () => 1)).ParamName);
            Assert.Equal("module", Assert.Throws<ArgumentException>(() =>
                runtime.RegisterStateProvider("Module::Nested", "state", () => 1)).ParamName);
            Assert.Equal("key", Assert.Throws<ArgumentException>(() =>
                runtime.RegisterStateProvider("Module", " ", () => 1)).ParamName);
            Assert.Equal("key", Assert.Throws<ArgumentException>(() =>
                runtime.RegisterStateProvider("Module", "state::nested", () => 1)).ParamName);
        }

        [Fact]
        public void RegisterStateProvider_Identity_IsOrdinalAndCaseSensitive()
        {
            using var runtime = new InspectionRuntime();
            using var upper = runtime.RegisterStateProvider("Module", "State", () => 1);
            using var lower = runtime.RegisterStateProvider("module", "state", () => 2);

            Assert.Equal(new[] { "Module::State", "module::state" }, runtime.StateKeys());
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
        public void RegisterStateProvider_StaleHandle_DoesNotRemoveLaterOwner()
        {
            using var runtime = new InspectionRuntime();
            var original = runtime.RegisterStateProvider("Module", "state", () => 1);
            original.Dispose();
            using var replacement = runtime.RegisterStateProvider("Module", "state", () => 2);

            original.Dispose();

            Assert.Equal(2, runtime.CaptureState()["Module::state"]);
        }

        [Fact]
        public async Task CaptureSnapshot_CopiedProviderMayFinishAfterUnregisterAndDispose()
        {
            using var provider = new CountingBlockingSnapshotProvider();
            var runtime = new InspectionRuntime();
            var registration = runtime.RegisterStateProvider("Module", "state", provider.Capture);
            var captureTask = Task.Run(() =>
                runtime.CaptureSnapshot(0, TimeSpan.FromSeconds(5)));

            try
            {
                await AssertSignaledAsync(provider.CaptureStarted);
                registration.Dispose();
                runtime.Dispose();
            }
            finally
            {
                provider.Release();
            }

            var snapshot = await captureTask;
            Assert.Equal(42, snapshot.State["Module::state"]);
        }

        [Fact]
#pragma warning disable CS0618 // Deliberate coverage of the experimental action boundary.
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
#pragma warning restore CS0618

        [Fact]
#pragma warning disable CS0618 // Deliberate coverage of the experimental action boundary.
        public void RegisterAction_Duplicate_ThrowsWithoutReplacingOwner()
        {
            using var runtime = new InspectionRuntime();
            using var original = runtime.RegisterAction("Module", "action", value => value);

            Assert.Throws<InvalidOperationException>(() =>
                runtime.RegisterAction("Module", "action", value => null));

            Assert.Equal(1, runtime.GetDiagnostics().ActionCount);
        }
#pragma warning restore CS0618

        [Fact]
#pragma warning disable CS0618 // Deliberate coverage of experimental identity and ordering behavior.
        public void RegisterAction_IdentityAndKeys_AreOrdinalAndRegistrationOrdered()
        {
            using var runtime = new InspectionRuntime();
            using var first = runtime.RegisterAction("Module", "zeta", value => value);
            using var second = runtime.RegisterAction("module", "alpha", value => value);

            Assert.Equal(new[] { "Module::zeta", "module::alpha" }, runtime.ActionKeys());
            Assert.True(runtime.TryInvokeAction("Module", "zeta", 1, out var result));
            Assert.Equal(1, result);
            Assert.False(runtime.TryInvokeAction("MODULE", "zeta", 1, out _));
        }
#pragma warning restore CS0618

        [Fact]
#pragma warning disable CS0618 // Deliberate coverage of experimental identifier validation.
        public void RegisterAction_InvalidIdentity_ThrowsWithParameterName()
        {
            using var runtime = new InspectionRuntime();

            Assert.Equal("module", Assert.Throws<ArgumentException>(() =>
                runtime.RegisterAction(" ", "action", value => value)).ParamName);
            Assert.Equal("module", Assert.Throws<ArgumentException>(() =>
                runtime.TryInvokeAction("Module::Nested", "action", null, out _)).ParamName);
            Assert.Equal("name", Assert.Throws<ArgumentException>(() =>
                runtime.RegisterAction("Module", " ", value => value)).ParamName);
            Assert.Equal("name", Assert.Throws<ArgumentException>(() =>
                runtime.TryInvokeAction("Module", "action::nested", null, out _)).ParamName);
        }
#pragma warning restore CS0618

        [Fact]
        public void ExperimentalMembers_HaveExactCompatibilityMarker()
        {
            var members = new MemberInfo[]
            {
                typeof(InspectionRuntime).GetMethod("RegisterAction"),
                typeof(InspectionRuntime).GetMethod("TryInvokeAction"),
                typeof(InspectionRuntime).GetMethod("ActionKeys"),
                typeof(InspectionRuntimeDiagnostics).GetProperty("ActionCount")
            };

            foreach (var member in members)
            {
                var marker = member.GetCustomAttribute<ObsoleteAttribute>();
                Assert.NotNull(marker);
                Assert.Equal(
                    "Experimental API NEKOEXP0001: compatibility is not guaranteed.",
                    marker.Message);
                Assert.False(marker.IsError);
            }
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
        public void ClearOperations_PreservesTotalsEvictionsAndSequence()
        {
            using var runtime = new InspectionRuntime(new InspectionOptions { Capacity = 2 });
            runtime.Record("Module", "one");
            runtime.Record("Module", "two");
            runtime.Record("Module", "three");

            runtime.ClearOperations();
            runtime.Record("Module", "four");

            var diagnostics = runtime.GetDiagnostics();
            var operation = runtime.GetOperations().Single();
            Assert.Equal(4, diagnostics.TotalRecorded);
            Assert.Equal(1, diagnostics.EvictedCount);
            Assert.Equal(1, diagnostics.ClearCount);
            Assert.Equal(4, operation.Sequence);
        }

        [Fact]
        public void ClearOperations_AfterDispose_IsInert()
        {
            var runtime = new InspectionRuntime();
            runtime.ClearOperations();
            runtime.Dispose();

            runtime.ClearOperations();

            Assert.Equal(1, runtime.GetDiagnostics().ClearCount);
        }

        [Fact]
#pragma warning disable CS0618 // Disposal covers the experimental action registry too.
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
#pragma warning restore CS0618

        [Fact]
        public void Dispose_PassiveMethods_ReturnDisabledEmptyState()
        {
            var runtime = new InspectionRuntime();
            runtime.Record("Module", "operation");
            runtime.Dispose();

            using var ignored = runtime.RegisterStateProvider("Module", "state", () => 42);
            runtime.Record("Module", "ignored");

            Assert.Empty(runtime.GetOperations());
            Assert.Empty(runtime.StateKeys());
            Assert.Empty(runtime.CaptureState());
            Assert.Empty(runtime.CaptureSnapshot(10, TimeSpan.Zero).Operations);
            Assert.False(runtime.GetDiagnostics().IsEnabled);
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

        [Fact]
        public void EnableGlobal_DisposedAfterProviderInstall_RollsBackProvider()
        {
            Assert.Same(NullInspection.Instance, InspectionProvider.Current);

            Assert.Throws<ObjectDisposedException>(() => InspectionRuntime.EnableGlobal(null, () =>
            {
                var installed = Assert.IsType<InspectionRuntime>(InspectionProvider.Current);
                installed.Dispose();
            }));

            Assert.Same(NullInspection.Instance, InspectionProvider.Current);
        }

        private static async Task AssertSignaledAsync(ManualResetEventSlim signal)
        {
            var signaled = await Task.Run(() => signal.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(signaled);
        }

        private sealed class CountingBlockingSnapshotProvider : IDisposable
        {
            private readonly ManualResetEventSlim _release = new ManualResetEventSlim(false);
            private readonly ManualResetEventSlim _captureCompleted = new ManualResetEventSlim(false);
            private int _invocationCount;

            public ManualResetEventSlim CaptureStarted { get; } = new ManualResetEventSlim(false);

            public int InvocationCount => Volatile.Read(ref _invocationCount);

            public object Capture()
            {
                Interlocked.Increment(ref _invocationCount);
                CaptureStarted.Set();
                _release.Wait();
                _captureCompleted.Set();
                return 42;
            }

            public void Release()
            {
                _release.Set();
            }

            public bool WaitForCaptureStarted(TimeSpan timeout)
            {
                return CaptureStarted.Wait(timeout);
            }

            public bool WaitForCaptureCompleted(TimeSpan timeout)
            {
                return _captureCompleted.Wait(timeout);
            }

            public void Dispose()
            {
                CaptureStarted.Dispose();
                _captureCompleted.Dispose();
                _release.Dispose();
            }
        }

        private sealed class FirstInvocationBlockingFailureProvider : IDisposable
        {
            private readonly ManualResetEventSlim _captureStarted = new ManualResetEventSlim(false);
            private readonly ManualResetEventSlim _releaseFailure = new ManualResetEventSlim(false);
            private readonly ManualResetEventSlim _failureCompleted = new ManualResetEventSlim(false);
            private int _invocationCount;

            public int InvocationCount => Volatile.Read(ref _invocationCount);

            public object Capture()
            {
                if (Interlocked.Increment(ref _invocationCount) == 1)
                {
                    _captureStarted.Set();
                    _releaseFailure.Wait();
                    _failureCompleted.Set();
                    throw new InvalidOperationException("late failure");
                }

                return 42;
            }

            public bool WaitForCaptureStarted(TimeSpan timeout)
            {
                return _captureStarted.Wait(timeout);
            }

            public void ReleaseFailure()
            {
                _releaseFailure.Set();
            }

            public bool WaitForFailureCompleted(TimeSpan timeout)
            {
                return _failureCompleted.Wait(timeout);
            }

            public void Dispose()
            {
                _captureStarted.Dispose();
                _releaseFailure.Dispose();
                _failureCompleted.Dispose();
            }
        }

        private sealed class TimestampBlockingSnapshotProvider : IDisposable
        {
            private readonly ManualResetEventSlim _release = new ManualResetEventSlim(false);

            public ManualResetEventSlim CaptureStarted { get; } = new ManualResetEventSlim(false);
            public DateTime CompletedUtc { get; private set; }

            public object Capture()
            {
                CaptureStarted.Set();
                _release.Wait();
                CompletedUtc = DateTime.UtcNow;
                return 42;
            }

            public void Release()
            {
                _release.Set();
            }

            public void Dispose()
            {
                CaptureStarted.Dispose();
                _release.Dispose();
            }
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
