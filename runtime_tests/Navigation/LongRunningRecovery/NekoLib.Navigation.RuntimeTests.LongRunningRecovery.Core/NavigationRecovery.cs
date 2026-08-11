#nullable enable
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NekoLib.Navigation;
using NekoLib.RuntimeTests.Harness.Faults;
using NekoLib.RuntimeTests.Harness.Reporting;

namespace NekoLib.Navigation.RuntimeTests.LongRunningRecovery
{
    internal static class NavigationRecovery
    {
        public static async Task RunAsync(
            NavigationRunContext context,
            FaultSchedule schedule,
            long startedTimestamp)
        {
            foreach (FaultEvent planned in schedule.Events)
            {
                await WaitUntilAsync(startedTimestamp, planned.OffsetSeconds, context);
                await context.ExclusiveAsync(async () =>
                {
                    context.Sampler.Take("recovery", "pre-fault");
                    context.Artifacts.Event("fault-dispatch", json =>
                    {
                        json.Prop("eventId", planned.Id);
                        json.Prop("kind", planned.Kind);
                        json.Prop("offsetSeconds", planned.OffsetSeconds);
                    });

                    await DispatchAsync(context, planned);
                    await RecoverAsync(context);

                    context.Artifacts.Event("fault-recovered", json =>
                    {
                        json.Prop("eventId", planned.Id);
                        json.Prop("kind", planned.Kind);
                    });
                    context.Sampler.Take("recovery", "post-recovery");
                });
            }
        }

        private static async Task DispatchAsync(NavigationRunContext context, FaultEvent planned)
        {
            await context.Runner.RunAsync("recovery", "fault-" + planned.Kind,
                "the planned fault produces its documented terminal and recovery remains possible",
                async check =>
                {
                    switch (planned.Kind)
                    {
                        case FaultKinds.RegistryLookup:
                            {
                                Exception? error = await context.ExecuteRequestAsync(
                                    () => NavigationService.SwitchPage(typeof(string)), true);
                                check.That(error is InvalidOperationException,
                                    "registry lookup did not fail with InvalidOperationException");
                                break;
                            }

                        case FaultKinds.PageCreation:
                            await InjectPageFaultAsync(context, check, PageFaultPoint.Creation);
                            break;

                        case FaultKinds.PageLoad:
                            await InjectPageFaultAsync(context, check, PageFaultPoint.Load);
                            break;

                        case FaultKinds.EnterLifecycle:
                            await InjectPageFaultAsync(context, check, PageFaultPoint.Enter);
                            break;

                        case FaultKinds.LeaveLifecycle:
                            {
                                await context.NavigateSuccessAsync(context.Platform.Pages.Fault);
                                using (context.State.Inject(context.Platform.Pages.Fault, PageFaultPoint.Leave))
                                {
                                    Exception? error = await context.NavigateExpectedFailureAsync(
                                        context.Platform.Pages.Idle);
                                    check.That(error is ScenarioInjectedException,
                                        "leave failure did not propagate the scenario exception");
                                    check.Equal(context.Platform.Pages.Fault.FullName,
                                        NavigationService.Current.GetType().FullName,
                                        "current page after leave failure");
                                }
                                break;
                            }

                        case FaultKinds.BackgroundLoad:
                            {
                                context.State.ConfigureLoad(
                                    context.Platform.Pages.Background,
                                    ScenarioLoadBehavior.Fail);
                                long before = context.State.Metrics(context.Platform.Pages.Background).AppliedCount;
                                await context.NavigateSuccessAsync(context.Platform.Pages.Background);
                                await NavigationWorkload.WaitUntilAsync(
                                    () => context.State.ActiveBackground == 0,
                                    TimeSpan.FromSeconds(5), context.Ct);
                                check.Equal(before,
                                    context.State.Metrics(context.Platform.Pages.Background).AppliedCount,
                                    "failed background load apply count");
                                check.That(context.Inspection.GetOperations().Any(operation =>
                                    operation.Module == "Navigation" &&
                                    operation.Operation == "BackgroundLoadFailed"),
                                    "passive Inspection did not record the background failure terminal");
                                context.Counters.ExpectedFailure();
                                break;
                            }

                        case FaultKinds.GuardTimeout:
                            {
                                Type before = NavigationService.Current.GetType();
                                Exception? error = await context.ExecuteRequestAsync(
                                    () => NavigationService.SwitchPage(context.Platform.Pages.GuardTimeout), true);
                                check.That(error == null, "a timed-out guard escaped as an exception");
                                check.Equal(before.FullName, NavigationService.Current.GetType().FullName,
                                    "current page after guard timeout");
                                break;
                            }

                        case FaultKinds.GuardThrow:
                            {
                                Type before = NavigationService.Current.GetType();
                                Exception? error = await context.ExecuteRequestAsync(
                                    () => NavigationService.SwitchPage(context.Platform.Pages.GuardThrow), true);
                                check.That(error == null, "a throwing guard escaped as an exception");
                                check.Equal(before.FullName, NavigationService.Current.GetType().FullName,
                                    "current page after throwing guard");
                                break;
                            }

                        case FaultKinds.RedirectCycle:
                            await AssertRedirectRejectionAsync(context, check, context.Platform.Pages.CycleA, "cycle");
                            break;

                        case FaultKinds.RedirectDepth:
                            await AssertRedirectRejectionAsync(context, check, context.Platform.Pages.DepthStart, "depth");
                            break;

                        case FaultKinds.SurfaceBinding:
                            {
                                Exception? error = await context.Platform.ShowBindingFailureAsync();
                                check.That(error is ScenarioInjectedException,
                                    "surface BindCompletion failure did not propagate");
                                context.Counters.ExpectedFailure();
                                break;
                            }

                        case FaultKinds.SurfaceShow:
                            {
                                Exception? error = await NavigationWorkload.CaptureAsync(() =>
                                    context.Platform.ShowDialogAsync(
                                        new SurfaceDirective { ThrowOnShow = true }));
                                check.That(error is ScenarioInjectedException,
                                    "surface OnShown failure did not propagate");
                                context.Counters.ExpectedFailure();
                                break;
                            }

                        case FaultKinds.SurfaceCleanup:
                            {
                                context.Platform.Controls.FailNextViewRemoval();
                                Exception? error = await NavigationWorkload.CaptureAsync(() =>
                                    context.Platform.ShowDialogAsync(new SurfaceDirective()));
                                check.That(error is ScenarioInjectedException,
                                    "surface cleanup failure did not fault its awaiter");
                                check.Equal(0, context.Platform.Controls.Metrics.ViewsLive,
                                    "native surfaces after cleanup failure");
                                context.Counters.ExpectedFailure();
                                break;
                            }

                        case FaultKinds.DispatcherUnavailable:
                            {
                                context.Platform.Controls.RejectDispatch = true;
                                Exception? error;
                                try
                                {
                                    error = await context.NavigateExpectedFailureAsync(
                                        context.Platform.Pages.Strong);
                                }
                                finally
                                {
                                    context.Platform.Controls.RejectDispatch = false;
                                }
                                check.That(error is InvalidOperationException,
                                    "dispatcher rejection did not surface as InvalidOperationException");
                                check.That(context.Platform.Controls.Metrics.DispatcherRejections > 0,
                                    "the scenario dispatcher did not record a rejection");
                                break;
                            }

                        default:
                            check.That(false, "unknown planned fault kind '" + planned.Kind + "'");
                            break;
                    }
                });
        }

        private static async Task InjectPageFaultAsync(
            NavigationRunContext context,
            Check check,
            PageFaultPoint point)
        {
            using (context.State.Inject(context.Platform.Pages.Fault, point))
            {
                Exception? error = await context.NavigateExpectedFailureAsync(context.Platform.Pages.Fault);
                check.That(error is ScenarioInjectedException,
                    point + " fault did not propagate the scenario exception");
            }
        }

        private static async Task AssertRedirectRejectionAsync(
            NavigationRunContext context,
            Check check,
            Type page,
            string name)
        {
            Type before = NavigationService.Current.GetType();
            Exception? error = await context.ExecuteRequestAsync(
                () => NavigationService.SwitchPage(page), true);
            check.That(error == null, "redirect " + name + " escaped as an exception");
            check.Equal(before.FullName, NavigationService.Current.GetType().FullName,
                "current page after redirect " + name + " rejection");
        }

        private static async Task RecoverAsync(NavigationRunContext context)
        {
            context.State.ClearFaultsAndReleaseLoads();
            context.Platform.Controls.RejectDispatch = false;
            if (!context.Mounted) await context.StartAsync();

            try
            {
                await NavigationService.ResetAsync();
            }
            catch
            {
                await context.ShutdownAsync();
                await context.StartAsync();
            }

            await context.NavigateSuccessAsync(context.Platform.Pages.Idle);
            await context.NavigateSuccessAsync(context.Platform.Pages.Strong);
        }

        private static async Task WaitUntilAsync(
            long startedTimestamp,
            double offsetSeconds,
            NavigationRunContext context)
        {
            while (true)
            {
                context.Ct.ThrowIfCancellationRequested();
                double elapsed = (Stopwatch.GetTimestamp() - startedTimestamp) /
                                 (double)Stopwatch.Frequency;
                double remaining = offsetSeconds - elapsed;
                if (remaining <= 0) return;
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(remaining * 1000.0, 500.0)), context.Ct);
            }
        }
    }
}
