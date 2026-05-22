# Self-Destruct Messages Feature

## 📋 Tổng Quan

Tính năng **Tin nhắn tự hủy (Self-Destruct Messages)** cho phép người dùng gửi tin nhắn có thời gian tồn tại giới hạn. Sau khi hết thời gian, tin nhắn sẽ tự động bị xóa khỏi client local.

## 🎯 Tính Năng

- ⏱️ **Chọn thời gian tự hủy**: 5s, 10s, 30s, 1m, 5m, 1h, 1d hoặc không tự hủy
- 🔔 **Countdown timer**: Hiển thị thời gian còn lại trên chat bubble
- 🗑️ **Tự động xóa**: Tin nhắn tự động biến mất khi hết hạn
- 🔒 **E2EE**: Tin nhắn vẫn được mã hóa end-to-end như bình thường
- 🔄 **Real-time sync**: Expiration tracking hoạt động với SignalR

## 🏗️ Kiến Trúc

### Database Schema
```sql
ALTER TABLE Messages 
ADD COLUMN expires_at DATETIME NULL;
```

### Backend (Server)
- **MessageController.cs**: Tính `ExpiresAt = DateTime.UtcNow + ExpiresAfterSeconds`
- **SendMessageRequest DTO**: Thêm field `ExpiresAfterSeconds`
- **MessageResponse DTO**: Thêm field `ExpiresAt`

### Frontend (Client)
- **MessageExpirationService.cs**: Service quản lý tracking và xóa messages
  - Timer check expired messages mỗi 1 giây
  - Event-driven architecture với `MessageExpired` event
  - Thread-safe với `ConcurrentDictionary`
  
- **frmMainChat.cs**: UI integration
  - Button chọn self-destruct timer
  - Countdown display trên chat bubble
  - Auto-refresh UI mỗi giây để update countdown
  - Track messages khi gửi/nhận/sync

## 📊 Flow Diagram

### Gửi Tin Nhắn Tự Hủy
```
User chọn timer (⏱️ button) 
  → Gửi tin nhắn với ExpiresAfterSeconds
  → Server tính ExpiresAt = Now + Seconds
  → Lưu vào DB với expires_at
  → Trả về MessageResponse với ExpiresAt
  → Client track message trong ExpirationService
  → Hiển thị countdown trên bubble
```

### Nhận Tin Nhắn Tự Hủy
```
SignalR MessageReceived event
  → Kiểm tra ExpiresAt != null
  → Track message trong ExpirationService
  → Hiển thị countdown trên bubble
  → Timer check định kỳ
  → Khi hết hạn: trigger MessageExpired event
  → Xóa message khỏi UI
```

## 🔧 API Changes

### SendMessageRequest
```csharp
public record SendMessageRequest(
    MessageType Type,
    string? Content,
    string? ContentIV,
    string? ReplyToID,
    string? OriginalSenderID,
    List<CreateAttachmentRequest>? Attachments,
    List<string>? MentionedMemberIDs,
    int? ExpiresAfterSeconds = null  // ← NEW
);
```

### MessageResponse
```csharp
public record MessageResponse(
    string MessageID,
    string ConversationID,
    // ... other fields
    DateTime? ExpiresAt,  // ← NEW
    List<AttachmentResponse>? Attachments,
    List<ReactionResponse>? Reactions,
    List<string>? MentionedMemberIDs
);
```

## 🎨 UI Components

### Timer Button (⏱️)
- **Location**: Input bar, bên cạnh attach button
- **States**:
  - Default: ⏱ (blue)
  - Active: ⏱5s, ⏱1m, ⏱1h (orange)
- **Menu Options**:
  - Không tự hủy
  - 5 giây, 10 giây, 30 giây
  - 1 phút, 5 phút
  - 1 giờ, 1 ngày

### Countdown Display
- **Location**: Bottom-left của chat bubble
- **Format**: ⏱5s, ⏱1m, ⏱1h, ⏱1d
- **Color**: Orange (#FF5722)
- **Refresh**: Mỗi 1 giây

## 🧪 Testing Checklist

- [ ] Gửi tin nhắn với timer 5s → Kiểm tra countdown → Xác nhận tự động xóa
- [ ] Gửi tin nhắn với timer 1m → Reload app → Kiểm tra vẫn track được
- [ ] Nhận tin nhắn tự hủy từ người khác → Kiểm tra countdown hiển thị
- [ ] Gửi tin nhắn không tự hủy → Kiểm tra không có countdown
- [ ] Đóng conversation → Mở lại → Kiểm tra countdown vẫn hoạt động
- [ ] Gửi nhiều tin nhắn tự hủy cùng lúc → Kiểm tra tất cả đều track
- [ ] Kiểm tra performance với 100+ messages tracked
- [ ] Kiểm tra thread-safety: gửi tin nhắn trong khi timer đang chạy

## 📝 Code Locations

### Server
- `SecureChat.Server/Controllers/MessageController.cs:50-62` - ExpiresAt calculation
- `SecureChat.Server/Migrations/AddExpiresAtToMessages.sql` - Database migration

### Shared
- `SecureChat.Shared/Models/Messages.cs:43-44` - ExpiresAt field
- `SecureChat.Shared/DTOs/MessageDTOs.cs:14` - ExpiresAfterSeconds parameter
- `SecureChat.Shared/DTOs/MessageDTOs.cs:54` - ExpiresAt in response

### Client
- `SecureChat.Client/Services/MessageExpirationService.cs` - Core service (213 lines)
- `SecureChat.Client/Forms/Chat/frmMainChat.cs:107-112` - Fields & timer
- `SecureChat.Client/Forms/Chat/frmMainChat.cs:1595-1614` - Timer button UI
- `SecureChat.Client/Forms/Chat/frmMainChat.cs:1863-1887` - Helper methods
- `SecureChat.Client/Forms/Chat/frmMainChat.cs:2950-2970` - Countdown display
- `SecureChat.Client/Forms/Chat/frmMainChat.cs:3234-3239` - SignalR tracking
- `SecureChat.Client/Forms/Chat/frmMainChat.cs:2434-2441` - Sync tracking
- `SecureChat.Client/Forms/Chat/frmMainChat.cs:3609-3614` - Send tracking
- `SecureChat.Client/Forms/Chat/frmMainChat.cs:3697-3720` - OnMessageExpired handler

## 🚀 Deployment

### Database Migration
```bash
mysql -u root -p SecureChat < SecureChat.Server/Migrations/AddExpiresAtToMessages.sql
```

### Enable Event Scheduler (Optional - for server-side cleanup)
```sql
SET GLOBAL event_scheduler = ON;
```

## 🔮 Future Enhancements

- [ ] Server-side cleanup: Tự động xóa expired messages khỏi database
- [ ] Custom timer: Cho phép user nhập thời gian tùy chỉnh
- [ ] Notification: Thông báo trước khi message sắp hết hạn
- [ ] Statistics: Tracking số lượng messages tự hủy
- [ ] Batch operations: Xóa nhiều messages cùng lúc khi hết hạn

## 📚 References

- Telegram Secret Chats: https://telegram.org/faq#secret-chats
- Signal Disappearing Messages: https://support.signal.org/hc/en-us/articles/360007320771
- WhatsApp View Once: https://faq.whatsapp.com/general/chats/about-view-once

## 👥 Contributors

- Implementation: OpenCode AI Assistant
- Date: May 22, 2026
- Branch: `feature/self-destruct-messages`
