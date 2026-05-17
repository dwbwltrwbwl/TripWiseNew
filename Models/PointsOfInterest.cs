using System;
using System.Collections.Generic;

namespace TripWise.Models;

public partial class PointsOfInterest
{
    public int IdPoint { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }

    public string? Address { get; set; }

    public int IdInterestCategory { get; set; }

    public DateTime? PlannedDate { get; set; }

    public TimeOnly? PlannedTime { get; set; }

    public decimal? Cost { get; set; }

    public string? BookingLink { get; set; }

    public string? Notes { get; set; }

    public int IdTrip { get; set; }

    public int? AddedById { get; set; }

    public virtual User? AddedBy { get; set; }

    public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();

    public virtual ICollection<Expense> Expenses { get; set; } = new List<Expense>();

    public virtual InterestCategory IdInterestCategoryNavigation { get; set; } = null!;

    public virtual Trip IdTripNavigation { get; set; } = null!;

    public virtual ICollection<VotingSystem> VotingSystems { get; set; } = new List<VotingSystem>();
}
