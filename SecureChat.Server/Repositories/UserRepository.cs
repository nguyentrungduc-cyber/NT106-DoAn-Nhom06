using Microsoft.EntityFrameworkCore;
using SecureChat.Models;

namespace SecureChat.Repositories
{
	public class UserRepository(AppDbContext db)
	{
		public async Task<User> CreateAsync(User user)
		{
			user.CreatedAt = DateTime.UtcNow;
			user.UpdatedAt = DateTime.UtcNow;
			db.Users.Add(user);
			await db.SaveChangesAsync();
			return user;
		}

		public async Task<User?> GetByIdAsync(string userID)
			=> await db.Users.FindAsync(userID);

		public async Task<User?> GetByUsernameAsync(string username)
			=> await db.Users.FirstOrDefaultAsync(u => u.Username == username);

		public async Task<User?> GetByEmailAsync(string email)
			=> await db.Users.FirstOrDefaultAsync(u => u.Email == email);

		public async Task<List<User>> SearchAsync(string query)
			=> await db.Users
				.Where(u => (EF.Functions.Like(u.Username, $"%{query}%") || EF.Functions.Like(u.DisplayName, $"%{query}%")))
				.ToListAsync();

		public async Task<bool> ExistsByIdAsync(string userID)
			=> await db.Users.AnyAsync(u => u.UserID == userID);

		public async Task<bool> ExistsByUsernameAsync(string username)
			=> await db.Users.AnyAsync(u => u.Username == username);

		public async Task<bool> ExistsByEmailAsync(string email)
			=> await db.Users.AnyAsync(u => u.Email == email);

		public async Task UpdateAsync(User user)
		{
			user.UpdatedAt = DateTime.UtcNow;
			db.Users.Update(user);
			await db.SaveChangesAsync();
		}

		public async Task UpdateHashedPasswordAsync(string userID, string newHashedPassword, string newHashedBKey, string newKeySalt)
		{
			var user = await db.Users.FindAsync(userID)
				?? throw new KeyNotFoundException($"Không tìm thấy người dùng {userID}.");
			user.HashedPassword = newHashedPassword;
			user.HashedBKey = newHashedBKey;
			user.KeySalt = newKeySalt;
			user.UpdatedAt = DateTime.UtcNow;
			await db.SaveChangesAsync();
		}

        public async Task UpdatePasswordAsync(string userID, string newHashedPassword, string newSalt)
        {
            var user = await db.Users.FindAsync(userID)
                ?? throw new KeyNotFoundException($"Không tìm thấy người dùng {userID}.");

            // Cập nhật cả Hash và Salt mới
            user.HashedPassword = newHashedPassword;
            user.KeySalt = newSalt;
            user.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
        }

        public async Task UpdateAvatarAsync(string userID, string? avatarURL)
		{
			var user = await db.Users.FindAsync(userID)
				?? throw new KeyNotFoundException($"Không tìm thấy người dùng {userID}.");
			user.AvatarURL = avatarURL;
			user.UpdatedAt = DateTime.UtcNow;
			await db.SaveChangesAsync();
		}

		public async Task UpdatePrivacySettingsAsync(string userID, bool showReadStatus, bool showOnlineStatus)
		{
			var user = await db.Users.FindAsync(userID)
				?? throw new KeyNotFoundException($"Không tìm thấy người dùng {userID}.");
			user.ShowReadStatus = showReadStatus;
			user.ShowOnlineStatus = showOnlineStatus;
			user.UpdatedAt = DateTime.UtcNow;
			await db.SaveChangesAsync();
		}

		/// <summary>
		/// Update the user's public key for hybrid encryption.
		/// </summary>
		public async Task UpdatePublicKeyAsync(string userID, string publicKey)
		{
			if (string.IsNullOrWhiteSpace(publicKey))
				throw new ArgumentException("Public key is required.", nameof(publicKey));

			var user = await db.Users.FindAsync(userID)
				?? throw new KeyNotFoundException($"Không tìm thấy người dùng {userID}.");
			user.PublicKey = publicKey;
			user.UpdatedAt = DateTime.UtcNow;
			await db.SaveChangesAsync();
		}

		public async Task DeleteAsync(string userID)
		{
			var user = await db.Users.FindAsync(userID);
			if (user is null)
				return;
			db.Users.Remove(user);
			await db.SaveChangesAsync();
		}

		public async Task<UserSession> CreateSessionAsync(UserSession session)
		{
			session.CreatedAt  = DateTime.UtcNow;
			session.LastUsedAt = DateTime.UtcNow;
			db.UserSessions.Add(session);
			await db.SaveChangesAsync();
			return session;
		}

		public async Task<UserSession?> GetSessionByIdAsync(string sessionID)
			=> await db.UserSessions
			.Include(s => s.User)
			.FirstOrDefaultAsync(s => s.SessionID == sessionID);

		public async Task<UserSession?> GetSessionByRefreshTokenAsync(string refreshToken)
			=> await db.UserSessions
			.Include(s => s.User)
			.FirstOrDefaultAsync(s => s.RefreshToken == refreshToken);

		public async Task<List<UserSession>> GetAllSessionsByUserAsync(string userID)
			=> await db.UserSessions
			.Where(s => s.UserID == userID)
			.OrderByDescending(s => s.CreatedAt)
			.ToListAsync();

		public async Task UpdateSessionAsync(string sessionID, string newRefreshToken, DateTime newExpiry)
		{
			var session = await db.UserSessions.FindAsync(sessionID)
				?? throw new KeyNotFoundException($"Không tìm thấy session {sessionID}.");
			session.RefreshToken = newRefreshToken;
			session.ExpiresAt    = newExpiry;
			session.LastUsedAt   = DateTime.UtcNow;
			await db.SaveChangesAsync();
		}

		public async Task DeleteSessionAsync(string sessionID)
		{
			var session = await db.UserSessions.FindAsync(sessionID);
			if (session is null)
				return;
			db.UserSessions.Remove(session);
			await db.SaveChangesAsync();
		}

		public async Task RevokeAllSessionsAsync(string userID)
		{
			var sessions = await db.UserSessions
				.Where(s => s.UserID == userID)
				.ToListAsync();
			db.UserSessions.RemoveRange(sessions);
			await db.SaveChangesAsync();
		}
	}
}
