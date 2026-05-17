using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWise.Models
{
    public class FavoriteTrain
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required]
        [MaxLength(100)]
        public string TrainGroupId { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string ForwardTrainNumber { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? ReturnTrainNumber { get; set; }

        [Required]
        [MaxLength(200)]
        public string DepartureStation { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string ArrivalStation { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? DepartureStationId { get; set; }

        [MaxLength(50)]
        public string? ArrivalStationId { get; set; }

        [Required]
        public DateTime DepartureDateTime { get; set; }

        public DateTime? ReturnDepartureDateTime { get; set; }

        [Required]
        public DateTime ArrivalDateTime { get; set; }

        public DateTime? ReturnArrivalDateTime { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [MaxLength(10)]
        public string Currency { get; set; } = "RUB";

        public int Duration { get; set; }

        public int? ReturnDuration { get; set; }

        [MaxLength(100)]
        public string? TrainBrand { get; set; }

        [MaxLength(100)]
        public string? Carrier { get; set; }

        public bool IsFirm { get; set; }

        public bool IsRoundTrip { get; set; }

        public int Passengers { get; set; } = 1;

        [MaxLength(500)]
        public string? BookingUrl { get; set; }

        [Required]
        public DateTime AddedDate { get; set; } = DateTime.Now;

        [MaxLength(1000)]
        public string? Notes { get; set; }
    }
}