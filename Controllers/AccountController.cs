using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWise.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TripWise.Models.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using TripWise.Services;

namespace TripWise.Controllers
{
    public class AccountController : Controller
    {
        private readonly TripWiseContext _context;
        private readonly EmailService _emailService;
        private readonly ILogger<AccountController> _logger;
        private readonly IMemoryCache _cache;
        private readonly IFileService _fileService;
        private const string DELETE_CODE_PREFIX = "DeleteCode_";
        private const string PASSWORD_CHANGE_CODE_PREFIX = "PasswordChangeCode_";
        private const string PASSWORD_CHANGE_DATA_PREFIX = "PasswordChangeData_";
        private const string FORGOT_PASSWORD_CODE_PREFIX = "ForgotPasswordCode_";
        private const string FORGOT_PASSWORD_DATA_PREFIX = "ForgotPasswordData_";

        public AccountController(TripWiseContext context, EmailService emailService,
            ILogger<AccountController> logger, IMemoryCache memoryCache, IFileService fileService)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
            _cache = memoryCache;
            _fileService = fileService;
        }

        [HttpGet]
        public async Task<IActionResult> TestEmail()
        {
            await _emailService.SendAsync(
                "tyumenelizaveta@yandex.ru",
                "Тест TripWise",
                "<b>SMTP Яндекс работает!</b>"
            );

            return Content("Письмо отправлено");
        }

        [HttpGet]
        public IActionResult GetAuthStatus()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            return Json(new
            {
                isAuthenticated = userId.HasValue,
                userId = userId
            });
        }

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login()
        {
            // Проверяем, есть ли сообщение об успешной регистрации
            if (TempData["RegistrationSuccess"] != null && (bool)TempData["RegistrationSuccess"])
            {
                ViewData["RegistrationSuccess"] = true;
                ViewData["RegisteredEmail"] = TempData["RegisteredEmail"];
            }

            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        public async Task<IActionResult> Login(string email, string password, string rememberMe)
        {
            // Валидация
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Email и пароль обязательны для заполнения");
                return View();
            }

            // Проверка email на валидность
            if (!IsValidEmail(email))
            {
                ModelState.AddModelError("", "Введите корректный email адрес");
                return View();
            }

            try
            {
                // Хэшируем введенный пароль для сравнения
                var hashedPassword = HashPassword(password);

                // Ищем пользователя в базе
                var user = await _context.Users
                    .Include(u => u.IdRoleNavigation)
                    .FirstOrDefaultAsync(u => u.Email == email && u.PasswordHash == hashedPassword);

                if (user != null)
                {
                    // ========== ПРОВЕРКА БЛОКИРОВКИ ПОЛЬЗОВАТЕЛЯ ==========
                    if (user.IsBlocked)
                    {
                        ModelState.AddModelError("", "Ваш аккаунт заблокирован. Обратитесь к администратору.");
                        return View();
                    }

                    // Успешная авторизация
                    // Сохраняем информацию о пользователе в сессии
                    HttpContext.Session.SetInt32("UserId", user.IdUser);
                    HttpContext.Session.SetString("UserName", GetFullUserName(user));
                    HttpContext.Session.SetString("UserEmail", user.Email);
                    HttpContext.Session.SetInt32("UserRole", user.IdRole);

                    // РЕАЛЬНАЯ РАБОТА ЗАПОМНИТЬ МЕНЯ
                    // Если пользователь отметил "Запомнить меня"
                    bool remember = !string.IsNullOrEmpty(rememberMe) && rememberMe == "true";

                    if (remember)
                    {
                        // Создаем токен
                        var authToken = GenerateAuthToken(user.IdUser, user.Email);

                        // Сохраняем в БД
                        await SaveAuthToken(user.IdUser, authToken);

                        // Устанавливаем куки
                        var cookieOptions = new CookieOptions
                        {
                            Expires = DateTime.Now.AddDays(30),
                            HttpOnly = true,
                            IsEssential = true,
                            SameSite = SameSiteMode.Lax
                        };

                        Response.Cookies.Append("AuthToken", authToken, cookieOptions);
                        Response.Cookies.Append("RememberMe", "true", cookieOptions);
                        Response.Cookies.Append("UserEmail", user.Email, cookieOptions);
                    }
                    else
                    {
                        // Если не "запомнить", то только сессия
                        // Удаляем старые куки если есть
                        Response.Cookies.Delete("AuthToken");
                        Response.Cookies.Delete("RememberMe");
                    }

                    // Добавляем стандартную аутентификацию ASP.NET Core
                    var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.IdUser.ToString()),
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.GivenName, GetFullUserName(user)),
                new Claim(ClaimTypes.Role, user.IdRole.ToString())
            };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = remember,
                        ExpiresUtc = remember ? DateTimeOffset.UtcNow.AddDays(30) : (DateTimeOffset?)null
                    };

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity),
                        authProperties);

                    // Редирект на главную страницу
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    ModelState.AddModelError("", "Неверный email или пароль");
                    return View();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при авторизации пользователя {Email}", email);
                ModelState.AddModelError("", "Произошла ошибка при авторизации. Попробуйте еще раз.");
                return View();
            }
        }
        private string GetFullUserName(User user)
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
        private string GenerateAuthToken(int userId, string email)
        {
            // Используем GUID + timestamp для уникальности
            var tokenData = $"{userId}|{email}|{DateTime.UtcNow.Ticks}|{Guid.NewGuid()}";
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(tokenData);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        private async Task SaveAuthToken(int userId, string token)
        {
            var authToken = new UserAuthToken
            {
                UserId = userId,
                Token = token,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(30)
            };

            // Удаляем старые токены этого пользователя
            var oldTokens = await _context.UserAuthTokens
                .Where(t => t.UserId == userId && t.ExpiresAt < DateTime.UtcNow)
                .ToListAsync();

            if (oldTokens.Any())
            {
                _context.UserAuthTokens.RemoveRange(oldTokens);
            }

            _context.UserAuthTokens.Add(authToken);
            await _context.SaveChangesAsync();
        }

        private async Task<bool> ValidateAuthToken(int userId, string token)
        {
            // Ищем валидный токен в базе
            var authToken = await _context.UserAuthTokens
                .FirstOrDefaultAsync(t =>
                    t.UserId == userId &&
                    t.Token == token &&
                    t.ExpiresAt > DateTime.UtcNow);

            return authToken != null;
        }
        private async Task DeleteAuthToken(int userId, string token)
        {
            var authToken = await _context.UserAuthTokens
                .FirstOrDefaultAsync(t => t.UserId == userId && t.Token == token);

            if (authToken != null)
            {
                _context.UserAuthTokens.Remove(authToken);
                await _context.SaveChangesAsync();
            }
        }

        // GET: /Account/Logout
        // GET: /Account/Logout
        public async Task<IActionResult> Logout()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var authToken = Request.Cookies["AuthToken"];

            if (userId.HasValue && !string.IsNullOrEmpty(authToken))
            {
                // Удаляем токен из БД
                var token = await _context.UserAuthTokens
                    .FirstOrDefaultAsync(t => t.UserId == userId.Value && t.Token == authToken);
                if (token != null)
                {
                    _context.UserAuthTokens.Remove(token);
                    await _context.SaveChangesAsync();
                }
            }

            // Очищаем сессию
            HttpContext.Session.Clear();

            // Удаляем ВСЕ куки
            var cookieOptions = new CookieOptions
            {
                Expires = DateTime.Now.AddDays(-1),
                HttpOnly = true
            };

            Response.Cookies.Delete("AuthToken");
            Response.Cookies.Delete("RememberMe");
            Response.Cookies.Delete("UserEmail");

            // ⚠️⚠️⚠️ ДОБАВЬТЕ ВЫХОД ИЗ COOKIE АУТЕНТИФИКАЦИИ ⚠️⚠️⚠️
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction("Index", "Home");
        }
        [HttpGet]
        [Route("Account/CleanupExpiredTokens")]
        public async Task<IActionResult> CleanupExpiredTokens()
        {
            var expiredTokens = await _context.UserAuthTokens
                .Where(t => t.ExpiresAt < DateTime.UtcNow)
                .ToListAsync();

            _context.UserAuthTokens.RemoveRange(expiredTokens);
            await _context.SaveChangesAsync();

            return Content($"Удалено {expiredTokens.Count} устаревших токенов");
        }
        [HttpGet]
        public async Task<IActionResult> DebugAuth()
        {
            var result = new
            {
                SessionUserId = HttpContext.Session.GetInt32("UserId"),
                Cookies = new
                {
                    AuthToken = Request.Cookies["AuthToken"],
                    RememberMe = Request.Cookies["RememberMe"],
                    UserEmail = Request.Cookies["UserEmail"]
                },
                DatabaseTokens = await _context.UserAuthTokens.ToListAsync()
            };

            return Json(result);
        }

        // GET: /Account/Register
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        public async Task<IActionResult> Register(string lastName, string firstName, string middleName, string email, string password, string confirmPassword, string agreeTerms)
        {
            ViewData["LastName"] = lastName;    
            ViewData["FirstName"] = firstName; 
            ViewData["MiddleName"] = middleName;
            ViewData["Email"] = email;
            ViewData["Password"] = password;
            ViewData["ConfirmPassword"] = confirmPassword;
            ViewData["AgreeTerms"] = agreeTerms;

            // Валидация
            if (string.IsNullOrEmpty(lastName) || string.IsNullOrEmpty(firstName) ||
        string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) ||
        string.IsNullOrEmpty(confirmPassword))
            {
                ModelState.AddModelError("", "Все поля, кроме отчества, обязательны для заполнения");
                return View();
            }

            // Проверка email на валидность
            if (!IsValidEmail(email))
            {
                ModelState.AddModelError("", "Введите корректный email адрес");
                return View();
            }

            // Проверка пароля
            var passwordValidationResult = ValidatePassword(password);
            if (!passwordValidationResult.IsValid)
            {
                ModelState.AddModelError("", passwordValidationResult.ErrorMessage);
                return View();
            }

            if (password != confirmPassword)
            {
                ModelState.AddModelError("", "Пароли не совпадают");
                return View();
            }

            // Проверяем, что чекбокс отмечен (значение "on")
            if (string.IsNullOrEmpty(agreeTerms) || agreeTerms != "on")
            {
                ModelState.AddModelError("", "Необходимо согласие с условиями использования");
                return View();
            }

            // Проверка существования пользователя
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (existingUser != null)
            {
                ModelState.AddModelError("", "Пользователь с таким email уже существует");
                return View();
            }

            try
            {
                // ⚠️⚠️⚠️ ИСПРАВЛЕНО: Теперь сохраняем FirstName и LastName ⚠️⚠️⚠️
                var user = new User
                {
                    LastName = lastName?.Trim() ?? "",      // Фамилия
                    FirstName = firstName?.Trim() ?? "",     // Имя
                    MiddleName = string.IsNullOrWhiteSpace(middleName) ? null : middleName.Trim(), // Отчество (может быть null)
                    Email = email.Trim().ToLower(),
                    PasswordHash = HashPassword(password),
                    Age = null,
                    CreatedAt = DateTime.UtcNow,
                    IdRole = 2 // Роль User
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // УСПЕШНАЯ РЕГИСТРАЦИЯ - очищаем поля
                ViewData["SuccessMessage"] = "Регистрация прошла успешно! Теперь вы можете войти в систему.";
                ViewData["LastName"] = "";
                ViewData["FirstName"] = "";
                ViewData["MiddleName"] = "";
                ViewData["Email"] = "";
                ViewData["Password"] = "";
                ViewData["ConfirmPassword"] = "";
                ViewData["AgreeTerms"] = "";

                // Добавляем TempData для отображения успеха на странице логина
                TempData["RegistrationSuccess"] = true;
                TempData["RegisteredEmail"] = email;

                return View();
            }
            catch (Exception ex)
            {
                // Логирование ошибки
                Console.WriteLine($"Ошибка при регистрации: {ex.Message}");
                ModelState.AddModelError("", "Произошла ошибка при регистрации. Попробуйте еще раз.");
                return View();
            }
        }

        // GET: /Account/Edit
        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.IdUser == userId);
            if (user == null)
                return RedirectToAction("Login");

            var model = new EditProfileViewModel
            {
                LastName = user.LastName,
                FirstName = user.FirstName,
                MiddleName = user.MiddleName,
                Email = user.Email,
                Age = user.Age,
                CurrentAvatarPath = user.AvatarPath // ВАЖНО: передаем текущий путь к аватарке
            };

            return View(model);
        }

        // POST: /Account/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditProfileViewModel model)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login");

            _logger.LogInformation("=== НАЧАЛО РЕДАКТИРОВАНИЯ ПРОФИЛЯ ===");
            _logger.LogInformation("UserId: {UserId}", userId);
            _logger.LogInformation("RemoveAvatar: {RemoveAvatar}", model.RemoveAvatar);
            _logger.LogInformation("Avatar файл получен: {HasFile}", model.Avatar != null);

            if (model.Avatar != null)
            {
                _logger.LogInformation("Имя файла: {FileName}", model.Avatar.FileName);
                _logger.LogInformation("Размер: {FileSize} байт", model.Avatar.Length);
                _logger.LogInformation("ContentType: {ContentType}", model.Avatar.ContentType);
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("Ошибки валидации: {Errors}", string.Join(", ", errors));
                return View(model);
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.IdUser == userId);
            if (user == null)
                return RedirectToAction("Login");

            _logger.LogInformation("Текущий AvatarPath пользователя: {AvatarPath}", user.AvatarPath);

            // Обновляем данные
            user.LastName = model.LastName?.Trim() ?? user.LastName;
            user.FirstName = model.FirstName?.Trim() ?? user.FirstName;
            user.MiddleName = string.IsNullOrWhiteSpace(model.MiddleName) ? null : model.MiddleName.Trim();
            user.Email = model.Email?.Trim().ToLower() ?? user.Email;
            user.Age = model.Age;

            // Обработка аватарки
            if (model.RemoveAvatar)
            {
                _logger.LogInformation("Удаление аватарки");
                if (!string.IsNullOrEmpty(user.AvatarPath))
                {
                    _fileService.DeleteAvatar(user.AvatarPath);
                    user.AvatarPath = null;
                    _logger.LogInformation("Аватарка удалена");
                }
            }
            else if (model.Avatar != null && model.Avatar.Length > 0)
            {
                _logger.LogInformation("Начинаем обработку новой аватарки");

                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif" };
                if (!allowedTypes.Contains(model.Avatar.ContentType.ToLower()))
                {
                    _logger.LogWarning("Неподдерживаемый тип файла: {ContentType}", model.Avatar.ContentType);
                    ModelState.AddModelError("Avatar", "Разрешены только изображения (JPEG, PNG, GIF)");
                    return View(model);
                }

                if (model.Avatar.Length > 2 * 1024 * 1024)
                {
                    _logger.LogWarning("Файл слишком большой: {FileSize} байт", model.Avatar.Length);
                    ModelState.AddModelError("Avatar", "Размер файла не должен превышать 2MB");
                    return View(model);
                }

                if (!string.IsNullOrEmpty(user.AvatarPath))
                {
                    _logger.LogInformation("Удаляем старую аватарку: {AvatarPath}", user.AvatarPath);
                    _fileService.DeleteAvatar(user.AvatarPath);
                }

                _logger.LogInformation("Сохраняем новую аватарку...");
                user.AvatarPath = await _fileService.SaveAvatarAsync(model.Avatar, user.IdUser);
                _logger.LogInformation("Новая аватарка сохранена: {AvatarPath}", user.AvatarPath);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Изменения сохранены в БД. Новый AvatarPath: {AvatarPath}", user.AvatarPath);

            // Обновляем сессию
            HttpContext.Session.SetString("UserName", GetFullUserName(user));
            HttpContext.Session.SetString("UserEmail", user.Email);

            TempData["SuccessMessage"] = "Профиль успешно обновлен";
            return RedirectToAction("Profile");
        }

        // GET: /Account/ChangePassword
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        // GET: /Account/Profile
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login");

            var user = await _context.Users
                .Include(u => u.IdRoleNavigation)
                .FirstOrDefaultAsync(u => u.IdUser == userId);

            if (user == null)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login");
            }

            // Количество поездок
            var trips = await _context.TripParticipants
                .Where(tp => tp.IdUser == userId)
                .Select(tp => tp.IdTrip)
                .Distinct()
                .ToListAsync();

            var tripCount = trips.Count;

            // Количество дней в поездках
            var travelDays = await _context.Trips
                .Where(t => trips.Contains(t.IdTrip))
                .Select(t => new { t.StartDate, t.EndDate })
                .ToListAsync();
            var totalTravelDays = travelDays.Sum(t =>
                (t.EndDate.Date - t.StartDate.Date).Days
            );

            // Количество групп (разные поездки = разные группы)
            var groupCount = tripCount;
            var totalShare = await _context.ExpenseShares
                .Where(es => es.IdUser == userId)
                .SumAsync(es => es.ShareAmount);

            var unpaidShare = await _context.ExpenseShares
                .Where(es => es.IdUser == userId && !es.IsPaid)
                .SumAsync(es => es.ShareAmount);

            ViewBag.TripCount = tripCount;
            ViewBag.TravelDays = totalTravelDays;
            ViewBag.GroupCount = groupCount;
            ViewBag.TotalShare = totalShare;
            ViewBag.UnpaidShare = unpaidShare;
            ViewBag.LastExpenses = await _context.Expenses
                .Where(e => e.PaidById == userId)
                .OrderByDescending(e => e.CreatedAt)
                .Take(5)
                .ToListAsync();
            // В методе Profile добавьте:
            ViewBag.DocumentCount = await _context.UserDocuments
                .Where(d => d.UserId == userId)
                .CountAsync();

            ViewBag.FolderCount = await _context.DocumentFolders
                .Where(f => f.UserId == userId)
                .CountAsync();

            ViewBag.RecentDocuments = await _context.UserDocuments
                .Where(d => d.UserId == userId)
                .Include(d => d.Folder)
                .OrderByDescending(d => d.CreatedAt)
                .Take(3)
                .Select(d => new {
                    d.Name,
                    d.FileType,
                    d.FileSize,
                    d.CreatedAt,
                    FolderName = d.Folder != null ? d.Folder.Name : null
                })
                .ToListAsync();

            ViewBag.UserFolders = await _context.DocumentFolders
                .Where(f => f.UserId == userId)
                .OrderBy(f => f.Name)
                .Select(f => new {
                    f.IdFolder,
                    f.Name
                })
                .ToListAsync();

            return View(user);
        }

        // GET: /Account/Delete
        [HttpGet]
        public IActionResult Delete()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login");

            var email = HttpContext.Session.GetString("UserEmail");

            var model = new DeleteAccountViewModel
            {
                Email = email,
                CodeSent = false
            };

            return View(model);
        }

        // POST: /Account/SendDeleteCode
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendDeleteCode()
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                Console.WriteLine($"[DEBUG] SendDeleteCode called. UserId from session: {userId}");

                if (userId == null)
                    return Json(new { success = false, message = "Сессия истекла" });

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return Json(new { success = false, message = "Пользователь не найден" });

                var random = new Random();
                var code = random.Next(100000, 999999).ToString();

                var cacheKey = DELETE_CODE_PREFIX + userId;
                _cache.Set(cacheKey, code, TimeSpan.FromMinutes(15));

                Console.WriteLine($"[DEBUG] Code generated: '{code}', Cache key: '{cacheKey}'");
                Console.WriteLine($"[DEBUG] Code from cache after set: '{_cache.Get<string>(cacheKey)}'");

                await _emailService.SendConfirmationCodeAsync(user.Email, code);

                return Json(new { success = true, message = "Код подтверждения отправлен на ваш email" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error in SendDeleteCode: {ex.Message}");
                return Json(new { success = false, message = "Ошибка при отправке кода: " + ex.Message });
            }
        }

        // POST: /Account/ConfirmDelete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmDelete([FromBody] ConfirmDeleteRequest request)
        {
            try
            {
                Console.WriteLine($"[DEBUG] ========== CONFIRM DELETE START ==========");

                var code = request?.Code;
                var userId = HttpContext.Session.GetInt32("UserId");

                Console.WriteLine($"[DEBUG] UserId from session: {userId}");
                Console.WriteLine($"[DEBUG] Request code: '{code}'");

                if (userId == null)
                    return Json(new { success = false, message = "Сессия истекла. Пожалуйста, войдите заново." });

                if (string.IsNullOrWhiteSpace(code))
                    return Json(new { success = false, message = "Введите код подтверждения" });

                var cleanCode = new string(code.Where(char.IsDigit).ToArray());

                if (cleanCode.Length != 6)
                    return Json(new { success = false, message = "Код должен содержать 6 цифр" });

                var cacheKey = DELETE_CODE_PREFIX + userId;
                bool hasCache = _cache.TryGetValue(cacheKey, out string cachedCode);

                if (!hasCache || cachedCode == null)
                    return Json(new { success = false, message = "Код подтверждения истек или не был отправлен. Запросите новый код." });

                if (cachedCode != cleanCode)
                    return Json(new { success = false, message = "Неверный код подтверждения. Проверьте правильность ввода." });

                Console.WriteLine($"[DEBUG] Code verified successfully! Proceeding with account deletion...");

                // Используем транзакцию для безопасности
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        Console.WriteLine($"[DEBUG] Transaction started - Deleting user ID: {userId}");

                        // ========== 1. УДАЛЯЕМ ЗАПИСИ, КОТОРЫЕ ССЫЛАЮТСЯ НА ПОЛЬЗОВАТЕЛЯ ==========

                        // Удаляем запросы в друзья (где пользователь отправитель или получатель)
                        var friendRequestsDeleted = await _context.FriendRequests
                            .Where(fr => fr.SenderId == userId || fr.ReceiverId == userId)
                            .ExecuteDeleteAsync();
                        Console.WriteLine($"[DEBUG] Deleted {friendRequestsDeleted} friend requests");

                        // Удаляем друзей (где пользователь в любой роли)
                        var friendsDeleted = await _context.Friends
                            .Where(f => f.UserId == userId || f.FriendId == userId)
                            .ExecuteDeleteAsync();
                        Console.WriteLine($"[DEBUG] Deleted {friendsDeleted} friends");

                        // Удаляем приглашения в поездки
                        var tripInvitationsDeleted = await _context.TripInvitations
                            .Where(ti => ti.InviterId == userId || ti.InvitedId == userId)
                            .ExecuteDeleteAsync();
                        Console.WriteLine($"[DEBUG] Deleted {tripInvitationsDeleted} trip invitations");

                        // Удаляем закрепленные сообщения пользователя
                        var pinnedMessagesDeleted = await _context.UserPinnedMessages
                            .Where(upm => upm.UserId == userId)
                            .ExecuteDeleteAsync();
                        Console.WriteLine($"[DEBUG] Deleted {pinnedMessagesDeleted} pinned messages");

                        // Удаляем голосования пользователя
                        var votesDeleted = await _context.VotingSystems
                            .Where(v => v.CreatedById == userId)
                            .ExecuteDeleteAsync();
                        Console.WriteLine($"[DEBUG] Deleted {votesDeleted} voting systems");

                        // Удаляем опции голосования (через голосования)
                        // Это делается каскадно, но для безопасности сначала удалим голоса пользователя
                        var userVotesDeleted = await _context.UserVotes
                            .Where(uv => uv.IdUser == userId)
                            .ExecuteDeleteAsync();
                        Console.WriteLine($"[DEBUG] Deleted {userVotesDeleted} user votes");

                        // Удаляем чаты, созданные пользователем
                        var chatsDeleted = await _context.Chats
                            .Where(c => c.CreatedById == userId)
                            .ExecuteDeleteAsync();
                        Console.WriteLine($"[DEBUG] Deleted {chatsDeleted} chats created by user");

                        // Удаляем участников чатов
                        var chatMembersDeleted = await _context.ChatMembers
                            .Where(cm => cm.UserId == userId)
                            .ExecuteDeleteAsync();
                        Console.WriteLine($"[DEBUG] Deleted {chatMembersDeleted} chat members");

                        // Удаляем прочтения сообщений
                        var messageReadsDeleted = await _context.ChatMessageReads
                            .Where(cmr => cmr.UserId == userId)
                            .ExecuteDeleteAsync();
                        Console.WriteLine($"[DEBUG] Deleted {messageReadsDeleted} message reads");

                        // Удаляем сообщения пользователя
                        var messagesDeleted = await _context.ChatMessages
                            .Where(cm => cm.SenderId == userId)
                            .ExecuteDeleteAsync();
                        Console.WriteLine($"[DEBUG] Deleted {messagesDeleted} chat messages");

                        // Удаляем токены авторизации
                        var tokensDeleted = await _context.UserAuthTokens
                            .Where(t => t.UserId == userId)
                            .ExecuteDeleteAsync();
                        Console.WriteLine($"[DEBUG] Deleted {tokensDeleted} auth tokens");

                        // Удаляем доли расходов
                        var sharesDeleted = await _context.ExpenseShares
                            .Where(es => es.IdUser == userId)
                            .ExecuteDeleteAsync();
                        Console.WriteLine($"[DEBUG] Deleted {sharesDeleted} expense shares");

                        // Удаляем расходы пользователя (как плательщика)
                        var expensesDeleted = await _context.Expenses
                            .Where(e => e.PaidById == userId)
                            .ExecuteDeleteAsync();
                        Console.WriteLine($"[DEBUG] Deleted {expensesDeleted} expenses");

                        // Удаляем участников поездок
                        var participantsDeleted = await _context.TripParticipants
                            .Where(tp => tp.IdUser == userId)
                            .ExecuteDeleteAsync();
                        Console.WriteLine($"[DEBUG] Deleted {participantsDeleted} trip participants");

                        // Удаляем документы пользователя
                        var userDocsDeleted = await _context.UserDocuments
                            .Where(ud => ud.UserId == userId)
                            .ExecuteDeleteAsync();
                        Console.WriteLine($"[DEBUG] Deleted {userDocsDeleted} user documents");

                        // Удаляем папки документов
                        var foldersDeleted = await _context.DocumentFolders
                            .Where(df => df.UserId == userId)
                            .ExecuteDeleteAsync();
                        Console.WriteLine($"[DEBUG] Deleted {foldersDeleted} document folders");

                        // Удаляем документы, загруженные пользователем
                        var docsDeleted = await _context.Documents
                            .Where(d => d.UploadedById == userId)
                            .ExecuteDeleteAsync();
                        Console.WriteLine($"[DEBUG] Deleted {docsDeleted} documents");

                        // Удаляем избранные авиабилеты
                        var favoriteFlightsDeleted = await _context.FavoriteFlights
                            .Where(f => f.UserId == userId)
                            .ExecuteDeleteAsync();
                        Console.WriteLine($"[DEBUG] Deleted {favoriteFlightsDeleted} favorite flights");

                        // Удаляем избранные отели
                        var favoriteHotelsDeleted = await _context.FavoriteHotels
                            .Where(f => f.UserId == userId)
                            .ExecuteDeleteAsync();
                        Console.WriteLine($"[DEBUG] Deleted {favoriteHotelsDeleted} favorite hotels");

                        // Удаляем избранные поезда
                        var favoriteTrainsDeleted = await _context.FavoriteTrains
                            .Where(f => f.UserId == userId)
                            .ExecuteDeleteAsync();
                        Console.WriteLine($"[DEBUG] Deleted {favoriteTrainsDeleted} favorite trains");

                        // Удаляем бронирования авиабилетов
                        var flightBookingsDeleted = await _context.FlightBookings
                            .Where(f => f.UserId == userId)
                            .ExecuteDeleteAsync();
                        Console.WriteLine($"[DEBUG] Deleted {flightBookingsDeleted} flight bookings");

                        // Удаляем бронирования отелей
                        var hotelBookingsDeleted = await _context.HotelBookings
                            .Where(h => h.UserId == userId)
                            .ExecuteDeleteAsync();
                        Console.WriteLine($"[DEBUG] Deleted {hotelBookingsDeleted} hotel bookings");

                        // Удаляем заказы поездов
                        var trainOrdersDeleted = await _context.TrainOrders
                            .Where(t => t.UserId == userId)
                            .ExecuteDeleteAsync();
                        Console.WriteLine($"[DEBUG] Deleted {trainOrdersDeleted} train orders");

                        // Удаляем заметки
                        var notesDeleted = await _context.Notes
                            .Where(n => n.UserId == userId)
                            .ExecuteDeleteAsync();
                        Console.WriteLine($"[DEBUG] Deleted {notesDeleted} notes");

                        // Удаляем запланированные активности
                        var plannedActivitiesDeleted = await _context.PlannedActivities
                            .Where(pa => pa.UserId == userId)
                            .ExecuteDeleteAsync();
                        Console.WriteLine($"[DEBUG] Deleted {plannedActivitiesDeleted} planned activities");

                        // Удаляем отзывы
                        var reviewsDeleted = await _context.Reviews
                            .Where(r => r.UserId == userId)
                            .ExecuteDeleteAsync();
                        Console.WriteLine($"[DEBUG] Deleted {reviewsDeleted} reviews");

                        // Обновляем точки интереса (убираем связь)
                        var poisUpdated = await _context.PointsOfInterests
                            .Where(p => p.AddedById == userId)
                            .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.AddedById, (int?)null));
                        Console.WriteLine($"[DEBUG] Updated {poisUpdated} points of interest");

                        // Обновляем поездки (убираем создателя)
                        var tripsUpdated = await _context.Trips
                            .Where(t => t.CreatedById == userId)
                            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.CreatedById, (int?)null));
                        Console.WriteLine($"[DEBUG] Updated {tripsUpdated} trips");

                        // ========== 2. УДАЛЯЕМ ПОЛЬЗОВАТЕЛЯ ==========
                        var userDeleted = await _context.Users
                            .Where(u => u.IdUser == userId)
                            .ExecuteDeleteAsync();
                        Console.WriteLine($"[DEBUG] User deleted: {userDeleted > 0}");

                        await transaction.CommitAsync();
                        Console.WriteLine($"[DEBUG] Transaction committed successfully");

                        // Очищаем кэш
                        _cache.Remove(cacheKey);
                        Console.WriteLine($"[DEBUG] Cache cleared for key: {cacheKey}");

                        // Очищаем сессию
                        HttpContext.Session.Clear();
                        Console.WriteLine($"[DEBUG] Session cleared");

                        // Удаляем куки
                        Response.Cookies.Delete("AuthToken");
                        Response.Cookies.Delete("RememberMe");
                        Response.Cookies.Delete("UserEmail");
                        Console.WriteLine($"[DEBUG] Cookies deleted");

                        // Выход из аутентификации
                        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        Console.WriteLine($"[DEBUG] User signed out");

                        Console.WriteLine($"[DEBUG] ========== CONFIRM DELETE SUCCESS ==========");

                        return Json(new
                        {
                            success = true,
                            message = "Аккаунт успешно удален. Спасибо, что были с нами!",
                            redirectUrl = Url.Action("Index", "Home")
                        });
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        Console.WriteLine($"[DEBUG] ERROR in transaction: {ex.Message}");
                        Console.WriteLine($"[DEBUG] Stack trace: {ex.StackTrace}");
                        _logger.LogError(ex, "Ошибка при удалении аккаунта в транзакции");
                        return Json(new { success = false, message = "Ошибка при удалении аккаунта: " + ex.Message });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] CRITICAL ERROR in ConfirmDelete: {ex.Message}");
                Console.WriteLine($"[DEBUG] Stack trace: {ex.StackTrace}");
                _logger.LogError(ex, "Критическая ошибка при подтверждении удаления");
                return Json(new { success = false, message = "Произошла критическая ошибка: " + ex.Message });
            }
        }

        private PasswordValidationResult ValidatePassword(string password)
        {
            var result = new PasswordValidationResult { IsValid = true };

            // Проверка минимальной длины
            if (password.Length < 6)
            {
                result.IsValid = false;
                result.ErrorMessage = "Пароль должен содержать минимум 6 символов";
                return result;
            }

            // Проверка на наличие заглавных букв
            if (!Regex.IsMatch(password, @"[A-ZА-Я]"))
            {
                result.IsValid = false;
                result.ErrorMessage = "Пароль должен содержать хотя бы одну заглавную букву";
                return result;
            }

            // Проверка на наличие строчных букв
            if (!Regex.IsMatch(password, @"[a-zа-я]"))
            {
                result.IsValid = false;
                result.ErrorMessage = "Пароль должен содержать хотя бы одну строчную букву";
                return result;
            }

            // Проверка на наличие специальных символов
            if (!Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]"))
            {
                result.IsValid = false;
                result.ErrorMessage = "Пароль должен содержать хотя бы один специальный символ (!@#$%^&*()_+-=[]{};':\"|,.<>/? и т.д.)";
                return result;
            }

            return result;
        }

        // Метод для хэширования пароля
        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(password);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        // Метод для проверки валидности email
        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyCurrentPassword([FromBody] VerifyCurrentPasswordRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                    return Json(new { success = false, message = "Сессия истекла" });

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return Json(new { success = false, message = "Пользователь не найден" });

                // Проверяем текущий пароль
                var hashedPassword = HashPassword(request.CurrentPassword);
                if (user.PasswordHash != hashedPassword)
                    return Json(new { success = false, message = "Неверный текущий пароль" });

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке текущего пароля");
                return Json(new { success = false, message = "Произошла ошибка" });
            }
        }

        // POST: /Account/SendPasswordChangeCode
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendPasswordChangeCode([FromBody] PasswordChangeRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                    return Json(new { success = false, message = "Сессия истекла" });

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return Json(new { success = false, message = "Пользователь не найден" });

                // Проверяем новый пароль
                var passwordValidation = ValidatePassword(request.NewPassword);
                if (!passwordValidation.IsValid)
                    return Json(new { success = false, message = passwordValidation.ErrorMessage });

                // Генерируем 6-значный код
                var random = new Random();
                var code = random.Next(100000, 999999).ToString();

                // Сохраняем код в кэш на 15 минут
                var codeCacheKey = PASSWORD_CHANGE_CODE_PREFIX + userId;
                _cache.Set(codeCacheKey, code, TimeSpan.FromMinutes(15));

                // Сохраняем данные для смены пароля (новый пароль) на 15 минут
                var dataCacheKey = PASSWORD_CHANGE_DATA_PREFIX + userId;
                var passwordData = new PasswordChangeData
                {
                    NewPassword = request.NewPassword,
                    Timestamp = DateTime.UtcNow
                };
                _cache.Set(dataCacheKey, passwordData, TimeSpan.FromMinutes(15));

                // Отправляем код на email
                await _emailService.SendPasswordChangeCodeAsync(user.Email, code);

                _logger.LogInformation($"Код подтверждения смены пароля отправлен пользователю {user.Email}");

                return Json(new { success = true, message = "Код подтверждения отправлен на ваш email" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отправке кода подтверждения смены пароля");
                return Json(new { success = false, message = "Ошибка при отправке кода" });
            }
        }
        // GET: /Account/MyDocuments
        [Route("Account/MyDocuments")]
        public IActionResult MyDocuments()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login");

            return View(); // Ищет Views/Account/MyDocuments.cshtml
        }

        // POST: /Account/ChangePasswordWithVerification
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePasswordWithVerification([FromBody] VerifyPasswordChangeRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                    return Json(new { success = false, message = "Сессия истекла" });

                // Убираем все нецифровые символы
                var code = new string(request.VerificationCode.Where(char.IsDigit).ToArray());

                if (code.Length != 6)
                    return Json(new { success = false, message = "Код должен содержать 6 цифр" });

                // Проверяем код из кэша
                var codeCacheKey = PASSWORD_CHANGE_CODE_PREFIX + userId;
                if (!_cache.TryGetValue(codeCacheKey, out string cachedCode))
                    return Json(new { success = false, message = "Код истек или не был отправлен" });

                if (cachedCode != code)
                    return Json(new { success = false, message = "Неверный код подтверждения" });

                // Получаем данные для смены пароля
                var dataCacheKey = PASSWORD_CHANGE_DATA_PREFIX + userId;
                if (!_cache.TryGetValue(dataCacheKey, out PasswordChangeData passwordData))
                    return Json(new { success = false, message = "Данные для смены пароля устарели" });

                // Находим пользователя
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return Json(new { success = false, message = "Пользователь не найден" });

                // Обновляем пароль
                user.PasswordHash = HashPassword(passwordData.NewPassword);

                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                // Очищаем кэш
                _cache.Remove(codeCacheKey);
                _cache.Remove(dataCacheKey);

                // Отправляем уведомление об успешной смене пароля
                await SendPasswordChangeSuccessEmail(user.Email);

                _logger.LogInformation($"Пароль пользователя {user.Email} успешно изменен");

                return Json(new
                {
                    success = true,
                    message = "Пароль успешно изменен",
                    redirectUrl = Url.Action("Profile", "Account")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при смене пароля");
                return Json(new { success = false, message = "Произошла ошибка при смене пароля" });
            }
        }
        // GET: /Account/ForgotPassword
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // POST: /Account/SendResetCode
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendResetCode([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Email))
                    return Json(new { success = false, message = "Введите email" });

                if (!IsValidEmail(request.Email))
                    return Json(new { success = false, message = "Введите корректный email" });

                var email = request.Email.Trim().ToLower();

                // Ищем пользователя
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    // Для безопасности не говорим, что email не найден
                    return Json(new { success = true, message = "Если email зарегистрирован, код будет отправлен" });
                }

                // Генерируем 6-значный код
                var random = new Random();
                var code = random.Next(100000, 999999).ToString();

                // Сохраняем код в кэш на 15 минут
                var codeCacheKey = FORGOT_PASSWORD_CODE_PREFIX + email;
                _cache.Set(codeCacheKey, code, TimeSpan.FromMinutes(15));

                // Сохраняем данные для сброса пароля
                var dataCacheKey = FORGOT_PASSWORD_DATA_PREFIX + email;
                var resetData = new ForgotPasswordData
                {
                    Email = email,
                    UserId = user.IdUser,
                    Timestamp = DateTime.UtcNow
                };
                _cache.Set(dataCacheKey, resetData, TimeSpan.FromMinutes(15));

                // Отправляем код на email
                await SendPasswordResetCodeAsync(email, code);

                _logger.LogInformation($"Код сброса пароля отправлен на email {email}");

                return Json(new { success = true, message = "Код подтверждения отправлен на ваш email" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отправке кода сброса пароля");
                return Json(new { success = false, message = "Ошибка при отправке кода" });
            }
        }

        // POST: /Account/VerifyResetCode
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyResetCode([FromBody] VerifyResetCodeRequest request)
        {
            try
            {
                var email = request.Email?.Trim().ToLower();
                if (string.IsNullOrEmpty(email))
                    return Json(new { success = false, message = "Email не указан" });

                // Очищаем код от нецифровых символов
                var code = new string(request.Code.Where(char.IsDigit).ToArray());

                if (code.Length != 6)
                    return Json(new { success = false, message = "Код должен содержать 6 цифр" });

                // Проверяем код из кэша
                var codeCacheKey = FORGOT_PASSWORD_CODE_PREFIX + email;
                if (!_cache.TryGetValue(codeCacheKey, out string cachedCode))
                    return Json(new { success = false, message = "Код истек или не был отправлен" });

                if (cachedCode != code)
                    return Json(new { success = false, message = "Неверный код подтверждения" });

                // Получаем данные из кэша
                var dataCacheKey = FORGOT_PASSWORD_DATA_PREFIX + email;
                if (!_cache.TryGetValue(dataCacheKey, out ForgotPasswordData resetData))
                    return Json(new { success = false, message = "Данные для сброса пароля устарели" });

                return Json(new { success = true, message = "Код подтвержден" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке кода");
                return Json(new { success = false, message = "Ошибка при проверке кода" });
            }
        }

        // POST: /Account/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                var email = request.Email?.Trim().ToLower();
                if (string.IsNullOrEmpty(email))
                    return Json(new { success = false, message = "Email не указан" });

                // Проверяем новый пароль
                var passwordValidation = ValidatePassword(request.NewPassword);
                if (!passwordValidation.IsValid)
                    return Json(new { success = false, message = passwordValidation.ErrorMessage });

                // Проверяем данные в кэше
                var dataCacheKey = FORGOT_PASSWORD_DATA_PREFIX + email;
                if (!_cache.TryGetValue(dataCacheKey, out ForgotPasswordData resetData))
                    return Json(new { success = false, message = "Сессия сброса пароля истекла" });

                // Находим пользователя
                var user = await _context.Users.FindAsync(resetData.UserId);
                if (user == null)
                    return Json(new { success = false, message = "Пользователь не найден" });

                // Обновляем пароль
                user.PasswordHash = HashPassword(request.NewPassword);
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                // Очищаем кэш
                _cache.Remove(FORGOT_PASSWORD_CODE_PREFIX + email);
                _cache.Remove(FORGOT_PASSWORD_DATA_PREFIX + email);

                // Отправляем уведомление об успешной смене пароля
                await SendPasswordResetSuccessEmail(user.Email);

                _logger.LogInformation($"Пароль для пользователя {user.Email} успешно сброшен");

                return Json(new { success = true, message = "Пароль успешно изменен" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при сбросе пароля");
                return Json(new { success = false, message = "Ошибка при сбросе пароля" });
            }
        }
        // GET: /Account/MyOrders
        [HttpGet]
        public async Task<IActionResult> MyOrders()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login");

            var bookings = await _context.FlightBookings
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return View(bookings);
        }
        private async Task SendPasswordResetCodeAsync(string toEmail, string code)
        {
            var subject = "Восстановление пароля - Вместе В Путь";
            var body = $@"
    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
        <div style='background: #e8f4fe; border: 1px solid #b6d4fe; border-radius: 10px; padding: 20px; margin-bottom: 20px;'>
            <h2 style='color: #0379D9; margin-top: 0;'>
                <i class='fas fa-key'></i> Восстановление пароля
            </h2>
            <p style='color: #0379D9;'>
                Вы запросили восстановление пароля для вашего аккаунта в <strong>Вместе В Путь</strong>.
            </p>
        </div>
        
        <div style='text-align: center; margin: 30px 0;'>
            <h3 style='color: #333;'>Ваш код подтверждения:</h3>
            <div style='background: #f8f9fa; padding: 25px; border-radius: 12px; border: 3px dashed #0379D9; 
                        display: inline-block; margin: 20px 0;'>
                <h1 style='color: #0379D9; margin: 0; letter-spacing: 15px; font-size: 36px; font-weight: bold;'>
                    {code}
                </h1>
            </div>
            <p style='color: #666;'>
                Введите этот 6-значный код для восстановления доступа к аккаунту.
            </p>
        </div>
        
        <div style='background: #fff3cd; padding: 15px; border-radius: 8px; margin-bottom: 20px;'>
            <p style='margin: 0; color: #856404;'>
                <strong><i class='fas fa-shield-alt'></i> В целях безопасности</strong><br>
                Никогда никому не сообщайте этот код. Сотрудники поддержки никогда не запрашивают коды подтверждения.
            </p>
        </div>
        
        <div style='background: #e8f4fe; padding: 15px; border-radius: 8px; margin-bottom: 20px;'>
            <p style='margin: 0; color: #0379D9;'>
                <strong><i class='fas fa-clock'></i> Код действителен 15 минут</strong>
            </p>
        </div>
        
        <div style='border-top: 1px solid #eee; padding-top: 20px; margin-top: 30px;'>
            <p style='color: #888; font-size: 14px;'>
                <strong>Если вы не запрашивали восстановление пароля:</strong><br>
                Просто проигнорируйте это письмо. Ваш текущий пароль остается действительным.
            </p>
        </div>
        
        <div style='text-align: center; margin-top: 30px;'>
            <p style='color: #aaa; font-size: 12px;'>
                С уважением, команда <strong>Вместе В Путь</strong><br>
                {DateTime.Now.Year} © Все права защищены
            </p>
        </div>
    </div>";

            await _emailService.SendAsync(toEmail, subject, body);
        }

        private async Task SendPasswordResetSuccessEmail(string toEmail)
        {
            var subject = "Пароль успешно изменен - Вместе В Путь";
            var body = $@"
    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
        <div style='background: #d4edda; border: 1px solid #c3e6cb; border-radius: 10px; padding: 20px; margin-bottom: 20px;'>
            <h2 style='color: #155724; margin-top: 0;'>
                <i class='fas fa-check-circle'></i> Пароль успешно изменен
            </h2>
            <p style='color: #155724;'>
                Пароль для вашего аккаунта в <strong>Вместе В Путь</strong> был успешно изменен.
            </p>
        </div>
        
        <div style='background: #fff3cd; padding: 15px; border-radius: 8px; margin-bottom: 20px;'>
            <p style='margin: 0; color: #856404;'>
                <strong><i class='fas fa-exclamation-triangle'></i> Важно!</strong><br>
                Если вы не меняли пароль, немедленно свяжитесь с нашей службой поддержки.
            </p>
        </div>
        
        <div style='text-align: center; margin: 30px 0;'>
            <a href='{Url.Action("Login", "Account", null, "https")}' 
               style='display: inline-block; background: #0379D9; color: white; 
                      padding: 12px 30px; border-radius: 8px; text-decoration: none; 
                      font-weight: bold;'>
                Войти в аккаунт
            </a>
        </div>
        
        <div style='text-align: center; margin-top: 30px;'>
            <p style='color: #aaa; font-size: 12px;'>
                С уважением, команда <strong>Вместе В Путь</strong><br>
                {DateTime.Now.Year} © Все права защищены
            </p>
        </div>
    </div>";

            await _emailService.SendAsync(toEmail, subject, body);
        }
        private async Task SendPasswordChangeSuccessEmail(string toEmail)
        {
            var subject = "Пароль успешно изменен - Вместе В Путь";
            var body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
                <div style='background: #d4edda; border: 1px solid #c3e6cb; border-radius: 10px; padding: 20px; margin-bottom: 20px;'>
                    <h2 style='color: #155724; margin-top: 0;'>
                        <i class='fas fa-check-circle'></i> Пароль успешно изменен
                    </h2>
                    <p style='color: #155724;'>
                        Пароль для вашего аккаунта в <strong>Вместе В Путь</strong> был успешно изменен.
                    </p>
                </div>
                
                <div style='background: #fff3cd; padding: 15px; border-radius: 8px; margin-bottom: 20px;'>
                    <p style='margin: 0; color: #856404;'>
                        <strong><i class='fas fa-exclamation-triangle'></i> Важно!</strong><br>
                        Если вы не меняли пароль, немедленно свяжитесь с нашей службой поддержки.
                    </p>
                </div>
                
                <div style='border-top: 1px solid #eee; padding-top: 20px; margin-top: 30px;'>
                    <p style='color: #888; font-size: 14px;'>
                        Для дополнительной безопасности рекомендуется:<br>
                        1. Использовать уникальный пароль для каждого сервиса<br>
                        2. Включить двухфакторную аутентификацию (если доступно)<br>
                        3. Регулярно обновлять пароль
                    </p>
                </div>
                
                <div style='text-align: center; margin-top: 30px;'>
                    <p style='color: #aaa; font-size: 12px;'>
                        С уважением, команда <strong>Вместе В Путь</strong><br>
                        {DateTime.Now.Year} © Все права защищены
                    </p>
                </div>
            </div>";

            await _emailService.SendAsync(toEmail, subject, body);
        
            }
        }



    // Вспомогательный класс для результата проверки пароля
    public class PasswordValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
    public class VerifyCurrentPasswordRequest
    {
        public string CurrentPassword { get; set; }
    }

    public class PasswordChangeRequest
    {
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
    }

    public class VerifyPasswordChangeRequest
    {
        public string VerificationCode { get; set; }
    }

    public class PasswordChangeData
    {
        public string NewPassword { get; set; }
        public DateTime Timestamp { get; set; }
    }
    // Вспомогательные классы
    public class ForgotPasswordRequest
    {
        public string Email { get; set; }
    }

    public class VerifyResetCodeRequest
    {
        public string Email { get; set; }
        public string Code { get; set; }
    }

    public class ResetPasswordRequest
    {
        public string Email { get; set; }
        public string NewPassword { get; set; }
    }

    public class ForgotPasswordData
    {
        public string Email { get; set; }
        public int UserId { get; set; }
        public DateTime Timestamp { get; set; }
    }
    public class ConfirmDeleteRequest
    {
        public string Code { get; set; }
    }
}