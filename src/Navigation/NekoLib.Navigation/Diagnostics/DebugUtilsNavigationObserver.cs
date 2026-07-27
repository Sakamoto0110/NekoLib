// FILE: NekoLib.Navigation/Diagnostics/DebugUtilsNavigationObserver.cs
#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using NekoLib.Core;
using NekoLib.Core.Observability;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Runtime.Core;

namespace NekoLib.Navigation.Diagnostics
{
    /// <summary>
    /// Forwards navigation telemetry into an <see cref="IDebugUtils"/> sink and
    /// exposes pull-based snapshots of the navigation state.
    ///
    /// <para>
    /// This is a pure subscriber — it never touches the frozen
    /// <see cref="NavigationContext"/> or <c>NavigationRuntime</c> lifecycle, so
    /// observability is fully opt-in and removable. Attaching with a disabled
    /// <see cref="IDebugUtils"/> (e.g. <see cref="NullDebugUtils"/>) is a no-op that
    /// allocates nothing beyond a shared empty handle.
    /// </para>
    ///
    /// <para><b>Two fidelity levels.</b>
    /// <see cref="Attach(NavigationEventHub, IDebugUtils)"/> observes only the
    /// context event hub: outcome operations (<c>Navigated</c>,
    /// <c>NavigationFailed</c>, <c>GuardDenied</c>) plus <c>current</c> and
    /// <c>stats</c> state. <see cref="Attach(NavigationContext, IDebugUtils)"/> —
    /// the path the bootstrap uses — additionally subscribes to the static
    /// <see cref="NavigationService"/> events, which are the only public seam for
    /// the *intent* of a navigation (<c>NavigationStarted</c>) and for
    /// attach/visibility transitions, and registers <c>history</c> / <c>session</c>
    /// snapshots.
    /// </para>
    ///
    /// <para><b>Why the intent signal matters:</b> the hub only emits once a
    /// navigation has resolved. If navigation hangs — a guard that never returns, a
    /// page whose <c>OnNavigatedToAsync</c> deadlocks — the hub stays silent and the
    /// ring buffer shows nothing. <c>NavigationStarted</c> without a matching
    /// outcome is the fingerprint of that freeze.
    /// </para>
    /// </summary>
    public sealed class DebugUtilsNavigationObserver : IDisposable
    {
        /// <summary>Module name used for every recorded operation and state key.</summary>
        public const string Module = "Navigation";

        private readonly NavigationEventHub _hub;
        private readonly NavigationContext? _context;
        private readonly IDebugUtils _debug;
        private readonly List<IDisposable> _registrations = new List<IDisposable>(4);
        private readonly bool _staticWired;

        private volatile object _lastState = "<no navigation yet>";
        private volatile string _lastStarted = "<none>";
        private long _lastStartedTicksUtc;
        private int _started;
        private int _navigated;
        private int _failed;
        private int _guardDenied;
        private int _timeouts;
        private int _backNavigations;
        private int _blankShell;
        private int _disposed;

        private DebugUtilsNavigationObserver(NavigationEventHub hub, NavigationContext? context, IDebugUtils debug)
        {
            _hub = hub;
            _context = context;
            _debug = debug;

            _hub.NavigationLogged += OnNavigationLogged;
            _hub.GuardDenied += OnGuardDenied;

            _registrations.Add(_debug.RegisterStateProvider(Module, "current", () => _lastState));
            _registrations.Add(_debug.RegisterStateProvider(Module, "stats", SnapshotStats));

            if (context != null)
            {
                // Static facade events: the only public seam carrying navigation
                // intent and attach/visibility transitions. Shutdown() nulls these,
                // which silently drops our handlers — harmless, and the next
                // bootstrap re-attaches a fresh observer.
                NavigationService.Navigating += OnNavigating;
                NavigationService.OnFirstPageAttached += OnFirstPageAttached;
                NavigationService.OnNoPageAttached += OnNoPageAttached;
                NavigationService.OnNoPageVisible += OnNoPageVisible;
                _staticWired = true;

                _registrations.Add(_debug.RegisterStateProvider(Module, "history", SnapshotHistory));
                _registrations.Add(_debug.RegisterStateProvider(Module, "session", SnapshotSession));
            }
        }

        /// <summary>
        /// Attaches a full-fidelity observer to a navigation context. Dispose the
        /// returned handle to detach. No-op (returns a shared empty handle) when
        /// <paramref name="debug"/> is null or disabled.
        /// </summary>
        public static IDisposable Attach(NavigationContext context, IDebugUtils debug)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));
            if (debug is null || !debug.IsEnabled) return Disposable.Empty;
            return new DebugUtilsNavigationObserver(context.Events, context, debug);
        }

        /// <summary>
        /// Attaches a hub-only observer: navigation outcomes plus <c>current</c> and
        /// <c>stats</c> state, without subscribing to the static facade. Use this
        /// when there is no context to observe, or to keep global state out of a
        /// test.
        /// </summary>
        public static IDisposable Attach(NavigationEventHub hub, IDebugUtils debug)
        {
            if (hub is null) throw new ArgumentNullException(nameof(hub));
            if (debug is null || !debug.IsEnabled) return Disposable.Empty;
            return new DebugUtilsNavigationObserver(hub, null, debug);
        }

        // ----------------------------------------------------------------
        // Hub events — navigation outcomes
        // ----------------------------------------------------------------

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

            if (entry.Success) Interlocked.Increment(ref _navigated);
            else Interlocked.Increment(ref _failed);
            if (entry.IsTimeout) Interlocked.Increment(ref _timeouts);
            if (entry.IsBackNavigation) Interlocked.Increment(ref _backNavigations);

            _debug.Record(Module, entry.Success ? "Navigated" : "NavigationFailed", () => snapshot);
        }

        private void OnGuardDenied(GuardDeniedEvent e)
        {
            if (e is null) return;

            Interlocked.Increment(ref _guardDenied);

            _debug.Record(Module, "GuardDenied", () => new
            {
                from = e.FromPage?.Name,
                target = e.TargetPage?.Name,
                redirect = e.RedirectPage?.Name,
                reason = e.Reason,
                timestampUtc = e.TimestampUtc
            });
        }

        // ----------------------------------------------------------------
        // Static facade events — intent and attach/visibility
        // ----------------------------------------------------------------

        private void OnNavigating(IPageView from, Type toType, NavigationArgs args)
        {
            var target = toType?.Name ?? "<null>";
            var startedUtc = DateTime.UtcNow;

            _lastStarted = target;
            Interlocked.Exchange(ref _lastStartedTicksUtc, startedUtc.Ticks);
            Interlocked.Increment(ref _started);

            _debug.Record(Module, "NavigationStarted", () => new
            {
                from = from?.Name,
                to = target,
                loadMode = args?.LoadMode.ToString(),
                back = args?.IsBackNavigation ?? false,
                timestampUtc = startedUtc
            });
        }

        private void OnFirstPageAttached(IPageView page)
            => _debug.Record(Module, "FirstPageAttached", () => new
            {
                page = page?.Name,
                timestampUtc = DateTime.UtcNow
            });

        // No page attached / visible means the shell went blank. Neither state is
        // reachable from the hub, and both are the classic symptom of a page leak or
        // a detach that never re-attached.
        private void OnNoPageAttached()
        {
            Interlocked.Increment(ref _blankShell);
            _debug.Record(Module, "NoPageAttached", () => new { timestampUtc = DateTime.UtcNow });
        }

        private void OnNoPageVisible()
        {
            Interlocked.Increment(ref _blankShell);
            _debug.Record(Module, "NoPageVisible", () => new { timestampUtc = DateTime.UtcNow });
        }

        // ----------------------------------------------------------------
        // Pull-based state snapshots
        // ----------------------------------------------------------------

        /// <summary>
        /// Aggregate counters. These outlive the ring buffer: once it wraps, the
        /// oldest operations are gone but the totals still show what happened.
        /// A <c>started</c> count above <c>navigated + failed</c> means a navigation
        /// was entered and never resolved.
        /// </summary>
        private object SnapshotStats()
        {
            var ticks = Interlocked.Read(ref _lastStartedTicksUtc);
            return new
            {
                started = _started,
                navigated = _navigated,
                failed = _failed,
                guardDenied = _guardDenied,
                timeouts = _timeouts,
                backNavigations = _backNavigations,
                blankShellEvents = _blankShell,
                lastStarted = _lastStarted,
                lastStartedUtc = ticks == 0 ? (DateTime?)null : new DateTime(ticks, DateTimeKind.Utc)
            };
        }

        /// <summary>
        /// Back/forward stacks as page names, newest first.
        /// <para>
        /// Best-effort: <c>NavigationHistory</c> is UI-thread-affine and carries no
        /// internal synchronization, so capturing from another thread while a
        /// navigation mutates it can throw. The sink catches that per provider and
        /// yields a placeholder rather than failing the whole capture.
        /// </para>
        /// </summary>
        private object SnapshotHistory()
        {
            var history = _context!.History;

            var back = new List<string>();
            foreach (var entry in history.HistoryBack)
                back.Add(entry?.PageName ?? "<null>");

            var forward = new List<string>();
            foreach (var entry in history.HistoryForward)
                forward.Add(entry?.PageName ?? "<null>");

            return new
            {
                canGoBack = history.CanGoBack,
                canGoForward = history.CanGoForward,
                back,
                forward
            };
        }

        /// <summary>
        /// The framework-owned session as guards see it. Copied into fresh lists so
        /// the snapshot cannot change under the consumer.
        /// </summary>
        private object SnapshotSession()
        {
            var session = _context!.Session;
            return new
            {
                authenticated = session.IsAuthenticated,
                roles = new List<string>(session.Roles),
                permissions = new List<string>(session.Permissions)
            };
        }

        /// <summary>
        /// Detaches from the hub and the static facade, and unregisters every state
        /// provider. Idempotent.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

            _hub.NavigationLogged -= OnNavigationLogged;
            _hub.GuardDenied -= OnGuardDenied;

            if (_staticWired)
            {
                // Safe even after Shutdown() nulled the events: removing a handler
                // from a null delegate is a no-op.
                NavigationService.Navigating -= OnNavigating;
                NavigationService.OnFirstPageAttached -= OnFirstPageAttached;
                NavigationService.OnNoPageAttached -= OnNoPageAttached;
                NavigationService.OnNoPageVisible -= OnNoPageVisible;
            }

            foreach (var registration in _registrations)
                registration.Dispose();
            _registrations.Clear();
        }
    }
}
