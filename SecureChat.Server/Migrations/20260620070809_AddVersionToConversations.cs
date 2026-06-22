using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureChat.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddVersionToConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "Conversations",
                type: "int",
                nullable: false,
                defaultValueSql: "0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "version",
                table: "Conversations");
        }
    }
}
