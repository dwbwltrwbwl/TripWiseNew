// Models/TrainOrder.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWise.Models
{
    public class TrainOrder
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();

        [Required]
        public int UserId { get; set; }

        [Required]
        public string OrderNumber { get; set; } = GenerateOrderNumber();

        [Required]
        public string TrainNumber { get; set; }

        public string? ReturnTrainNumber { get; set; }

        [Required]
        public string DepartureStationId { get; set; }

        [Required]
        public string DepartureStationName { get; set; }

        [Required]
        public string ArrivalStationId { get; set; }

        [Required]
        public string ArrivalStationName { get; set; }

        [Required]
        public DateTime DepartureDateTime { get; set; }

        public DateTime? ArrivalDateTime { get; set; }

        public DateTime? ReturnDepartureDateTime { get; set; }

        public DateTime? ReturnArrivalDateTime { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }

        [Required]
        public string Currency { get; set; } = "RUB";

        [Required]
        public int Passengers { get; set; }

        [Required]
        public string CarType { get; set; } // Тип вагона (Плацкарт, Купе и т.д.)

        [Required]
        public string CarClass { get; set; } // Класс обслуживания (2К, 3Б и т.д.)

        public string? SeatNumbers { get; set; } // Номера мест через запятую

        public string? CarNumber { get; set; } // Номер вагона

        [Required]
        public string ContactEmail { get; set; }

        [Required]
        public string ContactPhone { get; set; }

        [Required]
        [StringLength(2000)]
        public string PassengerFullName { get; set; }

        public string? PassengerDocumentType { get; set; }

        public string? PassengerDocumentNumber { get; set; }

        [Required]
        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

        public string? PaymentMethod { get; set; }

        public string? TransactionId { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ConfirmedAt { get; set; }

        public string? BookingReference { get; set; } // Номер бронирования

        public string? TicketNumber { get; set; } // Номер билета

        public string? ElectronicTicketUrl { get; set; } // URL для скачивания билета

        public string? Notes { get; set; }

        [Required]
        public bool IsRoundTrip { get; set; }

        [Required]
        public int Duration { get; set; } // Время в пути в минутах
        public string? PassengersJson { get; set; }

        public int? ReturnDuration { get; set; }

        // Навигационное свойство
        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        private static string GenerateOrderNumber()
        {
            return "RZD" + DateTime.Now.ToString("yyyyMMddHHmmss") + new Random().Next(100, 999).ToString();
        }
    }

    // Модель для пассажиров (если нужно несколько пассажиров)
    public class TrainPassenger
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string OrderId { get; set; }

        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        public string? MiddleName { get; set; }

        [Required]
        public DateTime DateOfBirth { get; set; }

        [Required]
        public string Gender { get; set; } // M/F

        [Required]
        public string DocumentType { get; set; } // Passport, Birth Certificate, etc.

        [Required]
        public string DocumentNumber { get; set; }

        public string? Citizenship { get; set; }

        public string? SeatNumber { get; set; }

        public string? CarNumber { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [ForeignKey("OrderId")]
        public virtual TrainOrder Order { get; set; }
    }

    public enum OrderStatus
    {
        Pending,        // Ожидает подтверждения
        Confirmed,      // Подтвержден
        Cancelled,      // Отменен
        Completed,      // Выполнен (поездка состоялась)
        Refunded        // Возврат
    }

    public enum PaymentStatus
    {
        Pending,
        Processing,
        Paid,
        Failed,
        Refunded
    }
}