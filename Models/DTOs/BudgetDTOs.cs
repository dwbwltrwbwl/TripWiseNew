namespace TripWise.Models.DTOs
{
    public class BudgetSummaryDto
    {
        public decimal TotalBudget { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal Remaining => TotalBudget - TotalSpent;
        public decimal MyTotalSpent { get; set; }  // ЛИЧНЫЕ РАСХОДЫ ПОЛЬЗОВАТЕЛЯ (ВСЕГО)
        public int TripCount { get; set; }
        public List<BudgetCategoryDto> Categories { get; set; } = new();
        public List<RecentExpenseDto> RecentExpenses { get; set; } = new();
        public List<TripBudgetDto> Trips { get; set; } = new();
    }

    public class BudgetCategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public decimal Budget { get; set; }
        public decimal Spent { get; set; }
        public decimal MySpent { get; set; }  // ЛИЧНЫЕ РАСХОДЫ ПОЛЬЗОВАТЕЛЯ ПО КАТЕГОРИИ
        public decimal Remaining => Budget - Spent;
        public int Percentage => Budget > 0 ? (int)((Spent / Budget) * 100) : 0;
        public string Color { get; set; } = "#0379D9";
        public int ExpenseCount { get; set; }
        public int? TripId { get; set; }
    }

    public class RecentExpenseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public decimal Amount { get; set; }
        public DateTime ExpenseDate { get; set; }
        public string CategoryName { get; set; } = "";
        public string TripName { get; set; } = "";
        public int TripId { get; set; }
        public int CategoryId { get; set; }
        public string PaidByName { get; set; } = "";
        public int PaidById { get; set; }
        public string? Description { get; set; }
        public bool IsDebtPayment { get; set; }
        public decimal MyShareAmount { get; set; }  // ЛИЧНАЯ ДОЛЯ ПОЛЬЗОВАТЕЛЯ В ЭТОМ РАСХОДЕ
        public List<ExpenseShareDto> Shares { get; set; } = new();
    }

    public class ExpenseShareDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = "";
        public decimal Amount { get; set; }
        public bool IsPaid { get; set; }
    }

    public class TripBudgetDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal TotalBudget { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal MySpent { get; set; }  // ЛИЧНЫЕ РАСХОДЫ ПОЛЬЗОВАТЕЛЯ В ЭТОЙ ПОЕЗДКЕ
        public decimal Remaining => TotalBudget - TotalSpent;
        public int ParticipantCount { get; set; }
        public List<string> Participants { get; set; } = new();
        public List<BudgetCategoryDto> Categories { get; set; } = new();
    }

    public class CreateExpenseRequest
    {
        public int TripId { get; set; }
        public string Title { get; set; } = "";
        public decimal Amount { get; set; }
        public int CategoryId { get; set; }
        public DateTime ExpenseDate { get; set; }
        public int? PointId { get; set; }
        public List<int> SharedWith { get; set; } = new(); // ID пользователей, с кем разделить расход
        public List<ExpenseShareDto> Shares { get; set; } = new(); // ДЛЯ НЕРАВНОГО РАСПРЕДЕЛЕНИЯ
    }

    public class CreateCategoryRequest
    {
        public string Name { get; set; } = "";
        public decimal Budget { get; set; }
        public int? TripId { get; set; } // null - общая категория для всех поездок
    }

    public class UpdateExpenseShareRequest
    {
        public int ExpenseId { get; set; }
        public int UserId { get; set; }
        public bool IsPaid { get; set; }
    }

    public class UpdateExpenseRequest
    {
        public int ExpenseId { get; set; }
        public string Title { get; set; } = "";
        public decimal Amount { get; set; }
        public int CategoryId { get; set; }
        public DateTime ExpenseDate { get; set; }
        public List<int> SharedWith { get; set; } = new();
        public List<ExpenseShareDto> Shares { get; set; } = new();
    }

    public class DeleteExpenseRequest
    {
        public int ExpenseId { get; set; }
    }

    public class UpdateCategoryRequest
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = "";
    }

    public class DeleteCategoryRequest
    {
        public int CategoryId { get; set; }
    }

    public class MarkDebtAsPaidRequest
    {
        public int FromUserId { get; set; }
        public int ToUserId { get; set; }
        public decimal Amount { get; set; }
        public int TripId { get; set; }
    }

    public class ExpenseWithChatDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public decimal Amount { get; set; }
        public string CategoryName { get; set; } = "";
        public string TripName { get; set; } = "";
        public int TripId { get; set; }
        public string PaidByName { get; set; } = "";
        public int? ChatId { get; set; }
        public List<ExpenseShareDto> Shares { get; set; } = new();
    }

    public class DebtReminderDto
    {
        public int DebtorId { get; set; }
        public string DebtorName { get; set; } = "";
        public int CreditorId { get; set; }
        public string CreditorName { get; set; } = "";
        public decimal Amount { get; set; }
        public int TripId { get; set; }
        public string TripName { get; set; } = "";
        public int? ChatId { get; set; }
        public List<int> ExpenseIds { get; set; } = new();
    }

    public class SendDebtReminderRequest
    {
        public int FromUserId { get; set; }
        public int ToUserId { get; set; }
        public decimal Amount { get; set; }
        public int TripId { get; set; }
        public string Message { get; set; } = "";
    }
}