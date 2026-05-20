using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWise.Models;
using TripWise.Models.DTOs;
using System.Security.Claims;
//using Microsoft.Data.SqlClient;

namespace TripWise.Controllers
{
    public class BudgetController : Controller
    {
        private readonly TripWiseContext _context;
        private readonly ILogger<BudgetController> _logger;
        private static readonly Dictionary<string, DateTime> _paidDebtCache = new Dictionary<string, DateTime>();

        public BudgetController(TripWiseContext context, ILogger<BudgetController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public IActionResult Index()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }
            return View();
        }

        // GET: /Budget/GetSummary
        [HttpGet]
        public async Task<IActionResult> GetSummary()
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<BudgetSummaryDto>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                _logger.LogInformation("GetSummary для пользователя {UserId}", userId);

                // Получаем все поездки, где пользователь является участником
                var userTrips = await _context.TripParticipants
                    .Where(tp => tp.IdUser == userId)
                    .Select(tp => tp.IdTrip)
                    .ToListAsync();

                if (!userTrips.Any())
                {
                    return Json(new ApiResponse<BudgetSummaryDto>
                    {
                        Success = true,
                        Data = new BudgetSummaryDto
                        {
                            TotalBudget = 0,
                            TotalSpent = 0,
                            MyTotalSpent = 0,
                            TripCount = 0,
                            Categories = new List<BudgetCategoryDto>(),
                            RecentExpenses = new List<RecentExpenseDto>(),
                            Trips = new List<TripBudgetDto>()
                        }
                    });
                }

                // Получаем расходы для этих поездок
                var expenses = await _context.Expenses
                    .Include(e => e.IdExpenseCategoryNavigation)
                    .Include(e => e.IdTripNavigation)
                    .Include(e => e.PaidBy)
                    .Include(e => e.ExpenseShares)
                        .ThenInclude(es => es.IdUserNavigation)
                    .Where(e => userTrips.Contains(e.IdTrip))
                    .OrderByDescending(e => e.ExpenseDate)
                    .ToListAsync();

                // РАССЧИТЫВАЕМ ЛИЧНЫЕ РАСХОДЫ ПОЛЬЗОВАТЕЛЯ (ВСЕГО)
                decimal myTotalSpent = 0;
                foreach (var expense in expenses)
                {
                    var myShare = expense.ExpenseShares
                        .Where(es => es.IdUser == userId)
                        .Sum(es => es.ShareAmount);
                    myTotalSpent += myShare;
                }

                // Получаем категории расходов
                var categories = await _context.ExpenseCategories
                    .Where(c => c.TripId == null || userTrips.Contains(c.TripId.Value))
                    .ToListAsync();

                // Информация о поездках (с личными расходами для каждой)
                var trips = new List<TripBudgetDto>();
                foreach (var tripId in userTrips)
                {
                    var trip = await _context.Trips.FindAsync(tripId);
                    if (trip != null)
                    {
                        var tripExpenses = expenses.Where(e => e.IdTrip == tripId).ToList();
                        var participants = await _context.TripParticipants
                            .Where(tp => tp.IdTrip == tripId)
                            .Select(tp => tp.IdUserNavigation.LastName + " " + tp.IdUserNavigation.FirstName)
                            .ToListAsync();

                        // ЛИЧНЫЕ РАСХОДЫ ПОЛЬЗОВАТЕЛЯ В ЭТОЙ ПОЕЗДКЕ
                        decimal myTripSpent = 0;
                        foreach (var expense in tripExpenses)
                        {
                            var myShare = expense.ExpenseShares
                                .Where(es => es.IdUser == userId)
                                .Sum(es => es.ShareAmount);
                            myTripSpent += myShare;
                        }

                        trips.Add(new TripBudgetDto
                        {
                            Id = trip.IdTrip,
                            Title = trip.Title ?? "Без названия",
                            StartDate = trip.StartDate,
                            EndDate = trip.EndDate,
                            TotalBudget = trip.TotalBudget,
                            TotalSpent = tripExpenses.Sum(e => e.Amount),
                            MySpent = myTripSpent,  // ДОБАВИТЬ В DTO
                            ParticipantCount = participants.Count,
                            Participants = participants
                        });
                    }
                }

                // Категории с суммами
                var categoryDtos = new List<BudgetCategoryDto>();
                foreach (var cat in categories)
                {
                    var categoryExpenses = expenses.Where(e => e.IdExpenseCategory == cat.IdExpenseCategory).ToList();
                    var spent = categoryExpenses.Sum(e => e.Amount);
                    var expenseCount = categoryExpenses.Count;

                    // ЛИЧНЫЕ РАСХОДЫ ПОЛЬЗОВАТЕЛЯ ПО КАТЕГОРИИ
                    decimal myCategorySpent = 0;
                    foreach (var expense in categoryExpenses)
                    {
                        var myShare = expense.ExpenseShares
                            .Where(es => es.IdUser == userId)
                            .Sum(es => es.ShareAmount);
                        myCategorySpent += myShare;
                    }

                    categoryDtos.Add(new BudgetCategoryDto
                    {
                        Id = cat.IdExpenseCategory,
                        Name = cat.ExpenseCategoryName ?? "Без категории",
                        Budget = 0,
                        Spent = spent,
                        MySpent = myCategorySpent,  // ДОБАВИТЬ В DTO
                        Color = GetCategoryColor(cat.ExpenseCategoryName ?? ""),
                        ExpenseCount = expenseCount,
                        TripId = cat.TripId
                    });
                }

                // Последние расходы (с личной долей пользователя)
                var recentExpenses = expenses.Select(e => new RecentExpenseDto
                {
                    Id = e.IdExpense,
                    Title = e.Title ?? "Без названия",
                    Amount = e.Amount,
                    ExpenseDate = e.ExpenseDate,
                    CategoryName = e.IdExpenseCategoryNavigation?.ExpenseCategoryName ?? "Другое",
                    CategoryId = e.IdExpenseCategory,
                    TripName = e.IdTripNavigation?.Title ?? "Поездка",
                    TripId = e.IdTrip,
                    PaidByName = e.PaidBy != null ? $"{e.PaidBy.LastName} {e.PaidBy.FirstName}".Trim() : "Неизвестно",
                    PaidById = e.PaidById ?? 0,
                    IsDebtPayment = e.Title != null && e.Title.StartsWith("💰") && e.IdExpenseCategory == 0,
                    MyShareAmount = e.ExpenseShares.Where(es => es.IdUser == userId).Sum(es => es.ShareAmount),  // ДОБАВИТЬ
                    Shares = e.ExpenseShares.Select(es => new ExpenseShareDto
                    {
                        UserId = es.IdUser,
                        UserName = es.IdUserNavigation != null ? $"{es.IdUserNavigation.LastName} {es.IdUserNavigation.FirstName}".Trim() : "Неизвестно",
                        Amount = es.ShareAmount,
                        IsPaid = es.IsPaid
                    }).ToList()
                }).ToList();

                var summary = new BudgetSummaryDto
                {
                    TotalBudget = trips.Sum(t => t.TotalBudget),
                    TotalSpent = expenses.Sum(e => e.Amount),
                    MyTotalSpent = myTotalSpent,  // ДОБАВИТЬ
                    TripCount = trips.Count,
                    Categories = categoryDtos.OrderByDescending(c => c.Spent).ToList(),
                    RecentExpenses = recentExpenses,
                    Trips = trips
                };

                return Json(new ApiResponse<BudgetSummaryDto>
                {
                    Success = true,
                    Data = summary
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении сводки бюджета");
                return Json(new ApiResponse<BudgetSummaryDto>
                {
                    Success = false,
                    Message = "Ошибка при загрузке данных: " + ex.Message
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddExpense([FromBody] CreateExpenseRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                    return Json(new ApiResponse<object> { Success = false, Message = "Не авторизован" });

                // Получаем информацию о поездке
                var trip = await _context.Trips.FindAsync(request.TripId);
                if (trip == null)
                    return Json(new ApiResponse<object> { Success = false, Message = "Поездка не найдена" });

                // ПРОВЕРКА ДАТЫ РАСХОДА
                var expenseDate = request.ExpenseDate.Date;
                var tripStart = trip.StartDate.Date;
                var tripEnd = trip.EndDate.Date;

                if (expenseDate < tripStart || expenseDate > tripEnd)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = $"Расход можно добавить только в период поездки: {tripStart:dd.MM.yyyy} - {tripEnd:dd.MM.yyyy}"
                    });
                }

                // Проверяем участие
                var isParticipant = await _context.TripParticipants
                    .AnyAsync(tp => tp.IdTrip == request.TripId && tp.IdUser == userId);
                if (!isParticipant)
                    return Json(new ApiResponse<object> { Success = false, Message = "Вы не участник" });

                // Создаем расход
                var expense = new Expense
                {
                    Title = request.Title,
                    Amount = request.Amount,
                    IdExpenseCategory = request.CategoryId,
                    ExpenseDate = request.ExpenseDate.ToUniversalTime(),
                    CreatedAt = DateTime.UtcNow,
                    IdTrip = request.TripId,
                    PaidById = userId.Value
                };

                _context.Expenses.Add(expense);
                await _context.SaveChangesAsync();

                // НЕРАВНОЕ РАСПРЕДЕЛЕНИЕ
                // В AddExpense контроллере:
                if (request.Shares != null && request.Shares.Any())
                {
                    // Неравное распределение - используем только то, что пришло от клиента
                    // НЕ ДОБАВЛЯЕМ плательщика автоматически!
                    // НЕ ДОБАВЛЯЕМ участников с нулевой долей!
                    foreach (var share in request.Shares.Where(s => s.Amount > 0)) // <-- ТОЛЬКО ТЕ, У КОГО ДОЛЯ > 0
                    {
                        var expenseShare = new ExpenseShare
                        {
                            IdExpense = expense.IdExpense,
                            IdUser = share.UserId,
                            ShareAmount = share.Amount,
                            IsPaid = share.UserId == userId
                        };
                        _context.ExpenseShares.Add(expenseShare);
                    }
                }
                else if (request.SharedWith != null && request.SharedWith.Any())
                {
                    // Равное распределение - добавляем всех участников + плательщика
                    var allParticipants = request.SharedWith.Distinct().ToList();
                    if (!allParticipants.Contains(userId.Value))
                        allParticipants.Add(userId.Value);
                    var shareAmount = request.Amount / allParticipants.Count;

                    foreach (var participantId in allParticipants)
                    {
                        var expenseShare = new ExpenseShare
                        {
                            IdExpense = expense.IdExpense,
                            IdUser = participantId,
                            ShareAmount = shareAmount,
                            IsPaid = participantId == userId
                        };
                        _context.ExpenseShares.Add(expenseShare);
                    }
                }
                else
                {
                    var expenseShare = new ExpenseShare
                    {
                        IdExpense = expense.IdExpense,
                        IdUser = userId.Value,
                        ShareAmount = request.Amount,
                        IsPaid = true
                    };
                    _context.ExpenseShares.Add(expenseShare);
                }

                await _context.SaveChangesAsync();

                return Json(new ApiResponse<object> { Success = true, Message = "Расход добавлен" });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse<object> { Success = false, Message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCategory([FromBody] CreateCategoryRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                    return Json(new ApiResponse<object> { Success = false, Message = "Пользователь не авторизован" });

                if (request.TripId == null || request.TripId == 0)
                    return Json(new ApiResponse<object> { Success = false, Message = "Необходимо указать поездку" });

                var isParticipant = await _context.TripParticipants
                    .AnyAsync(tp => tp.IdTrip == request.TripId && tp.IdUser == userId);
                if (!isParticipant)
                    return Json(new ApiResponse<object> { Success = false, Message = "Вы не участник этой поездки" });

                // ПРОВЕРКА НА СУЩЕСТВУЮЩУЮ КАТЕГОРИЮ (без учета регистра)
                var existingCategory = await _context.ExpenseCategories
                    .FirstOrDefaultAsync(c => c.ExpenseCategoryName.ToLower() == request.Name.ToLower() && c.TripId == request.TripId);
                if (existingCategory != null)
                    return Json(new ApiResponse<object> { Success = false, Message = "Категория уже существует в этой поездке" });

                var category = new ExpenseCategory
                {
                    ExpenseCategoryName = request.Name.Trim(),
                    TripId = request.TripId
                };

                _context.ExpenseCategories.Add(category);
                await _context.SaveChangesAsync();

                return Json(new ApiResponse<object> { Success = true, Message = "Категория добавлена" });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse<object> { Success = false, Message = ex.Message });
            }
        }

        // POST: /Budget/MarkShareAsPaid
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkShareAsPaid([FromBody] UpdateExpenseShareRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                var share = await _context.ExpenseShares
                    .FirstOrDefaultAsync(es => es.IdExpense == request.ExpenseId && es.IdUser == request.UserId);

                if (share == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Доля не найдена"
                    });
                }

                share.IsPaid = request.IsPaid;
                await _context.SaveChangesAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = request.IsPaid ? "Доля отмечена как оплаченная" : "Доля отмечена как неоплаченная"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении статуса оплаты");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при обновлении статуса: " + ex.Message
                });
            }
        }

        // GET: /Budget/GetTripParticipants?tripId=5
        [HttpGet]
        public async Task<IActionResult> GetTripParticipants(int tripId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                var participants = await _context.TripParticipants
                    .Include(tp => tp.IdUserNavigation)
                    .Where(tp => tp.IdTrip == tripId)
                    .Select(tp => new
                    {
                        tp.IdUser,
                        FullName = tp.IdUserNavigation.LastName + " " + tp.IdUserNavigation.FirstName,
                        tp.IdUserNavigation.AvatarPath
                    })
                    .ToListAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Data = participants
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении участников поездки");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при загрузке участников: " + ex.Message
                });
            }
        }

        private string GetCategoryColor(string categoryName)
        {
            var colors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Транспорт"] = "#0379D9",
                ["Проживание"] = "#40B624",
                ["Питание"] = "#FF6B6B",
                ["Развлечения"] = "#FFC107",
                ["Шоппинг"] = "#6F42C1",
                ["Экскурсии"] = "#17A2B8",
                ["Другое"] = "#6c757d"
            };

            return colors.ContainsKey(categoryName) ? colors[categoryName] : "#6c757d";
        }
        // GET: /Budget/GetExpensesForChat?chatId=5
        [HttpGet]
        public async Task<IActionResult> GetExpensesForChat(int chatId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                // Получаем поездку по chatId
                var chat = await _context.Chats
                    .FirstOrDefaultAsync(c => c.IdChat == chatId && c.Type == "trip");

                if (chat == null || !chat.IdTrip.HasValue)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = true,
                        Data = new List<ExpenseWithChatDto>()
                    });
                }

                // Получаем расходы для этой поездки
                var expenses = await _context.Expenses
                    .Include(e => e.IdExpenseCategoryNavigation)
                    .Include(e => e.IdTripNavigation)
                    .Include(e => e.PaidBy)
                    .Include(e => e.ExpenseShares)
                        .ThenInclude(es => es.IdUserNavigation)
                    .Where(e => e.IdTrip == chat.IdTrip)
                    .OrderByDescending(e => e.ExpenseDate)
                    .Take(20)
                    .Select(e => new ExpenseWithChatDto
                    {
                        Id = e.IdExpense,
                        Title = e.Title ?? "Без названия",
                        Amount = e.Amount,
                        CategoryName = e.IdExpenseCategoryNavigation != null
                            ? e.IdExpenseCategoryNavigation.ExpenseCategoryName ?? "Другое"
                            : "Другое",
                        TripName = e.IdTripNavigation != null ? e.IdTripNavigation.Title ?? "Поездка" : "Поездка",
                        TripId = e.IdTrip,
                        PaidByName = e.PaidBy != null
                            ? $"{e.PaidBy.LastName} {e.PaidBy.FirstName}".Trim()
                            : "Неизвестно",
                        ChatId = chatId,
                        Shares = e.ExpenseShares.Select(es => new ExpenseShareDto
                        {
                            UserId = es.IdUser,
                            UserName = es.IdUserNavigation != null
                                ? $"{es.IdUserNavigation.LastName} {es.IdUserNavigation.FirstName}".Trim()
                                : "Неизвестно",
                            Amount = es.ShareAmount,
                            IsPaid = es.IsPaid
                        }).ToList()
                    })
                    .ToListAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Data = expenses
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении расходов для чата {ChatId}", chatId);
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при загрузке расходов: " + ex.Message
                });
            }
        }

        // GET: /Budget/GetDebtsForChat?chatId=5
        [HttpGet]
        public async Task<IActionResult> GetDebtsForChat(int chatId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                // Получаем поездку по chatId
                var chat = await _context.Chats
                    .FirstOrDefaultAsync(c => c.IdChat == chatId && c.Type == "trip");

                if (chat == null || !chat.IdTrip.HasValue)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = true,
                        Data = new List<DebtReminderDto>()
                    });
                }

                var tripId = chat.IdTrip.Value;

                // Получаем все расходы для этой поездки
                var expenses = await _context.Expenses
                    .Include(e => e.ExpenseShares)
                    .Where(e => e.IdTrip == tripId)
                    .ToListAsync();

                // Получаем всех участников поездки
                var participants = await _context.TripParticipants
                    .Include(tp => tp.IdUserNavigation)
                    .Where(tp => tp.IdTrip == tripId)
                    .ToDictionaryAsync(tp => tp.IdUser, tp => $"{tp.IdUserNavigation.LastName} {tp.IdUserNavigation.FirstName}".Trim());

                // Рассчитываем балансы
                var balances = new Dictionary<int, decimal>();
                var expenseGroups = new Dictionary<int, List<int>>(); // Для группировки долгов по расходам

                foreach (var expense in expenses)
                {
                    var shares = await _context.ExpenseShares
                        .Where(es => es.IdExpense == expense.IdExpense)
                        .ToListAsync();

                    foreach (var share in shares)
                    {
                        if (!balances.ContainsKey(share.IdUser))
                            balances[share.IdUser] = 0;

                        if (!expenseGroups.ContainsKey(share.IdUser))
                            expenseGroups[share.IdUser] = new List<int>();

                        if (share.IdUser == expense.PaidById)
                        {
                            // Тот, кто заплатил, должен получить деньги
                            balances[share.IdUser] += expense.Amount - share.ShareAmount;
                            expenseGroups[share.IdUser].Add(expense.IdExpense);
                        }
                        else
                        {
                            // Остальные должны
                            balances[share.IdUser] -= share.ShareAmount;
                            expenseGroups[share.IdUser].Add(expense.IdExpense);
                        }
                    }
                }

                // Формируем список долгов
                var debts = new List<DebtReminderDto>();
                var users = balances.Keys.ToList();

                for (int i = 0; i < users.Count; i++)
                {
                    for (int j = i + 1; j < users.Count; j++)
                    {
                        var user1 = users[i];
                        var user2 = users[j];

                        if (balances[user1] > 0 && balances[user2] < 0)
                        {
                            var amount = Math.Min(balances[user1], -balances[user2]);
                            if (amount > 0.01m)
                            {
                                debts.Add(new DebtReminderDto
                                {
                                    DebtorId = user2,
                                    DebtorName = participants.ContainsKey(user2) ? participants[user2] : "Неизвестно",
                                    CreditorId = user1,
                                    CreditorName = participants.ContainsKey(user1) ? participants[user1] : "Неизвестно",
                                    Amount = amount,
                                    TripId = tripId,
                                    TripName = chat.Name?.Replace("Чат: ", "") ?? "Поездка",
                                    ChatId = chatId,
                                    ExpenseIds = expenseGroups[user2].Intersect(expenseGroups[user1]).ToList()
                                });
                            }
                        }
                        else if (balances[user1] < 0 && balances[user2] > 0)
                        {
                            var amount = Math.Min(-balances[user1], balances[user2]);
                            if (amount > 0.01m)
                            {
                                debts.Add(new DebtReminderDto
                                {
                                    DebtorId = user1,
                                    DebtorName = participants.ContainsKey(user1) ? participants[user1] : "Неизвестно",
                                    CreditorId = user2,
                                    CreditorName = participants.ContainsKey(user2) ? participants[user2] : "Неизвестно",
                                    Amount = amount,
                                    TripId = tripId,
                                    TripName = chat.Name?.Replace("Чат: ", "") ?? "Поездка",
                                    ChatId = chatId,
                                    ExpenseIds = expenseGroups[user1].Intersect(expenseGroups[user2]).ToList()
                                });
                            }
                        }
                    }
                }

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Data = debts
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении долгов для чата {ChatId}", chatId);
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при загрузке долгов: " + ex.Message
                });
            }
        }

        // POST: /Budget/SendDebtReminder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendDebtReminder([FromBody] SendDebtReminderRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                // Проверяем, что пользователь - либо должник, либо кредитор
                if (userId != request.FromUserId && userId != request.ToUserId)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Вы не можете отправлять напоминания по этому долгу"
                    });
                }

                // Получаем чат поездки
                var chat = await _context.Chats
                    .FirstOrDefaultAsync(c => c.IdTrip == request.TripId && c.Type == "trip");

                if (chat == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Чат поездки не найден"
                    });
                }

                // Определяем текст сообщения в зависимости от того, кто отправляет
                string messageText;
                if (request.FromUserId == userId)
                {
                    // Я должен другому
                    messageText = $"🔔 Напоминание о долге: Я должен {request.Amount} ₽";
                }
                else if (request.ToUserId == userId)
                {
                    // Мне должны
                    messageText = $"🔔 Напоминание о долге: Мне должны {request.Amount} ₽";
                }
                else
                {
                    // Кто-то другой напоминает кому-то
                    messageText = $"🔔 Напоминание о долге: {request.Amount} ₽";
                }

                // Создаем сообщение-напоминание в чате
                var message = new ChatMessage
                {
                    Message = messageText,
                    SentAt = DateTime.UtcNow,
                    SenderId = userId.Value, // Используем SenderId вместо IdUser
                    ChatId = chat.IdChat, // Используем ChatId вместо IdChat
                    AttachmentType = "reminder",
                    AttachmentsJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        type = "debt_reminder",
                        fromUserId = request.FromUserId,
                        toUserId = request.ToUserId,
                        amount = request.Amount,
                        tripId = request.TripId
                    })
                };

                _context.ChatMessages.Add(message);
                await _context.SaveChangesAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Напоминание отправлено в чат"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отправке напоминания о долге");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при отправке напоминания: " + ex.Message
                });
            }
        }

        // POST: /Budget/CreateExpenseFromChat
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateExpenseFromChat([FromBody] CreateExpenseRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                // Создаем расход (как в обычном AddExpense)
                var expense = new Expense
                {
                    Title = request.Title,
                    Amount = request.Amount,
                    IdExpenseCategory = request.CategoryId,
                    ExpenseDate = request.ExpenseDate.ToUniversalTime(),
                    CreatedAt = DateTime.UtcNow,
                    IdTrip = request.TripId,
                    PaidById = userId.Value,
                    IdPoint = request.PointId
                };

                _context.Expenses.Add(expense);
                await _context.SaveChangesAsync();

                // Добавляем доли
                if (request.SharedWith != null && request.SharedWith.Any())
                {
                    var allParticipants = request.SharedWith.Distinct().ToList();
                    if (!allParticipants.Contains(userId.Value))
                    {
                        allParticipants.Add(userId.Value);
                    }

                    var shareAmount = request.Amount / allParticipants.Count;

                    foreach (var participantId in allParticipants)
                    {
                        var share = new ExpenseShare
                        {
                            IdExpense = expense.IdExpense,
                            IdUser = participantId,
                            ShareAmount = shareAmount,
                            IsPaid = participantId == userId.Value
                        };
                        _context.ExpenseShares.Add(share);
                    }

                    await _context.SaveChangesAsync();
                }

                // Отправляем уведомление в чат
                var chat = await _context.Chats
                    .FirstOrDefaultAsync(c => c.IdTrip == request.TripId && c.Type == "trip");

                if (chat != null)
                {
                    var participantNames = new List<string>();
                    if (request.SharedWith != null)
                    {
                        var participants = await _context.Users
                            .Where(u => request.SharedWith.Contains(u.IdUser))
                            .Select(u => $"{u.FirstName} {u.LastName}")
                            .ToListAsync();
                        participantNames = participants;
                    }

                    var shareText = participantNames.Any()
                        ? $" (разделено с: {string.Join(", ", participantNames)})"
                        : "";

                    var message = new ChatMessage
                    {
                        Message = $"💰 Новый расход: {request.Title} - {request.Amount} ₽{shareText}",
                        SentAt = DateTime.UtcNow,
                        SenderId = userId.Value, // Используем SenderId
                        ChatId = chat.IdChat, // Используем ChatId
                        AttachmentType = "expense",
                        AttachmentsJson = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            expenseId = expense.IdExpense,
                            amount = request.Amount,
                            title = request.Title,
                            categoryId = request.CategoryId
                        })
                    };

                    _context.ChatMessages.Add(message);
                    await _context.SaveChangesAsync();
                }

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Расход добавлен и уведомление отправлено в чат",
                    Data = new { expenseId = expense.IdExpense }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании расхода из чата");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при создании расхода: " + ex.Message
                });
            }
        }
        // POST: /Budget/UpdateExpense
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateExpense([FromBody] UpdateExpenseRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<object> { Success = false, Message = "Пользователь не авторизован" });
                }

                _logger.LogInformation("UpdateExpense: expenseId={ExpenseId}, userId={UserId}, newAmount={Amount}",
                    request.ExpenseId, userId, request.Amount);

                // Находим расход
                var expense = await _context.Expenses
                    .Include(e => e.ExpenseShares)
                    .FirstOrDefaultAsync(e => e.IdExpense == request.ExpenseId);

                if (expense == null)
                {
                    return Json(new ApiResponse<object> { Success = false, Message = "Расход не найден" });
                }

                // Проверяем, что пользователь является создателем расхода
                if (expense.PaidById != userId)
                {
                    return Json(new ApiResponse<object> { Success = false, Message = "Только создатель может редактировать расход" });
                }

                // Обновляем основные данные расхода (ВСЕГДА)
                expense.Title = request.Title;
                expense.Amount = request.Amount;
                expense.IdExpenseCategory = request.CategoryId;
                expense.ExpenseDate = request.ExpenseDate.ToUniversalTime();

                // ВАЖНО: Сохраняем изменения в любом случае!
                // Если нужно обновить доли - делаем это, иначе просто сохраняем основные данные
                if (request.Shares != null && request.Shares.Any())
                {
                    // Обновление долей при неравном распределении
                    var oldShares = _context.ExpenseShares.Where(es => es.IdExpense == request.ExpenseId);
                    _context.ExpenseShares.RemoveRange(oldShares);

                    foreach (var share in request.Shares.Where(s => s.Amount > 0))
                    {
                        var newShare = new ExpenseShare
                        {
                            IdExpense = expense.IdExpense,
                            IdUser = share.UserId,
                            ShareAmount = share.Amount,
                            IsPaid = share.UserId == userId
                        };
                        _context.ExpenseShares.Add(newShare);
                    }
                }
                else if (request.SharedWith != null && request.SharedWith.Any())
                {
                    // Обновление долей при равном распределении
                    var oldShares = _context.ExpenseShares.Where(es => es.IdExpense == request.ExpenseId);
                    _context.ExpenseShares.RemoveRange(oldShares);

                    var allParticipants = request.SharedWith.Distinct().ToList();
                    if (!allParticipants.Contains(userId.Value))
                    {
                        allParticipants.Add(userId.Value);
                    }
                    var shareAmount = request.Amount / allParticipants.Count;

                    foreach (var participantId in allParticipants)
                    {
                        var newShare = new ExpenseShare
                        {
                            IdExpense = expense.IdExpense,
                            IdUser = participantId,
                            ShareAmount = shareAmount,
                            IsPaid = participantId == userId
                        };
                        _context.ExpenseShares.Add(newShare);
                    }
                }
                else
                {
                    // Если доли не переданы - пересчитываем их на основе старого распределения, но с новой суммой
                    var oldShares = expense.ExpenseShares.ToList();
                    var totalOldShares = oldShares.Sum(s => s.ShareAmount);

                    if (totalOldShares > 0)
                    {
                        // Пересчитываем доли пропорционально новой сумме
                        foreach (var share in oldShares)
                        {
                            share.ShareAmount = (share.ShareAmount / totalOldShares) * request.Amount;
                        }
                    }
                }

                // ОБЯЗАТЕЛЬНО сохраняем изменения
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Expense {request.ExpenseId} updated successfully. New amount: {request.Amount}");

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Расход успешно обновлен"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении расхода {ExpenseId}", request?.ExpenseId);
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при обновлении расхода: " + ex.Message
                });
            }
        }

        // POST: /Budget/DeleteExpense
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteExpense([FromBody] DeleteExpenseRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                _logger.LogInformation("DeleteExpense: expenseId={ExpenseId}, userId={UserId}",
                    request.ExpenseId, userId);

                // Находим расход
                var expense = await _context.Expenses
                    .Include(e => e.ExpenseShares)
                    .FirstOrDefaultAsync(e => e.IdExpense == request.ExpenseId);

                if (expense == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Расход не найден"
                    });
                }

                // Проверяем, что пользователь является создателем расхода
                if (expense.PaidById != userId)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Только создатель может удалить расход"
                    });
                }

                // Удаляем доли расхода
                var shares = _context.ExpenseShares.Where(es => es.IdExpense == request.ExpenseId);
                _context.ExpenseShares.RemoveRange(shares);

                // Удаляем расход
                _context.Expenses.Remove(expense);
                await _context.SaveChangesAsync();

                // Отправляем уведомление в чат об удалении
                var chat = await _context.Chats
                    .FirstOrDefaultAsync(c => c.IdTrip == expense.IdTrip && c.Type == "trip");

                if (chat != null)
                {
                    var message = new ChatMessage
                    {
                        Message = $"🗑️ Расход удален: {expense.Title}",
                        SentAt = DateTime.UtcNow,
                        SenderId = userId.Value,
                        ChatId = chat.IdChat,
                        AttachmentType = "expense_delete",
                        AttachmentsJson = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            expenseId = expense.IdExpense,
                            title = expense.Title
                        })
                    };

                    _context.ChatMessages.Add(message);
                    await _context.SaveChangesAsync();
                }

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Расход успешно удален"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении расхода {ExpenseId}", request?.ExpenseId);
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при удалении расхода: " + ex.Message
                });
            }
        }
        // POST: /Budget/UpdateCategory
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCategory([FromBody] UpdateCategoryRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                var category = await _context.ExpenseCategories
                    .FirstOrDefaultAsync(c => c.IdExpenseCategory == request.CategoryId);

                if (category == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Категория не найдена"
                    });
                }

                // Проверяем, не существует ли уже категория с таким именем
                var existingCategory = await _context.ExpenseCategories
                    .FirstOrDefaultAsync(c => c.ExpenseCategoryName == request.Name && c.IdExpenseCategory != request.CategoryId);

                if (existingCategory != null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Категория с таким названием уже существует"
                    });
                }

                category.ExpenseCategoryName = request.Name;
                await _context.SaveChangesAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Категория обновлена"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении категории");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при обновлении категории: " + ex.Message
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory([FromBody] DeleteCategoryRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                var category = await _context.ExpenseCategories.FindAsync(request.CategoryId);
                if (category == null)
                    return Json(new ApiResponse<object> { Success = false, Message = "Категория не найдена" });

                // Проверяем расходы ТОЛЬКО в этой поездке
                var expenseCount = await _context.Expenses
                    .CountAsync(e => e.IdExpenseCategory == request.CategoryId && e.IdTrip == category.TripId);

                if (expenseCount > 0)
                    return Json(new ApiResponse<object> { Success = false, Message = $"Нельзя удалить: {expenseCount} расходов" });

                _context.ExpenseCategories.Remove(category);
                await _context.SaveChangesAsync();

                return Json(new ApiResponse<object> { Success = true, Message = "Категория удалена" });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse<object> { Success = false, Message = ex.Message });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkDebtAsPaid([FromBody] MarkDebtAsPaidRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<object> { Success = false, Message = "Не авторизован" });
                }

                // Проверяем, что пользователь - либо должник, либо кредитор
                if (userId != request.FromUserId && userId != request.ToUserId)
                {
                    return Json(new ApiResponse<object> { Success = false, Message = "Вы не можете отметить этот долг" });
                }

                // Предотвращение повторных нажатий
                var cacheKey = $"{request.FromUserId}_{request.ToUserId}_{request.TripId}_{request.Amount}";
                if (_paidDebtCache.ContainsKey(cacheKey) &&
                    (DateTime.UtcNow - _paidDebtCache[cacheKey]).TotalSeconds < 30)
                {
                    return Json(new ApiResponse<object> { Success = true, Message = "Долг уже отмечен" });
                }

                _paidDebtCache[cacheKey] = DateTime.UtcNow;

                // Очистка кэша
                if (_paidDebtCache.Count > 100)
                {
                    var oldKeys = _paidDebtCache.Where(kvp => (DateTime.UtcNow - kvp.Value).TotalMinutes > 5).Select(kvp => kvp.Key).ToList();
                    foreach (var key in oldKeys)
                        _paidDebtCache.Remove(key);
                }

                // НАХОДИМ НЕОПЛАЧЕННУЮ ДОЛЮ ДОЛЖНИКА ПЕРЕД КОНКРЕТНЫМ КРЕДИТОРОМ
                // Получаем все расходы в этой поездке
                var tripExpenseIds = await _context.Expenses
                    .Where(e => e.IdTrip == request.TripId)
                    .Select(e => e.IdExpense)
                    .ToListAsync();

                // Находим долю, где должник (fromUserId) должен кредитору (toUserId)
                // и которая еще не оплачена
                var debtShare = await _context.ExpenseShares
                    .Join(_context.Expenses,
                          es => es.IdExpense,
                          e => e.IdExpense,
                          (es, e) => new { ExpenseShare = es, Expense = e })
                    .Where(x => tripExpenseIds.Contains(x.Expense.IdExpense) &&
                                x.ExpenseShare.IdUser == request.FromUserId &&
                                x.Expense.PaidById == request.ToUserId &&
                                x.ExpenseShare.ShareAmount > 0 &&
                                x.ExpenseShare.IsPaid == false)
                    .Select(x => x.ExpenseShare)
                    .FirstOrDefaultAsync();

                if (debtShare != null)
                {
                    // Отмечаем долю как оплаченную
                    debtShare.IsPaid = true;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Доля {debtShare.IdExpenseShare} отмечена как оплаченная. Сумма: {debtShare.ShareAmount}, Должник: {request.FromUserId}, Кредитор: {request.ToUserId}");

                    return Json(new ApiResponse<object>
                    {
                        Success = true,
                        Message = $"Долг {debtShare.ShareAmount} ₽ отмечен как оплаченный"
                    });
                }
                else
                {
                    _logger.LogWarning($"Не найдена неоплаченная доля для пользователя {request.FromUserId} перед пользователем {request.ToUserId}");

                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Не найдена неоплаченная доля для этого пользователя"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отметке долга");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при отметке долга: " + ex.Message
                });
            }
        }
    }
        // Модели запросов для бюджета
        public class CreateExpenseRequest
    {
        public int TripId { get; set; }
        public string Title { get; set; }
        public decimal Amount { get; set; }
        public int CategoryId { get; set; }
        public DateTime ExpenseDate { get; set; }
        public List<int> SharedWith { get; set; } // Старый способ
        public List<ExpenseShareDto> Shares { get; set; } // Новый способ - неравное распределение
        public int? PointId { get; set; }
    }

    public class CreateCategoryRequest
    {
        public string Name { get; set; }
        public int? TripId { get; set; }
    }

    public class UpdateExpenseShareRequest
    {
        public int ExpenseId { get; set; }
        public int UserId { get; set; }
        public bool IsPaid { get; set; }
    }

    public class SendDebtReminderRequest
    {
        public int FromUserId { get; set; }
        public int ToUserId { get; set; }
        public decimal Amount { get; set; }
        public int TripId { get; set; }
    }

    public class UpdateExpenseRequest
    {
        public int ExpenseId { get; set; }
        public string Title { get; set; }
        public decimal Amount { get; set; }
        public int CategoryId { get; set; }
        public DateTime ExpenseDate { get; set; }
        public List<int> SharedWith { get; set; }
        public List<ExpenseShareDto> Shares { get; set; }
    }

    public class DeleteExpenseRequest
    {
        public int ExpenseId { get; set; }
    }
    public class UpdateCategoryRequest
    {
        public int CategoryId { get; set; }
        public string Name { get; set; }
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
}