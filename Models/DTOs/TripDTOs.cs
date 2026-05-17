namespace TripWise.Models.DTOs
{
    public class TripListDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal TotalBudget { get; set; }
        public int Duration => (EndDate - StartDate).Days;
        public string Status { get; set; } = ""; // upcoming, active, completed
        public int ParticipantCount { get; set; }
        public List<TripParticipantDto> Participants { get; set; } = new();
        public int? ChatId { get; set; }
        public bool HasChat => ChatId.HasValue;
        public string? CoverImage { get; set; }
        public DateTime CreatedAt { get; set; }
        public TripCreatorDto CreatedBy { get; set; } = new();
        public int PointsCount { get; set; }
        public decimal SpentBudget { get; set; }
        public decimal RemainingBudget => TotalBudget - SpentBudget;
    }

    public class TripParticipantDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = "";
        public string? AvatarPath { get; set; }
        public string Role { get; set; } = "";
        public bool IsFriend { get; set; }
    }

    public class TripCreatorDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = "";
        public string? AvatarPath { get; set; }
    }

    public class CreateTripRequest
    {
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal TotalBudget { get; set; }
        public bool IsPublic { get; set; }
        public List<int>? InvitedFriends { get; set; }
    }

    public class TripDetailDto : TripListDto
    {
        public List<PointOfInterestDto> Points { get; set; } = new();
        public List<ExpenseDto> Expenses { get; set; } = new();
        public List<TripMessageDto> RecentMessages { get; set; } = new();
    }

    public class PointOfInterestDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public decimal? Cost { get; set; }
        public DateTime? PlannedDate { get; set; }
        public string? Category { get; set; }
    }

    public class ExpenseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public decimal Amount { get; set; }
        public string Category { get; set; } = "";
        public DateTime Date { get; set; }
        public string PaidBy { get; set; } = "";
    }

    public class TripMessageDto
    {
        public int Id { get; set; }
        public string Text { get; set; } = "";
        public string SenderName { get; set; } = "";
        public DateTime SentAt { get; set; }
    }

    public class InviteFriendsRequest
    {
        public int TripId { get; set; }
        public List<int> FriendIds { get; set; } = new();
        public string? Message { get; set; }
    }

    public class TripInvitationDto
    {
        public int Id { get; set; }
        public int TripId { get; set; }
        public string TripTitle { get; set; } = "";
        public int InviterId { get; set; }
        public string InviterName { get; set; } = "";
        public string? InviterAvatar { get; set; }
        public int InvitedId { get; set; }
        public string? Message { get; set; }
        public DateTime InvitedAt { get; set; }
        public DateTime? RespondedAt { get; set; }
        public string Status { get; set; } = ""; // pending, accepted, declined
        public int? ChatId { get; set; }
    }
    public class UpdateTripRequest
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal TotalBudget { get; set; }
        public bool IsPublic { get; set; }
    }
    public class ManageParticipantRequest
    {
        public int TripId { get; set; }
        public int UserId { get; set; }
    }

    public class TripParticipantManageDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = "";
        public string? AvatarPath { get; set; }
        public string Role { get; set; } = "";
        public bool IsFriend { get; set; }
        public bool IsCreator { get; set; }
        public bool IsCurrentUser { get; set; }
        public DateTime JoinedAt { get; set; }
    }

    public class SendTripInvitationRequest
    {
        public int TripId { get; set; }
        public int FriendId { get; set; }
        public string? Message { get; set; }
    }

    public class RespondToInvitationRequest
    {
        public int InvitationId { get; set; }
        public bool Accept { get; set; }
    }
}