// Controllers/FlightBookingController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWise.Models;
using TripWise.Models.ViewModels;
using TripWise.Services;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace TripWise.Controllers
{
    public class FlightBookingController : Controller
    {
        private readonly TripWiseContext _context;
        private readonly EmailService _emailService;
        private readonly ILogger<FlightBookingController> _logger;
        private readonly IMemoryCache _cache;

        public FlightBookingController(
            TripWiseContext context,
            EmailService emailService,
            ILogger<FlightBookingController> logger,
            IMemoryCache memoryCache)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
            _cache = memoryCache;
        }

        // GET: /FlightBooking/Book
        [HttpGet]
        public IActionResult Book(
            string flightId,
            string airline,
            string airlineCode,
            string airlineLogo,
            string flightNumber,
            string departureCity,
            string arrivalCity,
            string departureAirport,
            string arrivalAirport,
            DateTime departureDateTime,
            DateTime arrivalDateTime,
            decimal price,
            int duration,
            int transfers,
            string aircraft,
            string baggage,
            string handLuggage,
            string meal,
            string flightClass,
            bool isRoundTrip,
            int passengers,
            string returnFlightId = null,
            string returnAirline = null,
            string returnFlightNumber = null,
            DateTime? returnDepartureDateTime = null,
            DateTime? returnArrivalDateTime = null,
            int? returnDuration = null,
            int? returnTransfers = null)
        {
            _logger.LogInformation("=== GET BOOK METHOD CALLED ===");
            _logger.LogInformation($"isRoundTrip: {isRoundTrip}");
            _logger.LogInformation($"returnFlightNumber: {returnFlightNumber}");

            // Проверяем, что обязательные параметры есть
            if (string.IsNullOrEmpty(flightId) || string.IsNullOrEmpty(departureCity) || string.IsNullOrEmpty(arrivalCity))
            {
                _logger.LogError("Обязательные параметры отсутствуют!");
                return RedirectToAction("Index", "Flights");
            }

            // ✅ ПРИНУДИТЕЛЬНО УСТАНАВЛИВАЕМ isRoundTrip = false,
            // чтобы всегда бронировался только один рейс
            bool actualIsRoundTrip = false;

            // Если всё же нужно сохранить возможность бронирования туда-обратно,
            // можно добавить проверку: если есть данные обратного рейса - бронируем оба
            // bool actualIsRoundTrip = isRoundTrip && !string.IsNullOrEmpty(returnFlightNumber);

            _logger.LogInformation($"Фактическое isRoundTrip (после обработки): {actualIsRoundTrip}");

            // Исправляем даты, если они приходят в формате UTC
            if (departureDateTime.Kind == DateTimeKind.Utc)
            {
                departureDateTime = departureDateTime.ToLocalTime();
            }
            if (arrivalDateTime.Kind == DateTimeKind.Utc)
            {
                arrivalDateTime = arrivalDateTime.ToLocalTime();
            }

            // Создаем модель для формы
            var viewModel = new CompleteFlightBookingViewModel
            {
                Flight = new FlightBookingViewModel
                {
                    FlightId = flightId,
                    Airline = airline ?? "Авиакомпания",
                    AirlineCode = airlineCode ?? "",
                    AirlineLogo = airlineLogo ?? "",
                    FlightNumber = flightNumber ?? "------",
                    DepartureCity = departureCity,
                    ArrivalCity = arrivalCity,
                    DepartureAirport = departureAirport ?? "",
                    ArrivalAirport = arrivalAirport ?? "",
                    DepartureDateTime = departureDateTime,
                    ArrivalDateTime = arrivalDateTime,
                    Price = price,
                    Duration = duration,
                    Transfers = transfers,
                    Aircraft = aircraft ?? "Airbus A320",
                    Baggage = baggage ?? "1x23кг",
                    HandLuggage = handLuggage ?? "1x10кг",
                    Meal = meal ?? "Включено",
                    FlightClass = flightClass ?? "economy",
                    IsRoundTrip = actualIsRoundTrip,  // ✅ Используем исправленное значение
                    Passengers = passengers > 0 ? passengers : 1,

                    // ❌ ОБНУЛЯЕМ данные обратного рейса, чтобы они не отображались
                    ReturnFlightId = null,
                    ReturnAirline = null,
                    ReturnFlightNumber = null,
                    ReturnDepartureDateTime = null,
                    ReturnArrivalDateTime = null,
                    ReturnDuration = null,
                    ReturnTransfers = null
                },
                Passengers = new List<FlightPassengerViewModel>(),
                Contact = new FlightContactViewModel()
            };

            // Добавляем пассажиров по умолчанию
            for (int i = 0; i < viewModel.Flight.Passengers; i++)
            {
                viewModel.Passengers.Add(new FlightPassengerViewModel
                {
                    DateOfBirth = new DateTime(1990, 1, 1),
                    Gender = "M",
                    DocumentType = "passport",
                    Nationality = "Россия"
                });
            }

            // Если пользователь авторизован, подставляем его данные
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId.HasValue)
            {
                var user = _context.Users.Find(userId.Value);
                if (user != null)
                {
                    viewModel.Contact.Email = user.Email;
                    viewModel.Contact.Name = $"{user.FirstName} {user.LastName}".Trim();

                    if (viewModel.Passengers.Count > 0)
                    {
                        viewModel.Passengers[0].FirstName = user.FirstName ?? "";
                        viewModel.Passengers[0].LastName = user.LastName ?? "";
                        viewModel.Passengers[0].MiddleName = user.MiddleName;
                    }
                }
            }

            _logger.LogInformation($"Модель создана: Flight={viewModel.Flight.FlightNumber}, IsRoundTrip={viewModel.Flight.IsRoundTrip}");

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> ProcessBooking([FromBody] CompleteFlightBookingViewModel model)
        {
            try
            {
                _logger.LogInformation("=== НАЧАЛО БРОНИРОВАНИЯ АВИАБИЛЕТА ===");

                // ========== ЛОГИРУЕМ ПОЛУЧЕННЫЕ ДАННЫЕ ==========
                _logger.LogInformation($"model == null: {model == null}");
                if (model != null)
                {
                    _logger.LogInformation($"model.Flight == null: {model.Flight == null}");
                    if (model.Flight != null)
                    {
                        _logger.LogInformation($"Flight.FlightId: {model.Flight.FlightId}");
                        _logger.LogInformation($"Flight.Airline: {model.Flight.Airline}");
                        _logger.LogInformation($"Flight.FlightNumber: {model.Flight.FlightNumber}");
                        _logger.LogInformation($"Flight.DepartureCity: {model.Flight.DepartureCity}");
                        _logger.LogInformation($"Flight.ArrivalCity: {model.Flight.ArrivalCity}");
                        _logger.LogInformation($"Flight.DepartureDateTime: {model.Flight.DepartureDateTime}");
                        _logger.LogInformation($"Flight.ArrivalDateTime: {model.Flight.ArrivalDateTime}");
                        _logger.LogInformation($"Flight.Price: {model.Flight.Price}");
                        _logger.LogInformation($"Flight.IsRoundTrip: {model.Flight.IsRoundTrip}");
                        _logger.LogInformation($"Flight.Passengers: {model.Flight.Passengers}");
                        _logger.LogInformation($"Flight.ReturnFlightId: {model.Flight.ReturnFlightId}");
                        _logger.LogInformation($"Flight.ReturnFlightNumber: {model.Flight.ReturnFlightNumber}");
                        _logger.LogInformation($"Flight.ReturnDepartureDateTime: {model.Flight.ReturnDepartureDateTime}");
                        _logger.LogInformation($"Flight.ReturnArrivalDateTime: {model.Flight.ReturnArrivalDateTime}");
                        _logger.LogInformation($"Flight.ReturnDuration: {model.Flight.ReturnDuration}");
                        _logger.LogInformation($"Flight.ReturnTransfers: {model.Flight.ReturnTransfers}");
                    }
                    _logger.LogInformation($"model.Passengers: {model.Passengers?.Count ?? 0}");
                    if (model.Passengers != null && model.Passengers.Any())
                    {
                        foreach (var p in model.Passengers)
                        {
                            _logger.LogInformation($"  Пассажир: {p.LastName} {p.FirstName} {p.MiddleName}");
                        }
                    }
                    _logger.LogInformation($"model.Contact == null: {model.Contact == null}");
                    if (model.Contact != null)
                    {
                        _logger.LogInformation($"Contact.Name: {model.Contact.Name}");
                        _logger.LogInformation($"Contact.Email: {model.Contact.Email}");
                        _logger.LogInformation($"Contact.Phone: {model.Contact.Phone}");
                        _logger.LogInformation($"Contact.AgreeToTerms: {model.Contact.AgreeToTerms}");
                    }
                }

                // ========== ПРОВЕРКА АВТОРИЗАЦИИ ==========
                var userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                {
                    _logger.LogWarning("Попытка бронирования без авторизации");
                    return Json(new
                    {
                        success = false,
                        message = "Для бронирования необходимо войти в систему",
                        redirectUrl = Url.Action("Login", "Account", new { returnUrl = Request.Path })
                    });
                }

                // ========== ПРОВЕРКА МОДЕЛИ ==========
                if (model == null)
                {
                    _logger.LogError("Модель равна null");
                    return Json(new { success = false, message = "Не получены данные бронирования" });
                }

                if (model.Flight == null)
                {
                    _logger.LogError("model.Flight равен null");
                    return Json(new { success = false, message = "Не получены данные рейса" });
                }

                if (model.Passengers == null)
                {
                    model.Passengers = new List<FlightPassengerViewModel>();
                }

                if (model.Contact == null)
                {
                    model.Contact = new FlightContactViewModel();
                }

                // ========== ДОПОЛНИТЕЛЬНАЯ ПРОВЕРКА ОБЯЗАТЕЛЬНЫХ ПОЛЕЙ ==========
                if (string.IsNullOrEmpty(model.Flight.FlightId))
                {
                    _logger.LogError("FlightId отсутствует");
                    return Json(new { success = false, message = "Идентификатор рейса отсутствует" });
                }

                if (model.Flight.Passengers <= 0)
                {
                    model.Flight.Passengers = model.Passengers.Count > 0 ? model.Passengers.Count : 1;
                }

                if (model.Passengers.Count != model.Flight.Passengers)
                {
                    _logger.LogWarning($"Количество пассажиров не совпадает: Passengers={model.Passengers.Count}, Flight.Passengers={model.Flight.Passengers}");
                }

                // Исправляем даты, если они приходят в формате UTC
                if (model.Flight.DepartureDateTime.Kind == DateTimeKind.Utc)
                {
                    model.Flight.DepartureDateTime = model.Flight.DepartureDateTime.ToLocalTime();
                }
                if (model.Flight.ArrivalDateTime.Kind == DateTimeKind.Utc)
                {
                    model.Flight.ArrivalDateTime = model.Flight.ArrivalDateTime.ToLocalTime();
                }
                if (model.Flight.ReturnDepartureDateTime.HasValue && model.Flight.ReturnDepartureDateTime.Value.Kind == DateTimeKind.Utc)
                {
                    model.Flight.ReturnDepartureDateTime = model.Flight.ReturnDepartureDateTime.Value.ToLocalTime();
                }
                if (model.Flight.ReturnArrivalDateTime.HasValue && model.Flight.ReturnArrivalDateTime.Value.Kind == DateTimeKind.Utc)
                {
                    model.Flight.ReturnArrivalDateTime = model.Flight.ReturnArrivalDateTime.Value.ToLocalTime();
                }

                // ========== ВАЛИДАЦИЯ ДАТ РЕЙСА ==========
                if (model.Flight.DepartureDateTime < DateTime.Now)
                {
                    _logger.LogWarning($"Дата вылета в прошлом: {model.Flight.DepartureDateTime}");
                    // Не блокируем, просто логируем
                }

                // ========== ПРОВЕРКА СОГЛАСИЯ С УСЛОВИЯМИ ==========
                if (!model.Contact.AgreeToTerms)
                {
                    return Json(new { success = false, message = "Необходимо согласиться с условиями перевозки" });
                }

                // ========== СОЗДАНИЕ БРОНИРОВАНИЯ ==========
                var passengersJson = JsonSerializer.Serialize(model.Passengers);
                var bookingReference = GeneratePnrCode();
                var ticketNumber = GenerateTicketNumber();
                var seatNumbers = GenerateSeatNumbers(model.Passengers.Count);

                // Вычисляем общую стоимость
                decimal totalPrice = model.Flight.Price * model.Passengers.Count;
                if (model.Flight.IsRoundTrip)
                {
                    totalPrice *= 2;
                }

                // Создаем бронирование
                var booking = new FlightBooking
                {
                    Id = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserId = userId.Value,
                    BookingNumber = "FLT" + DateTime.Now.ToString("yyyyMMddHHmmss") + new Random().Next(100, 999),

                    // Данные рейса туда
                    FlightId = model.Flight.FlightId ?? "",
                    Airline = model.Flight.Airline ?? "",
                    AirlineCode = model.Flight.AirlineCode ?? "",
                    AirlineLogo = model.Flight.AirlineLogo ?? "",
                    FlightNumber = model.Flight.FlightNumber ?? "",
                    DepartureCity = model.Flight.DepartureCity ?? "",
                    ArrivalCity = model.Flight.ArrivalCity ?? "",
                    DepartureAirport = model.Flight.DepartureAirport ?? "",
                    ArrivalAirport = model.Flight.ArrivalAirport ?? "",
                    DepartureDateTime = model.Flight.DepartureDateTime,
                    ArrivalDateTime = model.Flight.ArrivalDateTime,
                    Duration = model.Flight.Duration,
                    Transfers = model.Flight.Transfers,
                    Aircraft = model.Flight.Aircraft ?? "",

                    // Данные обратного рейса (если есть)
                    ReturnFlightId = model.Flight.ReturnFlightId,
                    ReturnAirline = model.Flight.ReturnAirline,
                    ReturnFlightNumber = model.Flight.ReturnFlightNumber,
                    ReturnDepartureDateTime = model.Flight.ReturnDepartureDateTime,
                    ReturnArrivalDateTime = model.Flight.ReturnArrivalDateTime,
                    ReturnDuration = model.Flight.ReturnDuration,
                    ReturnTransfers = model.Flight.ReturnTransfers,

                    // Цена и пассажиры
                    Price = model.Flight.Price,
                    TotalPrice = totalPrice,
                    Passengers = model.Passengers.Count,
                    FlightClass = model.Flight.FlightClass ?? "Economy",
                    IsRoundTrip = model.Flight.IsRoundTrip,
                    Currency = "RUB",

                    // Багаж и услуги
                    Baggage = model.Flight.Baggage ?? "1x23кг",
                    HandLuggage = model.Flight.HandLuggage ?? "1x10кг",
                    Meal = model.Flight.Meal ?? "Включено",

                    // Контактные данные
                    ContactName = model.Contact.Name ?? "",
                    ContactEmail = model.Contact.Email ?? "",
                    ContactPhone = model.Contact.Phone ?? "",

                    // Данные пассажиров
                    PassengersJson = passengersJson,
                    SeatNumbers = seatNumbers,  // ← ДОБАВЬТЕ ЭТУ СТРОКУ

                    // Статусы
                    Status = BookingStatus.Confirmed,
                    PaymentStatus = PaymentStatus.Paid,
                    PaymentMethod = "Банковская карта",
                    TransactionId = "TXN" + DateTime.Now.Ticks.ToString().Substring(0, 12),
                    CreatedAt = DateTime.UtcNow,
                    ConfirmedAt = DateTime.UtcNow,
                    CancelledAt = null,

                    // Бронирование и билет
                    BookingReference = bookingReference,
                    TicketNumber = ticketNumber,
                    CancellationReason = "",
                    Notes = ""
                };

                _context.FlightBookings.Add(booking);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Бронирование сохранено с ID: {booking.Id}, номер билета: {ticketNumber}");

                // ========== УДАЛЕНИЕ ИЗ ИЗБРАННОГО ==========
                try
                {
                    if (!string.IsNullOrEmpty(model.Flight.FlightId))
                    {
                        var favoriteFlight = await _context.FavoriteFlights
                            .FirstOrDefaultAsync(f => f.FlightId == model.Flight.FlightId && f.UserId == userId.Value);

                        if (favoriteFlight != null)
                        {
                            _context.FavoriteFlights.Remove(favoriteFlight);
                            await _context.SaveChangesAsync();
                            _logger.LogInformation($"Рейс {model.Flight.FlightId} удален из избранного");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Не удалось удалить рейс из избранного");
                }

                // ========== ОТПРАВКА EMAIL (В ФОНОВОМ РЕЖИМЕ) ==========
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await SendBookingConfirmationEmail(booking, model.Passengers);
                        _logger.LogInformation($"Email подтверждения отправлен на {booking.ContactEmail}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Ошибка отправки email на {booking.ContactEmail}");
                    }
                });

                // ========== ВОЗВРАТ УСПЕШНОГО ОТВЕТА ==========
                return Json(new
                {
                    success = true,
                    message = "Бронирование успешно оформлено",
                    redirectUrl = Url.Action("Confirmation", "FlightBooking", new { bookingId = booking.Id })
                });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Ошибка базы данных при бронировании");
                return Json(new { success = false, message = "Ошибка при сохранении данных. Пожалуйста, попробуйте позже." });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Ошибка сериализации JSON");
                return Json(new { success = false, message = "Ошибка обработки данных пассажиров" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "НЕИЗВЕСТНАЯ ОШИБКА при бронировании авиабилета");
                return Json(new { success = false, message = "Произошла ошибка при бронировании: " + ex.Message });
            }
        }

        // GET: /FlightBooking/Confirmation
        [HttpGet]
        public async Task<IActionResult> Confirmation(string bookingId)
        {
            if (string.IsNullOrEmpty(bookingId))
            {
                return RedirectToAction("Index", "Flights");
            }

            var cacheKey = "FlightBooking_" + bookingId;
            FlightBooking booking = null;

            if (!_cache.TryGetValue(cacheKey, out booking))
            {
                booking = await _context.FlightBookings.FirstOrDefaultAsync(b => b.Id == bookingId);
                if (booking != null)
                {
                    _cache.Set(cacheKey, booking, TimeSpan.FromMinutes(30));
                }
            }

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Бронирование не найдено";
                return RedirectToAction("Index", "Flights");
            }

            // Десериализуем пассажиров
            var passengers = JsonSerializer.Deserialize<List<FlightPassengerViewModel>>(booking.PassengersJson) ?? new List<FlightPassengerViewModel>();

            // Создаем модель для представления
            var viewModel = new FlightBookingViewModel
            {
                FlightId = booking.FlightId,
                Airline = booking.Airline,
                AirlineCode = booking.AirlineCode,
                AirlineLogo = booking.AirlineLogo,
                FlightNumber = booking.FlightNumber,
                DepartureCity = booking.DepartureCity,
                ArrivalCity = booking.ArrivalCity,
                DepartureAirport = booking.DepartureAirport,
                ArrivalAirport = booking.ArrivalAirport,
                DepartureDateTime = booking.DepartureDateTime,
                ArrivalDateTime = booking.ArrivalDateTime,
                Price = booking.Price,
                Duration = booking.Duration,
                Transfers = booking.Transfers,
                Aircraft = booking.Aircraft,
                Baggage = booking.Baggage,
                HandLuggage = booking.HandLuggage,
                Meal = booking.Meal,
                ReturnFlightId = booking.ReturnFlightId,
                ReturnAirline = booking.ReturnAirline,
                ReturnFlightNumber = booking.ReturnFlightNumber,
                ReturnDepartureDateTime = booking.ReturnDepartureDateTime,
                ReturnArrivalDateTime = booking.ReturnArrivalDateTime,
                ReturnDuration = booking.ReturnDuration,
                ReturnTransfers = booking.ReturnTransfers,
                Passengers = booking.Passengers,
                FlightClass = booking.FlightClass,
                IsRoundTrip = booking.IsRoundTrip
            };

            // Передаем дополнительные данные через ViewBag
            ViewBag.BookingId = booking.Id;
            ViewBag.BookingNumber = booking.BookingNumber;
            ViewBag.BookingReference = booking.BookingReference;
            ViewBag.TicketNumber = booking.TicketNumber;
            ViewBag.ContactName = booking.ContactName;
            ViewBag.ContactEmail = booking.ContactEmail;
            ViewBag.ContactPhone = booking.ContactPhone;
            ViewBag.SeatNumbers = booking.SeatNumbers;
            ViewBag.TotalPrice = booking.Price * booking.Passengers * (booking.IsRoundTrip ? 2 : 1);
            ViewBag.PassengersData = passengers;

            return View(viewModel);
        }

        // GET: /FlightBooking/MyBookings
        [HttpGet]
        public async Task<IActionResult> MyBookings()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var bookings = await _context.FlightBookings
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return View(bookings);
        }

        // GET: /FlightBooking/Ticket/{bookingId}
        [HttpGet]
        public async Task<IActionResult> Ticket(string bookingId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            var booking = await _context.FlightBookings
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null)
                return NotFound();

            if (booking.UserId != userId && !User.IsInRole("Admin"))
                return Forbid();

            var passengers = JsonSerializer.Deserialize<List<FlightPassengerViewModel>>(booking.PassengersJson);

            var viewModel = new FlightBookingConfirmationViewModel
            {
                BookingId = booking.Id,
                BookingNumber = booking.BookingNumber,
                Airline = booking.Airline,
                FlightNumber = booking.FlightNumber,
                DepartureCity = booking.DepartureCity,
                ArrivalCity = booking.ArrivalCity,
                DepartureAirport = booking.DepartureAirport,
                ArrivalAirport = booking.ArrivalAirport,
                DepartureDateTime = booking.DepartureDateTime,
                ArrivalDateTime = booking.ArrivalDateTime,
                ReturnFlightNumber = booking.ReturnFlightNumber,
                ReturnDepartureDateTime = booking.ReturnDepartureDateTime,
                ReturnArrivalDateTime = booking.ReturnArrivalDateTime,
                Passengers = booking.Passengers,
                FlightClass = booking.FlightClass,
                Price = booking.Price,
                TotalPrice = booking.Price * booking.Passengers * (booking.IsRoundTrip ? 2 : 1),
                Currency = booking.Currency,
                ContactName = booking.ContactName,
                ContactEmail = booking.ContactEmail,
                ContactPhone = booking.ContactPhone,
                SeatNumbers = booking.SeatNumbers,
                BookingReference = booking.BookingReference,
                TicketNumber = booking.TicketNumber,
                IsRoundTrip = booking.IsRoundTrip,
                CreatedAt = booking.CreatedAt,
                Status = GetStatusText(booking.Status)
            };

            return View(viewModel);
        }

        // POST: /FlightBooking/Cancel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel([FromBody] CancelBookingRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");

                var booking = await _context.FlightBookings
                    .FirstOrDefaultAsync(b => b.Id == request.BookingId);

                if (booking == null)
                    return Json(new { success = false, message = "Бронирование не найдено" });

                if (booking.UserId != userId && !User.IsInRole("Admin"))
                    return Json(new { success = false, message = "Нет прав для отмены" });

                // Проверяем, можно ли отменить (за 24 часа до вылета)
                if (booking.DepartureDateTime <= DateTime.UtcNow.AddHours(24))
                {
                    return Json(new { success = false, message = "Отмена невозможна менее чем за 24 часа до вылета" });
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

        // GET: /FlightBooking/DownloadTicket/{bookingId}
        [HttpGet]
        public async Task<IActionResult> DownloadTicket(string bookingId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            var booking = await _context.FlightBookings
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null)
                return NotFound();

            if (booking.UserId != userId && !User.IsInRole("Admin"))
                return Forbid();

            // Десериализуем пассажиров
            var passengers = JsonSerializer.Deserialize<List<FlightPassengerViewModel>>(booking.PassengersJson);

            // Генерируем HTML билета
            var html = GenerateTicketHtml(booking, passengers);

            // Возвращаем как файл с правильным Content-Type
            var bytes = System.Text.Encoding.UTF8.GetBytes(html);
            return File(bytes, "text/html", $"ticket_{booking.TicketNumber}.html");
        }

        // НОВЫЙ МЕТОД для генерации HTML билета (не async)
        private string GenerateTicketHtml(FlightBooking booking, List<FlightPassengerViewModel> passengers)
        {
            var seatsList = new List<string>();
            if (!string.IsNullOrEmpty(booking.SeatNumbers))
            {
                seatsList = booking.SeatNumbers.Split(new[] { ", " }, StringSplitOptions.None).ToList();
            }

            // Дополняем список мест, если их меньше чем пассажиров
            while (seatsList.Count < passengers.Count)
            {
                seatsList.Add("—");
            }

            var passengersHtml = "";
            for (int i = 0; i < passengers.Count; i++)
            {
                var p = passengers[i];
                var seat = i < seatsList.Count ? seatsList[i] : "—";

                passengersHtml += $@"
    <tr>
        <td data-label='ФИО' style='word-break: break-word; white-space: normal;'>{p.LastName} {p.FirstName} {p.MiddleName}</td>
        <td data-label='Дата рождения'>{p.DateOfBirth:dd.MM.yyyy}</td>
        <td data-label='Документ'>{GetDocumentTypeName(p.DocumentType)} {p.DocumentNumber}</td>
        <td data-label='Место'>{seat}</td>
    </tr>";
            }

            var body = $@"
    <!DOCTYPE html>
    <html>
    <head>
        <meta charset='UTF-8'>
        <title>Электронный билет - {booking.FlightNumber}</title>
        <style>
    * {{ margin: 0; padding: 0; box-sizing: border-box; }}
    body {{ font-family: 'Segoe UI', 'Arial', sans-serif; background: #e2e8f0; padding: 40px 20px; }}
    .ticket {{ max-width: 900px; margin: 0 auto; background: white; border-radius: 20px; box-shadow: 0 20px 40px rgba(0,0,0,0.1); overflow: hidden; }}
    .ticket-header {{ background: linear-gradient(135deg, #0379D9, #40B624); color: white; padding: 30px; text-align: center; }}
    .ticket-header h1 {{ font-size: 28px; margin-bottom: 10px; }}
    .ticket-header p {{ opacity: 0.9; font-size: 14px; }}
    .ticket-body {{ padding: 30px; }}
    .airline {{ text-align: center; margin-bottom: 30px; }}
    .airline h2 {{ color: #0379D9; font-size: 24px; margin-bottom: 5px; }}
    .route {{ display: flex; justify-content: space-between; align-items: center; margin: 30px 0; flex-wrap: wrap; gap: 15px; }}
    .route-city {{ text-align: center; flex: 1; min-width: 0; }}
    .route-city .time {{ font-size: 28px; font-weight: bold; color: #0379D9; }}
    .route-city .city {{ font-size: 18px; font-weight: 600; margin: 5px 0; word-break: break-word; }}
    .route-city .airport {{ color: #64748b; font-size: 12px; }}
    .route-city .date {{ color: #64748b; font-size: 12px; }}
    .route-icon {{ color: #94a3b8; font-size: 24px; }}
    .info-grid {{ display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin: 30px 0; }}
    .info-item {{ border-bottom: 1px solid #e2e8f0; padding: 10px 0; }}
    .info-item .label {{ color: #64748b; font-size: 12px; }}
    .info-item .value {{ font-size: 16px; font-weight: 600; color: #334155; word-break: break-word; overflow-wrap: break-word; white-space: normal; }}
    table {{ width: 100%; border-collapse: collapse; margin: 20px 0; table-layout: fixed; }}
    th {{ background: #f1f5f9; padding: 12px; text-align: left; font-weight: 600; }}
    td {{ padding: 12px; border-bottom: 1px solid #e2e8f0; word-break: break-word; overflow-wrap: break-word; white-space: normal; }}
    td:first-child, th:first-child {{ word-break: break-word; white-space: normal; max-width: 300px; }}
    .price-block {{ background: #e8f4fe; padding: 20px; border-radius: 12px; text-align: center; margin: 20px 0; }}
    .price-block .total {{ font-size: 28px; font-weight: bold; color: #0379D9; }}
    .footer {{ background: #f8fafc; padding: 20px; text-align: center; font-size: 12px; color: #64748b; }}
    .badge {{ display: inline-block; background: #40B624; color: white; padding: 4px 12px; border-radius: 20px; font-size: 12px; }}
    /* Мобильная адаптация */
    @media (max-width: 600px) {{
        body {{ padding: 10px; }}
        .ticket-body {{ padding: 20px; }}
        .route {{ flex-direction: column; gap: 20px; }}
        .info-grid {{ grid-template-columns: 1fr; }}
        .ticket-header h1 {{ font-size: 22px; }}
        .route-city .time {{ font-size: 22px; }}
        table, thead, tbody, th, td, tr {{ display: block; }}
        th {{ display: none; }}
        td {{ display: flex; justify-content: space-between; align-items: center; gap: 10px; padding: 10px; border-bottom: 1px solid #e2e8f0; }}
        td:before {{ content: attr(data-label); font-weight: bold; width: 40%; color: #64748b; }}
        td:first-child {{ max-width: 100%; }}
    }}
</style>
    </head>
    <body>
        <div class='ticket'>
            <div class='ticket-header'>
                <h1>Электронный билет</h1>
                <p>Номер бронирования: {booking.BookingReference} | Номер билета: {booking.TicketNumber}</p>
            </div>
            <div class='ticket-body'>
                <div class='airline'>
                    <h2>{booking.Airline}</h2>
                    <p>Рейс {booking.FlightNumber}</p>
                </div>

                <div class='route'>
                    <div class='route-city'>
                        <div class='time'>{booking.DepartureDateTime:HH:mm}</div>
                        <div class='city'>{booking.DepartureCity}</div>
                        <div class='airport'>{booking.DepartureAirport}</div>
                        <div class='date'>{booking.DepartureDateTime:dd.MM.yyyy}</div>
                    </div>
                    <div class='route-icon'>✈</div>
                    <div class='route-city'>
                        <div class='time'>{booking.ArrivalDateTime:HH:mm}</div>
                        <div class='city'>{booking.ArrivalCity}</div>
                        <div class='airport'>{booking.ArrivalAirport}</div>
                        <div class='date'>{booking.ArrivalDateTime:dd.MM.yyyy}</div>
                    </div>
                </div>";

            if (booking.IsRoundTrip && !string.IsNullOrEmpty(booking.ReturnFlightNumber))
            {
                body += $@"
                <div style='margin: 30px 0; border-top: 2px dashed #e2e8f0; padding-top: 30px;'>
                    <div class='airline'><h3 style='color: #40B624;'>Обратный рейс {booking.ReturnFlightNumber}</h3></div>
                    <div class='route'>
                        <div class='route-city'>
                            <div class='time'>{booking.ReturnDepartureDateTime:HH:mm}</div>
                            <div class='city'>{booking.ArrivalCity}</div>
                            <div class='airport'>{booking.ArrivalAirport}</div>
                            <div class='date'>{booking.ReturnDepartureDateTime:dd.MM.yyyy}</div>
                        </div>
                        <div class='route-icon'>✈</div>
                        <div class='route-city'>
                            <div class='time'>{booking.ReturnArrivalDateTime:HH:mm}</div>
                            <div class='city'>{booking.DepartureCity}</div>
                            <div class='airport'>{booking.DepartureAirport}</div>
                            <div class='date'>{booking.ReturnArrivalDateTime:dd.MM.yyyy}</div>
                        </div>
                    </div>
                </div>";
            }

            body += $@"
                <h3>Пассажиры</h3>
                <table>
    <thead>
        <tr>
            <th>ФИО</th>
            <th>Дата рождения</th>
            <th>Документ</th>
            <th>Место</th>
        </tr>
    </thead>
    <tbody>
        {passengersHtml}
    </tbody>
</table>

                <div class='info-grid'>
                    <div class='info-item'><div class='label'>Класс</div><div class='value'>{GetClassName(booking.FlightClass)}</div></div>
                    <div class='info-item'><div class='label'>Багаж</div><div class='value'>{booking.Baggage}</div></div>
                    <div class='info-item'><div class='label'>Ручная кладь</div><div class='value'>{booking.HandLuggage}</div></div>
                    <div class='info-item'><div class='label'>Питание</div><div class='value'>{booking.Meal}</div></div>
                    <div class='info-item'><div class='label'>Места</div><div class='value'>{booking.SeatNumbers}</div></div>
                    <div class='info-item'><div class='label'>Контакт</div><div class='value'>{booking.ContactName}, {booking.ContactPhone}</div></div>
                </div>

                <div class='price-block'>
                    <p>Цена за билет: {booking.Price:N0} {booking.Currency}</p>
                    <p>Количество пассажиров: {booking.Passengers}</p>
                    <div class='total'>Итого: {(booking.Price * booking.Passengers * (booking.IsRoundTrip ? 2 : 1)):N0} {booking.Currency}</div>
                </div>

                <div style='background: #fef3c7; padding: 15px; border-radius: 12px; margin-top: 20px;'>
                    <p style='margin: 0;'><strong>⚠️ Важно!</strong> Для посадки необходим документ, указанный при оформлении. Регистрация открывается за 24 часа.</p>
                </div>
            </div>
            <div class='footer'>
                <p>Спасибо, что путешествуете с <strong>Вместе В Путь</strong></p>
                <p>© {DateTime.Now.Year} Все права защищены</p>
            </div>
        </div>
    </body>
    </html>";

            return body;
        }

        // Вспомогательные методы
        private string GeneratePnrCode()
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 6)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
        [HttpPost("debug")]
        public async Task<IActionResult> DebugBooking([FromBody] object rawData)
        {
            _logger.LogInformation("=== DEBUG BOOKING ===");
            _logger.LogInformation($"Raw data: {JsonSerializer.Serialize(rawData)}");

            // Попробуем десериализовать вручную
            try
            {
                var json = JsonSerializer.Serialize(rawData);
                var model = JsonSerializer.Deserialize<CompleteFlightBookingViewModel>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                _logger.LogInformation($"Deserialized successfully: Flight={model?.Flight?.FlightNumber}");
                return Json(new { success = true, flightNumber = model?.Flight?.FlightNumber });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deserialization error");
                return Json(new { success = false, error = ex.Message });
            }
        }
        private string GenerateTicketNumber()
        {
            var random = new Random();
            return $"TKT{DateTime.Now:yyyyMMdd}{random.Next(1000, 9999)}";
        }

        private string GenerateSeatNumbers(int count)
        {
            if (count <= 0) return "";

            var seats = new List<string>();
            var random = new Random();
            var rows = new[] { "A", "B", "C", "D", "E", "F" };

            for (int i = 0; i < count; i++)
            {
                var row = random.Next(1, 35);
                var seat = rows[random.Next(rows.Length)];
                seats.Add($"{row}{seat}");
            }

            return string.Join(", ", seats);
        }

        private async Task SendBookingConfirmationEmail(FlightBooking booking, List<FlightPassengerViewModel> passengers)
        {
            var subject = $"Ваш билет на рейс {booking.FlightNumber} - Вместе В Путь";

            var departureDate = booking.DepartureDateTime.ToString("dd.MM.yyyy HH:mm");
            var arrivalDate = booking.ArrivalDateTime.ToString("dd.MM.yyyy HH:mm");

            var passengersHtml = "";
            foreach (var p in passengers)
            {
                passengersHtml += $@"
    <tr>
        <td data-label='ФИО' style='word-break: break-word; white-space: normal;'>${p.LastName} ${p.FirstName} ${p.MiddleName}</td>
        <td data-label='Дата рождения'>${p.DateOfBirth:dd.MM.yyyy}</td>
        <td data-label='Документ'>${GetDocumentTypeName(p.DocumentType)} ${p.DocumentNumber}</td>
    </tr>";
            }

            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        body {{ 
            font-family: 'Arial', sans-serif; 
            max-width: 600px; 
            margin: 0 auto; 
            padding: 20px; 
            color: #333; 
        }}
        .ticket {{ 
            border: 2px solid #0379D9; 
            border-radius: 12px; 
            padding: 20px; 
            background: #f8fafc; 
            word-wrap: break-word;
            word-break: break-word;
            overflow-wrap: break-word;
        }}
        .header {{ 
            background: linear-gradient(135deg, #0379D9, #40B624); 
            color: white; 
            padding: 20px; 
            border-radius: 12px 12px 0 0; 
            margin: -20px -20px 20px -20px; 
        }}
        .header h2 {{ 
            margin: 0; 
            font-size: 24px; 
        }}
        .airline {{ 
            font-size: 24px; 
            font-weight: bold; 
            text-align: center; 
            margin: 20px 0; 
            color: #0379D9; 
            word-break: break-word;
        }}
        .flight {{ 
            font-size: 20px; 
            font-weight: bold; 
            text-align: center; 
            color: #334155; 
            margin: 10px 0; 
            word-break: break-word;
        }}
        .route {{ 
            display: flex; 
            justify-content: space-between; 
            align-items: center; 
            margin: 30px 0; 
            flex-wrap: wrap;
        }}
        .city {{ 
            text-align: center; 
            flex: 1;
            min-width: 0;
        }}
        .city-name {{ 
            font-size: 18px; 
            font-weight: bold; 
            word-break: break-word;
            overflow-wrap: break-word;
        }}
        .airport {{ 
            color: #64748b; 
            font-size: 12px;
        }}
        .time {{ 
            font-size: 16px; 
            color: #0379D9; 
            font-weight: bold; 
            margin-top: 5px; 
        }}
        .arrow {{ 
            color: #94a3b8; 
            font-size: 24px; 
            padding: 0 10px;
        }}
        .info {{ 
            display: grid; 
            grid-template-columns: 1fr 1fr; 
            gap: 15px; 
            margin: 20px 0; 
        }}
        .info-item {{ 
            border-bottom: 1px solid #e2e8f0; 
            padding: 10px 0; 
        }}
        .info-item .label {{ 
            color: #64748b; 
            font-size: 12px; 
        }}
        .info-item .value {{ 
            font-size: 16px; 
            font-weight: bold; 
            color: #334155; 
            word-break: break-all;
            overflow-wrap: break-word;
        }}
        table {{ 
            width: 100%; 
            border-collapse: collapse; 
            margin: 20px 0; 
            table-layout: fixed;
        }}
        th {{ 
            background: #f1f5f9; 
            color: #334155; 
            padding: 10px; 
            text-align: left; 
        }}
        td {{ 
            padding: 10px; 
            border-bottom: 1px solid #e2e8f0; 
            word-wrap: break-word;
            word-break: break-word;
            white-space: normal;
        }}
        td:first-child, th:first-child {{
            word-break: break-all;
        }}
        .price {{ 
            background: #e8f4fe; 
            padding: 15px; 
            border-radius: 8px; 
            text-align: center; 
            margin: 20px 0; 
        }}
        .price .total {{ 
            font-size: 24px; 
            font-weight: bold; 
            color: #0379D9; 
        }}
        .payment-info {{ 
            background: #fff3cd; 
            padding: 15px; 
            border-radius: 8px; 
            text-align: center; 
            margin: 20px 0; 
        }}
        .payment-info p {{ margin: 0; }}
        .btn {{
            display: inline-block;
            background: #40B624;
            color: white;
            padding: 10px 20px;
            text-decoration: none;
            border-radius: 8px;
            margin-top: 10px;
        }}
        .qr {{ 
            text-align: center; 
            margin: 30px 0; 
        }}
        .qr-placeholder {{ 
            width: 150px; 
            height: 150px; 
            background: #f1f5f9; 
            border: 2px dashed #0379D9; 
            border-radius: 12px; 
            margin: 0 auto; 
            display: flex; 
            align-items: center; 
            justify-content: center; 
            color: #0379D9; 
        }}
        .footer {{ 
            text-align: center; 
            margin-top: 30px; 
            color: #94a3b8; 
            font-size: 12px; 
        }}
        /* Мобильная адаптация для email */
        @@media (max-width: 600px) {{
            .route {{
                flex-direction: column;
                gap: 15px;
            }}
            .info {{
                grid-template-columns: 1fr;
            }}
            .city {{
                width: 100%;
            }}
            .arrow {{
                transform: rotate(90deg);
            }}
            table, thead, tbody, th, td, tr {{
                display: block;
            }}
            th {{
                display: none;
            }}
            td {{
                display: flex;
                justify-content: space-between;
                align-items: center;
                gap: 10px;
                padding: 10px;
                border-bottom: 1px solid #e2e8f0;
            }}
            td:before {{
                content: attr(data-label);
                font-weight: bold;
                width: 40%;
                color: #64748b;
            }}
        }}
    </style>
</head>
<body>
    <div class='ticket'>
        <div class='header'>
            <h2>Электронный билет</h2>
            <p>Номер бронирования: {booking.BookingReference}</p>
            <p>Номер билета: {booking.TicketNumber}</p>
        </div>

        <div class='airline'>
            {booking.Airline}
        </div>

        <div class='flight'>
            Рейс {booking.FlightNumber}
        </div>

        <div class='route'>
            <div class='city'>
                <div class='city-name'>{booking.DepartureCity}</div>
                <div class='airport'>{booking.DepartureAirport}</div>
                <div class='time'>{booking.DepartureDateTime:HH:mm}</div>
                <div class='date'>{booking.DepartureDateTime:dd.MM.yyyy}</div>
            </div>
            <div class='arrow'>
                <i class='fas fa-plane'></i> ✈
            </div>
            <div class='city'>
                <div class='city-name'>{booking.ArrivalCity}</div>
                <div class='airport'>{booking.ArrivalAirport}</div>
                <div class='time'>{booking.ArrivalDateTime:HH:mm}</div>
                <div class='date'>{booking.ArrivalDateTime:dd.MM.yyyy}</div>
            </div>
        </div>";

            if (booking.IsRoundTrip && booking.ReturnFlightNumber != null)
            {
                body += $@"
        <div style='margin: 30px 0; border-top: 2px dashed #e2e8f0; padding-top: 30px;'>
            <div class='flight'>Обратный рейс {booking.ReturnFlightNumber}</div>
            <div class='route'>
                <div class='city'>
                    <div class='city-name'>{booking.ArrivalCity}</div>
                    <div class='airport'>{booking.ArrivalAirport}</div>
                    <div class='time'>{booking.ReturnDepartureDateTime:HH:mm}</div>
                    <div class='date'>{booking.ReturnDepartureDateTime:dd.MM.yyyy}</div>
                </div>
                <div class='arrow'>✈</div>
                <div class='city'>
                    <div class='city-name'>{booking.DepartureCity}</div>
                    <div class='airport'>{booking.DepartureAirport}</div>
                    <div class='time'>{booking.ReturnArrivalDateTime:HH:mm}</div>
                    <div class='date'>{booking.ReturnArrivalDateTime:dd.MM.yyyy}</div>
                </div>
            </div>
        </div>";
            }

            body += $@"
        <h3>Пассажиры</h3>
        <table>
            <thead>
                <tr>
                    <th>ФИО</th>
                    <th>Дата рождения</th>
                    <th>Документ</th>
                </tr>
            </thead>
            <tbody>
                {passengersHtml}
            </tbody>
        </table>

        <div class='info'>
            <div class='info-item'>
                <div class='label'>Класс</div>
                <div class='value'>{GetClassName(booking.FlightClass)}</div>
            </div>
            <div class='info-item'>
                <div class='label'>Багаж</div>
                <div class='value'>{booking.Baggage}</div>
            </div>
            <div class='info-item'>
                <div class='label'>Ручная кладь</div>
                <div class='value'>{booking.HandLuggage}</div>
            </div>
            <div class='info-item'>
                <div class='label'>Питание</div>
                <div class='value'>{booking.Meal}</div>
            </div>
            <div class='info-item'>
                <div class='label'>Места</div>
                <div class='value'>{booking.SeatNumbers}</div>
            </div>
            <div class='info-item'>
                <div class='label'>Контакт</div>
                <div class='value'>{booking.ContactName}, {booking.ContactPhone}</div>
            </div>
        </div>

        <div class='price'>
            <p>Цена за билет: {booking.Price:N0} {booking.Currency}</p>
            <p>Количество пассажиров: {booking.Passengers}</p>
            <p class='total'>Итого: {(booking.Price * booking.Passengers * (booking.IsRoundTrip ? 2 : 1)):N0} {booking.Currency}</p>
        </div>

        <div class='payment-info'>
            <p><strong>💡 Оплата билета</strong></p>
            <p>Для оплаты билета перейдите в раздел <strong>«Мои заказы»</strong> в вашем личном кабинете.</p>
        </div>

        <div style='background: #e2e8f0; padding: 15px; border-radius: 8px; margin-top: 20px;'>
            <p style='margin: 0; color: #334155;'><strong>Важно!</strong> Для посадки на рейс необходимо предъявить документ, указанный при оформлении, и данный электронный билет (можно на экране телефона).</p>
            <p style='margin: 10px 0 0 0;'><strong>Регистрация на рейс открывается за 24 часа до вылета.</strong></p>
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

        private async Task SendCancellationEmail(FlightBooking booking)
        {
            var subject = $"Отмена бронирования рейса {booking.FlightNumber} - Вместе В Путь";

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
                        <p>Номер бронирования: {booking.BookingReference}</p>
                    </div>

                    <p><strong>Авиакомпания:</strong> {booking.Airline}</p>
                    <p><strong>Рейс:</strong> {booking.FlightNumber}</p>
                    <p><strong>Маршрут:</strong> {booking.DepartureCity} → {booking.ArrivalCity}</p>
                    <p><strong>Дата вылета:</strong> {booking.DepartureDateTime:dd.MM.yyyy HH:mm}</p>
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
                _ => "Неизвестно"
            };
        }

        private string GetClassName(string flightClass)
        {
            return flightClass.ToLower() switch
            {
                "economy" => "Эконом",
                "business" => "Бизнес",
                "first" => "Первый",
                _ => flightClass
            };
        }

        private string GetDocumentTypeName(string type)
        {
            return type switch
            {
                "passport" => "Паспорт РФ",
                "foreign_passport" => "Загранпаспорт",
                "birth_certificate" => "Свидетельство о рождении",
                "military_id" => "Военный билет",
                _ => type
            };
        }
        private FlightBookingConfirmationViewModel CreateConfirmationViewModel(FlightBooking booking)
        {
            // Десериализуем пассажиров из JSON
            List<FlightPassengerViewModel> passengersData = new List<FlightPassengerViewModel>();
            try
            {
                if (!string.IsNullOrEmpty(booking.PassengersJson))
                {
                    passengersData = JsonSerializer.Deserialize<List<FlightPassengerViewModel>>(booking.PassengersJson)
                                    ?? new List<FlightPassengerViewModel>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка десериализации пассажиров для бронирования {BookingId}", booking.Id);
            }

            return new FlightBookingConfirmationViewModel
            {
                BookingId = booking.Id,
                BookingNumber = booking.BookingNumber,
                Airline = booking.Airline,
                FlightNumber = booking.FlightNumber,
                DepartureCity = booking.DepartureCity,
                ArrivalCity = booking.ArrivalCity,
                DepartureAirport = booking.DepartureAirport,
                ArrivalAirport = booking.ArrivalAirport,
                DepartureDateTime = booking.DepartureDateTime,
                ArrivalDateTime = booking.ArrivalDateTime,
                ReturnFlightNumber = booking.ReturnFlightNumber,
                ReturnDepartureDateTime = booking.ReturnDepartureDateTime,
                ReturnArrivalDateTime = booking.ReturnArrivalDateTime,
                Passengers = booking.Passengers,
                FlightClass = booking.FlightClass,
                Price = booking.Price,
                TotalPrice = booking.Price * booking.Passengers * (booking.IsRoundTrip ? 2 : 1),
                Currency = booking.Currency,
                ContactName = booking.ContactName,
                ContactEmail = booking.ContactEmail,
                ContactPhone = booking.ContactPhone,
                SeatNumbers = booking.SeatNumbers,
                BookingReference = booking.BookingReference,
                TicketNumber = booking.TicketNumber,
                IsRoundTrip = booking.IsRoundTrip,
                CreatedAt = booking.CreatedAt,
                Status = GetStatusText(booking.Status),
                PassengersData = passengersData
            };
        }
    }
}