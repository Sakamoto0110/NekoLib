using NekoLib.Core;
using NekoLib.Core.Inspection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace NekoLib.Inspection
{
    /// <summary>
    /// Opt-in in-process inspection runtime. Modules receive only the
    /// <see cref="IInspectionRecorder"/> push/register surface; consumers and
    /// Diagnostics receive the read-only <see cref="IInspectionSnapshotSource"/>.
    /// Registered actions never appear on the snapshot surface.
    /// </summary>
    public sealed class InspectionRuntime :
        IInspectionRecorder,
        IInspectionSnapshotSource,
        IDisposable
    {
        private readonly int _capacity;
        private readonly object _operationGate = new object();
        private readonly Queue<InspectionOperation> _operations;
        private readonly object _registryGate = new object();
        private readonly Dictionary<string, StateRegistration> _stateProviders =
            new Dictionary<string, StateRegistration>(StringComparer.Ordinal);
        private readonly Dictionary<string, ActionRegistration> _actions =
            new Dictionary<string, ActionRegistration>(StringComparer.Ordinal);
        private long _nextSequence;
        private long _totalRecorded;
        private long _evictedCount;
        private long _clearCount;
        private long _nextRegistrationId;
        private IDisposable? _globalInstallation;
        private int _disposed;

        public InspectionRuntime(InspectionOptions? options = null)
        {
            var capacity = options?.Capacity ?? 1024;
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(InspectionOptions.Capacity));

            _capacity = capacity;
            _operations = new Queue<InspectionOperation>(_capacity);
        }

        public static InspectionRuntime EnableGlobal(InspectionOptions? options = null)
            => EnableGlobal(options, null);

        internal static InspectionRuntime EnableGlobal(
            InspectionOptions? options,
            Action? afterProviderInstall)
        {
            var runtime = new InspectionRuntime(options);
            IDisposable? installation = null;
            try
            {
                installation = InspectionProvider.Install(runtime);
                afterProviderInstall?.Invoke();
                Interlocked.Exchange(ref runtime._globalInstallation, installation);
                installation = null;

                if (!runtime.IsEnabled)
                {
                    Interlocked.Exchange(ref runtime._globalInstallation, null)?.Dispose();
                    throw new ObjectDisposedException(
                        nameof(InspectionRuntime),
                        "The global Inspection runtime was disposed while it was being enabled.");
                }

                return runtime;
            }
            catch
            {
                installation?.Dispose();
                runtime.Dispose();
                throw;
            }
        }

        public bool IsEnabled => Volatile.Read(ref _disposed) == 0;

        public void Record(string module, string operation, Func<object>? payload = null)
        {
            ValidateModule(module, nameof(module));
            ValidateRequired(operation, nameof(operation));
            if (!IsEnabled)
                return;

            object? captured = null;
            if (payload != null)
            {
                try { captured = payload(); }
                catch (Exception ex) { captured = "<payload threw: " + ex.GetType().Name + ">"; }
            }

            lock (_operationGate)
            {
                if (!IsEnabled)
                    return;

                var sequence = unchecked(++_nextSequence);
                _totalRecorded++;
                _operations.Enqueue(new InspectionOperation(
                    sequence,
                    DateTime.UtcNow,
                    module,
                    operation,
                    captured));

                while (_operations.Count > _capacity)
                {
                    _operations.Dequeue();
                    _evictedCount++;
                }
            }
        }

        public IDisposable RegisterStateProvider(
            string module,
            string key,
            Func<object> snapshot)
        {
            ValidateModule(module, nameof(module));
            ValidateIdentityComponent(key, nameof(key));
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (!IsEnabled)
                return Disposable.Empty;

            var id = Compose(module, key);
            long registrationId;
            lock (_registryGate)
            {
                if (!IsEnabled)
                    return Disposable.Empty;
                if (_stateProviders.ContainsKey(id))
                    throw DuplicateRegistration("state provider", id);

                registrationId = unchecked(++_nextRegistrationId);
                _stateProviders.Add(id, new StateRegistration(registrationId, snapshot));
            }

            return new Unregister(() => UnregisterStateProvider(id, registrationId));
        }

        [Obsolete("Experimental API NEKOEXP0001: compatibility is not guaranteed.", error: false)]
        public IDisposable RegisterAction(
            string module,
            string name,
            Func<object?, object?> action)
        {
            ValidateModule(module, nameof(module));
            ValidateIdentityComponent(name, nameof(name));
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            if (!IsEnabled)
                return Disposable.Empty;

            var id = Compose(module, name);
            long registrationId;
            lock (_registryGate)
            {
                if (!IsEnabled)
                    return Disposable.Empty;
                if (_actions.ContainsKey(id))
                    throw DuplicateRegistration("action", id);

                registrationId = unchecked(++_nextRegistrationId);
                _actions.Add(id, new ActionRegistration(registrationId, action));
            }

            return new Unregister(() => UnregisterAction(id, registrationId));
        }

        public InspectionSnapshot CaptureSnapshot(int maxOperations, TimeSpan timeout)
        {
            if (maxOperations < 0)
                throw new ArgumentOutOfRangeException(nameof(maxOperations));
            if (timeout < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout));

            InspectionOperation[] operations;
            long totalRecorded;
            long evictedCount;
            lock (_operationGate)
            {
                var all = _operations.ToArray();
                var take = Math.Min(maxOperations, all.Length);
                operations = new InspectionOperation[take];
                Array.Copy(all, all.Length - take, operations, 0, take);
                totalRecorded = _totalRecorded;
                evictedCount = _evictedCount;
            }

            List<KeyValuePair<string, StateRegistration>> providers;
            lock (_registryGate)
                providers = new List<KeyValuePair<string, StateRegistration>>(_stateProviders);
            providers.Sort(CompareStateRegistrations);

            var watch = Stopwatch.StartNew();
            var state = new Dictionary<string, object>(providers.Count, StringComparer.Ordinal);
            foreach (var pair in providers)
            {
                var remaining = timeout - watch.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    state[pair.Key] = "<snapshot timed out>";
                    continue;
                }

                try
                {
                    var task = pair.Value.GetOrStartSnapshot();
                    if (!task.Wait(remaining))
                    {
                        state[pair.Key] = "<snapshot timed out>";
                        continue;
                    }

                    var capture = task.Result;
                    state[pair.Key] = capture.ExceptionType == null
                        ? capture.Value ?? "<null>"
                        : "<snapshot threw: " + capture.ExceptionType + ">";
                }
                catch (Exception ex)
                {
                    state[pair.Key] = "<snapshot threw: " + RootExceptionType(ex) + ">";
                }
            }

            return new InspectionSnapshot(
                DateTime.UtcNow,
                operations,
                state,
                _capacity,
                totalRecorded,
                evictedCount);
        }

        /// <summary>
        /// Convenience synchronous read for interactive Inspection owners. Provider
        /// failures are isolated exactly as before the rename. Diagnostics should
        /// depend on <see cref="IInspectionSnapshotSource"/> and supply a budget.
        /// </summary>
        public IReadOnlyDictionary<string, object> CaptureState()
        {
            List<KeyValuePair<string, StateRegistration>> providers;
            lock (_registryGate)
                providers = new List<KeyValuePair<string, StateRegistration>>(_stateProviders);
            providers.Sort(CompareStateRegistrations);

            var state = new Dictionary<string, object>(providers.Count, StringComparer.Ordinal);
            foreach (var pair in providers)
            {
                try { state[pair.Key] = pair.Value.Snapshot() ?? "<null>"; }
                catch (Exception ex)
                {
                    state[pair.Key] = "<snapshot threw: " + RootExceptionType(ex) + ">";
                }
            }

            return state;
        }

        public IReadOnlyList<InspectionOperation> GetOperations()
        {
            lock (_operationGate)
                return _operations.ToArray();
        }

        public void ClearOperations()
        {
            lock (_operationGate)
            {
                if (!IsEnabled)
                    return;

                _operations.Clear();
                _clearCount++;
            }
        }

        public InspectionRuntimeDiagnostics GetDiagnostics()
        {
            int retainedCount;
            long totalRecorded;
            long evictedCount;
            long clearCount;
            long? oldestSequence;
            long? newestSequence;
            lock (_operationGate)
            {
                retainedCount = _operations.Count;
                totalRecorded = _totalRecorded;
                evictedCount = _evictedCount;
                clearCount = _clearCount;
                oldestSequence = retainedCount == 0 ? (long?)null : _operations.Peek().Sequence;
                newestSequence = retainedCount == 0 ? (long?)null : _nextSequence;
            }

            int providerCount;
            int actionCount;
            lock (_registryGate)
            {
                providerCount = _stateProviders.Count;
                actionCount = _actions.Count;
            }

            return new InspectionRuntimeDiagnostics(
                IsEnabled,
                _capacity,
                retainedCount,
                totalRecorded,
                evictedCount,
                clearCount,
                oldestSequence,
                newestSequence,
                providerCount,
                actionCount);
        }

        [Obsolete("Experimental API NEKOEXP0001: compatibility is not guaranteed.", error: false)]
        public bool TryInvokeAction(
            string module,
            string name,
            object? argument,
            out object? result)
        {
            ValidateModule(module, nameof(module));
            ValidateIdentityComponent(name, nameof(name));

            ActionRegistration? registration;
            lock (_registryGate)
                _actions.TryGetValue(Compose(module, name), out registration);

            if (registration == null)
            {
                result = null;
                return false;
            }

            result = registration.Action(argument);
            return true;
        }

        public IReadOnlyList<string> StateKeys()
        {
            lock (_registryGate)
            {
                var providers = new List<KeyValuePair<string, StateRegistration>>(_stateProviders);
                providers.Sort(CompareStateRegistrations);
                var keys = new List<string>(providers.Count);
                foreach (var pair in providers)
                    keys.Add(pair.Key);
                return keys;
            }
        }

        [Obsolete("Experimental API NEKOEXP0001: compatibility is not guaranteed.", error: false)]
        public IReadOnlyList<string> ActionKeys()
        {
            lock (_registryGate)
            {
                var actions = new List<KeyValuePair<string, ActionRegistration>>(_actions);
                actions.Sort(CompareActionRegistrations);
                var keys = new List<string>(actions.Count);
                foreach (var pair in actions)
                    keys.Add(pair.Key);
                return keys;
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            Interlocked.Exchange(ref _globalInstallation, null)?.Dispose();
            lock (_registryGate)
            {
                _stateProviders.Clear();
                _actions.Clear();
            }

            lock (_operationGate)
                _operations.Clear();
        }

        private static int CompareStateRegistrations(
            KeyValuePair<string, StateRegistration> left,
            KeyValuePair<string, StateRegistration> right)
            => left.Value.RegistrationId.CompareTo(right.Value.RegistrationId);

        private static int CompareActionRegistrations(
            KeyValuePair<string, ActionRegistration> left,
            KeyValuePair<string, ActionRegistration> right)
            => left.Value.RegistrationId.CompareTo(right.Value.RegistrationId);

        private static string Compose(string module, string name) => module + "::" + name;

        private static void ValidateModule(string value, string paramName)
        {
            ValidateRequired(value, paramName);
            if (value.IndexOf("::", StringComparison.Ordinal) >= 0)
                throw new ArgumentException("Module cannot contain '::'.", paramName);
        }

        private static void ValidateIdentityComponent(string value, string paramName)
        {
            ValidateRequired(value, paramName);
            if (value.IndexOf("::", StringComparison.Ordinal) >= 0)
                throw new ArgumentException("Identity component cannot contain '::'.", paramName);
        }

        private static void ValidateRequired(string value, string paramName)
        {
            if (value == null)
                throw new ArgumentNullException(paramName);
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Value cannot be empty or whitespace.", paramName);
        }

        private static InvalidOperationException DuplicateRegistration(string kind, string id)
            => new InvalidOperationException("A " + kind + " is already registered for '" + id + "'.");

        private static string RootExceptionType(Exception exception)
        {
            while (exception.InnerException != null)
                exception = exception.InnerException;
            return exception.GetType().Name;
        }

        private void UnregisterStateProvider(string id, long registrationId)
        {
            lock (_registryGate)
            {
                StateRegistration? current;
                if (_stateProviders.TryGetValue(id, out current) &&
                    current.RegistrationId == registrationId)
                {
                    _stateProviders.Remove(id);
                }
            }
        }

        private void UnregisterAction(string id, long registrationId)
        {
            lock (_registryGate)
            {
                ActionRegistration? current;
                if (_actions.TryGetValue(id, out current) &&
                    current.RegistrationId == registrationId)
                {
                    _actions.Remove(id);
                }
            }
        }

        private sealed class StateRegistration
        {
            private readonly object _captureGate = new object();
            private Task<StateCapture>? _inFlight;

            public StateRegistration(long registrationId, Func<object> snapshot)
            {
                RegistrationId = registrationId;
                Snapshot = snapshot;
            }

            public long RegistrationId { get; }
            public Func<object> Snapshot { get; }

            public Task<StateCapture> GetOrStartSnapshot()
            {
                lock (_captureGate)
                {
                    if (_inFlight == null || _inFlight.IsCompleted)
                    {
                        _inFlight = Task.Run(() =>
                        {
                            try
                            {
                                return StateCapture.FromValue(Snapshot());
                            }
                            catch (Exception ex)
                            {
                                return StateCapture.FromException(RootExceptionType(ex));
                            }
                        });
                    }

                    return _inFlight;
                }
            }
        }

        private sealed class StateCapture
        {
            private StateCapture(object? value, string? exceptionType)
            {
                Value = value;
                ExceptionType = exceptionType;
            }

            public object? Value { get; }
            public string? ExceptionType { get; }

            public static StateCapture FromValue(object? value)
                => new StateCapture(value, null);

            public static StateCapture FromException(string exceptionType)
                => new StateCapture(null, exceptionType);
        }

        private sealed class ActionRegistration
        {
            public ActionRegistration(long registrationId, Func<object?, object?> action)
            {
                RegistrationId = registrationId;
                Action = action;
            }

            public long RegistrationId { get; }
            public Func<object?, object?> Action { get; }
        }

        private sealed class Unregister : IDisposable
        {
            private Action? _action;

            public Unregister(Action action)
            {
                _action = action;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref _action, null)?.Invoke();
            }
        }
    }
}
