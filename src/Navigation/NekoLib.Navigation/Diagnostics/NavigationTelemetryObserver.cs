using NekoLib.Core.Telemetry;
using NekoLib.Navigation.Telemetry;
using System;
using System.Collections.Generic;

namespace NekoLib.Navigation.Diagnostics
{
    internal sealed class NavigationTelemetryObserver : IDisposable
    {
        private readonly object _gate = new object();
        private readonly NavigationEventHub _hub;
        private readonly ITelemetry _telemetry;
        private readonly Dictionary<string, RequestOperation> _requests =
            new Dictionary<string, RequestOperation>(StringComparer.Ordinal);
        private bool _disposed;

        private NavigationTelemetryObserver(NavigationEventHub hub, ITelemetry telemetry)
        {
            _hub = hub;
            _telemetry = telemetry;
            _hub.NavigationStarted += OnNavigationStarted;
            _hub.NavigationTrace += OnTrace;
        }

        public static IDisposable Attach(NavigationEventHub hub, ITelemetry telemetry)
        {
            if (hub == null)
                throw new ArgumentNullException(nameof(hub));
            if (telemetry == null)
                throw new ArgumentNullException(nameof(telemetry));
            if (ReferenceEquals(telemetry, NullTelemetry.Instance))
                return NekoLib.Core.Disposable.Empty;

            return new NavigationTelemetryObserver(hub, telemetry);
        }

        private void OnTrace(NavigationTraceEvent trace)
        {
            if (trace == null || string.IsNullOrEmpty(trace.RequestId))
                return;

            if (trace.Kind == NavigationTraceKind.RequestStarted)
            {
                Start(trace);
                return;
            }

            if (trace.Kind == NavigationTraceKind.RequestCompleted)
                Complete(trace);
        }

        private void Start(NavigationTraceEvent trace)
        {
            ITelemetryOperation operation;
            try
            {
                operation = _telemetry.StartOperation(
                    "Navigation",
                    "page_switch",
                    trace.RequestId,
                    dimensions: new Dictionary<string, object>
                    {
                        ["runtime_id"] = trace.RuntimeId,
                        ["from_page"] = trace.FromPage ?? string.Empty,
                        ["target_page"] = trace.TargetPage ?? string.Empty,
                        ["trigger"] = trace.Trigger.ToString(),
                        ["requested_load_mode"] = trace.RequestedLoadMode ?? string.Empty,
                        ["is_back_navigation"] = trace.IsBackNavigation
                    });
                operation.Checkpoint("page_switch_started");
            }
            catch
            {
                return;
            }

            lock (_gate)
            {
                if (_disposed || _requests.ContainsKey(trace.RequestId!))
                {
                    try { operation.Complete(TelemetryOutcome.Cancelled); }
                    catch { }
                    return;
                }

                _requests.Add(trace.RequestId!, new RequestOperation(operation));
            }
        }

        private void OnNavigationStarted(NavigationStartedEvent started)
        {
            if (started == null || string.IsNullOrEmpty(started.RequestId))
                return;

            RequestOperation? request;
            lock (_gate)
                _requests.TryGetValue(started.RequestId!, out request);

            var timing = started.Args?.Timing;
            if (request == null || timing == null)
                return;

            request.Timing = timing;
            timing.Bind(request.Operation);
        }

        private void Complete(NavigationTraceEvent trace)
        {
            RequestOperation? request;
            lock (_gate)
            {
                if (!_requests.TryGetValue(trace.RequestId!, out request))
                    return;
                _requests.Remove(trace.RequestId!);
            }

            try
            {
                var measurements = new Dictionary<string, double>
                {
                    ["page_switch.total_ms"] = trace.ElapsedMilliseconds
                };

                var authenticationElapsed = request.Timing?.AuthenticationElapsed;
                if (authenticationElapsed.HasValue)
                {
                    var authenticationMilliseconds = authenticationElapsed.Value.TotalMilliseconds;
                    measurements["page_switch.time_to_authenticated_ms"] = authenticationMilliseconds;
                    measurements["page_switch.post_auth_to_ready_ms"] =
                        Math.Max(0, trace.ElapsedMilliseconds - authenticationMilliseconds);
                }

                if (trace.Outcome == NavigationTraceOutcome.Succeeded)
                    request.Operation.Checkpoint("page_ready");

                request.Operation.Complete(
                    MapOutcome(trace.Outcome),
                    new Dictionary<string, object>
                    {
                        ["outcome"] = trace.Outcome.ToString(),
                        ["target_page"] = trace.TargetPage ?? string.Empty,
                        ["effective_load_mode"] = trace.EffectiveLoadMode ?? string.Empty,
                        ["decision"] = trace.Decision ?? string.Empty,
                        ["failure_kind"] = trace.FailureKind ?? string.Empty,
                        ["error_type"] = trace.ErrorType ?? string.Empty,
                        ["page_ready_semantics"] = "synchronous_navigation_lifecycle_completed"
                    },
                    measurements);
            }
            catch
            {
                // Optional telemetry must never alter navigation control flow.
            }
        }

        private static TelemetryOutcome MapOutcome(NavigationTraceOutcome outcome)
        {
            switch (outcome)
            {
                case NavigationTraceOutcome.Succeeded:
                case NavigationTraceOutcome.Redirected:
                case NavigationTraceOutcome.NoHistory:
                    return TelemetryOutcome.Succeeded;
                case NavigationTraceOutcome.Denied:
                case NavigationTraceOutcome.Discarded:
                    return TelemetryOutcome.Cancelled;
                case NavigationTraceOutcome.Failed:
                    return TelemetryOutcome.Failed;
                default:
                    return TelemetryOutcome.Unknown;
            }
        }

        public void Dispose()
        {
            RequestOperation[] active;
            lock (_gate)
            {
                if (_disposed)
                    return;

                _disposed = true;
                active = new List<RequestOperation>(_requests.Values).ToArray();
                _requests.Clear();
            }

            _hub.NavigationStarted -= OnNavigationStarted;
            _hub.NavigationTrace -= OnTrace;

            for (int i = 0; i < active.Length; i++)
            {
                try { active[i].Operation.Complete(TelemetryOutcome.Cancelled); }
                catch { }
            }
        }

        private sealed class RequestOperation
        {
            public RequestOperation(ITelemetryOperation operation)
            {
                Operation = operation;
            }

            public ITelemetryOperation Operation { get; }
            public NavigationTimingContext? Timing { get; set; }
        }
    }
}
