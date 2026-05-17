// Models/ViewModels/HomeViewModel.cs
namespace TripWise.Models.ViewModels
{
    public class HomeViewModel
    {
        public List<HomeReviewDto> RecentReviews { get; set; } = new();
        public ReviewStatisticsDto Statistics { get; set; } = new();
        public bool HasReviews => RecentReviews != null && RecentReviews.Any();
    }

    // Специальная модель для отображения на главной странице
    public class HomeReviewDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        // Вычисляемые свойства для отображения
        public string ShortText => Text?.Length > 150 ? Text.Substring(0, 150) + "..." : Text ?? "";
        public string FormattedDate => CreatedAt.ToString("dd MMMM yyyy");

        // Для отображения звезд
        public string GetStarsHtml()
        {
            string stars = "";
            for (int i = 1; i <= 5; i++)
            {
                stars += i <= Rating
                    ? "<i class='fas fa-star text-warning'></i>"
                    : "<i class='far fa-star text-warning'></i>";
            }
            return stars;
        }
    }
}