using System;
using System.Collections.Generic;

namespace TripWise.Models;

public partial class ExpenseCategory
{
    public int IdExpenseCategory { get; set; }
    public string? ExpenseCategoryName { get; set; }
    public int? TripId { get; set; } // NULL = глобальные категории

    // Навигационные свойства
    public virtual Trip? Trip { get; set; }
    public virtual ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}
