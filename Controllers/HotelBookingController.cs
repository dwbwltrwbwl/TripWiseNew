// Controllers/HotelBookingController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWise.Models;
using TripWise.Models.ViewModels;
using TripWise.Services;
using Microsoft.Extensions.Caching.Memory;

namespace TripWise.Controllers
{
    public class HotelBookingController : Controller
    {
        private readonly TripWiseContext _context;
        private readonly EmailService _emailService;
        private readonly ILogger<HotelBookingController> _logger;
        private readonly IMemoryCache _cache;

        public HotelBookingController(
            TripWiseContext context,
            EmailService emailService,
            ILogger<HotelBookingController> logger,
            IMemoryCache memoryCache)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
            _cache = memoryCache;
        }

        // GET: /HotelBooking/Book
        [HttpGet]
        public IActionResult Book([FromQuery] HotelBookingViewModel model)
        {
            if (model == null || string.IsNullOrEmpty(model.HotelId))
            {
                return RedirectToAction("Index", "Hotels");
            }

            // Если пользователь авторизован, подставляем его данные
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId.HasValue)
            {
                var user = _context.Users.Find(userId.Value);
                if (user != null)
                {
                    model.ContactEmail = user.Email;
                    model.ContactName = $"{user.FirstName} {user.LastName}".Trim();
                }
            }

            return View(model);
        }

        // POST: /HotelBooking/ProcessBooking
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessBooking([FromBody] HotelBookingViewModel model)
        {
            try
            {
                _logger.LogInformation("=== НАЧАЛО БРОНИРОВАНИЯ ОТЕЛЯ ===");
                _logger.LogInformation("Модель: {@Model}", model);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return Json(new { success = false, message = "Проверьте правильность заполнения полей", errors });
                }

                var userId = HttpContext.Session.GetInt32("UserId") ?? 0;

                // Создаем бронирование со ВСЕМИ обязательными полями
                var booking = new HotelBooking
                {
                    Id = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserId = userId,
                    BookingNumber = "HTL" + DateTime.Now.ToString("yyyyMMddHHmmss") + new Random().Next(100, 999),

                    // Информация об отеле (все NOT NULL)
                    HotelId = model.HotelId ?? "unknown",
                    HotelName = model.HotelName ?? "Отель не указан",
                    HotelAddress = string.IsNullOrEmpty(model.HotelAddress) ? "Адрес не указан" : model.HotelAddress,
                    HotelPhone = string.IsNullOrEmpty(model.HotelPhone) ? "Не указан" : model.HotelPhone,
                    HotelWebsite = string.IsNullOrEmpty(model.HotelWebsite) ? "" : model.HotelWebsite,
                    HotelLatitude = model.HotelLatitude,
                    HotelLongitude = model.HotelLongitude,
                    AccommodationType = string.IsNullOrEmpty(model.AccommodationType) ? "Отель" : model.AccommodationType,
                    Stars = model.Stars,

                    // Даты
                    CheckInDate = model.CheckInDate,
                    CheckOutDate = model.CheckOutDate,
                    Nights = (model.CheckOutDate - model.CheckInDate).Days,
                    Guests = model.Guests,
                    Rooms = model.Rooms,
                    PricePerNight = model.PricePerNight,
                    TotalPrice = model.PricePerNight * (model.CheckOutDate - model.CheckInDate).Days * model.Rooms,

                    // Контактные данные (все NOT NULL)
                    ContactName = model.ContactName ?? "Не указано",
                    ContactEmail = model.ContactEmail ?? "email@example.com",
                    ContactPhone = model.ContactPhone ?? "0000000000",
                    SpecialRequests = string.IsNullOrEmpty(model.SpecialRequests) ? "" : model.SpecialRequests,

                    // Статусы (все NOT NULL)
                    Status = BookingStatus.Pending,  // Изменено: Pending вместо Confirmed
                    PaymentStatus = PaymentStatus.Pending,  // Изменено: Pending вместо Paid
                    PaymentMethod = "Не оплачено", // Изменено: теперь ждем оплаты
                    TransactionId = null, // NOT NULL? В БД должно быть nullable, измените в модели если нужно

                    // Даты создания
                    CreatedAt = DateTime.UtcNow,
                    ConfirmedAt = DateTime.UtcNow,

                    // Остальные поля (все NOT NULL в БД)
                    CancellationReason = "", // NOT NULL - обязательно!
                    Currency = "RUB", // NOT NULL - обязательно!
                    Notes = "", // NOT NULL - обязательно!

                    // Nullable поля
                    CancelledAt = null,
                    CheckedInAt = null,
                    CheckedOutAt = null
                };

                _logger.LogInformation("Создан объект бронирования: {@Booking}", booking);

                _context.HotelBookings.Add(booking);
                await _context.SaveChangesAsync();
                // После успешного сохранения бронирования - удаляем отель из избранного
                try
                {
                    var userId2 = HttpContext.Session.GetInt32("UserId");
                    if (userId2.HasValue && !string.IsNullOrEmpty(model.HotelId))
                    {
                        // Находим запись в избранном
                        var favorite = await _context.FavoriteHotels
                            .FirstOrDefaultAsync(f => f.UserId == userId2.Value && f.HotelId == model.HotelId);

                        if (favorite != null)
                        {
                            _context.FavoriteHotels.Remove(favorite);
                            await _context.SaveChangesAsync();
                            _logger.LogInformation("Отель {HotelName} удален из избранного после бронирования", model.HotelName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Не удалось удалить отель из избранного: {Message}", ex.Message);
                }
                _logger.LogInformation("Сохранение успешно! ID: {BookingId}", booking.Id);

                // Сохраняем в кэш для страницы подтверждения
                var cacheKey = "HotelBooking_" + booking.Id;
                _cache.Set(cacheKey, booking, TimeSpan.FromMinutes(30));

                // Отправляем подтверждение на email
                await SendBookingConfirmationEmail(booking);

                return Json(new
                {
                    success = true,
                    message = "Бронирование успешно оформлено",
                    redirectUrl = Url.Action("Confirmation", new { bookingId = booking.Id })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при бронировании отеля");

                // Детальный вывод ошибки
                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage += " | Inner: " + ex.InnerException.Message;
                }

                return Json(new { success = false, message = "Ошибка: " + errorMessage });
            }
        }

        // GET: /HotelBooking/Confirmation
        [HttpGet]
        public IActionResult Confirmation(string bookingId)
        {
            if (string.IsNullOrEmpty(bookingId))
                return RedirectToAction("Index", "Hotels");

            var cacheKey = "HotelBooking_" + bookingId;
            if (_cache.TryGetValue(cacheKey, out HotelBooking booking))
            {
                var viewModel = new HotelBookingConfirmationViewModel
                {
                    BookingId = booking.Id,
                    BookingNumber = booking.BookingNumber,
                    HotelName = booking.HotelName,
                    HotelAddress = booking.HotelAddress,
                    CheckInDate = booking.CheckInDate,
                    CheckOutDate = booking.CheckOutDate,
                    Nights = booking.Nights,
                    Guests = booking.Guests,
                    Rooms = booking.Rooms,
                    PricePerNight = booking.PricePerNight,
                    TotalPrice = booking.TotalPrice,
                    ContactName = booking.ContactName,
                    ContactEmail = booking.ContactEmail,
                    ContactPhone = booking.ContactPhone,
                    SpecialRequests = booking.SpecialRequests,
                    CreatedAt = booking.CreatedAt,
                    Status = GetStatusText(booking.Status)
                };
                return View(viewModel);
            }

            // Если нет в кэше, ищем в БД
            var dbBooking = _context.HotelBookings.FirstOrDefault(b => b.Id == bookingId);
            if (dbBooking != null)
            {
                var viewModel = new HotelBookingConfirmationViewModel
                {
                    BookingId = dbBooking.Id,
                    BookingNumber = dbBooking.BookingNumber,
                    HotelName = dbBooking.HotelName,
                    HotelAddress = dbBooking.HotelAddress,
                    CheckInDate = dbBooking.CheckInDate,
                    CheckOutDate = dbBooking.CheckOutDate,
                    Nights = dbBooking.Nights,
                    Guests = dbBooking.Guests,
                    Rooms = dbBooking.Rooms,
                    PricePerNight = dbBooking.PricePerNight,
                    TotalPrice = dbBooking.TotalPrice,
                    ContactName = dbBooking.ContactName,
                    ContactEmail = dbBooking.ContactEmail,
                    ContactPhone = dbBooking.ContactPhone,
                    SpecialRequests = dbBooking.SpecialRequests,
                    CreatedAt = dbBooking.CreatedAt,
                    Status = GetStatusText(dbBooking.Status)
                };
                return View(viewModel);
            }

            return RedirectToAction("Index", "Hotels");
        }

        // GET: /HotelBooking/MyBookings
        [HttpGet]
        public async Task<IActionResult> MyBookings()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var bookings = await _context.HotelBookings
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return View(bookings);
        }

        // GET: /HotelBooking/Details/{bookingId}
        [HttpGet]
        public async Task<IActionResult> Details(string bookingId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            var booking = await _context.HotelBookings
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null)
                return NotFound();

            if (booking.UserId != userId && !User.IsInRole("Admin"))
                return Forbid();

            var viewModel = new HotelBookingConfirmationViewModel
            {
                BookingId = booking.Id,
                BookingNumber = booking.BookingNumber,
                HotelName = booking.HotelName,
                HotelAddress = booking.HotelAddress,
                CheckInDate = booking.CheckInDate,
                CheckOutDate = booking.CheckOutDate,
                Nights = booking.Nights,
                Guests = booking.Guests,
                Rooms = booking.Rooms,
                PricePerNight = booking.PricePerNight,
                TotalPrice = booking.TotalPrice,
                ContactName = booking.ContactName,
                ContactEmail = booking.ContactEmail,
                ContactPhone = booking.ContactPhone,
                SpecialRequests = booking.SpecialRequests,
                CreatedAt = booking.CreatedAt,
                Status = GetStatusText(booking.Status)
            };

            return View(viewModel);
        }

        // POST: /HotelBooking/Cancel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel([FromBody] CancelBookingRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");

                var booking = await _context.HotelBookings
                    .FirstOrDefaultAsync(b => b.Id == request.BookingId);

                if (booking == null)
                    return Json(new { success = false, message = "Бронирование не найдено" });

                if (booking.UserId != userId && !User.IsInRole("Admin"))
                    return Json(new { success = false, message = "Нет прав для отмены" });

                // Проверяем, можно ли отменить
                if (booking.CheckInDate <= DateTime.Today.AddDays(1))
                {
                    return Json(new { success = false, message = "Отмена невозможна менее чем за 24 часа до заезда" });
                }

                booking.Status = BookingStatus.Cancelled;
                booking.CancelledAt = DateTime.UtcNow;
                booking.CancellationReason = request.Reason;

                await _context.SaveChangesAsync();

                // Отправляем уведомление об отмене
                await SendCancellationEmail(booking);

                return Json(new { success = true, message = "Бронирование отменено" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отмене бронирования");
                return Json(new { success = false, message = "Ошибка при отмене: " + ex.Message });
            }
        }

        private async Task SendBookingConfirmationEmail(HotelBooking booking)
        {
            var subject = $"Подтверждение бронирования отеля {booking.HotelName} - Вместе В Путь";

            var checkInDate = booking.CheckInDate.ToString("dd.MM.yyyy");
            var checkOutDate = booking.CheckOutDate.ToString("dd.MM.yyyy");

            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        body {{ font-family: 'Arial', sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; color: #333; }}
        .booking {{ border: 2px solid #0379D9; border-radius: 12px; padding: 20px; background: #f8fafc; }}
        .header {{ background: linear-gradient(135deg, #0379D9, #40B624); color: white; padding: 20px; border-radius: 12px 12px 0 0; margin: -20px -20px 20px -20px; }}
        .header h2 {{ margin: 0; font-size: 24px; }}
        .hotel-name {{ font-size: 28px; font-weight: bold; text-align: center; margin: 20px 0; color: #0379D9; }}
        .info {{ display: grid; grid-template-columns: 1fr 1fr; gap: 15px; margin: 20px 0; }}
        .info-item {{ border-bottom: 1px solid #e2e8f0; padding: 10px 0; }}
        .info-item .label {{ color: #64748b; font-size: 12px; }}
        .info-item .value {{ font-size: 16px; font-weight: bold; color: #334155; }}
        .price {{ background: #e8f4fe; padding: 15px; border-radius: 8px; text-align: center; margin: 20px 0; }}
        .price .total {{ font-size: 24px; font-weight: bold; color: #0379D9; }}
        .payment-info {{ background: #fff3cd; padding: 15px; border-radius: 8px; text-align: center; margin: 20px 0; }}
        .payment-info p {{ margin: 0; }}
        .btn {{ display: inline-block; background: #40B624; color: white; padding: 10px 20px; text-decoration: none; border-radius: 8px; margin-top: 10px; }}
        .footer {{ text-align: center; margin-top: 30px; color: #94a3b8; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='booking'>
        <div class='header'>
            <h2>Подтверждение бронирования</h2>
            <p>Номер брони: {booking.BookingNumber}</p>
        </div>

        <div class='hotel-name'>
            {booking.HotelName}
        </div>

        <div class='info'>
            <div class='info-item'>
                <div class='label'>Адрес отеля</div>
                <div class='value'>{booking.HotelAddress}</div>
            </div>
            <div class='info-item'>
                <div class='label'>Телефон</div>
                <div class='value'>{booking.HotelPhone}</div>
            </div>
            <div class='info-item'>
                <div class='label'>Заезд</div>
                <div class='value'>{checkInDate} (после 14:00)</div>
            </div>
            <div class='info-item'>
                <div class='label'>Выезд</div>
                <div class='value'>{checkOutDate} (до 12:00)</div>
            </div>
            <div class='info-item'>
                <div class='label'>Гостей</div>
                <div class='value'>{booking.Guests}</div>
            </div>
            <div class='info-item'>
                <div class='label'>Комнат</div>
                <div class='value'>{booking.Rooms}</div>
            </div>
            <div class='info-item'>
                <div class='label'>Имя гостя</div>
                <div class='value'>{booking.ContactName}</div>
            </div>
            <div class='info-item'>
                <div class='label'>Телефон для связи</div>
                <div class='value'>{booking.ContactPhone}</div>
            </div>
        </div>

        {(!string.IsNullOrEmpty(booking.SpecialRequests) ? $@"
        <div style='background: #fff3cd; padding: 15px; border-radius: 8px; margin: 20px 0;'>
            <p><strong>Особые пожелания:</strong> {booking.SpecialRequests}</p>
        </div>" : "")}

        <div class='price'>
            <p>Цена за ночь: {booking.PricePerNight:N0} ₽</p>
            <p>Всего ночей: {booking.Nights}</p>
            <p class='total'>Итого: {booking.TotalPrice:N0} ₽</p>
        </div>

        <div class='payment-info'>
            <p><strong>💡 Оплата бронирования</strong></p>
            <p>Для оплаты бронирования перейдите в раздел <strong>«Мои заказы»</strong> в вашем личном кабинете.</p>
        </div>

        <div style='background: #e2e8f0; padding: 15px; border-radius: 8px; margin-top: 20px;'>
            <p style='margin: 0; color: #334155;'><strong>Важно!</strong> При заселении необходимо предъявить документ, удостоверяющий личность, и данный ваучер (можно на экране телефона).</p>
        </div>

        <div class='footer'>
            <p>Спасибо, что пользуетесь сервисом <strong>Вместе В Путь</strong></p>
            <p>© {DateTime.Now.Year} Все права защищены</p>
        </div>
    </div>
</body>
</html>";

            await _emailService.SendAsync(booking.ContactEmail, subject, body);
        }

        private async Task SendCancellationEmail(HotelBooking booking)
        {
            var subject = $"Отмена бронирования {booking.HotelName} - Вместе В Путь";

            var body = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='UTF-8'>
                <style>
                    body {{ font-family: 'Arial', sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; color: #333; }}
                    .cancellation {{ border: 2px solid #dc3545; border-radius: 12px; padding: 20px; background: #f8fafc; }}
                    .header {{ background: #dc3545; color: white; padding: 20px; border-radius: 12px 12px 0 0; margin: -20px -20px 20px -20px; }}
                </style>
            </head>
            <body>
                <div class='cancellation'>
                    <div class='header'>
                        <h2>Бронирование отменено</h2>
                        <p>Номер брони: {booking.BookingNumber}</p>
                    </div>

                    <p><strong>Отель:</strong> {booking.HotelName}</p>
                    <p><strong>Даты:</strong> {booking.CheckInDate:dd.MM.yyyy} - {booking.CheckOutDate:dd.MM.yyyy}</p>
                    <p><strong>Причина отмены:</strong> {booking.CancellationReason ?? "Не указана"}</p>

                    <p>Средства будут возвращены на карту в течение 3-7 рабочих дней.</p>
                </div>
            </body>
            </html>";

            await _emailService.SendAsync(booking.ContactEmail, subject, body);
        }

        private string GetStatusText(BookingStatus status)
        {
            return status switch
            {
                BookingStatus.Pending => "Ожидает подтверждения",
                BookingStatus.Confirmed => "Подтверждено",
                BookingStatus.Cancelled => "Отменено",
                BookingStatus.Completed => "Завершено",
                BookingStatus.NoShow => "Не заселились",
                _ => "Неизвестно"
            };
        }
    }

    public class CancelBookingRequest
    {
        public string BookingId { get; set; }
        public string Reason { get; set; }
    }
}