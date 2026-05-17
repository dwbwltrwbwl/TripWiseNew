using System;
using System.Collections.Generic;

namespace TripWise.Models;

public partial class Expense
{
    public int IdExpense { get; set; }

    public string Title { get; set; } = null!;

    public decimal Amount { get; set; }

    public int IdExpenseCategory { get; set; }

    public DateTime ExpenseDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public int IdTrip { get; set; }

    public int? PaidById { get; set; }

    public int? IdPoint { get; set; }

    public virtual ICollection<ExpenseShare> ExpenseShares { get; set; } = new List<ExpenseShare>();

    public virtual ExpenseCategory IdExpenseCategoryNavigation { get; set; } = null!;

    public virtual PointsOfInterest? IdPointNavigation { get; set; }

    public virtual Trip IdTripNavigation { get; set; } = null!;

    public virtual User PaidBy { get; set; } = null!;
}
