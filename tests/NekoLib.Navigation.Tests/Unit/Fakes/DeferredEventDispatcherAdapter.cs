using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Navigation.Contracts.Platform;

namespace NekoLib.Navigation.Tests.Unit.Fakes
{
    internal sealed class DeferredEventDispatcherAdapter : IEventDispatcherAdapter
    {
        private readonly Queue<Action> _pending = new Queue<Action>();

        public int PendingCount => _pending.Count;

        public void Invoke(Action action)
            => action?.Invoke();

        public void BeginInvoke(Action action)
        {
            if (action != null)
                _pending.Enqueue(action);
        }

        public void RunNext()
        {
            if (_pending.Count != 0)
                _pending.Dequeue().Invoke();
        }

        public void RunAll()
        {
            while (_pending.Count != 0)
                RunNext();
        }
    }

    internal sealed class BlockingFirstBeginInvokeDispatcher :
        IEventDispatcherAdapter,
        IDisposable
    {
        private readonly ConcurrentQueue<Action> _pending =
            new ConcurrentQueue<Action>();
        private readonly ManualResetEventSlim _releaseFirst =
            new ManualResetEventSlim(false);
        private readonly TaskCompletionSource<bool> _firstEntered =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _firstQueued =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _secondQueued =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        private int _beginInvokeCount;

        public int BeginInvokeCount => Volatile.Read(ref _beginInvokeCount);
        public int PendingCount => _pending.Count;
        public Task FirstBeginInvokeEntered => _firstEntered.Task;
        public Task FirstBeginInvokeQueued => _firstQueued.Task;
        public Task SecondBeginInvokeQueued => _secondQueued.Task;

        public void Invoke(Action action)
            => action?.Invoke();

        public void BeginInvoke(Action action)
        {
            if (action == null)
                return;

            var call = Interlocked.Increment(ref _beginInvokeCount);
            if (call == 1)
            {
                _firstEntered.TrySetResult(true);
                _releaseFirst.Wait();
            }

            _pending.Enqueue(action);
            if (call == 1)
                _firstQueued.TrySetResult(true);
            else if (call == 2)
                _secondQueued.TrySetResult(true);
        }

        public void ReleaseFirst()
            => _releaseFirst.Set();

        public void RunNext()
        {
            if (_pending.TryDequeue(out var action))
                action();
        }

        public void RunAll()
        {
            while (_pending.TryDequeue(out var action))
                action();
        }

        public void Dispose()
        {
            _releaseFirst.Set();
            _releaseFirst.Dispose();
        }
    }
}
