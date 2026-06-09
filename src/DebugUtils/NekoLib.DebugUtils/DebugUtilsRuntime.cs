using System;
using System.Collections.Generic;
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
    /// All members are thread-safe. <see cref="IsEnabled"/> is always true; this
    /// type is meant to be registered only in debug/diagnostic builds.
    /// </summary>
    public sealed class DebugUtilsRuntime : IDebugUtils
    {
        private readonly int _capacity;

        private readonly object _opLock = new object();
        private readonly Queue<DebugOperation> _operations;

        private readonly object _registryLock = new object();
        private readonly Dictionary<string, Func<object>> _stateProviders = new Dictionary<string, Func<object>>();
        private readonly Dictionary<string, Func<object?, object?>> _commands = new Dictionary<string, Func<object?, object?>>();

        public DebugUtilsRuntime(DebugUtilsOptions? options = null)
        {
            var capacity = options?.Capacity ?? 1024;
            if (capacity < 1) capacity = 1;
            _capacity = capacity;
            _operations = new Queue<DebugOperation>(_capacity);
        }

        /// <inheritdoc />
        public bool IsEnabled => true;

        // ----------------------------------------------------------------
        // Push side (IDebugUtils) — called by observed modules
        // ----------------------------------------------------------------

        /// <inheritdoc />
        public void Record(string module, string operation, Func<object>? payload = null)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            if (operation == null) throw new ArgumentNullException(nameof(operation));

            object? captured = null;
            if (payload != null)
            {
                // The payload delegate is module code; never let it break recording.
                try { captured = payload(); }
                catch (Exception ex) { captured = "<payload threw: " + ex.GetType().Name + ">"; }
            }

            var entry = new DebugOperation(DateTime.UtcNow, module, operation, captured);

            lock (_opLock)
            {
                _operations.Enqueue(entry);
                while (_operations.Count > _capacity)
                    _operations.Dequeue();
            }
        }

        /// <inheritdoc />
        public IDisposable RegisterStateProvider(string module, string key, Func<object> snapshot)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            var id = Compose(module, key);
            lock (_registryLock)
                _stateProviders[id] = snapshot;

            return new Unregister(() =>
            {
                lock (_registryLock)
                    _stateProviders.Remove(id);
            });
        }

        /// <inheritdoc />
        public IDisposable RegisterCommand(string module, string name, Func<object?, object?> command)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (command == null) throw new ArgumentNullException(nameof(command));

            var id = Compose(module, name);
            lock (_registryLock)
                _commands[id] = command;

            return new Unregister(() =>
            {
                lock (_registryLock)
                    _commands.Remove(id);
            });
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
                _operations.Clear();
        }

        /// <summary>
        /// Invokes every registered state provider and returns a snapshot keyed by
        /// <c>module::key</c>. A provider that throws yields a placeholder value
        /// instead of failing the whole capture.
        /// </summary>
        public IReadOnlyDictionary<string, object> CaptureState()
        {
            List<KeyValuePair<string, Func<object>>> providers;
            lock (_registryLock)
                providers = new List<KeyValuePair<string, Func<object>>>(_stateProviders);

            var result = new Dictionary<string, object>(providers.Count);
            foreach (var kv in providers)
            {
                try { result[kv.Key] = kv.Value(); }
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

            Func<object?, object?>? command;
            lock (_registryLock)
                _commands.TryGetValue(Compose(module, name), out command);

            if (command == null)
            {
                result = null;
                return false;
            }

            result = command(argument);
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

        private sealed class Unregister : IDisposable
        {
            private Action? _action;

            public Unregister(Action action) => _action = action;

            public void Dispose()
            {
                // Idempotent: double-dispose must not run the action twice.
                var action = _action;
                _action = null;
                action?.Invoke();
            }
        }
    }
}
