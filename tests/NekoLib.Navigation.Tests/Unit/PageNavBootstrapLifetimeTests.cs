using NekoLib.Navigation.Bootstrap;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Contracts.Runtime;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Tests.Unit.Fakes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    [Collection("NavigationServiceFacade")]
    public class PageNavBootstrapLifetimeTests
    {
        [Fact]
        public async Task Shutdown_DisposesIdleTimerObserverAndSubscriptions()
        {
            await NavigationService.Shutdown();
            var bootstrap = PageNavBootstrap
                .Use<TrackingPlatformAdapter>(new object())
                .UseIdleTimeout(1_000);
            var adapter = TrackingPlatformAdapter.LastCreated;

            try
            {
                bootstrap.Start();

                Assert.True(adapter.Timer.IsStarted);
                Assert.Equal(1, adapter.Timer.SubscriberCount);
                Assert.Equal(1, adapter.Observer.SubscriberCount);

                await NavigationService.Shutdown();

                Assert.True(adapter.Timer.IsDisposed);
                Assert.True(adapter.Observer.IsDisposed);
                Assert.Equal(0, adapter.Timer.SubscriberCount);
                Assert.Equal(0, adapter.Observer.SubscriberCount);
            }
            finally
            {
                await NavigationService.Shutdown();
            }
        }

        [Fact]
        public async Task Start_WhenFacadeAlreadyMounted_DisposesRejectedBootstrapResources()
        {
            await NavigationService.Shutdown();
            var first = PageNavBootstrap.Use<TrackingPlatformAdapter>(new object());
            first.Start();

            try
            {
                var second = PageNavBootstrap
                    .Use<TrackingPlatformAdapter>(new object())
                    .UseIdleTimeout(1_000);
                var rejectedAdapter = TrackingPlatformAdapter.LastCreated;

                Assert.Throws<InvalidOperationException>(() => second.Start());

                Assert.True(rejectedAdapter.Timer.IsDisposed);
                Assert.True(rejectedAdapter.Observer.IsDisposed);
                Assert.Equal(0, rejectedAdapter.Timer.SubscriberCount);
                Assert.Equal(0, rejectedAdapter.Observer.SubscriberCount);
            }
            finally
            {
                await NavigationService.Shutdown();
            }
        }

        [Fact]
        public async Task Start_WhenServiceConfigurationFails_DisposesCreatedPlatformResources()
        {
            await NavigationService.Shutdown();
            var bootstrap = PageNavBootstrap
                .Use<TrackingPlatformAdapter>(new object())
                .ConfigureServices((_, __) =>
                    throw new InvalidOperationException("configuration failed"));
            var adapter = TrackingPlatformAdapter.LastCreated;

            Assert.Throws<InvalidOperationException>(() => bootstrap.Start());

            Assert.True(adapter.Timer.IsDisposed);
            Assert.True(adapter.Observer.IsDisposed);
            Assert.Equal(0, adapter.Timer.SubscriberCount);
            Assert.Equal(0, adapter.Observer.SubscriberCount);
        }

        [Fact]
        public async Task UseContext_DoubleMount_DisposesBothIncomingHandlesEvenWhenOneThrows()
        {
            await NavigationService.Shutdown();
            var mounted = RuntimeTestFixture.Build<StubIdle>();
            var rejected = RuntimeTestFixture.Build<StubIdle>();
            NavigationService.UseContext(mounted.Context);

            var throwingObserver = new ThrowingDisposable();
            var lifetime = new TrackingDisposable();

            try
            {
                Assert.Throws<InvalidOperationException>(
                    () => NavigationService.UseContext(
                        rejected.Context,
                        throwingObserver,
                        lifetime));

                Assert.Equal(1, throwingObserver.DisposeCalls);
                Assert.Equal(1, lifetime.DisposeCalls);
            }
            finally
            {
                await NavigationService.Shutdown();
            }
        }

        // NAV-011: a successful idle transition used to leave the timer stopped
        // until the next interaction inside the host, so anything that later moved
        // off the idle page without user input left the shell with no idle timeout.
        [Fact]
        public async Task IdleTick_AfterSuccessfulTransition_KeepsTimerArmed()
        {
            await NavigationService.Shutdown();
            var fixture = RuntimeTestFixture.Build<StubIdle>();
            NavigationService.UseContext(fixture.Context);

            var timer = new TrackingTimer();
            var observer = new TrackingObserver();
            var lifetime = new NavigationBootstrapLifetime(
                observer,
                timer,
                () => Task.CompletedTask);

            try
            {
                lifetime.ConfigureIdle(1_000, fixture.Context);
                var startCallsBeforeTick = timer.StartCalls;

                timer.RaiseTick();

                Assert.True(timer.IsStarted);
                Assert.True(timer.StartCalls > startCallsBeforeTick);
            }
            finally
            {
                lifetime.Dispose();
                await NavigationService.Shutdown();
            }
        }

        [Fact]
        public async Task IdleTick_AfterStopIdle_DoesNotRearmTimer()
        {
            await NavigationService.Shutdown();
            var fixture = RuntimeTestFixture.Build<StubIdle>();
            NavigationService.UseContext(fixture.Context);

            var timer = new TrackingTimer();
            var observer = new TrackingObserver();
            var lifetime = new NavigationBootstrapLifetime(
                observer,
                timer,
                () => Task.CompletedTask);

            try
            {
                lifetime.ConfigureIdle(1_000, fixture.Context);
                lifetime.StopIdle();
                var startCallsAfterStop = timer.StartCalls;

                timer.RaiseTick();

                Assert.False(timer.IsStarted);
                Assert.Equal(startCallsAfterStop, timer.StartCalls);
            }
            finally
            {
                lifetime.Dispose();
                await NavigationService.Shutdown();
            }
        }

        [Fact]
        public async Task IdleTick_WhenAlreadyIdleAndSignedOut_StaysArmedWithoutRenavigating()
        {
            await NavigationService.Shutdown();
            var fixture = RuntimeTestFixture.Build<StubIdle>();
            NavigationService.UseContext(fixture.Context);

            var timer = new TrackingTimer();
            var observer = new TrackingObserver();

            // No navigateIdle override: this lifetime owns the real GoIdleAsync and
            // can therefore verify — and skip — an already-settled idle state.
            var lifetime = new NavigationBootstrapLifetime(observer, timer);

            var navigations = 0;
            NavigationService.Navigated += (_, __, ___) => navigations++;

            try
            {
                lifetime.ConfigureIdle(1_000, fixture.Context);

                timer.RaiseTick();
                await Task.Yield();
                Assert.IsType<StubIdle>(NavigationService.Current);
                var navigationsAfterFirstTick = navigations;
                Assert.True(navigationsAfterFirstTick > 0);

                // Already idle and already signed out: the tick must do nothing but
                // keep the watchdog armed.
                timer.RaiseTick();
                await Task.Yield();

                Assert.Equal(navigationsAfterFirstTick, navigations);
                Assert.True(timer.IsStarted);
            }
            finally
            {
                lifetime.Dispose();
                await NavigationService.Shutdown();
            }
        }

        [Fact]
        public async Task IdleLifecycle_EmitsConfiguredInteractionElapsedAndDisposed()
        {
            await NavigationService.Shutdown();
            var fixture = RuntimeTestFixture.Build<StubIdle>();
            var traces = new List<NavigationTraceEvent>();
            fixture.Context.Events.NavigationTrace += traces.Add;
            NavigationService.UseContext(fixture.Context);

            var timer = new TrackingTimer();
            var observer = new TrackingObserver();
            var lifetime = new NavigationBootstrapLifetime(
                observer,
                timer,
                () => Task.CompletedTask);

            try
            {
                lifetime.ConfigureIdle(1_234, fixture.Context);
                observer.RaiseInteraction();
                timer.RaiseTick();
                lifetime.StopIdle();
                timer.RaiseTick();

                var idle = traces
                    .Where(e =>
                        e.Kind >= NavigationTraceKind.IdleConfigured &&
                        e.Kind <= NavigationTraceKind.IdleDisposed)
                    .ToArray();
                Assert.Equal(
                    new[]
                    {
                        NavigationTraceKind.IdleConfigured,
                        NavigationTraceKind.IdleInteraction,
                        NavigationTraceKind.IdleElapsed,
                        NavigationTraceKind.IdleDisposed
                    },
                    idle.Select(e => e.Kind).ToArray());
                Assert.All(
                    idle,
                    e =>
                    {
                        Assert.Equal(
                            fixture.Context.Diagnostics.RuntimeId,
                            e.RuntimeId);
                        Assert.Equal(1_234, e.IdleIntervalMilliseconds);
                    });
                Assert.Equal("Configured", idle[0].Decision);
                Assert.True(idle[0].Success);
                Assert.Equal("TimerReset", idle[1].Decision);
                Assert.True(idle[2].ElapsedMilliseconds >= 0);
                Assert.Equal("Disposed", idle[3].Decision);
            }
            finally
            {
                lifetime.Dispose();
                await NavigationService.Shutdown();
            }
        }

        [Fact]
        public void ConfigureIdle_WithoutInteractionObserver_EmitsUnavailable()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>();
            var traces = new List<NavigationTraceEvent>();
            fixture.Context.Events.NavigationTrace += traces.Add;
            var timer = new TrackingTimer();
            var lifetime = new NavigationBootstrapLifetime(null, timer);

            try
            {
                lifetime.ConfigureIdle(900, fixture.Context);

                var configured = Assert.Single(
                    traces.Where(
                        e => e.Kind == NavigationTraceKind.IdleConfigured));
                Assert.False(configured.Success);
                Assert.Equal(
                    "InteractionObserverUnavailable",
                    configured.Decision);
                Assert.Equal(900, configured.IdleIntervalMilliseconds);
                Assert.False(timer.IsStarted);
            }
            finally
            {
                lifetime.Dispose();
            }
        }

        [Fact]
        public async Task StopIdle_DuringPendingTick_DoesNotEmitFailureAfterDisposed()
        {
            await NavigationService.Shutdown();
            var fixture = RuntimeTestFixture.Build<StubIdle>();
            var traces = new List<NavigationTraceEvent>();
            fixture.Context.Events.NavigationTrace += traces.Add;
            NavigationService.UseContext(fixture.Context);

            var navigation = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var timer = new TrackingTimer();
            var observer = new TrackingObserver();
            var lifetime = new NavigationBootstrapLifetime(
                observer,
                timer,
                () => navigation.Task);

            try
            {
                lifetime.ConfigureIdle(500, fixture.Context);
                timer.RaiseTick();
                lifetime.StopIdle();
                navigation.TrySetException(
                    new InvalidOperationException("late failure"));

                await Task.Delay(50);

                var idle = traces
                    .Where(e =>
                        e.Kind >= NavigationTraceKind.IdleConfigured &&
                        e.Kind <= NavigationTraceKind.IdleDisposed)
                    .ToArray();
                Assert.Equal(
                    NavigationTraceKind.IdleDisposed,
                    idle[idle.Length - 1].Kind);
                Assert.DoesNotContain(
                    idle,
                    e => e.Kind ==
                         NavigationTraceKind.IdleNavigationFailed);
            }
            finally
            {
                lifetime.Dispose();
                await NavigationService.Shutdown();
            }
        }

        [Fact]
        public async Task IdleNavigationFailure_EmitsErrorTypeBeforeDisposal()
        {
            await NavigationService.Shutdown();
            var fixture = RuntimeTestFixture.Build<StubIdle>();
            var traces = new List<NavigationTraceEvent>();
            fixture.Context.Events.NavigationTrace += traces.Add;
            NavigationService.UseContext(fixture.Context);

            var timer = new TrackingTimer();
            var observer = new TrackingObserver();
            var lifetime = new NavigationBootstrapLifetime(
                observer,
                timer,
                () => Task.FromException(
                    new InvalidOperationException("idle failed")));

            try
            {
                lifetime.ConfigureIdle(750, fixture.Context);
                timer.RaiseTick();

                for (int i = 0;
                     i < 100 &&
                     !traces.Any(e =>
                         e.Kind ==
                         NavigationTraceKind.IdleNavigationFailed);
                     i++)
                {
                    await Task.Delay(10);
                }

                var failed = Assert.Single(
                    traces.Where(
                        e => e.Kind ==
                             NavigationTraceKind.IdleNavigationFailed));
                Assert.Equal(
                    typeof(InvalidOperationException).FullName,
                    failed.ErrorType);
                Assert.False(failed.Success);
                Assert.Equal(750, failed.IdleIntervalMilliseconds);
                Assert.True(timer.IsStarted);
                Assert.True(timer.StartCalls >= 2);
            }
            finally
            {
                lifetime.Dispose();
                await NavigationService.Shutdown();
            }
        }

        [Fact]
        public async Task InteractionDuringElapsed_InvalidatesTickBeforeSignOutAndNavigation()
        {
            await NavigationService.Shutdown();
            var fixture = RuntimeTestFixture.Build<StubIdle>();
            NavigationService.UseContext(fixture.Context);
            fixture.Context.Session.SignIn("operator");

            var timer = new TrackingTimer();
            var observer = new TrackingObserver();
            int navigationCalls = 0;
            var lifetime = new NavigationBootstrapLifetime(
                observer,
                timer,
                () =>
                {
                    navigationCalls++;
                    return Task.CompletedTask;
                });
            fixture.Context.Events.NavigationTrace += trace =>
            {
                if (trace.Kind == NavigationTraceKind.IdleElapsed)
                    observer.RaiseInteraction();
            };

            try
            {
                lifetime.ConfigureIdle(600, fixture.Context);
                timer.RaiseTick();

                Assert.True(fixture.Context.Session.IsAuthenticated);
                Assert.Equal(0, navigationCalls);
                Assert.True(timer.IsStarted);
                Assert.True(timer.StartCalls >= 2);
            }
            finally
            {
                lifetime.Dispose();
                await NavigationService.Shutdown();
            }
        }

        [Fact]
        public async Task StopIdleDuringElapsed_PreventsSignOutAndNavigation()
        {
            await NavigationService.Shutdown();
            var fixture = RuntimeTestFixture.Build<StubIdle>();
            NavigationService.UseContext(fixture.Context);
            fixture.Context.Session.SignIn("operator");

            var timer = new TrackingTimer();
            var observer = new TrackingObserver();
            int navigationCalls = 0;
            var lifetime = new NavigationBootstrapLifetime(
                observer,
                timer,
                () =>
                {
                    navigationCalls++;
                    return Task.CompletedTask;
                });
            fixture.Context.Events.NavigationTrace += trace =>
            {
                if (trace.Kind == NavigationTraceKind.IdleElapsed)
                    lifetime.StopIdle();
            };

            try
            {
                lifetime.ConfigureIdle(650, fixture.Context);
                timer.RaiseTick();

                Assert.True(fixture.Context.Session.IsAuthenticated);
                Assert.Equal(0, navigationCalls);
                Assert.False(timer.IsStarted);
            }
            finally
            {
                lifetime.Dispose();
                await NavigationService.Shutdown();
            }
        }

        [Fact]
        public async Task GuardDeniedIdleNavigation_RearmsTimer()
        {
            await NavigationService.Shutdown();
            var fixture =
                RuntimeTestFixture.Build<StubAuthenticated>();
            var traces = new List<NavigationTraceEvent>();
            fixture.Context.Events.NavigationTrace += traces.Add;
            NavigationService.UseContext(fixture.Context);

            var timer = new TrackingTimer();
            var observer = new TrackingObserver();
            var lifetime = new NavigationBootstrapLifetime(
                observer,
                timer);

            try
            {
                lifetime.ConfigureIdle(700, fixture.Context);
                timer.RaiseTick();

                for (int i = 0;
                     i < 100 && timer.StartCalls < 2;
                     i++)
                {
                    await Task.Delay(10);
                }

                Assert.True(timer.IsStarted);
                Assert.True(timer.StartCalls >= 2);
                Assert.Null(NavigationService.Current);
                Assert.Contains(
                    traces,
                    e =>
                        e.Kind ==
                            NavigationTraceKind.IdleNavigationFailed &&
                        e.Decision == "IdlePageNotReached");
            }
            finally
            {
                lifetime.Dispose();
                await NavigationService.Shutdown();
            }
        }

        private sealed class TrackingPlatformAdapter : IPlatformAdapter
        {
            public static TrackingPlatformAdapter LastCreated { get; private set; }

            public TrackingTimer Timer { get; } = new TrackingTimer();
            public TrackingObserver Observer { get; } = new TrackingObserver();

            public TrackingPlatformAdapter()
            {
                LastCreated = this;
            }

            public bool CanHandle(object host) => true;
            public IPageHost CreateHost(object host) => new FakePageHost();
            public IEventDispatcherAdapter CreateEventDispatcher(object host)
                => new SyncEventDispatcherAdapter();
            public IInteractionBlocker CreateInteractionBlocker(object host)
                => new NoOpInteractionBlocker();
            public ITimerAdapter CreateTimerAdapter() => Timer;
            public IInteractionObserverService CreateInteractionObserverAdapter(object host)
                => Observer;
            public IEventSubscriptionAdapter CreateEventSubscriber(object host) => null;
            public IFocusObserverAdapter CreateFocusObserver(object host) => null;
            public Type GetDefaultLoadingMaskType() => null;
        }

        private sealed class TrackingTimer : ITimerAdapter
        {
            private Action _tick;

            public int IntervalMilliseconds { get; set; }
            public bool IsStarted { get; private set; }
            public bool IsDisposed { get; private set; }
            public int StartCalls { get; private set; }
            public int StopCalls { get; private set; }
            public int SubscriberCount => _tick?.GetInvocationList().Length ?? 0;

            public event Action Tick
            {
                add { _tick += value; }
                remove { _tick -= value; }
            }

            public void Start()
            {
                StartCalls++;
                IsStarted = true;
            }

            public void Stop()
            {
                StopCalls++;
                IsStarted = false;
            }
            public void RaiseTick()
                => _tick?.Invoke();

            public void Dispose()
            {
                IsStarted = false;
                IsDisposed = true;
            }
        }

        private sealed class TrackingObserver : IInteractionObserverService, IDisposable
        {
            private Action _interactionDetected;

            public bool IsDisposed { get; private set; }
            public int SubscriberCount =>
                _interactionDetected?.GetInvocationList().Length ?? 0;

            public event Action InteractionDetected
            {
                add { _interactionDetected += value; }
                remove { _interactionDetected -= value; }
            }

            public void RaiseInteraction()
                => _interactionDetected?.Invoke();

            public void Dispose()
            {
                IsDisposed = true;
                _interactionDetected = null;
            }
        }

        private sealed class NoOpInteractionBlocker : IInteractionBlocker
        {
            public void Block() { }
            public void Unblock() { }
        }

        private sealed class ThrowingDisposable : IDisposable
        {
            public int DisposeCalls { get; private set; }

            public void Dispose()
            {
                DisposeCalls++;
                throw new InvalidOperationException("dispose failed");
            }
        }

        private sealed class TrackingDisposable : IDisposable
        {
            public int DisposeCalls { get; private set; }
            public void Dispose() => DisposeCalls++;
        }
    }
}
