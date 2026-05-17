using System;
using System.Collections.Generic;

namespace TripWise.Models.DTOs
{
    public class ChatDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Type { get; set; } = string.Empty;
        public int? TripId { get; set; }
        public string? TripName { get; set; }
        public int CreatedById { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastMessageAt { get; set; }
        public int MemberCount { get; set; }
        public int UnreadCount { get; set; }
        public string? AvatarPath { get; set; }
        public LastMessageDto? LastMessage { get; set; }
    }

    public class LastMessageDto
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public int SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public string? AttachmentType { get; set; }
        public string? AttachmentName { get; set; }
    }

    public class ChatDetailDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Type { get; set; } = string.Empty;
        public int? TripId { get; set; }
        public string? TripName { get; set; }
        public DateTime CreatedAt { get; set; }
        public int CreatedById { get; set; }  // ← ДОБАВЬТЕ ЭТО
        public bool IsAdmin { get; set; }
        public UserDto? Creator { get; set; }
        public string? AvatarPath { get; set; }
        public List<ChatMemberDto> Members { get; set; } = new();
        public int TotalMessages { get; set; }
    }

    public class ChatMemberDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Role { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; }
        public DateTime? LastReadAt { get; set; }
        public string? AvatarPath { get; set; }
    }

    public class ChatMessageDto
    {
        public int Id { get; set; }
        public string Text { get; set; } = "";
        public int SenderId { get; set; }
        public string SenderName { get; set; } = "";
        public DateTime SentAt { get; set; }
        public DateTime? EditedAt { get; set; }
        public int? ReplyToId { get; set; }
        public ReplyMessageDto? ReplyTo { get; set; }

        public string? AttachmentType { get; set; }
        public string? AttachmentUrl { get; set; }
        public string? AttachmentName { get; set; }
        public long? AttachmentSize { get; set; }
        public List<AttachmentDto>? Attachments { get; set; }

        // ДОБАВИТЬ ЭТИ ДВЕ СТРОКИ:
        public string? AttachmentsJson { get; set; }  // ← ДОЛЖНО БЫТЬ
        public bool IsVote { get; set; }
        public string? VoteDataJson { get; set; }

        public bool IsOutgoing { get; set; }
        public List<int> ReadBy { get; set; } = new();
    }

    public class ReplyMessageDto
    {
        public int Id { get; set; }
        public string Text { get; set; } = "";
        public int SenderId { get; set; }
        public string SenderName { get; set; } = "";
        public string? AttachmentType { get; set; }
        public List<AttachmentDto>? Attachments { get; set; }
        public bool HasAttachment { get; set; }
    }
    public class AttachmentDto
    {
        public string FileName { get; set; } = "";
        public string FileUrl { get; set; } = "";
        public long FileSize { get; set; }
        public string FileType { get; set; } = "";
    }
    public class UserDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string Email { get; set; } = string.Empty;
        public int? Age { get; set; }
    }

    public class CreateChatRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string Type { get; set; } = "group";
        public int? TripId { get; set; }
        public List<int> UserIds { get; set; } = new();
    }

    public class SendMessageRequest
    {
        public int ChatId { get; set; }
        public string Text { get; set; } = "";
        public int? ReplyToId { get; set; }

        // Для нескольких файлов
        public List<FileAttachment>? Attachments { get; set; }
    }

    public class FileAttachment
    {
        public string FileName { get; set; } = "";
        public string FileUrl { get; set; } = "";
        public long FileSize { get; set; }
        public string FileType { get; set; } = "";
    }

    public class AddMemberRequest
    {
        public int ChatId { get; set; }
        public int UserId { get; set; }
    }

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
    }
}