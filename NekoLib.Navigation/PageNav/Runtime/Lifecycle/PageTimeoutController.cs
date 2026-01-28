//using NekoLib.Navigation.Contracts.Plataform;
//using NekoLib.Navigation.Contracts.Runtime;
//using NekoLib.Navigation.Metadata;
//using System;
//using System.Threading;

//namespace NekoLib.Navigation.Runtime.Lifecycle
//{
//    /// <summary>
//    /// Context-scoped timeout controller.
//    /// Counts elapsed seconds and raises TimeoutReached when threshold is exceeded.
//    /// </summary>
//    public sealed class PageTimeoutController : IPageTimeoutService
//    {

//        private bool _disposed;

       
//        private readonly int _timeoutSeconds;
//        private int _elapsedSeconds;
//        private int _fired; // 0 = not fired, 1 = fired (interlocked)
//        private bool _running;
//        public TimeoutState State { get; private set; } = TimeoutState.Uninitialized;

//        /// <summary>
//        /// Fired when timeout threshold is reached.
//        /// Guaranteed to fire at most once per cycle.
//        /// </summary>
//        public event Action TimeoutReached;

//        public PageTimeoutController(IPageTimeoutAdapter timer, int timeoutMilliseconds)
//        {
//            if(timer == null) throw new ArgumentNullException(nameof(timer));
//            if(timeoutMilliseconds <= 0)
//                throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds));

//            _timer = timer;
//            _timeoutSeconds = timeoutMilliseconds;
//            _timer.TimeoutElapsed += TimeoutReached;
//        }

//        // --------------------------------------------------------------------
//        // Lifecycle
//        // --------------------------------------------------------------------

//        public void Start()
//        {
//            if(State != TimeoutState.Uninitialized)
//                throw new InvalidOperationException("Timeout already started.");

//            _elapsedSeconds = 0;
//            _fired = 0;

//            State = TimeoutState.Running;

//            // 1-second resolution by design
//            _timer.Start(_timeoutSeconds);
//        }

//        public void Reset()
//        {
//            // Reset is allowed in Running / Paused / Fired
//            _elapsedSeconds = 0;
//            Interlocked.Exchange(ref _fired, 0);

//            if(State == TimeoutState.Fired)
//                State = TimeoutState.Running;
//        }

         

        

//        public void Stop()
//        {
//            if(State == TimeoutState.Stopped)
//                return;

//            _timer.Stop();
//            State = TimeoutState.Stopped;
//        }





//        // --------------------------------------------------------------------
//        // Disposal
//        // --------------------------------------------------------------------
//        public void Dispose()
//        {
//            if(_disposed)
//                return;

//            _disposed = true;
//             _timer.TimeoutElapsed -= TimeoutReached;
//            _timer.Dispose();
//        }

//    }
//}
