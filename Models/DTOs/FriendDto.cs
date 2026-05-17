// Models/DTOs/FriendDto.cs
using System;
using System.Collections.Generic;

namespace TripWise.Models.DTOs
{
    public class FriendDto
    {
        public int Id { get; set; }
        public int FriendId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? AvatarPath { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
    }

    public class FriendRequestDto
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string? SenderAvatar { get; set; }
        public string? Message { get; set; }
        public DateTime SentAt { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}