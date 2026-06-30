using System.Collections.Concurrent;

namespace SecureChat.Server.Services
{
    public sealed class GroupLockService
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

        public async Task<IDisposable> AcquireAsync(string conversationId, CancellationToken ct = default)
        {
            var sem = _locks.GetOrAdd(conversationId, _ => new SemaphoreSlim(1, 1));
            await sem.WaitAsync(ct).ConfigureAwait(false);
            return new Releaser(conversationId, sem, _locks);
        }

        private sealed class Releaser : IDisposable
        {
            private readonly string _key;
            private readonly SemaphoreSlim _sem;
            private readonly ConcurrentDictionary<string, SemaphoreSlim> _store;
            private bool _disposed;

            public Releaser(string key, SemaphoreSlim sem, ConcurrentDictionary<string, SemaphoreSlim> store)
            {
                _key = key;
                _sem = sem;
                _store = store;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _sem.Release();
                // Intentionally NOT removing from _store: removing the semaphore while
                // another thread may have already retrieved it via GetOrAdd creates a
                // TOCTOU race that lets two threads enter the critical section
                // simultaneously. SemaphoreSlim is ~40 bytes; the dictionary is bounded
                // by conversation count, not by lock acquisitions.
            }
        }
    }
}
