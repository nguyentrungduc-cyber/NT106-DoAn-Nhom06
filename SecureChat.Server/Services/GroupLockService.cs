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
                // Cleanup: if no waiters, remove from dictionary to prevent memory leak
                if (_sem.CurrentCount == 1)
                    _store.TryRemove(_key, out _);
            }
        }
    }
}
