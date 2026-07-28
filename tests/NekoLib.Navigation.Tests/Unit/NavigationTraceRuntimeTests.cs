using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Runtime;
using NekoLib.Navigation.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    public sealed class NavigationTraceRuntimeTests
    {
        [Fact]
        public async Task NavigateAsync_BeforeUiDispatch_EmitsRequestStartedOnlyOnce()
        {
            var dispatcher = new DeferredEventDispatcherAdapter();
            var fixture = RuntimeTestFixture.BuildWithDispatcher<StubIdle>(
                dispatcher,
                typeof(StubA));
            var traces = new ConcurrentQueue<NavigationTraceEvent>();
            fixture.Context.Events.NavigationTrace += traces.Enqueue;

            var navigation = fixture.Runtime.NavigateAsync(
                typeof(StubA),
                NavigationArgs.Default());

            var beforeDispatch = traces.ToArray();
            Assert.Equal(
                NavigationTraceKind.RequestStarted,
                beforeDispatch[0].Kind);
            Assert.DoesNotContain(
                beforeDispatch,
                e => e.Kind == NavigationTraceKind.AttemptStarted);
            Assert.Equal(1, dispatcher.PendingCount);

            dispatcher.RunAll();
            await navigation;

            var completed = traces.ToArray();
            var requestStart = Assert.Single(
                completed.Where(e => e.Kind == NavigationTraceKind.RequestStarted));
            var requestEnd = Assert.Single(
                completed.Where(e => e.Kind == NavigationTraceKind.RequestCompleted));
            var attemptStart = Assert.Single(
                completed.Where(e => e.Kind == NavigationTraceKind.AttemptStarted));
            var attemptEnd = Assert.Single(
                completed.Where(e => e.Kind == NavigationTraceKind.AttemptCompleted));

            Assert.False(string.IsNullOrEmpty(requestStart.RuntimeId));
            Assert.False(string.IsNullOrEmpty(requestStart.RequestId));
            Assert.Equal(requestStart.RuntimeId, requestEnd.RuntimeId);
            Assert.Equal(requestStart.RequestId, requestEnd.RequestId);
            Assert.Equal(requestStart.RequestId, attemptStart.RequestId);
            Assert.Equal(attemptStart.AttemptId, attemptEnd.AttemptId);
            Assert.Null(attemptStart.ParentAttemptId);
            Assert.Equal(NavigationTraceOutcome.Succeeded, requestEnd.Outcome);
            Assert.Equal(NavigationTraceOutcome.Succeeded, attemptEnd.Outcome);
            Assert.True(requestEnd.ElapsedMilliseconds >= 0);
            Assert.True(attemptEnd.StageElapsedMilliseconds >= 0);
        }

        [Fact]
        public async Task NavigateAsync_OrdersRequestStartThenNavigatingThenGuardEvaluation()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubAuthenticated));
            var sequence = new List<string>();

            fixture.Context.Events.NavigationTrace += trace =>
            {
                if (trace.Kind == NavigationTraceKind.RequestStarted)
                    sequence.Add("started");
                if (trace.Kind == NavigationTraceKind.AttemptStage &&
                    trace.Stage == NavigationTraceStage.GuardEvaluation)
                {
                    sequence.Add("guard");
                }
            };
            fixture.Runtime.Navigating += (_, __, ___) =>
                sequence.Add("navigating");

            await fixture.Runtime.NavigateAsync(
                typeof(StubAuthenticated),
                NavigationArgs.Default());

            Assert.Equal(
                new[] { "started", "navigating", "guard" },
                sequence);
        }

        [Fact]
        public async Task NavigateAsync_DescriptorLoadMode_IsEffectiveEverywhere()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubConditionalLoadBefore));
            var traces = new ConcurrentQueue<NavigationTraceEvent>();
            var logs = new List<PageLogEntry>();
            NavigationArgs navigatingArgs = null;
            NavigationArgs navigatedArgs = null;

            fixture.Context.Events.NavigationTrace += traces.Enqueue;
            fixture.Context.Events.NavigationLogged += logs.Add;
            fixture.Runtime.Navigating += (_, __, args) => navigatingArgs = args;
            fixture.Runtime.Navigated += (_, __, args) => navigatedArgs = args;

            await fixture.Runtime.NavigateAsync(
                typeof(StubConditionalLoadBefore),
                NavigationArgs.Background("payload"));

            var page = Assert.IsType<StubConditionalLoadBefore>(
                fixture.Runtime.Current);
            var log = Assert.Single(logs);
            var requestStart = Assert.Single(
                traces.Where(e => e.Kind == NavigationTraceKind.RequestStarted));
            var attemptEnd = Assert.Single(
                traces.Where(e => e.Kind == NavigationTraceKind.AttemptCompleted));
            var requestEnd = Assert.Single(
                traces.Where(e => e.Kind == NavigationTraceKind.RequestCompleted));

            Assert.Equal(
                NavigationLoadMode.LoadBeforeShow,
                navigatingArgs.LoadMode);
            Assert.Equal(
                NavigationLoadMode.LoadBeforeShow,
                navigatedArgs.LoadMode);
            Assert.Equal(
                NavigationLoadMode.LoadBeforeShow,
                page.LastNavArgs.LoadMode);
            Assert.Equal(
                NavigationLoadMode.LoadBeforeShow,
                log.LoadMode);
            Assert.Equal(
                NavigationLoadMode.LoadInBackground.ToString(),
                requestStart.RequestedLoadMode);
            Assert.Equal(
                NavigationLoadMode.LoadBeforeShow.ToString(),
                attemptEnd.EffectiveLoadMode);
            Assert.Equal(attemptEnd.AttemptId, requestEnd.AttemptId);
            Assert.Null(requestEnd.ParentAttemptId);
            Assert.Equal(0, requestEnd.RedirectDepth);
            Assert.Equal(attemptEnd.TargetPage, requestEnd.TargetPage);
            Assert.Equal(attemptEnd.EffectiveLoadMode, requestEnd.EffectiveLoadMode);
            Assert.Equal(attemptEnd.ReusePolicy, requestEnd.ReusePolicy);
            Assert.Equal(attemptEnd.Presentation, requestEnd.Presentation);
        }

        [Fact]
        public async Task NavigateAsync_GuardRedirect_CreatesLinkedChildAttempt()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubRoleRedirect));
            var traces = new ConcurrentQueue<NavigationTraceEvent>();
            var logs = new List<PageLogEntry>();
            var denied = new List<GuardDeniedEvent>();

            fixture.Context.Events.NavigationTrace += traces.Enqueue;
            fixture.Context.Events.NavigationLogged += logs.Add;
            fixture.Context.Events.GuardDenied += denied.Add;

            await fixture.Runtime.NavigateAsync(
                typeof(StubRoleRedirect),
                NavigationArgs.Default());

            var all = traces.ToArray();
            var attempts = all
                .Where(e => e.Kind == NavigationTraceKind.AttemptStarted)
                .ToArray();
            var terminals = all
                .Where(e => e.Kind == NavigationTraceKind.AttemptCompleted)
                .ToArray();
            var requestEnd = Assert.Single(
                all.Where(e => e.Kind == NavigationTraceKind.RequestCompleted));

            Assert.Equal(2, attempts.Length);
            Assert.Equal(2, terminals.Length);

            var parent = attempts.Single(e =>
                e.TargetPage == typeof(StubRoleRedirect).FullName);
            var child = attempts.Single(e =>
                e.TargetPage == typeof(StubIdle).FullName);
            var parentEnd = terminals.Single(e => e.AttemptId == parent.AttemptId);
            var childEnd = terminals.Single(e => e.AttemptId == child.AttemptId);

            Assert.Null(parent.ParentAttemptId);
            Assert.Equal(parent.AttemptId, child.ParentAttemptId);
            Assert.Equal(NavigationTraceTrigger.Redirect, child.Trigger);
            Assert.Equal(NavigationTraceOutcome.Redirected, parentEnd.Outcome);
            Assert.Equal(NavigationTraceOutcome.Succeeded, childEnd.Outcome);
            Assert.Equal(NavigationTraceOutcome.Succeeded, requestEnd.Outcome);
            Assert.Equal("Redirected", requestEnd.Decision);
            Assert.Equal(childEnd.AttemptId, requestEnd.AttemptId);
            Assert.Equal(parentEnd.AttemptId, requestEnd.ParentAttemptId);
            Assert.Equal(1, requestEnd.RedirectDepth);
            Assert.Equal(childEnd.TargetPage, requestEnd.TargetPage);
            Assert.Equal(childEnd.EffectiveLoadMode, requestEnd.EffectiveLoadMode);
            Assert.Equal(childEnd.ReusePolicy, requestEnd.ReusePolicy);
            Assert.Equal(childEnd.Presentation, requestEnd.Presentation);

            var guardEvent = Assert.Single(denied);
            var pageLog = Assert.Single(logs);
            Assert.Equal(parent.RequestId, guardEvent.RequestId);
            Assert.Equal(parent.AttemptId, guardEvent.AttemptId);
            Assert.Equal(child.AttemptId, pageLog.AttemptId);
            Assert.Equal(parent.RequestId, pageLog.RequestId);
            Assert.IsType<StubIdle>(fixture.Runtime.Current);
        }

        [Fact]
        public async Task NavigateAsync_RedirectChildFailure_RequestProjectsChildTerminal()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubRoleRedirectToFailing),
                typeof(StubFailingTransientLoadBefore));
            var traces = new ConcurrentQueue<NavigationTraceEvent>();
            fixture.Context.Events.NavigationTrace += traces.Enqueue;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => fixture.Runtime.NavigateAsync(
                    typeof(StubRoleRedirectToFailing),
                    NavigationArgs.Default()));

            var all = traces.ToArray();
            var attempts = all
                .Where(e => e.Kind == NavigationTraceKind.AttemptCompleted)
                .ToArray();
            var parent = attempts.Single(e => e.RedirectDepth == 0);
            var child = attempts.Single(e => e.RedirectDepth == 1);
            var request = Assert.Single(all.Where(
                e => e.Kind == NavigationTraceKind.RequestCompleted));

            Assert.Equal(NavigationTraceOutcome.Failed, child.Outcome);
            Assert.Equal(NavigationTraceOutcome.Failed, request.Outcome);
            Assert.Equal(child.AttemptId, request.AttemptId);
            Assert.Equal(parent.AttemptId, request.ParentAttemptId);
            Assert.Equal(1, request.RedirectDepth);
            Assert.Equal(child.TargetPage, request.TargetPage);
            Assert.Equal(
                NavigationLoadMode.LoadBeforeShow.ToString(),
                request.EffectiveLoadMode);
            Assert.Equal(
                PageReusePolicy.Transient.ToString(),
                request.ReusePolicy);
            Assert.Equal(
                PagePresentationMode.Replace.ToString(),
                request.Presentation);
        }

        [Fact]
        public async Task NavigateAsync_UnregisteredTarget_ClosesAttemptAndRequestAsFailed()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>();
            var traces = new ConcurrentQueue<NavigationTraceEvent>();
            fixture.Context.Events.NavigationTrace += traces.Enqueue;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => fixture.Runtime.NavigateAsync(
                    typeof(string),
                    NavigationArgs.Default()));

            var attempt = Assert.Single(
                traces.Where(e => e.Kind == NavigationTraceKind.AttemptCompleted));
            var request = Assert.Single(
                traces.Where(e => e.Kind == NavigationTraceKind.RequestCompleted));

            Assert.Equal(NavigationTraceOutcome.Failed, attempt.Outcome);
            Assert.Equal(NavigationTraceOutcome.Failed, request.Outcome);
            Assert.Equal(request.RequestId, attempt.RequestId);
            Assert.Equal(
                NavigationFailureKind.PageNotRegistered.ToString(),
                attempt.FailureKind);
            Assert.Equal(
                typeof(InvalidOperationException).FullName,
                attempt.ErrorType);
        }

        [Fact]
        public async Task NavigateAsync_GuardDenial_ClosesAttemptAndRequestAsDenied()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubAuthenticated));
            var traces = new ConcurrentQueue<NavigationTraceEvent>();
            fixture.Context.Events.NavigationTrace += traces.Enqueue;

            await fixture.Runtime.NavigateAsync(
                typeof(StubAuthenticated),
                NavigationArgs.Default());

            Assert.Equal(
                NavigationTraceOutcome.Denied,
                Assert.Single(traces.Where(
                    e => e.Kind == NavigationTraceKind.AttemptCompleted)).Outcome);
            Assert.Equal(
                NavigationTraceOutcome.Denied,
                Assert.Single(traces.Where(
                    e => e.Kind == NavigationTraceKind.RequestCompleted)).Outcome);
            Assert.Null(fixture.Runtime.Current);
        }

        [Fact]
        public async Task GoBackAsync_NoHistory_ClosesRequestWithoutAttempt()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>();
            var traces = new ConcurrentQueue<NavigationTraceEvent>();
            fixture.Context.Events.NavigationTrace += traces.Enqueue;

            Assert.False(await fixture.Runtime.GoBackAsync());

            Assert.DoesNotContain(
                traces,
                e => e.Kind == NavigationTraceKind.AttemptStarted);
            var request = Assert.Single(traces.Where(
                e => e.Kind == NavigationTraceKind.RequestCompleted));
            Assert.Equal(NavigationTraceOutcome.NoHistory, request.Outcome);
            Assert.Equal(NavigationTraceTrigger.Back, request.Trigger);
        }

        [Fact]
        public async Task GoBackAsync_PageLog_CarriesBackFlagAndCorrelation()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(typeof(StubA));
            var logs = new List<PageLogEntry>();
            fixture.Context.Events.NavigationLogged += logs.Add;

            await fixture.Runtime.GoIdleAsync();
            await fixture.Runtime.NavigateAsync(
                typeof(StubA),
                NavigationArgs.Default());
            logs.Clear();

            Assert.True(await fixture.Runtime.GoBackAsync());

            var log = Assert.Single(logs);
            Assert.True(log.IsBackNavigation);
            Assert.Equal("Back", log.Trigger);
            Assert.False(string.IsNullOrEmpty(log.RuntimeId));
            Assert.False(string.IsNullOrEmpty(log.RequestId));
            Assert.False(string.IsNullOrEmpty(log.AttemptId));
            Assert.Equal(
                NavigationLoadMode.ShowImmediately,
                log.LoadMode);
        }

        [Fact]
        public async Task GoBackAsync_TargetFailure_RequestProjectsActualAttempt()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubConditionalLoadBefore),
                typeof(StubA));
            var traces = new ConcurrentQueue<NavigationTraceEvent>();
            fixture.Context.Events.NavigationTrace += traces.Enqueue;

            await fixture.Runtime.NavigateAsync(
                typeof(StubConditionalLoadBefore),
                NavigationArgs.Default());
            var target = Assert.IsType<StubConditionalLoadBefore>(
                fixture.Runtime.Current);
            await fixture.Runtime.NavigateAsync(
                typeof(StubA),
                NavigationArgs.Default());
            while (traces.TryDequeue(out _))
            {
            }
            target.FailLoad = true;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => fixture.Runtime.GoBackAsync());

            var attempt = Assert.Single(traces.Where(
                e => e.Kind == NavigationTraceKind.AttemptCompleted));
            var request = Assert.Single(traces.Where(
                e => e.Kind == NavigationTraceKind.RequestCompleted));

            Assert.Equal(NavigationTraceTrigger.Back, request.Trigger);
            Assert.True(request.IsBackNavigation);
            Assert.Equal(NavigationTraceOutcome.Failed, request.Outcome);
            Assert.Equal(attempt.AttemptId, request.AttemptId);
            Assert.Null(request.ParentAttemptId);
            Assert.Equal(0, request.RedirectDepth);
            Assert.Equal(attempt.TargetPage, request.TargetPage);
            Assert.NotEqual("<history>", request.TargetPage);
            Assert.Equal(
                NavigationLoadMode.LoadBeforeShow.ToString(),
                request.EffectiveLoadMode);
            Assert.Equal(
                PageReusePolicy.StrongSingleton.ToString(),
                request.ReusePolicy);
            Assert.Equal(
                PagePresentationMode.Replace.ToString(),
                request.Presentation);
        }

        [Fact]
        public async Task History_DescriptorAlias_IsUsedForBackAndForwardEntries()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubAliased),
                typeof(StubA));

            await fixture.Runtime.GoIdleAsync();
            await fixture.Runtime.NavigateAsync(
                typeof(StubAliased),
                NavigationArgs.Default());
            await fixture.Runtime.NavigateAsync(
                typeof(StubA),
                NavigationArgs.Default());

            Assert.Equal(
                "alias",
                fixture.Context.History.HistoryBack.First().PageName);

            Assert.True(await fixture.Runtime.GoBackAsync());
            Assert.True(await fixture.Runtime.GoBackAsync());

            Assert.Equal(
                "alias",
                fixture.Context.History.HistoryForward.First().PageName);
        }

        [Fact]
        public async Task NavigateAsync_AllowAnonymous_BypassesDescriptorGuard()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubAnonymousGuarded));
            var traces = new ConcurrentQueue<NavigationTraceEvent>();
            var denied = 0;
            fixture.Context.Events.NavigationTrace += traces.Enqueue;
            fixture.Context.Events.GuardDenied += _ => denied++;

            await fixture.Runtime.NavigateAsync(
                typeof(StubAnonymousGuarded),
                NavigationArgs.Default());

            Assert.IsType<StubAnonymousGuarded>(fixture.Runtime.Current);
            Assert.Equal(0, denied);
            Assert.Contains(
                traces,
                e => e.Kind == NavigationTraceKind.Page &&
                     e.Decision == "GuardBypassedAllowAnonymous");
            Assert.Equal(
                NavigationTraceOutcome.Succeeded,
                Assert.Single(traces.Where(
                    e => e.Kind == NavigationTraceKind.AttemptCompleted)).Outcome);
        }

        [Fact]
        public async Task BackgroundLoad_Failure_HasIndependentTerminalAndNoFailurePageLog()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubControllableBackground));
            var traces = new ConcurrentQueue<NavigationTraceEvent>();
            var logs = new ConcurrentQueue<PageLogEntry>();
            var failed = new TaskCompletionSource<NavigationTraceEvent>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            fixture.Context.Events.NavigationTrace += e =>
            {
                traces.Enqueue(e);
                if (e.Kind == NavigationTraceKind.BackgroundLoadFailed)
                    failed.TrySetResult(e);
            };
            fixture.Context.Events.NavigationLogged += logs.Enqueue;

            await fixture.Runtime.NavigateAsync(
                typeof(StubControllableBackground),
                NavigationArgs.Default());
            var page = Assert.IsType<StubControllableBackground>(
                fixture.Runtime.Current);
            await page.Started;
            page.FailLoad(new InvalidOperationException("background failed"));

            var terminal = await AwaitSignal(failed.Task);
            var start = Assert.Single(traces.Where(
                e => e.Kind == NavigationTraceKind.BackgroundLoadStarted));

            Assert.Equal(start.BackgroundOperationId, terminal.BackgroundOperationId);
            Assert.Equal(1, start.BackgroundLoadCount);
            Assert.Equal(0, terminal.BackgroundLoadCount);
            Assert.Equal(typeof(InvalidOperationException).FullName, terminal.ErrorType);
            Assert.Single(logs);
            Assert.All(logs, entry => Assert.True(entry.Success));
            Assert.DoesNotContain(
                traces,
                e => e.Kind == NavigationTraceKind.AttemptCompleted &&
                     e.Outcome == NavigationTraceOutcome.Failed);
        }

        [Fact]
        public async Task BackgroundLoad_Completion_AppliesAndEmitsCompleted()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubControllableBackground));
            var completed = new TaskCompletionSource<NavigationTraceEvent>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Context.Events.NavigationTrace += e =>
            {
                if (e.Kind == NavigationTraceKind.BackgroundLoadCompleted)
                    completed.TrySetResult(e);
            };

            await fixture.Runtime.NavigateAsync(
                typeof(StubControllableBackground),
                NavigationArgs.Default());
            var page = Assert.IsType<StubControllableBackground>(
                fixture.Runtime.Current);
            await page.Started;
            page.CompleteLoad();

            var terminal = await AwaitSignal(completed.Task);
            Assert.Equal(1, page.ApplyCount);
            Assert.True(terminal.Success == true);
            Assert.Equal(0, terminal.BackgroundLoadCount);
        }

        [Fact]
        public async Task BackgroundLoad_WhenPageLeaves_EmitsDiscardedWithoutApply()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubControllableBackground),
                typeof(StubA));
            var traces = new ConcurrentQueue<NavigationTraceEvent>();
            var discarded = new TaskCompletionSource<NavigationTraceEvent>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Context.Events.NavigationTrace += e =>
            {
                traces.Enqueue(e);
                if (e.Kind == NavigationTraceKind.BackgroundLoadDiscarded)
                    discarded.TrySetResult(e);
            };

            await fixture.Runtime.NavigateAsync(
                typeof(StubControllableBackground),
                NavigationArgs.Default());
            var page = Assert.IsType<StubControllableBackground>(
                fixture.Runtime.Current);
            await page.Started;

            await fixture.Runtime.NavigateAsync(
                typeof(StubA),
                NavigationArgs.Default());
            page.CompleteLoad();

            var terminal = await AwaitSignal(discarded.Task);
            Assert.Equal(0, page.ApplyCount);
            Assert.Equal("PageDisposed", terminal.Decision);
            Assert.Equal(0, terminal.BackgroundLoadCount);
            Assert.DoesNotContain(
                traces,
                e => e.Kind == NavigationTraceKind.BackgroundLoadFailed);
        }

        [Fact]
        public async Task Rollback_TargetBackgroundCancellation_DoesNotCancelPreviousLoad()
        {
            var fixture =
                RuntimeTestFixture.BuildWithPageCreated<StubIdle>(
                    page =>
                    {
                        if (page is StubLifecycleBackground target)
                        {
                            target.Observer = operation =>
                            {
                                if (operation == "enter")
                                {
                                    throw new InvalidOperationException(
                                        "target enter failed");
                                }
                            };
                        }
                    },
                    typeof(StubControllableBackground),
                    typeof(StubLifecycleBackground));
            var traces = new ConcurrentQueue<NavigationTraceEvent>();
            var previousCompleted =
                new TaskCompletionSource<NavigationTraceEvent>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Context.Events.NavigationTrace += trace =>
            {
                traces.Enqueue(trace);
                if (trace.Kind ==
                        NavigationTraceKind.BackgroundLoadCompleted &&
                    trace.TargetPage != null &&
                    trace.TargetPage.Contains(
                        nameof(StubControllableBackground)))
                {
                    previousCompleted.TrySetResult(trace);
                }
            };

            await fixture.Runtime.NavigateAsync(
                typeof(StubControllableBackground),
                NavigationArgs.Default());
            var previous = Assert.IsType<StubControllableBackground>(
                fixture.Runtime.Current);
            await previous.Started;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => fixture.Runtime.NavigateAsync(
                    typeof(StubLifecycleBackground),
                    NavigationArgs.Default()));

            Assert.Same(previous, fixture.Runtime.Current);
            Assert.Contains(
                traces,
                trace =>
                    trace.Kind ==
                        NavigationTraceKind.BackgroundLoadDiscarded &&
                    trace.Decision ==
                        NavigationTraceCloseReasons.NavigationRollback);
            Assert.DoesNotContain(
                traces,
                trace =>
                    trace.Kind ==
                        NavigationTraceKind.BackgroundLoadDiscarded &&
                    trace.TargetPage != null &&
                    trace.TargetPage.Contains(
                        nameof(StubControllableBackground)));

            previous.CompleteLoad();
            await AwaitSignal(previousCompleted.Task);
            Assert.Equal(1, previous.ApplyCount);

            var failedTarget = Assert.Single(
                fixture.CreatedPages.OfType<StubLifecycleBackground>());
            failedTarget.CompleteLoad();
        }

        [Fact]
        public async Task ResetAsync_BlockedBackgroundLoad_DiscardsClosesMaskAndRenewsGeneration()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubControllableBackground),
                typeof(StubLoadingMask));
            var traces = new ConcurrentQueue<NavigationTraceEvent>();
            var discarded = new TaskCompletionSource<NavigationTraceEvent>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var completed = new TaskCompletionSource<NavigationTraceEvent>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Context.Events.NavigationTrace += trace =>
            {
                traces.Enqueue(trace);
                if (trace.Kind == NavigationTraceKind.BackgroundLoadDiscarded)
                    discarded.TrySetResult(trace);
                else if (trace.Kind == NavigationTraceKind.BackgroundLoadCompleted)
                    completed.TrySetResult(trace);
            };

            await fixture.Runtime.NavigateAsync(
                typeof(StubControllableBackground),
                NavigationArgs.Default());
            var blocked = Assert.IsType<StubControllableBackground>(
                fixture.Runtime.Current);
            await blocked.Started;
            var firstMask = Assert.Single(
                fixture.CreatedPages.OfType<StubLoadingMask>());

            await AwaitCompletion(fixture.Runtime.ResetAsync());

            Assert.True(discarded.Task.IsCompleted);
            var resetTerminal = await discarded.Task;
            Assert.Equal(
                NavigationTraceCloseReasons.Reset,
                resetTerminal.Decision);
            Assert.Equal(0, resetTerminal.BackgroundLoadCount);
            Assert.Equal(1, firstMask.CloseCount);
            Assert.Contains(firstMask.NativeView, fixture.Host.RemovedViews);
            Assert.True(firstMask.IsDisposed);
            Assert.True(blocked.IsDisposed);
            Assert.Equal(0, blocked.ApplyCount);

            // Reset installs a fresh generation: a later background operation is
            // not born canceled and can complete normally.
            await fixture.Runtime.NavigateAsync(
                typeof(StubControllableBackground),
                NavigationArgs.Default());
            var afterReset = Assert.IsType<StubControllableBackground>(
                fixture.Runtime.Current);
            Assert.NotSame(blocked, afterReset);
            await afterReset.Started;
            afterReset.CompleteLoad();
            await AwaitSignal(completed.Task);
            Assert.Equal(1, afterReset.ApplyCount);

            // The detached page-owned task may finish or fault later, but it must
            // not create a second terminal for the canceled wrapper.
            blocked.FailLoad(new InvalidOperationException("late failure"));
            await Task.Yield();
            Assert.Single(traces.Where(trace =>
                trace.Kind == NavigationTraceKind.BackgroundLoadDiscarded));
        }

        [Fact]
        public async Task ShowImmediately_OrdersCompletePageLifecycle()
        {
            var order = new List<string>();
            var fixture = RuntimeTestFixture.BuildWithPageCreated<StubIdle>(
                page =>
                {
                    if (page is StubLifecycleRecordingPage recording)
                    {
                        var prefix = page is StubLifecycleSource ? "from" : "to";
                        recording.Observer =
                            operation => order.Add(prefix + "." + operation);
                    }
                },
                typeof(StubLifecycleSource),
                typeof(StubLifecycleShowImmediately));

            await fixture.Runtime.NavigateAsync(
                typeof(StubLifecycleSource),
                NavigationArgs.Default());
            order.Clear();
            fixture.Host.OperationObserved = (operation, page) =>
            {
                var prefix = page is StubLifecycleSource ? "from" : "to";
                order.Add(prefix + "." + operation);
            };

            await fixture.Runtime.NavigateAsync(
                typeof(StubLifecycleShowImmediately),
                NavigationArgs.Default());

            Assert.Equal(
                new[]
                {
                    "from.hide",
                    "from.leave",
                    "from.detach",
                    "to.attach",
                    "to.front",
                    "to.show",
                    "to.load",
                    "to.apply",
                    "to.enter",
                    "from.dispose"
                },
                order);
        }

        [Fact]
        public async Task LoadBeforeShow_LoadsBeforeLeavingCurrentPage()
        {
            var order = new List<string>();
            var fixture = RuntimeTestFixture.BuildWithPageCreated<StubIdle>(
                page =>
                {
                    if (page is StubLifecycleRecordingPage recording)
                    {
                        var prefix = page is StubLifecycleSource ? "from" : "to";
                        recording.Observer =
                            operation => order.Add(prefix + "." + operation);
                    }
                },
                typeof(StubLifecycleSource),
                typeof(StubLifecycleLoadBeforeShow));

            await fixture.Runtime.NavigateAsync(
                typeof(StubLifecycleSource),
                NavigationArgs.Default());
            order.Clear();
            fixture.Host.OperationObserved = (operation, page) =>
            {
                var prefix = page is StubLifecycleSource ? "from" : "to";
                order.Add(prefix + "." + operation);
            };

            await fixture.Runtime.NavigateAsync(
                typeof(StubLifecycleLoadBeforeShow),
                NavigationArgs.Default());

            Assert.Equal(
                new[]
                {
                    "to.load",
                    "to.apply",
                    "from.hide",
                    "from.leave",
                    "from.detach",
                    "to.attach",
                    "to.front",
                    "to.show",
                    "to.enter",
                    "from.dispose"
                },
                order);
        }

        [Fact]
        public async Task LoadInBackground_RequestCompletesBeforeBackgroundTerminal()
        {
            var order = new ConcurrentQueue<string>();
            var traces = new ConcurrentQueue<NavigationTraceEvent>();
            var backgroundTerminal =
                new TaskCompletionSource<NavigationTraceEvent>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            var fixture = RuntimeTestFixture.BuildWithPageCreated<StubIdle>(
                page =>
                {
                    if (page is StubLifecycleRecordingPage recording)
                    {
                        var prefix = page is StubLifecycleSource ? "from" : "to";
                        recording.Observer =
                            operation => order.Enqueue(prefix + "." + operation);
                    }
                },
                typeof(StubLifecycleSource),
                typeof(StubLifecycleBackground));

            await fixture.Runtime.NavigateAsync(
                typeof(StubLifecycleSource),
                NavigationArgs.Default());
            while (order.TryDequeue(out _))
            {
            }

            fixture.Context.Events.NavigationTrace += trace =>
            {
                traces.Enqueue(trace);
                if (trace.Kind == NavigationTraceKind.RequestCompleted)
                    order.Enqueue("request.completed");
                else if (trace.Kind == NavigationTraceKind.BackgroundLoadCompleted)
                {
                    order.Enqueue("background.completed");
                    backgroundTerminal.TrySetResult(trace);
                }
            };

            await fixture.Runtime.NavigateAsync(
                typeof(StubLifecycleBackground),
                NavigationArgs.Default());
            var page = Assert.IsType<StubLifecycleBackground>(
                fixture.Runtime.Current);
            await page.Started;

            var beforeBackgroundTerminal = order.ToArray();
            Assert.Contains("to.enter", beforeBackgroundTerminal);
            Assert.Contains("request.completed", beforeBackgroundTerminal);
            Assert.True(
                Array.IndexOf(beforeBackgroundTerminal, "to.enter") <
                Array.IndexOf(beforeBackgroundTerminal, "request.completed"));
            Assert.DoesNotContain("to.apply", beforeBackgroundTerminal);
            Assert.DoesNotContain("background.completed", beforeBackgroundTerminal);

            page.CompleteLoad();
            await AwaitSignal(backgroundTerminal.Task);

            var completed = order.ToArray();
            Assert.True(
                Array.IndexOf(completed, "request.completed") <
                Array.IndexOf(completed, "background.completed"));
            Assert.True(
                Array.IndexOf(completed, "to.apply") <
                Array.IndexOf(completed, "background.completed"));
            Assert.All(
                traces.Where(trace =>
                    trace.Kind == NavigationTraceKind.BackgroundLoadStarted ||
                    trace.Kind == NavigationTraceKind.BackgroundLoadCompleted),
                trace => Assert.Equal("background-alias", trace.TargetPage));
        }

        [Fact]
        public async Task KeepAttached_ReturnNavigationDoesNotAttachSamePageAgain()
        {
            var order = new List<string>();
            var fixture = RuntimeTestFixture.BuildWithPageCreated<StubIdle>(
                page =>
                {
                    if (page is StubLifecycleRecordingPage recording)
                    {
                        var prefix = page is StubLifecycleKeepAttached
                            ? "kept"
                            : "other";
                        recording.Observer =
                            operation => order.Add(prefix + "." + operation);
                    }
                },
                typeof(StubLifecycleKeepAttached),
                typeof(StubLifecycleShowImmediately));

            await fixture.Runtime.NavigateAsync(
                typeof(StubLifecycleKeepAttached),
                NavigationArgs.Default());
            var kept = Assert.IsType<StubLifecycleKeepAttached>(
                fixture.Runtime.Current);
            order.Clear();
            fixture.Host.OperationObserved = (operation, page) =>
            {
                var prefix = page is StubLifecycleKeepAttached
                    ? "kept"
                    : "other";
                order.Add(prefix + "." + operation);
            };

            await fixture.Runtime.NavigateAsync(
                typeof(StubLifecycleShowImmediately),
                NavigationArgs.Default());

            Assert.Equal(1, fixture.Host.Attached.Count(page =>
                ReferenceEquals(page, kept)));
            Assert.DoesNotContain(kept, fixture.Host.Detached);
            Assert.DoesNotContain("kept.detach", order);
            Assert.DoesNotContain("kept.dispose", order);
            Assert.Equal("kept.hide", order[0]);
            Assert.Equal("kept.leave", order[1]);

            order.Clear();
            await fixture.Runtime.NavigateAsync(
                typeof(StubLifecycleKeepAttached),
                NavigationArgs.Default());

            Assert.Equal(1, fixture.Host.Attached.Count(page =>
                ReferenceEquals(page, kept)));
            Assert.DoesNotContain("kept.attach", order);
            Assert.Equal(
                new[]
                {
                    "other.hide",
                    "other.leave",
                    "other.detach",
                    "kept.front",
                    "kept.show",
                    "kept.load",
                    "kept.apply",
                    "kept.enter",
                    "other.dispose"
                },
                order);
        }

        [Fact]
        public void StartRequest_WithoutAnyObserver_ReturnsNullScope()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(typeof(StubA));

            Assert.False(fixture.Context.Diagnostics.IsTracingEnabled);
            Assert.Null(fixture.Context.Diagnostics.StartRequest(
                fixture.Runtime.RuntimeId,
                null,
                typeof(StubA),
                typeof(StubA).FullName,
                NavigationArgs.Default(),
                NavigationTraceTrigger.Navigate));
        }

        [Fact]
        public async Task PagePresenceEvents_ReplacementAndRepeatedReset_ReportOnlyTransitions()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(typeof(StubA));
            var first = 0;
            var noAttached = 0;
            var noVisible = 0;
            fixture.Runtime.OnFirstPageAttached += _ => first++;
            fixture.Runtime.OnNoPageAttached += () => noAttached++;
            fixture.Runtime.OnNoPageVisible += () => noVisible++;

            await fixture.Runtime.GoIdleAsync();
            await fixture.Runtime.NavigateAsync(
                typeof(StubA),
                NavigationArgs.Default());

            Assert.Equal(1, first);
            Assert.Equal(0, noAttached);
            Assert.Equal(0, noVisible);

            await fixture.Runtime.ResetAsync();

            Assert.Equal(1, first);
            Assert.Equal(1, noAttached);
            Assert.Equal(1, noVisible);

            await fixture.Runtime.ResetAsync();

            Assert.Equal(1, first);
            Assert.Equal(1, noAttached);
            Assert.Equal(1, noVisible);
        }

        [Fact]
        public async Task ResetAsync_ActivePage_OrdersHideLeaveDetachDispose()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubTeardownPage));
            await fixture.Runtime.NavigateAsync(
                typeof(StubTeardownPage),
                NavigationArgs.Default());
            var page = Assert.IsType<StubTeardownPage>(fixture.Runtime.Current);
            var order = new List<string>();
            page.Observer = order.Add;
            fixture.Host.OperationObserved = (operation, observedPage) =>
            {
                if (ReferenceEquals(page, observedPage))
                    order.Add(operation);
            };

            await fixture.Runtime.ResetAsync();

            Assert.Equal(
                new[] { "hide", "leave", "detach", "dispose" },
                order);
            Assert.True(page.IsDisposed);
            Assert.Null(fixture.Runtime.Current);
        }

        [Fact]
        public async Task DisposeAsync_ActivePage_OrdersHideLeaveDetachDispose()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubTeardownPage));
            await fixture.Runtime.NavigateAsync(
                typeof(StubTeardownPage),
                NavigationArgs.Default());
            var page = Assert.IsType<StubTeardownPage>(fixture.Runtime.Current);
            var order = new List<string>();
            page.Observer = order.Add;
            fixture.Host.OperationObserved = (operation, observedPage) =>
            {
                if (ReferenceEquals(page, observedPage))
                    order.Add(operation);
            };

            await fixture.Runtime.DisposeAsync();

            Assert.Equal(
                new[] { "hide", "leave", "detach", "dispose" },
                order);
            Assert.True(page.IsDisposed);
            Assert.Null(fixture.Runtime.Current);
        }

        [Fact]
        public async Task ResetAsync_HiddenKeepAttached_DoesNotRepeatExitLifecycle()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubKeepAttachedTeardown),
                typeof(StubA));
            await fixture.Runtime.NavigateAsync(
                typeof(StubKeepAttachedTeardown),
                NavigationArgs.Default());
            var kept = Assert.IsType<StubKeepAttachedTeardown>(
                fixture.Runtime.Current);
            await fixture.Runtime.NavigateAsync(
                typeof(StubA),
                NavigationArgs.Default());

            Assert.Equal(1, kept.TeardownCalls.Count(c => c == "hide"));
            Assert.Equal(1, kept.TeardownCalls.Count(c => c == "leave"));

            await fixture.Runtime.ResetAsync();

            Assert.Equal(1, kept.TeardownCalls.Count(c => c == "hide"));
            Assert.Equal(1, kept.TeardownCalls.Count(c => c == "leave"));
            Assert.Equal(1, kept.TeardownCalls.Count(c => c == "dispose"));
        }

        [Fact]
        public async Task ResetAsync_ExitHookThrows_StillCleansPagesAndHistory()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubThrowingTeardown));
            await fixture.Runtime.GoIdleAsync();
            await fixture.Runtime.NavigateAsync(
                typeof(StubThrowingTeardown),
                NavigationArgs.Default());
            var page = Assert.IsType<StubThrowingTeardown>(
                fixture.Runtime.Current);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => fixture.Runtime.ResetAsync());

            Assert.Equal("hide failed", error.Message);
            Assert.Null(fixture.Runtime.Current);
            Assert.True(page.IsDisposed);
            Assert.Contains(page, fixture.Host.Detached);
            Assert.Empty(fixture.Context.History.HistoryBack);
            Assert.Empty(fixture.Context.History.HistoryForward);
            Assert.Contains("hide", page.TeardownCalls);
            Assert.Contains("leave", page.TeardownCalls);
            Assert.Contains("dispose", page.TeardownCalls);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RuntimeTeardown_PageDisposeThrows_FailsAfterBestEffortCleanup(
            bool disposeRuntime)
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubThrowingDispose));
            var traces = new ConcurrentQueue<NavigationTraceEvent>();
            fixture.Context.Events.NavigationTrace += traces.Enqueue;

            await fixture.Runtime.NavigateAsync(
                typeof(StubThrowingDispose),
                NavigationArgs.Default());
            var page = Assert.IsType<StubThrowingDispose>(
                fixture.Runtime.Current);

            async Task TeardownAsync()
            {
                if (disposeRuntime)
                    await fixture.Runtime.DisposeAsync();
                else
                    await fixture.Runtime.ResetAsync();
            }

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                TeardownAsync);

            Assert.Equal("dispose failed", error.Message);
            Assert.Equal(1, page.DisposeCallCount);
            Assert.Contains(page, fixture.Host.Detached);
            Assert.Null(fixture.Runtime.Current);
            Assert.Empty(fixture.Context.History.HistoryBack);
            Assert.Empty(fixture.Context.History.HistoryForward);
            Assert.Contains(
                traces,
                trace =>
                    trace.Kind == NavigationTraceKind.Runtime &&
                    trace.Decision == (
                        disposeRuntime
                            ? "DisposeFailed"
                            : "ResetFailed") &&
                    trace.Success == false &&
                    trace.ErrorType ==
                        typeof(InvalidOperationException).FullName);
        }

        [Fact]
        public async Task ResetAsync_CachedDisposeThrows_ContinuesAndClearsAllCaches()
        {
            var fixture = RuntimeTestFixture.Build<StubIdle>(
                typeof(StubThrowingDispose),
                typeof(StubTeardownPage),
                typeof(StubA));

            await fixture.Runtime.NavigateAsync(
                typeof(StubThrowingDispose),
                NavigationArgs.Default());
            var throwing = Assert.IsType<StubThrowingDispose>(
                fixture.Runtime.Current);
            await fixture.Runtime.NavigateAsync(
                typeof(StubTeardownPage),
                NavigationArgs.Default());
            var otherCached = Assert.IsType<StubTeardownPage>(
                fixture.Runtime.Current);
            await fixture.Runtime.NavigateAsync(
                typeof(StubA),
                NavigationArgs.Default());

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => fixture.Runtime.ResetAsync());

            Assert.Equal("dispose failed", error.Message);
            Assert.Equal(1, throwing.DisposeCallCount);
            Assert.True(otherCached.IsDisposed);
            Assert.Null(fixture.Runtime.Current);
            Assert.Empty(fixture.Context.History.HistoryBack);
            Assert.Empty(fixture.Context.History.HistoryForward);

            await fixture.Runtime.NavigateAsync(
                typeof(StubThrowingDispose),
                NavigationArgs.Default());

            Assert.NotSame(throwing, fixture.Runtime.Current);
        }

        private static async Task<NavigationTraceEvent> AwaitSignal(
            Task<NavigationTraceEvent> signal)
        {
            var completed = await Task.WhenAny(signal, Task.Delay(5000));
            Assert.Same(signal, completed);
            return await signal;
        }

        private static async Task AwaitCompletion(Task operation)
        {
            var completed = await Task.WhenAny(operation, Task.Delay(5000));
            Assert.Same(operation, completed);
            await operation;
        }
    }
}
