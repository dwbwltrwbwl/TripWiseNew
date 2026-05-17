using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TripWise.Models;
using Microsoft.AspNetCore.Http;
using TripWise.Services; // Добавьте этот using

namespace TripWise.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FavoritesController : ControllerBase
    {
        private readonly TripWiseContext _context;
        private readonly ILogger<FavoritesController> _logger;
        private readonly IFavoriteService _favoriteService; // Добавьте это поле

        public FavoritesController(
            TripWiseContext context,
            ILogger<FavoritesController> logger,
            IFavoriteService favoriteService) // Добавьте параметр в конструктор
        {
            _context = context;
            _logger = logger;
            _favoriteService = favoriteService; // Инициализируйте поле
        }

        // POST: api/favorites/add
        [HttpPost("add")]
        public async Task<IActionResult> AddFavoriteFlight([FromBody] AddFavoriteRequest request)
        {
            try
            {
                _logger.LogInformation("========== ДОБАВЛЕНИЕ В ИЗБРАННОЕ ==========");
                _logger.LogInformation("Получен запрос на добавление: {@Request}", request);

                var userId = HttpContext.Session.GetInt32("UserId");
                _logger.LogInformation("UserId из сессии: {UserId}", userId);

                if (!userId.HasValue)
                {
                    _logger.LogWarning("Пользователь не авторизован");
                    return Unauthorized(new { success = false, message = "Требуется авторизация" });
                }

                _logger.LogInformation("Добавление рейса в избранное. UserId: {UserId}, FlightId: {FlightId}",
                    userId.Value, request?.FlightId);

                if (request == null)
                {
                    _logger.LogWarning("Request is null");
                    return BadRequest(new { success = false, message = "Запрос не может быть пустым" });
                }

                if (string.IsNullOrEmpty(request.FlightId))
                {
                    _logger.LogWarning("FlightId is null or empty");
                    return BadRequest(new { success = false, message = "FlightId не может быть пустым" });
                }

                // Используем сервис вместо прямого обращения к контексту
                var favorite = new FavoriteFlight
                {
                    UserId = userId.Value,
                    FlightId = request.FlightId,
                    Airline = request.Airline ?? "Авиакомпания",
                    AirlineCode = request.AirlineCode ?? "",
                    FlightNumber = request.FlightNumber ?? "",
                    DepartureCity = request.DepartureCity ?? "",
                    ArrivalCity = request.ArrivalCity ?? "",
                    DepartureAirport = request.DepartureAirport ?? "",
                    ArrivalAirport = request.ArrivalAirport ?? "",
                    DepartureTime = request.DepartureTime,
                    ArrivalTime = request.ArrivalTime,
                    Price = request.Price,
                    Currency = request.Currency ?? "RUB",
                    Transfers = request.Transfers,
                    Duration = request.Duration,
                    Aircraft = request.Aircraft ?? "",
                    IsReturn = request.IsReturn,
                    BookingUrl = request.BookingUrl ?? "",
                    AddedDate = DateTime.Now
                };

                var result = await _favoriteService.AddFavoriteFlightAsync(favorite);

                if (result)
                {
                    _logger.LogInformation("Рейс успешно добавлен в избранное. FlightId: {FlightId}", request.FlightId);
                    return Ok(new { success = true, message = "Рейс добавлен в избранное" });
                }
                else
                {
                    return Ok(new { success = false, message = "Рейс уже в избранном" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ОШИБКА при добавлении рейса в избранное");
                return StatusCode(500, new { success = false, message = "Ошибка сервера: " + ex.Message });
            }
        }

        // POST: api/favorites/remove
        [HttpPost("remove")]
        public async Task<IActionResult> RemoveFavoriteFlight([FromBody] RemoveFavoriteRequest request)
        {
            try
            {
                _logger.LogInformation("========== УДАЛЕНИЕ ИЗ ИЗБРАННОГО ==========");
                _logger.LogInformation("Получен запрос на удаление: {@Request}", request);

                var userId = HttpContext.Session.GetInt32("UserId");
                _logger.LogInformation("UserId из сессии: {UserId}", userId);

                if (!userId.HasValue)
                {
                    _logger.LogWarning("Пользователь не авторизован");
                    return Unauthorized(new { success = false, message = "Требуется авторизация" });
                }

                if (request == null || string.IsNullOrEmpty(request.FlightId))
                {
                    _logger.LogWarning("FlightId is null or empty");
                    return BadRequest(new { success = false, message = "FlightId не может быть пустым" });
                }

                var result = await _favoriteService.RemoveFavoriteFlightAsync(userId.Value, request.FlightId);

                if (result)
                {
                    _logger.LogInformation("Рейс успешно удален из избранного");
                    return Ok(new { success = true, message = "Рейс удален из избранного" });
                }
                else
                {
                    return NotFound(new { success = false, message = "Рейс не найден в избранном" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ОШИБКА при удалении рейса из избранного");
                return StatusCode(500, new { success = false, message = "Ошибка сервера: " + ex.Message });
            }
        }

        // GET: api/favorites/check/{flightId}
        [HttpGet("check/{flightId}")]
        public async Task<IActionResult> CheckFavorite(string flightId)
        {
            try
            {
                _logger.LogInformation("========== ПРОВЕРКА ИЗБРАННОГО ==========");
                _logger.LogInformation("FlightId: {FlightId}", flightId);

                var userId = HttpContext.Session.GetInt32("UserId");
                _logger.LogInformation("UserId из сессии: {UserId}", userId);

                if (!userId.HasValue)
                {
                    return Ok(new
                    {
                        success = true,
                        isFavorite = false,
                        isAuthenticated = false,
                        message = "Пользователь не авторизован"
                    });
                }

                if (string.IsNullOrEmpty(flightId))
                {
                    return BadRequest(new { success = false, message = "FlightId не может быть пустым" });
                }

                var isFavorite = await _favoriteService.IsFlightInFavoritesAsync(userId.Value, flightId);

                _logger.LogInformation("Результат проверки: {IsFavorite}", isFavorite);

                return Ok(new
                {
                    success = true,
                    isFavorite,
                    isAuthenticated = true,
                    message = isFavorite ? "Рейс в избранном" : "Рейс не в избранном"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ОШИБКА при проверке избранного");
                return StatusCode(500, new { success = false, message = "Ошибка сервера" });
            }
        }

        // GET: api/favorites/list
        [HttpGet("list")]
        public async Task<IActionResult> GetFavoriteFlights()
        {
            try
            {
                _logger.LogInformation("========== ПОЛУЧЕНИЕ СПИСКА ИЗБРАННОГО ==========");

                var userId = HttpContext.Session.GetInt32("UserId");
                _logger.LogInformation("UserId из сессии: {UserId}", userId);

                if (!userId.HasValue)
                {
                    return Ok(new { success = true, favorites = new List<string>() });
                }

                var favorites = await _favoriteService.GetUserFavoriteFlightsAsync(userId.Value);
                var favoriteIds = favorites.Select(f => f.FlightId).ToList();

                _logger.LogInformation("Найдено избранных рейсов: {Count}", favoriteIds.Count);

                return Ok(new { success = true, favorites = favoriteIds });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ОШИБКА при получении списка избранного");
                return StatusCode(500, new { success = false, message = "Ошибка сервера" });
            }
        }

        // GET: api/favorites/test
        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok(new
            {
                message = "FavoritesController работает",
                timestamp = DateTime.Now,
                routes = new[] {
                    "GET /api/favorites/test",
                    "GET /api/favorites/list",
                    "GET /api/favorites/check/{flightId}",
                    "POST /api/favorites/add",
                    "POST /api/favorites/remove",
                    "POST /api/favorites/train/add",
                    "POST /api/favorites/train/remove",
                    "GET /api/favorites/train/list",
                    "GET /api/favorites/train/check/{trainGroupId}"
                }
            });
        }

        // GET: api/favorites/debug/{userId}
        [HttpGet("debug/{userId}")]
        public async Task<IActionResult> Debug(int userId)
        {
            try
            {
                _logger.LogInformation("=== DEBUG: Прямой запрос к БД для пользователя {UserId} ===", userId);

                var favorites = await _favoriteService.GetUserFavoriteFlightsAsync(userId);

                _logger.LogInformation("DEBUG: Найдено {Count} рейсов", favorites.Count);

                foreach (var flight in favorites)
                {
                    _logger.LogInformation("DEBUG: {FlightId} - {Airline} {FlightNumber}",
                        flight.FlightId, flight.Airline, flight.FlightNumber);
                }

                return Ok(new
                {
                    success = true,
                    count = favorites.Count,
                    flights = favorites.Select(f => new
                    {
                        f.FlightId,
                        f.Airline,
                        f.FlightNumber,
                        f.DepartureCity,
                        f.ArrivalCity,
                        f.Price
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DEBUG ошибка");
                return Ok(new { success = false, error = ex.Message });
            }
        }

        // ==================== МЕТОДЫ ДЛЯ ЖД ====================

        // POST: api/favorites/train/add
        [HttpPost("train/add")]
        public async Task<IActionResult> AddFavoriteTrain([FromBody] AddFavoriteTrainRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                    return Unauthorized(new { success = false, message = "Требуется авторизация" });

                var favorite = new FavoriteTrain
                {
                    UserId = userId.Value,
                    TrainGroupId = request.TrainGroupId,
                    ForwardTrainNumber = request.ForwardTrainNumber,
                    ReturnTrainNumber = request.ReturnTrainNumber,
                    DepartureStation = request.DepartureStation,
                    ArrivalStation = request.ArrivalStation,
                    DepartureStationId = request.DepartureStationId,
                    ArrivalStationId = request.ArrivalStationId,
                    DepartureDateTime = request.DepartureDateTime,
                    ReturnDepartureDateTime = request.ReturnDepartureDateTime,
                    ArrivalDateTime = request.ArrivalDateTime,
                    ReturnArrivalDateTime = request.ReturnArrivalDateTime,
                    Price = request.Price,
                    Currency = request.Currency ?? "RUB",
                    Duration = request.Duration,
                    ReturnDuration = request.ReturnDuration,
                    TrainBrand = request.TrainBrand,
                    Carrier = request.Carrier,
                    IsFirm = request.IsFirm,
                    IsRoundTrip = request.IsRoundTrip,
                    Passengers = request.Passengers,
                    BookingUrl = request.BookingUrl,
                    AddedDate = DateTime.Now
                };

                var result = await _favoriteService.AddFavoriteTrainAsync(favorite);
                return result
                    ? Ok(new { success = true, message = "Поезд добавлен в избранное" })
                    : Ok(new { success = false, message = "Поезд уже в избранном" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении поезда в избранное");
                return StatusCode(500, new { success = false, message = "Ошибка сервера" });
            }
        }

        // POST: api/favorites/train/remove
        [HttpPost("train/remove")]
        public async Task<IActionResult> RemoveFavoriteTrain([FromBody] RemoveFavoriteTrainRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                    return Unauthorized(new { success = false, message = "Требуется авторизация" });

                var result = await _favoriteService.RemoveFavoriteTrainAsync(userId.Value, request.TrainGroupId);
                return result
                    ? Ok(new { success = true, message = "Поезд удален из избранного" })
                    : NotFound(new { success = false, message = "Поезд не найден в избранном" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении поезда из избранного");
                return StatusCode(500, new { success = false, message = "Ошибка сервера" });
            }
        }

        // GET: api/favorites/train/list
        [HttpGet("train/list")]
        public async Task<IActionResult> GetFavoriteTrains()
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                    return Ok(new { success = true, favorites = new List<string>() });

                var favorites = await _favoriteService.GetUserFavoriteTrainsAsync(userId.Value);
                var favoriteIds = favorites.Select(f => f.TrainGroupId).ToList();
                return Ok(new { success = true, favorites = favoriteIds });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка избранных поездов");
                return StatusCode(500, new { success = false, message = "Ошибка сервера" });
            }
        }

        // GET: api/favorites/train/check/{trainGroupId}
        [HttpGet("train/check/{trainGroupId}")]
        public async Task<IActionResult> CheckFavoriteTrain(string trainGroupId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                    return Ok(new { success = true, isFavorite = false, isAuthenticated = false });

                var isFavorite = await _favoriteService.IsTrainInFavoritesAsync(userId.Value, trainGroupId);
                return Ok(new { success = true, isFavorite, isAuthenticated = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке поезда в избранном");
                return StatusCode(500, new { success = false, message = "Ошибка сервера" });
            }
        }

        // POST: api/favorites/hotel/add
        [HttpPost("hotel/add")]
        public async Task<IActionResult> AddFavoriteHotel([FromBody] AddFavoriteHotelRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                    return Unauthorized(new { success = false, message = "Требуется авторизация" });

                var favorite = new FavoriteHotel
                {
                    UserId = userId.Value,
                    HotelId = request.HotelId,
                    HotelName = request.HotelName,
                    HotelAddress = request.HotelAddress,
                    Latitude = request.Latitude,
                    Longitude = request.Longitude,
                    AccommodationType = request.AccommodationType,
                    Stars = request.Stars,
                    Phone = request.Phone,
                    Website = request.Website,
                    PricePerNight = request.PricePerNight,
                    Currency = request.Currency ?? "RUB",
                    BookingUrl = request.BookingUrl,
                    TagsJson = request.Tags != null ? JsonSerializer.Serialize(request.Tags) : null,
                    AddedDate = DateTime.Now
                };

                var result = await _favoriteService.AddFavoriteHotelAsync(favorite);
                return result
                    ? Ok(new { success = true, message = "Отель добавлен в избранное" })
                    : Ok(new { success = false, message = "Отель уже в избранном" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении отеля в избранное");
                return StatusCode(500, new { success = false, message = "Ошибка сервера" });
            }
        }

        // POST: api/favorites/hotel/remove
        [HttpPost("hotel/remove")]
        public async Task<IActionResult> RemoveFavoriteHotel([FromBody] RemoveFavoriteHotelRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                    return Unauthorized(new { success = false, message = "Требуется авторизация" });

                var result = await _favoriteService.RemoveFavoriteHotelAsync(userId.Value, request.HotelId);
                return result
                    ? Ok(new { success = true, message = "Отель удален из избранного" })
                    : NotFound(new { success = false, message = "Отель не найден в избранном" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении отеля из избранного");
                return StatusCode(500, new { success = false, message = "Ошибка сервера" });
            }
        }

        // GET: api/favorites/hotel/list
        [HttpGet("hotel/list")]
        public async Task<IActionResult> GetFavoriteHotels()
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                    return Ok(new { success = true, favorites = new List<string>() });

                var favorites = await _favoriteService.GetUserFavoriteHotelsAsync(userId.Value);
                var favoriteIds = favorites.Select(f => f.HotelId).ToList();
                return Ok(new { success = true, favorites = favoriteIds });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка избранных отелей");
                return StatusCode(500, new { success = false, message = "Ошибка сервера" });
            }
        }

        // GET: api/favorites/hotel/check/{hotelId}
        [HttpGet("hotel/check/{hotelId}")]
        public async Task<IActionResult> CheckFavoriteHotel(string hotelId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                    return Ok(new { success = true, isFavorite = false, isAuthenticated = false });

                var isFavorite = await _favoriteService.IsHotelInFavoritesAsync(userId.Value, hotelId);
                return Ok(new { success = true, isFavorite, isAuthenticated = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке отеля в избранном");
                return StatusCode(500, new { success = false, message = "Ошибка сервера" });
            }
        }
    }

    public class AddFavoriteRequest
    {
        public string FlightId { get; set; }
        public string Airline { get; set; }
        public string AirlineCode { get; set; }
        public string FlightNumber { get; set; }
        public string DepartureCity { get; set; }
        public string ArrivalCity { get; set; }
        public string DepartureAirport { get; set; }
        public string ArrivalAirport { get; set; }
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; }
        public int Transfers { get; set; }
        public int Duration { get; set; }
        public string Aircraft { get; set; }
        public bool IsReturn { get; set; }
        public string BookingUrl { get; set; }
    }

    public class RemoveFavoriteRequest
    {
        public string FlightId { get; set; }
    }

    public class AddFavoriteTrainRequest
    {
        public string TrainGroupId { get; set; } = string.Empty;
        public string ForwardTrainNumber { get; set; } = string.Empty;
        public string? ReturnTrainNumber { get; set; }
        public string DepartureStation { get; set; } = string.Empty;
        public string ArrivalStation { get; set; } = string.Empty;
        public string? DepartureStationId { get; set; }
        public string? ArrivalStationId { get; set; }
        public DateTime DepartureDateTime { get; set; }
        public DateTime? ReturnDepartureDateTime { get; set; }
        public DateTime ArrivalDateTime { get; set; }
        public DateTime? ReturnArrivalDateTime { get; set; }
        public decimal Price { get; set; }
        public string? Currency { get; set; }
        public int Duration { get; set; }
        public int? ReturnDuration { get; set; }
        public string? TrainBrand { get; set; }
        public string? Carrier { get; set; }
        public bool IsFirm { get; set; }
        public bool IsRoundTrip { get; set; }
        public int Passengers { get; set; } = 1;
        public string? BookingUrl { get; set; }
    }

    public class RemoveFavoriteTrainRequest
    {
        public string TrainGroupId { get; set; } = string.Empty;
    }

    public class AddFavoriteHotelRequest
    {
        public string HotelId { get; set; } = string.Empty;
        public string HotelName { get; set; } = string.Empty;
        public string? HotelAddress { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? AccommodationType { get; set; }
        public int? Stars { get; set; }
        public string? Phone { get; set; }
        public string? Website { get; set; }
        public decimal? PricePerNight { get; set; }
        public string? Currency { get; set; }
        public string? BookingUrl { get; set; }
        public Dictionary<string, string>? Tags { get; set; }
    }

    public class RemoveFavoriteHotelRequest
    {
        public string HotelId { get; set; } = string.Empty;
    }
}