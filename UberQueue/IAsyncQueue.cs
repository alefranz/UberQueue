namespace UberQueue
{
    public interface IAsyncQueue<T>
    {
        public Task<T> DequeueAsync(CancellationToken cancellationToken);
    }
}