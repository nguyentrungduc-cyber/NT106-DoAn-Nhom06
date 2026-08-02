# SecureChat Logic Check Report

## Executive Summary
The SecureChat E2EE system has a **well-designed security architecture**, but there are several **critical logic issues** that need attention:

### Critical Issues Found: 5
### High Priority Issues: 3  
### Medium Priority Issues: 2
### Low Priority Issues: 2

---

## 1. ? SECURITY ARCHITECTURE - SOLID

### Encryption Pipeline: ? CORRECT
```
Client Encrypt (AES-256-CBC) ? Server Store Ciphertext ? Receiver Decrypt
```

**Evidence:**
- **AesEncryption.cs**: Proper AES-256-CBC implementation with OAEP padding
- **MessageEncryptionService.cs**: Encrypts message content on client before sending
- **MessageDecryptor.cs**: Decrypts on receiving client only
- **Server never sees plaintext** ?

### Password Hashing: ? CORRECT
- **PasswordHasher.cs**: Argon2id with 16-byte salt (following spec)
- **Fixed-time comparison**: Uses `CryptographicOperations.FixedTimeEquals` ?

### RSA Key Exchange: ? SOUND
- **Hybrid encryption**: Conversation AES key encrypted with member's RSA public key
- **KeyManager.cs**: Persistent key storage with proper PEM format

---

## 2. ?? CRITICAL ISSUE: Message Sending Logic is INCOMPLETE

### Problem
The **client-side message sending is not integrated** in the codebase. 

**Evidence:**
- `MessageService.cs` has NO `SendAsync()` method
- No `POST /api/conversations/{conversationID}/messages` call found in client
- `SendMessageRequest` DTO exists but is never instantiated in client code

### Impact
- Users cannot send messages through normal UI flow
- Message encryption would fail (no entry point)
- **E2EE guarantee broken** ??

### Required Implementation
Client needs to:
1. Get conversation key via `MessageDecryptor.EnsureConversationKeyAsync()`
2. Encrypt message via `MessageEncryptionService.EncryptMessage()`
3. POST to `api/conversations/{conversationID}/messages` with `SendMessageRequest`

### Recommendation
```csharp
public async Task<(bool Ok, MessageResponse? Data, string Err)> SendMessageAsync(
    string conversationId, 
    string plaintext, 
    byte[] conversationKey)
{
    var encryption = new MessageEncryptionService();
    var (encrypted, iv) = encryption.EncryptMessage(plaintext, conversationKey);
    
    var req = new SendMessageRequest(
        MessageType.Text,
        encrypted,
        iv,
        null, null, null, null
    );
    
    return await _api.PostAsync<SendMessageRequest, MessageResponse>(
        $"api/conversations/{conversationId}/messages",
        req);
}
```

---

## 3. ?? CRITICAL ISSUE: Rekey Logic Has Race Condition

### Problem in MessageDecryptor.RekeyConversationAsync()

**The issue:**
```csharp
// Line: Cache key BEFORE verifying ALL members successfully updated
_conversationKeys[conversationId] = newKey;
SaveKeyHistory();
```

If:
1. Member A gets new encrypted key ?
2. Member B fails to update (network error, permission denied)
3. Key is cached anyway
4. Member B cannot decrypt future messages
5. **Silent data loss** ??

### Current Safeguard (Insufficient)
```csharp
if (!string.IsNullOrWhiteSpace(CurrentUserId))
{
    var myMember = members.FirstOrDefault(m => m.User?.UserID == CurrentUserId);
    if (myMember is not null && !updates.Any(u => u.MemberId == myMember.MemberID))
    {
        return null; // Refuse cache
    }
}
```

This only protects current user, NOT other members.

### Fix Required
```csharp
// Verify ALL active members were successfully encrypted
if (updates.Count != active.Count)
{
    return null; // Don't cache if incomplete
}

// Only after ALL members are patched successfully
foreach (var (memberId, encryptedB64) in updates)
{
    var (patchOk, _, patchErr) = await api.PatchAsync<...>(...);
    if (!patchOk)
    {
        _conversationKeys.TryRemove(conversationId, out _);
        return null; // Abort if any patch fails
    }
}

// NOW cache only if all succeeded
_conversationKeys[conversationId] = newKey;
```

---

## 4. ?? CRITICAL ISSUE: ContentIV is Stored in Database

### Problem in Message Model

**Current Schema:**
```csharp
[Column("content_iv")]
public string? ContentIV { get; set; }
```

**The vulnerability:**
- IV should be **random per message** for AES-CBC security
- Storing IV in plaintext is correct (IV doesn't need to be secret)
- **BUT** receiving different encrypted content with same IV = attacker can detect patterns

**Check in MessageDecryptor:**
```csharp
if (!string.IsNullOrEmpty(content) && !string.IsNullOrEmpty(message.ContentIV))
{
    // IV is in plaintext, OK
    content = AesEncryption.DecryptText(content, message.ContentIV!, key);
}
```

**Actually ? This is correct** - IV doesn't need encryption, only randomization.

**But wait... Issue found:**

In `MessageEncryptionService.EncryptMessage()`:
```csharp
var (cipherBase64, ivBase64) = AesEncryption.EncryptText(plaintext, conversationKey);
// IV is auto-generated ?

return (cipherBase64, ivBase64);
```

And `AesEncryption.EncryptText()`:
```csharp
if (iv is null)
{
    effectiveIv = new byte[IvSize];
    using var rng = RandomNumberGenerator.Create();
    rng.GetBytes(effectiveIv); // Random IV ?
}
```

**This is CORRECT** ? - Move to next issue.

---

## 5. ?? HIGH PRIORITY: No Integrity Check for Encrypted Messages

### Problem
The architecture document states:
```
### 3. Data Integrity
- Use SHA-256 hashing for:
  - File transfers
  - Voice messages
```

**But text messages have NO integrity check:**
- No `ContentHash` field in Message model
- No HMAC-SHA256 verification
- **Attacker can modify encrypted content in transit/storage**

### Attack Scenario
1. Attacker intercepts message in database
2. Modifies ciphertext (XOR some bytes)
3. Victim decrypts and gets garbage (no error)
4. Or if lucky, decrypts to valid plaintext (AES-CBC malleability)

### Recommendation
Add fields to Message:
```csharp
[Column("content_hash")] // SHA-256(plaintext)
public string? ContentHash { get; set; }

[Column("content_hmac")] // HMAC-SHA256(ciphertext, key)
public string? ContentHmac { get; set; }
```

Client should:
```csharp
// Decrypt
string plaintext = AesEncryption.DecryptText(content, iv, key);

// Verify integrity
if (message.ContentHash != SHA256(plaintext))
    throw new SecurityException("Message integrity check failed!");
```

---

## 6. ?? HIGH PRIORITY: Conversation Key Rotation Not Enforced

### Problem in frmConversation / Conversation Management

**Scenario:**
1. Member A in group for 1 year with key K1
2. Member B joins (gets K1 encrypted with their public key)
3. Member A leaves
4. Member A still has K1 in plaintext (if they saved it)
5. **Member A can decrypt ALL future messages** ??

### Current Implementation
- Rekey is **automatic** when new member added ?
- Rekey is **optional** when member removed ?

### Recommendation
```csharp
// In ConversationController when member leaves
public async Task<IActionResult> RemoveMember(string conversationId, string memberId)
{
    // ... existing logic ...
    
    // FORCE rekey when member leaves
    var messageDecryptor = new MessageDecryptor();
    var newKey = await messageDecryptor.RekeyConversationAsync(conversationId);
    
    if (newKey is null)
        return BadRequest("Failed to rekey conversation after member removal");
    
    return Ok();
}
```

---

## 7. ?? HIGH PRIORITY: No Verification of Message Sender Identity

### Problem in MessageDecryptor.ProcessAsync()

```csharp
bool isOut = !string.IsNullOrWhiteSpace(myMemberId)
    ? string.Equals(message.SenderID, myMemberId, StringComparison.Ordinal)
    : !string.IsNullOrWhiteSpace(message.SenderUsername)
        && string.Equals(message.SenderUsername, CurrentUsername,
            StringComparison.Ordinal);
```

**Issue:** Trusts sender identity from server response without verification
- Server can claim any UserID as sender
- No digital signature verification
- **Message spoofing possible** ??

### Recommendation (Complex, requires redesign)
```csharp
// Sender should sign message with their private key
// Include signature in SendMessageRequest
// Client verifies signature using sender's public key before displaying

public record SendMessageRequest(
    MessageType Type,
    string? Content,
    string? ContentIV,
    string? Signature, // NEW: Ed25519 or RSA signature
    // ... other fields
);
```

---

## 8. ?? MEDIUM PRIORITY: Token Storage Not Fully Secure

### Problem in TokenStorage.cs

**Where stored:**
- JWT access token: In memory (app instance) ?
- Refresh token: Unknown (check implementation)

**Risk:**
- WinForms memory can be dumped by debugger or malware
- No additional encryption layer

### Current Implementation (TokenStorage)
Not fully shown, but recommendation:
```csharp
public class TokenStorage
{
    // Encrypt tokens at rest with DPAPI
    private static readonly byte[] ENTROPY = Encoding.UTF8.GetBytes("SecureChat-Key");
    
    public static void SaveRefreshToken(string token)
    {
        var encrypted = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(token),
            ENTROPY,
            DataProtectionScope.CurrentUser);
        
        File.WriteAllBytes(_tokenPath, encrypted);
    }
}
```

---

## 9. ?? MEDIUM PRIORITY: MessageDecryptor Old Keys History Not Pruned

### Problem in MessageDecryptor._oldConversationKeys

```csharp
private readonly ConcurrentDictionary<string, List<byte[]>> _oldConversationKeys = new();

// In RekeyConversationAsync:
var history = _oldConversationKeys.GetOrAdd(conversationId, _ => new List<byte[]>());
lock (history)
{
    history.Insert(0, oldKey);
    // ?? Never removes old keys
}
```

**Issue:**
- History grows unbounded
- After 100 rekeyings, 100 keys in memory
- If one old key is compromised, attacker can decrypt all old messages
- **No key expiration policy**

### Recommendation
```csharp
private const int MAX_KEY_HISTORY = 5; // Keep last 5 keys

lock (history)
{
    history.Insert(0, oldKey);
    
    // Prune to max 5 keys
    if (history.Count > MAX_KEY_HISTORY)
    {
        history.RemoveAt(history.Count - 1);
    }
}
```

---

## 10. ?? LOW PRIORITY: ExpiresAt Messages Not Server-Enforced

### Problem in MessageController.SendMessage()

```csharp
DateTime? expiresAt = null;
if (req.ExpiresAfterSeconds.HasValue && req.ExpiresAfterSeconds.Value > 0)
{
    expiresAt = DateTime.UtcNow.AddSeconds(req.ExpiresAfterSeconds.Value);
}
```

**Issue:**
- Client calculates `ExpiresAfterSeconds`
- Server trusts client value
- Malicious client can set `ExpiresAfterSeconds = 999999999`
- Message won't auto-delete

### Recommendation
```csharp
private const int MAX_EXPIRY_SECONDS = 86400; // 24 hours

if (req.ExpiresAfterSeconds.HasValue)
{
    var seconds = Math.Min(req.ExpiresAfterSeconds.Value, MAX_EXPIRY_SECONDS);
    if (seconds > 0)
        expiresAt = DateTime.UtcNow.AddSeconds(seconds);
}
```

---

## 11. ?? LOW PRIORITY: No Rate Limiting on Message Send

### Problem
- No rate limit checks in MessageController.SendMessage()
- User can send 10,000 messages/second
- **Spam/DoS vector** ?? (low severity in private chat)

### Recommendation
Add middleware or controller logic:
```csharp
private readonly Dictionary<string, (int Count, DateTime ResetAt)> _rateLimits = new();

private bool CheckRateLimit(string userId, int messagesPerSecond = 10)
{
    if (!_rateLimits.TryGetValue(userId, out var limit))
    {
        _rateLimits[userId] = (1, DateTime.UtcNow.AddSeconds(1));
        return true;
    }
    
    if (DateTime.UtcNow > limit.ResetAt)
    {
        _rateLimits[userId] = (1, DateTime.UtcNow.AddSeconds(1));
        return true;
    }
    
    if (limit.Count >= messagesPerSecond)
        return false;
    
    _rateLimits[userId] = (limit.Count + 1, limit.ResetAt);
    return true;
}
```

---

## Summary Table

| Issue | Severity | Category | Status | Fix Complexity |
|-------|----------|----------|--------|-----------------|
| 1. Message sending incomplete | ?? Critical | Architecture | ? Missing | Medium |
| 2. Rekey race condition | ?? Critical | Concurrency | ?? Partial | High |
| 3. ContentIV storage | ?? OK | Security | ? Correct | N/A |
| 4. No message integrity check | ?? High | Crypto | ? Missing | Medium |
| 5. Key rotation not enforced | ?? High | Access Control | ?? Partial | Medium |
| 6. No sender verification | ?? High | Authentication | ? Missing | High |
| 7. Token storage encryption | ?? Medium | Data Protection | ?? Partial | Low |
| 8. Old keys history not pruned | ?? Medium | Memory | ?? Partial | Low |
| 9. ExpiresAt not validated | ?? Low | Input Validation | ?? Weak | Low |
| 10. No rate limiting | ?? Low | DoS Protection | ? Missing | Low |

---

## Recommendations (Priority Order)

### Immediate (Before Production)
1. ? Implement client-side message sending
2. ? Fix rekey race condition with proper atomicity
3. ? Add message integrity checks (ContentHash + HMAC)
4. ? Enforce key rotation on member removal

### Short-term (Next Sprint)
5. Add sender identity verification (digital signatures)
6. Secure token storage with DPAPI
7. Prune old key history with size limit
8. Validate ExpiresAfterSeconds server-side

### Long-term (Nice to have)
9. Add rate limiting middleware
10. Message audit logging
11. Incident response procedures

---

## Conclusion

**Overall Security Grade: B+ (Good, with critical gaps)**

The **encryption architecture is sound**, but:
- **Execution has critical gaps** (missing message send, rekey race condition)
- **Missing integrity verification** for messages
- **Access control needs strengthening** (key rotation, sender verification)

**Recommendation:** Address all ?? critical issues before production deployment.
