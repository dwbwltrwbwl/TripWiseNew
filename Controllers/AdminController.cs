using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using TripWise.Models;
using TripWise.Models.ViewModels;

namespace TripWise.Controllers
{
    [Route("Admin")]
    public class AdminController : Controller
    {
        private readonly TripWiseContext _context;
        private readonly ILogger<AdminController> _logger;

        public AdminController(TripWiseContext context, ILogger<AdminController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("")]
        [HttpGet("Login")]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost("Login")]
        public async Task<IActionResult> Login(string username, string password)
        {

            // Проверяем, что поля не пустые
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Введите email и пароль");
                return View();
            }

            // Поиск пользователя в базе по email
            var user = await _context.Users
                .Include(u => u.IdRoleNavigation)
                .FirstOrDefaultAsync(u => u.Email == username);

            // Если пользователь не найден
            if (user == null)
            {
                Console.WriteLine($"Пользователь с email {username} не найден");
                ModelState.AddModelError("", "Неверное имя пользователя или пароль");
                return View();
            }

            // Проверяем, что пользователь является админом
            if (user.IdRole != 1)
            {
                Console.WriteLine($"Пользователь {username} не является админом (роль: {user.IdRole})");
                ModelState.AddModelError("", "Доступ запрещен. Только для администраторов");
                return View();
            }

            // Хэшируем введенный пароль
            var inputHash = HashPassword(password);

            // Проверяем пароль
            if (user.PasswordHash != inputHash)
            {
                Console.WriteLine($"Неверный пароль для пользователя {username}");
                ModelState.AddModelError("", "Неверное имя пользователя или пароль");
                return View();
            }

            // Успешная авторизация
            Console.WriteLine($"Успешный вход админа: {username}");

            // Создаем сессию
            HttpContext.Session.SetInt32("UserId", user.IdUser);
            HttpContext.Session.SetString("UserName", $"{user.LastName} {user.FirstName}");
            HttpContext.Session.SetString("UserEmail", user.Email);
            HttpContext.Session.SetInt32("UserRole", user.IdRole);

            // Устанавливаем куки для админа
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax
            };

            Response.Cookies.Append("AdminAuth", "true", cookieOptions);

            return RedirectToAction("Dashboard");
        }
        [HttpGet("Dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            // Проверка авторизации
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetInt32("UserRole");

            if (userId == null || userRole != 1)
            {
                return RedirectToAction("Login");
            }

            var model = new AdminDashboardViewModel();
            var today = DateTime.UtcNow.Date;
            var weekAgo = today.AddDays(-7);

            try
            {
                // ========== ПОЛЬЗОВАТЕЛИ ==========
                model.TotalUsers = await _context.Users.CountAsync();
                model.NewUsersToday = await _context.Users.CountAsync(u => u.CreatedAt.Date == today);
                model.NewUsersWeek = await _context.Users.CountAsync(u => u.CreatedAt >= weekAgo);

                // ========== БРОНИРОВАНИЯ ==========
                model.FlightBookings = await _context.FlightBookings.CountAsync();
                model.TrainBookings = await _context.TrainOrders.CountAsync();
                model.HotelBookings = await _context.HotelBookings.CountAsync();
                model.TotalBookings = model.FlightBookings + model.TrainBookings + model.HotelBookings;

                // ========== ФИНАНСЫ ==========
                var confirmedFlights = await _context.FlightBookings
                    .Where(f => f.Status == BookingStatus.Confirmed).ToListAsync();
                model.FlightRevenue = confirmedFlights.Sum(f => f.Price);

                var confirmedTrains = await _context.TrainOrders
                    .Where(t => t.Status == OrderStatus.Confirmed).ToListAsync();
                model.TrainRevenue = confirmedTrains.Sum(t => t.TotalPrice);

                var confirmedHotels = await _context.HotelBookings
                    .Where(h => h.Status == BookingStatus.Confirmed).ToListAsync();
                model.HotelRevenue = confirmedHotels.Sum(h => h.TotalPrice);

                model.TotalRevenue = model.FlightRevenue + model.TrainRevenue + model.HotelRevenue;

                // ========== ОТЗЫВЫ ==========
                var reviews = await _context.Reviews
                    .Where(r => !r.IsDeleted && r.IsApproved).ToListAsync();
                model.TotalReviews = reviews.Count;
                model.AverageRating = reviews.Any() ? reviews.Average(r => r.Rating) : 0;

                // ========== ГРАФИК (7 дней) ==========
                for (int i = 6; i >= 0; i--)
                {
                    var date = today.AddDays(-i);
                    var nextDate = date.AddDays(1);

                    model.ChartLabels.Add(date.ToString("dd MMM", new System.Globalization.CultureInfo("ru-RU")));

                    var newUsers = await _context.Users
                        .CountAsync(u => u.CreatedAt >= date && u.CreatedAt < nextDate);
                    model.NewUsersData.Add(newUsers);

                    var bookings = await _context.FlightBookings
                        .CountAsync(f => f.CreatedAt >= date && f.CreatedAt < nextDate);
                    bookings += await _context.TrainOrders
                        .CountAsync(t => t.CreatedAt >= date && t.CreatedAt < nextDate);
                    bookings += await _context.HotelBookings
                        .CountAsync(h => h.CreatedAt >= date && h.CreatedAt < nextDate);
                    model.BookingsData.Add(bookings);
                }

                // ========== ПОСЛЕДНИЕ БРОНИРОВАНИЯ ==========
                var recentBookings = new List<RecentBookingDto>();

                // Авиа
                var flights = await _context.FlightBookings
                    .OrderByDescending(f => f.CreatedAt)
                    .Take(5)
                    .Select(f => new RecentBookingDto
                    {
                        Id = f.BookingNumber,
                        UserName = f.ContactName,
                        Type = "flight",
                        Route = $"{f.DepartureCity} → {f.ArrivalCity}",
                        Price = f.Price,
                        Status = f.Status.ToString().ToLower(),
                        CreatedAt = f.CreatedAt
                    }).ToListAsync();
                recentBookings.AddRange(flights);

                // ЖД
                var trains = await _context.TrainOrders
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(5)
                    .Select(t => new RecentBookingDto
                    {
                        Id = t.OrderNumber,
                        UserName = t.PassengerFullName,
                        Type = "train",
                        Route = $"{t.DepartureStationName} → {t.ArrivalStationName}",
                        Price = t.TotalPrice,
                        Status = t.Status.ToString().ToLower(),
                        CreatedAt = t.CreatedAt
                    }).ToListAsync();
                recentBookings.AddRange(trains);

                // Отели
                var hotels = await _context.HotelBookings
                    .OrderByDescending(h => h.CreatedAt)
                    .Take(5)
                    .Select(h => new RecentBookingDto
                    {
                        Id = h.BookingNumber,
                        UserName = h.ContactName,
                        Type = "hotel",
                        Route = h.HotelName,
                        Price = h.TotalPrice,
                        Status = h.Status.ToString().ToLower(),
                        CreatedAt = h.CreatedAt
                    }).ToListAsync();
                recentBookings.AddRange(hotels);

                model.RecentBookings = recentBookings
                    .OrderByDescending(b => b.CreatedAt)
                    .Take(10)
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки дашборда: {ex.Message}");
            }

            return View(model);
        }

        [HttpGet("Logout")]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        // Вспомогательный метод для хэширования пароля
        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(password);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }
        
        [HttpGet("CheckUsers")]
        public async Task<IActionResult> CheckUsers()
        {
            var users = await _context.Users
                .Include(u => u.IdRoleNavigation)
                .ToListAsync();

            var result = users.Select(u => new
            {
                Id = u.IdUser,
                Email = u.Email,
                Name = $"{u.LastName} {u.FirstName}",
                Role = u.IdRole,
                RoleName = u.IdRoleNavigation?.Name,
                PasswordHash = u.PasswordHash
            });

            return Json(result);
        }
        [HttpGet("Users")]
        public async Task<IActionResult> Users()
        {
            // Проверка авторизации
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetInt32("UserRole");

            if (userId == null || userRole != 1)
            {
                return RedirectToAction("Login");
            }

            // Получаем всех пользователей с их ролями
            var users = await _context.Users
                .Include(u => u.IdRoleNavigation)
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            return View(users);
        }
        [HttpGet("GetUserStats/{userId}")]
        public async Task<IActionResult> GetUserStats(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound();

            // Получаем статистику пользователя
            var trips = await _context.TripParticipants
                .Where(tp => tp.IdUser == userId)
                .Select(tp => tp.IdTrip)
                .Distinct()
                .CountAsync();

            var expenses = await _context.ExpenseShares
                .Where(es => es.IdUser == userId)
                .SumAsync(es => es.ShareAmount);

            var documents = await _context.UserDocuments
                .Where(d => d.UserId == userId)
                .CountAsync();

            var reviews = await _context.Reviews
                .Where(r => r.UserId == userId)
                .CountAsync();

            var viewModel = new
            {
                UserName = $"{user.LastName} {user.FirstName} {user.MiddleName}",
                UserEmail = user.Email,
                RegisteredAt = user.CreatedAt,
                TripsCount = trips,
                TotalExpenses = expenses,
                DocumentsCount = documents,
                ReviewsCount = reviews,
                HasAvatar = !string.IsNullOrEmpty(user.AvatarPath),
                AvatarPath = user.AvatarPath
            };

            return PartialView("_UserStats", viewModel);
        }

        [HttpPost("DeleteUser/{userId}")]
        public async Task<IActionResult> DeleteUser(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return Json(new { success = false, message = "Пользователь не найден" });

                // Нельзя удалить самого себя
                var currentUserId = HttpContext.Session.GetInt32("UserId");
                if (currentUserId == userId)
                    return Json(new { success = false, message = "Нельзя удалить свой собственный аккаунт" });

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("ToggleUserRole/{userId}")]
        public async Task<IActionResult> ToggleUserRole(int userId, [FromBody] dynamic data)
        {
            try
            {
                int newRole = data.newRole;

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return Json(new { success = false, message = "Пользователь не найден" });

                user.IdRole = newRole;
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        [HttpGet("Analytics")]
        public async Task<IActionResult> Analytics()
        {
            // Проверка авторизации
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetInt32("UserRole");

            if (userId == null || userRole != 1)
            {
                return RedirectToAction("Login");
            }

            var model = new AnalyticsViewModel();
            var today = DateTime.UtcNow.Date;
            var weekAgo = today.AddDays(-7);
            var monthAgo = today.AddMonths(-1);

            try
            {
                // ========== ПОЛЬЗОВАТЕЛИ ==========
                model.TotalUsers = await _context.Users.CountAsync();

                model.NewUsersToday = await _context.Users
                    .CountAsync(u => u.CreatedAt.Date == today);

                model.NewUsersWeek = await _context.Users
                    .CountAsync(u => u.CreatedAt >= weekAgo);

                model.NewUsersMonth = await _context.Users
                    .CountAsync(u => u.CreatedAt >= monthAgo);

                // ========== АВИАБИЛЕТЫ ==========
                model.TotalFlightBookings = await _context.FlightBookings.CountAsync();

                // Для подсчета дохода берем только подтвержденные бронирования
                // Предполагаем, что Status = 1 означает "Подтвержден"
                var confirmedFlightBookings = await _context.FlightBookings
                    .Where(f => f.Status == BookingStatus.Confirmed) // Или используйте нужное значение enum
                    .ToListAsync();
                model.FlightRevenue = confirmedFlightBookings.Sum(f => f.Price);

                // ========== ЖД БИЛЕТЫ ==========
                model.TotalTrainBookings = await _context.TrainOrders.CountAsync();

                var confirmedTrainOrders = await _context.TrainOrders
                    .Where(t => t.Status == OrderStatus.Confirmed) // Или используйте нужное значение enum
                    .ToListAsync();
                model.TrainRevenue = confirmedTrainOrders.Sum(t => t.TotalPrice);

                // ========== ОТЕЛИ ==========
                model.TotalHotelBookings = await _context.HotelBookings.CountAsync();

                var confirmedHotelBookings = await _context.HotelBookings
                    .Where(h => h.Status == BookingStatus.Confirmed) // Или используйте нужное значение enum
                    .ToListAsync();
                model.HotelRevenue = confirmedHotelBookings.Sum(h => h.TotalPrice);

                // Общий оборот
                model.TotalRevenue = model.FlightRevenue + model.TrainRevenue + model.HotelRevenue;

                // ========== ОТЗЫВЫ ==========
                var reviews = await _context.Reviews
                    .Where(r => !r.IsDeleted && r.IsApproved)
                    .ToListAsync();

                model.TotalReviews = reviews.Count;
                model.AverageRating = reviews.Any() ? Math.Round(reviews.Average(r => r.Rating), 1) : 0;

                // Последние отзывы
                model.RecentReviews = await _context.Reviews
                    .Where(r => !r.IsDeleted && r.IsApproved)
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(4)
                    .Select(r => new RecentReview
                    {
                        UserName = r.Name,
                        Rating = r.Rating,
                        RatingStars = GetStars(r.Rating),
                        CreatedAt = r.CreatedAt
                    })
                    .ToListAsync();

                // ========== АКТИВНОСТЬ ПОЛЬЗОВАТЕЛЕЙ ==========
                var lastDay = DateTime.UtcNow.AddDays(-1);
                var lastWeek = DateTime.UtcNow.AddDays(-7);
                var lastMonth = DateTime.UtcNow.AddMonths(-1);

                // Активные пользователи за день
                var activeUserIds = new HashSet<int>();

                var flightUsers = await _context.FlightBookings
                    .Where(f => f.CreatedAt >= lastDay)
                    .Select(f => f.UserId)
                    .ToListAsync();

                var trainUsers = await _context.TrainOrders
                    .Where(t => t.CreatedAt >= lastDay)
                    .Select(t => t.UserId)
                    .ToListAsync();

                var hotelUsers = await _context.HotelBookings
                    .Where(h => h.CreatedAt >= lastDay)
                    .Select(h => h.UserId)
                    .ToListAsync();

                var reviewUsers = await _context.Reviews
                    .Where(r => r.CreatedAt >= lastDay)
                    .Select(r => r.UserId)
                    .ToListAsync();

                foreach (var id in flightUsers) activeUserIds.Add(id);
                foreach (var id in trainUsers) activeUserIds.Add(id);
                foreach (var id in hotelUsers) activeUserIds.Add(id);
                foreach (var id in reviewUsers) activeUserIds.Add(id);

                model.ActiveUsersToday = activeUserIds.Count;

                // Активные пользователи за неделю
                activeUserIds.Clear();
                flightUsers = await _context.FlightBookings
                    .Where(f => f.CreatedAt >= lastWeek)
                    .Select(f => f.UserId)
                    .ToListAsync();
                foreach (var id in flightUsers) activeUserIds.Add(id);

                trainUsers = await _context.TrainOrders
                    .Where(t => t.CreatedAt >= lastWeek)
                    .Select(t => t.UserId)
                    .ToListAsync();
                foreach (var id in trainUsers) activeUserIds.Add(id);

                hotelUsers = await _context.HotelBookings
                    .Where(h => h.CreatedAt >= lastWeek)
                    .Select(h => h.UserId)
                    .ToListAsync();
                foreach (var id in hotelUsers) activeUserIds.Add(id);

                reviewUsers = await _context.Reviews
                    .Where(r => r.CreatedAt >= lastWeek)
                    .Select(r => r.UserId)
                    .ToListAsync();
                foreach (var id in reviewUsers) activeUserIds.Add(id);

                model.ActiveUsersWeek = activeUserIds.Count;

                // Активные пользователи за месяц
                activeUserIds.Clear();
                flightUsers = await _context.FlightBookings
                    .Where(f => f.CreatedAt >= lastMonth)
                    .Select(f => f.UserId)
                    .ToListAsync();
                foreach (var id in flightUsers) activeUserIds.Add(id);

                trainUsers = await _context.TrainOrders
                    .Where(t => t.CreatedAt >= lastMonth)
                    .Select(t => t.UserId)
                    .ToListAsync();
                foreach (var id in trainUsers) activeUserIds.Add(id);

                hotelUsers = await _context.HotelBookings
                    .Where(h => h.CreatedAt >= lastMonth)
                    .Select(h => h.UserId)
                    .ToListAsync();
                foreach (var id in hotelUsers) activeUserIds.Add(id);

                reviewUsers = await _context.Reviews
                    .Where(r => r.CreatedAt >= lastMonth)
                    .Select(r => r.UserId)
                    .ToListAsync();
                foreach (var id in reviewUsers) activeUserIds.Add(id);

                model.ActiveUsersMonth = activeUserIds.Count;

                // ========== ГРАФИК АКТИВНОСТИ ПОЛЬЗОВАТЕЛЕЙ (7 дней) ==========
                for (int i = 6; i >= 0; i--)
                {
                    var date = today.AddDays(-i);
                    var dayStart = date;
                    var dayEnd = date.AddDays(1);

                    // Считаем новых пользователей за этот день
                    var newUsers = await _context.Users
                        .CountAsync(u => u.CreatedAt >= dayStart && u.CreatedAt < dayEnd);

                    // Считаем бронирования за этот день
                    var flightBookings = await _context.FlightBookings
                        .CountAsync(f => f.CreatedAt >= dayStart && f.CreatedAt < dayEnd);

                    var trainBookings = await _context.TrainOrders
                        .CountAsync(t => t.CreatedAt >= dayStart && t.CreatedAt < dayEnd);

                    var hotelBookings = await _context.HotelBookings
                        .CountAsync(h => h.CreatedAt >= dayStart && h.CreatedAt < dayEnd);

                    var totalActivity = newUsers + flightBookings + trainBookings + hotelBookings;

                    model.UserActivity.Add(new ChartDataPoint
                    {
                        Label = date.ToString("dd MMM", new System.Globalization.CultureInfo("ru-RU")),
                        Value = totalActivity
                    });
                }

                // ========== ДОХОД ПО МЕСЯЦАМ (текущий год) ==========
                var currentYear = DateTime.UtcNow.Year;

                for (int month = 1; month <= 12; month++)
                {
                    var monthStart = new DateTime(currentYear, month, 1);
                    var monthEnd = monthStart.AddMonths(1);

                    // Авиа доход за месяц
                    var flightBookingsMonth = await _context.FlightBookings
                        .Where(f => f.CreatedAt >= monthStart && f.CreatedAt < monthEnd && f.Status == BookingStatus.Confirmed)
                        .ToListAsync();
                    var flightRevenue = flightBookingsMonth.Sum(f => f.Price);

                    // ЖД доход за месяц
                    var trainOrdersMonth = await _context.TrainOrders
                        .Where(t => t.CreatedAt >= monthStart && t.CreatedAt < monthEnd && t.Status == OrderStatus.Confirmed)
                        .ToListAsync();
                    var trainRevenue = trainOrdersMonth.Sum(t => t.TotalPrice);

                    // Отели доход за месяц
                    var hotelBookingsMonth = await _context.HotelBookings
                        .Where(h => h.CreatedAt >= monthStart && h.CreatedAt < monthEnd && h.Status == BookingStatus.Confirmed)
                        .ToListAsync();
                    var hotelRevenue = hotelBookingsMonth.Sum(h => h.TotalPrice);

                    var totalRevenue = flightRevenue + trainRevenue + hotelRevenue;

                    model.MonthlyRevenue.Add(new ChartDataPoint
                    {
                        Label = monthStart.ToString("MMM", new System.Globalization.CultureInfo("ru-RU")),
                        Amount = totalRevenue / 1000 // в тысячах рублей
                    });
                }

                // ========== ПОПУЛЯРНЫЕ НАПРАВЛЕНИЯ ==========
                var destinations = new List<PopularDestination>();

                // Авиа направления
                var flightRoutes = await _context.FlightBookings
                    .GroupBy(f => new { f.DepartureCity, f.ArrivalCity })
                    .Select(g => new
                    {
                        Route = $"{g.Key.DepartureCity} → {g.Key.ArrivalCity}",
                        Count = g.Count(),
                        Type = "Авиа"
                    })
                    .OrderByDescending(x => x.Count)
                    .Take(5)
                    .ToListAsync();

                foreach (var route in flightRoutes)
                {
                    destinations.Add(new PopularDestination
                    {
                        Route = route.Route,
                        Type = route.Type,
                        Count = route.Count,
                        Icon = "fa-plane",
                        Color = "primary"
                    });
                }

                // ЖД направления
                var trainRoutes = await _context.TrainOrders
                    .GroupBy(t => new { t.DepartureStationName, t.ArrivalStationName })
                    .Select(g => new
                    {
                        Route = $"{g.Key.DepartureStationName} → {g.Key.ArrivalStationName}",
                        Count = g.Count(),
                        Type = "ЖД"
                    })
                    .OrderByDescending(x => x.Count)
                    .Take(5)
                    .ToListAsync();

                foreach (var route in trainRoutes)
                {
                    destinations.Add(new PopularDestination
                    {
                        Route = route.Route,
                        Type = route.Type,
                        Count = route.Count,
                        Icon = "fa-train",
                        Color = "success"
                    });
                }

                // Отели
                var hotels = await _context.HotelBookings
                    .GroupBy(h => h.HotelName)
                    .Select(g => new
                    {
                        Route = g.Key,
                        Count = g.Count(),
                        Type = "Отель"
                    })
                    .OrderByDescending(x => x.Count)
                    .Take(5)
                    .ToListAsync();

                foreach (var hotel in hotels)
                {
                    destinations.Add(new PopularDestination
                    {
                        Route = hotel.Route,
                        Type = hotel.Type,
                        Count = hotel.Count,
                        Icon = "fa-hotel",
                        Color = "warning"
                    });
                }

                // Сортируем по популярности и берем топ-5
                model.PopularDestinations = destinations
                    .OrderByDescending(d => d.Count)
                    .Take(5)
                    .ToList();

                // Вычисляем проценты
                if (model.PopularDestinations.Any())
                {
                    var maxCount = model.PopularDestinations.Max(d => d.Count);
                    foreach (var dest in model.PopularDestinations)
                    {
                        dest.Percentage = maxCount > 0 ? (int)((double)dest.Count / maxCount * 100) : 0;
                    }
                }
            }
            catch (Exception ex)
            {
                // Используем ILogger для логирования ошибки
                // Если у вас нет ILogger, можно использовать Console.WriteLine или временно убрать
                Console.WriteLine($"Ошибка при загрузке аналитики: {ex.Message}");
            }

            return View(model);
        }

        // Вспомогательный метод для отображения звезд
        private string GetStars(int rating)
        {
            return string.Concat(Enumerable.Repeat("★", rating)) +
                   string.Concat(Enumerable.Repeat("☆", 5 - rating));
        }

        [HttpGet("Settings")]
        public IActionResult Settings()
        {
            // Проверка авторизации
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetInt32("UserRole");

            if (userId == null || userRole != 1)
            {
                return RedirectToAction("Login");
            }

            // Загрузить текущие настройки из БД (если есть таблица Settings)
            return View();
        }
        [HttpPost("ExportUsersData")]
        public IActionResult ExportUsersData([FromBody] List<List<string>> data)
        {
            try
            {
                if (data == null || data.Count == 0)
                {
                    return BadRequest("Нет данных для экспорта");
                }

                var sb = new StringBuilder();

                // Используем кодировку Windows-1251 для корректного отображения русских букв в Excel
                // Сначала добавляем разделитель - точка с запятой для русской версии Excel
                foreach (var row in data)
                {
                    var escapedRow = row.Select(cell =>
                        $"\"{cell?.Replace("\"", "\"\"") ?? ""}\"");
                    sb.AppendLine(string.Join(";", escapedRow));  // Используем ; вместо ,
                }

                // Кодировка Windows-1251 для русских символов
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                var win1251 = Encoding.GetEncoding(1251);
                var bytes = win1251.GetBytes(sb.ToString());
                var fileName = $"users_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv";

                return File(bytes, "text/csv;charset=windows-1251", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка экспорта пользователей");
                return StatusCode(500, "Ошибка при создании CSV файла");
            }
        }
        // ========== УПРАВЛЕНИЕ ОТЗЫВАМИ ==========

        [HttpGet("ManageReviews")]
        public async Task<IActionResult> ManageReviews()
        {
            // Проверка авторизации
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetInt32("UserRole");

            if (userId == null || userRole != 1)
            {
                return RedirectToAction("Login");
            }

            // Получаем все отзывы с информацией о пользователях
            var reviews = await _context.Reviews
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReviewManageViewModel
                {
                    Id = r.Id,
                    UserName = r.User != null ? $"{r.User.LastName} {r.User.FirstName}" : r.Name,
                    UserEmail = r.User != null ? r.User.Email : r.Email,
                    Rating = r.Rating,
                    Text = r.Text,
                    CreatedAt = r.CreatedAt,
                    IsApproved = r.IsApproved,
                    IsDeleted = r.IsDeleted
                })
                .ToListAsync();

            return View(reviews);
        }

        [HttpPost("ApproveReview/{id}")]
        public async Task<IActionResult> ApproveReview(int id)
        {
            try
            {
                var review = await _context.Reviews.FindAsync(id);
                if (review == null)
                    return Json(new { success = false, message = "Отзыв не найден" });

                review.IsApproved = true;
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Отзыв одобрен" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        [HttpGet("GetPaymentStats")]
        public async Task<IActionResult> GetPaymentStats()
        {
            var paid = await _context.FlightBookings.CountAsync(f => f.PaymentStatus == PaymentStatus.Paid) +
                       await _context.TrainOrders.CountAsync(t => t.PaymentStatus == PaymentStatus.Paid) +
                       await _context.HotelBookings.CountAsync(h => h.PaymentStatus == PaymentStatus.Paid);

            var pending = await _context.FlightBookings.CountAsync(f => f.PaymentStatus == PaymentStatus.Pending) +
                          await _context.TrainOrders.CountAsync(t => t.PaymentStatus == PaymentStatus.Pending) +
                          await _context.HotelBookings.CountAsync(h => h.PaymentStatus == PaymentStatus.Pending);

            var cancelled = await _context.FlightBookings.CountAsync(f => f.Status == BookingStatus.Cancelled) +
                            await _context.TrainOrders.CountAsync(t => t.Status == OrderStatus.Cancelled) +
                            await _context.HotelBookings.CountAsync(h => h.Status == BookingStatus.Cancelled);

            return Ok(new { paid, pending, cancelled });
        }

        [HttpGet("GetTopUsers")]
        public async Task<IActionResult> GetTopUsers()
        {
            // 1. Авиабилеты - количество бронирований
            var flightUsers = await _context.FlightBookings
                .GroupBy(f => f.UserId)
                .Select(g => new { UserId = g.Key, Actions = g.Count() })
                .ToListAsync();

            // 2. ЖД билеты - количество заказов
            var trainUsers = await _context.TrainOrders
                .GroupBy(t => t.UserId)
                .Select(g => new { UserId = g.Key, Actions = g.Count() })
                .ToListAsync();

            // 3. Отели - количество бронирований
            var hotelUsers = await _context.HotelBookings
                .GroupBy(h => h.UserId)
                .Select(g => new { UserId = g.Key, Actions = g.Count() })
                .ToListAsync();

            // 4. Участие в поездках - количество уникальных поездок
            var tripUsers = await _context.TripParticipants
                .GroupBy(tp => tp.IdUser)
                .Select(g => new { UserId = g.Key, Actions = g.Select(tp => tp.IdTrip).Distinct().Count() })
                .ToListAsync();

            // 5. Отзывы - количество написанных отзывов
            var reviewUsers = await _context.Reviews
                .Where(r => !r.IsDeleted && r.IsApproved)
                .GroupBy(r => r.UserId)
                .Select(g => new { UserId = g.Key, Actions = g.Count() })
                .ToListAsync();

            // 6. Создание поездок - сколько поездок создал пользователь
            var createdTrips = await _context.Trips
                .Where(t => t.CreatedById.HasValue)
                .GroupBy(t => t.CreatedById.Value)
                .Select(g => new { UserId = g.Key, Actions = g.Count() })
                .ToListAsync();

            // 7. Создание чатов - сколько чатов создал пользователь
            var createdChats = await _context.Chats
                .GroupBy(c => c.CreatedById)
                .Select(g => new { UserId = g.Key, Actions = g.Count() })
                .ToListAsync();

            // Объединяем все действия
            var allActions = flightUsers
                .Concat(trainUsers)
                .Concat(hotelUsers)
                .Concat(tripUsers)
                .Concat(reviewUsers)
                .Concat(createdTrips)
                .Concat(createdChats)
                .GroupBy(u => u.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    TotalActions = g.Sum(x => x.Actions)
                })
                .OrderByDescending(u => u.TotalActions)
                .Take(5)
                .ToList();

            // Получаем имена пользователей
            var userIds = allActions.Select(a => a.UserId).Distinct().ToList();
            var users = await _context.Users
                .Where(u => userIds.Contains(u.IdUser))
                .ToDictionaryAsync(u => u.IdUser, u => $"{u.LastName} {u.FirstName}".Trim());

            var result = allActions.Select(a => new
            {
                userName = users.ContainsKey(a.UserId) ? users[a.UserId] : "Пользователь",
                totalSpent = a.TotalActions  // Переименовано, но на фронте используется totalSpent
            }).ToList();

            return Ok(result);
        }

        [HttpGet("GetHotelTypes")]
        public async Task<IActionResult> GetHotelTypes()
        {
            var types = await _context.HotelBookings
                .Where(h => !string.IsNullOrEmpty(h.AccommodationType))
                .GroupBy(h => h.AccommodationType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .OrderByDescending(t => t.Count)
                .Take(5)
                .ToListAsync();

            return Ok(types);
        }
        [HttpPost("RejectReview/{id}")]
        public async Task<IActionResult> RejectReview(int id)
        {
            try
            {
                var review = await _context.Reviews.FindAsync(id);
                if (review == null)
                    return Json(new { success = false, message = "Отзыв не найден" });

                review.IsDeleted = true;
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Отзыв отклонен и удален" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("DeleteReview/{id}")]
        public async Task<IActionResult> DeleteReview(int id)
        {
            try
            {
                var review = await _context.Reviews.FindAsync(id);
                if (review == null)
                    return Json(new { success = false, message = "Отзыв не найден" });

                review.IsDeleted = true;
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Отзыв удален" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        // POST: /Admin/BlockUser/{userId}
        [HttpPost("BlockUser/{userId}")]  // ← ВАЖНО: указываем полный маршрут!
        public async Task<IActionResult> BlockUser(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return Json(new { success = false, message = "Пользователь не найден" });

                // Нельзя заблокировать самого себя
                var currentUserId = HttpContext.Session.GetInt32("UserId");
                if (currentUserId == userId)
                    return Json(new { success = false, message = "Нельзя заблокировать свой собственный аккаунт" });

                user.IsBlocked = true;
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Пользователь заблокирован" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: /Admin/UnblockUser/{userId}
        [HttpPost("UnblockUser/{userId}")]  // ← ВАЖНО: указываем полный маршрут!
        public async Task<IActionResult> UnblockUser(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return Json(new { success = false, message = "Пользователь не найден" });

                var currentUserId = HttpContext.Session.GetInt32("UserId");
                if (currentUserId == userId)
                    return Json(new { success = false, message = "Нельзя разблокировать свой собственный аккаунт" });

                user.IsBlocked = false;
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Пользователь разблокирован" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
    public class ReviewManageViewModel
    {
        public int Id { get; set; }
        public string UserName { get; set; }
        public string UserEmail { get; set; }
        public int Rating { get; set; }
        public string Text { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsApproved { get; set; }
        public bool IsDeleted { get; set; }
    }
}