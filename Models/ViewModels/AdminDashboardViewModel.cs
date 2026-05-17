namespace TripWise.Models.ViewModels
{
    public class AdminDashboardViewModel
    {
        // Статистика пользователей
        public int TotalUsers { get; set; }
        public int NewUsersToday { get; set; }
        public int NewUsersWeek { get; set; }
        public int NewUsersMonth { get; set; }

        // Статистика бронирований
        public int TotalBookings { get; set; }
        public int FlightBookings { get; set; }
        public int TrainBookings { get; set; }
        public int HotelBookings { get; set; }

        // Финансы
        public decimal TotalRevenue { get; set; }
        public decimal FlightRevenue { get; set; }
        public decimal TrainRevenue { get; set; }
        public decimal HotelRevenue { get; set; }

        // Отзывы
        public int TotalReviews { get; set; }
        public double AverageRating { get; set; }

        // Активность
        public int ActiveUsersToday { get; set; }
        public int ActiveUsersWeek { get; set; }
        public int ActiveUsersMonth { get; set; }

        // График
        public List<string> ChartLabels { get; set; } = new();
        public List<int> NewUsersData { get; set; } = new();
        public List<int> BookingsData { get; set; } = new();

        // Последние бронирования
        public List<RecentBookingDto> RecentBookings { get; set; } = new();
    }

    public class RecentBookingDto
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string Type { get; set; }
        public string Route { get; set; }
        public decimal Price { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}