using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorBlockJam.Runtime.SceneFlow
{
    /// <summary>
    /// Ends a caller's wait when the caller's token is cancelled while the awaited work keeps running under its own
    /// owner, because the scene that asks for a scene change is usually the one the change unloads (DEC-198, DEC-199).
    /// </summary>
    internal static class CallerWait
    {
        private static readonly Action<object> s_cancel = state => ((TaskCompletionSource<bool>)state).TrySetCanceled();

        /// <summary>Completes, faults or cancels with <paramref name="work"/>, or cancels when the token is cancelled.</summary>
        public static async Task WaitAsync(Task work, CancellationToken cancellationToken)
        {
            if (cancellationToken.CanBeCanceled)
            {
                var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using (cancellationToken.Register(s_cancel, cancelled))
                {
                    if (await Task.WhenAny(work, cancelled.Task) != work)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }
                }
            }

            await work;
        }

        /// <summary>As <see cref="WaitAsync(Task, CancellationToken)"/>, returning the work's result.</summary>
        public static async Task<T> WaitAsync<T>(Task<T> work, CancellationToken cancellationToken)
        {
            await WaitAsync((Task)work, cancellationToken);
            return await work;
        }
    }
}
