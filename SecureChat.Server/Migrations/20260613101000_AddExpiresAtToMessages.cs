using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureChat.Server.Migrations
{
    public partial class AddExpiresAtToMessages : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "expires_at",
                table: "Messages",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_messages_expires_at",
                table: "Messages",
                column: "expires_at");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_messages_expires_at",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "expires_at",
                table: "Messages");
        }
    }
}
