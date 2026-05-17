using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWise.Models;
using TripWise.Models.ViewModels;
using System.Diagnostics;

namespace TripWise.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly TripWiseContext _context;
    private readonly EmailService _emailService;

    public HomeController(ILogger<HomeController> logger, TripWiseContext context, EmailService emailService)
    {
        _logger = logger;
        _context = context;
        _emailService = emailService;
    }

    public async Task<IActionResult> Index()
    {
        var model = new HomeViewModel();

        try
        {
            // Получаем последние 6 одобренных отзывов и преобразуем в HomeReviewDto
            var recentReviews = await _context.Reviews
                .Where(r => !r.IsDeleted && r.IsApproved)
                .OrderByDescending(r => r.CreatedAt)
                .Take(6)
                .Select(r => new HomeReviewDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    Rating = r.Rating,
                    Text = r.Text,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync();

            model.RecentReviews = recentReviews;
            var allReviews = await _context.Reviews
                .Where(r => !r.IsDeleted && r.IsApproved)
                .ToListAsync();

            if (allReviews.Any())
            {
                model.Statistics = new ReviewStatisticsDto
                {
                    TotalReviews = allReviews.Count,
                    AverageRating = Math.Round(allReviews.Average(r => r.Rating), 1),
                    RatingCounts = new Dictionary<int, int>
                {
                    { 5, allReviews.Count(r => r.Rating == 5) },
                    { 4, allReviews.Count(r => r.Rating == 4) },
                    { 3, allReviews.Count(r => r.Rating == 3) },
                    { 2, allReviews.Count(r => r.Rating == 2) },
                    { 1, allReviews.Count(r => r.Rating == 1) }
                }
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке отзывов на главную страницу");
        }

        return View(model);
    }

    public IActionResult Flights()
    {
        return View();
    }
    public IActionResult MyOrders()
    {
        // Проверяем авторизацию
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null)
        {
            return RedirectToAction("Login", "Account");
        }

        // Указываем явно, что представление находится в папке Account
        return View("~/Views/Account/MyOrders.cshtml");
    }
    public IActionResult Railway()
    {
        return View();
    }

    public IActionResult Hotels()
    {
        return View();
    }

    public IActionResult Trips()
    {
        return View();
    }

    public IActionResult Groups()
    {
        return View();
    }

    public IActionResult Budget()
    {
        return View();
    }

    public IActionResult Activities()
    {
        return View();
    }

    public IActionResult Favorites()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Terms()
    {
        return View();
    }

    public IActionResult About()
    {
        return View();
    }

    public IActionResult Partners()
    {
        return View();
    }

    public IActionResult Help()
    {
        return View();
    }

    public IActionResult Contact()
    {
        return View();
    }

    public IActionResult FAQ()
    {
        return View();
    }

    public IActionResult Reviews()
    {
        return View();
    }

    public IActionResult Chats()
    {
        // Проверяем авторизацию пользователя
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null)
        {
            // Пользователь не авторизован, но показываем страницу
            // В реальном приложении можно редиректить на логин
        }

        return View();
    }

    // POST: /Home/SendContactMessage
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendContactMessage([FromBody] ContactMessageRequest request)
    {
        try
        {
            // Валидация
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Json(new { success = false, message = "Введите ваше имя" });
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return Json(new { success = false, message = "Введите ваш email" });
            }

            if (!IsValidEmail(request.Email))
            {
                return Json(new { success = false, message = "Введите корректный email адрес" });
            }

            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return Json(new { success = false, message = "Введите сообщение" });
            }

            _logger.LogInformation($"Получено сообщение от {request.Name} ({request.Email})");

            // Формируем письмо для администратора
            var adminSubject = $"Обратная связь: {request.Subject} от {request.Name}";
            var adminBody = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 16px;'>
                <h2 style='color: #0379D9; margin-bottom: 20px;'>📧 Новое сообщение с сайта</h2>
                
                <div style='background: #f8fafc; padding: 15px; border-radius: 12px; margin-bottom: 20px;'>
                    <p style='margin: 5px 0;'><strong>Отправитель:</strong> {request.Name}</p>
                    <p style='margin: 5px 0;'><strong>Email:</strong> {request.Email}</p>
                    <p style='margin: 5px 0;'><strong>Тема:</strong> {request.Subject}</p>
                    <p style='margin: 5px 0;'><strong>Дата:</strong> {DateTime.Now:dd.MM.yyyy HH:mm}</p>
                </div>
                
                <div style='background: #f8fafc; padding: 15px; border-radius: 12px; margin-bottom: 20px;'>
                    <p style='margin: 0 0 10px 0;'><strong>Сообщение:</strong></p>
                    <p style='margin: 0; line-height: 1.6;'>{request.Message.Replace("\n", "<br>")}</p>
                </div>
                
                <hr style='border-color: #e2e8f0; margin: 20px 0;'>
                
                <p style='color: #64748b; font-size: 12px; text-align: center;'>
                    Это сообщение отправлено через форму обратной связи на сайте tripwise.ru
                </p>
            </div>";

            // Отправляем письмо администратору
            await _emailService.SendAsync("tripwise@yandex.ru", adminSubject, adminBody);

            // Формируем письмо-подтверждение для пользователя
            var userSubject = "Ваше сообщение получено - Вместе В Путь";
            var userBody = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 16px;'>
                <h2 style='color: #0379D9; margin-bottom: 20px;'>Здравствуйте, {request.Name}!</h2>
                
                <p style='margin-bottom: 20px;'>Спасибо за ваше обращение. Мы получили ваше сообщение и ответим вам в ближайшее время.</p>
                
                <div style='background: #f8fafc; padding: 15px; border-radius: 12px; margin-bottom: 20px;'>
                    <p style='margin: 0 0 10px 0;'><strong>Ваше сообщение:</strong></p>
                    <p style='margin: 0; line-height: 1.6;'>{request.Message.Replace("\n", "<br>")}</p>
                </div>
                
                <div style='background: #e8f4fe; padding: 15px; border-radius: 12px; margin-bottom: 20px;'>
                    <p style='margin: 0; color: #0379D9;'>
                        <strong>💡 Что дальше?</strong><br>
                        Наш специалист свяжется с вами в ближайшее время. Обычно мы отвечаем в течение 1-2 часов в рабочее время.
                    </p>
                </div>
                
                <hr style='border-color: #e2e8f0; margin: 20px 0;'>
                
                <p style='color: #64748b; font-size: 12px; text-align: center;'>
                    С уважением,<br>
                    <strong>Команда Вместе В Путь</strong><br>
                    {DateTime.Now.Year} © Все права защищены
                </p>
                
                <p style='color: #94a3b8; font-size: 10px; text-align: center; margin-top: 15px;'>
                    Если вы не отправляли это сообщение, просто проигнорируйте его.
                </p>
            </div>";

            // Отправляем подтверждение пользователю
            await _emailService.SendAsync(request.Email, userSubject, userBody);

            _logger.LogInformation($"Сообщение от {request.Email} успешно отправлено");

            return Json(new
            {
                success = true,
                message = "Ваше сообщение отправлено! Мы свяжемся с вами в ближайшее время."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке сообщения через форму обратной связи");
            return Json(new
            {
                success = false,
                message = "Произошла ошибка при отправке сообщения. Пожалуйста, попробуйте позже или напишите нам напрямую на tripwise@yandex.ru"
            });
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

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
}

// DTO для запроса контактной формы
public class ContactMessageRequest
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Message { get; set; } = "";
}