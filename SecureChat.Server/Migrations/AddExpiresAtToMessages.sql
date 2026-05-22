-- Migration: Add ExpiresAt column to Messages table for self-destruct messages feature
-- Date: 2026-05-22
-- Description: Adds nullable expires_at column to track when messages should be automatically deleted

USE SecureChat;

-- Add expires_at column to Messages table
ALTER TABLE Messages 
ADD COLUMN expires_at DATETIME NULL 
COMMENT 'UTC timestamp when message should be automatically deleted (self-destruct)';

-- Create index on expires_at for efficient expiration queries
CREATE INDEX idx_messages_expires_at ON Messages(expires_at);

-- Optional: Add a stored procedure to clean up expired messages (server-side cleanup)
DELIMITER $$

CREATE PROCEDURE CleanupExpiredMessages()
BEGIN
    -- Delete messages that have expired
    DELETE FROM Messages 
    WHERE expires_at IS NOT NULL 
      AND expires_at <= UTC_TIMESTAMP();
    
    -- Return number of deleted messages
    SELECT ROW_COUNT() AS deleted_count;
END$$

DELIMITER ;

-- Optional: Create an event to run cleanup periodically (every 5 minutes)
-- Note: Requires event_scheduler to be enabled (SET GLOBAL event_scheduler = ON;)
CREATE EVENT IF NOT EXISTS cleanup_expired_messages_event
ON SCHEDULE EVERY 5 MINUTE
DO
    CALL CleanupExpiredMessages();

-- Verify the changes
DESCRIBE Messages;
SHOW INDEX FROM Messages WHERE Key_name = 'idx_messages_expires_at';
