
set foreign_key_checks = 0;
set names utf8mb4;

create database if not exists SecureChat default character set utf8mb4 collate utf8mb4_unicode_ci;
use SecureChat;

create table if not exists Users (
    user_id varchar(8) not null,
    username varchar(16) not null,
    display_name varchar(32),
    email varchar(64) not null,
    avatar_url text,
    bio_text text,

    hashed_password text not null,
    hashed_b_key text not null,
    hashed_recovery_key text not null,
    key_salt text not null,
    public_key text not null,

    show_read_status boolean not null default true,
    show_online_status boolean not null default true,

    created_at datetime not null default current_timestamp,
    updated_at datetime not null default current_timestamp,

    primary key (user_id),
    unique key uq_users_username (username),
    unique key uq_users_email (email)
);

create table if not exists UserSessions (
    session_id varchar(8) not null,
    user_id varchar(8) not null,
    device_name varchar(64) not null,

    refresh_token text not null,

    created_at datetime not null default current_timestamp,
    expires_at datetime not null,
    last_used_at datetime not null default current_timestamp,

    primary key (session_id),
    unique key uq_sessions_refresh_token (refresh_token),
    constraint fk_sessions_user foreign key (user_id) references Users (user_id) on delete cascade
);

create table if not exists Friends (
    friendship_id varchar(8) not null,
    user_a_id varchar(8) not null,
    user_b_id varchar(8) not null,

    created_at datetime not null default current_timestamp,

    primary key (friendship_id),
    unique key uq_friends_pair (user_a_id, user_b_id),
    constraint fk_friends_user_a foreign key (user_a_id) references Users (user_id) on delete cascade,
    constraint fk_friends_user_b foreign key (user_b_id) references Users (user_id) on delete cascade,
    constraint chk_friends_order check (user_a_id < user_b_id)
);

-- status: 0 = đang chờ, 1 = chấp nhận, 2 = từ chối, 3 = người gửi hủy
create table if not exists FriendRequests (
    request_id varchar(8) not null,
    sender_id varchar(8) not null,
    recipient_id varchar(8) not null,

    status tinyint not null default 0,
    created_at datetime not null default current_timestamp,
    responded_at datetime,

    primary key (request_id),
    unique key uq_friendreq_pair (sender_id, recipient_id),
    constraint fk_friendreq_sender foreign key (sender_id) references Users (user_id) on delete cascade,
    constraint fk_friendreq_recipient foreign key (recipient_id) references Users (user_id) on delete cascade,
    constraint chk_friendreq_status check (status between 0 and 3)
);

create table if not exists BlockedUsers (
    block_id varchar(8) not null,
    blocker_id varchar(8) not null,
    blocked_id varchar(8) not null,

    created_at datetime not null default current_timestamp,

    primary key (block_id),
    unique key uq_block_pair (blocker_id, blocked_id),
    constraint fk_block_blocker foreign key (blocker_id) references Users (user_id) on delete cascade,
    constraint fk_block_blocked foreign key (blocked_id) references Users (user_id) on delete cascade
);

-- type: 0 = cá nhân, 1 = nhóm
create table if not exists Conversations (
    conversation_id varchar(8) not null,
    conversation_type tinyint not null default 0,
    name varchar(64),
    avatar_url text,

    created_by varchar(8),
    last_message_id varchar(8),
    last_activity_at datetime,

    created_at datetime not null default current_timestamp,

    primary key (conversation_id),
    constraint fk_conv_created_by foreign key (created_by) references Users (user_id) on delete set null,
    constraint fk_conv_last_message foreign key (last_message_id) references Messages (message_id) on delete set null,
    constraint chk_conv_type check (conversation_type in (0, 1))
);

-- role: 0 = dân đen, 1 = mod, 2 = chủ
-- banned_until: null = không bị cấm chat, khác null = bị cấm tới $banned_until
-- thông báo: 0 = không, 1 = chỉ @mention, 2 = có
create table if not exists ConversationMembers (
    member_id varchar(8) not null,
    conversation_id varchar(8) not null,
    user_id varchar(8) not null,
    role tinyint not null default 0,
    nickname varchar(64),

    encrypted_key text not null,

    joined_at datetime not null default current_timestamp,
    left_at datetime,

    show_notifications tinyint not null default 2,
    banned_until datetime,
    last_read_msg_id varchar(8),

    primary key (member_id),
    unique key uq_convmems_user (conversation_id, user_id),
    constraint fk_convmems_conv foreign key (conversation_id) references Conversations (conversation_id) on delete cascade,
    constraint fk_convmems_last_read foreign key (last_read_msg_id) references Messages (message_id) on delete set null,
    constraint fk_convmems_user foreign key (user_id) references Users (user_id) on delete cascade,
    constraint chk_convmems_notif check (show_notifications between 0 and 2),
    constraint chk_convmems_role check (role between 0 and 2)
);

-- type: 0 = văn bản, 1 = ảnh, 2 = video, 3 = audio, 4 = tệp, 5 = sticker, 6 = cuộc gọi, 7 = thông báo hệ thống
-- edited_at: NULL = chưa thực hiện, khác NULL = đã thực hiện tại mốc thời gian đó
-- original_sender_id: NULL = không được chuyển tiếp, khác NULL = được $original_sender_id chuyển tiếp
create table if not exists Messages (
    message_id varchar(8) not null,
    conversation_id varchar(8) not null,
    original_sender_id varchar(8),

    sender_id varchar(8),
    reply_to_id varchar(8),

    message_type tinyint default 0,
    content text,
    content_iv text,
    sent_at datetime not null default current_timestamp,
    deleted_at datetime,
    edited_at datetime,
    expires_at datetime,

    primary key (message_id),
    constraint fk_msg_conv foreign key (conversation_id) references Conversations (conversation_id) on delete cascade,
    constraint fk_msg_org_sender foreign key (original_sender_id) references Users (user_id) on delete set null,
    constraint fk_msg_reply_to foreign key (reply_to_id) references Messages (message_id) on delete set null,
    constraint fk_msg_sender foreign key (sender_id) references ConversationMembers (member_id) on delete cascade,
    constraint chk_message_type check (message_type between 0 and 7)
);

-- file_hash sẽ là hash gốc (tức trước khi mã hóa)
create table if not exists MessageAttachments (
    attachment_id varchar(8) not null,
    message_id varchar(8) not null,
    
    file_url text not null,
    file_name varchar(64) not null,
    file_name_in_storage varchar(64) not null,
    file_type varchar(128) not null,
    file_hash varchar(256) not null,

    file_size bigint not null,

    width int,
    height int,
    thumbnail_url text,
    duration_secs int,

    file_iv text,
    thumbnail_iv text,
    encrypted_aes_key varchar(1024),
    encrypted_aes_iv varchar(1024),
    receiver_id varchar(8),
    uploaded_at datetime not null default current_timestamp,

    primary key (attachment_id),
    constraint fk_attach_message foreign key (message_id) references Messages (message_id) on delete cascade,
    constraint fk_attach_receiver foreign key (receiver_id) references Users (user_id) on delete set null
);

create table if not exists MessageMentions (
    message_id varchar(8) not null,
    member_id varchar(8) not null,

    primary key (message_id, member_id),
    constraint fk_mention_message foreign key (message_id) references Messages (message_id) on delete cascade,
    constraint fk_mention_member foreign key (member_id) references ConversationMembers (member_id) on delete cascade
);

create table if not exists MessagePins (
    message_id varchar(8) not null,
    conversation_id varchar(8) not null,

    pinned_by varchar(8),
    pinned_at datetime not null default current_timestamp,
    
    primary key (message_id, conversation_id),
    constraint fk_pin_conv foreign key (conversation_id) references Conversations (conversation_id) on delete cascade,
    constraint fk_pin_message foreign key (message_id) references Messages (message_id) on delete cascade,
    constraint fk_pin_pinned_by foreign key (pinned_by) references ConversationMembers (member_id) on delete set null
);

create table if not exists MessageReactions (
    reaction_id varchar(8) not null,
    message_id varchar(8) not null,
    member_id varchar(8) not null,

    reaction varchar(8) not null,
    created_at datetime not null default current_timestamp,

    primary key (reaction_id),
    unique key uq_reaction_msg_users (message_id, member_id, reaction),
    constraint fk_reaction_message foreign key (message_id) references Messages (message_id) on delete cascade,
    constraint fk_reaction_member foreign key (member_id) references ConversationMembers (member_id) on delete cascade
);

create table if not exists MessageStatuses (
    status_id varchar(8) not null,
    message_id varchar(8) not null,
    member_id varchar(8) not null,
    
    delivered_at datetime,
    read_at datetime,

    primary key (status_id),
    unique key uq_status_msg_user (message_id, member_id),
    constraint fk_status_message foreign key (message_id) references Messages (message_id) on delete cascade,
    constraint fk_status_user foreign key (member_id) references ConversationMembers (member_id) on delete cascade
);

-- Loại: 0 = gọi thường, 1 = gọi video
-- Trạng thái: 0 = đổ chuông, 1 = đang tham gia, 2 = đã kết thúc, 3 = thất bại
create table if not exists CallLogs (
    call_id varchar(8) not null,
    conversation_id varchar(8) not null,
    call_type tinyint not null default 0,
    status tinyint not null default 0,

    started_by varchar(8),
    started_at datetime not null default current_timestamp,
    ended_at datetime,

    primary key (call_id),
    constraint fk_call_caller foreign key (started_by) references ConversationMembers (member_id) on delete cascade,
    constraint fk_call_conv foreign key (conversation_id) references Conversations (conversation_id) on delete cascade,
    constraint chk_call_type check (call_type in (0, 1)),
    constraint chk_call_status check (status between 0 and 3)
);

-- Trạng thái tham gia: 0 = đang đổ chuông, 1 = đã tham gia, 2 = từ chối, 3 = nhỡ máy, 4 = rời sớm
create table if not exists CallParticipants (
    participant_id varchar(8) not null,
    call_id varchar(8) not null,

    status tinyint not null default 0,
    joined_at datetime default null,
    left_at datetime default null,

    primary key (participant_id, call_id),
    constraint fk_participant_call foreign key (call_id) references CallLogs (call_id) on delete cascade,
    constraint fk_participant_participant foreign key (participant_id) references ConversationMembers (member_id) on delete cascade,
    constraint chk_participant_status check (status between 0 and 4)
);

create index if not exists idx_attachments_hash on MessageAttachments(file_hash);
create index if not exists idx_conv_activity on Conversations(last_activity_at DESC);
create index if not exists idx_conv_members_user on ConversationMembers(user_id);
create index if not exists idx_friend_req_receiver on FriendRequests(recipient_id, status);
create index if not exists idx_is_blocked on BlockedUsers(blocker_id);
create index if not exists idx_mentions_user on MessageMentions(member_id);
create index if not exists idx_messages_conversation on Messages(conversation_id, sent_at DESC);
create index if not exists idx_messages_sender on Messages(sender_id);
create index if not exists idx_msg_status_user on MessageStatuses(member_id, read_at);
create index if not exists idx_users_email on Users(email);
create index if not exists idx_users_username on Users(username);
create index if not exists idx_call_logs_conversation on CallLogs(conversation_id);
create index if not exists idx_call_participants_user on CallParticipants(participant_id);
create index if not exists idx_message_pins_conv on MessagePins(conversation_id);
create index if not exists idx_messages_expires_at on Messages(expires_at);

set foreign_key_checks = 1;
