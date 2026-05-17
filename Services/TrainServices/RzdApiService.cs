using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Caching.Memory;
using TripWise.Models;

namespace TripWise.Services
{
    public class RzdApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<RzdApiService> _logger;
        private readonly IMemoryCache _cache;

        public RzdApiService(HttpClient httpClient, ILogger<RzdApiService> logger, IMemoryCache memoryCache)
        {
            _httpClient = httpClient;
            _logger = logger;
            _cache = memoryCache;

            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://rasp.rzd.ru/");
            _httpClient.DefaultRequestHeaders.Add("Origin", "https://rasp.rzd.ru");

            // Устанавливаем таймаут
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
        }

        public async Task<List<TrainSearchResponse>> SearchTrains(TrainSearchRequest request)
        {
            // Создаем ключ кэша
            var cacheKey = $"trains_{request.DepartureStationId}_{request.ArrivalStationId}_{request.DepartureDate}_{request.ReturnDate}_{request.Passengers}_{request.IsReturn}";

            // Пытаемся получить из кэша (кэш на 10 минут)
            if (_cache.TryGetValue(cacheKey, out List<TrainSearchResponse> cachedResult))
            {
                _logger.LogInformation("Возвращаем результат из кэша для ключа: {CacheKey}", cacheKey);
                return cachedResult;
            }

            try
            {
                _logger.LogInformation($"Поиск поездов: {request.DepartureStationId} -> {request.ArrivalStationId}");
                _logger.LogInformation($"Дата: {request.DepartureDate}, IsReturn: {request.IsReturn}");

                var rzdRequest = new RzdApiRequest
                {
                    Code0 = request.DepartureStationId,
                    Code1 = request.ArrivalStationId,
                    Dt0 = FormatDateForRzd(request.DepartureDate),
                    Dir = 0,
                    Tfl = 3,
                    CheckSeats = 1
                };

                // Первый запрос с таймаутом
                var firstResponse = await MakeFirstRequestWithTimeout(rzdRequest, 10);

                if (firstResponse?.Result == "RID" && !string.IsNullOrEmpty(firstResponse.GetRid()))
                {
                    _logger.LogInformation($"Получен RID: {firstResponse.GetRid()}");

                    // Второй запрос с таймаутом
                    var trains = await MakeSecondRequestWithTimeout(firstResponse.GetRid(), 15);

                    if (trains != null && trains.Count > 0)
                    {
                        var result = MapToTrainResponse(trains, request);

                        // Сохраняем в кэш на 10 минут
                        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(10));

                        return result;
                    }
                }

                _logger.LogWarning("Не удалось получить данные от API РЖД");
                return new List<TrainSearchResponse>(); // Возвращаем пустой список, а не тестовые данные
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске поездов");
                return new List<TrainSearchResponse>(); // Возвращаем пустой список
            }
        }

        private async Task<RzdApiResponse> MakeFirstRequestWithTimeout(RzdApiRequest request, int secondsTimeout)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(secondsTimeout));

                var parameters = new Dictionary<string, string>
                {
                    ["layer_id"] = "5827",
                    ["dir"] = "0",
                    ["tfl"] = "1",
                    ["checkSeats"] = "0",
                    ["code0"] = request.Code0,
                    ["code1"] = request.Code1,
                    ["dt0"] = request.Dt0,
                    ["md"] = "0"
                };

                var queryString = string.Join("&", parameters.Select(x => $"{x.Key}={HttpUtility.UrlEncode(x.Value)}"));
                var url = $"https://pass.rzd.ru/timetable/public/ru?{queryString}";

                _logger.LogDebug($"Запрос к RZD: {url}");

                var response = await _httpClient.GetAsync(url, cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"HTTP ошибка: {response.StatusCode}");
                    return new RzdApiResponse { Result = "ERROR" };
                }

                var content = await response.Content.ReadAsStringAsync(cts.Token);

                return ParseFirstResponse(content);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Первый запрос к API РЖД отменен по таймауту");
                return new RzdApiResponse { Result = "TIMEOUT" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в первом запросе");
                return new RzdApiResponse { Result = "ERROR" };
            }
        }

        private async Task<List<RzdRoute>> MakeSecondRequestWithTimeout(string rid, int secondsTimeout)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(secondsTimeout));

                var url = $"https://pass.rzd.ru/timetable/public/ru?layer_id=5827&rid={rid}";
                _logger.LogDebug($"Второй запрос: {url}");

                // Небольшая задержка перед вторым запросом
                await Task.Delay(1000, cts.Token);

                var response = await _httpClient.GetAsync(url, cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"HTTP ошибка второго запроса: {response.StatusCode}");
                    return new List<RzdRoute>();
                }

                var content = await response.Content.ReadAsStringAsync(cts.Token);

                return ParseSecondResponse(content);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Второй запрос к API РЖД отменен по таймауту");
                return new List<RzdRoute>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка во втором запросе");
                return new List<RzdRoute>();
            }
        }

        private RzdApiResponse ParseFirstResponse(string content)
        {
            try
            {
                using var jsonDoc = JsonDocument.Parse(content);
                var json = jsonDoc.RootElement;

                string result = null;
                string rid = null;

                foreach (var property in json.EnumerateObject())
                {
                    if (property.Name.Equals("result", StringComparison.OrdinalIgnoreCase))
                        result = property.Value.GetString();
                    else if (property.Name.Equals("rid", StringComparison.OrdinalIgnoreCase))
                    {
                        rid = property.Value.ValueKind switch
                        {
                            JsonValueKind.String => property.Value.GetString(),
                            JsonValueKind.Number => property.Value.GetInt64().ToString(),
                            _ => null
                        };
                    }
                }

                return new RzdApiResponse { Result = result, Rid = rid };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка парсинга первого ответа");
                return new RzdApiResponse { Result = "ERROR" };
            }
        }

        private List<RzdRoute> ParseSecondResponse(string content)
        {
            var trains = new List<RzdRoute>();

            try
            {
                using var jsonDoc = JsonDocument.Parse(content);
                var json = jsonDoc.RootElement;

                // Пробуем найти список поездов в разных местах ответа
                if (json.TryGetProperty("tp", out var tpProperty) && tpProperty.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tpItem in tpProperty.EnumerateArray())
                    {
                        if (tpItem.TryGetProperty("list", out var listProperty) && listProperty.ValueKind == JsonValueKind.Array)
                        {
                            trains = JsonSerializer.Deserialize<List<RzdRoute>>(listProperty.GetRawText(), new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            }) ?? new List<RzdRoute>();
                            break;
                        }
                    }
                }

                if (trains.Count == 0 && json.TryGetProperty("lst", out var lstProperty) && lstProperty.ValueKind == JsonValueKind.Array)
                {
                    trains = JsonSerializer.Deserialize<List<RzdRoute>>(lstProperty.GetRawText(), new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new List<RzdRoute>();
                }

                if (trains.Count == 0)
                {
                    foreach (var property in json.EnumerateObject())
                    {
                        if (property.Name.Equals("list", StringComparison.OrdinalIgnoreCase) &&
                            property.Value.ValueKind == JsonValueKind.Array)
                        {
                            trains = JsonSerializer.Deserialize<List<RzdRoute>>(property.Value.GetRawText(), new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            }) ?? new List<RzdRoute>();
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка парсинга второго ответа");
            }

            return trains;
        }

        private List<TrainSearchResponse> GetMockTrains(TrainSearchRequest request)
        {
            _logger.LogInformation("Возвращаем тестовые данные");

            var mockTrains = new List<TrainSearchResponse>();

            var testTrains = new[]
            {
                new { Number = "002Э", Departure = "01:00", Arrival = "21:32", TravelTime = "20:32", Brand = "Фирменный" },
                new { Number = "038С", Departure = "00:32", Arrival = "06:14", TravelTime = "05:42", Brand = "Скоростной" },
                new { Number = "059А", Departure = "03:08", Arrival = "08:30", TravelTime = "05:22", Brand = "Пассажирский" },
                new { Number = "137С", Departure = "00:32", Arrival = "06:14", TravelTime = "05:42", Brand = "Скоростной" },
                new { Number = "099А", Departure = "05:04", Arrival = "13:55", TravelTime = "08:51", Brand = "Скорый" }
            };

            foreach (var testTrain in testTrains)
            {
                mockTrains.Add(new TrainSearchResponse
                {
                    Name = testTrain.Brand,
                    TrainNumber = testTrain.Number,
                    DepartureStation = request.DepartureStationId,
                    ArrivalStation = request.ArrivalStationId,
                    DepartureTime = testTrain.Departure,
                    ArrivalTime = testTrain.Arrival,
                    TravelTime = testTrain.TravelTime,
                    DepartureDate = request.DepartureDate,
                    Firm = testTrain.Brand == "Фирменный",
                    IsReturn = request.IsReturn,
                    Categories = new List<TrainCategory>
                    {
                        new TrainCategory { Type = "plazcard", Price = 1500 },
                        new TrainCategory { Type = "coupe", Price = 3000 },
                        new TrainCategory { Type = "lux", Price = 5000 },
                        new TrainCategory { Type = "sedentary", Price = 1000 }
                    }
                });
            }

            return mockTrains;
        }

        private List<TrainSearchResponse> MapToTrainResponse(List<RzdRoute> routes, TrainSearchRequest request)
        {
            var responses = new List<TrainSearchResponse>();

            foreach (var route in routes)
            {
                try
                {
                    var response = new TrainSearchResponse
                    {
                        Name = route.Brand ?? "Поезд",
                        TrainNumber = route.Number ?? "0000",
                        DepartureStation = request.DepartureStationId,
                        ArrivalStation = request.ArrivalStationId,
                        DepartureTime = route.Time0 ?? "00:00",
                        ArrivalTime = route.Time1 ?? "00:00",
                        TravelTime = route.TimeInWay ?? "00:00",
                        DepartureDate = request.DepartureDate,
                        Firm = (route.Carrier?.Contains("Фирменный") == true) || route.BFirm,
                        IsReturn = request.IsReturn,
                        Categories = new List<TrainCategory>()
                    };

                    if (route.Cars != null && route.Cars.Any())
                    {
                        foreach (var car in route.Cars)
                        {
                            response.Categories.Add(new TrainCategory
                            {
                                Type = MapCarType(car.TypeLoc, car.IType),
                                Price = car.Tariff > 0 ? car.Tariff : GetDefaultPrice(car.TypeLoc, car.IType)
                            });
                        }
                    }

                    // Убираем дубликаты категорий
                    response.Categories = response.Categories
                        .GroupBy(c => c.Type)
                        .Select(g => g.First())
                        .ToList();

                    if (response.Categories.Count == 0)
                    {
                        response.Categories.AddRange(GetDefaultCategories());
                    }

                    responses.Add(response);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Ошибка маппинга маршрута");
                }
            }

            return responses;
        }

        private string MapCarType(string typeLoc, int iType)
        {
            if (string.IsNullOrEmpty(typeLoc))
            {
                return iType switch
                {
                    1 => "plazcard",
                    3 => "sedentary",
                    4 => "coupe",
                    5 => "soft",
                    6 => "lux",
                    _ => "other"
                };
            }

            var lowerType = typeLoc.ToLower();

            if (lowerType.Contains("плацкарт") || lowerType.Contains("плац"))
                return "plazcard";
            if (lowerType.Contains("купе"))
                return "coupe";
            if (lowerType.Contains("сидяч"))
                return "sedentary";
            if (lowerType.Contains("св") || lowerType.Contains("люкс"))
                return "lux";
            if (lowerType.Contains("мягк"))
                return "soft";

            return "other";
        }

        private decimal GetDefaultPrice(string typeLoc, int iType)
        {
            return MapCarType(typeLoc, iType) switch
            {
                "plazcard" => 1500,
                "coupe" => 3000,
                "sedentary" => 1000,
                "lux" => 5000,
                "soft" => 4000,
                _ => 2000
            };
        }

        private List<TrainCategory> GetDefaultCategories()
        {
            return new List<TrainCategory>
            {
                new TrainCategory { Type = "plazcard", Price = 1500 },
                new TrainCategory { Type = "coupe", Price = 3000 },
                new TrainCategory { Type = "sedentary", Price = 1000 },
                new TrainCategory { Type = "lux", Price = 5000 }
            };
        }

        private string FormatDateForRzd(string date)
        {
            if (DateTime.TryParse(date, out DateTime dt))
            {
                return dt.ToString("dd.MM.yyyy");
            }
            return DateTime.Now.AddDays(1).ToString("dd.MM.yyyy");
        }
    }

    // Внутренние модели для работы с RZD API
    public class RzdApiRequest
    {
        public string Code0 { get; set; }
        public string Code1 { get; set; }
        public string Dt0 { get; set; }
        public int Dir { get; set; } = 0;
        public int Tfl { get; set; } = 3;
        public int CheckSeats { get; set; } = 1;
    }

    public class RzdApiResponse
    {
        public string Result { get; set; }
        public string Rid { get; set; }
        public long? RID { get; set; }
        public string Timestamp { get; set; }
        public List<RzdRoute> Lst { get; set; }

        public string GetRid() => Rid ?? RID?.ToString();
    }

    public class RzdRoute
    {
        public string Number { get; set; }
        public string Number2 { get; set; }
        public string Brand { get; set; }
        public string Carrier { get; set; }
        public string Route0 { get; set; }
        public string Route1 { get; set; }
        public string Station0 { get; set; }
        public string Station1 { get; set; }
        public string Date0 { get; set; }
        public string Time0 { get; set; }
        public string Date1 { get; set; }
        public string Time1 { get; set; }
        public string TimeInWay { get; set; }
        public bool BFirm { get; set; }
        public List<RzdCar> Cars { get; set; }
    }

    public class RzdCar
    {
        public string Type { get; set; }
        public string TypeLoc { get; set; }
        public string ServCls { get; set; }
        public int FreeSeats { get; set; }
        public decimal Tariff { get; set; }
        public int IType { get; set; }
    }
}