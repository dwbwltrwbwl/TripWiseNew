using Microsoft.AspNetCore.Mvc;
using TripWise.Models;
using TripWise.Services;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace TripWise.Controllers
{
    [ApiController]
    [Route("api/hotels")]
    public class HotelsController : ControllerBase
    {
        private readonly IHotelService _hotelService;
        private readonly ILogger<HotelsController> _logger;
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _memoryCache;

        public HotelsController(
            IHotelService hotelService,
            ILogger<HotelsController> logger,
            IHttpClientFactory httpClientFactory,
            IMemoryCache memoryCache)
        {
            _hotelService = hotelService;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
            _memoryCache = memoryCache;

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("TripWise/1.0");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        [HttpPost("search")]
        public async Task<ActionResult<HotelSearchResponse>> Search(
            [FromBody] HotelSearchRequest request)
        {
            try
            {
                _logger.LogInformation("Поиск отелей OSM: Город={City}, Радиус={Radius}м",
                    request?.City, request?.Radius);

                // Валидация запроса
                if (request == null)
                {
                    return BadRequest(new HotelSearchResponse
                    {
                        Success = false,
                        Error = "Запрос не может быть пустым"
                    });
                }

                // Проверяем координаты или город
                bool hasCoordinates = request.Latitude.HasValue && request.Longitude.HasValue;
                bool hasCity = !string.IsNullOrWhiteSpace(request.City);

                if (!hasCoordinates && !hasCity)
                {
                    return BadRequest(new HotelSearchResponse
                    {
                        Success = false,
                        Error = "Укажите город или координаты для поиска"
                    });
                }

                // Ограничиваем радиус для безопасности
                if (request.Radius > 20000) // максимум 20км
                {
                    request.Radius = 20000;
                }

                // Выполняем поиск
                var hotels = await _hotelService.SearchHotelsAsync(request);

                var response = new HotelSearchResponse
                {
                    Success = true,
                    Hotels = hotels,
                    OSMStats = new OSMStats
                    {
                        TotalFound = hotels.Count,
                        DataTimestamp = DateTime.UtcNow,
                        DataSource = "OpenStreetMap",
                        Attribution = "© OpenStreetMap contributors, данные доступны по лицензии ODbL"
                    }
                };

                return Ok(response);
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "Ошибка HTTP при запросе к OSM API");
                return StatusCode(503, new HotelSearchResponse
                {
                    Success = false,
                    Error = "Сервис OSM временно недоступен. Попробуйте позже."
                });
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Ошибка парсинга JSON от OSM API");
                return StatusCode(500, new HotelSearchResponse
                {
                    Success = false,
                    Error = "Ошибка обработки данных от OSM. Попробуйте другой запрос."
                });
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Таймаут запроса к OSM API");
                return StatusCode(504, new HotelSearchResponse
                {
                    Success = false,
                    Error = "Таймаут запроса. Попробуйте уменьшить радиус поиска."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при поиске отелей");
                return StatusCode(500, new HotelSearchResponse
                {
                    Success = false,
                    Error = $"Внутренняя ошибка сервера: {ex.Message}"
                });
            }
        }

        [HttpGet("nearby")]
        public async Task<ActionResult<HotelSearchResponse>> SearchNearby(
            [FromQuery] double lat,
            [FromQuery] double lon,
            [FromQuery] int radius = 5000,
            [FromQuery] string type = "all")
        {
            var request = new HotelSearchRequest
            {
                Latitude = lat,
                Longitude = lon,
                Radius = radius,
                AccommodationType = type
            };

            return await Search(request);
        }

        [HttpGet("osm/{id}")]
        public async Task<ActionResult> GetOSMDetails(string id)
        {
            try
            {
                var cacheKey = $"osm_details_{id}";

                if (_memoryCache.TryGetValue(cacheKey, out string cachedJson))
                {
                    return Content(cachedJson, "application/json");
                }

                // Прямой запрос к OSM API для получения детальной информации
                var url = $"https://api.openstreetmap.org/api/0.6/node/{id}.json";

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();

                // Кэшируем на 1 день
                _memoryCache.Set(cacheKey, json, TimeSpan.FromDays(1));

                return Content(json, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении деталей OSM для ID: {Id}", id);
                return StatusCode(500, new { error = "Не удалось получить данные из OSM" });
            }
        }

        [HttpGet("city/{cityName}/coordinates")]
        public async Task<ActionResult> GetCityCoordinates(string cityName)
        {
            try
            {
                var url = $"https://nominatim.openstreetmap.org/search?format=json&q={Uri.EscapeDataString(cityName)}&limit=5&accept-language=ru";

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var results = JsonSerializer.Deserialize<List<NominatimResult>>(json);

                if (results == null || results.Count == 0)
                {
                    return NotFound(new { error = $"Город '{cityName}' не найден" });
                }

                return Ok(results.Select(r => new
                {
                    Latitude = double.Parse(r.Lat, System.Globalization.CultureInfo.InvariantCulture),
                    Longitude = double.Parse(r.Lon, System.Globalization.CultureInfo.InvariantCulture),
                    r.Display_Name
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении координат города: {City}", cityName);
                return StatusCode(500, new { error = "Ошибка при получении координат" });
            }
        }
    }
}