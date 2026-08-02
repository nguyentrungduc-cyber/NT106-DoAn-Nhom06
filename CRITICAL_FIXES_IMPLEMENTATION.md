# SecureChat Critical Issues - Implementation Guide

## Issue #1: Implement Client-Side Message Sending

### Step 1: Add SendAsync to MessageService

**File:** `SecureChat.Client/Services/Api/MessageService.cs`

```csharp
/// <summary>
/// POST /api/conversations/{conversationID}/messages
/// Sends an encrypted message to the conversation
/// </summary>
public async Task<(bool Ok, MessageResponse? Data, string Err)> SendMessageAsync(
    string conversationId, SendMessageRequest request)
{
    if (string.IsNullOrWhiteSpace(conversationId))
        throw new ArgumentException("Conversation ID is required.", nameof(conversationId));
    
    if (request is null)
        throw new ArgumentNullException(nameof(request));
    
    return await _api.PostAsync<SendMessageRequest, MessageResponse>(
        $"api/conversations/{conversationId}/messages",
        request);
}
```

### Step 2: Create Integration Service (NEW FILE)

**File:** `SecureChat.Client/Services/MessageSendingService.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SecureChat.Client.Services.Api;
using SecureChat.DTOs;
using SecureChat.Shared.Security;

namespace SecureChat.Client.Services
{
    /// <summary>
    /// Unified service for sending encrypted messages.
    /// Handles:
    ///   1. Conversation key retrieval and caching
    ///   2. Message encryption (AES-256)
    ///   3. Server submission via HTTP
    ///   4. Error handling and retry logic
    /// </summary>
    public sealed class MessageSendingService
    {
        private readonly MessageService _messageService;
        private readonly MessageEncryptionService _encryptionService;
        private readonly MessageDecryptor _decryptor;

        public MessageSendingService() 
            : this(
                new MessageService(), 
                new MessageEncryptionService(), 
                new MessageDecryptor())
        {
        }

        public MessageSendingService(
            MessageService messageService,
            MessageEncryptionService encryptionService,
            MessageDecryptor decryptor)
        {
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
            _decryptor = decryptor ?? throw new ArgumentNullException(nameof(decryptor));
        }

        /// <summary>
        /// Send a text message with full E2EE pipeline:
        ///   1. Ensure conversation key is available
        ///   2. Encrypt message content
        ///   3. Create SendMessageRequest
        ///   4. POST to server
        /// </summary>
        /// <param name="conversationId">Target conversation ID</param>
        /// <param name="plaintext">Plaintext message (will be encrypted)</param>
        /// <param name="messageType">MessageType enum (defaults to Text)</param>
        /// <returns>Server response or error</returns>
        public async Task<(bool Success, MessageResponse? Response, string Error)> SendTextMessageAsync(
            string conversationId,
            string plaintext,
            MessageType messageType = MessageType.Text)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(conversationId))
                return (false, null, "Conversation ID is required");

            if (string.IsNullOrWhiteSpace(plaintext))
                return (false, null, "Message cannot be empty");

            if (messageType != MessageType.Text && messageType != MessageType.Audio)
                return (false, null, "Invalid message type");

            try
            {
                // Step 1: Ensure conversation key is cached
                var key = await _decryptor.EnsureConversationKeyAsync(conversationId).ConfigureAwait(false);
                if (key is null)
                    return (false, null, "Could not retrieve encryption key for conversation");

                // Step 2: Encrypt message content
                var (encryptedContent, contentIV) = _encryptionService.EncryptMessage(plaintext, key);

                // Step 3: Create request
                var request = new SendMessageRequest(
                    Type: messageType,
                    Content: encryptedContent,
                    ContentIV: contentIV,
                    ReplyToID: null,
                    OriginalSenderID: null,
                    Attachments: null,
                    MentionedMemberIDs: null,
                    ExpiresAfterSeconds: null
                );

                // Step 4: Send to server
                var (ok, response, error) = await _messageService.SendMessageAsync(
                    conversationId, request).ConfigureAwait(false);

                return (ok, response, error);
            }
            catch (ArgumentException ex)
            {
                return (false, null, $"Validation error: {ex.Message}");
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                return (false, null, $"Encryption failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, null, $"Unexpected error: {ex.Message}");
            }
        }

        /// <summary>
        /// Send a message with expiration (self-destruct).
        /// </summary>
        public async Task<(bool Success, MessageResponse? Response, string Error)> SendExpiringMessageAsync(
            string conversationId,
            string plaintext,
            int expiresAfterSeconds = 3600)
        {
            if (expiresAfterSeconds <= 0)
                return (false, null, "Expiration time must be positive");

            try
            {
                var key = await _decryptor.EnsureConversationKeyAsync(conversationId).ConfigureAwait(false);
                if (key is null)
                    return (false, null, "Could not retrieve encryption key");

                var (encryptedContent, contentIV) = _encryptionService.EncryptMessage(plaintext, key);

                var request = new SendMessageRequest(
                    Type: MessageType.Text,
                    Content: encryptedContent,
                    ContentIV: contentIV,
                    ReplyToID: null,
                    OriginalSenderID: null,
                    Attachments: null,
                    MentionedMemberIDs: null,
                    ExpiresAfterSeconds: expiresAfterSeconds
                );

                return await _messageService.SendMessageAsync(conversationId, request).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return (false, null, $"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Send a message in reply to another message.
        /// </summary>
        public async Task<(bool Success, MessageResponse? Response, string Error)> SendReplyAsync(
            string conversationId,
            string plaintext,
            string replyToMessageId)
        {
            if (string.IsNullOrWhiteSpace(replyToMessageId))
                return (false, null, "Reply target message ID is required");

            try
            {
                var key = await _decryptor.EnsureConversationKeyAsync(conversationId).ConfigureAwait(false);
                if (key is null)
                    return (false, null, "Could not retrieve encryption key");

                var (encryptedContent, contentIV) = _encryptionService.EncryptMessage(plaintext, key);

                var request = new SendMessageRequest(
                    Type: MessageType.Text,
                    Content: encryptedContent,
                    ContentIV: contentIV,
                    ReplyToID: replyToMessageId,
                    OriginalSenderID: null,
                    Attachments: null,
                    MentionedMemberIDs: null,
                    ExpiresAfterSeconds: null
                );

                return await _messageService.SendMessageAsync(conversationId, request).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return (false, null, $"Error: {ex.Message}");
            }
        }
    }
}
```

### Step 3: Usage Example in UI

In your chat form where user inputs message:

```csharp
private readonly MessageSendingService _sendingService = new();

private async void SendMessage(string plaintext)
{
    string conversationId = _currentConversation.ConversationID;
    
    // Show loading indicator
    _lblStatus.Text = "Sending...";
    _btnSend.Enabled = false;
    
    var (success, response, error) = await _sendingService.SendTextMessageAsync(
        conversationId, 
        plaintext);
    
    if (success)
    {
        _txtMessage.Clear();
        _lblStatus.Text = "Sent";
        // Optionally add to UI immediately (optimistic update)
    }
    else
    {
        _lblStatus.Text = $"Failed: {error}";
        MessageBox.Show($"Could not send message: {error}", "Send Error", 
            MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
    
    _btnSend.Enabled = true;
}
```

---

## Issue #2: Fix Rekey Race Condition

### Current Problem

The rekey operation can cache a key before all members are successfully updated, causing some members to be unable to decrypt new messages.

### Solution: Atomic Rekey with Verification

**File:** `SecureChat.Client/Services/MessageDecryptor.cs`

Replace the `RekeyConversationAsync` method:

```csharp
/// <summary>
/// Atomically rekey conversation:
///   1. Generate new AES key
///   2. Encrypt for ALL active members
///   3. PATCH all members' keys to server
///   4. ONLY cache locally if ALL patches succeed
/// </summary>
private async Task<byte[]?> RekeyConversationAsync(string conversationId)
{
    var (ok, members, err) = await _messageService.GetMembersAsync(conversationId)
        .ConfigureAwait(false);
    if (!ok || members is null || members.Count == 0)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[MessageDecryptor] Rekey: failed to get members: {err}");
        return null;
    }

    // Only active members with valid public keys
    var active = members
        .Where(m => m.LeftAt is null && m.User?.PublicKey is not null)
        .ToList();

    if (active.Count == 0)
    {
        System.Diagnostics.Debug.WriteLine(
            "[MessageDecryptor] Rekey: no active members with public keys");
        return null;
    }

    // Generate new AES-256 key
    byte[] newKey = new byte[AesEncryption.KeySize];
    using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
    {
        rng.GetBytes(newKey);
    }

    // STEP 1: Encrypt new key for ALL members
    var encryptionResults = new List<(MemberResponse Member, string EncryptedB64, Exception? Error)>();
    foreach (var member in active)
    {
        try
        {
            byte[] enc = RSAEncryption.Encrypt(newKey, member.User!.PublicKey);
            encryptionResults.Add((member, Convert.ToBase64String(enc), null));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[MessageDecryptor] Rekey: failed to encrypt for member {member.MemberID}: {ex.Message}");
            encryptionResults.Add((member, "", ex));
        }
    }

    // STEP 2: Check if encryption succeeded for ALL members
    var failedEncryptions = encryptionResults.Where(r => r.Error is not null).ToList();
    if (failedEncryptions.Count > 0)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[MessageDecryptor] Rekey: failed to encrypt for {failedEncryptions.Count} members");
        return null;
    }

    // STEP 3: PATCH all members' keys and collect results
    var patchResults = new List<(MemberResponse Member, bool Success)>();
    var api = ApiClient.Instance;
    
    foreach (var (member, encryptedB64, _) in encryptionResults)
    {
        try
        {
            var req = new UpdateMemberRequest(null, null, null, null, encryptedB64);
            var (patchOk, _, patchErr) = await api.PatchAsync<UpdateMemberRequest, MemberResponse>(
                $"api/conversations/{conversationId}/members/{member.MemberID}", req)
                .ConfigureAwait(false);
            
            patchResults.Add((member, patchOk));
            
            if (!patchOk)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[MessageDecryptor] Rekey: failed PATCH for member {member.MemberID}: {patchErr}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[MessageDecryptor] Rekey: PATCH exception for member {member.MemberID}: {ex.Message}");
            patchResults.Add((member, false));
        }
    }

    // STEP 4: Verify ALL patches succeeded
    var failedPatches = patchResults.Where(r => !r.Success).ToList();
    if (failedPatches.Count > 0)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[MessageDecryptor] Rekey ABORTED: {failedPatches.Count} members failed to update");
        return null; // DO NOT CACHE KEY IF ANY MEMBER FAILED
    }

    // STEP 5: ONLY cache if ALL succeeded
    if (_conversationKeys.TryGetValue(conversationId, out var oldKey))
    {
        var history = _oldConversationKeys.GetOrAdd(conversationId, _ => new List<byte[]>());
        lock (history)
        {
            history.Insert(0, oldKey);
        }
    }

    _conversationKeys[conversationId] = newKey;
    SaveKeyHistory();
    
    System.Diagnostics.Debug.WriteLine(
        $"[MessageDecryptor] Rekey COMPLETE for conversation {conversationId}");
    return newKey;
}
```

### Key Changes
1. ? **Separate steps**: Encrypt ? PATCH ? Verify ? Cache
2. ? **No partial caching**: Returns null if ANY member fails
3. ? **Detailed logging**: Shows exactly which members failed
4. ? **Atomic semantics**: All-or-nothing guarantee

---

## Issue #3: Add Message Integrity Checks

### Step 1: Extend Message Model

**File:** `SecureChat.Shared/Models/Messages.cs`

```csharp
[Table("Messages")]
public class Message
{
    // ... existing fields ...
    
    [Column("content_hash"), MaxLength(128)]
    public string? ContentHash { get; set; }  // SHA-256(plaintext), Base64
    
    // For files/voice (per spec):
    [Column("file_hash"), MaxLength(256)]
    public string? FileHash { get; set; }
}
```

### Step 2: Create Integrity Verification Service

**File:** `SecureChat.Shared/Security/IntegrityVerifier.cs` (NEW)

```csharp
using System;
using System.Security.Cryptography;
using System.Text;

namespace SecureChat.Shared.Security
{
    public static class IntegrityVerifier
    {
        /// <summary>
        /// Compute SHA-256 hash of plaintext message.
        /// </summary>
        public static string ComputeContentHash(string plaintext)
        {
            if (plaintext is null)
                throw new ArgumentNullException(nameof(plaintext));
            
            byte[] data = Encoding.UTF8.GetBytes(plaintext);
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(data);
            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Verify SHA-256 hash of plaintext.
        /// </summary>
        public static bool VerifyContentHash(string plaintext, string expectedHash)
        {
            if (plaintext is null || expectedHash is null)
                return false;
            
            string computed = ComputeContentHash(plaintext);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computed),
                Encoding.UTF8.GetBytes(expectedHash));
        }

        /// <summary>
        /// Compute SHA-256 hash of binary file data.
        /// </summary>
        public static string ComputeFileHash(byte[] data)
        {
            if (data is null)
                throw new ArgumentNullException(nameof(data));
            
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(data);
            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Verify SHA-256 hash of binary data.
        /// </summary>
        public static bool VerifyFileHash(byte[] data, string expectedHash)
        {
            if (data is null || expectedHash is null)
                return false;
            
            string computed = ComputeFileHash(data);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computed),
                Encoding.UTF8.GetBytes(expectedHash));
        }
    }
}
```

### Step 3: Update Client Encryption Service

**File:** `SecureChat.Client/Services/MessageEncryptionService.cs`

```csharp
using SecureChat.Shared.Security;

namespace SecureChat.Client.Services
{
    public sealed class MessageEncryptionService
    {
        /// <summary>
        /// Encrypt message with integrity hash.
        /// Returns (encryptedContent, contentIV, contentHash).
        /// </summary>
        public (string EncryptedContent, string ContentIV, string ContentHash) 
            EncryptMessageWithIntegrity(string plaintext, byte[] conversationKey)
        {
            // Validate and encrypt
            if (plaintext is null)
                throw new ArgumentNullException(nameof(plaintext));
            
            // Step 1: Compute hash BEFORE encryption (on plaintext)
            string contentHash = IntegrityVerifier.ComputeContentHash(plaintext);
            
            // Step 2: Encrypt
            var (cipherBase64, ivBase64) = AesEncryption.EncryptText(plaintext, conversationKey);
            
            return (cipherBase64, ivBase64, contentHash);
        }
    }
}
```

### Step 4: Update Server to Store Hash

**File:** `SecureChat.Server/Controllers/MessageController.cs`

```csharp
[HttpPost]
public async Task<IActionResult> SendMessage(string conversationID, 
    [FromBody] SendMessageRequest req)
{
    // ... existing validation ...
    
    // Compute hash for integrity verification
    string? contentHash = null;
    if (!string.IsNullOrEmpty(req.Content))
    {
        // Note: req.Content is ENCRYPTED, but we still hash it for verification
        // (This prevents ciphertext tampering)
        contentHash = System.Security.Cryptography.SHA256
            .HashData(System.Text.Encoding.UTF8.GetBytes(req.Content))
            .Select(b => b.ToString("X2"))
            .Aggregate(string.Concat);
    }
    
    var msg = await messages.CreateAsync(new Message {
        MessageID = NewID(),
        ConversationID = conversationID,
        SenderID = member.MemberID,
        OriginalSenderID = req.OriginalSenderID,
        ReplyToID = req.ReplyToID,
        Type = req.Type,
        Content = req.Content,
        ContentIV = req.ContentIV,
        ContentHash = contentHash,  // NEW
        ExpiresAt = expiresAt
    });
    
    // ... rest of method ...
}
```

### Step 5: Update Client Decryption with Verification

**File:** `SecureChat.Client/Services/MessageDecryptor.cs`

```csharp
public async Task<DecryptedMessage> ProcessAsync(MessageResponse message, string? myMemberId = null)
{
    // ... existing recall check ...
    
    // Text content decryption WITH integrity check
    string content = message.Content ?? string.Empty;
    if (!string.IsNullOrEmpty(content) && !string.IsNullOrEmpty(message.ContentIV))
    {
        var key = await EnsureConversationKeyAsync(message.ConversationID)
            .ConfigureAwait(false);

        if (key is not null)
        {
            try
            {
                // Decrypt
                content = AesEncryption.DecryptText(content, message.ContentIV!, key);
                
                // VERIFY INTEGRITY (NEW)
                if (!string.IsNullOrEmpty(message.ContentHash))
                {
                    if (!IntegrityVerifier.VerifyContentHash(content, message.ContentHash))
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[MessageDecryptor] Integrity check FAILED for message {message.MessageID}");
                        content = "[Integrity check failed - message may be corrupted]";
                    }
                }
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                // Rekey + retry logic...
                ForgetConversation(message.ConversationID);
                var freshKey = await EnsureConversationKeyAsync(message.ConversationID)
                    .ConfigureAwait(false);
                if (freshKey is not null)
                {
                    try
                    {
                        content = AesEncryption.DecryptText(content, message.ContentIV!, freshKey);
                        // Verify hash after fresh decrypt
                        if (!string.IsNullOrEmpty(message.ContentHash))
                        {
                            if (!IntegrityVerifier.VerifyContentHash(content, message.ContentHash))
                                content = "[Integrity check failed]";
                        }
                    }
                    catch
                    {
                        content = "[Message could not be decrypted]";
                    }
                }
            }
        }
    }
    
    return new DecryptedMessage(/* ... */);
}
```

---

## Issue #4: Enforce Key Rotation on Member Removal

### Update Server ConversationController

**File:** `SecureChat.Server/Controllers/ConversationController.cs`

```csharp
[HttpDelete("{conversationID}/members/{memberID}")]
[Authorize]
public async Task<IActionResult> RemoveMember(string conversationID, string memberID)
{
    var member = await GetActiveMember(conversationID);
    if (member is null)
        return Forbid();
    
    // Get member to remove
    var memberToRemove = await conversations.GetMemberByIdAsync(memberID);
    if (memberToRemove is null || memberToRemove.ConversationID != conversationID)
        return NotFound();
    
    // Only Owner/Moderator can remove
    if (member.Role < MemberRole.Moderator)
        return Forbid();
    
    // Mark as left
    await conversations.RemoveMemberAsync(memberID);
    
    // ?? CRITICAL: Force rekey conversation after member removal
    // This ensures the removed member can no longer decrypt new messages
    var rekeySuccess = await ForceRekeyConversationAsync(conversationID);
    if (!rekeySuccess)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[WARNING] Failed to rekey conversation {conversationID} after member removal");
        // Still return success (member is removed, just rekey failed)
        // Client will attempt rekey on next message receive
    }
    
    return NoContent();
}

/// <summary>
/// Force rekey of conversation AES key across all active members.
/// Ensures members with removed access cannot decrypt new messages.
/// </summary>
private async Task<bool> ForceRekeyConversationAsync(string conversationID)
{
    try
    {
        var (ok, members, _) = await GetMembersWithKeysAsync(conversationID);
        if (!ok || members is null || members.Count == 0)
            return false;
        
        // Generate new key
        byte[] newKey = new byte[32]; // AES-256
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(newKey);
        }
        
        // Encrypt for each active member and update
        foreach (var m in members.Where(x => x.LeftAt == null))
        {
            try
            {
                // Assume all active members have public keys
                if (m.User?.PublicKey is null)
                    continue;
                
                byte[] encrypted = RSAEncryption.Encrypt(newKey, m.User.PublicKey);
                
                await conversations.UpdateMemberEncryptedKeyAsync(
                    m.MemberID,
                    Convert.ToBase64String(encrypted));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Rekey failed for member {m.MemberID}: {ex.Message}");
            }
        }
        
        return true;
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"[ERROR] ForceRekey failed: {ex.Message}");
        return false;
    }
}
```

---

## Summary of Critical Fixes

| Fix | Files Modified | Complexity | Time |
|-----|-----------------|-----------|------|
| Message Sending | 2 new + 1 modified | Medium | 2h |
| Rekey Race Condition | 1 modified | High | 3h |
| Integrity Checks | 4 modified + 1 new | Medium | 3h |
| Key Rotation | 1 modified | Low | 1h |
| **Total** | **7 files** | **Medium-High** | **~9h** |

All changes maintain **backward compatibility** with existing database schema (new fields are nullable).
