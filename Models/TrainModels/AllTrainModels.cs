namespace TripWise.Models
{
    public class TrainSearchRequest
    {
        public string DepartureStationId { get; set; }
        public string ArrivalStationId { get; set; }
        public string DepartureDate { get; set; }
        public string? ReturnDate { get; set; } // Добавляем обратную дату
        public int Passengers { get; set; } = 1;
        public bool IsReturn { get; set; } = false; // Флаг для рейсов обратно
    }

    public class TrainSearchResponse
    {
        public string Name { get; set; }
        public string DepartureStation { get; set; }
        public string ArrivalStation { get; set; }
        public string DepartureTime { get; set; }
        public string ArrivalTime { get; set; }
        public string TrainNumber { get; set; }
        public string TravelTime { get; set; }
        public string DepartureDate { get; set; }
        public decimal Price { get; set; }
        public List<TrainCategory> Categories { get; set; }
        public bool Firm { get; set; }
        public bool IsReturn { get; set; } = false; // Добавляем флаг
    }

    public class TrainGroupResponse
    {
        public string Id { get; set; }
        public TrainSearchResponse ForwardTrain { get; set; }
        public TrainSearchResponse ReturnTrain { get; set; }
        public decimal TotalPrice { get; set; }
        public bool IsRoundTrip { get; set; }
    }

    public class TrainCategory
    {
        public string Type { get; set; }
        public decimal Price { get; set; }
    }

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
        public string Rid { get; set; } // строковый RID
        public long? RID { get; set; } // числовой RID
        public string Timestamp { get; set; }
        public List<RzdRoute> Lst { get; set; }

        public string GetRid() => Rid ?? RID?.ToString();
    }

    public class RzdRoute
    {
        public string Number { get; set; } // "022А"
        public string Number2 { get; set; } // "022А"
        public string Brand { get; set; } // "Night Express" 
        public string Carrier { get; set; } // "ТВЕРСК"
        public string Route0 { get; set; } // "МОСКВА ОКТ"
        public string Route1 { get; set; } // "С-ПЕТЕР-ГЛ"
        public string Station0 { get; set; } // "МОСКВА ОКТЯБРЬСКАЯ (ЛЕНИНГРАДСКИЙ ВОКЗАЛ)"
        public string Station1 { get; set; } // "САНКТ-ПЕТЕРБУРГ-ГЛАВН. (МОСКОВСКИЙ ВОКЗАЛ)"
        public string Date0 { get; set; } // "23.11.2025"
        public string Time0 { get; set; } // "00:25"
        public string Date1 { get; set; } // "23.11.2025" 
        public string Time1 { get; set; } // "09:30"
        public string TimeInWay { get; set; } // "09:05"
        public bool BFirm { get; set; } // true/firm
        public List<RzdCar> Cars { get; set; }
    }

    public class RzdCar
    {
        public string Type { get; set; } // "Люкс", "Купе", "Плац"
        public string TypeLoc { get; set; } // "СВ", "Купе", "Плацкартный" 
        public string ServCls { get; set; } // "1Б", "2Ф", "3Б"
        public int FreeSeats { get; set; } // 17, 55, 5
        public decimal Tariff { get; set; } // 14335, 5123, 3099
        public int IType { get; set; } // 6, 4, 1
    }

    public class Station
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Region { get; set; }
    }
}