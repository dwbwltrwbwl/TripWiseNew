// Models/ViewModels/FlightBookingViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace TripWise.Models.ViewModels
{
    public class FlightBookingViewModel
    {
        // Информация о рейсе туда
        public string FlightId { get; set; }
        public string Airline { get; set; }
        public string AirlineCode { get; set; }
        public string AirlineLogo { get; set; }
        public string FlightNumber { get; set; }
        public string DepartureCity { get; set; }
        public string ArrivalCity { get; set; }
        public string DepartureAirport { get; set; }
        public string ArrivalAirport { get; set; }
        public DateTime DepartureDateTime { get; set; }
        public DateTime ArrivalDateTime { get; set; }
        public decimal Price { get; set; }
        public int Duration { get; set; }
        public int Transfers { get; set; }
        public string Aircraft { get; set; }
        public string Baggage { get; set; }
        public string HandLuggage { get; set; }
        public string Meal { get; set; }

        // Информация о рейсе обратно (если есть)
        public string? ReturnFlightId { get; set; }
        public string? ReturnAirline { get; set; }
        public string? ReturnFlightNumber { get; set; }
        public DateTime? ReturnDepartureDateTime { get; set; }
        public DateTime? ReturnArrivalDateTime { get; set; }
        public int? ReturnDuration { get; set; }
        public int? ReturnTransfers { get; set; }

        [Required]
        [Range(1, 9, ErrorMessage = "Количество пассажиров должно быть от 1 до 9")]
        [Display(Name = "Пассажиры")]
        public int Passengers { get; set; } = 1;

        [Required]
        [Display(Name = "Класс")]
        public string FlightClass { get; set; } = "economy";

        public bool IsRoundTrip { get; set; }
    }

    public class FlightPassengerViewModel
    {
        [Required(ErrorMessage = "Укажите фамилию")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Фамилия должна содержать от 2 до 100 символов")]
        [RegularExpression(@"^[a-zA-Zа-яА-ЯёЁ\s\-]+$", ErrorMessage = "Фамилия может содержать только буквы, пробелы и дефисы")]
        [Display(Name = "Фамилия")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Укажите имя")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Имя должно содержать от 2 до 100 символов")]
        [RegularExpression(@"^[a-zA-Zа-яА-ЯёЁ\s\-]+$", ErrorMessage = "Имя может содержать только буквы, пробелы и дефисы")]
        [Display(Name = "Имя")]
        public string FirstName { get; set; }

        [Display(Name = "Отчество")]
        [StringLength(100, ErrorMessage = "Отчество должно содержать до 100 символов")]
        [RegularExpression(@"^[a-zA-Zа-яА-ЯёЁ\s\-]*$", ErrorMessage = "Отчество может содержать только буквы, пробелы и дефисы")]
        public string? MiddleName { get; set; }

        [Required(ErrorMessage = "Укажите дату рождения")]
        [DataType(DataType.Date)]
        [Display(Name = "Дата рождения")]
        public DateTime DateOfBirth { get; set; }

        [Required(ErrorMessage = "Укажите пол")]
        [Display(Name = "Пол")]
        public string Gender { get; set; }

        [Required(ErrorMessage = "Укажите тип документа")]
        [Display(Name = "Тип документа")]
        public string DocumentType { get; set; } = "passport";

        [Required(ErrorMessage = "Укажите номер документа")]
        [Display(Name = "Номер документа")]
        [RegularExpression(@"^\d+$", ErrorMessage = "Номер документа должен содержать только цифры")]
        [StringLength(10, MinimumLength = 4, ErrorMessage = "Номер документа должен содержать от 4 до 10 цифр")]
        public string DocumentNumber { get; set; }

        [Required(ErrorMessage = "Укажите гражданство")]
        [Display(Name = "Гражданство")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Гражданство должно содержать от 2 до 50 символов")]
        [RegularExpression(@"^[a-zA-Zа-яА-ЯёЁ\s\-]+$", ErrorMessage = "Гражданство может содержать только буквы, пробелы и дефисы")]
        public string Nationality { get; set; } = "Россия";
    }

    public class FlightContactViewModel
    {
        [Required(ErrorMessage = "Укажите ваше имя")]
        [Display(Name = "Имя")]
        [StringLength(200, MinimumLength = 2, ErrorMessage = "Имя должно содержать от 2 до 200 символов")]
        [RegularExpression(@"^[a-zA-Zа-яА-ЯёЁ\s\-]+$", ErrorMessage = "Имя может содержать только буквы, пробелы и дефисы")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Укажите email")]
        [EmailAddress(ErrorMessage = "Введите корректный email")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Укажите телефон")]
        [Phone(ErrorMessage = "Введите корректный номер телефона")]
        [Display(Name = "Телефон")]
        [RegularExpression(@"^\+7\s\(\d{3}\)\s\d{3}-\d{2}-\d{2}$", ErrorMessage = "Телефон должен быть в формате +7 (XXX) XXX-XX-XX")]
        public string Phone { get; set; }

        [Display(Name = "Согласен с условиями")]
        [Range(typeof(bool), "true", "true", ErrorMessage = "Необходимо согласие с условиями")]
        public bool AgreeToTerms { get; set; }
    }

    public class CompleteFlightBookingViewModel
    {
        public FlightBookingViewModel Flight { get; set; }
        public List<FlightPassengerViewModel> Passengers { get; set; } = new List<FlightPassengerViewModel>();
        public FlightContactViewModel Contact { get; set; }

        public decimal TotalPrice => Flight.Price * (Flight.IsRoundTrip ? 2 : 1) * (Passengers?.Count ?? 1);
        public string Currency => "RUB";
    }

    public class FlightBookingConfirmationViewModel
    {
        public string BookingId { get; set; }
        public string BookingNumber { get; set; }
        public string Airline { get; set; }
        public string FlightNumber { get; set; }
        public string DepartureCity { get; set; }
        public string ArrivalCity { get; set; }
        public string DepartureAirport { get; set; }
        public string ArrivalAirport { get; set; }
        public DateTime DepartureDateTime { get; set; }
        public DateTime ArrivalDateTime { get; set; }
        public string ReturnFlightNumber { get; set; }
        public DateTime? ReturnDepartureDateTime { get; set; }
        public DateTime? ReturnArrivalDateTime { get; set; }
        public int Passengers { get; set; }
        public string FlightClass { get; set; }
        public decimal Price { get; set; }
        public decimal TotalPrice { get; set; }
        public string Currency { get; set; }
        public string ContactName { get; set; }
        public string ContactEmail { get; set; }
        public string ContactPhone { get; set; }
        public string SeatNumbers { get; set; }
        public string BookingReference { get; set; }
        public string TicketNumber { get; set; }
        public bool IsRoundTrip { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; }
        public int Duration => (int)(ArrivalDateTime - DepartureDateTime).TotalMinutes;
        public int? ReturnDuration => ReturnDepartureDateTime.HasValue && ReturnArrivalDateTime.HasValue
            ? (int?)(ReturnArrivalDateTime.Value - ReturnDepartureDateTime.Value).TotalMinutes
            : null;

        // Список пассажиров с полными данными
        public List<FlightPassengerViewModel> PassengersData { get; set; } = new List<FlightPassengerViewModel>();
    }
}