using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWise.Models
{
    public class FavoriteFlight
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required]
        [MaxLength(100)]
        public string FlightId { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Airline { get; set; } = string.Empty;

        [MaxLength(10)]
        public string? AirlineCode { get; set; }

        [Required]
        [MaxLength(50)]
        public string FlightNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string DepartureCity { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string ArrivalCity { get; set; } = string.Empty;

        [MaxLength(10)]
        public string? DepartureAirport { get; set; }

        [MaxLength(10)]
        public string? ArrivalAirport { get; set; }

        [Required]
        public DateTime DepartureTime { get; set; }

        [Required]
        public DateTime ArrivalTime { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public string? Currency { get; set; } = "RUB";

        public int Transfers { get; set; }

        public int Duration { get; set; }

        [MaxLength(100)]
        public string? Aircraft { get; set; }

        public bool IsReturn { get; set; }

        [MaxLength(500)]
        public string? BookingUrl { get; set; }

        public string? SearchParameters { get; set; }

        [Required]
        public DateTime AddedDate { get; set; } = DateTime.Now;

        public DateTime? TripDate { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        // ========== ДОБАВЬТЕ ЭТИ ПОЛЯ ==========
        [MaxLength(50)]
        public string? Baggage { get; set; } = "1x23кг";

        [MaxLength(50)]
        public string? HandLuggage { get; set; } = "1x10кг";

        [MaxLength(100)]
        public string? Meal { get; set; } = "Включено";

        [MaxLength(50)]
        public string? FlightClass { get; set; } = "Economy";
    }
}