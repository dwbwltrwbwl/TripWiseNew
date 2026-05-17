// Models/DTOs/SearchUsersResponse.cs
namespace TripWise.Models.DTOs
{
    public class SearchUsersResponse
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? AvatarPath { get; set; }
        public bool IsFriend { get; set; }
        public string? FriendStatus { get; set; } // friend, pending_sent, pending_received, none
    }
}