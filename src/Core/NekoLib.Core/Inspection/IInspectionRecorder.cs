using System;

namespace NekoLib.Core.Inspection
{
    /// <summary>Defines the opt-in, module-facing runtime inspection contract.</summary>
    /// <remarks>
    /// Recording and delegate invocation are in-process. Implementations can run
    /// callbacks inline, so producers must supply bounded diagnostic projections
    /// rather than UI objects, secrets, or unbounded application graphs. Supplying
    /// a recorder does not transfer its disposal ownership to a feature module.
    /// </remarks>
    public interface IInspectionRecorder
    {
        /// <summary>Gets whether the recorder currently accepts optional inspection work.</summary>
        /// <remarks>The value can change concurrently; it is an optimization hint, not a lease.</remarks>
        bool IsEnabled { get; }

        /// <summary>Records one named operation with an optional lazy payload.</summary>
        /// <param name="module">Non-null logical module identity.</param>
        /// <param name="operation">Non-null operation name.</param>
        /// <param name="payload">
        /// Optional factory for a bounded diagnostic projection. An implementation
        /// must not invoke it when recording is disabled.
        /// </param>
        /// <remarks>
        /// Payload values remain application-owned shallow references unless a
        /// concrete implementation documents a stronger capture policy.
        /// </remarks>
        void Record(string module, string operation, Func<object>? payload = null);

        /// <summary>Registers a pull-based state provider.</summary>
        /// <param name="module">Non-null logical module identity.</param>
        /// <param name="key">Non-null provider key within the module.</param>
        /// <param name="snapshot">Delegate that produces one bounded state projection.</param>
        /// <returns>
        /// A caller-owned, idempotent handle that unregisters this registration;
        /// disposing it does not dispose captured application objects.
        /// </returns>
        /// <remarks>
        /// A provider already admitted by a concurrent capture may finish after
        /// unregistration. The delegate must tolerate the concrete owner's
        /// documented concurrency and completion-budget behavior.
        /// </remarks>
        IDisposable RegisterStateProvider(string module, string key, Func<object> snapshot);

        /// <summary>
        /// Registers an in-process action with the concrete Inspection owner.
        /// Authorization, asynchronous execution, cancellation, timeout, UI
        /// marshalling, and module adoption are not stable contracts.
        /// </summary>
        /// <param name="module">Non-null logical module identity.</param>
        /// <param name="name">Non-null action name within the module.</param>
        /// <param name="action">Synchronous in-process action delegate.</param>
        /// <returns>A caller-owned handle that unregisters this action.</returns>
        /// <remarks>
        /// The delegate is not an authorization boundary. Do not expose it through
        /// IPC, reflection, or remote control as privileged access. Callers that use
        /// this member deliberately accept experimental warning <c>NEKOEXP0001</c>.
        /// </remarks>
        [Obsolete("Experimental API NEKOEXP0001: compatibility is not guaranteed.", error: false)]
        IDisposable RegisterAction(string module, string name, Func<object?, object?> action);
    }
}
