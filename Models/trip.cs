using System;
using System.Collections.Generic;

namespace TripWise.Models;

public partial class Trip
{
    public int IdTrip { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public decimal TotalBudget { get; set; }

    public DateTime CreatedAt { get; set; }

    public int? CreatedById { get; set; }

    public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();

    public virtual User CreatedBy { get; set; } = null!;

    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();

    public virtual ICollection<Expense> Expenses { get; set; } = new List<Expense>();

    public virtual ICollection<PointsOfInterest> PointsOfInterests { get; set; } = new List<PointsOfInterest>();

    public virtual ICollection<TripParticipant> TripParticipants { get; set; } = new List<TripParticipant>();

    public virtual ICollection<VotingSystem> VotingSystems { get; set; } = new List<VotingSystem>();
}
