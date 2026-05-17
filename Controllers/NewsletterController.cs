using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWise.Models;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace TripWise.Controllers
{
    [Route("Newsletter")]
    public class NewsletterController : Controller
    {
        private readonly TripWiseContext _context;
        private readonly ILogger<NewsletterController> _logger;
        private readonly EmailService _emailService;
        private readonly IConfiguration _configuration;

        public NewsletterController(TripWiseContext context,
            ILogger<NewsletterController> logger,
            EmailService emailService, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _emailService = emailService;
            _configuration = configuration;
        }

        // POST: /Newsletter/Subscribe
        [HttpPost]
        [Route("Subscribe")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Subscribe([FromForm] string email)
        {
            try
            {
                _logger.LogInformation($"Subscribe attempt for email: {email}");

                // Валидация email
                if (string.IsNullOrWhiteSpace(email))
                {
                    _logger.LogWarning("Empty email provided");
                    return Json(new { success = false, message = "Введите email адрес" });
                }

                email = email.Trim().ToLower();

                if (!IsValidEmail(email))
                {
                    _logger.LogWarning($"Invalid email format: {email}");
                    return Json(new { success = false, message = "Введите корректный email" });
                }

                // Проверка, подписан ли уже пользователь
                var existingSubscription = await _context.NewsletterSubscriptions
                    .FirstOrDefaultAsync(ns => ns.Email == email);

                if (existingSubscription != null)
                {
                    if (existingSubscription.IsActive)
                    {
                        _logger.LogInformation($"Email {email} is already subscribed");
                        return Json(new
                        {
                            success = false,
                            message = "Этот email уже подписан на рассылку",
                            alreadySubscribed = true
                        });
                    }
                    else
                    {
                        // Возобновляем подписку
                        existingSubscription.IsActive = true;
                        existingSubscription.UnsubscribedAt = null;
                        existingSubscription.SubscribedAt = DateTime.UtcNow;
                        existingSubscription.Source = "footer";

                        _context.NewsletterSubscriptions.Update(existingSubscription);
                        await _context.SaveChangesAsync();

                        _logger.LogInformation($"Reactivated subscription for: {email}");

                        // Отправляем приветственное письмо
                        await SendWelcomeEmail(email, true);

                        return Json(new
                        {
                            success = true,
                            message = "Вы снова подписаны на рассылку! Проверьте ваш email."
                        });
                    }
                }
                else
                {
                    // Создаем новую подписку
                    var subscription = new NewsletterSubscription
                    {
                        Email = email,
                        SubscribedAt = DateTime.UtcNow,
                        IsActive = true,
                        Source = "footer"
                    };

                    _context.NewsletterSubscriptions.Add(subscription);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"New subscription created for: {email}");

                    // Отправляем приветственное письмо
                    await SendWelcomeEmail(email, false);

                    return Json(new
                    {
                        success = true,
                        message = "Вы успешно подписались на рассылку! Проверьте ваш email."
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при подписке на рассылку");
                return Json(new
                {
                    success = false,
                    message = "Произошла ошибка при подписке. Попробуйте позже."
                });
            }
        }

        // GET: /Newsletter/CheckSubscription
        [HttpGet]
        [Route("CheckSubscription")]
        public async Task<IActionResult> CheckSubscription([FromQuery] string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                {
                    return Json(new { error = "Email не указан" });
                }

                email = email.Trim().ToLower();

                var subscription = await _context.NewsletterSubscriptions
                    .FirstOrDefaultAsync(ns => ns.Email == email);

                return Json(new
                {
                    email = email,
                    isSubscribed = subscription?.IsActive ?? false,
                    subscribedAt = subscription?.SubscribedAt.ToString("yyyy-MM-dd HH:mm"),
                    source = subscription?.Source
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке подписки");
                return Json(new { error = "Произошла ошибка при проверке подписки" });
            }
        }
        // GET: /Newsletter/Unsubscribe
        [HttpGet]
        [Route("Unsubscribe")]
        public async Task<IActionResult> Unsubscribe(string email, string token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
                {
                    ViewBag.Error = "Некорректная ссылка для отписки";
                    return View();
                }

                email = email.Trim().ToLower();

                // Декодируем email из URL
                email = Uri.UnescapeDataString(email);

                // Проверяем токен (простая валидация)
                var expectedToken = GenerateUnsubscribeToken(email);
                if (token != expectedToken)
                {
                    ViewBag.Error = "Недействительная ссылка для отписки";
                    return View();
                }

                var subscription = await _context.NewsletterSubscriptions
                    .FirstOrDefaultAsync(ns => ns.Email == email);

                if (subscription == null)
                {
                    ViewBag.Error = "Email не найден в списке подписчиков";
                    return View();
                }

                if (!subscription.IsActive)
                {
                    ViewBag.Success = true;
                    ViewBag.Message = "Вы уже отписаны от рассылки.";
                    return View();
                }

                ViewBag.Email = email;
                ViewBag.Token = token;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при открытии страницы отписки");
                ViewBag.Error = "Произошла ошибка. Пожалуйста, попробуйте позже.";
                return View();
            }
        }

        // POST: /Newsletter/UnsubscribeConfirm
        [HttpPost]
        [Route("UnsubscribeConfirm")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnsubscribeConfirm(string email, string token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
                {
                    ViewBag.Error = "Некорректные данные для отписки";
                    return View("Unsubscribe");
                }

                email = email.Trim().ToLower();

                // Проверяем токен
                var expectedToken = GenerateUnsubscribeToken(email);
                if (token != expectedToken)
                {
                    ViewBag.Error = "Недействительная ссылка для отписки";
                    return View("Unsubscribe");
                }

                var subscription = await _context.NewsletterSubscriptions
                    .FirstOrDefaultAsync(ns => ns.Email == email);

                if (subscription == null)
                {
                    ViewBag.Error = "Email не найден в списке подписчиков";
                    return View("Unsubscribe");
                }

                if (!subscription.IsActive)
                {
                    ViewBag.Success = true;
                    ViewBag.Message = "Вы уже отписаны от рассылки.";
                    return View("Unsubscribe");
                }

                // Отписываем
                subscription.IsActive = false;
                subscription.UnsubscribedAt = DateTime.UtcNow;

                _context.NewsletterSubscriptions.Update(subscription);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"User {email} unsubscribed from newsletter via link");

                // Отправляем письмо об отписке
                await SendUnsubscribeEmail(email);

                ViewBag.Success = true;
                ViewBag.Message = "Вы успешно отписались от рассылки. Мы будем рады видеть вас снова!";

                return View("Unsubscribe");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отписке от рассылки");
                ViewBag.Error = "Произошла ошибка при отписке. Пожалуйста, попробуйте позже.";
                return View("Unsubscribe");
            }
        }

        private string GenerateUnsubscribeToken(string email)
        {
            var salt = _configuration["NewsletterSettings:UnsubscribeSalt"] ?? "DefaultSecretSalt123!";
            var input = email + salt;
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(input);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash)
                .Replace("/", "_")
                .Replace("+", "-")
                .Replace("=", "");
        }

        // GET: /Newsletter/UnsubscribePage
        [HttpGet]
        [Route("UnsubscribePage")]
        public IActionResult UnsubscribePage()
        {
            return View("Unsubscribe");
        }

        // POST: /Newsletter/Unsubscribe
        [HttpPost]
        [Route("Unsubscribe")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unsubscribe([FromForm] string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                {
                    return Json(new { success = false, message = "Email не указан" });
                }

                email = email.Trim().ToLower();

                var subscription = await _context.NewsletterSubscriptions
                    .FirstOrDefaultAsync(ns => ns.Email == email);

                if (subscription == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Email не найден в списке подписчиков"
                    });
                }

                if (!subscription.IsActive)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Вы уже отписаны от рассылки"
                    });
                }

                // Отписываем
                subscription.IsActive = false;
                subscription.UnsubscribedAt = DateTime.UtcNow;

                _context.NewsletterSubscriptions.Update(subscription);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"User {email} unsubscribed from newsletter");

                // Отправляем письмо об отписке
                await SendUnsubscribeEmail(email);

                return Json(new
                {
                    success = true,
                    message = "Вы успешно отписались от рассылки"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отписке от рассылки");
                return Json(new
                {
                    success = false,
                    message = "Произошла ошибка при отписке"
                });
            }
        }

        // POST: /Newsletter/UnsubscribeFromAccount
        [HttpPost]
        [Route("UnsubscribeFromAccount")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnsubscribeFromAccount([FromBody] UnsubscribeRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new { success = false, message = "Не авторизован" });
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return Json(new { success = false, message = "Пользователь не найден" });
                }

                var email = user.Email;
                if (string.IsNullOrWhiteSpace(email))
                {
                    return Json(new { success = false, message = "Email не указан" });
                }

                email = email.Trim().ToLower();

                var subscription = await _context.NewsletterSubscriptions
                    .FirstOrDefaultAsync(ns => ns.Email == email);

                if (subscription == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Email не найден в списке подписчиков"
                    });
                }

                if (!subscription.IsActive)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Вы уже отписаны от рассылки"
                    });
                }

                // Отписываем
                subscription.IsActive = false;
                subscription.UnsubscribedAt = DateTime.UtcNow;

                _context.NewsletterSubscriptions.Update(subscription);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"User {email} unsubscribed from newsletter");

                // Отправляем письмо об отписке
                await SendUnsubscribeEmail(email);

                return Json(new
                {
                    success = true,
                    message = "Вы успешно отписались от рассылки"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отписке от рассылки");
                return Json(new
                {
                    success = false,
                    message = "Произошла ошибка при отписке"
                });
            }
        }

        private async Task SendWelcomeEmail(string email, bool isReactivation = false)
        {
            try
            {
                var subject = isReactivation
                    ? "С возвращением в рассылку Вместе В Путь! 🎉"
                    : "Добро пожаловать в рассылку Вместе В Путь! 🎉";

                // Генерируем токен для отписки
                var unsubscribeToken = GenerateUnsubscribeToken(email);
                var encodedEmail = Uri.EscapeDataString(email);
                var unsubscribeLink = Url.Action("Unsubscribe", "Newsletter",
                    new { email = encodedEmail, token = unsubscribeToken }, "https");

                var body = $@"
        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
            <div style='text-align: center; margin-bottom: 30px;'>
                <h2 style='color: #0379D9;'>
                    {(isReactivation ? "С возвращением!" : "Спасибо за подписку!")}
                </h2>
            </div>
            
            <div style='background: #f8f9fa; padding: 20px; border-radius: 10px; margin-bottom: 20px;'>
                <h3 style='color: #333; margin-top: 0;'>Что вас ждет?</h3>
                <ul style='color: #555; line-height: 1.6; padding-left: 20px;'>
                    <li>🔥 Лучшие предложения на авиабилеты и отели</li>
                    <li>📅 Уведомления о скидках и акциях</li>
                    <li>🗺️ Полезные советы для путешественников</li>
                    <li>👥 Идеи для групповых поездок</li>
                </ul>
            </div>
            
            <div style='text-align: center; margin: 30px 0;'>
                <a href='{Url.Action("Index", "Home", null, "https")}' 
                   style='display: inline-block; background: #0379D9; color: white; 
                          padding: 12px 30px; border-radius: 8px; text-decoration: none; 
                          font-weight: bold;'>
                    Начать планирование
                </a>
            </div>
            
            <div style='border-top: 1px solid #eee; padding-top: 20px; margin-top: 30px;'>
                <p style='color: #888; font-size: 12px; text-align: center;'>
                    <strong>Важно:</strong> Если вы не хотите получать рассылку, 
                    <a href='{unsubscribeLink}' 
                       style='color: #0379D9; text-decoration: underline;'>отпишитесь здесь</a>.
                </p>
            </div>
            
            <div style='text-align: center; margin-top: 30px; padding-top: 20px; border-top: 1px solid #eee;'>
                <p style='color: #aaa; font-size: 12px;'>
                    С уважением, команда <strong>Вместе В Путь</strong><br>
                    {DateTime.Now.Year} © Все права защищены
                </p>
            </div>
        </div>";

                await _emailService.SendAsync(email, subject, body);
                _logger.LogInformation($"Welcome email sent to: {email}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send welcome email to: {email}");
            }
        }

        private async Task SendUnsubscribeEmail(string email)
        {
            try
            {
                var subject = "Вы отписались от рассылки Вместе В Путь";

                // Генерируем ссылку для повторной подписки (опционально)
                var resubscribeLink = Url.Action("Index", "Home", null, "https");

                var body = $@"
        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
            <div style='text-align: center; margin-bottom: 30px;'>
                <h2 style='color: #666;'>Мы сожалеем, что вы уходите</h2>
            </div>
            
            <div style='background: #f8f9fa; padding: 20px; border-radius: 10px; margin-bottom: 20px;'>
                <p style='color: #555; text-align: center;'>
                    Вы успешно отписались от рассылки <strong>Вместе В Путь</strong>.
                </p>
                <p style='color: #555; text-align: center;'>
                    Если это произошло по ошибке, вы можете 
                    <a href='{resubscribeLink}' 
                       style='color: #0379D9;'>снова подписаться</a> в любое время.
                </p>
            </div>
            
            <div style='text-align: center; margin-top: 30px; padding-top: 20px; border-top: 1px solid #eee;'>
                <p style='color: #aaa; font-size: 12px;'>
                    С уважением, команда <strong>Вместе В Путь</strong><br>
                    {DateTime.Now.Year} © Все права защищены
                </p>
            </div>
        </div>";

                await _emailService.SendAsync(email, subject, body);
                _logger.LogInformation($"Unsubscribe email sent to: {email}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send unsubscribe email to: {email}");
            }
        }

        public class UnsubscribeRequest
        {
            public string Email { get; set; } = "";
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
}