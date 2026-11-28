using NekoLib.Navigation.Bootstrap;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Contracts.Runtime;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Runtime.Services;
using NekoLib.Navigation.Tests.Unit.Fakes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public async Task ContextProperties_BeforeStartAndAfterShutdown_ReportPublicBootstrapGuidance()
        {
            await NavigationService.Shutdown();
            AssertUnmountedPropertyMessages();
            try
            {
                var context = PageNavBootstrap.Use<TrackingPlatformAdapter>(new object()).Start();
                Assert.Same(context.Session, NavigationService.Session);
            }
            finally
            {
                await NavigationService.Shutdown();
            }
            AssertUnmountedPropertyMessages();
        }

        private static void AssertUnmountedPropertyMessages()
        {
            foreach (var read in new Func<object>[]
            {
                () => NavigationService.Session,
                () => NavigationService.History,
                () => NavigationService.CanGoBack,
                () => NavigationService.Events
            })
            {
                var error = Assert.Throws<InvalidOperationException>(read);
                Assert.Contains("PageNavBootstrap.Start()", error.Message);
                Assert.Contains("await NavigationService.Shutdown()", error.Message);
                Assert.DoesNotContain("UseContext", error.Message);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ExplicitObserverFactory_OwnsResultOnShutdownAndFailedStartup(bool failStartup)
        {
            await NavigationService.Shutdown();
            var replacement = new TrackingObserver();
            var created = 0;
            var bootstrap = PageNavBootstrap.Use<TrackingPlatformAdapter>(new object())
                .UseInteractionObserver(() => { created++; return replacement; });
            var adapter = TrackingPlatformAdapter.LastCreated;
            if (failStartup)
                bootstrap.ConfigureServices((_, __) => throw new InvalidOperationException("startup failed"));
            try
            {
                if (failStartup) Assert.Throws<InvalidOperationException>(() => bootstrap.Start());
                else
                {
                    var context = bootstrap.Start();
                    Assert.Same(replacement, context.Services.Get<IInteractionObserverService>());
                    await NavigationService.Shutdown();
                }
                Assert.Equal(1, created);
                Assert.True(replacement.IsDisposed);
                // The unused platform observer was never acquired or owned.
                Assert.False(adapter.Observer.IsDisposed);
            }
            finally { await NavigationService.Shutdown(); }
        }

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

                var error = Assert.Throws<InvalidOperationException>(() => second.Start());
                Assert.Contains("NavigationService.Shutdown()", error.Message);
                Assert.Contains("PageNavBootstrap.Start()", error.Message);
                Assert.DoesNotContain("UseContext", error.Message);

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

        // NAV-001: the tick validated the interaction generation again *after*
        // Session.SignOut(). Session observers run synchronously, so an application
        // that refreshes its UI on sign-out could make the platform observer report
        // an interaction from inside SignOut and cancel the transition the tick had
        // already committed to — signing the terminal out but leaving it on the
        // operator's page.
        [Fact]
        public async Task IdleTick_InteractionCausedBySignOut_StillReachesTheIdlePage()
        {
            await NavigationService.Shutdown();
            var fixture = RuntimeTestFixture.Build<StubIdle>();
            NavigationService.UseContext(fixture.Context);

            var timer = new TrackingTimer();
            var observer = new TrackingObserver();
            var navigations = 0;
            var lifetime = timer.CreateLifetime(observer,
                () => { navigations++; return Task.CompletedTask; });

            try
            {
                lifetime.ConfigureIdle(1_000, fixture.Context);
                fixture.Context.Session.SignIn("operator");

                // The application reacts to sign-out by touching its UI, which the
                // platform interaction observer reports synchronously and reentrantly.
                fixture.Context.Session.Changed += () => observer.RaiseInteraction();

                timer.RaiseTick();

                Assert.Equal(1, navigations);
                Assert.False(fixture.Context.Session.IsAuthenticated);

                // NAV-011 composition: the interaction rearmed the timer itself, so
                // the watchdog is still running afterwards.
                Assert.True(timer.IsStarted);
            }
            finally
            {
                lifetime.Dispose();
                await NavigationService.Shutdown();
            }
        }

        [Fact]
        public async Task IdleTick_GenuineInteractionBeforeAdmission_CancelsWithoutSigningOut()
        {
            await NavigationService.Shutdown();
            var fixture = RuntimeTestFixture.Build<StubIdle>();
            NavigationService.UseContext(fixture.Context);

            var timer = new TrackingTimer();
            var observer = new TrackingObserver();
            var navigations = 0;
            var lifetime = timer.CreateLifetime(observer,
                () => { navigations++; return Task.CompletedTask; });

            try
            {
                lifetime.ConfigureIdle(1_000, fixture.Context);
                fixture.Context.Session.SignIn("operator");

                // IdleElapsed is emitted after the tick starts and before the
                // transition is admitted, so this lands in exactly the window a real
                // user interaction would.
                fixture.Context.Events.NavigationTrace += e =>
                {
                    if (e.Kind == NavigationTraceKind.IdleElapsed)
                        observer.RaiseInteraction();
                };

                timer.RaiseTick();

                Assert.Equal(0, navigations);
                Assert.True(fixture.Context.Session.IsAuthenticated);
            }
            finally
            {
                lifetime.Dispose();
                await NavigationService.Shutdown();
            }
        }

        [Fact]
        public async Task IdleTick_StopIdleDuringSignOut_CancelsAdmittedTransition()
        {
            await NavigationService.Shutdown();
            var fixture = RuntimeTestFixture.Build<StubIdle>();
            NavigationService.UseContext(fixture.Context);

            var timer = new TrackingTimer();
            var observer = new TrackingObserver();
            var navigations = 0;
            NavigationBootstrapLifetime lifetime = null;
            lifetime = timer.CreateLifetime(observer,
                () => { navigations++; return Task.CompletedTask; });

            try
            {
                lifetime.ConfigureIdle(1_000, fixture.Context);

                // Admission must still yield to teardown, which the relaxed
                // post-sign-out check keeps revalidating.
                fixture.Context.Session.Changed += () => lifetime.StopIdle();

                timer.RaiseTick();

                Assert.Equal(0, navigations);
            }
            finally
            {
                lifetime.Dispose();
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
            var lifetime = timer.CreateLifetime(observer,
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
            var lifetime = timer.CreateLifetime(observer,
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
            var lifetime = timer.CreateLifetime(observer);

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
            var lifetime = timer.CreateLifetime(observer,
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
            var lifetime = timer.CreateLifetime(observer,
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
            var lifetime = timer.CreateLifetime(observer,
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
            var lifetime = timer.CreateLifetime(observer,
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
            var lifetime = timer.CreateLifetime(observer,
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
            var lifetime = timer.CreateLifetime(observer);

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

        [Theory]
        [InlineData("sidebar")]
        [InlineData("observed host")]
        [InlineData("programmatic")]
        public async Task IdleDeadline_OnlyUserInputRenewsItAndInvalidatesStaleTicks(string source)
        {
            await NavigationService.Shutdown();
            var fixture = RuntimeTestFixture.Build<StubIdle>(typeof(StubA));
            var timer = new TrackingTimer();
            var observer = new TrackingObserver();
            var lifetime = timer.CreateLifetime(observer);
            NavigationService.UseContext(fixture.Context, null, lifetime);

            try
            {
                await NavigationService.GoIdleAsync();
                lifetime.ConfigureIdle(1_000, fixture.Context);
                fixture.Context.Session.SignIn("first-operator");
                var idleReached = ObserveIdlePresentation();

                timer.Advance(900);
                if (source == "sidebar")
                    NavigationService.NotifyUserInteraction();
                else if (source == "observed host")
                    observer.RaiseInteraction();

                await NavigationService.SwitchPage<StubA>();
                timer.Advance(100);
                timer.DeliverTick();

                if (source != "programmatic")
                {
                    Assert.IsType<StubA>(NavigationService.Current);
                    Assert.True(fixture.Context.Session.IsAuthenticated);
                    Assert.Equal(900, timer.IntervalMilliseconds);
                    timer.Advance(899);
                    timer.DeliverTick();
                    Assert.IsType<StubA>(NavigationService.Current);
                    Assert.True(fixture.Context.Session.IsAuthenticated);
                    timer.Advance(1);
                    timer.DeliverTick();
                }

                await AssertCompletes(idleReached);
                Assert.IsType<StubIdle>(NavigationService.Current);
                Assert.False(fixture.Context.Session.IsAuthenticated);
            }
            finally
            {
                await NavigationService.Shutdown();
            }
        }

        [Fact]
        public async Task IdleDeadline_UserInputWhileTimeoutWaitsForNavigationGateCancelsSignOut()
        {
            await NavigationService.Shutdown();
            var fixture = RuntimeTestFixture.Build<StubIdle>(typeof(StubBlockingShutdownPage), typeof(StubA));
            var timer = new TrackingTimer();
            var lifetime = timer.CreateLifetime(new TrackingObserver());
            NavigationService.UseContext(fixture.Context, null, lifetime);
            StubBlockingShutdownPage blockingPage = null;

            try
            {
                await NavigationService.SwitchPage<StubBlockingShutdownPage>();
                blockingPage = Assert.IsType<StubBlockingShutdownPage>(NavigationService.Current);
                lifetime.ConfigureIdle(1_000, fixture.Context);
                fixture.Context.Session.SignIn("operator");
                var idleDiscarded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                fixture.Context.Events.NavigationTrace += e =>
                {
                    if (e.Trigger == NavigationTraceTrigger.Idle && e.Outcome == NavigationTraceOutcome.Discarded)
                        idleDiscarded.TrySetResult(true);
                };

                var pendingNavigation = NavigationService.SwitchPage<StubA>();
                await AssertCompletes(blockingPage.LeaveStarted);
                timer.RaiseTick();
                Assert.True(fixture.Context.Session.IsAuthenticated);
                NavigationService.NotifyUserInteraction();
                blockingPage.ReleaseLeave();

                await AssertCompletes(pendingNavigation);
                await AssertCompletes(idleDiscarded.Task);
                Assert.IsType<StubA>(NavigationService.Current);
                Assert.True(fixture.Context.Session.IsAuthenticated);
                Assert.True(timer.IsStarted);
            }
            finally
            {
                blockingPage?.ReleaseLeave();
                await NavigationService.Shutdown();
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task AutomaticIdle_DetachesAndCancelsPreviousSessionPopoverBeforePresentingIdle(bool delaySetup)
        {
            await NavigationService.Shutdown();
            var finishSetup = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var fixture = RuntimeTestFixture.BuildWithServices<StubIdle>((services, host, factory) =>
            {
                factory.Register(typeof(StubPopoverView), () => delaySetup
                    ? new DelayedPopoverView(finishSetup.Task)
                    : new StubPopoverView());
                services.Register<IPopoverService>(new PopoverService(host, factory));
            }, typeof(StubA), typeof(StubB));
            var timer = new TrackingTimer();
            var lifetime = timer.CreateLifetime(new TrackingObserver());
            NavigationService.UseContext(fixture.Context, null, lifetime);

            try
            {
                await NavigationService.SwitchPage<StubA>();
                lifetime.ConfigureIdle(1_000, fixture.Context);
                fixture.Context.Session.SignIn("first-operator");
                var result = NavigationService.ShowPopoverAsync<StubPopoverView>("first session content");
                var previousPopover = Assert.IsAssignableFrom<StubPopoverView>(Assert.Single(fixture.Host.AddedViews));
                Assert.Equal("first session content", previousPopover.LastShownPayload);

                await NavigationService.SwitchPage<StubB>();
                Assert.Empty(fixture.Host.RemovedViews);
                Assert.False(result.IsCompleted);

                var idleReached = ObserveIdlePresentation();
                bool cleanedAtIdleAttach = false;
                fixture.Host.OperationObserved = (operation, page) =>
                {
                    if (operation == "attach" && page is StubIdle)
                        cleanedAtIdleAttach = !fixture.Context.Session.IsAuthenticated &&
                            fixture.Host.RemovedViews.Contains(previousPopover) && previousPopover.IsDisposed;
                };

                timer.RaiseTick();
                await AssertCompletes(idleReached);
                Assert.True(cleanedAtIdleAttach);
                await AssertCompletes(result);
                Assert.False(await result);
                previousPopover.CompletionCallback(true);
                Assert.False(await result);
                finishSetup.TrySetResult(true);

                fixture.Context.Session.SignIn("second-operator");
                await NavigationService.SwitchPage<StubA>();
                Assert.Same(previousPopover, Assert.Single(fixture.Host.AddedViews));
                Assert.Same(previousPopover, Assert.Single(fixture.Host.RemovedViews));

                var resetResult = NavigationService.ShowPopoverAsync<StubPopoverView>("second session content");
                var secondPopover = Assert.IsAssignableFrom<StubPopoverView>(fixture.Host.AddedViews.Last());
                await NavigationService.ResetAsync();
                await AssertCompletes(resetResult);
                Assert.False(await resetResult);
                Assert.Equal(new object[] { previousPopover, secondPopover }, fixture.Host.RemovedViews);
            }
            finally
            {
                finishSetup.TrySetResult(true);
                await NavigationService.Shutdown();
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task AutomaticSignOut_CancelsPromptBeforeIdleIsPresented(bool delaySetup)
        {
            await NavigationService.Shutdown();
            var finishSetup = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var blocker = new DialogServiceTests.CountingInteractionBlocker();
            var prompt = new SessionPromptView(delaySetup ? finishSetup.Task : Task.CompletedTask);
            var fixture = BuildPromptFixture(prompt, blocker);
            var timer = new TrackingTimer();
            var lifetime = timer.CreateLifetime(new TrackingObserver());
            NavigationService.UseContext(fixture.Context, null, lifetime);

            try
            {
                var promptClosures = new List<NavigationTraceEvent>();
                fixture.Context.Events.NavigationTrace += e =>
                {
                    if (e.Kind == NavigationTraceKind.SurfaceClosed && e.SurfaceKind == NavigationTraceSurfaceKinds.Prompt)
                        promptClosures.Add(e);
                };
                await NavigationService.SwitchPage<StubA>();
                lifetime.ConfigureIdle(1_000, fixture.Context);
                fixture.Context.Session.SignIn("first-operator");
                var result = NavigationService.ShowPromptAsync<SessionPromptView, string>();
                Assert.True(prompt.TryType("synthetic session text"));
                Assert.Equal(1, blocker.Depth);
                Assert.False(result.IsCompleted);

                var stateAtIdle = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                NavigationService.CurrentChanged += page =>
                {
                    if (page is StubIdle)
                        stateAtIdle.TrySetResult(!fixture.Context.Session.IsAuthenticated &&
                            fixture.Host.RemovedViews.Contains(prompt) && prompt.IsDisposed &&
                            !prompt.TryType("late input") && blocker.Depth == 0 &&
                            result.Status == TaskStatus.RanToCompletion);
                };

                timer.RaiseTick();
                await AssertCompletes(stateAtIdle.Task);
                Assert.True(await stateAtIdle.Task);
                Assert.Equal("synthetic session text", prompt.Text);
                Assert.Null(await result);

                prompt.CompletionCallback(prompt.Text);
                Assert.Null(await result);
                Assert.Same(prompt, Assert.Single(fixture.Host.RemovedViews));
                Assert.Equal(1, prompt.DisposeCount);
                Assert.Equal(1, blocker.UnblockCalls);
                Assert.Equal(NavigationTraceCloseReasons.SessionEnded, Assert.Single(promptClosures).CloseReason);
            }
            finally
            {
                finishSetup.TrySetResult(true);
                await NavigationService.Shutdown();
            }
        }

        [Theory]
        [InlineData("idle")]
        [InlineData("switch")]
        [InlineData("back")]
        public async Task NavigateToIdle_CancelsPromptBeforePresentationAndPreservesSession(string entryPoint)
        {
            await NavigationService.Shutdown();
            var blocker = new DialogServiceTests.CountingInteractionBlocker();
            var prompt = new SessionPromptView(Task.CompletedTask);
            var fixture = BuildPromptFixture(prompt, blocker);
            NavigationService.UseContext(fixture.Context);

            try
            {
                await NavigationService.GoIdleAsync();
                await NavigationService.SwitchPage<StubA>();
                fixture.Context.Session.SignIn(new[] { "operator" }, new[] { "sale.confirm" });
                var result = NavigationService.ShowPromptAsync<SessionPromptView, string>();
                Assert.True(prompt.TryType("synthetic session text"));

                var readyAtPresentation = false;
                NavigationService.CurrentChanged += page =>
                {
                    if (page is StubIdle)
                        readyAtPresentation = fixture.Context.Session.IsAuthenticated &&
                            fixture.Host.RemovedViews.Contains(prompt) && prompt.IsDisposed &&
                            !prompt.TryType("late input") && blocker.Depth == 0 &&
                            result.Status == TaskStatus.RanToCompletion;
                };

                switch (entryPoint)
                {
                    case "switch":
                        await NavigationService.SwitchPage<StubIdle>();
                        break;
                    case "back":
                        Assert.True(await NavigationService.GoBackAsync());
                        break;
                    default:
                        await NavigationService.GoIdleAsync();
                        break;
                }

                Assert.IsType<StubIdle>(NavigationService.Current);
                Assert.True(readyAtPresentation);
                Assert.Equal(new[] { "operator" }, fixture.Context.Session.Roles);
                Assert.Equal(new[] { "sale.confirm" }, fixture.Context.Session.Permissions);
                Assert.Null(await result);

                prompt.CompletionCallback(prompt.Text);
                Assert.Null(await result);
                Assert.Same(prompt, Assert.Single(fixture.Host.RemovedViews));
                Assert.Equal(1, prompt.DisposeCount);
                Assert.Equal(1, blocker.UnblockCalls);
            }
            finally
            {
                await NavigationService.Shutdown();
            }
        }

        [Fact]
        public async Task ExplicitSignOut_CancelsPromptAndKeepsTheCurrentPage()
        {
            await NavigationService.Shutdown();
            var blocker = new DialogServiceTests.CountingInteractionBlocker();
            var prompt = new SessionPromptView(Task.CompletedTask);
            var fixture = BuildPromptFixture(prompt, blocker);
            NavigationService.UseContext(fixture.Context);

            try
            {
                await NavigationService.SwitchPage<StubA>();
                fixture.Context.Session.SignIn("operator");
                var result = NavigationService.ShowPromptAsync<SessionPromptView, string>();
                Assert.True(prompt.TryType("synthetic session text"));

                var dashboard = Assert.IsType<StubA>(NavigationService.Current);
                Assert.True(fixture.Context.Session.IsAuthenticated);
                Assert.Empty(fixture.Host.RemovedViews);
                Assert.False(result.IsCompleted);

                NavigationService.Session.SignOut();
                Assert.False(fixture.Context.Session.IsAuthenticated);
                Assert.Same(dashboard, NavigationService.Current);
                Assert.Same(prompt, Assert.Single(fixture.Host.RemovedViews));
                Assert.True(prompt.IsDisposed);
                Assert.Equal(0, blocker.Depth);
                await AssertCompletes(result);
                Assert.Null(await result);
            }
            finally
            {
                await NavigationService.Shutdown();
            }
        }

        [Theory]
        [InlineData("same session")]
        [InlineData("sign out")]
        [InlineData("replace session")]
        [InlineData("replace session before idle")]
        [InlineData("reset")]
        [InlineData("signed out")]
        public async Task IdlePromptDraft_RefillsTheNextPromptOnlyOnItsOriginPageAndSession(string sessionChange)
        {
            await NavigationService.Shutdown();
            var prompts = new List<DraftPromptView>();
            var fixture = RuntimeTestFixture.BuildWithServices<StubIdle>((services, host, factory) =>
            {
                factory.Register(typeof(DraftPromptView), () =>
                {
                    var prompt = new DraftPromptView();
                    prompts.Add(prompt);
                    return prompt;
                });
                services.Register<IPromptService>(new PromptService(host, factory));
            }, typeof(StubA), typeof(StubB));
            NavigationService.UseContext(fixture.Context);

            try
            {
                const string draft = "unfinished text";
                await NavigationService.SwitchPage<StubA>();
                if (sessionChange != "signed out")
                    fixture.Context.Session.SignIn("first-operator");

                var original = NavigationService.ShowPromptAsync<DraftPromptView, string>();
                var sourcePrompt = Assert.Single(prompts);
                Assert.True(sourcePrompt.TryType(draft));
                if (sessionChange == "replace session before idle")
                    fixture.Context.Session.SignIn("second-operator");

                await NavigationService.GoIdleAsync();
                Assert.True(sourcePrompt.IsDisposed);
                Assert.Null(await original);
                Assert.Same(sourcePrompt, Assert.Single(fixture.Host.AddedViews));

                switch (sessionChange)
                {
                    case "sign out":
                        fixture.Context.Session.SignOut();
                        fixture.Context.Session.SignIn("second-operator");
                        break;
                    case "replace session":
                        fixture.Context.Session.SignIn("second-operator");
                        break;
                    case "reset":
                        await NavigationService.ResetAsync();
                        break;
                    case "same session":
                        await NavigationService.SwitchPage<StubB>();
                        var unrelated = NavigationService.ShowPromptAsync<DraftPromptView, string>();
                        Assert.Null(prompts.Last().Text);
                        prompts.Last().CompletionCallback(null);
                        Assert.Null(await unrelated);
                        break;
                }

                await NavigationService.SwitchPage<StubA>();
                var reopened = NavigationService.ShowPromptAsync<DraftPromptView, string>();
                var destination = prompts.Last();
                var expected = sessionChange == "same session" ? draft : null;
                Assert.Equal(expected, destination.Text);
                Assert.False(reopened.IsCompleted);
                destination.CompletionCallback(destination.Text);
                Assert.Equal(expected, await reopened);

                var subsequent = NavigationService.ShowPromptAsync<DraftPromptView, string>();
                Assert.Null(prompts.Last().Text);
                prompts.Last().CompletionCallback(null);
                Assert.Null(await subsequent);
            }
            finally
            {
                await NavigationService.Shutdown();
            }
        }

        private static RuntimeTestFixture BuildPromptFixture(SessionPromptView prompt, IInteractionBlocker blocker)
            => RuntimeTestFixture.BuildWithServices<StubIdle>((services, host, factory) =>
            {
                factory.Register(typeof(SessionPromptView), () => prompt);
                services.Register<IPromptService>(new PromptService(host, factory, blocker));
            }, typeof(StubA));

        private class SessionPromptView : StubPromptView
        {
            private readonly Task _finishSetup;

            public SessionPromptView(Task finishSetup) => _finishSetup = finishSetup;

            public string Text { get; private set; }
            public int DisposeCount { get; private set; }

            public bool TryType(string value)
            {
                if (IsDisposed)
                    return false;

                Text = value;
                return true;
            }

            public override Task OnShownAsync(object payload) => _finishSetup;

            public override void Dispose()
            {
                DisposeCount++;
                base.Dispose();
            }
        }

        private sealed class DraftPromptView : SessionPromptView, IPromptDraftState
        {
            public DraftPromptView() : base(Task.CompletedTask) { }

            public object CaptureDraft() => Text;

            public void RestoreDraft(object draft) => TryType((string)draft);
        }

        private sealed class DelayedPopoverView : StubPopoverView
        {
            private readonly Task _finishSetup;

            public DelayedPopoverView(Task finishSetup) => _finishSetup = finishSetup;

            public override async Task OnShownAsync(object payload)
            {
                await base.OnShownAsync(payload);
                await _finishSetup;
            }
        }

        private static Task ObserveIdlePresentation()
        {
            var reached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            NavigationService.CurrentChanged += page =>
            {
                if (page is StubIdle)
                    reached.TrySetResult(true);
            };
            return reached.Task;
        }

        private static async Task AssertCompletes(Task task)
        {
            Assert.Same(task, await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5))));
            await task;
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
            private long _timestamp;

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
            public NavigationBootstrapLifetime CreateLifetime(
                TrackingObserver observer,
                Func<Task> navigateIdle = null)
                => new NavigationBootstrapLifetime(observer, this, navigateIdle,
                    getTimestamp: () => _timestamp);

            public void Advance(int milliseconds)
                => _timestamp += milliseconds * Stopwatch.Frequency / 1_000;

            public void DeliverTick() => _tick?.Invoke();

            public void RaiseTick()
            {
                Advance(IntervalMilliseconds);
                DeliverTick();
            }

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
