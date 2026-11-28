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
        private readonly IEventDispatcherAdapter? _dispatcher;
        private readonly Func<Task>? _navigateIdle;
        private readonly Func<long> _getTimestamp;
        private readonly object _idleSync = new object();

        private Action? _interactionHandler;
        private Action? _tickHandler;
        private SessionTimeoutLifetime? _sessionTimeout;
        private NavigationDiagnostics? _diagnostics;
        private TimeSpan _idleInterval;
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
            Func<Task>? navigateIdle = null,
            IEventDispatcherAdapter? dispatcher = null,
            Func<long>? getTimestamp = null)
        {
            _observer = observer;
            _timer = timer ?? throw new ArgumentNullException(nameof(timer));
            _dispatcher = dispatcher;
            _navigateIdle = navigateIdle;
            _getTimestamp = getTimestamp ?? Stopwatch.GetTimestamp;
        }

        internal void ConfigureSessionTimeout(
            TimeSpan timeout,
            NavigationContext context,
            Func<long>? getTimestamp = null,
            Func<int, CancellationToken, Task>? delay = null)
            => _sessionTimeout = new SessionTimeoutLifetime(context, timeout, getTimestamp, delay);

        public void ConfigureIdle(TimeSpan interval, NavigationContext context)
        {
            if (interval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(interval));
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
            _idleInterval = interval;
            _configuredTimestamp = _getTimestamp();
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

            _timer.IntervalMilliseconds = TimeoutTiming.GetTimerIntervalMilliseconds(_idleInterval);

            _interactionHandler = () => RecordInteraction(context);

            _tickHandler = async () =>
            {
                long interactionGeneration;
                TimeSpan elapsed;
                int sessionGeneration;

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

                    elapsed = ElapsedSince(_idleWindowTimestamp);
                    if (elapsed < _idleInterval)
                    {
                        // A native tick may already have been queued when input
                        // renewed the window. Keep the new deadline intact.
                        RestartTimer(_idleInterval - elapsed);
                        return;
                    }

                    // The runtime is already idle and already signed out, so there
                    // is nothing to transition to. Stay armed and stay quiet rather
                    // than re-running the whole transition every interval, which
                    // would dispose and recreate a transient idle page — and emit a
                    // trace — for as long as the terminal is unattended.
                    if (settled)
                    {
                        _idleWindowTimestamp = _getTimestamp();
                        RestartTimer(_idleInterval);
                        return;
                    }

                    _idleTickInProgress = 1;
                    interactionGeneration = _interactionGeneration;
                    sessionGeneration = context.Session.Generation;
                    _timer.Stop();
                }

                bool transitionStarted = false;

                bool TryBeginTransition()
                {
                    lock (_idleSync)
                    {
                        // Recheck after acquiring the runtime navigation gate.
                        if (!CanContinueTick(context, interactionGeneration) ||
                            !context.Session.TrySignOut(sessionGeneration))
                            return false;

                        // A synchronous sign-out callback can itself report
                        // activity. Teardown or a replacement sign-in can still
                        // prevent the old session from changing the page.
                        transitionStarted = CanHandleIdle(context) && !context.Session.IsAuthenticated;
                        return transitionStarted;
                    }
                }

                try
                {
                    EmitIdle(
                        NavigationTraceKind.IdleElapsed,
                        elapsedMilliseconds: (long)elapsed.TotalMilliseconds);

                    if (_navigateIdle == null)
                        await NavigationService.ExpireIdleAsync(context, TryBeginTransition);
                    else if (TryBeginTransition())
                        await _navigateIdle();

                    if (_navigateIdle == null && transitionStarted &&
                        !IsIdlePageCurrent(context))
                    {
                        EmitIdle(
                            NavigationTraceKind.IdleNavigationFailed,
                            outcome: NavigationTraceOutcome.Failed,
                            success: false,
                            decision: "IdlePageNotReached",
                            elapsedMilliseconds: (long)elapsed.TotalMilliseconds);
                    }
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
                            elapsedMilliseconds: (long)elapsed.TotalMilliseconds);
                    }
                    System.Diagnostics.Debug.WriteLine(
                        "[PageNavBootstrap] Idle timeout navigation failed: " + ex);
                }
                finally
                {
                    FinishIdleTick(context, interactionGeneration);
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

        internal void NotifyUserInteraction(NavigationContext context)
        {
            if (_observer == null || Volatile.Read(ref _idleConfigured) == 0 ||
                !CanHandleIdle(context))
                return;

            if (_dispatcher != null) _dispatcher.Invoke(() => RecordInteraction(context));
            else RecordInteraction(context);
        }

        private void RecordInteraction(NavigationContext context)
        {
            lock (_idleSync)
            {
                if (!CanHandleIdle(context))
                    return;

                _interactionGeneration++;
                _idleWindowTimestamp = _getTimestamp();
                RestartTimer(_idleInterval);
            }

            EmitIdle(
                NavigationTraceKind.IdleInteraction,
                outcome: NavigationTraceOutcome.Succeeded,
                success: true,
                decision: "TimerReset");
        }

        private void RestartTimer(TimeSpan interval)
        {
            _timer.Stop();
            _timer.IntervalMilliseconds = TimeoutTiming.GetTimerIntervalMilliseconds(interval);
            _timer.Start();
        }

        /// <summary>
        /// Stops inactivity and session deadlines before runtime teardown while
        /// keeping the shared interaction observer alive for the runtime.
        /// </summary>
        internal void StopIdle()
        {
            if (Volatile.Read(ref _idleStopped) != 0) return;
            if (_dispatcher != null) _dispatcher.Invoke(StopIdleCore);
            else StopIdleCore();
        }

        private void StopIdleCore()
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

            _sessionTimeout?.Dispose();
            _sessionTimeout = null;

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
                    elapsedMilliseconds: (long)ElapsedSince(_configuredTimestamp).TotalMilliseconds);
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

        private void FinishIdleTick(
            NavigationContext context,
            long interactionGeneration)
        {
            try
            {
                lock (_idleSync)
                {
                    _idleTickInProgress = 0;
                    if (!CanHandleIdle(context))
                        return;

                    if (_interactionGeneration == interactionGeneration)
                    {
                        // Keep watching after both successful and failed transitions.
                        _idleWindowTimestamp = _getTimestamp();
                        _timer.IntervalMilliseconds = TimeoutTiming.GetTimerIntervalMilliseconds(_idleInterval);
                        _timer.Start();
                    }
                    else if (ElapsedSince(_idleWindowTimestamp) >= _idleInterval)
                    {
                        // The renewed window expired while this tick was in flight.
                        RestartTimer(TimeSpan.FromMilliseconds(1));
                    }
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
            return _navigateIdle == null &&
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
            if (Volatile.Read(ref _disposed) != 0) return;
            if (_dispatcher != null) _dispatcher.Invoke(DisposeCore);
            else DisposeCore();
        }

        private void DisposeCore()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            StopIdleCore();

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
                idleIntervalMilliseconds: (long)Math.Ceiling(_idleInterval.TotalMilliseconds),
                timestampUtc: DateTime.UtcNow,
                elapsedMilliseconds: elapsedMilliseconds));
        }

        private TimeSpan ElapsedSince(long startedTimestamp)
            => TimeoutTiming.GetElapsedTime(startedTimestamp, _getTimestamp());
    }
}
