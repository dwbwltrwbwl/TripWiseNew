namespace TripWise.Models
{
    public class ReviewsViewModel
    {
        public bool IsAuthenticated { get; set; }
        public string UserName { get; set; } = "";
        public string UserEmail { get; set; } = "";
    }
}