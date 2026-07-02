using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace SecureChat.Client.Services
{
    /// <summary>
    /// Service quản lý việc tự động xóa tin nhắn đã hết hạn (self-destruct messages).
    /// Sử dụng Timer để kiểm tra và xóa messages local khi ExpiresAt đã qua.
    /// </summary>
    public sealed class MessageExpirationService : IDisposable
    {
        // Dictionary lưu trữ messages cần track: MessageID -> (ExpiresAt, OriginalDurationSeconds)
        private readonly ConcurrentDictionary<string, (DateTime ExpiresAt, int DurationSeconds)> _trackedMessages = new();

        // Timer để check expired messages định kỳ
        private System.Threading.Timer? _checkTimer;

        // Callback khi message hết hạn
        public event Action<string>? MessageExpired;

        // Lock để đảm bảo thread-safe khi start/stop
        private readonly object _lock = new();
        private bool _isRunning = false;
        private bool _disposed = false;

        // Interval để check expired messages (mặc định: 1 giây)
        private readonly int _checkIntervalMs;

        public MessageExpirationService(int checkIntervalMs = 1000)
        {
            _checkIntervalMs = checkIntervalMs;
        }

        /// <summary>
        /// Bắt đầu service và timer để check expired messages.
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(MessageExpirationService));

                if (_isRunning)
                    return;

                _checkTimer = new System.Threading.Timer(CheckExpiredMessages, null, _checkIntervalMs, _checkIntervalMs);
                _isRunning = true;
            }
        }

        /// <summary>
        /// Dừng service và timer.
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                if (!_isRunning)
                    return;

                _checkTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                _checkTimer?.Dispose();
                _checkTimer = null;
                _isRunning = false;
            }
        }

        /// <summary>
        /// Track một message để tự động xóa khi hết hạn.
        /// </summary>
        /// <param name="messageId">ID của message</param>
        /// <param name="expiresAt">Thời điểm message sẽ hết hạn (UTC)</param>
        /// <param name="durationSeconds">Số giây gốc user chọn (để hiển thị countdown chính xác)</param>
        public void TrackMessage(string messageId, DateTime expiresAt, int durationSeconds = 0)
        {
            if (string.IsNullOrWhiteSpace(messageId))
                throw new ArgumentException("Message ID cannot be null or empty.", nameof(messageId));

            if (expiresAt == DateTime.MinValue)
                throw new ArgumentException("ExpiresAt cannot be MinValue.", nameof(expiresAt));

            // Chỉ track nếu message chưa hết hạn
            if (expiresAt > DateTime.UtcNow)
            {
                _trackedMessages[messageId] = (expiresAt, durationSeconds > 0 ? durationSeconds : (int)(expiresAt - DateTime.UtcNow).TotalSeconds);
            }
        }

        /// <summary>
        /// Ngừng track một message (khi message bị xóa thủ công hoặc conversation đóng).
        /// </summary>
        /// <param name="messageId">ID của message</param>
        public void UntrackMessage(string messageId)
        {
            if (string.IsNullOrWhiteSpace(messageId))
                return;

            _trackedMessages.TryRemove(messageId, out _);
        }

        /// <summary>
        /// Xóa tất cả tracked messages của một conversation.
        /// </summary>
        /// <param name="conversationId">ID của conversation</param>
        public void UntrackConversation(string conversationId)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                return;

            // Remove all messages that belong to this conversation
            // Note: MessageID format might include conversationId, but we'll remove all for safety
            var keysToRemove = new System.Collections.Generic.List<string>();
            foreach (var key in _trackedMessages.Keys)
            {
                // If your MessageID contains conversationId, you can filter here
                // For now, we'll provide a method to clear all
                keysToRemove.Add(key);
            }

            foreach (var key in keysToRemove)
            {
                _trackedMessages.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// Xóa tất cả tracked messages.
        /// </summary>
        public void ClearAll()
        {
            _trackedMessages.Clear();
        }

        /// <summary>
        /// Lấy số lượng messages đang được track.
        /// </summary>
        public int TrackedMessageCount => _trackedMessages.Count;

        /// <summary>
        /// Kiểm tra xem message có đang được track không.
        /// </summary>
        public bool IsTracking(string messageId)
        {
            return _trackedMessages.ContainsKey(messageId);
        }

        /// <summary>
        /// Lấy thời gian còn lại trước khi message hết hạn (seconds).
        /// Trả về null nếu message không được track hoặc đã hết hạn.
        /// Giá trị trả về không bao giờ vượt quá durationSeconds gốc,
        /// tránh hiển thị countdown thấp hơn thực tế do round-trip latency.
        /// </summary>
        public int? GetRemainingSeconds(string messageId)
        {
            if (!_trackedMessages.TryGetValue(messageId, out var entry))
                return null;

            var remaining = (entry.ExpiresAt - DateTime.UtcNow).TotalSeconds;
            if (remaining <= 0) return 0;
            // Cap display at the original selected duration so user sees
            // e.g. "10" not "7" when 3s were lost to network round-trip
            int displaySec = (int)Math.Ceiling(remaining);
            if (entry.DurationSeconds > 0 && displaySec > entry.DurationSeconds)
                displaySec = entry.DurationSeconds;
            return displaySec;
        }

        /// <summary>
        /// Timer callback để check và xóa expired messages.
        /// </summary>
        private void CheckExpiredMessages(object? state)
        {
            if (_disposed)
                return;

            var now = DateTime.UtcNow;
            var expiredMessages = new System.Collections.Generic.List<string>();

            // Tìm tất cả messages đã hết hạn
            foreach (var kvp in _trackedMessages)
            {
                if (kvp.Value.ExpiresAt <= now)
                {
                    expiredMessages.Add(kvp.Key);
                }
            }

            // Xóa và trigger event cho mỗi expired message
            foreach (var messageId in expiredMessages)
            {
                if (_trackedMessages.TryRemove(messageId, out _))
                {
                    try
                    {
                        // Trigger event trên thread pool để không block timer
                        Task.Run(() => MessageExpired?.Invoke(messageId));
                    }
                    catch
                    {
                        // Ignore exceptions from event handlers
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            lock (_lock)
            {
                _disposed = true;
                Stop();
                _trackedMessages.Clear();
            }
        }
    }
}
