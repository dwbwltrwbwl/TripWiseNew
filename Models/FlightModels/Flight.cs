// Models/Flight.cs
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TripWise.Models
{
    public class FlightSearchRequest
    {
        public string DepartureCity { get; set; }
        public string ArrivalCity { get; set; }

        [JsonConverter(typeof(DateTimeJsonConverter))]
        public DateTime DepartureDate { get; set; }

        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? ReturnDate { get; set; }

        public int Passengers { get; set; } = 1;
        public string Class { get; set; } = "economy";
        public string TripType { get; set; } = "oneway";
    }

    public class DateTimeJsonConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return DateTime.Parse(reader.GetString());
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString("yyyy-MM-dd"));
        }
    }

    public class NullableDateTimeJsonConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var str = reader.GetString();
            if (string.IsNullOrEmpty(str) || str == "null")
                return null;
            return DateTime.Parse(str);
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
                writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd"));
            else
                writer.WriteNullValue();
        }
    }

    // Остальные модели остаются без изменений
    public class FlightSearchResponse
    {
        public bool Success { get; set; }
        public List<Flight> Flights { get; set; } = new();
        public string Error { get; set; }
        public string Message { get; set; }
        public string SearchId { get; set; }
        public PartnerLinks PartnerLinks { get; set; }
    }

    public class Flight
    {
        public string Id { get; set; }
        public string Airline { get; set; }
        public string AirlineCode { get; set; }
        public string AirlineLogo { get; set; }
        public string FlightNumber { get; set; }
        public string DepartureCity { get; set; }
        public string ArrivalCity { get; set; }
        public string DepartureAirport { get; set; }
        public string ArrivalAirport { get; set; }
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; } = "RUB";
        public int Transfers { get; set; }
        public int Duration { get; set; }
        public string Aircraft { get; set; }
        public bool IsReturn { get; set; }
        public string BookingUrl { get; set; }
        public FlightDetails Details { get; set; }
    }

    public class FlightDetails
    {
        public bool IsRefundable { get; set; }
        public bool IsChangeable { get; set; }
        public string Baggage { get; set; } = "1x23кг";
        public string HandLuggage { get; set; } = "1x10кг";
        public string Meal { get; set; } = "Включено";
    }

    public class PartnerLinks
    {
        public string AviasalesUrl { get; set; }
        public string YandexTravelUrl { get; set; }
        public string TutuUrl { get; set; }
        public string SkyscannerUrl { get; set; }
    }

    public class City
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Country { get; set; }
        public string CountryCode { get; set; }
        public List<Airport> Airports { get; set; } = new();
        public string TimeZone { get; set; }
    }

    public class Airport
    {
        public string Iata { get; set; }
        public string Name { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class RouteInfo
    {
        public string From { get; set; }
        public string To { get; set; }
        public int Distance { get; set; }
        public int AverageDuration { get; set; }
        public decimal AveragePrice { get; set; }
        public List<string> CommonAirlines { get; set; } = new();
    }
}