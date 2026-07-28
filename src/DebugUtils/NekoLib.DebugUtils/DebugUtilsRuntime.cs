using System;
using System.Collections.Generic;
using System.Threading;
using NekoLib.Core;
using NekoLib.Core.Observability;

namespace NekoLib.DebugUtils
{
    /// <summary>
    /// Concrete <see cref="IDebugUtils"/>: an opt-in, in-process observability hub.
    ///
    /// Modules push operations through <see cref="Record"/> and expose pull-based
    /// state / commands through the register methods, all against the
    /// <see cref="IDebugUtils"/> contract in NekoLib.Core — so a module never
    /// references this type and no dependency cycle is created.
    ///
    /// Whoever owns the runtime (a debug window, a diagnostics endpoint) consumes
    /// the captured data through the concrete-only query surface
    /// (<see cref="GetOperations"/>, <see cref="CaptureState"/>,
    /// <see cref="TryInvokeCommand"/>).
    ///
    /// All members are thread-safe. <see cref="IsEnabled"/> remains true until
    /// the runtime is disposed; this type is meant to be registered only in
    /// debug/diagnostic builds.
    /// </summary>
    public sealed class DebugUtilsRuntime : IDebugUtils, IDisposable
    {
        private readonly int _capacity;

        private readonly object _opLock = new object();
        private readonly Queue<DebugOperation> _operations;
        private long _nextSequence;
        private long _totalRecorded;
        private long _evictedCount;
        private long _clearCount;

        private readonly object _registryLock = new object();
        private readonly Dictionary<string, StateRegistration> _stateProviders =
            new Dictionary<string, StateRegistration>();
        private readonly Dictionary<string, CommandRegistration> _commands =
            new Dictionary<string, CommandRegistration>();
        private long _nextRegistrationId;
        private IDisposable? _globalInstallation;
        private int _disposed;

        public DebugUtilsRuntime(DebugUtilsOptions? options = null)
        {
            var capacity = options?.Capacity ?? 1024;
            if (capacity < 1) capacity = 1;
            _capacity = capacity;
            _operations = new Queue<DebugOperation>(_capacity);
        }

        /// <summary>
        /// Creates and installs the single process-wide runtime. Dispose the
        /// returned runtime to restore <see cref="NullDebugUtils.Instance"/> and
        /// remove every provider and command registered with it.
        /// </summary>
        public static DebugUtilsRuntime EnableGlobal(DebugUtilsOptions? options = null)
            => EnableGlobal(options, null);

        internal static DebugUtilsRuntime EnableGlobal(
            DebugUtilsOptions? options,
            Action? afterProviderInstall)
        {
            var runtime = new DebugUtilsRuntime(options);
            IDisposable? installation = null;
            try
            {
                installation = DebugUtilsProvider.Install(runtime);
                afterProviderInstall?.Invoke();

                Interlocked.Exchange(
                    ref runtime._globalInstallation,
                    installation);
                installation = null;

                if (!runtime.IsEnabled)
                {
                    var racedInstallation = Interlocked.Exchange(
                        ref runtime._globalInstallation,
                        null);
                    racedInstallation?.Dispose();
                    throw new ObjectDisposedException(
                        nameof(DebugUtilsRuntime),
                        "The global DebugUtils runtime was disposed while it was being enabled.");
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

        /// <inheritdoc />
        public bool IsEnabled => Volatile.Read(ref _disposed) == 0;

        // ----------------------------------------------------------------
        // Push side (IDebugUtils) — called by observed modules
        // ----------------------------------------------------------------

        /// <inheritdoc />
        public void Record(string module, string operation, Func<object>? payload = null)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (!IsEnabled) return;

            object? captured = null;
            if (payload != null)
            {
                // The payload delegate is module code; never let it break recording.
                try { captured = payload(); }
                catch (Exception ex) { captured = "<payload threw: " + ex.GetType().Name + ">"; }
            }

            lock (_opLock)
            {
                if (!IsEnabled) return;

                var sequence = unchecked(++_nextSequence);
                var entry = new DebugOperation(
                    sequence,
                    DateTime.UtcNow,
                    module,
                    operation,
                    captured);

                _totalRecorded++;
                _operations.Enqueue(entry);
                while (_operations.Count > _capacity)
                {
                    _operations.Dequeue();
                    _evictedCount++;
                }
            }
        }

        /// <inheritdoc />
        public IDisposable RegisterStateProvider(string module, string key, Func<object> snapshot)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (!IsEnabled) return Disposable.Empty;

            var id = Compose(module, key);
            long registrationId;
            lock (_registryLock)
            {
                if (!IsEnabled) return Disposable.Empty;
                if (_stateProviders.ContainsKey(id))
                    throw DuplicateRegistration("state provider", id);

                registrationId = unchecked(++_nextRegistrationId);
                _stateProviders.Add(
                    id,
                    new StateRegistration(registrationId, snapshot));
            }

            return new Unregister(() => UnregisterStateProvider(id, registrationId));
        }

        /// <inheritdoc />
        public IDisposable RegisterCommand(string module, string name, Func<object?, object?> command)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (!IsEnabled) return Disposable.Empty;

            var id = Compose(module, name);
            long registrationId;
            lock (_registryLock)
            {
                if (!IsEnabled) return Disposable.Empty;
                if (_commands.ContainsKey(id))
                    throw DuplicateRegistration("command", id);

                registrationId = unchecked(++_nextRegistrationId);
                _commands.Add(
                    id,
                    new CommandRegistration(registrationId, command));
            }

            return new Unregister(() => UnregisterCommand(id, registrationId));
        }

        // ----------------------------------------------------------------
        // Pull side (concrete-only) — called by the observability owner
        // ----------------------------------------------------------------

        /// <summary>Snapshot of the recorded operations, oldest first.</summary>
        public IReadOnlyList<DebugOperation> GetOperations()
        {
            lock (_opLock)
                return new List<DebugOperation>(_operations);
        }

        /// <summary>Drops all buffered operations.</summary>
        public void ClearOperations()
        {
            lock (_opLock)
            {
                _operations.Clear();
                _clearCount++;
            }
        }

        /// <summary>
        /// Captures scalar runtime diagnostics without retaining operation
        /// payloads, state-provider delegates, or command delegates.
        /// </summary>
        public DebugUtilsRuntimeDiagnostics GetDiagnostics()
        {
            int retainedCount;
            long totalRecorded;
            long evictedCount;
            long clearCount;
            long? oldestSequence;
            long? newestSequence;

            lock (_opLock)
            {
                retainedCount = _operations.Count;
                totalRecorded = _totalRecorded;
                evictedCount = _evictedCount;
                clearCount = _clearCount;
                oldestSequence = retainedCount == 0
                    ? (long?)null
                    : _operations.Peek().Sequence;
                newestSequence = retainedCount == 0
                    ? (long?)null
                    : _nextSequence;
            }

            int stateProviderCount;
            int commandCount;
            lock (_registryLock)
            {
                stateProviderCount = _stateProviders.Count;
                commandCount = _commands.Count;
            }

            return new DebugUtilsRuntimeDiagnostics(
                IsEnabled,
                _capacity,
                retainedCount,
                totalRecorded,
                evictedCount,
                clearCount,
                oldestSequence,
                newestSequence,
                stateProviderCount,
                commandCount);
        }

        /// <summary>
        /// Invokes every registered state provider and returns a snapshot keyed by
        /// <c>module::key</c>. A provider that throws yields a placeholder value
        /// instead of failing the whole capture.
        /// </summary>
        public IReadOnlyDictionary<string, object> CaptureState()
        {
            List<KeyValuePair<string, StateRegistration>> providers;
            lock (_registryLock)
                providers = new List<KeyValuePair<string, StateRegistration>>(_stateProviders);

            var result = new Dictionary<string, object>(providers.Count);
            foreach (var kv in providers)
            {
                try { result[kv.Key] = kv.Value.Snapshot(); }
                catch (Exception ex) { result[kv.Key] = "<snapshot threw: " + ex.GetType().Name + ">"; }
            }
            return result;
        }

        /// <summary>
        /// Invokes a registered command. Returns <c>false</c> if no command is
        /// registered under <paramref name="module"/>/<paramref name="name"/>.
        /// </summary>
        public bool TryInvokeCommand(string module, string name, object? argument, out object? result)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            if (name == null) throw new ArgumentNullException(nameof(name));

            CommandRegistration? registration;
            lock (_registryLock)
                _commands.TryGetValue(Compose(module, name), out registration);

            if (registration == null)
            {
                result = null;
                return false;
            }

            result = registration.Command(argument);
            return true;
        }

        /// <summary>The <c>module::key</c> identifiers of all registered state providers.</summary>
        public IReadOnlyList<string> StateKeys()
        {
            lock (_registryLock)
                return new List<string>(_stateProviders.Keys);
        }

        /// <summary>The <c>module::name</c> identifiers of all registered commands.</summary>
        public IReadOnlyList<string> CommandKeys()
        {
            lock (_registryLock)
                return new List<string>(_commands.Keys);
        }

        private static string Compose(string module, string name) => module + "::" + name;

        private static InvalidOperationException DuplicateRegistration(
            string registrationKind,
            string id)
            => new InvalidOperationException(
                "A " + registrationKind + " is already registered for '" + id + "'.");

        private void UnregisterStateProvider(string id, long registrationId)
        {
            lock (_registryLock)
            {
                StateRegistration? current;
                if (_stateProviders.TryGetValue(id, out current)
                    && current.RegistrationId == registrationId)
                {
                    _stateProviders.Remove(id);
                }
            }
        }

        private void UnregisterCommand(string id, long registrationId)
        {
            lock (_registryLock)
            {
                CommandRegistration? current;
                if (_commands.TryGetValue(id, out current)
                    && current.RegistrationId == registrationId)
                {
                    _commands.Remove(id);
                }
            }
        }

        /// <summary>
        /// Disables this runtime, restores the process-wide no-op slot when this
        /// instance owns it, and releases all captured delegates and operations.
        /// Idempotent.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            var installation = Interlocked.Exchange(ref _globalInstallation, null);
            installation?.Dispose();

            lock (_registryLock)
            {
                _stateProviders.Clear();
                _commands.Clear();
            }

            lock (_opLock)
                _operations.Clear();
        }

        private sealed class StateRegistration
        {
            public StateRegistration(long registrationId, Func<object> snapshot)
            {
                RegistrationId = registrationId;
                Snapshot = snapshot;
            }

            public long RegistrationId { get; }
            public Func<object> Snapshot { get; }
        }

        private sealed class CommandRegistration
        {
            public CommandRegistration(
                long registrationId,
                Func<object?, object?> command)
            {
                RegistrationId = registrationId;
                Command = command;
            }

            public long RegistrationId { get; }
            public Func<object?, object?> Command { get; }
        }

        private sealed class Unregister : IDisposable
        {
            private Action? _action;

            public Unregister(Action action) => _action = action;

            public void Dispose()
            {
                // Idempotent: double-dispose must not run the action twice.
                var action = Interlocked.Exchange(ref _action, null);
                action?.Invoke();
            }
        }
    }
}
