using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWise.Models
{
    public class FavoriteHotel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required]
        [MaxLength(100)]
        public string HotelId { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string HotelName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? HotelAddress { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        [MaxLength(100)]
        public string? AccommodationType { get; set; }

        public int? Stars { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        [MaxLength(500)]
        public string? Website { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? PricePerNight { get; set; }

        [MaxLength(10)]
        public string Currency { get; set; } = "RUB";

        [MaxLength(500)]
        public string? BookingUrl { get; set; }

        public string? TagsJson { get; set; }

        [Required]
        public DateTime AddedDate { get; set; } = DateTime.Now;

        [MaxLength(1000)]
        public string? Notes { get; set; }
    }
}