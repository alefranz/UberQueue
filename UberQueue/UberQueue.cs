using System.Diagnostics.CodeAnalysis;

namespace UberQueue
{
    /// <summary>
    /// Assumptions:
    /// - Single consumer (not threadsafe)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class UberQueue<T>
    {
        private readonly IAsyncQueue<T>[] _queues;
        private readonly Task<T>?[] _tasks;

        public UberQueue(IAsyncQueue<T>[] queues)
        {
            _queues = queues ?? throw new ArgumentNullException(nameof(queues));
            if (_queues.Length == 0) throw new ArgumentException("cannot be empty", nameof(queues));

            _tasks = new Task<T>[queues.Length];
        }

        public async Task<T> DequeueAsync(CancellationToken cancellationToken = default)
        {
            if (TryTakeFirst(out Task<T>? task)) return await task;

            for (var i = 0; i < _queues.Length; i++)
            {
                if (_tasks[i] != null) continue;
                _tasks[i] = _queues[i].DequeueAsync(cancellationToken);
            }
            
            await Task.WhenAny(_tasks!);

            if (TryTakeFirst(out task)) return await task;

            throw new InvalidOperationException();
        }

        private bool TryTakeFirst([NotNullWhen(true)] out Task<T>? result)
        {
            for (var i = 0; i < _queues.Length; i++)
            {
                var task = _tasks[i];
                if (task == null || !task.IsCompleted) continue;

                _tasks[i] = null;

                if (!task.IsCompletedSuccessfully) continue;
                
                result = task;
                return true;
            }
            result = null;
            return false;
        }
    }
}