using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureChat.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddEncryptedAesFieldsToAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "encrypted_aes_iv",
                table: "MessageAttachments",
                type: "varchar(1024)",
                maxLength: 1024,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "encrypted_aes_key",
                table: "MessageAttachments",
                type: "varchar(1024)",
                maxLength: 1024,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "file_name_in_storage",
                table: "MessageAttachments",
                type: "varchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "receiver_id",
                table: "MessageAttachments",
                type: "varchar(8)",
                maxLength: 8,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "MessageAttachments",
                keyColumn: "attachment_id",
                keyValue: "A0000001",
                columns: new[] { "encrypted_aes_iv", "encrypted_aes_key", "file_name_in_storage", "receiver_id" },
                values: new object[] { null, null, "", null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "encrypted_aes_iv",
                table: "MessageAttachments");

            migrationBuilder.DropColumn(
                name: "encrypted_aes_key",
                table: "MessageAttachments");

            migrationBuilder.DropColumn(
                name: "file_name_in_storage",
                table: "MessageAttachments");

            migrationBuilder.DropColumn(
                name: "receiver_id",
                table: "MessageAttachments");
        }
    }
}
