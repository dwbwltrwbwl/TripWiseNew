namespace TripWise.Models
{
    public class HotelSearchRequest
    {
        public string City { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int Radius { get; set; } = 5000;
        public string AccommodationType { get; set; } = "all";
        public int? MinStars { get; set; }
        public string SortBy { get; set; } = "distance";
    }

    public class HotelSearchResponse
    {
        public bool Success { get; set; }
        public List<Hotel> Hotels { get; set; } = new List<Hotel>();
        public string Error { get; set; }
        public OSMStats OSMStats { get; set; }
    }

    public class Hotel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string AccommodationType { get; set; }
        public int? Stars { get; set; }
        public string Phone { get; set; }
        public string Website { get; set; }
        public double Distance { get; set; }
        public string OSMUrl { get; set; }
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();
    }

    public class OSMStats
    {
        public int TotalFound { get; set; }
        public DateTime DataTimestamp { get; set; }
        public string DataSource { get; set; } = "OpenStreetMap";
        public string Attribution { get; set; }
    }

    // Вспомогательные классы для десериализации JSON
    public class NominatimResult
    {
        public string Lat { get; set; }
        public string Lon { get; set; }
        public string Display_Name { get; set; }
    }

    public class OverpassResponse
    {
        public List<OverpassElement> Elements { get; set; }
    }

    public class OverpassElement
    {
        public long Id { get; set; }
        public double Lat { get; set; }
        public double Lon { get; set; }
        public Dictionary<string, string> Tags { get; set; }
    }

    public class CityCoordinates
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string DisplayName { get; set; }
    }
}