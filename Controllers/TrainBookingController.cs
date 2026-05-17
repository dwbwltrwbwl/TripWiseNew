// Controllers/TrainBookingController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWise.Models;
using TripWise.Models.ViewModels;
using TripWise.Services;
using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;

namespace TripWise.Controllers
{
    public class TrainBookingController : Controller
    {
        private readonly TripWiseContext _context;
        private readonly EmailService _emailService;
        private readonly ILogger<TrainBookingController> _logger;
        private readonly IMemoryCache _cache;

        public TrainBookingController(
            TripWiseContext context,
            EmailService emailService,
            ILogger<TrainBookingController> logger,
            IMemoryCache memoryCache)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
            _cache = memoryCache;
        }

        // GET: /TrainBooking/Book
        [HttpGet]
        public IActionResult Book()
        {
            TrainBookingViewModel model = null;

            // Пытаемся получить модель из TempData
            if (TempData["TrainBookingModel"] != null)
            {
                var json = TempData["TrainBookingModel"].ToString();
                model = System.Text.Json.JsonSerializer.Deserialize<TrainBookingViewModel>(json);
                Console.WriteLine($"Model loaded from TempData: Departure={model?.DepartureDateTime}, Arrival={model?.ArrivalDateTime}");
            }

            // Если модель не найдена в TempData, пробуем получить из Query String
            if (model == null)
            {
                // В методе Book, при создании модели из Query String, добавьте:
                model = new TrainBookingViewModel
                {
                    TrainNumber = Request.Query["trainNumber"].ToString(),
                    ReturnTrainNumber = Request.Query["returnTrainNumber"].ToString(),
                    DepartureStationId = Request.Query["departureStationId"].ToString(),
                    DepartureStationName = Request.Query["departureStationName"].ToString(),
                    ArrivalStationId = Request.Query["arrivalStationId"].ToString(),
                    ArrivalStationName = Request.Query["arrivalStationName"].ToString(),
                    Price = decimal.TryParse(Request.Query["price"].ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var p) ? p : 0,
                    Passengers = int.TryParse(Request.Query["passengers"].ToString(), out var pas) ? pas : 1,
                    CarType = Request.Query["carType"].ToString(),
                    CarClass = Request.Query["carClass"].ToString(),
                    Duration = int.TryParse(Request.Query["duration"].ToString(), out var d) ? d : 0,
                    IsRoundTrip = bool.TryParse(Request.Query["isRoundTrip"].ToString(), out var rt) ? rt : false,
                    // ✅ ДОБАВЬТЕ ЭТИ СТРОКИ ДЛЯ ОБРАТНОГО РЕЙСА
                    ReturnDuration = int.TryParse(Request.Query["returnDuration"].ToString(), out var retDur) ? retDur : 0
                };

                // ВАЖНО: Используем DateTime для сохранения локального времени
                if (DateTime.TryParse(Request.Query["departureDateTime"].ToString(), null, System.Globalization.DateTimeStyles.AssumeLocal, out var depDt))
                    model.DepartureDateTime = depDt;
                if (DateTime.TryParse(Request.Query["arrivalDateTime"].ToString(), null, System.Globalization.DateTimeStyles.AssumeLocal, out var arrDt))
                    model.ArrivalDateTime = arrDt;
                if (DateTime.TryParse(Request.Query["returnDepartureDateTime"].ToString(), null, System.Globalization.DateTimeStyles.AssumeLocal, out var retDepDt))
                    model.ReturnDepartureDateTime = retDepDt;
                if (DateTime.TryParse(Request.Query["returnArrivalDateTime"].ToString(), null, System.Globalization.DateTimeStyles.AssumeLocal, out var retArrDt))
                    model.ReturnArrivalDateTime = retArrDt;

                Console.WriteLine($"Model from Query: Departure={model.DepartureDateTime}, Arrival={model.ArrivalDateTime}");
            }

            if (model == null || string.IsNullOrEmpty(model.TrainNumber))
            {
                return RedirectToAction("Index", "Railway");
            }

            // Создаем ViewModel для формы
            var viewModel = new CompleteBookingViewModel
            {
                TrainInfo = model,
                Passengers = new List<PassengerInfoViewModel>(),
                Contact = new ContactInfoViewModel()
            };

            // Добавляем пассажиров в соответствии с количеством
            for (int i = 0; i < model.Passengers; i++)
            {
                viewModel.Passengers.Add(new PassengerInfoViewModel
                {
                    DateOfBirth = new DateTime(1990, 1, 1),
                    Gender = "M",
                    DocumentType = "passport",
                    Citizenship = "РФ"
                });
            }

            // Если пользователь авторизован, подставляем его данные для первого пассажира
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId.HasValue)
            {
                var user = _context.Users.Find(userId.Value);
                if (user != null)
                {
                    viewModel.Contact.Email = user.Email;
                    // У user нет поля Phone, поэтому не заполняем его здесь

                    if (viewModel.Passengers.Count > 0)
                    {
                        viewModel.Passengers[0].LastName = user.LastName ?? "";
                        viewModel.Passengers[0].FirstName = user.FirstName ?? "";
                        viewModel.Passengers[0].MiddleName = user.MiddleName;
                    }
                }
            }

            return View(viewModel);
        }

        // POST: /TrainBooking/ProcessPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment([FromBody] CompleteBookingViewModel model)
        {
            try
            {
                _logger.LogInformation("Получен запрос на бронирование");
                _logger.LogInformation($"TrainInfo.Passengers: {model?.TrainInfo?.Passengers}");
                _logger.LogInformation($"model.Passengers.Count: {model?.Passengers?.Count}");
                _logger.LogInformation($"TrainInfo.IsRoundTrip: {model?.TrainInfo?.IsRoundTrip}");

                if (model == null)
                {
                    return Json(new { success = false, message = "Данные не переданы" });
                }

                if (model.TrainInfo == null)
                {
                    return Json(new { success = false, message = "Данные о поезде не переданы" });
                }

                if (model.Passengers == null || !model.Passengers.Any())
                {
                    return Json(new { success = false, message = "Данные пассажиров не переданы" });
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    return Json(new { success = false, message = "Проверьте правильность заполнения полей", errors });
                }

                var userId = HttpContext.Session.GetInt32("UserId");

                // Генерируем номер заказа
                var orderId = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();

                // ✅ Генерируем места ДЛЯ КАЖДОГО ПАССАЖИРА отдельно
                var random = new Random();
                var passengerCount = model.Passengers.Count;
                var forwardSeatsList = new List<string>();  // места туда для каждого пассажира
                var returnSeatsList = new List<string>();   // места обратно для каждого пассажира

                // Генерируем уникальные места для каждого пассажира (туда)
                for (int i = 0; i < passengerCount; i++)
                {
                    var carForward = random.Next(1, 15);
                    var seatForward = random.Next(1, 50);
                    forwardSeatsList.Add($"Вагон {carForward} | место {seatForward}");
                }

                // Генерируем места для обратного направления (если есть)
                if (model.TrainInfo.IsRoundTrip)
                {
                    for (int i = 0; i < passengerCount; i++)
                    {
                        var carReturn = random.Next(1, 15);
                        var seatReturn = random.Next(1, 50);
                        returnSeatsList.Add($"Вагон {carReturn} | место {seatReturn}");
                    }
                }

                // Формируем строку с местами для отображения в списке (общая строка)
                var seatNumbersParts = new List<string>();
                for (int i = 0; i < passengerCount; i++)
                {
                    var forwardSeat = forwardSeatsList[i];
                    if (model.TrainInfo.IsRoundTrip && i < returnSeatsList.Count)
                    {
                        var returnSeat = returnSeatsList[i];
                        seatNumbersParts.Add($"{forwardSeat} (туда) | {returnSeat} (обратно)");
                    }
                    else
                    {
                        seatNumbersParts.Add(forwardSeat);
                    }
                }
                var seatNumbers = string.Join(", ", seatNumbersParts);

                // Формируем строку с ФИО всех пассажиров
                var passengersFullNames = string.Join(", ", model.Passengers.Select(p => $"{p.LastName} {p.FirstName} {p.MiddleName}".Trim()));

                // Сохраняем пассажиров с их ПЕРСОНАЛЬНЫМИ местами в JSON
                var passengersWithSeats = new List<object>();
                for (int i = 0; i < passengerCount; i++)
                {
                    var p = model.Passengers[i];

                    // Парсим номер вагона и места из сгенерированных строк
                    var forwardMatch = System.Text.RegularExpressions.Regex.Match(forwardSeatsList[i], @"Вагон (\d+) \| место (\d+)");
                    var forwardCar = forwardMatch.Success ? forwardMatch.Groups[1].Value : "";
                    var forwardSeat = forwardMatch.Success ? forwardMatch.Groups[2].Value : "";

                    string returnCar = "";
                    string returnSeat = "";
                    if (model.TrainInfo.IsRoundTrip && i < returnSeatsList.Count)
                    {
                        var returnMatch = System.Text.RegularExpressions.Regex.Match(returnSeatsList[i], @"Вагон (\d+) \| место (\d+)");
                        returnCar = returnMatch.Success ? returnMatch.Groups[1].Value : "";
                        returnSeat = returnMatch.Success ? returnMatch.Groups[2].Value : "";
                    }

                    var passengerObj = new
                    {
                        p.LastName,
                        p.FirstName,
                        p.MiddleName,
                        p.DateOfBirth,
                        p.Gender,
                        p.DocumentType,
                        p.DocumentNumber,
                        p.Citizenship,
                        ForwardCarNumber = forwardCar,
                        ForwardSeatNumber = forwardSeat,
                        ReturnCarNumber = returnCar,
                        ReturnSeatNumber = returnSeat,
                        ForwardSeatDisplay = forwardSeatsList[i],
                        ReturnSeatDisplay = model.TrainInfo.IsRoundTrip ? (i < returnSeatsList.Count ? returnSeatsList[i] : "—") : ""
                    };
                    passengersWithSeats.Add(passengerObj);
                }
                var passengersJson = System.Text.Json.JsonSerializer.Serialize(passengersWithSeats);

                // Создаем заказ
                var trainOrder = new TrainOrder
                {
                    Id = orderId,
                    UserId = userId ?? 0,
                    OrderNumber = "RZD" + DateTime.Now.ToString("yyyyMMddHHmmss") + new Random().Next(100, 999),
                    TrainNumber = model.TrainInfo.TrainNumber,
                    ReturnTrainNumber = model.TrainInfo.ReturnTrainNumber,
                    DepartureStationId = model.TrainInfo.DepartureStationId,
                    DepartureStationName = model.TrainInfo.DepartureStationName,
                    ArrivalStationId = model.TrainInfo.ArrivalStationId,
                    ArrivalStationName = model.TrainInfo.ArrivalStationName,
                    DepartureDateTime = model.TrainInfo.DepartureDateTime,
                    ArrivalDateTime = model.TrainInfo.ArrivalDateTime ?? model.TrainInfo.DepartureDateTime.AddMinutes(model.TrainInfo.Duration),
                    ReturnDepartureDateTime = model.TrainInfo.ReturnDepartureDateTime,
                    ReturnArrivalDateTime = model.TrainInfo.ReturnArrivalDateTime,
                    TotalPrice = model.TotalPrice,
                    Currency = "RUB",
                    Passengers = passengerCount,
                    CarType = model.TrainInfo.CarType,
                    CarClass = model.TrainInfo.CarClass,
                    ContactEmail = model.Contact.Email,
                    ContactPhone = model.Contact.Phone,
                    PassengerFullName = passengersFullNames,
                    PassengerDocumentType = model.Passengers.FirstOrDefault()?.DocumentType,
                    PassengerDocumentNumber = model.Passengers.FirstOrDefault()?.DocumentNumber,
                    Status = OrderStatus.Pending,
                    PaymentStatus = PaymentStatus.Pending,
                    IsRoundTrip = model.TrainInfo.IsRoundTrip,
                    Duration = model.TrainInfo.Duration,
                    ReturnDuration = model.TrainInfo.ReturnDuration ??
                                     (model.TrainInfo.ReturnDepartureDateTime.HasValue && model.TrainInfo.ReturnArrivalDateTime.HasValue
                                         ? (int)(model.TrainInfo.ReturnArrivalDateTime.Value - model.TrainInfo.ReturnDepartureDateTime.Value).TotalMinutes
                                         : 0),
                    CreatedAt = DateTime.UtcNow,
                    ConfirmedAt = null,
                    TransactionId = null,
                    BookingReference = "BR" + new Random().Next(100000, 999999).ToString(),
                    TicketNumber = "TKT" + DateTime.Now.ToString("yyyyMMdd") + new Random().Next(1000, 9999),
                    PaymentMethod = null,
                    Notes = null,
                    ElectronicTicketUrl = null,
                    CarNumber = random.Next(1, 15).ToString(),
                    SeatNumbers = seatNumbers,
                    PassengersJson = passengersJson
                };

                _context.TrainOrders.Add(trainOrder);
                await _context.SaveChangesAsync();

                // Удаляем из избранного
                try
                {
                    var userId2 = HttpContext.Session.GetInt32("UserId");
                    if (userId2.HasValue && !string.IsNullOrEmpty(model.TrainInfo.TrainNumber))
                    {
                        var favorite = await _context.FavoriteTrains
                            .FirstOrDefaultAsync(f => f.UserId == userId2.Value && f.ForwardTrainNumber == model.TrainInfo.TrainNumber);

                        if (favorite != null)
                        {
                            _context.FavoriteTrains.Remove(favorite);
                            await _context.SaveChangesAsync();
                            _logger.LogInformation("Поезд {TrainNumber} удален из избранного после покупки", model.TrainInfo.TrainNumber);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Не удалось удалить поезд из избранного: {Message}", ex.Message);
                }

                // Сохраняем в кэш для страницы подтверждения
                var cacheKey = "TrainOrder_" + orderId;
                _cache.Set(cacheKey, trainOrder, TimeSpan.FromMinutes(30));

                // Отправляем билет на email
                await SendTicketEmail(trainOrder, model.Passengers, forwardSeatsList, returnSeatsList);

                return Json(new
                {
                    success = true,
                    message = "Билет успешно забронирован",
                    redirectUrl = Url.Action("Confirmation", new { orderId = orderId })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при бронировании");
                var innerMessage = ex.InnerException?.Message ?? ex.Message;
                return Json(new { success = false, message = "Произошла ошибка: " + innerMessage });
            }
        }

        [HttpGet]
        public IActionResult Confirmation(string orderId)
        {
            if (string.IsNullOrEmpty(orderId))
                return RedirectToAction("Index", "Railway");

            var cacheKey = "TrainOrder_" + orderId;
            if (_cache.TryGetValue(cacheKey, out TrainOrder order))
            {
                // Получаем пассажиров из JSON
                if (!string.IsNullOrEmpty(order.PassengersJson))
                {
                    var passengers = System.Text.Json.JsonSerializer.Deserialize<List<PassengerInfoViewModel>>(order.PassengersJson);
                    ViewBag.Passengers = passengers;

                    // Для отладки - выводим в консоль информацию о пассажирах
                    Console.WriteLine($"=== ЗАГРУЖЕНО ПАССАЖИРОВ: {passengers?.Count ?? 0} ===");
                    if (passengers != null)
                    {
                        foreach (var p in passengers)
                        {
                            Console.WriteLine($"Пассажир: {p.LastName} {p.FirstName}, Дата рождения: {p.DateOfBirth:yyyy-MM-dd}, Пол: {p.Gender}, Гражданство: {p.Citizenship}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("PassengersJson is null or empty");
                    // Если нет JSON, но есть данные из полей модели - создаем одного пассажира
                    var singlePassenger = new PassengerInfoViewModel
                    {
                        LastName = order.PassengerFullName?.Split(' ').FirstOrDefault() ?? "",
                        FirstName = order.PassengerFullName?.Split(' ').Skip(1).FirstOrDefault() ?? "",
                        MiddleName = order.PassengerFullName?.Split(' ').Skip(2).FirstOrDefault(),
                        DocumentType = order.PassengerDocumentType,
                        DocumentNumber = order.PassengerDocumentNumber,
                        DateOfBirth = DateTime.Now.AddYears(-30), // значение по умолчанию
                        Gender = "M",
                        Citizenship = "РФ"
                    };
                    ViewBag.Passengers = new List<PassengerInfoViewModel> { singlePassenger };
                }

                ViewBag.SeatNumbers = order.SeatNumbers;
                return View(order);
            }

            var dbOrder = _context.TrainOrders.FirstOrDefault(o => o.Id == orderId);
            if (dbOrder != null)
            {
                if (!string.IsNullOrEmpty(dbOrder.PassengersJson))
                {
                    var passengers = System.Text.Json.JsonSerializer.Deserialize<List<PassengerInfoViewModel>>(dbOrder.PassengersJson);
                    ViewBag.Passengers = passengers;
                }
                ViewBag.SeatNumbers = dbOrder.SeatNumbers;
                return View(dbOrder);
            }

            return RedirectToAction("Index", "Railway");
        }

        //[HttpGet]
        //public async Task<IActionResult> MyTickets()
        //{
        //    var userId = HttpContext.Session.GetInt32("UserId");
        //    if (userId == null)
        //        return RedirectToAction("Login", "Account");

        //    var orders = await _context.TrainOrders
        //        .Where(o => o.UserId == userId)
        //        .OrderByDescending(o => o.CreatedAt)
        //        .ToListAsync();

        //    return View(orders);
        //}
        [HttpGet]
        public async Task<IActionResult> Ticket(string orderId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            var order = await _context.TrainOrders
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
                return NotFound();

            // Проверяем, что это заказ текущего пользователя или админ
            if (order.UserId != userId && !User.IsInRole("Admin"))
                return Forbid();

            return View(order);
        }

        // GET: /TrainBooking/DownloadTicket/{orderId}
        [HttpGet]
        public async Task<IActionResult> DownloadTicket(string orderId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            var order = await _context.TrainOrders
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
                return NotFound();

            if (order.UserId != userId && !User.IsInRole("Admin"))
                return Forbid();

            // Генерируем HTML билета
            var htmlContent = GenerateTicketHtml(order);

            // Возвращаем HTML (можно потом конвертировать в PDF)
            return File(System.Text.Encoding.UTF8.GetBytes(htmlContent), "text/html", $"ticket_{order.OrderNumber}.html");
        }
        private string GenerateTicketHtml(TrainOrder order)
        {
            // Получаем данные пассажиров с местами из JSON
            List<PassengerWithSeatInfo> passengers = new List<PassengerWithSeatInfo>();

            if (!string.IsNullOrEmpty(order.PassengersJson))
            {
                try
                {
                    passengers = System.Text.Json.JsonSerializer.Deserialize<List<PassengerWithSeatInfo>>(order.PassengersJson);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Ошибка десериализации PassengersJson: {ex.Message}");
                }
            }

            // Если не удалось распарсить или нет данных, создаем из старых полей
            if (passengers == null || !passengers.Any())
            {
                passengers = new List<PassengerWithSeatInfo>();

                // Парсим места для отображения
                string forwardSeatDisplay = order.SeatNumbers ?? "—";
                string returnSeatDisplay = "";

                if (order.IsRoundTrip && !string.IsNullOrEmpty(order.SeatNumbers))
                {
                    var seatText = order.SeatNumbers;
                    if (seatText.Contains("(туда)") && seatText.Contains("(обратно)"))
                    {
                        var forwardMatch = System.Text.RegularExpressions.Regex.Match(seatText, @"(.+?)\(туда\)");
                        var returnMatch = System.Text.RegularExpressions.Regex.Match(seatText, @"\(туда\)\s*\|\s*(.+?)\(обратно\)");
                        if (forwardMatch.Success) forwardSeatDisplay = forwardMatch.Groups[1].Value.Trim();
                        if (returnMatch.Success) returnSeatDisplay = returnMatch.Groups[1].Value.Trim();
                    }
                }

                var singlePassenger = new PassengerWithSeatInfo
                {
                    LastName = order.PassengerFullName?.Split(' ').FirstOrDefault() ?? "",
                    FirstName = order.PassengerFullName?.Split(' ').Skip(1).FirstOrDefault() ?? "",
                    MiddleName = order.PassengerFullName?.Split(' ').Skip(2).FirstOrDefault(),
                    DocumentType = order.PassengerDocumentType,
                    DocumentNumber = order.PassengerDocumentNumber,
                    DateOfBirth = DateTime.Now.AddYears(-30),
                    Gender = "M",
                    Citizenship = "РФ",
                    ForwardSeatDisplay = forwardSeatDisplay,
                    ReturnSeatDisplay = returnSeatDisplay
                };
                passengers.Add(singlePassenger);

                // Если указано больше пассажиров, добавляем их с прочерками
                for (int i = 1; i < (order.Passengers > 0 ? order.Passengers : 1); i++)
                {
                    passengers.Add(new PassengerWithSeatInfo
                    {
                        LastName = "",
                        FirstName = "",
                        MiddleName = "",
                        DocumentType = "passport",
                        DocumentNumber = "",
                        DateOfBirth = DateTime.Now.AddYears(-30),
                        Gender = "M",
                        Citizenship = "РФ",
                        ForwardSeatDisplay = "—",
                        ReturnSeatDisplay = "—"
                    });
                }
            }

            var departureDate = order.DepartureDateTime.ToString("dd.MM.yyyy HH:mm");
            var arrivalDate = order.ArrivalDateTime?.ToString("dd.MM.yyyy HH:mm") ?? "—";

            var returnDepartureDate = order.ReturnDepartureDateTime?.ToString("dd.MM.yyyy HH:mm") ?? "—";
            var returnArrivalDate = order.ReturnArrivalDateTime?.ToString("dd.MM.yyyy HH:mm") ?? "—";

            // Формируем HTML пассажиров с персональными местами
            var passengersHtml = "";
            for (int i = 0; i < passengers.Count; i++)
            {
                var p = passengers[i];
                var fullName = $"{p.LastName} {p.FirstName} {p.MiddleName}".Trim();
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    fullName = "—";
                }
                var forwardSeat = !string.IsNullOrEmpty(p.ForwardSeatDisplay) ? p.ForwardSeatDisplay : "—";
                var returnSeat = !string.IsNullOrEmpty(p.ReturnSeatDisplay) ? p.ReturnSeatDisplay : "—";

                passengersHtml += $@"
        <div class='passenger-card'>
            <div class='passenger-name'><strong>Пассажир {i + 1}</strong>: {fullName}</div>
            <div class='passenger-details'>
                <div><span class='detail-label'>Дата рождения:</span> {p.DateOfBirth:dd.MM.yyyy}</div>
                <div><span class='detail-label'>Пол:</span> {(p.Gender == "M" ? "Мужской" : "Женский")}</div>
                <div><span class='detail-label'>Документ:</span> {GetDocumentTypeName(p.DocumentType)} {p.DocumentNumber}</div>
                <div><span class='detail-label'>Гражданство:</span> {p.Citizenship}</div>
                <div class='mt-2'><span class='detail-label'>Место (туда):</span> <strong>{forwardSeat}</strong></div>
                {(order.IsRoundTrip ? $"<div><span class='detail-label'>Место (обратно):</span> <strong>{returnSeat}</strong></div>" : "")}
            </div>
        </div>";
            }

            return $@"
    <!DOCTYPE html>
    <html>
    <head>
        <meta charset='UTF-8'>
        <title>ЖД билет {order.TicketNumber}</title>
        <style>
            body {{ 
                font-family: 'Arial', sans-serif; 
                margin: 0; 
                padding: 20px; 
                background: #f0f2f5; 
            }}
            .ticket {{ 
                max-width: 900px; 
                margin: 0 auto; 
                background: white; 
                border-radius: 16px; 
                box-shadow: 0 4px 12px rgba(0,0,0,0.1); 
                overflow: hidden; 
            }}
            .header {{ 
                background: linear-gradient(135deg, #0379D9, #40B624); 
                color: white; 
                padding: 30px; 
                text-align: center; 
            }}
            .header h1 {{ margin: 0; font-size: 28px; }}
            .header p {{ margin: 8px 0 0; opacity: 0.9; }}
            .content {{ padding: 30px; }}
            .route {{ 
                font-size: 22px; 
                font-weight: bold; 
                text-align: center; 
                margin: 10px 0; 
                color: #0379D9;
                word-break: break-word;
            }}
            .direction-block {{
                background: #f8fafc;
                border-radius: 12px;
                padding: 20px;
                margin-bottom: 25px;
            }}
            .direction-title {{
                font-size: 18px;
                font-weight: bold;
                margin-bottom: 15px;
                padding-bottom: 10px;
                border-bottom: 2px solid;
            }}
            .direction-title.forward {{ color: #0379D9; border-bottom-color: #0379D9; }}
            .direction-title.return {{ color: #fd7e14; border-bottom-color: #fd7e14; }}
            .info-grid {{
                display: grid;
                grid-template-columns: auto 1fr;
                gap: 12px 20px;
                margin-top: 15px;
            }}
            .info-label {{
                font-weight: bold;
                color: #64748b;
            }}
            .info-value {{
                color: #334155;
                word-break: break-word;
            }}
            .passenger-card {{
                background: white;
                border: 1px solid #e2e8f0;
                border-radius: 12px;
                padding: 15px;
                margin-bottom: 15px;
            }}
            .passenger-name {{
                font-size: 16px;
                font-weight: bold;
                margin-bottom: 12px;
                color: #0379D9;
                word-break: break-word;
            }}
            .passenger-details {{
                display: grid;
                grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
                gap: 10px;
                font-size: 14px;
            }}
            .passenger-details .detail-label {{
                font-weight: bold;
                color: #64748b;
                margin-right: 8px;
            }}
            .price-block {{ 
                background: #e8f4fe; 
                padding: 20px; 
                border-radius: 12px; 
                text-align: center; 
                margin: 25px 0 20px; 
            }}
            .price-block .total {{ 
                font-size: 28px; 
                font-weight: bold; 
                color: #0379D9; 
            }}
            .price-block .small-text {{
                margin: 10px 0 0;
                color: #64748b;
                font-size: 14px;
            }}
            .footer {{ 
                background: #f8fafc; 
                padding: 20px; 
                text-align: center; 
                font-size: 12px; 
                color: #94a3b8; 
            }}
            @media (max-width: 600px) {{
                body {{ padding: 10px; }}
                .content {{ padding: 15px; }}
                .info-grid {{ grid-template-columns: 1fr; gap: 8px; }}
                .passenger-details {{ grid-template-columns: 1fr; }}
                .route {{ font-size: 18px; }}
                .direction-block {{ padding: 15px; }}
                .header h1 {{ font-size: 22px; }}
            }}
            @media print {{
                body {{ background: white; padding: 0; }}
                .ticket {{ box-shadow: none; }}
            }}
        </style>
    </head>
    <body>
        <div class='ticket'>
            <div class='header'>
                <h1>Электронный билет{(order.IsRoundTrip ? "ы" : "")}</h1>
                <p>Номер бронирования: {order.BookingReference}</p>
                <p>Номер билета: {order.TicketNumber}</p>
                <p>Дата покупки: {order.CreatedAt:dd.MM.yyyy HH:mm}</p>
            </div>
            <div class='content'>
                <!-- НАПРАВЛЕНИЕ ТУДА -->
                <div class='direction-block'>
                    <div class='direction-title forward'>
                        🚆 Туда
                    </div>
                    <div class='route'>
                        {order.DepartureStationName} → {order.ArrivalStationName}
                    </div>
                    <div class='info-grid'>
                        <div><span class='info-label'>Поезд:</span></div>
                        <div class='info-value'>№ {order.TrainNumber}</div>
                        
                        <div><span class='info-label'>Отправление:</span></div>
                        <div class='info-value'>{departureDate}</div>
                        
                        <div><span class='info-label'>Прибытие:</span></div>
                        <div class='info-value'>{arrivalDate}</div>
                        
                        <div><span class='info-label'>Время в пути:</span></div>
                        <div class='info-value'>{FormatDuration(order.Duration)}</div>
                        
                        <div><span class='info-label'>Тип вагона:</span></div>
                        <div class='info-value'>{GetCarTypeName(order.CarType)} ({order.CarClass})</div>
                    </div>
                </div>

                <!-- НАПРАВЛЕНИЕ ОБРАТНО (если есть) -->
                {(order.IsRoundTrip ? $@"
                <div class='direction-block'>
                    <div class='direction-title return'>
                        🔄 Обратно
                    </div>
                    <div class='route'>
                        {order.ArrivalStationName} → {order.DepartureStationName}
                    </div>
                    <div class='info-grid'>
                        <div><span class='info-label'>Поезд:</span></div>
                        <div class='info-value'>№ {order.ReturnTrainNumber}</div>
                        
                        <div><span class='info-label'>Отправление:</span></div>
                        <div class='info-value'>{returnDepartureDate}</div>
                        
                        <div><span class='info-label'>Прибытие:</span></div>
                        <div class='info-value'>{returnArrivalDate}</div>
                        
                        <div><span class='info-label'>Время в пути:</span></div>
                        <div class='info-value'>{FormatDuration(order.ReturnDuration ?? 0)}</div>
                        
                        <div><span class='info-label'>Тип вагона:</span></div>
                        <div class='info-value'>{GetCarTypeName(order.CarType)} ({order.CarClass})</div>
                    </div>
                </div>
                " : "")}

                <!-- Данные пассажиров с местами -->
                <h3 style='color: #0379D9; margin: 25px 0 15px 0;'>👥 Пассажиры</h3>
                {passengersHtml}

                <!-- Цена -->
                <div class='price-block'>
                    <div class='total'>{order.TotalPrice:N0} {order.Currency}</div>
                    <div class='small-text'>Включая все сборы и сервисные услуги</div>
                    <div class='small-text'>Количество пассажиров: {order.Passengers}</div>
                </div>

                <div style='background: #e2e8f0; padding: 15px; border-radius: 12px; margin-top: 20px;'>
                    <p style='margin: 0; color: #334155;'>
                        <strong>⚠️ Важно!</strong> Для посадки необходимо предъявить документ, 
                        указанный в билете, и данный электронный билет (можно на экране телефона).<br>
                        При посадке на поезд пассажир должен занимать место, указанное в билете.
                    </p>
                </div>
            </div>
            <div class='footer'>
                <p>Спасибо, что пользуетесь сервисом <strong>Вместе В Путь</strong></p>
                <p>© {DateTime.Now.Year} Все права защищены</p>
            </div>
        </div>
    </body>
    </html>";
        }

        // Вспомогательный метод для типа вагона
        private string GetCarTypeName(string carType)
        {
            return carType switch
            {
                "sedentary" => "Сидячий",
                "reserved_seat" => "Плацкарт",
                "coupe" => "Купе",
                "lux" => "Люкс",
                "plazcard" => "Плацкарт",
                "compartment" => "Купе",
                "suite" => "Люкс",
                _ => carType ?? "Стандарт"
            };
        }

        // Вспомогательные методы
        private List<string> GenerateSeatNumbers(int count)
        {
            var seats = new List<string>();
            var random = new Random();

            for (int i = 0; i < count; i++)
            {
                var car = random.Next(1, 15);
                var seat = random.Next(1, 50);
                // Убираем запятую внутри - используем другой разделитель, например " | "
                seats.Add($"Вагон {car} | место {seat}");
            }

            return seats;
        }

        private async Task SendTicketEmail(TrainOrder order, List<PassengerInfoViewModel> passengers, List<string> forwardSeats, List<string> returnSeats)
        {
            var subject = order.IsRoundTrip
                ? $"Ваши билеты на поезд {order.TrainNumber} (туда и обратно) - Вместе В Путь"
                : $"Ваш билет на поезд {order.TrainNumber} - Вместе В Путь";

            // Форматируем даты
            var departureDate = order.DepartureDateTime.ToString("dd.MM.yyyy HH:mm");
            var arrivalDate = order.ArrivalDateTime?.ToString("dd.MM.yyyy HH:mm") ?? "—";

            var returnDepartureDate = order.ReturnDepartureDateTime?.ToString("dd.MM.yyyy HH:mm") ?? "—";
            var returnArrivalDate = order.ReturnArrivalDateTime?.ToString("dd.MM.yyyy HH:mm") ?? "—";

            // ✅ ФОРМАТИРУЕМ ВРЕМЯ В ПУТИ ДЛЯ ОБРАТНОГО НАПРАВЛЕНИЯ
            var returnDurationFormatted = FormatDuration(order.ReturnDuration ?? 0);
            var forwardDurationFormatted = FormatDuration(order.Duration);

            // Формируем HTML для всех пассажиров
            var passengersHtml = "";

            for (int i = 0; i < passengers.Count; i++)
            {
                var p = passengers[i];
                var forwardSeat = i < forwardSeats.Count ? forwardSeats[i] : "—";
                var returnSeat = order.IsRoundTrip && i < returnSeats.Count ? returnSeats[i] : "—";

                var fullName = $"{p.LastName} {p.FirstName} {p.MiddleName}".Trim();
                if (fullName.Length > 50)
                {
                    fullName = fullName.Substring(0, 47) + "...";
                }

                passengersHtml += $@"
    <tr>
        <td data-label='ФИО' style='word-break: break-word;'>{fullName}</td>
        <td data-label='Дата рождения'>{p.DateOfBirth:dd.MM.yyyy}</td>
        <td data-label='Документ'>{GetDocumentTypeName(p.DocumentType)} {p.DocumentNumber}</td>
        <td data-label='Место (туда)'>{forwardSeat}</td>
        {(order.IsRoundTrip ? $"<td data-label='Место (обратно)'>{returnSeat}</td>" : "")}
    </tr>";
            }

            // Формируем секцию обратного билета с временем в пути
            var returnSection = "";
            if (order.IsRoundTrip)
            {
                returnSection = $@"
        <div class='info-block' style='margin-top: 20px; border-left: 4px solid #fd7e14;'>
            <h3 style='color: #fd7e14; margin-top: 0;'>🔄 Обратный билет</h3>
            <div class='info-row'>
                <span class='info-label'>Поезд:</span>
                <span>№ {order.ReturnTrainNumber}</span>
            </div>
            <div class='info-row'>
                <span class='info-label'>Отправление:</span>
                <span>{returnDepartureDate}</span>
            </div>
            <div class='info-row'>
                <span class='info-label'>Прибытие:</span>
                <span>{returnArrivalDate}</span>
            </div>
            <div class='info-row'>
                <span class='info-label'>Время в пути:</span>
                <span>{returnDurationFormatted}</span>
            </div>
            <div class='info-row'>
                <span class='info-label'>Тип вагона:</span>
                <span>{order.CarType} ({order.CarClass})</span>
            </div>
        </div>";
            }

            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
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
            overflow-wrap: break-word;
        }}
        .header {{ 
            background: linear-gradient(135deg, #0379D9, #40B624); 
            color: white; 
            padding: 20px; 
            border-radius: 12px 12px 0 0; 
            margin: -20px -20px 20px -20px; 
        }}
        .header h2 {{ margin: 0; font-size: 24px; }}
        .route {{ 
            font-size: 28px; 
            font-weight: bold; 
            text-align: center; 
            margin: 20px 0; 
            color: #0379D9;
            word-wrap: break-word;
            overflow-wrap: break-word;
        }}
        .info-block {{
            background: white;
            border-radius: 8px;
            padding: 15px;
            margin: 15px 0;
            border: 1px solid #e2e8f0;
        }}
        .info-row {{
            display: flex;
            justify-content: space-between;
            padding: 8px 0;
            border-bottom: 1px solid #e2e8f0;
        }}
        .info-row:last-child {{
            border-bottom: none;
        }}
        .info-label {{
            font-weight: bold;
            color: #64748b;
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
        .footer {{ 
            text-align: center; 
            margin-top: 30px; 
            color: #94a3b8; 
            font-size: 12px; 
        }}
        @media (max-width: 600px) {{
            body {{
                padding: 10px;
            }}
            .route {{
                font-size: 20px;
            }}
            .info-row {{
                flex-direction: column;
                gap: 5px;
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
            <h2>Электронный билет{(order.IsRoundTrip ? "ы" : "")}</h2>
            <p>Номер бронирования: {order.BookingReference}</p>
            <p>Номер билета: {order.TicketNumber}</p>
        </div>

        <div class='route'>
            {order.DepartureStationName} → {order.ArrivalStationName}
        </div>

        <div class='info-block'>
            <div class='info-row'>
                <span class='info-label'>Поезд (туда):</span>
                <span>№ {order.TrainNumber}</span>
            </div>
            <div class='info-row'>
                <span class='info-label'>Отправление:</span>
                <span>{departureDate}</span>
            </div>
            <div class='info-row'>
                <span class='info-label'>Прибытие:</span>
                <span>{arrivalDate}</span>
            </div>
            <div class='info-row'>
                <span class='info-label'>Время в пути:</span>
                <span>{forwardDurationFormatted}</span>
            </div>
            <div class='info-row'>
                <span class='info-label'>Тип вагона:</span>
                <span>{order.CarType} ({order.CarClass})</span>
            </div>
        </div>

        {returnSection}

        <h3>Пассажиры</h3>
        <table>
            <thead>
                <tr>
                    <th style='width: 30%'>ФИО</th>
                    <th style='width: 15%'>Дата рождения</th>
                    <th style='width: 25%'>Документ</th>
                    <th style='width: 15%'>Место (туда)</th>
                    {(order.IsRoundTrip ? "<th style='width: 15%'>Место (обратно)</th>" : "")}
                </tr>
            </thead>
            <tbody>
                {passengersHtml}
            </tbody>
        </table>

        <div class='price'>
            <p>Цена за билет: {(order.TotalPrice / order.Passengers):N0} ₽</p>
            <p>Количество пассажиров: {order.Passengers}</p>
            <p class='total'>Итого: {order.TotalPrice:N0} {order.Currency}</p>
        </div>

        <div class='payment-info'>
            <p><strong>💡 Оплата билета</strong></p>
            <p>Для оплаты билета перейдите в раздел <strong>«Мои заказы»</strong> в вашем личном кабинете.</p>
        </div>

        <div style='background: #e2e8f0; padding: 15px; border-radius: 8px; margin-top: 20px;'>
            <p style='margin: 0; color: #334155;'><strong>Важно!</strong> Для посадки необходимо предъявить документ, указанный в билете, и данный электронный билет (можно на экране телефона).</p>
        </div>

        <div class='footer'>
            <p>Спасибо, что пользуетесь сервисом <strong>Вместе В Путь</strong></p>
            <p>© {DateTime.Now.Year} Все права защищены</p>
        </div>
    </div>
</body>
</html>";

            await _emailService.SendAsync(order.ContactEmail, subject, body);
        }

        private string GetTrainType(string trainNumber)
        {
            if (trainNumber.StartsWith("0") || trainNumber.StartsWith("1") || trainNumber.StartsWith("2"))
                return "Фирменный";
            if (trainNumber.StartsWith("3") || trainNumber.StartsWith("4"))
                return "Скоростной";
            if (trainNumber.StartsWith("7") || trainNumber.StartsWith("8"))
                return "Пригородный";
            return "Пассажирский";
        }

        private string FormatDuration(int minutes)
        {
            var hours = minutes / 60;
            var mins = minutes % 60;
            return $"{hours} ч {mins} мин";
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

        private byte[] GenerateTicketPdf(TrainOrder order)
        {
            // Здесь должна быть генерация PDF
            // Пока возвращаем пустой массив
            return new byte[0];
        }
    }
    public class PassengerWithSeatInfo : PassengerInfoViewModel
    {
        public string ForwardCarNumber { get; set; }
        public string ForwardSeatNumber { get; set; }
        public string ReturnCarNumber { get; set; }
        public string ReturnSeatNumber { get; set; }
        public string ForwardSeatDisplay { get; set; }
        public string ReturnSeatDisplay { get; set; }
    }
}