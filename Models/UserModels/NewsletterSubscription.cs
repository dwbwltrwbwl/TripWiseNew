using System.ComponentModel.DataAnnotations;

namespace TripWise.Models
{
    public class NewsletterSubscription
    {
        public int Id { get; set; }

        [Required]
        [EmailAddress]
        [MaxLength(255)] // Добавьте ограничение длины
        public string Email { get; set; } = null!;

        public DateTime SubscribedAt { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? UnsubscribedAt { get; set; }

        [MaxLength(50)] // Ограничим длину
        public string? Source { get; set; } // "footer", "registration", etc.
    }
}