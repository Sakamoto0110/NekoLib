using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Navigation.Bootstrap;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Runtime.Registry;
using NekoLib.Navigation.Runtime.Session;
using NekoLib.Navigation.Tests.Unit.Fakes;
using Xunit;
using static NekoLib.Navigation.Tests.Unit.PageNavBootstrapLifetimeTests;

namespace NekoLib.Navigation.Tests.Unit
{
    [Collection("NavigationServiceFacade")]
    public sealed class SessionTimeoutLifetimeTests
    {
        private static readonly TimeSpan MaximumLifetime = TimeSpan.FromSeconds(1);

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void SessionTimeout_RejectsNonPositiveDurations(int milliseconds)
        {
            var bootstrap = PageNavBootstrap.Use<FakePlatformAdapter>(new object());
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                bootstrap.UseSessionTimeout(TimeSpan.FromMilliseconds(milliseconds)));

            Assert.Equal("maximumLifetime", exception.ParamName);
        }

        [Fact]
        public void SessionTimeout_RequiresAResolvedIdlePage()
        {
            var bootstrap = PageNavBootstrap.Use<FakePlatformAdapter>(new object())
                .UseRegistry(PageRegistry.Create(pages => pages.RegisterType(typeof(StubA))))
                .UseSessionTimeout(MaximumLifetime);

            var exception = Assert.Throws<InvalidOperationException>(() => bootstrap.Start());
            Assert.Contains("UseSessionTimeout requires an idle page", exception.Message);
        }

        [Fact]
        public async Task UnconfiguredBootstrap_LeavesAuthenticationIndependentOfTimerTicks()
        {
            await NavigationService.Shutdown();
            var context = PageNavBootstrap.Use<TrackingPlatformAdapter>(new object())
                .UseRegistry(PageRegistry.Create(pages =>
                {
                    pages.RegisterType(typeof(StubA));
                    pages.RegisterType(typeof(StubIdle));
                }))
                .Start();

            try
            {
                await NavigationService.SwitchPage<StubA>();
                context.Session.SignIn("operator");
                var timer = TrackingPlatformAdapter.LastCreated.Timer;
                timer.Advance(int.MaxValue);
                timer.DeliverTick();

                Assert.Equal(0, timer.StartCalls);
                Assert.IsType<StubA>(NavigationService.Current);
                Assert.True(context.Session.IsAuthenticated);
            }
            finally
            {
                await NavigationService.Shutdown();
            }
        }

        [Fact]
        public async Task AbsoluteDeadline_SurvivesContinuousInputAndDirectIdleNavigation()
        {
            await NavigationService.Shutdown();
            var fixture = RuntimeTestFixture.Build<StubIdle>(typeof(StubA));
            var timer = new TrackingTimer();
            var observer = new TrackingObserver();
            var lifetime = timer.CreateLifetime(observer);
            var time = new ControlledTime(fixture.Context.Session);
            NavigationService.UseContext(fixture.Context, null, lifetime);
            lifetime.ConfigureIdle(400, fixture.Context);
            lifetime.ConfigureSessionTimeout(MaximumLifetime, fixture.Context, time.GetTimestamp, time.Delay);

            try
            {
                await NavigationService.SwitchPage<StubA>();
                fixture.Context.Session.SignIn("operator");
                var initialDelay = await time.TakeDelayAsync();
                Assert.Equal(1_000, initialDelay.Milliseconds);

                timer.Advance(300);
                time.SetElapsed(300);
                observer.RaiseInteraction();
                await NavigationService.GoIdleAsync();
                Assert.True(fixture.Context.Session.IsAuthenticated);
                await NavigationService.SwitchPage<StubA>();

                timer.Advance(300);
                time.SetElapsed(600);
                observer.RaiseInteraction();
                timer.Advance(300);
                time.SetElapsed(900);
                observer.RaiseInteraction();

                time.SetElapsed(999);
                initialDelay.Complete();
                var remainingDelay = await time.TakeDelayAsync();
                Assert.Equal(1, remainingDelay.Milliseconds);
                Assert.True(fixture.Context.Session.IsAuthenticated);

                var expired = ObserveSignedOutIdle(fixture.Context.Session);
                time.SetElapsed(1_000);
                remainingDelay.Complete();
                await RequireCompletion(expired);
                Assert.False(fixture.Context.Session.IsAuthenticated);
            }
            finally
            {
                await NavigationService.Shutdown();
                time.CompletePendingDelays();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CompetingDeadlines_SignOutAndNavigateOnlyOnce(bool inactivityFirst)
        {
            await NavigationService.Shutdown();
            var fixture = RuntimeTestFixture.Build<StubIdle>(typeof(StubBlockingShutdownPage), typeof(StubA));
            var timer = new TrackingTimer();
            var lifetime = timer.CreateLifetime(new TrackingObserver());
            var time = new ControlledTime(fixture.Context.Session);
            NavigationService.UseContext(fixture.Context, null, lifetime);
            lifetime.ConfigureIdle(1_000, fixture.Context);
            lifetime.ConfigureSessionTimeout(MaximumLifetime, fixture.Context, time.GetTimestamp, time.Delay);
            StubBlockingShutdownPage blockingPage = null;

            try
            {
                await NavigationService.SwitchPage<StubBlockingShutdownPage>();
                blockingPage = Assert.IsType<StubBlockingShutdownPage>(NavigationService.Current);
                fixture.Context.Session.SignIn("operator");
                var signOuts = 0;
                fixture.Context.Session.SignedOut += () => Interlocked.Increment(ref signOuts);
                var discarded = ObserveIdleTrace(fixture.Context.Events,
                    e => e.Outcome == NavigationTraceOutcome.Discarded);
                var queued = new SemaphoreSlim(0);
                fixture.Context.Events.NavigationTrace += e =>
                {
                    if (e.Trigger == NavigationTraceTrigger.Idle && e.Stage == NavigationTraceStage.GateWait)
                        queued.Release();
                };

                var dashboard = NavigationService.SwitchPage<StubA>();
                await RequireCompletion(blockingPage.LeaveStarted);
                var sessionDelay = await time.TakeDelayAsync();
                time.SetElapsed(1_000);

                if (inactivityFirst)
                    timer.RaiseTick();
                else
                    sessionDelay.Complete();
                Assert.True(await queued.WaitAsync(TimeSpan.FromSeconds(5)));

                if (inactivityFirst)
                    sessionDelay.Complete();
                else
                    timer.RaiseTick();
                Assert.True(await queued.WaitAsync(TimeSpan.FromSeconds(5)));

                var expired = ObserveSignedOutIdle(fixture.Context.Session);
                blockingPage.ReleaseLeave();
                await RequireCompletion(dashboard);
                await RequireCompletion(expired);
                await RequireCompletion(discarded);

                Assert.Equal(1, signOuts);
                Assert.Single(fixture.Host.Attached, page => page is StubIdle);
                Assert.True(sessionDelay.Cancellation.IsCancellationRequested);
            }
            finally
            {
                blockingPage?.ReleaseLeave();
                await NavigationService.Shutdown();
                time.CompletePendingDelays();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task QueuedAbsoluteExpiration_CannotSignOutAReplacementSession(bool signOutFirst)
        {
            await NavigationService.Shutdown();
            var fixture = RuntimeTestFixture.Build<StubIdle>(typeof(StubBlockingShutdownPage), typeof(StubA));
            var time = new ControlledTime(fixture.Context.Session);
            var lifetime = new TrackingTimer().CreateLifetime(new TrackingObserver());
            NavigationService.UseContext(fixture.Context, null, lifetime);
            lifetime.ConfigureSessionTimeout(MaximumLifetime, fixture.Context, time.GetTimestamp, time.Delay);
            StubBlockingShutdownPage blockingPage = null;

            try
            {
                await NavigationService.SwitchPage<StubBlockingShutdownPage>();
                blockingPage = Assert.IsType<StubBlockingShutdownPage>(NavigationService.Current);
                fixture.Context.Session.SignIn("first-operator");
                var oldDelay = await time.TakeDelayAsync();
                var queued = ObserveIdleTrace(fixture.Context.Events,
                    e => e.Stage == NavigationTraceStage.GateWait);
                var discarded = ObserveIdleTrace(fixture.Context.Events,
                    e => e.Outcome == NavigationTraceOutcome.Discarded);

                var dashboard = NavigationService.SwitchPage<StubA>();
                await RequireCompletion(blockingPage.LeaveStarted);
                time.SetElapsed(1_000);
                oldDelay.Complete();
                await RequireCompletion(queued);

                if (signOutFirst)
                    fixture.Context.Session.SignOut();
                time.SetElapsed(0);
                fixture.Context.Session.SignIn(new[] { "replacement" }, new[] { "sales.read" });
                var newDelay = await time.TakeDelayAsync();
                Assert.Equal(1_000, newDelay.Milliseconds);
                blockingPage.ReleaseLeave();
                await RequireCompletion(dashboard);
                await RequireCompletion(discarded);

                Assert.IsType<StubA>(NavigationService.Current);
                Assert.True(fixture.Context.Session.IsAuthenticated);
                Assert.Equal(new[] { "replacement" }, fixture.Context.Session.Roles);
                Assert.Equal(new[] { "sales.read" }, fixture.Context.Session.Permissions);

                var expired = ObserveSignedOutIdle(fixture.Context.Session);
                time.SetElapsed(1_000);
                newDelay.Complete();
                await RequireCompletion(expired);
            }
            finally
            {
                blockingPage?.ReleaseLeave();
                await NavigationService.Shutdown();
                time.CompletePendingDelays();
            }
        }

        [Fact]
        public async Task Shutdown_CancelsThePreviousMountsDeadline()
        {
            await NavigationService.Shutdown();
            var fixture = RuntimeTestFixture.Build<StubIdle>();
            var time = new ControlledTime(fixture.Context.Session);
            var lifetime = new TrackingTimer().CreateLifetime(new TrackingObserver());
            NavigationService.UseContext(fixture.Context, null, lifetime);
            lifetime.ConfigureSessionTimeout(MaximumLifetime, fixture.Context, time.GetTimestamp, time.Delay);

            try
            {
                fixture.Context.Session.SignIn("first-operator");
                var deadline = await time.TakeDelayAsync();
                await NavigationService.Shutdown();
                Assert.True(deadline.Cancellation.IsCancellationRequested);

                var nextMount = RuntimeTestFixture.Build<StubIdle>(typeof(StubA));
                NavigationService.UseContext(nextMount.Context);
                await NavigationService.SwitchPage<StubA>();
                nextMount.Context.Session.SignIn("new-operator");
                time.SetElapsed(1_000);
                deadline.Complete();

                Assert.True(fixture.Context.Session.IsAuthenticated);
                Assert.True(nextMount.Context.Session.IsAuthenticated);
                Assert.IsType<StubA>(NavigationService.Current);
            }
            finally
            {
                await NavigationService.Shutdown();
                time.CompletePendingDelays();
            }
        }

        private static Task ObserveSignedOutIdle(NavigationSession session)
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            NavigationService.CurrentChanged += page =>
            {
                if (page is StubIdle && !session.IsAuthenticated)
                    completion.TrySetResult(true);
            };
            return completion.Task;
        }

        private static Task ObserveIdleTrace(
            NavigationEventHub events,
            Func<NavigationTraceEvent, bool> predicate)
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            events.NavigationTrace += e =>
            {
                if (e.Trigger == NavigationTraceTrigger.Idle && predicate(e))
                    completion.TrySetResult(true);
            };
            return completion.Task;
        }

        private static async Task RequireCompletion(Task task)
        {
            Assert.Same(task, await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5))));
            await task;
        }

        private sealed class ControlledTime
        {
            private readonly NavigationSession _session;
            private readonly ConcurrentQueue<ScheduledDelay> _queue = new ConcurrentQueue<ScheduledDelay>();
            private readonly ConcurrentBag<ScheduledDelay> _delays = new ConcurrentBag<ScheduledDelay>();
            private readonly SemaphoreSlim _ready = new SemaphoreSlim(0);
            private long _elapsedTicks;

            public ControlledTime(NavigationSession session) => _session = session;

            public long GetTimestamp() => (_session.GetAuthenticatedLifetime()?.SignedInTimestamp ?? 0) +
                Interlocked.Read(ref _elapsedTicks);

            public void SetElapsed(int milliseconds) =>
                Interlocked.Exchange(ref _elapsedTicks, milliseconds * Stopwatch.Frequency / 1000);

            public Task Delay(int milliseconds, CancellationToken cancellation)
            {
                var delay = new ScheduledDelay(milliseconds, cancellation);
                _delays.Add(delay);
                _queue.Enqueue(delay);
                _ready.Release();
                return delay.Pending;
            }

            public async Task<ScheduledDelay> TakeDelayAsync()
            {
                Assert.True(await _ready.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.True(_queue.TryDequeue(out var delay));
                return delay;
            }

            public void CompletePendingDelays()
            {
                foreach (var delay in _delays)
                    delay.Complete();
            }
        }

        private sealed class ScheduledDelay
        {
            private readonly TaskCompletionSource<bool> _completion =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public ScheduledDelay(int milliseconds, CancellationToken cancellation)
            {
                Milliseconds = milliseconds;
                Cancellation = cancellation;
            }

            public int Milliseconds { get; }
            public CancellationToken Cancellation { get; }
            public Task Pending => _completion.Task;
            public void Complete() => _completion.TrySetResult(true);
        }
    }
}
