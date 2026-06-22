using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureChat.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPresenceTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserPresence",
                columns: table => new
                {
                    user_id = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    last_seen_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    active_session_count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPresence", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_UserPresence_Users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserPresence");
        }
    }
}
