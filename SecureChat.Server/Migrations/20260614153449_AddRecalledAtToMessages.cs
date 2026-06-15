using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SecureChat.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddRecalledAtToMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Note: idx_messages_expires_at was created manually in AddExpiresAtToMessages
            // (which has no Designer.cs), so the model snapshot doesn't track it.
            // We skip DropIndex here to avoid "index doesn't exist" on databases
            // where the previous migration was applied before the index was added.

            migrationBuilder.DeleteData(
                table: "BlockedUsers",
                keyColumn: "block_id",
                keyValue: "B0000001");

            migrationBuilder.DeleteData(
                table: "CallParticipants",
                keyColumns: new[] { "call_id", "participant_id" },
                keyValues: new object[] { "CL000001", "M0000003" });

            migrationBuilder.DeleteData(
                table: "CallParticipants",
                keyColumns: new[] { "call_id", "participant_id" },
                keyValues: new object[] { "CL000001", "M0000004" });

            migrationBuilder.DeleteData(
                table: "CallParticipants",
                keyColumns: new[] { "call_id", "participant_id" },
                keyValues: new object[] { "CL000001", "M0000005" });

            migrationBuilder.DeleteData(
                table: "FriendRequests",
                keyColumn: "request_id",
                keyValue: "RQ000001");

            migrationBuilder.DeleteData(
                table: "Friends",
                keyColumn: "friendship_id",
                keyValue: "F0000001");

            migrationBuilder.DeleteData(
                table: "MessageAttachments",
                keyColumn: "attachment_id",
                keyValue: "A0000001");

            migrationBuilder.DeleteData(
                table: "MessageMentions",
                keyColumns: new[] { "member_id", "message_id" },
                keyValues: new object[] { "M0000005", "MSG00005" });

            migrationBuilder.DeleteData(
                table: "MessagePins",
                keyColumns: new[] { "conversation_id", "message_id" },
                keyValues: new object[] { "C0000002", "MSG00006" });

            migrationBuilder.DeleteData(
                table: "MessageReactions",
                keyColumn: "reaction_id",
                keyValue: "RE000001");

            migrationBuilder.DeleteData(
                table: "MessageStatuses",
                keyColumn: "status_id",
                keyValue: "ST000001");

            migrationBuilder.DeleteData(
                table: "MessageStatuses",
                keyColumn: "status_id",
                keyValue: "ST000002");

            migrationBuilder.DeleteData(
                table: "MessageStatuses",
                keyColumn: "status_id",
                keyValue: "ST000003");

            migrationBuilder.DeleteData(
                table: "Messages",
                keyColumn: "message_id",
                keyValue: "MSG00003");

            migrationBuilder.DeleteData(
                table: "UserSessions",
                keyColumn: "session_id",
                keyValue: "S0000001");

            migrationBuilder.DeleteData(
                table: "UserSessions",
                keyColumn: "session_id",
                keyValue: "S0000002");

            migrationBuilder.DeleteData(
                table: "CallLogs",
                keyColumn: "call_id",
                keyValue: "CL000001");

            migrationBuilder.DeleteData(
                table: "Messages",
                keyColumn: "message_id",
                keyValue: "MSG00002");

            migrationBuilder.DeleteData(
                table: "Messages",
                keyColumn: "message_id",
                keyValue: "MSG00005");

            migrationBuilder.DeleteData(
                table: "Messages",
                keyColumn: "message_id",
                keyValue: "MSG00007");

            migrationBuilder.DeleteData(
                table: "ConversationMembers",
                keyColumn: "member_id",
                keyValue: "M0000002");

            migrationBuilder.DeleteData(
                table: "ConversationMembers",
                keyColumn: "member_id",
                keyValue: "M0000004");

            migrationBuilder.DeleteData(
                table: "Messages",
                keyColumn: "message_id",
                keyValue: "MSG00001");

            migrationBuilder.DeleteData(
                table: "Messages",
                keyColumn: "message_id",
                keyValue: "MSG00004");

            migrationBuilder.DeleteData(
                table: "Messages",
                keyColumn: "message_id",
                keyValue: "MSG00006");

            migrationBuilder.DeleteData(
                table: "ConversationMembers",
                keyColumn: "member_id",
                keyValue: "M0000001");

            migrationBuilder.DeleteData(
                table: "ConversationMembers",
                keyColumn: "member_id",
                keyValue: "M0000003");

            migrationBuilder.DeleteData(
                table: "ConversationMembers",
                keyColumn: "member_id",
                keyValue: "M0000005");

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "user_id",
                keyValue: "U0000002");

            migrationBuilder.DeleteData(
                table: "Conversations",
                keyColumn: "conversation_id",
                keyValue: "C0000001");

            migrationBuilder.DeleteData(
                table: "Conversations",
                keyColumn: "conversation_id",
                keyValue: "C0000002");

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "user_id",
                keyValue: "U0000003");

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "user_id",
                keyValue: "U0000001");

            migrationBuilder.AddColumn<DateTime>(
                name: "recalled_at",
                table: "Messages",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "recalled_at",
                table: "Messages");

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "user_id", "avatar_url", "bio_text", "created_at", "display_name", "email", "hashed_b_key", "hashed_password", "hashed_recovery_key", "key_salt", "public_key", "show_online_status", "show_read_status", "updated_at", "username" },
                values: new object[,]
                {
                    { "U0000001", null, null, new DateTime(2025, 1, 10, 8, 0, 0, 0, DateTimeKind.Utc), "Hoang Hieu", "u1@securechat.local", "hash_demo_value", "hash_demo_value", "hash_demo_value", "hash_demo_value", "encrypted_demo_value", true, true, new DateTime(2025, 1, 10, 8, 0, 0, 0, DateTimeKind.Utc), "hoanghieu" },
                    { "U0000002", null, null, new DateTime(2025, 1, 10, 8, 0, 0, 0, DateTimeKind.Utc), "Minh Quan", "u2@securechat.local", "hash_demo_value", "hash_demo_value", "hash_demo_value", "hash_demo_value", "encrypted_demo_value", true, true, new DateTime(2025, 1, 10, 8, 0, 0, 0, DateTimeKind.Utc), "minhquan" },
                    { "U0000003", null, null, new DateTime(2025, 1, 10, 8, 0, 0, 0, DateTimeKind.Utc), "Linh Nguyen", "u3@securechat.local", "hash_demo_value", "hash_demo_value", "hash_demo_value", "hash_demo_value", "encrypted_demo_value", true, true, new DateTime(2025, 1, 10, 8, 0, 0, 0, DateTimeKind.Utc), "linhnguyen" }
                });

            migrationBuilder.InsertData(
                table: "BlockedUsers",
                columns: new[] { "block_id", "blocked_id", "blocker_id", "created_at" },
                values: new object[] { "B0000001", "U0000003", "U0000002", new DateTime(2025, 1, 10, 8, 8, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "Conversations",
                columns: new[] { "conversation_id", "avatar_url", "created_at", "created_by", "last_activity_at", "last_message_id", "Name", "conversation_type" },
                values: new object[,]
                {
                    { "C0000001", null, new DateTime(2025, 1, 10, 8, 12, 0, 0, DateTimeKind.Utc), "U0000001", new DateTime(2025, 1, 10, 8, 21, 0, 0, DateTimeKind.Utc), null, null, (byte)0 },
                    { "C0000002", null, new DateTime(2025, 1, 10, 8, 12, 0, 0, DateTimeKind.Utc), "U0000001", new DateTime(2025, 1, 10, 8, 28, 0, 0, DateTimeKind.Utc), null, "NT106 Team", (byte)1 }
                });

            migrationBuilder.InsertData(
                table: "FriendRequests",
                columns: new[] { "request_id", "created_at", "recipient_id", "responded_at", "sender_id", "status" },
                values: new object[] { "RQ000001", new DateTime(2025, 1, 10, 8, 8, 0, 0, DateTimeKind.Utc), "U0000001", null, "U0000003", (byte)0 });

            migrationBuilder.InsertData(
                table: "Friends",
                columns: new[] { "friendship_id", "created_at", "user_a_id", "user_b_id" },
                values: new object[] { "F0000001", new DateTime(2025, 1, 10, 8, 5, 0, 0, DateTimeKind.Utc), "U0000001", "U0000002" });

            migrationBuilder.InsertData(
                table: "UserSessions",
                columns: new[] { "session_id", "created_at", "device_name", "expires_at", "last_used_at", "refresh_token", "user_id" },
                values: new object[,]
                {
                    { "S0000001", new DateTime(2025, 1, 10, 8, 2, 0, 0, DateTimeKind.Utc), "Windows 11 Dev Machine", new DateTime(2025, 2, 9, 8, 2, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 10, 8, 2, 0, 0, DateTimeKind.Utc), "refresh_token_demo_u1", "U0000001" },
                    { "S0000002", new DateTime(2025, 1, 10, 8, 2, 0, 0, DateTimeKind.Utc), "Windows 11 QA Laptop", new DateTime(2025, 2, 9, 8, 2, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 10, 8, 2, 0, 0, DateTimeKind.Utc), "refresh_token_demo_u2", "U0000002" }
                });

            migrationBuilder.InsertData(
                table: "ConversationMembers",
                columns: new[] { "member_id", "banned_until", "conversation_id", "encrypted_key", "joined_at", "last_read_msg_id", "left_at", "nickname", "role", "show_notifications", "user_id" },
                values: new object[,]
                {
                    { "M0000001", null, "C0000001", "encrypted_demo_value", new DateTime(2025, 1, 10, 8, 12, 0, 0, DateTimeKind.Utc), null, null, "Hieu", (byte)2, (byte)2, "U0000001" },
                    { "M0000002", null, "C0000001", "encrypted_demo_value", new DateTime(2025, 1, 10, 8, 12, 0, 0, DateTimeKind.Utc), null, null, "Quan", (byte)0, (byte)2, "U0000002" },
                    { "M0000003", null, "C0000002", "encrypted_demo_value", new DateTime(2025, 1, 10, 8, 12, 0, 0, DateTimeKind.Utc), null, null, "Admin Hieu", (byte)2, (byte)2, "U0000001" },
                    { "M0000004", null, "C0000002", "encrypted_demo_value", new DateTime(2025, 1, 10, 8, 12, 0, 0, DateTimeKind.Utc), null, null, "Mod Quan", (byte)1, (byte)2, "U0000002" },
                    { "M0000005", null, "C0000002", "encrypted_demo_value", new DateTime(2025, 1, 10, 8, 12, 0, 0, DateTimeKind.Utc), null, null, "Linh", (byte)0, (byte)2, "U0000003" }
                });

            migrationBuilder.InsertData(
                table: "CallLogs",
                columns: new[] { "call_id", "conversation_id", "ended_at", "started_at", "started_by", "status", "call_type" },
                values: new object[] { "CL000001", "C0000002", new DateTime(2025, 1, 10, 8, 28, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 10, 8, 24, 0, 0, DateTimeKind.Utc), "M0000003", (byte)2, (byte)0 });

            migrationBuilder.InsertData(
                table: "Messages",
                columns: new[] { "message_id", "content", "content_iv", "conversation_id", "deleted_at", "edited_at", "expires_at", "original_sender_id", "reply_to_id", "sender_id", "sent_at", "message_type" },
                values: new object[,]
                {
                    { "MSG00001", "hello bro", "iv_demo_value", "C0000001", null, null, null, "U0000001", null, "M0000001", new DateTime(2025, 1, 10, 8, 15, 0, 0, DateTimeKind.Utc), (byte)0 },
                    { "MSG00003", "encrypt ok ch?a?", "iv_demo_value", "C0000001", null, null, null, "U0000001", null, "M0000001", new DateTime(2025, 1, 10, 8, 21, 0, 0, DateTimeKind.Utc), (byte)0 },
                    { "MSG00004", "hello team, test group chat nhé", "iv_demo_value", "C0000002", null, null, null, "U0000001", null, "M0000003", new DateTime(2025, 1, 10, 8, 15, 0, 0, DateTimeKind.Utc), (byte)0 },
                    { "MSG00006", "done r?i, nh? test forgot password", "iv_demo_value", "C0000002", null, null, null, "U0000003", null, "M0000005", new DateTime(2025, 1, 10, 8, 21, 0, 0, DateTimeKind.Utc), (byte)0 }
                });

            migrationBuilder.InsertData(
                table: "CallParticipants",
                columns: new[] { "call_id", "participant_id", "joined_at", "left_at", "status" },
                values: new object[,]
                {
                    { "CL000001", "M0000003", new DateTime(2025, 1, 10, 8, 24, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 10, 8, 28, 0, 0, DateTimeKind.Utc), (byte)1 },
                    { "CL000001", "M0000004", new DateTime(2025, 1, 10, 8, 24, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 10, 8, 28, 0, 0, DateTimeKind.Utc), (byte)1 },
                    { "CL000001", "M0000005", null, null, (byte)3 }
                });

            migrationBuilder.InsertData(
                table: "MessagePins",
                columns: new[] { "conversation_id", "message_id", "pinned_at", "pinned_by" },
                values: new object[] { "C0000002", "MSG00006", new DateTime(2025, 1, 10, 8, 28, 0, 0, DateTimeKind.Utc), "M0000003" });

            migrationBuilder.InsertData(
                table: "MessageStatuses",
                columns: new[] { "status_id", "delivered_at", "member_id", "message_id", "read_at" },
                values: new object[,]
                {
                    { "ST000001", new DateTime(2025, 1, 10, 8, 15, 0, 0, DateTimeKind.Utc), "M0000002", "MSG00001", new DateTime(2025, 1, 10, 8, 18, 0, 0, DateTimeKind.Utc) },
                    { "ST000003", new DateTime(2025, 1, 10, 8, 21, 0, 0, DateTimeKind.Utc), "M0000003", "MSG00006", new DateTime(2025, 1, 10, 8, 24, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "Messages",
                columns: new[] { "message_id", "content", "content_iv", "conversation_id", "deleted_at", "edited_at", "expires_at", "original_sender_id", "reply_to_id", "sender_id", "sent_at", "message_type" },
                values: new object[,]
                {
                    { "MSG00002", "check API ch?a?", "iv_demo_value", "C0000001", null, null, null, "U0000002", "MSG00001", "M0000002", new DateTime(2025, 1, 10, 8, 18, 0, 0, DateTimeKind.Utc), (byte)0 },
                    { "MSG00005", "ok bro, SignalR realtime ?n", "iv_demo_value", "C0000002", null, null, null, "U0000002", "MSG00004", "M0000004", new DateTime(2025, 1, 10, 8, 18, 0, 0, DateTimeKind.Utc), (byte)0 },
                    { "MSG00007", "file sent: api_test_plan.pdf", "iv_demo_value", "C0000002", null, null, null, "U0000001", "MSG00006", "M0000003", new DateTime(2025, 1, 10, 8, 24, 0, 0, DateTimeKind.Utc), (byte)4 }
                });

            migrationBuilder.InsertData(
                table: "MessageAttachments",
                columns: new[] { "attachment_id", "duration_secs", "encrypted_aes_iv", "encrypted_aes_key", "file_hash", "file_iv", "file_name", "file_name_in_storage", "file_size", "file_type", "file_url", "height", "message_id", "receiver_id", "thumbnail_iv", "thumbnail_url", "uploaded_at", "width" },
                values: new object[] { "A0000001", null, null, null, "hash_demo_value", "iv_demo_value", "api_test_plan.pdf", "", 102400L, "application/pdf", "encrypted_demo_value", null, "MSG00007", null, null, null, new DateTime(2025, 1, 10, 8, 24, 0, 0, DateTimeKind.Utc), null });

            migrationBuilder.InsertData(
                table: "MessageMentions",
                columns: new[] { "member_id", "message_id" },
                values: new object[] { "M0000005", "MSG00005" });

            migrationBuilder.InsertData(
                table: "MessageReactions",
                columns: new[] { "reaction_id", "created_at", "member_id", "message_id", "reaction" },
                values: new object[] { "RE000001", new DateTime(2025, 1, 10, 8, 21, 0, 0, DateTimeKind.Utc), "M0000001", "MSG00002", "??" });

            migrationBuilder.InsertData(
                table: "MessageStatuses",
                columns: new[] { "status_id", "delivered_at", "member_id", "message_id", "read_at" },
                values: new object[] { "ST000002", new DateTime(2025, 1, 10, 8, 18, 0, 0, DateTimeKind.Utc), "M0000001", "MSG00002", new DateTime(2025, 1, 10, 8, 21, 0, 0, DateTimeKind.Utc) });
        }
    }
}
