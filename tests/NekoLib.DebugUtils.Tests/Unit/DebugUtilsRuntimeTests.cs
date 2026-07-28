using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Core.Observability;
using Xunit;

namespace NekoLib.DebugUtils.Tests.Unit
{
    public sealed class DebugUtilsRuntimeTests
    {
        [Fact]
        public void NullDebugUtils_Operations_DoNotExecutePayloads()
        {
            bool payloadInvoked = false;
            bool providerInvoked = false;
            bool commandInvoked = false;

            NullDebugUtils.Instance.Record(
                "Module",
                "Operation",
                () =>
                {
                    payloadInvoked = true;
                    return new object();
                });

            using (NullDebugUtils.Instance.RegisterStateProvider(
                       "Module",
                       "state",
                       () =>
                       {
                           providerInvoked = true;
                           return new object();
                       }))
            using (NullDebugUtils.Instance.RegisterCommand(
                       "Module",
                       "command",
                       argument =>
                       {
                           commandInvoked = true;
                           return argument;
                       }))
            {
            }

            Assert.False(NullDebugUtils.Instance.IsEnabled);
            Assert.False(payloadInvoked);
            Assert.False(providerInvoked);
            Assert.False(commandInvoked);
        }

        [Fact]
        public void Record_OverCapacity_EvictsOldestOperations()
        {
            using var runtime = new DebugUtilsRuntime(
                new DebugUtilsOptions { Capacity = 2 });

            runtime.Record("M", "first");
            runtime.Record("M", "second");
            runtime.Record("M", "third");

            Assert.Equal(
                new[] { "second", "third" },
                runtime.GetOperations().Select(operation => operation.Operation));

            var diagnostics = runtime.GetDiagnostics();
            Assert.True(diagnostics.IsEnabled);
            Assert.Equal(2, diagnostics.Capacity);
            Assert.Equal(2, diagnostics.RetainedCount);
            Assert.Equal(3, diagnostics.TotalRecorded);
            Assert.Equal(1, diagnostics.EvictedCount);
            Assert.Equal(0, diagnostics.ClearCount);
            Assert.Equal(2, diagnostics.OldestSequence);
            Assert.Equal(3, diagnostics.NewestSequence);
        }

        [Fact]
        public void ClearOperations_AfterRecords_EmptiesBuffer()
        {
            using var runtime = new DebugUtilsRuntime();
            runtime.Record("M", "one");
            runtime.Record("M", "two");

            runtime.ClearOperations();

            Assert.Empty(runtime.GetOperations());

            var diagnostics = runtime.GetDiagnostics();
            Assert.Equal(0, diagnostics.RetainedCount);
            Assert.Equal(2, diagnostics.TotalRecorded);
            Assert.Equal(0, diagnostics.EvictedCount);
            Assert.Equal(1, diagnostics.ClearCount);
            Assert.Null(diagnostics.OldestSequence);
            Assert.Null(diagnostics.NewestSequence);

            runtime.Record("M", "three");

            var next = Assert.Single(runtime.GetOperations());
            Assert.Equal(3, next.Sequence);
        }

        [Fact]
        public void Record_MultipleOperations_AssignsMonotonicSequence()
        {
            using var runtime = new DebugUtilsRuntime();

            runtime.Record("M", "one");
            runtime.Record("M", "two");
            runtime.Record("M", "three");

            Assert.Equal(
                new long[] { 1, 2, 3 },
                runtime.GetOperations().Select(operation => operation.Sequence));

            var legacy = new DebugOperation(
                DateTime.UtcNow,
                "M",
                "legacy",
                null);
            Assert.Equal(0, legacy.Sequence);
        }

        [Fact]
        public void RegisterCommand_InvokeAndDispose_UpdatesCommandKeys()
        {
            using var runtime = new DebugUtilsRuntime();
            var registration = runtime.RegisterCommand(
                "Module",
                "double",
                argument => (int)argument * 2);

            Assert.Equal(new[] { "Module::double" }, runtime.CommandKeys());
            Assert.True(runtime.TryInvokeCommand(
                "Module",
                "double",
                21,
                out var result));
            Assert.Equal(42, result);

            registration.Dispose();

            Assert.Empty(runtime.CommandKeys());
            Assert.False(runtime.TryInvokeCommand(
                "Module",
                "double",
                21,
                out result));
            Assert.Null(result);
        }

        [Fact]
        public void RegisterStateProvider_CaptureAndDispose_UpdatesStateKeys()
        {
            using var runtime = new DebugUtilsRuntime();
            var registration = runtime.RegisterStateProvider(
                "Module",
                "current",
                () => 42);

            Assert.Equal(new[] { "Module::current" }, runtime.StateKeys());
            Assert.Equal(42, runtime.CaptureState()["Module::current"]);

            registration.Dispose();

            Assert.Empty(runtime.StateKeys());
            Assert.Empty(runtime.CaptureState());
        }

        [Fact]
        public void RegisterStateProvider_DuplicateKey_ThrowsWithoutReplacingOwner()
        {
            using var runtime = new DebugUtilsRuntime();
            using var original = runtime.RegisterStateProvider(
                "Module",
                "current",
                () => 1);

            var error = Assert.Throws<InvalidOperationException>(
                () => runtime.RegisterStateProvider(
                    "Module",
                    "current",
                    () => 2));

            Assert.Contains("already registered", error.Message);
            Assert.Equal(1, runtime.CaptureState()["Module::current"]);
            Assert.Equal(1, runtime.GetDiagnostics().ProviderCount);
        }

        [Fact]
        public void RegisterCommand_DuplicateName_ThrowsWithoutReplacingOwner()
        {
            using var runtime = new DebugUtilsRuntime();
            using var original = runtime.RegisterCommand(
                "Module",
                "command",
                _ => "original");

            var error = Assert.Throws<InvalidOperationException>(
                () => runtime.RegisterCommand(
                    "Module",
                    "command",
                    _ => "replacement"));

            Assert.Contains("already registered", error.Message);
            Assert.True(runtime.TryInvokeCommand(
                "Module",
                "command",
                null,
                out var result));
            Assert.Equal("original", result);
            Assert.Equal(1, runtime.GetDiagnostics().CommandCount);
        }

        [Fact]
        public void RegistrationHandle_AfterKeyIsReused_DoesNotRemoveNewOwner()
        {
            using var runtime = new DebugUtilsRuntime();
            var oldHandle = runtime.RegisterStateProvider(
                "Module",
                "current",
                () => 1);

            oldHandle.Dispose();
            using var newHandle = runtime.RegisterStateProvider(
                "Module",
                "current",
                () => 2);

            oldHandle.Dispose();

            Assert.Equal(2, runtime.CaptureState()["Module::current"]);
            Assert.Equal(1, runtime.GetDiagnostics().ProviderCount);
        }

        [Fact]
        public void PayloadAndProviderExceptions_AreIsolated()
        {
            using var runtime = new DebugUtilsRuntime();
            runtime.Record(
                "Module",
                "throws",
                () => throw new InvalidOperationException("payload"));
            using var registration = runtime.RegisterStateProvider(
                "Module",
                "throws",
                () => throw new InvalidOperationException("provider"));

            var operation = Assert.Single(runtime.GetOperations());
            Assert.Contains("InvalidOperationException", operation.Payload.ToString());

            var state = runtime.CaptureState();
            Assert.Contains(
                "InvalidOperationException",
                state["Module::throws"].ToString());
        }

        [Fact]
        public void DebugOperationToString_PayloadToStringThrows_ReturnsPlaceholder()
        {
            using var runtime = new DebugUtilsRuntime();
            runtime.Record(
                "Module",
                "operation",
                () => new ThrowingStringPayload());

            var operation = Assert.Single(runtime.GetOperations());
            var text = operation.ToString();

            Assert.Contains("Module/operation", text);
            Assert.Contains("InvalidOperationException", text);
        }

        [Fact]
        public async Task RecordAndCaptureState_ConcurrentCalls_RemainConsistent()
        {
            using var runtime = new DebugUtilsRuntime(
                new DebugUtilsOptions { Capacity = 128 });
            int value = 0;
            using var registration = runtime.RegisterStateProvider(
                "Module",
                "counter",
                () => Interlocked.CompareExchange(ref value, 0, 0));
            var errors = new ConcurrentQueue<Exception>();

            var workers = Enumerable.Range(0, 6)
                .Select(worker => Task.Run(() =>
                {
                    try
                    {
                        for (int i = 0; i < 500; i++)
                        {
                            if (worker % 2 == 0)
                            {
                                var next = Interlocked.Increment(ref value);
                                runtime.Record("Module", "record", () => next);
                            }
                            else
                            {
                                Assert.True(
                                    runtime.CaptureState().ContainsKey(
                                        "Module::counter"));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Enqueue(ex);
                    }
                }))
                .ToArray();

            await Task.WhenAll(workers);

            Assert.Empty(errors);
            var operations = runtime.GetOperations();
            Assert.InRange(operations.Count, 1, 128);
            Assert.True(
                operations
                    .Zip(operations.Skip(1), (left, right) => left.Sequence < right.Sequence)
                    .All(inOrder => inOrder));
        }

        [Fact]
        public void EnableGlobal_Dispose_RestoresNoOpAndClearsRegistrations()
        {
            Assert.Same(NullDebugUtils.Instance, DebugUtilsProvider.Current);

            var runtime = DebugUtilsRuntime.EnableGlobal();
            runtime.RegisterStateProvider("Module", "state", () => 1);
            runtime.RegisterCommand("Module", "command", value => value);
            runtime.Record("Module", "operation");

            Assert.Same(runtime, DebugUtilsProvider.Current);

            runtime.Dispose();

            Assert.Same(NullDebugUtils.Instance, DebugUtilsProvider.Current);
            Assert.False(runtime.IsEnabled);
            Assert.Empty(runtime.StateKeys());
            Assert.Empty(runtime.CommandKeys());
            Assert.Empty(runtime.GetOperations());
        }

        [Fact]
        public void EnableGlobal_WhileAlreadyEnabled_ThrowsClearly()
        {
            using var runtime = DebugUtilsRuntime.EnableGlobal();

            var error = Assert.Throws<InvalidOperationException>(
                () => DebugUtilsRuntime.EnableGlobal());

            Assert.Contains("already enabled", error.Message);
            Assert.Same(runtime, DebugUtilsProvider.Current);
        }

        [Fact]
        public async Task EnableGlobal_DisposedDuringInstallation_RestoresNoOp()
        {
            using var providerInstalled = new ManualResetEventSlim();
            using var continueActivation = new ManualResetEventSlim();

            var activation = Task.Run(() =>
                DebugUtilsRuntime.EnableGlobal(
                    null,
                    () =>
                    {
                        providerInstalled.Set();
                        if (!continueActivation.Wait(TimeSpan.FromSeconds(5)))
                            throw new TimeoutException("Activation race was not released.");
                    }));

            try
            {
                Assert.True(providerInstalled.Wait(TimeSpan.FromSeconds(5)));

                var published = Assert.IsType<DebugUtilsRuntime>(
                    DebugUtilsProvider.Current);
                published.Dispose();
            }
            finally
            {
                continueActivation.Set();
            }

            var error = await Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await activation);

            Assert.Contains("disposed while it was being enabled", error.Message);
            Assert.Same(NullDebugUtils.Instance, DebugUtilsProvider.Current);
        }

        [Fact]
        public void ProviderInstall_HubDisablesDuringPublication_RollsBackToNoOp()
        {
            Assert.Same(NullDebugUtils.Instance, DebugUtilsProvider.Current);
            var debugUtils = new DisablesDuringInstallDebugUtils();

            var error = Assert.Throws<ArgumentException>(
                () => DebugUtilsProvider.Install(debugUtils));

            Assert.Contains("became disabled", error.Message);
            Assert.Same(NullDebugUtils.Instance, DebugUtilsProvider.Current);
        }

        private sealed class DisablesDuringInstallDebugUtils : IDebugUtils
        {
            private int _enabledReads;

            public bool IsEnabled =>
                Interlocked.Increment(ref _enabledReads) == 1;

            public void Record(
                string module,
                string operation,
                Func<object> payload = null)
                => throw new NotSupportedException();

            public IDisposable RegisterStateProvider(
                string module,
                string key,
                Func<object> snapshot)
                => throw new NotSupportedException();

            public IDisposable RegisterCommand(
                string module,
                string name,
                Func<object, object> command)
                => throw new NotSupportedException();
        }

        private sealed class ThrowingStringPayload
        {
            public override string ToString()
                => throw new InvalidOperationException("formatting");
        }
    }
}
