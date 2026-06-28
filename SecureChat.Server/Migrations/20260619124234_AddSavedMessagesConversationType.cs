using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureChat.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedMessagesConversationType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MySQL 9.x removed IF EXISTS support for DROP CHECK
            // Constraint was created by migration AddHasHistoryMessageToCallLogs
            migrationBuilder.Sql("ALTER TABLE Conversations DROP CHECK chk_conv_type");

            migrationBuilder.AddCheckConstraint(
                name: "chk_conv_type",
                table: "Conversations",
                sql: "conversation_type in (0, 1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE Conversations DROP CHECK chk_conv_type");

            migrationBuilder.AddCheckConstraint(
                name: "chk_conv_type",
                table: "Conversations",
                sql: "conversation_type in (0, 1)");
        }
    }
}
