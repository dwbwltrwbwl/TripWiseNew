using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWise.Models
{
    public class HotelBooking
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();

        [Required]
        public int UserId { get; set; }

        [Required]
        public string BookingNumber { get; set; } = GenerateBookingNumber();

        // Информация об отеле
        [Required]
        public string HotelId { get; set; }

        [Required]
        [StringLength(200)]
        public string HotelName { get; set; }

        [StringLength(500)]
        public string HotelAddress { get; set; } = "Адрес не указан";

        [StringLength(50)]
        public string HotelPhone { get; set; } = "";

        [StringLength(500)]
        public string HotelWebsite { get; set; } = "";

        public double HotelLatitude { get; set; }

        public double HotelLongitude { get; set; }

        [StringLength(50)]
        public string AccommodationType { get; set; }

        public int? Stars { get; set; }

        // Детали бронирования
        [Required]
        public DateTime CheckInDate { get; set; }

        [Required]
        public DateTime CheckOutDate { get; set; }

        [Required]
        public int Nights { get; set; }

        [Required]
        public int Guests { get; set; }

        public int Rooms { get; set; } = 1;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PricePerNight { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }

        [Required]
        public string Currency { get; set; } = "RUB";

        // Контактные данные
        [Required]
        [StringLength(300)]
        public string ContactName { get; set; }

        [Required]
        [StringLength(255)]
        public string ContactEmail { get; set; }

        [Required]
        [StringLength(20)]
        public string ContactPhone { get; set; }

        [StringLength(200)]
        public string SpecialRequests { get; set; } = "";

        // Статусы
        public BookingStatus Status { get; set; } = BookingStatus.Pending;

        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

        public string? PaymentMethod { get; set; }

        public string? TransactionId { get; set; }

        // Даты
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ConfirmedAt { get; set; }

        public DateTime? CancelledAt { get; set; }

        public DateTime? CheckedInAt { get; set; }

        public DateTime? CheckedOutAt { get; set; }

        // Дополнительная информация
        public string CancellationReason { get; set; } = "";

        public string Notes { get; set; } = "";

        // Навигационное свойство
        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        private static string GenerateBookingNumber()
        {
            return "HTL" + DateTime.Now.ToString("yyyyMMddHHmmss") + new Random().Next(100, 999).ToString();
        }
    }

    public enum BookingStatus
    {
        Pending,        // Ожидает подтверждения
        Confirmed,      // Подтверждено
        Cancelled,      // Отменено
        Completed,      // Завершено (после выезда)
        NoShow          // Не заселились
    }
}