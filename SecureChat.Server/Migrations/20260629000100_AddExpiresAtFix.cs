using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureChat.Server.Migrations
{
    public partial class AddExpiresAtFix : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Prior migration AddExpiresAtToMessages may have been
            // marked applied without the column actually being created
            // on some DB states (e.g. after a partial-failure deploy).
            // Safely add the column + index if missing.
            migrationBuilder.Sql(@"
SET @_col_exists = (SELECT COUNT(*) FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Messages' AND COLUMN_NAME = 'expires_at');
SET @_col_sql = IF(@_col_exists = 0, 'ALTER TABLE Messages ADD COLUMN expires_at datetime(6) NULL', 'SELECT 1');
PREPARE _col_stmt FROM @_col_sql;
EXECUTE _col_stmt;
DEALLOCATE PREPARE _col_stmt;

SET @_idx_exists = (SELECT COUNT(*) FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Messages' AND INDEX_NAME = 'idx_messages_expires_at');
SET @_idx_sql = IF(@_idx_exists = 0, 'CREATE INDEX idx_messages_expires_at ON Messages (expires_at)', 'SELECT 1');
PREPARE _idx_stmt FROM @_idx_sql;
EXECUTE _idx_stmt;
DEALLOCATE PREPARE _idx_stmt;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: don't risk dropping the column in case it existed
            // before this fix was applied.
        }
    }
}
