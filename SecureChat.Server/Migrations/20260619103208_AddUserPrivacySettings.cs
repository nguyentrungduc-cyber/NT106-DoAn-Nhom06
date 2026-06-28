using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureChat.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPrivacySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserPrivacySettings",
                columns: table => new
                {
                    user_id = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    last_seen_privacy = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    profile_photo_privacy = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    forwarded_messages_privacy = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    calls_privacy = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    voice_messages_privacy = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    messages_privacy = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    birthday_privacy = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    bio_privacy = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    auto_delete_mode = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPrivacySettings", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_UserPrivacySettings_Users_user_id",
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
                name: "UserPrivacySettings");
        }
    }
}
