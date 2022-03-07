using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UberQueue.Tests
{
    public class UberQueueTests
    {
        [Fact]
        public async Task ShouldDeque()
        {
            // Arrange
            var fixture = new UberQueue<int>(new[] { new FakeAsyncQueue(1) });

            // Act
            var value = await fixture.DequeueAsync();

            // Assert
            Assert.Equal(1, value);
        }

        [Fact]
        public async Task ShouldDequeTheAvailableOne()
        {
            // Arrange
            var fixture = new UberQueue<int>(new IAsyncQueue<int>[] { new StuckAsyncQueue(), new FakeAsyncQueue(1), new StuckAsyncQueue() });
            using var cancellationTokenSource = new CancellationTokenSource(100);

            // Act
            var value = await fixture.DequeueAsync(cancellationTokenSource.Token);

            // Assert
            Assert.Equal(1, value);
        }

        [Fact]
        public async Task ShouldBeFair()
        {
            // Arrange
            var fixture = new UberQueue<int>(new[] { new FakeAsyncQueue(1), new FakeAsyncQueue(2), new FakeAsyncQueue(3) });
            List<int> values = new();

            // Act
            for (int i = 0; i < 3; i++)
            {
                values.Add(await fixture.DequeueAsync());
            }

            // Assert
            Assert.Equal(new[] { 1, 2, 3 }, values);
        }

        [Fact]
        public async Task ShouldNotBurnValues()
        {
            // Arrange
            var fixture = new UberQueue<int>(new[] { new FakeAsyncQueue(1), new FakeAsyncQueue(2), new FakeAsyncQueue(3) });
            List<int> values = new();

            // Act
            for (int i = 0; i < 6; i++)
            {
                values.Add(await fixture.DequeueAsync());
            }


            // Assert
            values.Sort();
            Assert.Equal(new[] { 1, 2, 3, 10, 20, 30 }, values);
        }

        [Fact]
        public async Task ShouldSkipFailuresButStillBeFair()
        {
            // Arrange
            var fixture = new UberQueue<int>(new IAsyncQueue<int>[] { new RecoveringsyncQueue(1), new FakeAsyncQueue(2), new RecoveringsyncQueue(3) });

            List<int> values = new();

            // Act
            for (int i = 0; i < 5; i++)
            {
                values.Add(await fixture.DequeueAsync());
            }

            // Assert
            values.Sort();
            Assert.Equal(new[] { 1, 2, 3, 20, 200 }, values);
        }

        [Fact]
        public void ConstructorShouldThrowIfEmpty()
        {
            Assert.Throws<ArgumentException>(() => new UberQueue<int>(Array.Empty<FakeAsyncQueue>()));
        }

        [Fact]
        public void ConstructorShouldThrowIfNull()
        {
            Assert.Throws<ArgumentNullException>(() => new UberQueue<int>(null!));
        }

        public class FakeAsyncQueue : IAsyncQueue<int>
        {
            private int _value;

            public FakeAsyncQueue(int value)
            {
                _value = value;
            }

            public Task<int> DequeueAsync(CancellationToken cancellationToken)
            {
                var task = Task.FromResult(_value);
                _value *= 10;
                return task;
            }
        }

        public class StuckAsyncQueue : IAsyncQueue<int>
        {
            public TaskCompletionSource<int> TaskCompletionSource { get; } = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task<int> DequeueAsync(CancellationToken cancellationToken)
            {
                cancellationToken.Register(tcs => ((TaskCompletionSource<int>)tcs!).SetCanceled(), TaskCompletionSource);
                return TaskCompletionSource.Task;
            }
        }

        public class RecoveringsyncQueue : IAsyncQueue<int>
        {
            private Queue<Task<int>> _queue = new();

            public RecoveringsyncQueue(int value)
            {
                _queue.Enqueue(Task.FromCanceled<int>(new CancellationToken(true)));
                _queue.Enqueue(Task.FromException<int>(new Exception()));
                _queue.Enqueue(Task.FromResult(value));
            }

            public Task<int> DequeueAsync(CancellationToken cancellationToken)
            {
                return _queue.Dequeue();
            }
        }
    }
}