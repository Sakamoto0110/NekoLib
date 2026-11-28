using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Navigation.Runtime.Core;

namespace NekoLib.Navigation.Bootstrap
{
    /// <summary>Schedules the absolute deadline of the current authenticated session.</summary>
    internal sealed class SessionTimeoutLifetime : IDisposable
    {
        private readonly NavigationContext _context;
        private readonly double _timeoutMilliseconds;
        private readonly Func<long> _getTimestamp;
        private readonly Func<int, CancellationToken, Task> _delay;
        private readonly object _sync = new object();
        private CancellationTokenSource? _pending;
        private int? _scheduledGeneration;
        private bool _disposed;

        internal SessionTimeoutLifetime(
            NavigationContext context,
            TimeSpan timeout,
            Func<long>? getTimestamp = null,
            Func<int, CancellationToken, Task>? delay = null)
        {
            _context = context;
            _timeoutMilliseconds = timeout.TotalMilliseconds;
            _getTimestamp = getTimestamp ?? Stopwatch.GetTimestamp;
            _delay = delay ?? Task.Delay;
            _context.Session.Changed += OnSessionChanged;
            OnSessionChanged();
        }

        private void OnSessionChanged()
        {
            CancellationTokenSource pending;
            (int Generation, long SignedInTimestamp) session;

            lock (_sync)
            {
                if (_disposed)
                    return;

                var latest = _context.Session.GetAuthenticatedLifetime();
                if (latest?.Generation == _scheduledGeneration)
                    return;

                _scheduledGeneration = latest?.Generation;
                _pending?.Cancel();
                _pending = null;
                if (!latest.HasValue)
                    return;

                session = latest.Value;
                pending = new CancellationTokenSource();
                _pending = pending;
            }

            _ = ExpireAsync(session.Generation, session.SignedInTimestamp, pending);
        }

        private async Task ExpireAsync(int generation, long signedInTimestamp, CancellationTokenSource pending)
        {
            var cancellationToken = pending.Token;
            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var elapsedMilliseconds = (_getTimestamp() - signedInTimestamp) * 1000d / Stopwatch.Frequency;
                    var remainingMilliseconds = _timeoutMilliseconds - elapsedMilliseconds;
                    if (remainingMilliseconds <= 0)
                        break;

                    // Task.Delay on .NET Framework accepts at most Int32.MaxValue ms.
                    var delayMilliseconds = (int)Math.Min(int.MaxValue, Math.Ceiling(remainingMilliseconds));
                    // Preserve the caller's UI context when sign-in happens on the
                    // owner thread, including before a native host handle exists.
                    await _delay(delayMilliseconds, cancellationToken);
                }

                if (NavigationService.IsCurrentContext(_context))
                    await NavigationService.ExpireIdleAsync(_context, () => TryExpire(generation, pending)).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken) { }
            catch (Exception ex)
            {
                if (NavigationService.IsCurrentContext(_context))
                    Debug.WriteLine("[PageNavBootstrap] Session timeout navigation failed: " + ex);
            }
            finally
            {
                lock (_sync)
                {
                    if (ReferenceEquals(_pending, pending))
                        _pending = null;
                    pending.Dispose();
                }
            }
        }

        private bool TryExpire(int generation, CancellationTokenSource pending)
        {
            lock (_sync)
            {
                if (_disposed || !ReferenceEquals(_pending, pending) || !NavigationService.IsCurrentContext(_context))
                    return false;

                // Admission is serialized with navigation. The atomic generation
                // check also protects sign-ins from worker threads.
                return _context.Session.TrySignOut(generation) &&
                    !_disposed && !_context.Session.IsAuthenticated && NavigationService.IsCurrentContext(_context);
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                    return;

                _disposed = true;
                _context.Session.Changed -= OnSessionChanged;
                _pending?.Cancel();
                _pending = null;
            }
        }
    }
}
