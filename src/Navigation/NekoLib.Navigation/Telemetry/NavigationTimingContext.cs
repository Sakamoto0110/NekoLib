using NekoLib.Core.Telemetry;
using System;

namespace NekoLib.Navigation.Telemetry
{
    /// <summary>
    /// Correlates application-owned milestones with one page-switch operation.
    /// Custom guards can call <see cref="AuthenticationCompleted"/> after their
    /// authentication work; Navigation still owns the request start and page-ready
    /// terminal boundaries.
    /// </summary>
    public sealed class NavigationTimingContext
    {
        private readonly object _gate = new object();
        private ITelemetryOperation? _operation;
        private bool _authenticationPending;
        private TimeSpan? _authenticationElapsed;

        public void AuthenticationCompleted()
        {
            ITelemetryOperation? operation;
            lock (_gate)
            {
                if (_authenticationElapsed.HasValue || _authenticationPending)
                    return;

                operation = _operation;
                if (operation == null)
                {
                    _authenticationPending = true;
                    return;
                }
            }

            CaptureAuthenticationCheckpoint(operation);
        }

        internal void Bind(ITelemetryOperation operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            bool capturePending;
            lock (_gate)
            {
                if (_operation != null)
                    return;

                _operation = operation;
                capturePending = _authenticationPending;
                _authenticationPending = false;
            }

            if (capturePending)
                CaptureAuthenticationCheckpoint(operation);
        }

        internal TimeSpan? AuthenticationElapsed
        {
            get { lock (_gate) return _authenticationElapsed; }
        }

        private void CaptureAuthenticationCheckpoint(ITelemetryOperation operation)
        {
            try
            {
                var elapsed = operation.Checkpoint("authentication_completed");
                lock (_gate)
                {
                    if (!_authenticationElapsed.HasValue)
                        _authenticationElapsed = elapsed;
                }
            }
            catch
            {
                // Optional telemetry must never alter authentication or navigation.
            }
        }
    }
}
