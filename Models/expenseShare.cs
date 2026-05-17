using System;
using System.Collections.Generic;

namespace TripWise.Models;

public partial class ExpenseShare
{
    public int IdExpenseShare { get; set; }

    public int IdExpense { get; set; }

    public int IdUser { get; set; }

    public decimal ShareAmount { get; set; }

    public bool IsPaid { get; set; }

    public virtual Expense IdExpenseNavigation { get; set; } = null!;

    public virtual User IdUserNavigation { get; set; } = null!;
}
