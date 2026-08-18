using System;
using System.Threading;
using System.Threading.Tasks;

namespace NekoLib.Pipes
{
    internal static class PipeTaskCancellation
    {
        public static async Task WithCancellation(Task work, CancellationToken cancellationToken)
        {
            if (work == null)
                throw new ArgumentNullException(nameof(work));

            if (!cancellationToken.CanBeCanceled)
            {
                await work.ConfigureAwait(false);
                return;
            }

            var cancelled = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => cancelled.TrySetResult(true)))
            {
                var winner = await Task.WhenAny(work, cancelled.Task).ConfigureAwait(false);
                if (winner != work && !work.IsCompleted)
                {
                    Observe(work);
                    throw new OperationCanceledException(cancellationToken);
                }
            }

            await work.ConfigureAwait(false);
        }

        public static async Task<T> WithCancellation<T>(
            Task<T> work,
            CancellationToken cancellationToken)
        {
            if (work == null)
                throw new ArgumentNullException(nameof(work));

            if (!cancellationToken.CanBeCanceled)
                return await work.ConfigureAwait(false);

            var cancelled = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => cancelled.TrySetResult(true)))
            {
                var winner = await Task.WhenAny(work, cancelled.Task).ConfigureAwait(false);
                if (winner != work && !work.IsCompleted)
                {
                    Observe(work);
                    throw new OperationCanceledException(cancellationToken);
                }
            }

            return await work.ConfigureAwait(false);
        }

        private static void Observe(Task task)
        {
            task.ContinueWith(
                completed => { var _ = completed.Exception; },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }
    }
}
