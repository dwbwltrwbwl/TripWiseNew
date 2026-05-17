using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWise.Models
{
    [Table("Reviews")]
    public class Review
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("User")]
        public int UserId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        [StringLength(255)]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        [Required]
        public string Text { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        public bool IsApproved { get; set; } = true;

        public bool IsDeleted { get; set; } = false;

        // Навигационное свойство
        public virtual User User { get; set; }
    }

    // Модель для передачи данных (DTO)
    public class ReviewDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public int Rating { get; set; }
        public string Text { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // Модель для создания отзыва
    public class CreateReviewDto
    {
        [Required(ErrorMessage = "Имя обязательно")]
        [StringLength(100, ErrorMessage = "Имя не должно превышать 100 символов")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Email обязателен")]
        [EmailAddress(ErrorMessage = "Введите корректный email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Оценка обязательна")]
        [Range(1, 5, ErrorMessage = "Оценка должна быть от 1 до 5")]
        public int Rating { get; set; }

        [Required(ErrorMessage = "Текст отзыва обязателен")]
        [StringLength(2000, MinimumLength = 10, ErrorMessage = "Текст отзыва должен быть от 10 до 2000 символов")]
        public string Text { get; set; }
    }

    // Модель для статистики
    public class ReviewStatisticsDto
    {
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public Dictionary<int, int> RatingCounts { get; set; } = new Dictionary<int, int>();
    }
}