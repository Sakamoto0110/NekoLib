// FILE: NekoLib.Navigation/Diagnostics/DebugUtilsNavigationObserver.cs
#nullable enable
using System;
using System.Threading;
using NekoLib.Core;
using NekoLib.Core.Observability;
using NekoLib.Navigation.Runtime.Core;

namespace NekoLib.Navigation.Diagnostics
{
    /// <summary>
    /// Forwards navigation events from a <see cref="NavigationEventHub"/> into an
    /// <see cref="IDebugUtils"/> sink and exposes the most recent navigation as a
    /// pull-based state snapshot under <c>Navigation::current</c>.
    ///
    /// <para>
    /// This is a pure subscriber on the public event hub — it never touches the
    /// frozen <see cref="NavigationContext"/> lifecycle, so observability is fully
    /// opt-in and removable. Attaching with a disabled <see cref="IDebugUtils"/>
    /// (e.g. <see cref="NullDebugUtils"/>) is a no-op that allocates nothing beyond
    /// a shared empty handle.
    /// </para>
    /// </summary>
    public sealed class DebugUtilsNavigationObserver : IDisposable
    {
        /// <summary>Module name used for every recorded operation and state key.</summary>
        public const string Module = "Navigation";

        private readonly NavigationEventHub _hub;
        private readonly IDebugUtils _debug;
        private readonly IDisposable _stateRegistration;

        private volatile object _lastState = "<no navigation yet>";
        private int _disposed;

        private DebugUtilsNavigationObserver(NavigationEventHub hub, IDebugUtils debug)
        {
            _hub = hub;
            _debug = debug;

            _hub.NavigationLogged += OnNavigationLogged;
            _hub.GuardDenied += OnGuardDenied;

            _stateRegistration = _debug.RegisterStateProvider(Module, "current", () => _lastState);
        }

        /// <summary>
        /// Attaches an observer to the context's event hub. Dispose the returned
        /// handle to detach. No-op (returns a shared empty handle) when
        /// <paramref name="debug"/> is null or disabled.
        /// </summary>
        public static IDisposable Attach(NavigationContext context, IDebugUtils debug)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));
            return Attach(context.Events, debug);
        }

        /// <inheritdoc cref="Attach(NavigationContext, IDebugUtils)"/>
        public static IDisposable Attach(NavigationEventHub hub, IDebugUtils debug)
        {
            if (hub is null) throw new ArgumentNullException(nameof(hub));
            if (debug is null || !debug.IsEnabled) return Disposable.Empty;
            return new DebugUtilsNavigationObserver(hub, debug);
        }

        private void OnNavigationLogged(PageLogEntry entry)
        {
            if (entry is null) return;

            // Immutable snapshot, reused as both the recorded payload and the
            // pull-based "current" state. Anonymous type is fine: the consumer reads
            // it via ToString()/reflection, never by name.
            var snapshot = new
            {
                from = entry.FromPageName,
                to = entry.ToPageName,
                success = entry.Success,
                presentation = entry.Presentation.ToString(),
                loadMode = entry.LoadMode.ToString(),
                timeout = entry.IsTimeout,
                back = entry.IsBackNavigation,
                failureKind = entry.FailureKind.ToString(),
                error = entry.Error,
                timestampUtc = entry.TimestampUtc
            };

            _lastState = snapshot;
            _debug.Record(Module, entry.Success ? "Navigated" : "NavigationFailed", () => snapshot);
        }

        private void OnGuardDenied(GuardDeniedEvent e)
        {
            if (e is null) return;

            _debug.Record(Module, "GuardDenied", () => new
            {
                from = e.FromPage?.Name,
                target = e.TargetPage?.Name,
                redirect = e.RedirectPage?.Name,
                reason = e.Reason,
                timestampUtc = e.TimestampUtc
            });
        }

        /// <summary>Detaches from the hub and unregisters the state provider. Idempotent.</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

            _hub.NavigationLogged -= OnNavigationLogged;
            _hub.GuardDenied -= OnGuardDenied;
            _stateRegistration.Dispose();
        }
    }
}
