using Microsoft.EntityFrameworkCore;
using SecureChat.Models;

namespace SecureChat.Repositories
{
    public class FriendRepository(AppDbContext db)
    {
        public async Task<Friend> CreateFriendshipAsync(Friend friend)
        {
            if (string.Compare(friend.UserAID, friend.UserBID, StringComparison.Ordinal) > 0)
                (friend.UserAID, friend.UserBID) = (friend.UserBID, friend.UserAID);

            friend.CreatedAt = DateTime.UtcNow;
            db.Friends.Add(friend);
            await db.SaveChangesAsync();
            return friend;
        }

        public async Task<Friend?> GetFriendshipByIdAsync(string friendshipID)
            => await db.Friends
                .Include(f => f.UserA)
                .Include(f => f.UserB)
                .FirstOrDefaultAsync(f => f.FriendshipID == friendshipID);

        public async Task<Friend?> GetFriendshipByPairAsync(string userAID, string userBID)
        {
            if (string.Compare(userAID, userBID, StringComparison.Ordinal) > 0)
                (userAID, userBID) = (userBID, userAID);

            return await db.Friends
                .FirstOrDefaultAsync(f => f.UserAID == userAID && f.UserBID == userBID);
        }

        public async Task<List<Friend>> GetFriendsByUserAsync(string userID)
            => await db.Friends
                .Include(f => f.UserA)
                .Include(f => f.UserB)
                .Where(f => f.UserAID == userID || f.UserBID == userID)
                .OrderBy(f => f.CreatedAt)
                .ToListAsync();

        public async Task<bool> AreFriendsAsync(string userAID, string userBID)
            => await GetFriendshipByPairAsync(userAID, userBID) is not null;

        public async Task DeleteFriendshipAsync(string friendshipID)
        {
            var friend = await db.Friends.FindAsync(friendshipID);
            if (friend is null)
                return;

            db.Friends.Remove(friend);
            await db.SaveChangesAsync();
        }

        /*
		 * REQUESTS
		 */

        public async Task<FriendRequest> CreateFriendRequestAsync(FriendRequest request)
        {
            request.CreatedAt = DateTime.UtcNow;
            db.FriendRequests.Add(request);
            await db.SaveChangesAsync();
            return request;
        }

        public async Task<FriendRequest?> GetFriendRequestByIdAsync(string requestID)
            => await db.FriendRequests
                .Include(r => r.Sender)
                .Include(r => r.Recipient)
                .FirstOrDefaultAsync(r => r.RequestID == requestID);

        public async Task<FriendRequest?> GetFriendRequestByPairAsync(string senderID, string recipientID)
    => await db.FriendRequests
        .FirstOrDefaultAsync(r =>
            (r.SenderID == senderID && r.RecipientID == recipientID) ||
            (r.SenderID == recipientID && r.RecipientID == senderID));

        public async Task<List<FriendRequest>> GetPendingRequestsForRecipientAsync(string recipientID)
            => await db.FriendRequests
                .Include(r => r.Sender)
                .Include(r => r.Recipient)
                .Where(r => r.RecipientID == recipientID && r.Status == FriendRequestStatus.Pending)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

        public async Task<List<FriendRequest>> GetSentRequestsByUserAsync(string senderID)
            => await db.FriendRequests
                .Include(r => r.Sender)
                .Include(r => r.Recipient)
                .Where(r => r.SenderID == senderID && r.Status == FriendRequestStatus.Pending)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

        public async Task<FriendRequest> UpdateFriendRequestStatusAsync(string requestID, FriendRequestStatus status)
        {
            var request = await db.FriendRequests.FindAsync(requestID)
                ?? throw new KeyNotFoundException($"Không tìm thấy lời mời {requestID}.");

            request.Status = status;
            request.RespondedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return request;
        }

        public async Task DeleteFriendRequestAsync(string requestID)
        {
            var request = await db.FriendRequests.FindAsync(requestID);
            if (request is null)
                return;

            db.FriendRequests.Remove(request);
            await db.SaveChangesAsync();
        }

        /*
		 * BLOCK
		 */

        public async Task<BlockedUser> BlockUserAsync(BlockedUser block)
        {
            block.CreatedAt = DateTime.UtcNow;
            db.BlockedUsers.Add(block);
            await db.SaveChangesAsync();
            return block;
        }

        public async Task<BlockedUser?> GetBlockByIdAsync(string blockID)
            => await db.BlockedUsers
                .Include(b => b.Blocker)
                .Include(b => b.Blocked)
                .FirstOrDefaultAsync(b => b.BlockID == blockID);

        public async Task<BlockedUser?> GetBlockByPairAsync(string blockerID, string blockedID)
            => await db.BlockedUsers
                .FirstOrDefaultAsync(b => b.BlockerID == blockerID && b.BlockedID == blockedID);

        public async Task<List<BlockedUser>> GetBlockedByUserAsync(string blockerID)
            => await db.BlockedUsers
                .Include(b => b.Blocked)
                .Where(b => b.BlockerID == blockerID)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

        public async Task<bool> IsBlockedAsync(string blockerID, string blockedID)
            => await db.BlockedUsers
                .AnyAsync(b => b.BlockerID == blockerID && b.BlockedID == blockedID);

        public async Task<bool> IsBlockedEitherWayAsync(string userA, string userB)
            => await db.BlockedUsers
                .AnyAsync(b => (b.BlockerID == userA && b.BlockedID == userB) ||
                               (b.BlockerID == userB && b.BlockedID == userA));

        public async Task UnblockUserAsync(string blockID)
        {
            var block = await db.BlockedUsers.FindAsync(blockID);
            if (block is null)
                return;

            db.BlockedUsers.Remove(block);
            await db.SaveChangesAsync();
        }
    }
}
