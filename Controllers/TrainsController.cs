using Microsoft.AspNetCore.Mvc;
using TripWise.Models;
using TripWise.Models.ViewModels;
using TripWise.Services;

namespace TripWise.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TrainsController : Controller
    {
        private readonly RzdApiService _rzdApiService;
        private readonly ILogger<TrainsController> _logger;

        public TrainsController(RzdApiService rzdApiService, ILogger<TrainsController> logger)
        {
            _rzdApiService = rzdApiService;
            _logger = logger;
        }

        // ==================== ОСНОВНОЙ ПОИСК (с fallback на mock) ====================
        [HttpPost("search")]
        public async Task<IActionResult> SearchTrains([FromBody] TrainSearchRequest request)
        {
            try
            {
                _logger.LogInformation("=== ПОИСК ПОЕЗДОВ ===");
                _logger.LogInformation("Запрос: {@Request}", request);

                var allTrainGroups = new List<TrainGroupResponse>();

                // 1. Ищем поезда туда
                var departureRequest = new TrainSearchRequest
                {
                    DepartureStationId = request.DepartureStationId,
                    ArrivalStationId = request.ArrivalStationId,
                    DepartureDate = request.DepartureDate,
                    Passengers = request.Passengers,
                    IsReturn = false,
                    ReturnDate = null
                };

                _logger.LogInformation("Поиск поездов ТУДА...");
                var forwardTrains = await _rzdApiService.SearchTrains(departureRequest);
                _logger.LogInformation("Найдено поездов ТУДА: {Count}", forwardTrains.Count);

                // Если API не вернул данные, используем mock
                if (forwardTrains.Count == 0)
                {
                    _logger.LogWarning("API РЖД не вернул данные, используем тестовые данные");
                    return GetMockResults(request);
                }

                // 2. Ищем поезда обратно (если указана обратная дата)
                List<TrainSearchResponse> returnTrains = new List<TrainSearchResponse>();
                if (!string.IsNullOrEmpty(request.ReturnDate))
                {
                    _logger.LogInformation("Поиск поездов ОБРАТНО...");
                    var returnRequest = new TrainSearchRequest
                    {
                        DepartureStationId = request.ArrivalStationId,
                        ArrivalStationId = request.DepartureStationId,
                        DepartureDate = request.ReturnDate,
                        Passengers = request.Passengers,
                        IsReturn = true,
                        ReturnDate = null
                    };

                    returnTrains = await _rzdApiService.SearchTrains(returnRequest);
                    _logger.LogInformation("Найдено поездов ОБРАТНО: {Count}", returnTrains.Count);
                }

                // 3. Группируем рейсы в комбинированные карточки
                if (!string.IsNullOrEmpty(request.ReturnDate) && forwardTrains.Count > 0 && returnTrains.Count > 0)
                {
                    _logger.LogInformation("Создание комбинированных карточек...");
                    var forwardForCombination = forwardTrains.Take(10).ToList();
                    var returnForCombination = returnTrains.Take(10).ToList();

                    foreach (var forwardTrain in forwardForCombination)
                    {
                        foreach (var returnTrain in returnForCombination)
                        {
                            var forwardMinPrice = forwardTrain.Categories?.Min(c => c.Price) ?? 0;
                            var totalPrice = forwardMinPrice * 2;

                            allTrainGroups.Add(new TrainGroupResponse
                            {
                                Id = $"{forwardTrain.TrainNumber}-{returnTrain.TrainNumber}",
                                ForwardTrain = forwardTrain,
                                ReturnTrain = returnTrain,
                                TotalPrice = totalPrice,
                                IsRoundTrip = true
                            });
                        }
                    }
                }
                else
                {
                    foreach (var train in forwardTrains)
                    {
                        allTrainGroups.Add(new TrainGroupResponse
                        {
                            Id = train.TrainNumber,
                            ForwardTrain = train,
                            ReturnTrain = null,
                            TotalPrice = train.Categories?.Min(c => c.Price) ?? 0,
                            IsRoundTrip = false
                        });
                    }
                }

                _logger.LogInformation("=== ИТОГИ ===");
                _logger.LogInformation("Всего карточек: {TotalCount}", allTrainGroups.Count);

                return Ok(new
                {
                    success = true,
                    trainGroups = allTrainGroups,
                    message = allTrainGroups.Count > 0 ? $"Найдено {allTrainGroups.Count} вариантов" : "Варианты не найдены"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске поездов");
                // В случае ошибки возвращаем тестовые данные
                return GetMockResults(request);
            }
        }

        // ==================== ТЕСТОВЫЕ ДАННЫЕ (MOCK) ====================
        [HttpPost("search-mock")]
        public IActionResult SearchTrainsMock([FromBody] TrainSearchRequest request)
        {
            return GetMockResults(request);
        }

        private IActionResult GetMockResults(TrainSearchRequest request)
        {
            _logger.LogInformation("Поиск поездов (MOCK) для {From} -> {To}", request.DepartureStationId, request.ArrivalStationId);

            var mockTrains = GetMockTrainsData(request.DepartureStationId, request.ArrivalStationId);
            var allTrainGroups = new List<TrainGroupResponse>();

            if (!string.IsNullOrEmpty(request.ReturnDate))
            {
                // Показываем не все комбинации, а разумное количество (до 15)
                var forwardTrains = mockTrains.Take(8).ToList();
                var returnTrains = mockTrains.Take(8).ToList();
                var comboCount = 0;

                foreach (var forwardTrain in forwardTrains)
                {
                    foreach (var returnTrain in returnTrains)
                    {
                        if (comboCount >= 15) break;

                        // Не комбинируем одинаковые номера поездов
                        if (forwardTrain.TrainNumber == returnTrain.TrainNumber) continue;

                        // Цена: минимальная цена туда + минимальная цена обратно
                        var forwardPrice = GetMinPrice(forwardTrain.Categories);
                        var returnPrice = GetMinPrice(returnTrain.Categories);
                        var totalPrice = forwardPrice + returnPrice;

                        allTrainGroups.Add(new TrainGroupResponse
                        {
                            Id = $"{forwardTrain.TrainNumber}-{returnTrain.TrainNumber}",
                            ForwardTrain = forwardTrain,
                            ReturnTrain = returnTrain,
                            TotalPrice = totalPrice,
                            IsRoundTrip = true
                        });
                        comboCount++;
                    }
                    if (comboCount >= 15) break;
                }

                // Если комбинаций мало, добавляем одиночные варианты
                if (allTrainGroups.Count < 3)
                {
                    foreach (var train in forwardTrains.Take(5))
                    {
                        allTrainGroups.Add(new TrainGroupResponse
                        {
                            Id = train.TrainNumber,
                            ForwardTrain = train,
                            ReturnTrain = null,
                            TotalPrice = GetMinPrice(train.Categories),
                            IsRoundTrip = false
                        });
                    }
                }
            }
            else
            {
                // Только туда - показываем все варианты
                foreach (var train in mockTrains)
                {
                    allTrainGroups.Add(new TrainGroupResponse
                    {
                        Id = train.TrainNumber,
                        ForwardTrain = train,
                        ReturnTrain = null,
                        TotalPrice = GetMinPrice(train.Categories),
                        IsRoundTrip = false
                    });
                }
            }

            return Ok(new
            {
                success = true,
                trainGroups = allTrainGroups,
                message = allTrainGroups.Count > 0 ? $"Найдено {allTrainGroups.Count} вариантов" : "Варианты не найдены",
                isMock = true
            });
        }

        private List<TrainSearchResponse> GetMockTrainsData(string departureStationId, string arrivalStationId)
        {
            var trainsBase = new List<TrainSearchResponse>
    {
        // Утренние поезда
        new TrainSearchResponse
        {
            Name = "Ласточка",
            TrainNumber = "701Ч",
            DepartureTime = "07:00",
            ArrivalTime = "11:45",
            TravelTime = "04:45",
            Firm = false,
            Price = 1850,
            Categories = new List<TrainCategory>
            {
                new TrainCategory { Type = "standard", Price = 1850 }
            }
        },
        new TrainSearchResponse
        {
            Name = "Сапсан",
            TrainNumber = "759А",
            DepartureTime = "08:30",
            ArrivalTime = "12:15",
            TravelTime = "03:45",
            Firm = true,
            Price = 3200,
            Categories = new List<TrainCategory>
            {
                new TrainCategory { Type = "standard", Price = 3200 }
            }
        },
        new TrainSearchResponse
        {
            Name = "Экспресс",
            TrainNumber = "043В",
            DepartureTime = "09:15",
            ArrivalTime = "15:30",
            TravelTime = "06:15",
            Firm = true,
            Price = 2800,
            Categories = new List<TrainCategory>
            {
                new TrainCategory { Type = "standard", Price = 2800 }
            }
        },

        // Дневные поезда
        new TrainSearchResponse
        {
            Name = "Дневной экспресс",
            TrainNumber = "115Г",
            DepartureTime = "12:00",
            ArrivalTime = "19:20",
            TravelTime = "07:20",
            Firm = false,
            Price = 1950,
            Categories = new List<TrainCategory>
            {
                new TrainCategory { Type = "standard", Price = 1950 }
            }
        },
        new TrainSearchResponse
        {
            Name = "Стрела",
            TrainNumber = "067Д",
            DepartureTime = "14:30",
            ArrivalTime = "20:45",
            TravelTime = "06:15",
            Firm = false,
            Price = 2250,
            Categories = new List<TrainCategory>
            {
                new TrainCategory { Type = "standard", Price = 2250 }
            }
        },
        new TrainSearchResponse
        {
            Name = "Метеор",
            TrainNumber = "088Е",
            DepartureTime = "16:00",
            ArrivalTime = "22:10",
            TravelTime = "06:10",
            Firm = true,
            Price = 3100,
            Categories = new List<TrainCategory>
            {
                new TrainCategory { Type = "standard", Price = 3100 }
            }
        },

        // Вечерние поезда
        new TrainSearchResponse
        {
            Name = "Вечерний экспресс",
            TrainNumber = "202Ж",
            DepartureTime = "18:20",
            ArrivalTime = "23:55",
            TravelTime = "05:35",
            Firm = false,
            Price = 2100,
            Categories = new List<TrainCategory>
            {
                new TrainCategory { Type = "standard", Price = 2100 }
            }
        },
        new TrainSearchResponse
        {
            Name = "Заря",
            TrainNumber = "234И",
            DepartureTime = "20:00",
            ArrivalTime = "02:15",
            TravelTime = "06:15",
            Firm = true,
            Price = 2900,
            Categories = new List<TrainCategory>
            {
                new TrainCategory { Type = "standard", Price = 2900 }
            }
        },

        // Ночные поезда
        new TrainSearchResponse
        {
            Name = "Ночной экспресс",
            TrainNumber = "301К",
            DepartureTime = "22:30",
            ArrivalTime = "07:45",
            TravelTime = "09:15",
            Firm = true,
            Price = 3500,
            Categories = new List<TrainCategory>
            {
                new TrainCategory { Type = "standard", Price = 3500 }
            }
        },
        new TrainSearchResponse
        {
            Name = "Премиум",
            TrainNumber = "002М",
            DepartureTime = "23:15",
            ArrivalTime = "08:30",
            TravelTime = "09:15",
            Firm = true,
            Price = 4200,
            Categories = new List<TrainCategory>
            {
                new TrainCategory { Type = "standard", Price = 4200 }
            }
        },
        new TrainSearchResponse
        {
            Name = "Комфорт",
            TrainNumber = "452Н",
            DepartureTime = "00:15",
            ArrivalTime = "09:00",
            TravelTime = "08:45",
            Firm = false,
            Price = 2300,
            Categories = new List<TrainCategory>
            {
                new TrainCategory { Type = "standard", Price = 2300 }
            }
        },

        // Скорые поезда
        new TrainSearchResponse
        {
            Name = "Скорый",
            TrainNumber = "041П",
            DepartureTime = "10:45",
            ArrivalTime = "18:30",
            TravelTime = "07:45",
            Firm = false,
            Price = 1750,
            Categories = new List<TrainCategory>
            {
                new TrainCategory { Type = "standard", Price = 1750 }
            }
        },
        new TrainSearchResponse
        {
            Name = "Стандарт",
            TrainNumber = "098Р",
            DepartureTime = "13:20",
            ArrivalTime = "21:00",
            TravelTime = "07:40",
            Firm = false,
            Price = 1900,
            Categories = new List<TrainCategory>
            {
                new TrainCategory { Type = "standard", Price = 1900 }
            }
        }
    };

            // Добавляем даты отправления и прибытия
            foreach (var train in trainsBase)
            {
                train.DepartureStation = departureStationId;
                train.ArrivalStation = arrivalStationId;
                train.IsReturn = false;
            }

            return trainsBase;
        }
     

        private Dictionary<string, string> GetStationNames()
        {
            return new Dictionary<string, string>
            {
                { "2000000", "Москва" },
                { "2006000", "Санкт-Петербург" },
                { "2060001", "Нижний Новгород" },
                { "2060501", "Казань" },
                { "2044000", "Екатеринбург" },
                { "2038000", "Новосибирск" },
                { "2064130", "Сочи" },
                { "2064788", "Краснодар" },
                { "2024000", "Самара" },
                { "2024460", "Уфа" },
                { "2030000", "Красноярск" },
                { "2014000", "Воронеж" },
                { "2060151", "Владивосток" },
                { "2060002", "Калининград" },
                { "2047000", "Тюмень" },
                { "2054000", "Иркутск" },
                { "2060150", "Хабаровск" }
            };
        }

        private decimal GetMinPrice(List<TrainCategory> categories)
        {
            return categories?.Min(c => c.Price) ?? 0;
        }

        // ==================== СТАНЦИИ ====================
        [HttpGet("stations/search")]
        public IActionResult SearchStations([FromQuery] string query)
        {
            if (string.IsNullOrEmpty(query) || query.Length < 2)
            {
                return Ok(new List<object>());
            }

            var allStations = GetAllStationsData();
            var lowerQuery = query.ToLower();

            var results = allStations
                .Where(s => s.Name.ToLower().Contains(lowerQuery) ||
                           s.Region.ToLower().Contains(lowerQuery))
                .Take(10)
                .Select(s => new { id = s.Id, name = s.Name, region = s.Region })
                .ToList();

            return Ok(results);
        }

        [HttpGet("stations")]
        public IActionResult GetAllStations()
        {
            var stations = GetAllStationsData()
                .Select(s => new { id = s.Id, name = s.Name, region = s.Region })
                .ToList();

            return Ok(stations);
        }

        // ==================== БРОНИРОВАНИЕ ====================
        [HttpGet("Book")]
        public IActionResult Book([FromQuery] TrainBookingRequest request)
        {
            Console.WriteLine($"=== ПОЛУЧЕН ЗАПРОС НА БРОНИРОВАНИЕ ===");
            Console.WriteLine($"trainNumber: {request.TrainNumber}");
            Console.WriteLine($"departureDateTime (raw): {request.DepartureDateTime}");
            Console.WriteLine($"arrivalDateTime (raw): {request.ArrivalDateTime}");

            DateTime parsedDepartureDateTime = DateTime.Parse(request.DepartureDateTime);
            DateTime parsedArrivalDateTime = DateTime.Parse(request.ArrivalDateTime);

            var model = new TrainBookingViewModel
            {
                TrainNumber = request.TrainNumber,
                ReturnTrainNumber = request.ReturnTrainNumber,
                DepartureStationId = request.DepartureStationId,
                DepartureStationName = Uri.UnescapeDataString(request.DepartureStationName ?? ""),
                ArrivalStationId = request.ArrivalStationId,
                ArrivalStationName = Uri.UnescapeDataString(request.ArrivalStationName ?? ""),
                DepartureDateTime = parsedDepartureDateTime,
                ArrivalDateTime = parsedArrivalDateTime,
                Price = request.Price,
                Passengers = request.Passengers,
                CarType = request.CarType,
                CarClass = request.CarClass,
                Duration = request.Duration,
                IsRoundTrip = request.IsRoundTrip
            };

            if (!string.IsNullOrEmpty(request.ReturnDepartureDateTime))
            {
                model.ReturnDepartureDateTime = DateTime.Parse(request.ReturnDepartureDateTime);
            }
            if (!string.IsNullOrEmpty(request.ReturnArrivalDateTime))
            {
                model.ReturnArrivalDateTime = DateTime.Parse(request.ReturnArrivalDateTime);
            }
            if (request.ReturnDuration.HasValue)
            {
                model.ReturnDuration = request.ReturnDuration;
            }

            var json = System.Text.Json.JsonSerializer.Serialize(model);
            TempData["TrainBookingModel"] = json;

            return RedirectToAction("Book", "TrainBooking");
        }

        // ==================== ДАННЫЕ СТАНЦИЙ ====================
        private List<Station> GetAllStationsData()
        {
            return new List<Station>
            {
                new Station { Id = "2000000", Name = "Москва", Region = "Москва" },
                new Station { Id = "2006000", Name = "Санкт-Петербург", Region = "Санкт-Петербург" },
                new Station { Id = "2060000", Name = "Нижний Новгород", Region = "Нижегородская обл." },
                new Station { Id = "2060001", Name = "Нижний Новгород (Московский вокзал)", Region = "Нижегородская обл." },
                new Station { Id = "2064000", Name = "Ростов-на-Дону", Region = "Ростовская обл." },
                new Station { Id = "2024000", Name = "Самара", Region = "Самарская обл." },
                new Station { Id = "2024460", Name = "Уфа", Region = "Республика Башкортостан" },
                new Station { Id = "2030000", Name = "Красноярск", Region = "Красноярский край" },
                new Station { Id = "2014000", Name = "Воронеж", Region = "Воронежская обл." },
                new Station { Id = "2044000", Name = "Екатеринбург", Region = "Свердловская обл." },
                new Station { Id = "2038000", Name = "Новосибирск", Region = "Новосибирская обл." },
                new Station { Id = "2060501", Name = "Казань", Region = "Татарстан" },
                new Station { Id = "2064130", Name = "Сочи", Region = "Краснодарский край" },
                new Station { Id = "2064110", Name = "Новороссийск", Region = "Краснодарский край" },
                new Station { Id = "2064788", Name = "Краснодар", Region = "Краснодарский край" },
                new Station { Id = "2064188", Name = "Анапа", Region = "Краснодарский край" },
                new Station { Id = "2078001", Name = "Симферополь", Region = "Крым" },
                new Station { Id = "2064150", Name = "Адлер", Region = "Краснодарский край" },
                new Station { Id = "2060151", Name = "Владивосток", Region = "Приморский край" },
                new Station { Id = "2060150", Name = "Хабаровск", Region = "Хабаровский край" },
                new Station { Id = "2054000", Name = "Иркутск", Region = "Иркутская обл." },
                new Station { Id = "2047000", Name = "Тюмень", Region = "Тюменская обл." },
                new Station { Id = "2064050", Name = "Волгоград", Region = "Волгоградская обл." },
                new Station { Id = "2060002", Name = "Калининград", Region = "Калининградская обл." }
            };
        }
    }

    public class TrainBookingRequest
    {
        public string TrainNumber { get; set; }
        public string DepartureStationId { get; set; }
        public string DepartureStationName { get; set; }
        public string ArrivalStationId { get; set; }
        public string ArrivalStationName { get; set; }
        public string DepartureDateTime { get; set; }
        public string ArrivalDateTime { get; set; }
        public decimal Price { get; set; }
        public int Passengers { get; set; }
        public string CarType { get; set; }
        public string CarClass { get; set; }
        public int Duration { get; set; }
        public bool IsRoundTrip { get; set; }
        public string? ReturnTrainNumber { get; set; }
        public string? ReturnDepartureDateTime { get; set; }
        public string? ReturnArrivalDateTime { get; set; }
        public int? ReturnDuration { get; set; }
    }
}