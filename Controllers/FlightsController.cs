using Microsoft.AspNetCore.Mvc;
using TripWise.Models;
using System.Text.Json;
using TripWise.Models.ViewModels;
using TripWise.Services;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TripWise.Models.ViewModels;

namespace TripWise.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FlightsController : ControllerBase
    {
        private readonly IFlightService _flightService;
        private readonly ILogger<FlightsController> _logger;
        private readonly TripWiseContext _context;
        private readonly EmailService _emailService;

        public FlightsController(IFlightService flightService, ILogger<FlightsController> logger, TripWiseContext context, EmailService emailService)
        {
            _flightService = flightService;
            _logger = logger;
            _context = context;
            _emailService = emailService;
        }

        [HttpPost("search")]
        public async Task<ActionResult<FlightSearchResponse>> SearchFlights([FromBody] FlightSearchRequest request)
        {
            try
            {
                _logger.LogInformation("=== ПОИСК РЕЙСОВ API ===");
                _logger.LogInformation("Запрос получен: {@Request}", request);

                // Валидация запроса
                var validationError = ValidateFlightSearchRequest(request);
                if (!string.IsNullOrEmpty(validationError))
                {
                    _logger.LogWarning("Ошибка валидации: {Error}", validationError);
                    return BadRequest(new FlightSearchResponse
                    {
                        Success = false,
                        Error = validationError
                    });
                }

                _logger.LogInformation("Параметры поиска:");
                _logger.LogInformation("- Откуда: {DepartureCity}", request.DepartureCity);
                _logger.LogInformation("- Куда: {ArrivalCity}", request.ArrivalCity);
                _logger.LogInformation("- Дата вылета: {DepartureDate}", request.DepartureDate);
                _logger.LogInformation("- Дата обратно: {ReturnDate}", request.ReturnDate);
                _logger.LogInformation("- Пассажиры: {Passengers}", request.Passengers);
                _logger.LogInformation("- Класс: {Class}", request.Class);
                _logger.LogInformation("- Тип: {TripType}", request.TripType);

                // Выполняем поиск рейсов
                var result = await _flightService.SearchFlightsAsync(request);

                _logger.LogInformation("Результат поиска:");
                _logger.LogInformation("- Успех: {Success}", result.Success);
                _logger.LogInformation("- Найдено рейсов: {Count}", result.Flights?.Count ?? 0);
                _logger.LogInformation("- ID поиска: {SearchId}", result.SearchId);

                if (!string.IsNullOrEmpty(result.Error))
                {
                    _logger.LogError("Ошибка поиска: {Error}", result.Error);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске рейсов");
                return StatusCode(500, new FlightSearchResponse
                {
                    Success = false,
                    Error = "Внутренняя ошибка сервера",
                    Message = ex.Message
                });
            }
        }

        [HttpGet("cities")]
        public async Task<ActionResult> SearchCities([FromQuery] string query)
        {
            try
            {
                _logger.LogInformation("Поиск городов: {Query}", query);

                if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                {
                    return Ok(new
                    {
                        Success = true,
                        Cities = new List<City>(),
                        Message = "Введите минимум 2 символа"
                    });
                }

                var cities = await _flightService.SearchCitiesAsync(query);

                _logger.LogInformation("Найдено городов: {Count}", cities.Count);

                return Ok(new
                {
                    Success = true,
                    Cities = cities,
                    Message = $"Найдено {cities.Count} городов"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске городов");
                return StatusCode(500, new
                {
                    Success = false,
                    Error = "Ошибка при поиске городов",
                    Message = ex.Message
                });
            }
        }

        [HttpGet("popular-cities")]
        public async Task<ActionResult> GetPopularCities()
        {
            try
            {
                _logger.LogInformation("Запрос популярных городов");

                var cities = await _flightService.GetPopularCitiesAsync();

                _logger.LogInformation("Отправлено популярных городов: {Count}", cities.Count);

                return Ok(new
                {
                    Success = true,
                    Cities = cities,
                    Message = "Популярные города для путешествий"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении популярных городов");
                return StatusCode(500, new
                {
                    Success = false,
                    Error = "Ошибка при получении городов",
                    Message = ex.Message
                });
            }
        }

        [HttpGet("test")]
        public async Task<ActionResult> TestService()
        {
            try
            {
                _logger.LogInformation("Тестирование сервиса авиабилетов");

                var testRequest = new FlightSearchRequest
                {
                    DepartureCity = "Москва (MOW)",
                    ArrivalCity = "Санкт-Петербург (LED)",
                    DepartureDate = DateTime.Now.AddDays(7),
                    ReturnDate = DateTime.Now.AddDays(14),
                    Passengers = 2,
                    Class = "economy",
                    TripType = "round"
                };

                var result = await _flightService.SearchFlightsAsync(testRequest);

                return Ok(new
                {
                    Success = true,
                    Message = "Сервис авиабилетов работает корректно",
                    FlightsCount = result.Flights?.Count ?? 0,
                    SearchId = result.SearchId,
                    TestRequest = testRequest
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при тестировании сервиса");
                return StatusCode(500, new
                {
                    Success = false,
                    Error = ex.Message,
                    Message = "Сервис авиабилетов временно недоступен"
                });
            }
        }

        [HttpGet("test-search")]
        public async Task<ActionResult> TestSearch()
        {
            try
            {
                _logger.LogInformation("Тестовый поиск рейсов");

                var testRequest = new FlightSearchRequest
                {
                    DepartureCity = "Москва (MOW)",
                    ArrivalCity = "Санкт-Петербург (LED)",
                    DepartureDate = DateTime.Now.AddDays(7),
                    ReturnDate = DateTime.Now.AddDays(14),
                    Passengers = 2,
                    Class = "economy",
                    TripType = "round"
                };

                _logger.LogInformation("Тестовый запрос: {@Request}", testRequest);

                var result = await _flightService.SearchFlightsAsync(testRequest);

                return Ok(new
                {
                    TestRequest = testRequest,
                    SearchResult = result,
                    ServerTime = DateTime.Now,
                    Status = "OK"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка тестового поиска");
                return StatusCode(500, new
                {
                    Error = ex.Message,
                    StackTrace = ex.StackTrace,
                    ServerTime = DateTime.Now,
                    Status = "ERROR"
                });
            }
        }

        [HttpGet("debug")]
        public ActionResult Debug()
        {
            var endpointInfo = new
            {
                Timestamp = DateTime.Now,
                Endpoint = "/api/flights/search",
                Method = "POST",
                RequiredHeaders = new
                {
                    ContentType = "application/json"
                },
                ExpectedModel = new
                {
                    DepartureCity = "string (например: 'Москва' или 'Москва (MOW)')",
                    ArrivalCity = "string (например: 'Санкт-Петербург' или 'Санкт-Петербург (LED)')",
                    DepartureDate = "string (формат: YYYY-MM-DD)",
                    ReturnDate = "string (формат: YYYY-MM-DD) или null",
                    Passengers = "integer (от 1 до 9)",
                    Class = "string (economy, business, first)",
                    TripType = "string (oneway или round)"
                },
                ExampleRequest = new
                {
                    DepartureCity = "Москва",
                    ArrivalCity = "Санкт-Петербург",
                    DepartureDate = "2024-12-20",
                    ReturnDate = "2024-12-27",
                    Passengers = 2,
                    Class = "economy",
                    TripType = "round"
                }
            };

            return Ok(new
            {
                Success = true,
                Message = "Информация о API авиабилетов",
                ServerInfo = new
                {
                    ServerTime = DateTime.Now,
                    Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
                },
                Endpoints = new[]
                {
                    new { Path = "/api/flights/search", Method = "POST", Description = "Поиск рейсов" },
                    new { Path = "/api/flights/cities", Method = "GET", Description = "Поиск городов" },
                    new { Path = "/api/flights/popular-cities", Method = "GET", Description = "Популярные города" },
                    new { Path = "/api/flights/test", Method = "GET", Description = "Тест сервиса" },
                    new { Path = "/api/flights/test-search", Method = "GET", Description = "Тестовый поиск" },
                    new { Path = "/api/flights/debug", Method = "GET", Description = "Отладочная информация" }
                },
                Details = endpointInfo
            });
        }

        [HttpGet("health")]
        public ActionResult HealthCheck()
        {
            return Ok(new
            {
                Status = "Healthy",
                Timestamp = DateTime.Now,
                Service = "Flights API",
                Version = "1.0.0"
            });
        }

        [HttpGet("route-info/{fromCity}/{toCity}")]
        public async Task<ActionResult> GetRouteInfo(string fromCity, string toCity)
        {
            try
            {
                _logger.LogInformation("Получение информации о маршруте: {From} -> {To}", fromCity, toCity);

                if (string.IsNullOrEmpty(fromCity) || string.IsNullOrEmpty(toCity))
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Error = "Необходимо указать города отправления и назначения"
                    });
                }

                var routeInfo = await _flightService.GetRouteInfoAsync(fromCity, toCity);

                return Ok(new
                {
                    Success = true,
                    RouteInfo = routeInfo,
                    Message = $"Информация о маршруте {fromCity} → {toCity}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении информации о маршруте");
                return StatusCode(500, new
                {
                    Success = false,
                    Error = "Ошибка при получении информации о маршруте",
                    Message = ex.Message
                });
            }
        }

        [HttpGet("sample-response")]
        public ActionResult GetSampleResponse()
        {
            var sampleResponse = new FlightSearchResponse
            {
                Success = true,
                Message = "Демонстрационные данные",
                SearchId = Guid.NewGuid().ToString(),
                Flights = new List<Flight>
                {
                    new Flight
                    {
                        Id = "SU-1234",
                        Airline = "Аэрофлот",
                        AirlineCode = "SU",
                        FlightNumber = "SU 1234",
                        DepartureCity = "Москва",
                        ArrivalCity = "Санкт-Петербург",
                        DepartureAirport = "SVO",
                        ArrivalAirport = "LED",
                        DepartureTime = DateTime.Now.AddDays(1).AddHours(8),
                        ArrivalTime = DateTime.Now.AddDays(1).AddHours(10),
                        Price = 4500,
                        Currency = "RUB",
                        Transfers = 0,
                        Duration = 120,
                        Aircraft = "Airbus A320",
                        IsReturn = false,
                        BookingUrl = "https://www.aviasales.ru/search",
                        Details = new FlightDetails
                        {
                            IsRefundable = true,
                            IsChangeable = true,
                            Baggage = "1x23кг",
                            HandLuggage = "1x10кг",
                            Meal = "Завтрак"
                        }
                    },
                    new Flight
                    {
                        Id = "S7-5678",
                        Airline = "S7 Airlines",
                        AirlineCode = "S7",
                        FlightNumber = "S7 5678",
                        DepartureCity = "Москва",
                        ArrivalCity = "Санкт-Петербург",
                        DepartureAirport = "DME",
                        ArrivalAirport = "LED",
                        DepartureTime = DateTime.Now.AddDays(1).AddHours(14),
                        ArrivalTime = DateTime.Now.AddDays(1).AddHours(16),
                        Price = 5200,
                        Currency = "RUB",
                        Transfers = 0,
                        Duration = 120,
                        Aircraft = "Boeing 737",
                        IsReturn = false,
                        BookingUrl = "https://www.aviasales.ru/search",
                        Details = new FlightDetails
                        {
                            IsRefundable = false,
                            IsChangeable = true,
                            Baggage = "1x23кг",
                            HandLuggage = "1x10кг",
                            Meal = "Обед"
                        }
                    }
                },
                PartnerLinks = new PartnerLinks
                {
                    AviasalesUrl = "https://www.aviasales.ru/search",
                    YandexTravelUrl = "https://travel.yandex.ru/avia",
                    TutuUrl = "https://www.tutu.ru/avia",
                    SkyscannerUrl = "https://www.skyscanner.ru"
                }
            };

            return Ok(sampleResponse);
        }

        //[HttpPost("book")]
        //public async Task<ActionResult<FlightBookingResponse>> BookFlight([FromBody] CompleteFlightBookingViewModel request)
        //{
        //    try
        //    {
        //        _logger.LogInformation("=== БРОНИРОВАНИЕ РЕЙСА ===");
        //        _logger.LogInformation("Запрос: {@Request}", request);

        //        // Проверяем авторизацию
        //        int? userId = null;
        //        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        //        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int parsedUserId))
        //        {
        //            userId = parsedUserId;
        //            _logger.LogInformation("Пользователь авторизован, ID: {UserId}", userId);
        //        }

        //        // Валидация
        //        if (request == null)
        //            return BadRequest(new FlightBookingResponse { Success = false, Message = "Запрос не может быть пустым" });

        //        if (request.Flight == null)
        //            return BadRequest(new FlightBookingResponse { Success = false, Message = "Данные рейса отсутствуют" });

        //        if (request.Passengers == null || !request.Passengers.Any())
        //            return BadRequest(new FlightBookingResponse { Success = false, Message = "Добавьте хотя бы одного пассажира" });

        //        if (request.Contact == null)
        //            return BadRequest(new FlightBookingResponse { Success = false, Message = "Укажите контактные данные" });

        //        // Сериализуем данные пассажиров в JSON
        //        var passengersJson = JsonSerializer.Serialize(request.Passengers);

        //        // Генерируем номер бронирования (PNR)
        //        var bookingReference = GeneratePnrCode();
        //        var ticketNumber = GenerateTicketNumber();

        //        // Генерируем номера мест
        //        var seatNumbers = GenerateSeatNumbers(request.Passengers.Count);

        //        // Создаем бронирование
        //        var booking = new FlightBooking
        //        {
        //            Id = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
        //            UserId = userId ?? 0,
        //            BookingNumber = "FLT" + DateTime.Now.ToString("yyyyMMddHHmmss") + new Random().Next(100, 999),

        //            // Данные рейса туда
        //            FlightId = request.Flight.FlightId,
        //            Airline = request.Flight.Airline,
        //            AirlineCode = request.Flight.AirlineCode,
        //            AirlineLogo = request.Flight.AirlineLogo,
        //            FlightNumber = request.Flight.FlightNumber,
        //            DepartureCity = request.Flight.DepartureCity,
        //            ArrivalCity = request.Flight.ArrivalCity,
        //            DepartureAirport = request.Flight.DepartureAirport,
        //            ArrivalAirport = request.Flight.ArrivalAirport,
        //            DepartureDateTime = request.Flight.DepartureDateTime,
        //            ArrivalDateTime = request.Flight.ArrivalDateTime,
        //            Duration = request.Flight.Duration,
        //            Transfers = request.Flight.Transfers,
        //            Aircraft = request.Flight.Aircraft,

        //            // Данные обратного рейса (если есть)
        //            ReturnFlightId = request.Flight.ReturnFlightId,
        //            ReturnAirline = request.Flight.ReturnAirline,
        //            ReturnFlightNumber = request.Flight.ReturnFlightNumber,
        //            ReturnDepartureDateTime = request.Flight.ReturnDepartureDateTime,
        //            ReturnArrivalDateTime = request.Flight.ReturnArrivalDateTime,
        //            ReturnDuration = request.Flight.ReturnDuration,
        //            ReturnTransfers = request.Flight.ReturnTransfers,

        //            // Цена и пассажиры
        //            Price = request.Flight.Price,
        //            Passengers = request.Passengers.Count,
        //            FlightClass = request.Flight.FlightClass,
        //            IsRoundTrip = request.Flight.IsRoundTrip,

        //            // Багаж и услуги
        //            Baggage = request.Flight.Baggage ?? "1x23кг",
        //            HandLuggage = request.Flight.HandLuggage ?? "1x10кг",
        //            Meal = request.Flight.Meal ?? "Включено",

        //            // Контактные данные
        //            ContactName = request.Contact.Name,
        //            ContactEmail = request.Contact.Email,
        //            ContactPhone = request.Contact.Phone,

        //            // Данные пассажиров
        //            PassengersJson = passengersJson,
        //            SeatNumbers = seatNumbers,

        //            // Статусы
        //            Status = BookingStatus.Confirmed,
        //            PaymentStatus = PaymentStatus.Paid,
        //            PaymentMethod = "Банковская карта",
        //            TransactionId = "TXN" + DateTime.Now.Ticks.ToString().Substring(0, 12),
        //            CreatedAt = DateTime.UtcNow,
        //            ConfirmedAt = DateTime.UtcNow,

        //            // Бронирование и билет
        //            BookingReference = bookingReference,
        //            TicketNumber = ticketNumber
        //        };

        //        _context.FlightBookings.Add(booking);
        //        await _context.SaveChangesAsync();

        //        _logger.LogInformation("Бронирование создано: {BookingId}, номер билета: {TicketNumber}", booking.Id, ticketNumber);

        //        // Отправляем подтверждение на email
        //        await SendBookingConfirmationEmail(booking, request.Passengers);

        //        var response = new FlightBookingResponse
        //        {
        //            Success = true,
        //            BookingId = booking.Id,
        //            BookingNumber = booking.BookingNumber,
        //            TicketNumber = ticketNumber,
        //            BookingReference = bookingReference,
        //            TotalPrice = request.TotalPrice,
        //            Message = "Бронирование успешно создано"
        //        };

        //        return Ok(response);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Ошибка при бронировании рейса");
        //        return StatusCode(500, new FlightBookingResponse
        //        {
        //            Success = false,
        //            Message = "Ошибка при бронировании рейса: " + ex.Message
        //        });
        //    }
        //}

        [HttpGet("ticket/{ticketNumber}")]
        public async Task<ActionResult> GetTicketInfo(string ticketNumber)
        {
            try
            {
                var booking = await _context.FlightBookings
                    .FirstOrDefaultAsync(b => b.TicketNumber == ticketNumber);

                if (booking == null)
                    return NotFound(new { Success = false, Message = "Билет не найден" });

                // Десериализуем данные пассажиров
                var passengers = JsonSerializer.Deserialize<List<FlightPassengerViewModel>>(booking.PassengersJson);

                // Маскируем конфиденциальные данные для публичного просмотра
                var maskedBooking = new
                {
                    booking.Airline,
                    booking.FlightNumber,
                    booking.DepartureCity,
                    booking.ArrivalCity,
                    booking.DepartureAirport,
                    booking.ArrivalAirport,
                    booking.DepartureDateTime,
                    booking.ArrivalDateTime,
                    booking.Status,
                    booking.SeatNumbers,
                    Passengers = passengers.Select(p => new
                    {
                        p.FirstName,
                        p.LastName,
                        // Не показываем полный номер документа
                        DocumentNumber = MaskDocumentNumber(p.DocumentNumber)
                    }),
                    BoardingTime = booking.DepartureDateTime.AddHours(-2),
                    Gate = GetGateForFlight(booking.FlightNumber),
                    Terminal = GetTerminalForAirport(booking.DepartureAirport)
                };

                return Ok(new
                {
                    Success = true,
                    Ticket = maskedBooking,
                    Message = "Информация о билете"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении информации о билете");
                return StatusCode(500, new { Success = false, Message = "Ошибка при получении информации о билете" });
            }
        }

        private string ValidateFlightSearchRequest(FlightSearchRequest request)
        {
            if (request == null)
                return "Запрос не может быть пустым";

            if (string.IsNullOrWhiteSpace(request.DepartureCity))
                return "Город вылета обязателен";

            if (string.IsNullOrWhiteSpace(request.ArrivalCity))
                return "Город прилета обязателен";

            if (request.DepartureDate == default)
                return "Дата вылета обязательна";

            if (request.DepartureDate < DateTime.Today)
                return "Дата вылета не может быть в прошлом";

            if (request.ReturnDate.HasValue && request.ReturnDate.Value < request.DepartureDate)
                return "Дата обратного вылета не может быть раньше даты вылета";

            if (request.Passengers < 1 || request.Passengers > 9)
                return "Количество пассажиров должно быть от 1 до 9";

            if (!string.IsNullOrEmpty(request.Class) &&
                !new[] { "economy", "business", "first" }.Contains(request.Class.ToLower()))
                return "Класс должен быть: economy, business или first";

            if (!string.IsNullOrEmpty(request.TripType) &&
                !new[] { "oneway", "round" }.Contains(request.TripType.ToLower()))
                return "Тип поездки должен быть: oneway или round";

            return null;
        }
        private string GeneratePnrCode()
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 6)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private string GenerateTicketNumber()
        {
            var random = new Random();
            return $"TKT{DateTime.Now:yyyyMMdd}{random.Next(1000, 9999)}";
        }

        private string GenerateSeatNumbers(int count)
        {
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

        private string MaskDocumentNumber(string documentNumber)
        {
            if (string.IsNullOrEmpty(documentNumber) || documentNumber.Length < 4)
                return "****";

            return "****" + documentNumber.Substring(Math.Max(0, documentNumber.Length - 4));
        }

        private string GetGateForFlight(string flightNumber)
        {
            // Демо-логика для выбора выхода
            var random = new Random(flightNumber.GetHashCode());
            var gates = new[] { "A1", "A2", "B1", "B2", "C1", "C2", "D1", "D2" };
            return gates[random.Next(gates.Length)];
        }

        private string GetTerminalForAirport(string airportCode)
        {
            return airportCode switch
            {
                "SVO" => "D",
                "DME" => "A",
                "VKO" => "A",
                "LED" => "1",
                "AER" => "1",
                _ => "1"
            };
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
                <td>{p.LastName} {p.FirstName} {p.MiddleName}</td>
                <td>{p.DateOfBirth:dd.MM.yyyy}</td>
                <td>{GetDocumentTypeName(p.DocumentType)} {p.DocumentNumber}</td>
            </tr>";
            }

            var body = $@"
    <!DOCTYPE html>
    <html>
    <head>
        <meta charset='UTF-8'>
        <style>
            body {{ font-family: 'Arial', sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; color: #333; }}
            .ticket {{ border: 2px solid #0379D9; border-radius: 12px; padding: 20px; background: #f8fafc; }}
            .header {{ background: linear-gradient(135deg, #0379D9, #40B624); color: white; padding: 20px; border-radius: 12px 12px 0 0; margin: -20px -20px 20px -20px; }}
            .header h2 {{ margin: 0; font-size: 24px; }}
            .airline {{ font-size: 24px; font-weight: bold; text-align: center; margin: 20px 0; color: #0379D9; }}
            .flight {{ font-size: 20px; font-weight: bold; text-align: center; color: #334155; margin: 10px 0; }}
            .route {{ display: flex; justify-content: space-between; align-items: center; margin: 30px 0; }}
            .city {{ text-align: center; }}
            .city-name {{ font-size: 18px; font-weight: bold; }}
            .airport {{ color: #64748b; }}
            .time {{ font-size: 16px; color: #0379D9; font-weight: bold; margin-top: 5px; }}
            .arrow {{ color: #94a3b8; font-size: 24px; }}
            .info {{ display: grid; grid-template-columns: 1fr 1fr; gap: 15px; margin: 20px 0; }}
            .info-item {{ border-bottom: 1px solid #e2e8f0; padding: 10px 0; }}
            .info-item .label {{ color: #64748b; font-size: 12px; }}
            .info-item .value {{ font-size: 16px; font-weight: bold; color: #334155; }}
            table {{ width: 100%; border-collapse: collapse; margin: 20px 0; }}
            th {{ background: #f1f5f9; color: #334155; padding: 10px; text-align: left; }}
            td {{ padding: 10px; border-bottom: 1px solid #e2e8f0; }}
            .price {{ background: #e8f4fe; padding: 15px; border-radius: 8px; text-align: center; margin: 20px 0; }}
            .price .total {{ font-size: 24px; font-weight: bold; color: #0379D9; }}
            .qr {{ text-align: center; margin: 30px 0; }}
            .qr-placeholder {{ width: 150px; height: 150px; background: #f1f5f9; border: 2px dashed #0379D9; border-radius: 12px; margin: 0 auto; display: flex; align-items: center; justify-content: center; color: #0379D9; }}
            .footer {{ text-align: center; margin-top: 30px; color: #94a3b8; font-size: 12px; }}
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
                    ✈
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
                <p class='total'>Итого: {booking.Price * booking.Passengers * (booking.IsRoundTrip ? 2 : 1):N0} {booking.Currency}</p>
            </div>

            <div class='qr'>
                <div class='qr-placeholder'>
                    <i class='fas fa-qrcode fa-4x'></i>
                </div>
                <p style='color: #64748b; margin-top: 10px;'>QR-код для посадки</p>
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

        // Модель ответа для бронирования
        public class FlightBookingResponse
        {
            public bool Success { get; set; }
            public string BookingId { get; set; }
            public string BookingNumber { get; set; }
            public string TicketNumber { get; set; }
            public string BookingReference { get; set; }
            public decimal TotalPrice { get; set; }
            public string Message { get; set; }
        }
    }
}