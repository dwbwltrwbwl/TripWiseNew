// Services/RealisticFlightService.cs
using System.Text.Json;
using TripWise.Models;

namespace TripWise.Services
{
    public interface IFlightService
    {
        Task<FlightSearchResponse> SearchFlightsAsync(FlightSearchRequest request);
        Task<List<City>> SearchCitiesAsync(string query);
        Task<List<City>> GetPopularCitiesAsync();
        Task<RouteInfo> GetRouteInfoAsync(string fromCity, string toCity);
    }

    public class RealisticFlightService : IFlightService
    {
        private readonly ILogger<RealisticFlightService> _logger;
        private readonly IConfiguration _configuration;
        private readonly Random _random = new();

        // База городов (остается без изменений)
        private readonly Dictionary<string, City> _cities = new()
        {
            {
                "MOW", new City
                {
                    Code = "MOW",
                    Name = "Москва",
                    Country = "Россия",
                    CountryCode = "RU",
                    TimeZone = "Europe/Moscow",
                    Airports = new List<Airport>
                    {
                        new() { Iata = "SVO", Name = "Шереметьево", Latitude = 55.9726, Longitude = 37.4146 },
                        new() { Iata = "DME", Name = "Домодедово", Latitude = 55.4146, Longitude = 37.8995 },
                        new() { Iata = "VKO", Name = "Внуково", Latitude = 55.6042, Longitude = 37.2875 }
                    }
                }
            },
            {
                "LED", new City
                {
                    Code = "LED",
                    Name = "Санкт-Петербург",
                    Country = "Россия",
                    CountryCode = "RU",
                    TimeZone = "Europe/Moscow",
                    Airports = new List<Airport>
                    {
                        new() { Iata = "LED", Name = "Пулково", Latitude = 59.8003, Longitude = 30.2625 }
                    }
                }
            },
            {
                "TJM", new City
                {
                    Code = "TJM",
                    Name = "Тюмень",
                    Country = "Россия",
                    CountryCode = "RU",
                    TimeZone = "Asia/Yekaterinburg",
                    Airports = new List<Airport>
                    {
                        new() { Iata = "TJM", Name = "Рощино", Latitude = 57.1896, Longitude = 65.3243 }
                    }
                }
            },
            // Сочи
    {
        "AER", new City
        {
            Code = "AER",
            Name = "Сочи",
            Country = "Россия",
            CountryCode = "RU",
            TimeZone = "Europe/Moscow",
            Airports = new List<Airport>
            {
                new() { Iata = "AER", Name = "Адлер-Сочи", Latitude = 43.4499, Longitude = 39.9566 }
            }
        }
    },
    
    // Казань
    {
        "KZN", new City
        {
            Code = "KZN",
            Name = "Казань",
            Country = "Россия",
            CountryCode = "RU",
            TimeZone = "Europe/Moscow",
            Airports = new List<Airport>
            {
                new() { Iata = "KZN", Name = "Казань", Latitude = 55.6062, Longitude = 49.2787 }
            }
        }
    },
    
    // Екатеринбург
    {
        "SVX", new City
        {
            Code = "SVX",
            Name = "Екатеринбург",
            Country = "Россия",
            CountryCode = "RU",
            TimeZone = "Asia/Yekaterinburg",
            Airports = new List<Airport>
            {
                new() { Iata = "SVX", Name = "Кольцово", Latitude = 56.7431, Longitude = 60.8027 }
            }
        }
    },
    
    // Новосибирск
    {
        "OVB", new City
        {
            Code = "OVB",
            Name = "Новосибирск",
            Country = "Россия",
            CountryCode = "RU",
            TimeZone = "Asia/Novosibirsk",
            Airports = new List<Airport>
            {
                new() { Iata = "OVB", Name = "Толмачево", Latitude = 55.0126, Longitude = 82.6507 }
            }
        }
    },
    
    // Краснодар
    {
        "KRR", new City
        {
            Code = "KRR",
            Name = "Краснодар",
            Country = "Россия",
            CountryCode = "RU",
            TimeZone = "Europe/Moscow",
            Airports = new List<Airport>
            {
                new() { Iata = "KRR", Name = "Краснодар", Latitude = 45.0347, Longitude = 39.1705 }
            }
        }
    },
    
    // Симферополь
    {
        "SIP", new City
        {
            Code = "SIP",
            Name = "Симферополь",
            Country = "Россия",
            CountryCode = "RU",
            TimeZone = "Europe/Moscow",
            Airports = new List<Airport>
            {
                new() { Iata = "SIP", Name = "Симферополь", Latitude = 45.0522, Longitude = 33.9751 }
            }
        }
    },
    
    // Минеральные Воды
    {
        "MRV", new City
        {
            Code = "MRV",
            Name = "Минеральные Воды",
            Country = "Россия",
            CountryCode = "RU",
            TimeZone = "Europe/Moscow",
            Airports = new List<Airport>
            {
                new() { Iata = "MRV", Name = "Минеральные Воды", Latitude = 44.2251, Longitude = 43.0819 }
            }
        }
    },
    
    // Калининград
    {
        "KGD", new City
        {
            Code = "KGD",
            Name = "Калининград",
            Country = "Россия",
            CountryCode = "RU",
            TimeZone = "Europe/Kaliningrad",
            Airports = new List<Airport>
            {
                new() { Iata = "KGD", Name = "Храброво", Latitude = 54.8900, Longitude = 20.5926 }
            }
        }
    },
    
    // Уфа
    {
        "UFA", new City
        {
            Code = "UFA",
            Name = "Уфа",
            Country = "Россия",
            CountryCode = "RU",
            TimeZone = "Asia/Yekaterinburg",
            Airports = new List<Airport>
            {
                new() { Iata = "UFA", Name = "Уфа", Latitude = 54.5575, Longitude = 55.8744 }
            }
        }
    },
    
    // Самара
    {
        "KUF", new City
        {
            Code = "KUF",
            Name = "Самара",
            Country = "Россия",
            CountryCode = "RU",
            TimeZone = "Europe/Moscow",
            Airports = new List<Airport>
            {
                new() { Iata = "KUF", Name = "Курумоч", Latitude = 53.5049, Longitude = 50.1644 }
            }
        }
    },
    
    // Ростов-на-Дону
    {
        "ROV", new City
        {
            Code = "ROV",
            Name = "Ростов-на-Дону",
            Country = "Россия",
            CountryCode = "RU",
            TimeZone = "Europe/Moscow",
            Airports = new List<Airport>
            {
                new() { Iata = "ROV", Name = "Платов", Latitude = 47.4933, Longitude = 39.9248 }
            }
        }
    },
    
    // Нижний Новгород
    {
        "GOJ", new City
        {
            Code = "GOJ",
            Name = "Нижний Новгород",
            Country = "Россия",
            CountryCode = "RU",
            TimeZone = "Europe/Moscow",
            Airports = new List<Airport>
            {
                new() { Iata = "GOJ", Name = "Стригино", Latitude = 56.2302, Longitude = 43.7840 }
            }
        }
    },
    
    // Волгоград
    {
        "VOG", new City
        {
            Code = "VOG",
            Name = "Волгоград",
            Country = "Россия",
            CountryCode = "RU",
            TimeZone = "Europe/Moscow",
            Airports = new List<Airport>
            {
                new() { Iata = "VOG", Name = "Гумрак", Latitude = 48.7825, Longitude = 44.3455 }
            }
        }
    },
    
    // Пермь
    {
        "PEE", new City
        {
            Code = "PEE",
            Name = "Пермь",
            Country = "Россия",
            CountryCode = "RU",
            TimeZone = "Asia/Yekaterinburg",
            Airports = new List<Airport>
            {
                new() { Iata = "PEE", Name = "Большое Савино", Latitude = 57.9145, Longitude = 56.0212 }
            }
        }
    },
    
    // Омск
    {
        "OMS", new City
        {
            Code = "OMS",
            Name = "Омск",
            Country = "Россия",
            CountryCode = "RU",
            TimeZone = "Asia/Omsk",
            Airports = new List<Airport>
            {
                new() { Iata = "OMS", Name = "Омск", Latitude = 54.9670, Longitude = 73.3105 }
            }
        }
    },
    
    // Челябинск
    {
        "CEK", new City
        {
            Code = "CEK",
            Name = "Челябинск",
            Country = "Россия",
            CountryCode = "RU",
            TimeZone = "Asia/Yekaterinburg",
            Airports = new List<Airport>
            {
                new() { Iata = "CEK", Name = "Баландино", Latitude = 55.3058, Longitude = 61.5033 }
            }
        }
    },
    
    // Красноярск
    {
        "KJA", new City
        {
            Code = "KJA",
            Name = "Красноярск",
            Country = "Россия",
            CountryCode = "RU",
            TimeZone = "Asia/Krasnoyarsk",
            Airports = new List<Airport>
            {
                new() { Iata = "KJA", Name = "Емельяново", Latitude = 56.1729, Longitude = 92.4933 }
            }
        }
    },
    
    // Иркутск
    {
        "IKT", new City
        {
            Code = "IKT",
            Name = "Иркутск",
            Country = "Россия",
            CountryCode = "RU",
            TimeZone = "Asia/Irkutsk",
            Airports = new List<Airport>
            {
                new() { Iata = "IKT", Name = "Иркутск", Latitude = 52.2680, Longitude = 104.3890 }
            }
        }
    },
    
    // Хабаровск
    {
        "KHV", new City
        {
            Code = "KHV",
            Name = "Хабаровск",
            Country = "Россия",
            CountryCode = "RU",
            TimeZone = "Asia/Vladivostok",
            Airports = new List<Airport>
            {
                new() { Iata = "KHV", Name = "Хабаровск", Latitude = 48.5280, Longitude = 135.1885 }
            }
        }
    },
    
    // Владивосток
    {
        "VVO", new City
        {
            Code = "VVO",
            Name = "Владивосток",
            Country = "Россия",
            CountryCode = "RU",
            TimeZone = "Asia/Vladivostok",
            Airports = new List<Airport>
            {
                new() { Iata = "VVO", Name = "Кневичи", Latitude = 43.3983, Longitude = 132.1480 }
            }
        }
    }
        };

        // База маршрутов
        // База маршрутов (обновленная)
        private readonly Dictionary<string, RouteInfo> _routes = new()
{
    // Москва - Санкт-Петербург
    {
        "MOW-LED", new RouteInfo
        {
            From = "MOW", To = "LED",
            Distance = 634,
            AverageDuration = 90,
            AveragePrice = 4500,
            CommonAirlines = new List<string> { "SU", "S7", "DP", "FV" }
        }
    },
    // Тюмень - Москва
    {
        "TJM-MOW", new RouteInfo
        {
            From = "TJM", To = "MOW",
            Distance = 1720,
            AverageDuration = 180,
            AveragePrice = 7500,
            CommonAirlines = new List<string> { "SU", "S7", "U6", "DP" }
        }
    },
    // Москва - Сочи
    {
        "MOW-AER", new RouteInfo
        {
            From = "MOW", To = "AER",
            Distance = 1360,
            AverageDuration = 150,
            AveragePrice = 6500,
            CommonAirlines = new List<string> { "SU", "S7", "U6", "DP" }
        }
    },
    // Москва - Казань
    {
        "MOW-KZN", new RouteInfo
        {
            From = "MOW", To = "KZN",
            Distance = 720,
            AverageDuration = 95,
            AveragePrice = 4200,
            CommonAirlines = new List<string> { "SU", "S7", "U6", "DP" }
        }
    },
    // Москва - Екатеринбург
    {
        "MOW-SVX", new RouteInfo
        {
            From = "MOW", To = "SVX",
            Distance = 1420,
            AverageDuration = 165,
            AveragePrice = 5800,
            CommonAirlines = new List<string> { "SU", "S7", "U6", "DP" }
        }
    },
    // Москва - Новосибирск
    {
        "MOW-OVB", new RouteInfo
        {
            From = "MOW", To = "OVB",
            Distance = 2810,
            AverageDuration = 240,
            AveragePrice = 8500,
            CommonAirlines = new List<string> { "SU", "S7", "U6" }
        }
    },
    // Москва - Краснодар
    {
        "MOW-KRR", new RouteInfo
        {
            From = "MOW", To = "KRR",
            Distance = 1080,
            AverageDuration = 135,
            AveragePrice = 5200,
            CommonAirlines = new List<string> { "SU", "S7", "DP" }
        }
    },
    // Москва - Симферополь
    {
        "MOW-SIP", new RouteInfo
        {
            From = "MOW", To = "SIP",
            Distance = 1190,
            AverageDuration = 140,
            AveragePrice = 6200,
            CommonAirlines = new List<string> { "SU", "S7", "DP" }
        }
    },
    // Москва - Калининград
    {
        "MOW-KGD", new RouteInfo
        {
            From = "MOW", To = "KGD",
            Distance = 1085,
            AverageDuration = 135,
            AveragePrice = 5500,
            CommonAirlines = new List<string> { "SU", "S7", "DP" }
        }
    },
    // Санкт-Петербург - Сочи
    {
        "LED-AER", new RouteInfo
        {
            From = "LED", To = "AER",
            Distance = 1860,
            AverageDuration = 195,
            AveragePrice = 7200,
            CommonAirlines = new List<string> { "SU", "S7", "DP" }
        }
    },
    // Санкт-Петербург - Казань
    {
        "LED-KZN", new RouteInfo
        {
            From = "LED", To = "KZN",
            Distance = 1240,
            AverageDuration = 150,
            AveragePrice = 4800,
            CommonAirlines = new List<string> { "SU", "S7", "U6" }
        }
    }
};

        public RealisticFlightService(ILogger<RealisticFlightService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<FlightSearchResponse> SearchFlightsAsync(FlightSearchRequest request)
        {
            try
            {
                _logger.LogInformation("Поиск рейсов: {From} → {To} на {Date}",
                    request.DepartureCity, request.ArrivalCity, request.DepartureDate);

                // Извлекаем коды городов
                var fromCode = ExtractCityCode(request.DepartureCity);
                var toCode = ExtractCityCode(request.ArrivalCity);

                if (string.IsNullOrEmpty(fromCode) || string.IsNullOrEmpty(toCode))
                {
                    return new FlightSearchResponse
                    {
                        Success = false,
                        Error = "Не удалось определить коды городов"
                    };
                }

                // Получаем информацию о маршруте
                var routeKey = $"{fromCode}-{toCode}";
                var routeInfo = await GetRouteInfoAsync(fromCode, toCode);

                var flights = new List<Flight>();

                // Генерируем рейсы ТУДА
                var departureFlights = GenerateFlightsForRoute(
                    fromCode, toCode,
                    request.DepartureDate,
                    request.Passengers,
                    routeInfo,
                    isReturn: false
                );
                flights.AddRange(departureFlights);

                // Генерируем рейсы ОБРАТНО (если указана обратная дата)
                if (request.ReturnDate.HasValue)
                {
                    var returnFlights = GenerateFlightsForRoute(
                        toCode, fromCode,
                        request.ReturnDate.Value,
                        request.Passengers,
                        routeInfo,
                        isReturn: true
                    );
                    flights.AddRange(returnFlights);
                }

                // Генерируем партнерские ссылки
                var partnerLinks = GeneratePartnerLinks(fromCode, toCode, request);

                return new FlightSearchResponse
                {
                    Success = true,
                    Flights = flights,
                    SearchId = Guid.NewGuid().ToString(),
                    Message = $"Найдено {flights.Count} рейсов (демо-данные)",
                    PartnerLinks = partnerLinks
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске рейсов");
                return new FlightSearchResponse
                {
                    Success = false,
                    Error = "Внутренняя ошибка при поиске рейсов"
                };
            }
        }

        private List<Flight> GenerateFlightsForRoute(string fromCode, string toCode,
                                                    DateTime date, int passengers,
                                                    RouteInfo routeInfo, bool isReturn)
        {
            var flights = new List<Flight>();

            // Определяем количество рейсов для генерации
            var flightCount = _random.Next(4, 9);

            // Определяем авиакомпании для этого маршрута
            var airlines = routeInfo?.CommonAirlines ??
                          new List<string> { "SU", "S7", "U6", "DP" };

            for (int i = 0; i < flightCount; i++)
            {
                try
                {
                    // Выбираем случайную авиакомпанию
                    var airlineCode = airlines[_random.Next(airlines.Count)];
                    var airline = GetAirlineInfo(airlineCode);

                    // Генерируем время вылета (с 6:00 до 22:00)
                    var departureHour = _random.Next(6, 22);
                    var departureMinute = _random.Next(0, 60);
                    var departureTime = new DateTime(
                        date.Year, date.Month, date.Day,
                        departureHour, departureMinute, 0
                    );

                    // Рассчитываем время прибытия
                    var duration = routeInfo?.AverageDuration ?? 120;
                    duration += _random.Next(-30, 31); // ±30 минут
                    var arrivalTime = departureTime.AddMinutes(duration);

                    // Генерируем цену
                    var basePrice = routeInfo?.AveragePrice ?? 5000;
                    var priceVariation = (decimal)(_random.NextDouble() * 0.4 - 0.2); // -20% до +20%
                    var price = basePrice * (1 + priceVariation);
                    price = Math.Round(price / 100) * 100;
                    price = price * passengers;

                    // Определяем количество пересадок
                    var transfers = _random.NextDouble() < 0.8 ? 0 : 1;

                    // Выбираем аэропорты
                    var fromCity = GetCityByCode(fromCode);
                    var toCity = GetCityByCode(toCode);
                    var departureAirport = fromCity?.Airports?.FirstOrDefault()?.Iata ?? fromCode;
                    var arrivalAirport = toCity?.Airports?.FirstOrDefault()?.Iata ?? toCode;

                    var flight = new Flight
                    {
                        Id = $"{airlineCode}-{departureTime:yyyyMMddHHmm}-{i}",
                        Airline = airline.Name,
                        AirlineCode = airlineCode,
                        AirlineLogo = airline.LogoUrl,
                        FlightNumber = $"{airlineCode} {_random.Next(100, 9999)}",
                        DepartureCity = fromCity?.Name ?? fromCode,
                        ArrivalCity = toCity?.Name ?? toCode,
                        DepartureAirport = departureAirport,
                        ArrivalAirport = arrivalAirport,
                        DepartureTime = departureTime,
                        ArrivalTime = arrivalTime,
                        Price = price,
                        Currency = "RUB",
                        Transfers = transfers,
                        Duration = duration,
                        Aircraft = GetRandomAircraft(),
                        IsReturn = isReturn,
                        BookingUrl = GenerateBookingUrl(airlineCode, fromCode, toCode, departureTime),
                        Details = new FlightDetails
                        {
                            IsRefundable = _random.NextDouble() < 0.7,
                            IsChangeable = _random.NextDouble() < 0.6,
                            Baggage = transfers == 0 ? "1x23кг" : "1x20кг",
                            HandLuggage = "1x10кг",
                            Meal = departureHour >= 7 && departureHour <= 9 ? "Завтрак" :
                                  departureHour >= 12 && departureHour <= 14 ? "Обед" : "Ужин"
                        }
                    };

                    flights.Add(flight);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при генерации рейса");
                }
            }

            return flights.OrderBy(f => f.DepartureTime).ToList();
        }

        private City GetCityByCode(string code)
        {
            return _cities.ContainsKey(code) ? _cities[code] : null;
        }

        private AirlineInfo GetAirlineInfo(string code)
        {
            var airlines = new Dictionary<string, AirlineInfo>
            {
                { "SU", new AirlineInfo("Аэрофлот", "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c2/Aeroflot_Logo_rus.svg/320px-Aeroflot_Logo_rus.svg.png") },
                { "S7", new AirlineInfo("S7 Airlines", "https://upload.wikimedia.org/wikipedia/commons/thumb/8/8f/S7_new_logo.svg/320px-S7_new_logo.svg.png") },
                { "U6", new AirlineInfo("Ural Airlines", "https://upload.wikimedia.org/wikipedia/commons/thumb/9/92/Ural_Airlines_logo.svg/320px-Ural_Airlines_logo.svg.png") },
                { "DP", new AirlineInfo("Победа", "https://upload.wikimedia.org/wikipedia/commons/thumb/5/58/Pobeda_airlines_logo.svg/320px-Pobeda_airlines_logo.svg.png") },
                { "FV", new AirlineInfo("Россия", "https://upload.wikimedia.org/wikipedia/commons/thumb/a/a2/Rossiya_Airlines_Logo.svg/320px-Rossiya_Airlines_Logo.svg.png") }
            };

            return airlines.ContainsKey(code) ? airlines[code] : new AirlineInfo("Авиакомпания", "");
        }

        private string GenerateBookingUrl(string airlineCode, string fromCode, string toCode, DateTime departureTime)
        {
            var partnerId = _configuration["Aviasales:PartnerId"] ?? "tripwise";

            return $"https://www.aviasales.ru/search?" +
                   $"origin_iata={fromCode}&" +
                   $"destination_iata={toCode}&" +
                   $"depart_date={departureTime:dd.MM.yyyy}&" +
                   $"adults=1&" +
                   $"locale=ru&" +
                   $"currency=rub&" +
                   $"partner={partnerId}";
        }

        private PartnerLinks GeneratePartnerLinks(string fromCode, string toCode, FlightSearchRequest request)
        {
            var partnerId = _configuration["Aviasales:PartnerId"] ?? "tripwise";

            var baseParams = $"origin_iata={fromCode}&" +
                            $"destination_iata={toCode}&" +
                            $"depart_date={request.DepartureDate:dd.MM.yyyy}&" +
                            $"adults={request.Passengers}&" +
                            $"locale=ru&" +
                            $"currency=rub";

            if (request.ReturnDate.HasValue)
            {
                baseParams += $"&return_date={request.ReturnDate.Value:dd.MM.yyyy}";
            }

            return new PartnerLinks
            {
                AviasalesUrl = $"https://www.aviasales.ru/search?{baseParams}&partner={partnerId}",
                YandexTravelUrl = $"https://travel.yandex.ru/avia?{baseParams}",
                TutuUrl = $"https://www.tutu.ru/avia/search.php?{baseParams}",
                SkyscannerUrl = $"https://www.skyscanner.ru/transport/flights/{fromCode}/{toCode}/{request.DepartureDate:yyyyMMdd}/"
            };
        }

        private string GetRandomAircraft()
        {
            var aircrafts = new[]
            {
                "Airbus A320", "Airbus A321", "Boeing 737-800", "Boeing 737-900",
                "Airbus A319", "Sukhoi Superjet 100", "Embraer 170", "Embraer 190"
            };
            return aircrafts[_random.Next(aircrafts.Length)];
        }

        public async Task<List<City>> SearchCitiesAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                return new List<City>();

            var lowerQuery = query.ToLower();

            var results = _cities.Values
                .Where(c => c.Name.ToLower().Contains(lowerQuery) ||
                           c.Code.ToLower().Contains(lowerQuery))
                .Select(c => new City
                {
                    Code = c.Code,
                    Name = c.Name,
                    Country = c.Country,
                    CountryCode = c.CountryCode
                })
                .ToList();

            return await Task.FromResult(results);
        }

        public async Task<List<City>> GetPopularCitiesAsync()
        {
            // Возвращаем больше популярных городов
            var popularCodes = new[] { "MOW", "LED", "AER", "KZN", "SVX", "TJM", "OVB", "KRR", "SIP", "KGD" };

            var cities = popularCodes
                .Where(code => _cities.ContainsKey(code))
                .Select(code => _cities[code])
                .ToList();

            return await Task.FromResult(cities);
        }

        public async Task<RouteInfo> GetRouteInfoAsync(string fromCity, string toCity)
        {
            var routeKey = $"{fromCity}-{toCity}";
            var reverseKey = $"{toCity}-{fromCity}";

            // Проверяем прямой маршрут
            if (_routes.ContainsKey(routeKey))
                return _routes[routeKey];

            // Проверяем обратный маршрут
            if (_routes.ContainsKey(reverseKey))
            {
                var reverse = _routes[reverseKey];
                return new RouteInfo
                {
                    From = fromCity,
                    To = toCity,
                    Distance = reverse.Distance,
                    AverageDuration = reverse.AverageDuration,
                    AveragePrice = reverse.AveragePrice,
                    CommonAirlines = reverse.CommonAirlines
                };
            }

            // Если маршрут не найден, вычисляем примерные данные на основе реальных расстояний
            var from = GetCityByCode(fromCity);
            var to = GetCityByCode(toCity);

            if (from == null || to == null)
            {
                // Если города не найдены, возвращаем базовые данные
                return new RouteInfo
                {
                    From = fromCity,
                    To = toCity,
                    Distance = 800,
                    AverageDuration = 120,
                    AveragePrice = 5000,
                    CommonAirlines = new List<string> { "SU", "S7", "U6" }
                };
            }

            // Рассчитываем примерное расстояние и время
            // Для простоты используем среднюю скорость 800 км/ч
            var distance = CalculateDistance(from, to);
            var duration = (int)(distance / 800.0 * 60); // В минутах
            var price = CalculatePrice(distance);

            return new RouteInfo
            {
                From = fromCity,
                To = toCity,
                Distance = distance,
                AverageDuration = duration,
                AveragePrice = price,
                CommonAirlines = new List<string> { "SU", "S7", "U6", "DP" }
            };
        }

        // Вспомогательные методы
        private int CalculateDistance(City from, City to)
        {
            // Базовое расстояние между крупными городами
            var baseDistances = new Dictionary<string, int>
            {
                // Примерные расстояния между городами
                // Вы можете добавить больше расстояний при необходимости
            };

            var key = $"{from.Code}-{to.Code}";
            if (baseDistances.ContainsKey(key))
                return baseDistances[key];

            // Если расстояние не задано, возвращаем примерное
            return 1000; // Среднее расстояние
        }

        private decimal CalculatePrice(int distance)
        {
            // Примерная формула цены: базово + за км
            return 2000 + (distance * 3);
        }

        private string ExtractCityCode(string cityString)
        {
            if (string.IsNullOrEmpty(cityString))
                return "";

            // Пытаемся извлечь код из скобок
            var match = System.Text.RegularExpressions.Regex.Match(cityString, @"\(([A-Z]{3})\)");
            if (match.Success)
                return match.Groups[1].Value;

            // Пытаемся извлечь код из конца строки (после последнего пробела)
            var parts = cityString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                var lastPart = parts.Last().ToUpper();
                if (lastPart.Length == 3 && _cities.ContainsKey(lastPart))
                    return lastPart;
            }

            // Ищем в базе городов по названию (полному или частичному)
            var city = _cities.Values.FirstOrDefault(c =>
                cityString.Contains(c.Name, StringComparison.OrdinalIgnoreCase));

            if (city != null)
                return city.Code;

            // Ищем по части имени (для случаев когда введено "Моск" вместо "Москва")
            foreach (var c in _cities.Values)
            {
                if (c.Name.StartsWith(cityString, StringComparison.OrdinalIgnoreCase) ||
                    cityString.StartsWith(c.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return c.Code;
                }
            }

            // Если ничего не нашли, пробуем найти по первым буквам
            foreach (var c in _cities.Values)
            {
                if (c.Name.StartsWith(cityString, StringComparison.OrdinalIgnoreCase) ||
                    cityString.Contains(c.Name.Substring(0, Math.Min(3, c.Name.Length)), StringComparison.OrdinalIgnoreCase))
                {
                    return c.Code;
                }
            }

            _logger.LogWarning("Не удалось определить код города для: {CityString}", cityString);
            return "";
        }

        private class AirlineInfo
        {
            public string Name { get; }
            public string LogoUrl { get; }

            public AirlineInfo(string name, string logoUrl)
            {
                Name = name;
                LogoUrl = logoUrl;
            }
        }
    }
}