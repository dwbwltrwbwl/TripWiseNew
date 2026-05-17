// Models/ViewModels/AnalyticsViewModel.cs
namespace TripWise.Models.ViewModels
{
    public class AnalyticsViewModel
    {
        // Основные метрики
        public int TotalUsers { get; set; }
        public int NewUsersToday { get; set; }
        public int NewUsersWeek { get; set; }
        public int NewUsersMonth { get; set; }

        public int TotalFlightBookings { get; set; }
        public int TotalTrainBookings { get; set; }
        public int TotalHotelBookings { get; set; }
        public int TotalBookings => TotalFlightBookings + TotalTrainBookings + TotalHotelBookings;

        public decimal TotalRevenue { get; set; }
        public decimal FlightRevenue { get; set; }
        public decimal TrainRevenue { get; set; }
        public decimal HotelRevenue { get; set; }

        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }

        // Графики
        public List<ChartDataPoint> UserActivity { get; set; } = new();
        public List<ChartDataPoint> MonthlyRevenue { get; set; } = new();
        public List<PopularDestination> PopularDestinations { get; set; } = new();

        // Активность
        public int ActiveUsersToday { get; set; }
        public int ActiveUsersWeek { get; set; }
        public int ActiveUsersMonth { get; set; }

        // Последние отзывы
        public List<RecentReview> RecentReviews { get; set; } = new();
    }

    public class ChartDataPoint
    {
        public string Label { get; set; } = string.Empty;
        public int Value { get; set; }
        public decimal? Amount { get; set; }
    }

    public class PopularDestination
    {
        public string Route { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // Авиа, ЖД, Отель
        public int Count { get; set; }
        public int Percentage { get; set; }
        public string Icon { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
    }

    public class RecentReview
    {
        public string UserName { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string RatingStars { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}