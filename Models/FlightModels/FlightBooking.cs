// Models/FlightBooking.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWise.Models
{
    public class FlightBooking
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();

        [Required]
        public int UserId { get; set; }

        [Required]
        public string BookingNumber { get; set; } = GenerateBookingNumber();

        // Информация о рейсе
        [Required]
        public string FlightId { get; set; }

        [Required]
        public string Airline { get; set; }

        public string AirlineCode { get; set; }

        public string AirlineLogo { get; set; }

        [Required]
        public string FlightNumber { get; set; }

        [Required]
        public string DepartureCity { get; set; }

        [Required]
        public string ArrivalCity { get; set; }

        [Required]
        public string DepartureAirport { get; set; }

        [Required]
        public string ArrivalAirport { get; set; }

        [Required]
        public DateTime DepartureDateTime { get; set; }
        public decimal TotalPrice { get; set; } // Общая стоимость бронирования

        [Required]
        public DateTime ArrivalDateTime { get; set; }

        // Для обратного рейса (если есть)
        public string? ReturnFlightId { get; set; }
        public string? ReturnAirline { get; set; }
        public string? ReturnFlightNumber { get; set; }
        public DateTime? ReturnDepartureDateTime { get; set; }
        public DateTime? ReturnArrivalDateTime { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Required]
        public string Currency { get; set; } = "RUB";

        [Required]
        public int Passengers { get; set; }

        [Required]
        public string FlightClass { get; set; } = "economy";

        public int Duration { get; set; }

        public int? ReturnDuration { get; set; }

        public int Transfers { get; set; }

        public int? ReturnTransfers { get; set; }

        public string Aircraft { get; set; }

        // Детали багажа и услуг
        public string Baggage { get; set; } = "1x23кг";
        public string HandLuggage { get; set; } = "1x10кг";
        public string Meal { get; set; } = "Включено";

        [Required]
        public bool IsRoundTrip { get; set; }

        // Контактные данные
        [Required]
        public string ContactName { get; set; }

        [Required]
        public string ContactEmail { get; set; }

        [Required]
        public string ContactPhone { get; set; }

        // Информация о пассажирах (JSON)
        public string PassengersJson { get; set; }

        // Номера мест (генерируются после оплаты)
        public string SeatNumbers { get; set; }

        // Статусы
        public BookingStatus Status { get; set; } = BookingStatus.Pending;

        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

        public string PaymentMethod { get; set; }

        public string TransactionId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ConfirmedAt { get; set; }

        public DateTime? CancelledAt { get; set; }

        public string BookingReference { get; set; } // Номер бронирования (PNR)

        public string TicketNumber { get; set; } // Номер билета

        public string CancellationReason { get; set; }

        public string Notes { get; set; }

        // Навигационное свойство
        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        private static string GenerateBookingNumber()
        {
            return "FLT" + DateTime.Now.ToString("yyyyMMddHHmmss") + new Random().Next(100, 999).ToString();
        }
    }
}