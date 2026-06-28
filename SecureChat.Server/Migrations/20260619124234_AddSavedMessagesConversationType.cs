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
            // MySQL 9.x removed IF EXISTS support for DROP CHECK.
            // Constraint may not exist on fresh databases (was never explicitly created by prior migrations).
            // Use conditional drop via prepared statement.
            migrationBuilder.Sql(@"
SET @_exists = (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Conversations' AND CONSTRAINT_NAME = 'chk_conv_type');
SET @_sql = IF(@_exists > 0, 'ALTER TABLE Conversations DROP CHECK chk_conv_type', 'SELECT 1');
PREPARE _stmt FROM @_sql;
EXECUTE _stmt;
DEALLOCATE PREPARE _stmt;
");

            migrationBuilder.AddCheckConstraint(
                name: "chk_conv_type",
                table: "Conversations",
                sql: "conversation_type in (0, 1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // (same conditional-DROP approach as Up)
            migrationBuilder.Sql(@"
SET @_exists = (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Conversations' AND CONSTRAINT_NAME = 'chk_conv_type');
SET @_sql = IF(@_exists > 0, 'ALTER TABLE Conversations DROP CHECK chk_conv_type', 'SELECT 1');
PREPARE _stmt FROM @_sql;
EXECUTE _stmt;
DEALLOCATE PREPARE _stmt;
");

            migrationBuilder.AddCheckConstraint(
                name: "chk_conv_type",
                table: "Conversations",
                sql: "conversation_type in (0, 1)");
        }
    }
}
