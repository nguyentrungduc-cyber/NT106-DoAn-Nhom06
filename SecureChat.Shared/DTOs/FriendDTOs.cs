using System.ComponentModel.DataAnnotations;
using SecureChat.Models;

namespace SecureChat.DTOs
{
	public record SendFriendRequestRequest(
		[Required] string RecipientID
	);

	public record BlockUserRequest(
		[Required] string BlockedID
	);

	public record FriendResponse(
		string FriendshipID,
		UserResponse Friend,
		DateTime CreatedAt,
		bool IsOnline = false
	);

	public record FriendRequestResponse(
		[MaxLength(8)] string RequestID,
		UserResponse Sender,
		UserResponse Recipient,
		FriendRequestStatus Status,
		DateTime CreatedAt,
		DateTime? RespondedAt
	)
	{
		public static FriendRequestResponse From(FriendRequest r) => new(
			r.RequestID,
			UserResponse.From(r.Sender),
			UserResponse.From(r.Recipient),
			r.Status, r.CreatedAt, r.RespondedAt);
	}

	public record BlockedUserResponse(
		string BlockID,
		UserResponse Blocked,
		DateTime CreatedAt
	)
	{
		public static BlockedUserResponse From(BlockedUser b) => new(b.BlockID, UserResponse.From(b.Blocked), b.CreatedAt);
	}
}
