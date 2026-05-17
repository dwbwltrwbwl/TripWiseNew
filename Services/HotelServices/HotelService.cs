using System.Text.Json;
using TripWise.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json.Serialization;

namespace TripWise.Services
{
    public class HotelService : IHotelService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<HotelService> _logger;

        public HotelService(IHttpClientFactory httpClientFactory,
                   IMemoryCache memoryCache,
                   ILogger<HotelService> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _memoryCache = memoryCache;
            _logger = logger;

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("TripWise/1.0");
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
        }

        public async Task<List<Hotel>> SearchHotelsAsync(HotelSearchRequest request)
        {
            try
            {
                // Если указан город, используем поиск по границам
                if (!string.IsNullOrWhiteSpace(request.City))
                {
                    _logger.LogInformation("Поиск отелей в городе: {City}", request.City);
                    return await SearchHotelsByCityBoundaryAsync(request);
                }

                // Если указаны координаты, используем поиск по радиусу
                if (request.Latitude.HasValue && request.Longitude.HasValue)
                {
                    _logger.LogInformation("Поиск отелей по координатам: {Lat}, {Lon}, радиус: {Radius}",
                        request.Latitude, request.Longitude, request.Radius);
                    return await SearchOSMHotelsAsync(request);
                }

                return new List<Hotel>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в HotelService.SearchHotelsAsync");
                return new List<Hotel>();
            }
        }

        private async Task<CityCoordinates> GetCityCoordinatesAsync(string city)
        {
            // Fallback координаты для популярных городов
            var fallbackCoords = new Dictionary<string, (double lat, double lon)>(StringComparer.OrdinalIgnoreCase)
            {
                ["Москва"] = (55.7558, 37.6173),
                ["Санкт-Петербург"] = (59.9343, 30.3351),
                ["Сочи"] = (43.5855, 39.7231),
                ["Казань"] = (55.7887, 49.1221),
                ["Екатеринбург"] = (56.8389, 60.6057),
                ["Новосибирск"] = (55.0084, 82.9357),
                ["Калининград"] = (54.7065, 20.5110),
                ["Владивосток"] = (43.1155, 131.8855),
                ["Краснодар"] = (45.0355, 38.9753),
                ["Нижний Новгород"] = (56.2965, 43.9361),
                ["Самара"] = (53.1959, 50.1002),
                ["Ростов-на-Дону"] = (47.2221, 39.7203),
                ["Уфа"] = (54.7355, 55.9919),
                ["Красноярск"] = (56.0153, 92.8932),
                ["Пермь"] = (58.0105, 56.2294),
                ["Воронеж"] = (51.6608, 39.2003),
                ["Волгоград"] = (48.7080, 44.5133),
                // Международные города
                ["Рим"] = (41.9028, 12.4964),
                ["Берлин"] = (52.5200, 13.4050),
                ["Париж"] = (48.8566, 2.3522),
                ["Мадрид"] = (40.4168, -3.7038),
                ["Лиссабон"] = (38.7223, -9.1393),
                ["Прага"] = (50.0755, 14.4378),
                ["Стамбул"] = (41.0082, 28.9784),
                ["Афины"] = (37.9838, 23.7275),
                ["Лондон"] = (51.5074, -0.1278),
                ["Амстердам"] = (52.3676, 4.9041),
                ["Вена"] = (48.2082, 16.3738),
                ["Будапешт"] = (47.4979, 19.0402),
                ["Барселона"] = (41.3851, 2.1734),
                ["Милан"] = (45.4642, 9.1900),
                ["Неаполь"] = (40.8518, 14.2681),
                ["Токио"] = (35.6762, 139.6503),
                ["Нью-Йорк"] = (40.7128, -74.0060),
                ["Лос-Анджелес"] = (34.0522, -118.2437),
                ["Дубай"] = (25.2048, 55.2708),
                ["Сингапур"] = (1.3521, 103.8198),
                ["Бангкок"] = (13.7367, 100.5231),
                ["Сеул"] = (37.5665, 126.9780),
                ["Пекин"] = (39.9042, 116.4074),
                ["Шанхай"] = (31.2304, 121.4737)
            };

            try
            {
                var cacheKey = $"city_coords_{city}";

                if (_memoryCache.TryGetValue(cacheKey, out CityCoordinates cachedCoords))
                {
                    return cachedCoords;
                }

                // Проверяем fallback
                if (fallbackCoords.TryGetValue(city, out var fallback))
                {
                    var fallbackResult = new CityCoordinates
                    {
                        Latitude = fallback.lat,
                        Longitude = fallback.lon,
                        DisplayName = city
                    };
                    _memoryCache.Set(cacheKey, fallbackResult, TimeSpan.FromDays(7));
                    return fallbackResult;
                }

                // Если fallback нет, делаем запрос к Nominatim
                var url = $"https://nominatim.openstreetmap.org/search?format=json&q={Uri.EscapeDataString(city)}&limit=1&accept-language=ru&polygon_geojson=0&addressdetails=0";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var results = JsonSerializer.Deserialize<List<NominatimResult>>(json);

                if (results == null || results.Count == 0)
                {
                    return null;
                }

                var result = results[0];
                var coords = new CityCoordinates
                {
                    Latitude = double.Parse(result.Lat, System.Globalization.CultureInfo.InvariantCulture),
                    Longitude = double.Parse(result.Lon, System.Globalization.CultureInfo.InvariantCulture),
                    DisplayName = result.Display_Name
                };

                // Кэшируем на 7 дней
                _memoryCache.Set(cacheKey, coords, TimeSpan.FromDays(7));

                return coords;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при геокодировании города: {City}", city);
                return null;
            }
        }

        private async Task<List<Hotel>> SearchOSMHotelsAsync(HotelSearchRequest request)
        {
            var cacheKey = $"hotels_{request.Latitude}_{request.Longitude}_{request.Radius}_{request.AccommodationType}";

            if (_memoryCache.TryGetValue(cacheKey, out List<Hotel> cachedHotels))
            {
                return cachedHotels;
            }

            // Добавляем повторные попытки
            int maxRetries = 3;
            int attempt = 0;
            OverpassResponse osmData = null;

            while (attempt < maxRetries && osmData == null)
            {
                try
                {
                    // Формируем Overpass QL запрос
                    var query = BuildOverpassQuery(request);

                    var url = "https://overpass-api.de/api/interpreter";

                    var content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("data", query)
                    });

                    var response = await _httpClient.PostAsync(url, content);
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync();
                    osmData = JsonSerializer.Deserialize<OverpassResponse>(json);
                }
                catch (Exception ex)
                {
                    attempt++;
                    _logger.LogWarning(ex, "Попытка {Attempt} из {MaxRetries} не удалась", attempt, maxRetries);
                    if (attempt >= maxRetries)
                    {
                        throw;
                    }
                    await Task.Delay(1000 * attempt);
                }
            }

            if (osmData == null)
            {
                return new List<Hotel>();
            }

            var hotels = ProcessOSMResponse(osmData, request.Latitude.Value, request.Longitude.Value, request.MinStars);

            // Кэшируем на 30 минут
            _memoryCache.Set(cacheKey, hotels, TimeSpan.FromMinutes(30));

            return hotels;
        }

        private string BuildOverpassQuery(HotelSearchRequest request)
        {
            var radius = request.Radius;
            var lat = request.Latitude.Value;
            var lon = request.Longitude.Value;
            var type = request.AccommodationType;

            string tourismFilters;

            if (type == "all")
            {
                tourismFilters = @"
                    (
                        node[""tourism""=""hotel""](around:{radius},{lat},{lon});
                        node[""tourism""=""hostel""](around:{radius},{lat},{lon});
                        node[""tourism""=""guest_house""](around:{radius},{lat},{lon});
                        node[""tourism""=""apartment""](around:{radius},{lat},{lon});
                        node[""tourism""=""motel""](around:{radius},{lat},{lon});
                        node[""tourism""=""camp_site""](around:{radius},{lat},{lon});
                        node[""building""=""hotel""](around:{radius},{lat},{lon});
                        way[""tourism""=""hotel""](around:{radius},{lat},{lon});
                        way[""building""=""hotel""](around:{radius},{lat},{lon});
                        relation[""tourism""=""hotel""](around:{radius},{lat},{lon});
                    );
                ";
            }
            else
            {
                tourismFilters = $@"
                    (
                        node[""tourism""=""{type}""](around:{radius},{lat},{lon});
                        way[""tourism""=""{type}""](around:{radius},{lat},{lon});
                    );
                ";
            }

            return $@"
                [out:json][timeout:60];
                (
                    {tourismFilters}
                );
                out body;
                >;
                out skel qt;
            ".Replace("{radius}", radius.ToString())
             .Replace("{lat}", lat.ToString(System.Globalization.CultureInfo.InvariantCulture))
             .Replace("{lon}", lon.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private List<Hotel> ProcessOSMResponse(OverpassResponse osmData, double centerLat, double centerLon, int? minStars)
        {
            var hotels = new List<Hotel>();

            if (osmData?.Elements == null)
            {
                return hotels;
            }

            foreach (var element in osmData.Elements)
            {
                if (element.Tags == null || string.IsNullOrEmpty(element.Tags.GetValueOrDefault("name")))
                {
                    continue;
                }

                var hotel = new Hotel
                {
                    Id = element.Id.ToString(),
                    Name = element.Tags["name"],
                    Latitude = element.Lat,
                    Longitude = element.Lon,
                    Tags = element.Tags,
                    OSMUrl = $"https://www.openstreetmap.org/node/{element.Id}",
                    Distance = CalculateDistance(centerLat, centerLon, element.Lat, element.Lon)
                };

                // Извлекаем адрес
                hotel.Address = BuildAddress(element.Tags);

                // Извлекаем телефон и сайт
                hotel.Phone = element.Tags.GetValueOrDefault("phone")
                           ?? element.Tags.GetValueOrDefault("contact:phone");
                hotel.Website = element.Tags.GetValueOrDefault("website")
                              ?? element.Tags.GetValueOrDefault("contact:website");

                // Определяем тип жилья
                hotel.AccommodationType = GetAccommodationType(element.Tags);

                // Определяем количество звезд
                if (int.TryParse(element.Tags.GetValueOrDefault("stars"), out int stars))
                {
                    hotel.Stars = stars;
                }

                hotels.Add(hotel);
            }

            // Сортируем по расстоянию
            hotels = hotels.OrderBy(h => h.Distance).ToList();

            // Фильтруем по минимальному количеству звезд
            if (minStars.HasValue)
            {
                hotels = hotels.Where(h => h.Stars >= minStars.Value).ToList();
            }

            return hotels;
        }

        private string BuildAddress(Dictionary<string, string> tags)
        {
            var addressParts = new List<string>();

            if (tags.TryGetValue("addr:street", out var street))
            {
                if (tags.TryGetValue("addr:housenumber", out var houseNumber))
                {
                    addressParts.Add($"{street} {houseNumber}");
                }
                else
                {
                    addressParts.Add(street);
                }
            }

            if (tags.TryGetValue("addr:city", out var city))
            {
                addressParts.Add(city);
            }

            return addressParts.Count > 0 ? string.Join(", ", addressParts) : "Адрес не указан";
        }
        private async Task<List<Hotel>> SearchHotelsByCityBoundaryAsync(HotelSearchRequest request)
        {
            var cacheKey = $"city_boundary_hotels_{request.City}_{request.AccommodationType}";

            if (_memoryCache.TryGetValue(cacheKey, out List<Hotel> cachedHotels))
            {
                _logger.LogInformation("Возвращаем из кэша: {CacheKey}", cacheKey);
                return cachedHotels;
            }

            try
            {
                // Получаем границы города через Nominatim с полигоном
                var boundaryUrl = $"https://nominatim.openstreetmap.org/search?format=json&q={Uri.EscapeDataString(request.City)}&limit=1&accept-language=ru&polygon_geojson=1&addressdetails=0&featuretype=city";

                var response = await _httpClient.GetAsync(boundaryUrl);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Не удалось получить границы города {City}", request.City);
                    return new List<Hotel>();
                }

                var json = await response.Content.ReadAsStringAsync();
                var results = JsonSerializer.Deserialize<List<NominatimBoundaryResult>>(json);

                if (results == null || results.Count == 0)
                {
                    _logger.LogWarning("Город {City} не найден в Nominatim", request.City);
                    return new List<Hotel>();
                }

                var cityData = results[0];

                // Преобразуем строковые координаты в double
                double centerLat = 0;
                double centerLon = 0;

                if (!string.IsNullOrEmpty(cityData.Lat) && !string.IsNullOrEmpty(cityData.Lon))
                {
                    centerLat = double.Parse(cityData.Lat, System.Globalization.CultureInfo.InvariantCulture);
                    centerLon = double.Parse(cityData.Lon, System.Globalization.CultureInfo.InvariantCulture);
                }

                // Если нет полигона, используем обычный поиск по радиусу
                if (string.IsNullOrEmpty(cityData.Geojson))
                {
                    _logger.LogInformation("Нет полигона для города {City}, используем поиск по радиусу", request.City);
                    request.Latitude = centerLat;
                    request.Longitude = centerLon;
                    return await SearchOSMHotelsAsync(request);
                }

                // Парсим GeoJSON для получения координат полигона
                var polygon = ParseGeoJsonToPolygon(cityData.Geojson);
                if (string.IsNullOrEmpty(polygon))
                {
                    _logger.LogWarning("Не удалось распарсить полигон для города {City}", request.City);
                    request.Latitude = centerLat;
                    request.Longitude = centerLon;
                    return await SearchOSMHotelsAsync(request);
                }

                // Формируем Overpass QL запрос для поиска внутри полигона
                var type = request.AccommodationType;
                string tourismFilters;

                if (type == "all")
                {
                    tourismFilters = @"
                (
                    node[""tourism""=""hotel""](poly:""" + polygon + @""");
                    node[""tourism""=""hostel""](poly:""" + polygon + @""");
                    node[""tourism""=""guest_house""](poly:""" + polygon + @""");
                    node[""tourism""=""apartment""](poly:""" + polygon + @""");
                    node[""tourism""=""motel""](poly:""" + polygon + @""");
                    node[""tourism""=""camp_site""](poly:""" + polygon + @""");
                    node[""building""=""hotel""](poly:""" + polygon + @""");
                    way[""tourism""=""hotel""](poly:""" + polygon + @""");
                    way[""building""=""hotel""](poly:""" + polygon + @""");
                    relation[""tourism""=""hotel""](poly:""" + polygon + @""");
                );
            ";
                }
                else
                {
                    tourismFilters = $@"
                (
                    node[""tourism""=""{type}""](poly:""{polygon}"");
                    way[""tourism""=""{type}""](poly:""{polygon}"");
                );
            ";
                }

                var query = $@"
            [out:json][timeout:120];
            (
                {tourismFilters}
            );
            out body;
            >;
            out skel qt;
        ";

                _logger.LogInformation("Выполняем запрос к Overpass API для города {City}", request.City);

                // Добавляем повторные попытки
                int maxRetries = 3;
                int attempt = 0;
                OverpassResponse osmData = null;

                while (attempt < maxRetries && osmData == null)
                {
                    try
                    {
                        var content = new FormUrlEncodedContent(new[]
                        {
                    new KeyValuePair<string, string>("data", query)
                });

                        var overpassResponse = await _httpClient.PostAsync("https://overpass-api.de/api/interpreter", content);
                        overpassResponse.EnsureSuccessStatusCode();

                        var overpassJson = await overpassResponse.Content.ReadAsStringAsync();
                        osmData = JsonSerializer.Deserialize<OverpassResponse>(overpassJson);
                    }
                    catch (Exception ex)
                    {
                        attempt++;
                        _logger.LogWarning(ex, "Попытка {Attempt} из {MaxRetries} не удалась для города {City}", attempt, maxRetries, request.City);
                        if (attempt >= maxRetries)
                        {
                            throw;
                        }
                        await Task.Delay(2000 * attempt);
                    }
                }

                if (osmData?.Elements == null || osmData.Elements.Count == 0)
                {
                    _logger.LogWarning("Не найдено отелей в городе {City}", request.City);
                    return new List<Hotel>();
                }

                // Используем преобразованные координаты центра
                var hotels = ProcessOSMResponse(osmData, centerLat, centerLon, request.MinStars);

                // Кэшируем на 1 час
                _memoryCache.Set(cacheKey, hotels, TimeSpan.FromHours(1));

                _logger.LogInformation("Найдено {Count} отелей в городе {City}", hotels.Count, request.City);

                return hotels;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске отелей по границам города {City}", request.City);
                // В случае ошибки пробуем обычный поиск по радиусу
                return await SearchOSMHotelsAsync(request);
            }
        }

        private string ParseGeoJsonToPolygon(string geojson)
        {
            try
            {
                var doc = JsonDocument.Parse(geojson);
                var root = doc.RootElement;

                // Проверяем тип GeometryCollection или Polygon
                if (root.TryGetProperty("type", out var typeElement))
                {
                    var type = typeElement.GetString();

                    if (type == "Polygon")
                    {
                        // Прямое извлечение координат полигона
                        var coordinates = root.GetProperty("coordinates");
                        return ExtractPolygonCoordinates(coordinates);
                    }
                    else if (type == "GeometryCollection")
                    {
                        // Ищем полигон в коллекции
                        var geometries = root.GetProperty("geometries");
                        foreach (var geometry in geometries.EnumerateArray())
                        {
                            if (geometry.GetProperty("type").GetString() == "Polygon")
                            {
                                var coordinates = geometry.GetProperty("coordinates");
                                return ExtractPolygonCoordinates(coordinates);
                            }
                        }
                    }
                    else if (type == "MultiPolygon")
                    {
                        // Берем первый полигон из мультиполигона
                        var coordinates = root.GetProperty("coordinates");
                        if (coordinates.ValueKind == JsonValueKind.Array && coordinates.GetArrayLength() > 0)
                        {
                            var firstPolygon = coordinates[0];
                            if (firstPolygon.ValueKind == JsonValueKind.Array && firstPolygon.GetArrayLength() > 0)
                            {
                                var outerRing = firstPolygon[0];
                                return ExtractPolygonCoordinates(outerRing);
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка парсинга GeoJSON");
                return null;
            }
        }

        private string ExtractPolygonCoordinates(JsonElement coordinates)
        {
            var points = new List<string>();

            foreach (var ring in coordinates.EnumerateArray())
            {
                foreach (var point in ring.EnumerateArray())
                {
                    if (point.ValueKind == JsonValueKind.Array && point.GetArrayLength() >= 2)
                    {
                        var lon = point[0].GetDouble();
                        var lat = point[1].GetDouble();
                        points.Add($"{lat} {lon}");
                    }
                }
                // Берем только первый внешний контур
                break;
            }

            if (points.Count > 0)
            {
                // Добавляем первую точку в конец для замыкания полигона
                points.Add(points[0]);
                return string.Join(" ", points);
            }

            return null;
        }
        private string GetAccommodationType(Dictionary<string, string> tags)
        {
            if (tags.TryGetValue("tourism", out var tourismType))
            {
                return tourismType switch
                {
                    "hotel" => "Отель",
                    "hostel" => "Хостел",
                    "guest_house" => "Гостевой дом",
                    "apartment" => "Апартаменты",
                    "motel" => "Мотель",
                    "camp_site" => "Кемпинг",
                    _ => "Другое"
                };
            }

            if (tags.TryGetValue("building", out var buildingType) && buildingType == "hotel")
            {
                return "Отель";
            }

            return "Другое";
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000; // Радиус Земли в метрах

            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }
    }
}
public class NominatimBoundaryResult
{
    [JsonPropertyName("lat")]
    public string Lat { get; set; }

    [JsonPropertyName("lon")]
    public string Lon { get; set; }

    [JsonPropertyName("display_name")]
    public string Display_Name { get; set; }

    [JsonPropertyName("geojson")]
    public string Geojson { get; set; }
}