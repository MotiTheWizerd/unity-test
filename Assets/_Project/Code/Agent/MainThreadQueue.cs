using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Forge.Agent
{
    /// <summary>
    /// Carries work to the thread that pumps the queue: the editor's main thread, which owns the edit session and
    /// the scene. Any thread may queue work; <see cref="Pump"/> runs it, once per frame.
    /// </summary>
    public sealed class MainThreadQueue
    {
        readonly ConcurrentQueue<Action> _work = new ConcurrentQueue<Action>();

        /// <summary>Queues <paramref name="work"/>; the task completes with its result once it has run.</summary>
        public Task<T> Run<T>(Func<T> work)
        {
            if (work == null)
                throw new ArgumentNullException(nameof(work));

            // Whoever awaits this waits on another thread; their continuation must not run on the pumping thread.
            var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            _work.Enqueue(() =>
            {
                try
                {
                    completion.SetResult(work());
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            });
            return completion.Task;
        }

        /// <summary>Queues <paramref name="work"/> without waiting for it.</summary>
        public void Post(Action work)
        {
            if (work == null)
                throw new ArgumentNullException(nameof(work));
            _work.Enqueue(work);
        }

        /// <summary>Runs the work queued so far; work queued meanwhile waits for the next pump.</summary>
        public void Pump()
        {
            var count = _work.Count;
            for (var i = 0; i < count && _work.TryDequeue(out var work); i++)
                work();
        }
    }
}
