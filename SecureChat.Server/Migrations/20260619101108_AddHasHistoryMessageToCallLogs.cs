using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureChat.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddHasHistoryMessageToCallLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "has_history_message",
                table: "CallLogs",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "has_history_message",
                table: "CallLogs");
        }
    }
}
