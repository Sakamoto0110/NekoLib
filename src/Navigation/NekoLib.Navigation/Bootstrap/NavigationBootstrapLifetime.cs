using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Contracts.Runtime;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Runtime.Core;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Bootstrap
{
    /// <summary>
    /// Owns native bootstrap resources that are not part of the page runtime:
    /// the idle timer, the interaction observer and their event subscriptions.
    /// Ownership transfers to <see cref="NavigationService"/> on a successful mount.
    /// </summary>
    internal sealed class NavigationBootstrapLifetime : IDisposable
    {
        private readonly IInteractionObserverService? _observer;
        private readonly ITimerAdapter _timer;
        private readonly Func<Task> _navigateIdle;
        private readonly bool _verifyIdleReached;
        private readonly object _idleSync = new object();

        private Action? _interactionHandler;
        private Action? _tickHandler;
        private NavigationDiagnostics? _diagnostics;
        private int _idleIntervalMilliseconds;
        private long _configuredTimestamp;
        private long _idleWindowTimestamp;
        private int _idleConfigured;
        private int _idleTickInProgress;
        private int _idleStopped;
        private int _disposed;
        private long _interactionGeneration;

        public NavigationBootstrapLifetime(
            IInteractionObserverService? observer,
            ITimerAdapter timer,
            Func<Task>? navigateIdle = null)
        {
            _observer = observer;
            _timer = timer ?? throw new ArgumentNullException(nameof(timer));
            _verifyIdleReached = navigateIdle == null;
            _navigateIdle = navigateIdle ?? (() => NavigationService.GoIdleAsync());
        }

        public void ConfigureIdle(int intervalMilliseconds, NavigationContext context)
        {
            if (intervalMilliseconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(intervalMilliseconds));
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(NavigationBootstrapLifetime));
            if (Volatile.Read(ref _idleStopped) != 0)
                throw new InvalidOperationException(
                    "Idle lifetime has already been stopped.");
            if (Volatile.Read(ref _idleConfigured) != 0 ||
                _interactionHandler != null ||
                _tickHandler != null)
                throw new InvalidOperationException("Idle timeout is already configured.");

            _diagnostics = context.Diagnostics;
            _idleIntervalMilliseconds = intervalMilliseconds;
            _configuredTimestamp = Stopwatch.GetTimestamp();
            _idleWindowTimestamp = _configuredTimestamp;

            if (_observer == null)
            {
                Volatile.Write(ref _idleConfigured, 1);
                EmitIdle(
                    NavigationTraceKind.IdleConfigured,
                    outcome: NavigationTraceOutcome.Failed,
                    success: false,
                    decision: "InteractionObserverUnavailable");
                return;
            }

            _timer.IntervalMilliseconds = intervalMilliseconds;

            _interactionHandler = () =>
            {
                lock (_idleSync)
                {
                    if (!CanHandleIdle(context))
                        return;

                    _interactionGeneration++;
                    _idleWindowTimestamp = Stopwatch.GetTimestamp();
                    _timer.Stop();
                    _timer.Start();
                }

                EmitIdle(
                    NavigationTraceKind.IdleInteraction,
                    outcome: NavigationTraceOutcome.Succeeded,
                    success: true,
                    decision: "TimerReset");
            };

            _tickHandler = async () =>
            {
                long interactionGeneration;

                // Evaluated before taking the idle lock: this reaches into the
                // runtime through NavigationService.Current, and the idle lock must
                // not be held across that.
                var settled = IsIdleStateSettled(context);

                lock (_idleSync)
                {
                    if (!CanHandleIdle(context) ||
                        _idleTickInProgress != 0)
                    {
                        return;
                    }

                    // The runtime is already idle and already signed out, so there
                    // is nothing to transition to. Stay armed and stay quiet rather
                    // than re-running the whole transition every interval, which
                    // would dispose and recreate a transient idle page — and emit a
                    // trace — for as long as the terminal is unattended.
                    if (settled)
                    {
                        _idleWindowTimestamp = Stopwatch.GetTimestamp();
                        _timer.Stop();
                        _timer.Start();
                        return;
                    }

                    _idleTickInProgress = 1;
                    interactionGeneration = _interactionGeneration;
                    _timer.Stop();
                }

                try
                {
                    var elapsedMilliseconds = ElapsedSince(
                        Interlocked.Read(ref _idleWindowTimestamp));
                    EmitIdle(
                        NavigationTraceKind.IdleElapsed,
                        elapsedMilliseconds: elapsedMilliseconds);

                    Task navigationTask;
                    lock (_idleSync)
                    {
                        // Interaction or StopIdle may have won while the elapsed
                        // event was being observed. Neither sign-out nor navigation
                        // may cross that newer idle window/stop boundary.
                        if (!CanContinueTick(
                            context,
                            interactionGeneration))
                        {
                            return;
                        }

                        context.Session.SignOut();

                        // The transition is admitted from here on. Session observers
                        // run synchronously, so an application that refreshes its UI
                        // in response to sign-out can make the platform observer
                        // report an interaction reentrantly — on WinForms a
                        // programmatic TextChanged is enough. That is the application
                        // reacting to us, not a user arriving, and it must not cancel
                        // the transition we just committed to; the terminal would sign
                        // out and then stay on the operator's page. Genuine
                        // interaction *before* admission still cancels, through the
                        // check above, which deliberately keeps comparing the
                        // interaction generation. Disposal, StopIdle and context
                        // ownership are still revalidated here.
                        if (!CanHandleIdle(context))
                        {
                            return;
                        }

                        // Invoke under the same gate used by StopIdle. Once StopIdle
                        // returns, no handler can begin a new idle navigation.
                        navigationTask = _navigateIdle();
                    }

                    await navigationTask;

                    if (_verifyIdleReached &&
                        !IsIdlePageCurrent(context))
                    {
                        EmitIdle(
                            NavigationTraceKind.IdleNavigationFailed,
                            outcome: NavigationTraceOutcome.Failed,
                            success: false,
                            decision: "IdlePageNotReached",
                            elapsedMilliseconds: ElapsedSince(
                                Interlocked.Read(
                                    ref _idleWindowTimestamp)));
                    }

                    // Rearm after every completed tick, not only after a denied or
                    // failed one. A successful transition used to leave the timer
                    // stopped until the next user interaction inside the host, so
                    // anything that later moved off the idle page without input —
                    // ResetAsync, an IPC event, background work — left an
                    // unattended terminal with no idle timeout at all.
                    TryRearmIdle(
                        context,
                        interactionGeneration);
                }
                catch (Exception ex)
                {
                    if (Volatile.Read(ref _disposed) == 0 &&
                        Volatile.Read(ref _idleStopped) == 0)
                    {
                        EmitIdle(
                            NavigationTraceKind.IdleNavigationFailed,
                            outcome: NavigationTraceOutcome.Failed,
                            success: false,
                            decision: "IdleNavigationFailed",
                            errorType: ex.GetType().FullName ?? ex.GetType().Name,
                            elapsedMilliseconds: ElapsedSince(
                                Interlocked.Read(ref _idleWindowTimestamp)));
                    }
                    System.Diagnostics.Debug.WriteLine(
                        "[PageNavBootstrap] Idle timeout navigation failed: " + ex);
                    TryRearmIdle(
                        context,
                        interactionGeneration);
                }
                finally
                {
                    lock (_idleSync)
                    {
                        _idleTickInProgress = 0;
                    }
                }
            };

            _observer.InteractionDetected += _interactionHandler;
            _timer.Tick += _tickHandler;

            // Start immediately so an unattended boot also lands on the idle page.
            _timer.Start();
            Volatile.Write(ref _idleConfigured, 1);
            EmitIdle(
                NavigationTraceKind.IdleConfigured,
                outcome: NavigationTraceOutcome.Succeeded,
                success: true,
                decision: "Configured");
        }

        /// <summary>
        /// Stops idle activity without disposing the shared interaction observer.
        /// Shutdown calls this before runtime teardown so a timer tick cannot enqueue
        /// a fresh navigation while pages are being detached.
        /// </summary>
        internal void StopIdle()
        {
            Action? interactionHandler;
            Action? tickHandler;

            lock (_idleSync)
            {
                if (_idleStopped != 0)
                    return;

                _idleStopped = 1;
                interactionHandler = _interactionHandler;
                tickHandler = _tickHandler;
                _interactionHandler = null;
                _tickHandler = null;
            }

            if (_observer != null && interactionHandler != null)
            {
                try { _observer.InteractionDetected -= interactionHandler; }
                catch { }
            }

            if (tickHandler != null)
            {
                try { _timer.Tick -= tickHandler; }
                catch { }
            }

            try { _timer.Stop(); }
            catch { }

            if (Volatile.Read(ref _idleConfigured) != 0)
            {
                EmitIdle(
                    NavigationTraceKind.IdleDisposed,
                    outcome: NavigationTraceOutcome.Succeeded,
                    success: true,
                    decision: "Disposed",
                    elapsedMilliseconds: ElapsedSince(_configuredTimestamp));
            }
        }

        private bool CanHandleIdle(NavigationContext context)
        {
            return Volatile.Read(ref _disposed) == 0 &&
                   Volatile.Read(ref _idleStopped) == 0 &&
                   NavigationService.IsCurrentContext(context);
        }

        private bool CanContinueTick(
            NavigationContext context,
            long interactionGeneration)
        {
            return CanHandleIdle(context) &&
                   _interactionGeneration == interactionGeneration;
        }

        private void TryRearmIdle(
            NavigationContext context,
            long interactionGeneration)
        {
            try
            {
                lock (_idleSync)
                {
                    // An interaction that arrived while the tick was running has
                    // already rearmed the timer, and a stopped or disposed lifetime
                    // must not rearm at all.
                    if (!CanContinueTick(
                        context,
                        interactionGeneration))
                    {
                        return;
                    }

                    _idleWindowTimestamp = Stopwatch.GetTimestamp();
                    _timer.Start();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[PageNavBootstrap] Idle timer rearm failed: " + ex);
            }
        }

        /// <summary>
        /// True when a tick has nothing to do: the runtime already shows the idle
        /// page and the session is already signed out.
        /// <para>
        /// Only meaningful when this lifetime owns the real idle navigation. A
        /// caller-supplied navigation is opaque — its effect cannot be verified —
        /// so it is never skipped.
        /// </para>
        /// </summary>
        private bool IsIdleStateSettled(NavigationContext context)
        {
            return _verifyIdleReached &&
                   !context.Session.IsAuthenticated &&
                   IsIdlePageCurrent(context);
        }

        private static bool IsIdlePageCurrent(
            NavigationContext context)
        {
            var idle = IdlePageRules.Resolve(
                context.Registry.AllDescriptors());
            var current = NavigationService.Current;
            return idle != null &&
                   current != null &&
                   idle.PageType.IsInstanceOfType(current);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            StopIdle();

            try { _timer.Dispose(); }
            catch { }

            if (_observer is IDisposable disposableObserver)
            {
                try { disposableObserver.Dispose(); }
                catch { }
            }
        }

        private void EmitIdle(
            NavigationTraceKind kind,
            NavigationTraceOutcome outcome = NavigationTraceOutcome.None,
            bool? success = null,
            string? decision = null,
            string? errorType = null,
            long elapsedMilliseconds = 0)
        {
            var diagnostics = _diagnostics;
            if (diagnostics == null ||
                !diagnostics.TraceEventsEnabled ||
                (kind != NavigationTraceKind.IdleDisposed &&
                 Volatile.Read(ref _idleStopped) != 0))
                return;

            diagnostics.EmitTrace(new NavigationTraceEvent(
                kind,
                diagnostics.RuntimeId,
                outcome: outcome,
                trigger: NavigationTraceTrigger.Idle,
                decision: decision,
                errorType: errorType,
                success: success,
                idleIntervalMilliseconds: _idleIntervalMilliseconds,
                timestampUtc: DateTime.UtcNow,
                elapsedMilliseconds: elapsedMilliseconds));
        }

        private static long ElapsedSince(long startedTimestamp)
        {
            if (startedTimestamp == 0)
                return 0;

            var elapsedTicks = Stopwatch.GetTimestamp() - startedTimestamp;
            return (long)(elapsedTicks * 1000d / Stopwatch.Frequency);
        }
    }
}
