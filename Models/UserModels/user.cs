using System;
using System.Collections.Generic;

namespace TripWise.Models;

public partial class User
{
    public int IdUser { get; set; }

    public string LastName { get; set; } = null!;      // ← вместо Name
    public string FirstName { get; set; } = null!;     // ← новое
    public string? MiddleName { get; set; }

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public int? Age { get; set; }

    public DateTime CreatedAt { get; set; }

    public int IdRole { get; set; }
    public string? AvatarPath { get; set; }
    // В модели User добавьте новое свойство
    public bool IsBlocked { get; set; } = false;

    public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();

    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
    public virtual ICollection<DocumentFolder> DocumentFolders { get; set; }
    public virtual ICollection<UserDocument> UserDocuments { get; set; }

    public virtual ICollection<ExpenseShare> ExpenseShares { get; set; } = new List<ExpenseShare>();

    public virtual ICollection<Expense> Expenses { get; set; } = new List<Expense>();

    public virtual Role IdRoleNavigation { get; set; } = null!;

    public virtual ICollection<PointsOfInterest> PointsOfInterests { get; set; } = new List<PointsOfInterest>();

    public virtual ICollection<TripParticipant> TripParticipants { get; set; } = new List<TripParticipant>();

    public virtual ICollection<Trip> Trips { get; set; } = new List<Trip>();

    public virtual ICollection<UserVote> UserVotes { get; set; } = new List<UserVote>();

    public virtual ICollection<VotingSystem> VotingSystems { get; set; } = new List<VotingSystem>();
    public virtual ICollection<FavoriteFlight> FavoriteFlights { get; set; } = new List<FavoriteFlight>();
    public virtual ICollection<Chat> CreatedChats { get; set; } = new List<Chat>();
    public virtual ICollection<ChatMember> ChatMemberships { get; set; } = new List<ChatMember>();

    public virtual ICollection<ChatMessageRead> MessageReads { get; set; } = new List<ChatMessageRead>();
    public virtual ICollection<PlannedActivity> PlannedActivities { get; set; }
}
