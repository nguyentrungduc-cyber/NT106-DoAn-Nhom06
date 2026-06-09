using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureChat.DTOs;
using SecureChat.Models;
using SecureChat.Repositories;

namespace SecureChat.Controllers
{
	[Authorize]
	[ApiController]
	[Route("api/friends")]
	public class FriendController(FriendRepository friends, UserRepository users) : BaseController
	{
		string Me => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

		[HttpGet]
		public async Task<IActionResult> GetFriends()
		{
			var list = await friends.GetFriendsByUserAsync(Me);
			var result = list.Select(f => {
				var friend = f.UserAID == Me ? f.UserB : f.UserA;
				return new FriendResponse(f.FriendshipID, UserResponse.From(friend), f.CreatedAt);
			});

			return Ok(result);
		}

		[HttpDelete("{friendshipID}")]
		public async Task<IActionResult> Unfriend(string friendshipID)
		{
			var friendship = await friends.GetFriendshipByIdAsync(friendshipID);
			if (friendship is null)
				return NotFound();
			if (friendship.UserAID != Me && friendship.UserBID != Me)
				return Forbid();

			await friends.DeleteFriendshipAsync(friendshipID);
			return NoContent();
		}

		[HttpGet("requests/received")]
		public async Task<IActionResult> GetReceivedRequests()
		{
			var list = await friends.GetPendingRequestsForRecipientAsync(Me);
			return Ok(list.Select(FriendRequestResponse.From));
		}

		[HttpGet("requests/sent")]
		public async Task<IActionResult> GetSentRequests()
		{
			var list = await friends.GetSentRequestsByUserAsync(Me);
			return Ok(list.Select(FriendRequestResponse.From));
		}

        [HttpPost("requests")]
        public async Task<IActionResult> SendRequest([FromBody] SendFriendRequestRequest req)
        {
            if (await friends.AreFriendsAsync(Me, req.RecipientID))
                return Conflict(new { error = "Đã là bạn bè." });

            var existing = await friends.GetFriendRequestByPairAsync(Me, req.RecipientID);

            if (existing is not null)
            {
                if (existing.Status == FriendRequestStatus.Pending)
                    return Conflict(new { error = "Lời mời kết bạn đã được gửi." });

                // Nếu đã cancelled/declined trước đó → update lại thành Pending thay vì insert mới
                if (existing.SenderID == Me)
                {
                    await friends.UpdateFriendRequestStatusAsync(existing.RequestID, FriendRequestStatus.Pending);
                    return Ok(new { message = "Đã gửi lại lời mời." });
                }
            }

            // Tạo mới nếu chưa có record nào
            var newReq = new SecureChat.Models.FriendRequest
            {
                RequestID = NewID(),
                SenderID = Me,
                RecipientID = req.RecipientID,
                Status = FriendRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            await friends.CreateFriendRequestAsync(newReq);
            return CreatedAtAction(nameof(GetReceivedRequests), new { }, null);
        }

        [HttpPut("requests/{requestID}/accept")]
		public async Task<IActionResult> AcceptRequest(string requestID)
		{
			var request = await friends.GetFriendRequestByIdAsync(requestID);
			if (request is null)
				return NotFound();
			if (request.RecipientID != Me)
				return Forbid();
			if (request.Status != FriendRequestStatus.Pending)
				return BadRequest(new { error = "Lời mời không còn ở trạng thái chờ." });

			await friends.UpdateFriendRequestStatusAsync(requestID, FriendRequestStatus.Accepted);

			var friendship = await friends.CreateFriendshipAsync(new Friend {
				FriendshipID = NewID(),
				UserAID      = request.SenderID,
				UserBID      = request.RecipientID
			});

			return Ok(new { friendshipID = friendship.FriendshipID });
		}

		[HttpPut("requests/{requestID}/decline")]
		public async Task<IActionResult> DeclineRequest(string requestID)
		{
			var request = await friends.GetFriendRequestByIdAsync(requestID);
			if (request is null)
				return NotFound();
			if (request.RecipientID != Me)
				return Forbid();
			if (request.Status != FriendRequestStatus.Pending)
				return BadRequest(new { error = "Lời mời không còn ở trạng thái chờ." });

			await friends.UpdateFriendRequestStatusAsync(requestID, FriendRequestStatus.Declined);
			return NoContent();
		}

		[HttpDelete("requests/{requestID}")]
		public async Task<IActionResult> CancelRequest(string requestID)
		{
			var request = await friends.GetFriendRequestByIdAsync(requestID);
			if (request is null)
				return NotFound();
			if (request.SenderID != Me)
				return Forbid();
			if (request.Status != FriendRequestStatus.Pending)
				return BadRequest(new { error = "Không thể hủy lời mời này." });

			await friends.UpdateFriendRequestStatusAsync(requestID, FriendRequestStatus.Cancelled);
			return NoContent();
		}

		[HttpGet("blocked")]
		public async Task<IActionResult> GetBlockedUsers()
		{
			var list = await friends.GetBlockedByUserAsync(Me);
			return Ok(list.Select(BlockedUserResponse.From));
		}

		[HttpPost("blocked")]
		public async Task<IActionResult> BlockUser([FromBody] BlockUserRequest req)
		{
			if (req.BlockedID == Me)
				return BadRequest(new { error = "Không thể tự chặn mình." });
			if (!await users.ExistsByIdAsync(req.BlockedID))
				return NotFound(new { error = "Người dùng không tồn tại." });
			if (await friends.IsBlockedAsync(Me, req.BlockedID))
				return Conflict(new { error = "Đã chặn người dùng này rồi." });

			var friendship = await friends.GetFriendshipByPairAsync(Me, req.BlockedID);
			if (friendship is not null)
				await friends.DeleteFriendshipAsync(friendship.FriendshipID);

			var block = await friends.BlockUserAsync(new BlockedUser {
				BlockID   = NewID(),
				BlockerID = Me,
				BlockedID = req.BlockedID
			});

			var loaded = await friends.GetBlockByIdAsync(block.BlockID);
			return CreatedAtAction(nameof(GetBlockedUsers), BlockedUserResponse.From(loaded!));
		}

		[HttpDelete("blocked/{blockID}")]
		public async Task<IActionResult> UnblockUser(string blockID)
		{
			var block = await friends.GetBlockByIdAsync(blockID);
			if (block is null)
				return NotFound();
			if (block.BlockerID != Me)
				return Forbid();

			await friends.UnblockUserAsync(blockID);
			return NoContent();
		}
	}
}

