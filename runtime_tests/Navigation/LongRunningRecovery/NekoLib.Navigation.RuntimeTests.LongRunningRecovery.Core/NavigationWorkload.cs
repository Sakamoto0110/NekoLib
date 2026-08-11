#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NekoLib.Inspection;
using NekoLib.Navigation;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.RuntimeTests.Harness.Reporting;

namespace NekoLib.Navigation.RuntimeTests.LongRunningRecovery
{
    internal static class NavigationWorkload
    {
        public static async Task RunAllAsync(NavigationRunContext context)
        {
            await RunPageLifetimeAsync(context);
            await RunHistoryAndSessionAsync(context);
            await RunLoadModesAsync(context);
            await RunSurfacesAsync(context);
            await RunIdleAsync(context);
            await RunMountAndShutdownAsync(context);
        }

        public static async Task RunRecoveryProbeAsync(NavigationRunContext context)
        {
            if (!context.Mounted) await context.StartAsync();
            await context.NavigateSuccessAsync(context.Platform.Pages.Idle);
            await context.NavigateSuccessAsync(context.Platform.Pages.Strong);
            await context.NavigateSuccessAsync(context.Platform.Pages.Idle);
        }

        private static async Task RunPageLifetimeAsync(NavigationRunContext context)
        {
            await context.Runner.RunAsync("pages", "transient-strong-weak-lifetimes",
                "transient pages dispose, strong pages reuse identity, and weak pages can be recreated after collection",
                async check =>
                {
                    await NavigationService.ResetAsync();

                    await context.NavigateSuccessAsync(context.Platform.Pages.Transient);
                    PageProbe transient = CurrentProbe();
                    await context.NavigateSuccessAsync(context.Platform.Pages.Strong);
                    check.That(transient.Metrics.Disposed >= 1, "the transient page was not disposed after leaving");

                    PageProbe strong = CurrentProbe();
                    int strongId = strong.InstanceId;
                    await context.NavigateSuccessAsync(context.Platform.Pages.Idle);
                    await context.NavigateSuccessAsync(context.Platform.Pages.Strong);
                    check.Equal(strongId, CurrentProbe().InstanceId, "strong singleton identity");

                    await context.NavigateSuccessAsync(context.Platform.Pages.Weak);
                    int weakId = CurrentProbe().InstanceId;
                    await context.NavigateSuccessAsync(context.Platform.Pages.Weak);
                    check.Equal(weakId, CurrentProbe().InstanceId,
                        "weak singleton identity while the current page roots it");
                    WeakReference weak = new WeakReference(NavigationService.Current);
                    await context.NavigateSuccessAsync(context.Platform.Pages.Idle);
                    ForceCollection(weak);
                    check.That(!weak.IsAlive, "the weak singleton stayed alive after every scenario root was released");

                    long before = context.State.Metrics(context.Platform.Pages.Weak).Constructed;
                    await context.NavigateSuccessAsync(context.Platform.Pages.Weak);
                    check.Equal(before + 1,
                        context.State.Metrics(context.Platform.Pages.Weak).Constructed,
                        "weak singleton construction count after collection");
                });

            await context.Runner.RunAsync("pages", "keep-attached-and-reset",
                "a hidden keep-attached singleton stays attached, then reset releases and disposes it",
                async check =>
                {
                    long strongDisposed = context.State.Metrics(
                        context.Platform.Pages.Strong).Disposed;
                    await NavigationService.ResetAsync();
                    check.That(context.State.Metrics(context.Platform.Pages.Strong).Disposed > strongDisposed,
                        "reset did not dispose the strong singleton cache entry");
                    await context.NavigateSuccessAsync(context.Platform.Pages.KeepAttached);
                    PageProbe kept = CurrentProbe();
                    await context.NavigateSuccessAsync(context.Platform.Pages.Idle);

                    check.Equal(1, kept.Metrics.Attached, "keep-attached count while hidden");
                    check.Equal(0, kept.Metrics.Visible, "keep-attached visible count while hidden");

                    await NavigationService.ResetAsync();
                    check.Equal(0, kept.Metrics.Attached, "keep-attached count after reset");
                    check.That(kept.Metrics.Disposed >= 1, "reset did not dispose the kept singleton");
                    check.That(!NavigationService.History.HasHistory, "reset did not clear history");
                    await context.NavigateSuccessAsync(context.Platform.Pages.Idle);
                });
        }

        private static async Task RunHistoryAndSessionAsync(NavigationRunContext context)
        {
            await context.Runner.RunAsync("history", "back-restores-state-before-enter",
                "back navigation restores the captured state before the enter lifecycle and preserves history invariants",
                async check =>
                {
                    await NavigationService.ResetAsync();
                    await context.NavigateSuccessAsync(context.Platform.Pages.Strong);
                    PageProbe stateful = CurrentProbe();
                    stateful.StateValue = 173;

                    await context.NavigateSuccessAsync(context.Platform.Pages.Transient);
                    bool back = false;
                    Exception? error = await context.ExecuteRequestAsync(async () =>
                    {
                        back = await NavigationService.GoBackAsync();
                    }, false);

                    check.That(error == null && back, "GoBackAsync did not return true");
                    PageProbe restored = CurrentProbe();
                    check.Equal(173, restored.RestoredStateValue, "restored state value");
                    check.That(restored.LastSequence("restore") > 0 &&
                               restored.LastSequence("restore") < restored.LastSequence("enter"),
                        "RestoreState did not run before OnNavigatedToAsync");
                });

            await context.Runner.RunAsync("guards", "authentication-role-permission-and-redirect",
                "built-in session guards deny, allow, and redirect without corrupting current page or history",
                async check =>
                {
                    await NavigationService.ResetAsync();
                    await context.NavigateSuccessAsync(context.Platform.Pages.Idle);
                    Type initial = NavigationService.Current.GetType();
                    int backBefore = NavigationService.History.HistoryBack.Count();

                    NavigationService.Session.SignOut();
                    await context.ExecuteRequestAsync(
                        () => NavigationService.SwitchPage(context.Platform.Pages.Authenticated), true);
                    check.Equal(initial.FullName, NavigationService.Current.GetType().FullName,
                        "current page after authentication denial");
                    check.Equal(backBefore, NavigationService.History.HistoryBack.Count(),
                        "back history after authentication denial");

                    NavigationService.Session.SignIn("viewer");
                    await context.ExecuteRequestAsync(
                        () => NavigationService.SwitchPage(context.Platform.Pages.Role), true);
                    check.Equal(initial.FullName, NavigationService.Current.GetType().FullName,
                        "current page after role denial");

                    await context.ExecuteRequestAsync(
                        () => NavigationService.SwitchPage(context.Platform.Pages.Permission), true);
                    check.Equal(context.Platform.Pages.Idle.FullName,
                        NavigationService.Current.GetType().FullName,
                        "permission-denial redirect target");

                    NavigationService.Session.SignIn(
                        new[] { "operator" },
                        new[] { "sell" });
                    await context.NavigateSuccessAsync(context.Platform.Pages.Authenticated);
                    await context.NavigateSuccessAsync(context.Platform.Pages.Role);
                    await context.NavigateSuccessAsync(context.Platform.Pages.Permission);

                    NavigationService.Session.SignOut();
                    await context.NavigateSuccessAsync(context.Platform.Pages.Idle);
                    int backBeforeRedirect = NavigationService.History.HistoryBack.Count();
                    await context.ExecuteRequestAsync(
                        () => NavigationService.SwitchPage(context.Platform.Pages.RedirectToIdle), false);
                    check.Equal(context.Platform.Pages.Idle.FullName,
                        NavigationService.Current.GetType().FullName,
                        "redirect target");
                    var backAfterRedirect = NavigationService.History.HistoryBack.ToArray();
                    check.Equal(backBeforeRedirect + 1, backAfterRedirect.Length,
                        "back history count after redirect to the current idle page");
                    check.Equal(context.Platform.Pages.Idle.FullName,
                        backAfterRedirect[0].PageType.FullName,
                        "back history entry after redirect to the current idle page");
                    check.That(context.State.GuardDenials > 0, "no public guard-denied event was observed");
                });
        }

        private static async Task RunLoadModesAsync(NavigationRunContext context)
        {
            await context.Runner.RunAsync("loads", "load-mode-ordering-and-background-terminals",
                "load-before, show-immediately, successful, failed, discarded, and late background work keep their contracts",
                async check =>
                {
                    await NavigationService.ResetAsync();
                    await context.NavigateSuccessAsync(context.Platform.Pages.Idle);

                    await context.NavigateSuccessAsync(context.Platform.Pages.LoadBefore);
                    PageProbe before = CurrentProbe();
                    check.That(before.FirstSequence("load-start") < before.FirstSequence("show"),
                        "LoadBeforeShow attached or showed before loading started");

                    await context.NavigateSuccessAsync(context.Platform.Pages.ShowImmediately);
                    PageProbe immediate = CurrentProbe();
                    check.That(immediate.FirstSequence("show") < immediate.FirstSequence("load-start"),
                        "ShowImmediately loaded before the page was shown");

                    PageTypeMetrics backgroundMetrics = context.State.Metrics(context.Platform.Pages.Background);
                    long applyBefore = backgroundMetrics.AppliedCount;
                    context.State.ConfigureLoad(context.Platform.Pages.Background, ScenarioLoadBehavior.Block);
                    await context.NavigateSuccessAsync(context.Platform.Pages.Background);
                    await WaitUntilAsync(() => context.State.ActiveBackground > 0, TimeSpan.FromSeconds(5), context.Ct);
                    check.Equal(applyBefore, backgroundMetrics.AppliedCount,
                        "background apply count while load is blocked");
                    context.State.ReleaseLoad(context.Platform.Pages.Background);
                    await WaitUntilAsync(() => backgroundMetrics.AppliedCount > applyBefore,
                        TimeSpan.FromSeconds(5), context.Ct);

                    context.State.ConfigureLoad(context.Platform.Pages.Background, ScenarioLoadBehavior.Fail);
                    long appliedBeforeFailure = backgroundMetrics.AppliedCount;
                    await context.NavigateSuccessAsync(context.Platform.Pages.Background);
                    await WaitUntilAsync(() => context.State.ActiveBackground == 0,
                        TimeSpan.FromSeconds(5), context.Ct);
                    check.Equal(appliedBeforeFailure, backgroundMetrics.AppliedCount,
                        "apply count after failed background load");

                    context.State.ConfigureLoad(context.Platform.Pages.Background, ScenarioLoadBehavior.Block);
                    await context.NavigateSuccessAsync(context.Platform.Pages.Background);
                    await WaitUntilAsync(() => context.State.ActiveBackground > 0,
                        TimeSpan.FromSeconds(5), context.Ct);
                    long appliedBeforeDiscard = backgroundMetrics.AppliedCount;
                    await context.NavigateSuccessAsync(context.Platform.Pages.Idle);
                    context.State.ReleaseLoad(context.Platform.Pages.Background);
                    await WaitUntilAsync(() => context.State.ActiveBackground == 0,
                        TimeSpan.FromSeconds(5), context.Ct);
                    check.Equal(appliedBeforeDiscard, backgroundMetrics.AppliedCount,
                        "discarded background result was applied after navigation away");

                    context.State.ConfigureLoad(context.Platform.Pages.Background, ScenarioLoadBehavior.Block);
                    await context.NavigateSuccessAsync(context.Platform.Pages.Background);
                    await WaitUntilAsync(() => context.State.ActiveBackground > 0,
                        TimeSpan.FromSeconds(5), context.Ct);
                    long appliedBeforeReset = backgroundMetrics.AppliedCount;
                    await NavigationService.ResetAsync();
                    context.State.ReleaseLoad(context.Platform.Pages.Background);
                    await WaitUntilAsync(() => context.State.ActiveBackground == 0,
                        TimeSpan.FromSeconds(5), context.Ct);
                    check.Equal(appliedBeforeReset, backgroundMetrics.AppliedCount,
                        "late background result was applied after reset");

                    context.State.ConfigureLoad(context.Platform.Pages.Background, ScenarioLoadBehavior.Complete);
                    await context.NavigateSuccessAsync(context.Platform.Pages.Idle);
                });
        }

        private static async Task RunSurfacesAsync(NavigationRunContext context)
        {
            await context.Runner.RunAsync("surfaces", "surface-repetition-and-modal-depth",
                "toast, dialog, prompt, and popover repeat; overlapping modals and pending awaiters close on reset",
                async check =>
                {
                    for (int i = 0; i < 5; i++)
                    {
                        bool dialog = await context.Platform.ShowDialogAsync(new SurfaceDirective());
                        string? prompt = await context.Platform.ShowPromptAsync(new SurfaceDirective());
                        bool popover = await context.Platform.ShowPopoverAsync(new SurfaceDirective());
                        context.Platform.ShowToast(new SurfaceDirective(), 50);
                        NavigationService.DismissCurrentToast();

                        check.That(dialog, "dialog did not complete true on repetition " + i);
                        check.Equal("scenario-result", prompt, "prompt result on repetition " + i);
                        check.That(popover, "popover did not complete true on repetition " + i);
                    }

                    Task<bool> pendingDialog = context.Platform.ShowDialogAsync(
                        new SurfaceDirective { Complete = false });
                    Task<string?> pendingPrompt = context.Platform.ShowPromptAsync(
                        new SurfaceDirective { Complete = false });
                    await context.Platform.YieldUiAsync();

                    check.That(context.Platform.Controls.Metrics.MaxModalViews >= 2,
                        "overlapping dialog/prompt never reached modal depth two");

                    await NavigationService.ResetAsync();
                    check.That(!await pendingDialog, "reset did not resolve the pending dialog as false");
                    check.That(await pendingPrompt == null, "reset did not resolve the pending prompt as default");
                    check.Equal(0, context.Platform.Controls.Metrics.ModalViewsLive,
                        "modal views after reset");
                    await context.NavigateSuccessAsync(context.Platform.Pages.Idle);
                });
        }

        private static async Task RunIdleAsync(NavigationRunContext context)
        {
            await context.Runner.RunAsync("idle", "idle-denial-rearm-and-signout",
                "a denied idle transition rearms; interaction rearms; the next tick signs out and reaches idle",
                async check =>
                {
                    NavigationService.Session.SignIn("operator");
                    await context.NavigateSuccessAsync(context.Platform.Pages.Strong);

                    long starts = context.Platform.Controls.Metrics.TimerStarts;
                    long guardDenials = context.State.GuardDenials;
                    context.State.DenyIdle = true;
                    context.Platform.Controls.FireIdleTick();
                    await WaitUntilAsync(() => context.State.GuardDenials > guardDenials,
                        TimeSpan.FromSeconds(5), context.Ct);
                    await WaitUntilAsync(() => context.Platform.Controls.Metrics.TimerStarts > starts,
                        TimeSpan.FromSeconds(5), context.Ct);
                    check.Equal(context.Platform.Pages.Strong.FullName,
                        NavigationService.Current.GetType().FullName,
                        "current page after denied idle navigation");

                    long afterDenial = context.Platform.Controls.Metrics.TimerStarts;
                    context.Platform.Controls.PulseInteraction();
                    check.That(context.Platform.Controls.Metrics.TimerStarts > afterDenial,
                        "scenario-owned native interaction pulse did not rearm the timer");

                    context.State.DenyIdle = false;
                    NavigationService.Session.SignIn("operator");
                    context.Platform.Controls.FireIdleTick();
                    await WaitUntilAsync(() =>
                        NavigationService.Current != null &&
                        NavigationService.Current.GetType() == context.Platform.Pages.Idle,
                        TimeSpan.FromSeconds(5), context.Ct);
                    check.That(!NavigationService.Session.IsAuthenticated,
                        "the admitted idle transition did not sign out the session");
                });
        }

        private static async Task RunMountAndShutdownAsync(NavigationRunContext context)
        {
            await context.Runner.RunAsync("lifecycle", "repeated-mount-shutdown-and-pending-surfaces",
                "fresh contexts mount only after awaited shutdown, pending surfaces resolve, handlers collect, and idle stops",
                async check =>
                {
                    for (int i = 0; i < 3; i++)
                    {
                        await context.NavigateSuccessAsync(context.Platform.Pages.Strong);
                        PageProbe strong = CurrentProbe();
                        long strongDisposed = strong.Metrics.Disposed;
                        Task<bool> dialog = context.Platform.ShowDialogAsync(
                            new SurfaceDirective { Complete = false });
                        Task<bool> popover = context.Platform.ShowPopoverAsync(
                            new SurfaceDirective { Complete = false });
                        await context.Platform.YieldUiAsync();
                        long ticks = context.Platform.Controls.Metrics.TimerTicks;

                        await context.ShutdownAsync();
                        check.That(!await dialog, "shutdown did not resolve a pending dialog");
                        check.That(!await popover, "shutdown did not resolve a pending popover");
                        context.Platform.Controls.FireIdleTick();
                        check.Equal(ticks, context.Platform.Controls.Metrics.TimerTicks,
                            "a stopped/disposed idle timer accepted a tick after shutdown");
                        check.That(context.LastHandlerOwnerCollected(),
                            "Shutdown retained a static CurrentChanged subscriber owner");
                        check.That(strong.Metrics.Disposed > strongDisposed,
                            "Shutdown did not dispose the strong singleton");
                        check.Equal(0, context.Inspection.GetDiagnostics().ProviderCount,
                            "Navigation Inspection providers after shutdown");

                        await context.StartAsync();
                        await context.NavigateSuccessAsync(context.Platform.Pages.Idle);
                    }
                });

            await context.Runner.RunAsync("lifecycle", "admitted-request-and-shutdown-cutoff",
                "an admitted request finishes before shutdown and later requests are rejected until a fresh mount",
                async check =>
                {
                    context.State.ConfigureLoad(context.Platform.Pages.LoadBefore, ScenarioLoadBehavior.Block);
                    Task navigation = NavigationService.SwitchPage(context.Platform.Pages.LoadBefore);
                    await WaitUntilAsync(() => context.State.ActiveBackground > 0,
                        TimeSpan.FromSeconds(5), context.Ct);

                    Task shutdown = context.ShutdownAsync();
                    context.State.ReleaseLoad(context.Platform.Pages.LoadBefore);
                    await navigation;
                    await shutdown;

                    Exception? rejected = await CaptureAsync(
                        () => NavigationService.SwitchPage(context.Platform.Pages.Idle));
                    check.That(rejected is InvalidOperationException,
                        "a request after the shutdown cutoff was not rejected with InvalidOperationException");

                    await context.StartAsync();
                    await context.NavigateSuccessAsync(context.Platform.Pages.Idle);

                    context.State.ConfigureLoad(
                        context.Platform.Pages.Background, ScenarioLoadBehavior.Block);
                    await context.NavigateSuccessAsync(context.Platform.Pages.Background);
                    await WaitUntilAsync(() => context.State.ActiveBackground > 0,
                        TimeSpan.FromSeconds(5), context.Ct);
                    long applied = context.State.Metrics(
                        context.Platform.Pages.Background).AppliedCount;
                    await context.ShutdownAsync();
                    context.State.ReleaseLoad(context.Platform.Pages.Background);
                    await WaitUntilAsync(() => context.State.ActiveBackground == 0,
                        TimeSpan.FromSeconds(5), context.Ct);
                    check.Equal(applied,
                        context.State.Metrics(context.Platform.Pages.Background).AppliedCount,
                        "background result was applied during shutdown");

                    await context.StartAsync();
                    await context.NavigateSuccessAsync(context.Platform.Pages.Idle);
                });

            await context.Runner.RunAsync("lifecycle", "shutdown-near-idle-tick",
                "shutdown racing a scenario-triggered idle tick completes and leaves no admitted work behind",
                async check =>
                {
                    NavigationService.Session.SignIn("operator");
                    await context.NavigateSuccessAsync(context.Platform.Pages.Strong);
                    context.Platform.Controls.FireIdleTick();
                    await context.ShutdownAsync();
                    check.Equal(0, context.State.ActiveBackground, "background work after idle/shutdown race");
                    check.Equal(0, context.Platform.Controls.Metrics.ViewsLive,
                        "surface ownership after idle/shutdown race");
                    await context.StartAsync();
                    await context.NavigateSuccessAsync(context.Platform.Pages.Idle);
                });
        }

        public static async Task RunSustainedCycleAsync(NavigationRunContext context)
        {
            if (!context.Mounted) await context.StartAsync();
            await NavigationService.ResetAsync();
            await context.NavigateSuccessAsync(context.Platform.Pages.Idle);

            Type[] pages =
            {
                context.Platform.Pages.Transient,
                context.Platform.Pages.Strong,
                context.Platform.Pages.Weak,
                context.Platform.Pages.KeepAttached,
                context.Platform.Pages.ShowImmediately,
                context.Platform.Pages.LoadBefore
            };

            for (int i = 0; i < context.Options.SwitchesPerCycle; i++)
            {
                await context.NavigateSuccessAsync(pages[i % pages.Length]);
                if (i % 32 == 0)
                {
                    NavigationService.Session.SignIn(new[] { "operator" }, new[] { "sell" });
                    await context.NavigateSuccessAsync(context.Platform.Pages.Role);
                    NavigationService.Session.SignOut();
                }
            }

            for (int i = 0; i < 4; i++)
            {
                await context.Platform.ShowDialogAsync(new SurfaceDirective());
                await context.Platform.ShowPromptAsync(new SurfaceDirective());
                await context.Platform.ShowPopoverAsync(new SurfaceDirective());
                context.Platform.ShowToast(new SurfaceDirective(), 25);
                NavigationService.DismissCurrentToast();
            }

            context.Platform.Controls.PulseInteraction();
            await NavigationService.ResetAsync();
            await context.RestartAsync();
            await context.NavigateSuccessAsync(context.Platform.Pages.Idle);
        }

        public static async Task RunResourceAssertionsAsync(NavigationRunContext context)
        {
            await context.Runner.RunAsync("resources", "owned-resources-return-to-zero",
                "reset and shutdown release pages, surfaces, handlers, Inspection providers, timers, and native children",
                async check =>
                {
                    await NavigationService.ResetAsync();
                    context.State.ForceCollection();
                    check.Equal(0, context.State.AttachedPageCount, "attached scenario pages after reset");
                    check.Equal(0, context.State.VisiblePageCount, "visible scenario pages after reset");
                    check.Equal(0, context.State.ActiveBackground, "active scenario background loads after reset");
                    check.Equal(0, context.Platform.Controls.Metrics.ViewsLive, "live scenario surfaces after reset");
                    check.Equal(0, context.Platform.NativeChildCount, "native host children after reset");
                    check.Equal(0, context.Inspection.GetDiagnostics().ActionCount,
                        "Inspection actions during the run");

                    IReadOnlyDictionary<string, object> passiveState =
                        context.Inspection.CaptureState();
                    foreach (string provider in new[]
                    {
                        "Navigation::activeAttempts",
                        "Navigation::queue",
                        "Navigation::cache",
                        "Navigation::backgroundLoads",
                        "Navigation::overlays",
                        "Navigation::idle"
                    })
                    {
                        check.That(passiveState.ContainsKey(provider),
                            "passive Inspection provider missing: " + provider);
                    }

                    IReadOnlyList<ResourceSample> samples = context.Sampler.Taken;
                    ResourceSample? warm = samples.FirstOrDefault(sample => sample.Marker == "post-warm-up");
                    ResourceSample? final = samples.LastOrDefault();
                    if (warm != null && final != null)
                    {
                        check.Note("threads " + warm.ThreadCount + " -> " + final.ThreadCount);
                        check.Note("handles " + warm.HandleCount + " -> " + final.HandleCount);
                        check.Note("private bytes " + warm.PrivateBytes + " -> " + final.PrivateBytes);
                        check.Note("managed heap " + warm.ManagedHeapBytes + " -> " + final.ManagedHeapBytes);
                    }

                    await context.NavigateSuccessAsync(context.Platform.Pages.Idle);
                });
        }

        internal static PageProbe CurrentProbe()
        {
            if (NavigationService.Current is IScenarioProbedPage page) return page.Probe;
            throw new CheckFailure("the current page does not expose its scenario probe");
        }

        internal static async Task WaitUntilAsync(
            Func<bool> condition,
            TimeSpan timeout,
            System.Threading.CancellationToken cancellationToken)
        {
            DateTime deadline = DateTime.UtcNow + timeout;
            while (!condition())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (DateTime.UtcNow >= deadline)
                    throw new CheckFailure("the expected state was not reached within " + timeout);
                await Task.Delay(20, cancellationToken);
            }
        }

        internal static async Task<Exception?> CaptureAsync(Func<Task> action)
        {
            try { await action(); return null; }
            catch (Exception ex) { return ex; }
        }

        private static void ForceCollection(WeakReference weak)
        {
            for (int i = 0; i < 5 && weak.IsAlive; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                System.Threading.Thread.Sleep(20);
            }
        }
    }
}
