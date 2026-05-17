using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using TripWise.Models;

namespace TripWise.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewController : Controller
    {
        private readonly TripWiseContext _context;

        public ReviewController(TripWiseContext context)
        {
            _context = context;
        }

        // Вспомогательный метод для форматирования ФИО
        private string FormatFullUserName(User user)
        {
            if (user == null) return "Пользователь";

            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(user.LastName))
                parts.Add(user.LastName);

            if (!string.IsNullOrWhiteSpace(user.FirstName))
                parts.Add(user.FirstName);

            if (!string.IsNullOrWhiteSpace(user.MiddleName))
                parts.Add(user.MiddleName);

            return parts.Count > 0 ? string.Join(" ", parts) : user.Email?.Split('@')[0] ?? "Пользователь";
        }

        private string FormatShortUserName(User user)
        {
            if (user == null) return "Пользователь";

            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(user.LastName))
                parts.Add(user.LastName);

            if (!string.IsNullOrWhiteSpace(user.FirstName))
                parts.Add(user.FirstName.Substring(0, 1) + ".");

            if (!string.IsNullOrWhiteSpace(user.MiddleName))
                parts.Add(user.MiddleName.Substring(0, 1) + ".");

            return parts.Count > 0 ? string.Join(" ", parts) : user.Email?.Split('@')[0] ?? "Пользователь";
        }

        // GET: Review/Reviews
        [HttpGet]
        public async Task<IActionResult> Reviews()
        {
            ViewData["Title"] = "Отзывы";

            var model = new ReviewsViewModel();

            // ПРОВЕРЯЕМ ВСЕ ВОЗМОЖНЫЕ ИСТОЧНИКИ
            var sessionUserId = HttpContext.Session.GetInt32("UserId");
            var cookieAuth = User.Identity?.IsAuthenticated ?? false;
            var claimsUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var claimsEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            var claimsName = User.FindFirst(ClaimTypes.GivenName)?.Value;
            var cookieUserEmail = Request.Cookies["UserEmail"];

            // ЛОГИРУЕМ ВСЁ
            System.Diagnostics.Debug.WriteLine("========== REVIEW DEBUG ==========");
            System.Diagnostics.Debug.WriteLine($"Session UserId: {sessionUserId}");
            System.Diagnostics.Debug.WriteLine($"Cookie Auth: {cookieAuth}");
            System.Diagnostics.Debug.WriteLine($"Claims UserId: {claimsUserId}");
            System.Diagnostics.Debug.WriteLine($"Claims Email: {claimsEmail}");
            System.Diagnostics.Debug.WriteLine($"Claims Name: {claimsName}");
            System.Diagnostics.Debug.WriteLine($"Cookie UserEmail: {cookieUserEmail}");
            System.Diagnostics.Debug.WriteLine("==================================");

            // Устанавливаем флаг аутентификации
            model.IsAuthenticated = sessionUserId.HasValue ||
                                   cookieAuth ||
                                   !string.IsNullOrEmpty(claimsUserId) ||
                                   !string.IsNullOrEmpty(cookieUserEmail);

            // ПРОБУЕМ ПОЛУЧИТЬ ПОЛЬЗОВАТЕЛЯ ИЗ ВСЕХ ИСТОЧНИКОВ
            User user = null;
            int? actualUserId = sessionUserId;

            // 1. Пробуем из сессии
            if (actualUserId.HasValue)
            {
                user = await _context.Users.FindAsync(actualUserId.Value);
                System.Diagnostics.Debug.WriteLine($"User found from SESSION: {user?.Email}");
            }

            // 2. Пробуем из claims
            if (user == null && !string.IsNullOrEmpty(claimsUserId))
            {
                if (int.TryParse(claimsUserId, out int id))
                {
                    user = await _context.Users.FindAsync(id);
                    if (user != null)
                    {
                        // Восстанавливаем сессию - ИСПРАВЛЕНО: используем единый формат
                        HttpContext.Session.SetInt32("UserId", user.IdUser);
                        HttpContext.Session.SetString("UserName", FormatFullUserName(user)); // УЖЕ ПРАВИЛЬНО
                        HttpContext.Session.SetString("UserEmail", user.Email);
                        HttpContext.Session.SetInt32("UserRole", user.IdRole);
                    }
                }
            }

            // 3. Пробуем из cookie email
            if (user == null && !string.IsNullOrEmpty(cookieUserEmail))
            {
                user = await _context.Users.FirstOrDefaultAsync(u => u.Email == cookieUserEmail);
                if (user != null)
                {
                    // Восстанавливаем сессию - ИСПРАВЛЕНО: используем единый формат
                    HttpContext.Session.SetInt32("UserId", user.IdUser);
                    HttpContext.Session.SetString("UserName", FormatFullUserName(user));
                    HttpContext.Session.SetString("UserEmail", user.Email);
                    HttpContext.Session.SetInt32("UserRole", user.IdRole);
                    System.Diagnostics.Debug.WriteLine($"User found from COOKIE EMAIL, session restored: {user.Email}");
                }
            }

            // Заполняем модель данными пользователя - ИСПРАВЛЕНО: используем единый формат
            if (user != null)
            {
                model.UserName = FormatFullUserName(user);
                model.UserEmail = user.Email ?? "";
                model.IsAuthenticated = true;

                System.Diagnostics.Debug.WriteLine($"FINAL - UserName: {model.UserName}");
                System.Diagnostics.Debug.WriteLine($"FINAL - UserEmail: {model.UserEmail}");
                System.Diagnostics.Debug.WriteLine($"FINAL - IsAuthenticated: {model.IsAuthenticated}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("FINAL - NO USER FOUND");
            }

            return View("Reviews", model);
        }

        // GET: api/Review/GetAll
        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAllReviews([FromQuery] int? rating = null)
        {
            try
            {
                var query = _context.Reviews
                    .Where(r => !r.IsDeleted && r.IsApproved);

                if (rating.HasValue && rating.Value >= 1 && rating.Value <= 5)
                {
                    query = query.Where(r => r.Rating == rating.Value);
                }

                var reviews = await query
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => new ReviewDto
                    {
                        Id = r.Id,
                        Name = r.Name,
                        Email = r.Email,
                        Rating = r.Rating,
                        Text = r.Text,
                        CreatedAt = r.CreatedAt
                    })
                    .ToListAsync();

                return Ok(reviews);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Ошибка при получении отзывов", details = ex.Message });
            }
        }

        // GET: api/Review/GetStatistics
        [HttpGet("GetStatistics")]
        public async Task<IActionResult> GetStatistics()
        {
            try
            {
                var reviews = await _context.Reviews
                    .Where(r => !r.IsDeleted && r.IsApproved)
                    .ToListAsync();

                var statistics = new ReviewStatisticsDto
                {
                    TotalReviews = reviews.Count,
                    AverageRating = reviews.Any() ? Math.Round(reviews.Average(r => r.Rating), 1) : 0,
                    RatingCounts = new Dictionary<int, int>
                    {
                        { 5, reviews.Count(r => r.Rating == 5) },
                        { 4, reviews.Count(r => r.Rating == 4) },
                        { 3, reviews.Count(r => r.Rating == 3) },
                        { 2, reviews.Count(r => r.Rating == 2) },
                        { 1, reviews.Count(r => r.Rating == 1) }
                    }
                };

                return Ok(statistics);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Ошибка при получении статистики", details = ex.Message });
            }
        }

        // POST: api/Review/Create
        [HttpPost("Create")]
        public async Task<IActionResult> CreateReview([FromBody] CreateReviewDto reviewDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { error = "Неверные данные формы", details = ModelState });
                }

                var userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                {
                    return Unauthorized(new { error = "Необходимо авторизоваться" });
                }

                var user = await _context.Users.FindAsync(userId.Value);
                if (user == null)
                {
                    return NotFound(new { error = "Пользователь не найден" });
                }

                // Валидация данных
                if (reviewDto.Rating < 1 || reviewDto.Rating > 5)
                {
                    return BadRequest(new { error = "Оценка должна быть от 1 до 5" });
                }

                if (string.IsNullOrWhiteSpace(reviewDto.Text) || reviewDto.Text.Length < 10)
                {
                    return BadRequest(new { error = "Текст отзыва должен содержать не менее 10 символов" });
                }

                if (reviewDto.Text.Length > 2000)
                {
                    return BadRequest(new { error = "Текст отзыва не должен превышать 2000 символов" });
                }

                var today = DateTime.UtcNow.Date;
                var existingReviewToday = await _context.Reviews
                    .AnyAsync(r => r.UserId == userId.Value &&
                                  r.CreatedAt.Date == today &&
                                  !r.IsDeleted);

                if (existingReviewToday)
                {
                    return BadRequest(new { error = "Вы уже оставляли отзыв сегодня. Пожалуйста, попробуйте завтра." });
                }

                // ИСПРАВЛЕНО: отзыв создается НЕ одобренным, ожидает модерации
                var review = new Review
                {
                    UserId = userId.Value,
                    Name = string.IsNullOrWhiteSpace(reviewDto.Name)
                        ? FormatFullUserName(user)
                        : reviewDto.Name,
                    Email = string.IsNullOrWhiteSpace(reviewDto.Email) ? user.Email : reviewDto.Email,
                    Rating = reviewDto.Rating,
                    Text = reviewDto.Text.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    IsApproved = false,  // НЕ одобрен, ждет модерации
                    IsDeleted = false
                };

                _context.Reviews.Add(review);
                await _context.SaveChangesAsync();

                // ИЗМЕНЕНО: возвращаем сообщение о модерации
                return Ok(new
                {
                    success = true,
                    message = "Отзыв отправлен на модерацию! После проверки он появится на сайте.",
                    needModeration = true
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Ошибка при сохранении отзыва", details = ex.Message });
            }
        }

        // GET: api/Review/CheckAuth
        [HttpGet("CheckAuth")]
        public IActionResult CheckAuth()
        {
            var sessionUserId = HttpContext.Session.GetInt32("UserId");
            var cookieAuth = User.Identity?.IsAuthenticated ?? false;
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return Ok(new
            {
                sessionAuthenticated = sessionUserId.HasValue,
                sessionUserId = sessionUserId,
                cookieAuthenticated = cookieAuth,
                userIdClaim = userIdClaim,
                userName = User.Identity?.Name,
                userEmail = User.FindFirst(ClaimTypes.Email)?.Value,
                allClaims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
            });
        }

        // GET: api/Review/ForceAuth
        [HttpGet("ForceAuth")]
        public async Task<IActionResult> ForceAuth()
        {
            // Экстренное восстановление аутентификации
            var userEmail = Request.Cookies["UserEmail"];
            if (!string.IsNullOrEmpty(userEmail))
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
                if (user != null)
                {
                    HttpContext.Session.SetInt32("UserId", user.IdUser);
                    HttpContext.Session.SetString("UserName", FormatFullUserName(user));
                    HttpContext.Session.SetString("UserEmail", user.Email);

                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.IdUser.ToString()),
                        new Claim(ClaimTypes.Name, user.Email),
                        new Claim(ClaimTypes.Email, user.Email),
                        new Claim(ClaimTypes.GivenName, FormatFullUserName(user))
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity));

                    return Ok(new { success = true, message = "Аутентификация восстановлена", user = user.Email });
                }
            }
            return Ok(new { success = false, message = "Не удалось восстановить" });
        }
        // В ReviewController добавьте метод для получения отзывов для главной
        [HttpGet("GetHomeReviews")]
        public async Task<IActionResult> GetHomeReviews()
        {
            try
            {
                var reviews = await _context.Reviews
                    .Where(r => !r.IsDeleted && r.IsApproved)
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(6)
                    .Select(r => new ReviewDto
                    {
                        Id = r.Id,
                        Name = r.Name,
                        Rating = r.Rating,
                        Text = r.Text.Length > 150 ? r.Text.Substring(0, 150) + "..." : r.Text,
                        CreatedAt = r.CreatedAt
                    })
                    .ToListAsync();

                return Ok(reviews);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}