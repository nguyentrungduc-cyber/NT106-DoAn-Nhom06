using System.Collections.Concurrent;

namespace SecureChat.Server.Services
{
    public sealed class PresenceTracker
    {
        // userId → set of connectionIds
        private readonly ConcurrentDictionary<string, HashSet<string>> _connections = new();

        public bool IsOnline(string userId)
        {
            if (!_connections.TryGetValue(userId, out var connections))
                return false;

            lock (connections)
            {
                return connections.Count > 0;
            }
        }

        public int ConnectionCount(string userId)
        {
            if (!_connections.TryGetValue(userId, out var connections))
                return 0;

            lock (connections)
            {
                return connections.Count;
            }
        }

        public bool IsFirstConnection(string userId)
        {
            if (!_connections.TryGetValue(userId, out var connections))
                return true;

            lock (connections)
            {
                return connections.Count == 0;
            }
        }

        public void AddConnection(string userId, string connectionId)
        {
            var connections = _connections.GetOrAdd(userId, _ => new HashSet<string>());
            lock (connections)
            {
                connections.Add(connectionId);
            }
        }

        public bool RemoveConnection(string userId, string connectionId)
        {
            if (!_connections.TryGetValue(userId, out var connections))
                return true; // no connections => offline

            bool becameEmpty;
            lock (connections)
            {
                connections.Remove(connectionId);
                becameEmpty = connections.Count == 0;
            }

            // Remove the entry entirely to prevent memory leaks
            if (becameEmpty)
                _connections.TryRemove(userId, out _);

            return becameEmpty;
        }

        public IEnumerable<string> GetAllOnlineUsers()
        {
            foreach (var kvp in _connections)
            {
                bool hasConnections;
                lock (kvp.Value)
                {
                    hasConnections = kvp.Value.Count > 0;
                }
                if (hasConnections)
                    yield return kvp.Key;
            }
        }
    }
}
