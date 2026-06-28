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
            // DROP CHECK IF EXISTS — constraint may not exist if previous
            // migrations were generated after HasCheckConstraint was added to the model.
            migrationBuilder.Sql("ALTER TABLE Conversations DROP CHECK IF EXISTS chk_conv_type");

            migrationBuilder.AddCheckConstraint(
                name: "chk_conv_type",
                table: "Conversations",
                sql: "conversation_type in (0, 1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE Conversations DROP CHECK IF EXISTS chk_conv_type");

            migrationBuilder.AddCheckConstraint(
                name: "chk_conv_type",
                table: "Conversations",
                sql: "conversation_type in (0, 1)");
        }
    }
}
