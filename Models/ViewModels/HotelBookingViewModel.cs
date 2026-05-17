using System.ComponentModel.DataAnnotations;

namespace TripWise.Models.ViewModels
{
    public class HotelBookingViewModel
    {
        // Информация об отеле
        public string HotelId { get; set; }
        public string HotelName { get; set; }
        public string HotelAddress { get; set; }
        public string HotelPhone { get; set; }
        public string HotelWebsite { get; set; }
        public double HotelLatitude { get; set; }
        public double HotelLongitude { get; set; }
        public string AccommodationType { get; set; }
        public int? Stars { get; set; }
        public decimal PricePerNight { get; set; }

        // Детали бронирования
        [Required(ErrorMessage = "Укажите дату заезда")]
        [DataType(DataType.Date)]
        [Display(Name = "Дата заезда")]
        public DateTime CheckInDate { get; set; } = DateTime.Today.AddDays(1);

        [Required(ErrorMessage = "Укажите дату выезда")]
        [DataType(DataType.Date)]
        [Display(Name = "Дата выезда")]
        public DateTime CheckOutDate { get; set; } = DateTime.Today.AddDays(3);

        [Required(ErrorMessage = "Укажите количество гостей")]
        [Range(1, 10, ErrorMessage = "Количество гостей должно быть от 1 до 10")]
        [Display(Name = "Количество гостей")]
        public int Guests { get; set; } = 2;

        [Required(ErrorMessage = "Укажите количество комнат")]
        [Range(1, 5, ErrorMessage = "Количество комнат должно быть от 1 до 5")]
        [Display(Name = "Количество комнат")]
        public int Rooms { get; set; } = 1;

        // Контактные данные
        [Required(ErrorMessage = "Укажите имя")]
        [StringLength(300, ErrorMessage = "Имя должно содержать до 300 символов")]
        [Display(Name = "Имя")]
        public string ContactName { get; set; }

        [Required(ErrorMessage = "Укажите email")]
        [EmailAddress(ErrorMessage = "Введите корректный email")]
        [StringLength(255, ErrorMessage = "Email должен содержать до 255 символов")]
        [Display(Name = "Email")]
        public string ContactEmail { get; set; }

        [Required(ErrorMessage = "Укажите телефон")]
        [Phone(ErrorMessage = "Введите корректный номер телефона")]
        [Display(Name = "Телефон")]
        public string ContactPhone { get; set; }

        [Display(Name = "Особые пожелания")]
        [StringLength(200, ErrorMessage = "Особые пожелания должны содержать до 200 символов")]
        public string SpecialRequests { get; set; }

        [Display(Name = "Согласен с условиями бронирования")]
        [Range(typeof(bool), "true", "true", ErrorMessage = "Необходимо согласие с условиями")]
        public bool AgreeToTerms { get; set; }

        // Вычисляемые поля
        public int Nights => (CheckOutDate - CheckInDate).Days;
        public decimal TotalPrice => PricePerNight * Nights * Rooms;
    }

    public class HotelBookingConfirmationViewModel
    {
        public string BookingId { get; set; }
        public string BookingNumber { get; set; }
        public string HotelName { get; set; }
        public string HotelAddress { get; set; }
        public DateTime CheckInDate { get; set; }
        public DateTime CheckOutDate { get; set; }
        public int Nights { get; set; }
        public int Guests { get; set; }
        public int Rooms { get; set; }
        public decimal PricePerNight { get; set; }
        public decimal TotalPrice { get; set; }
        public string ContactName { get; set; }
        public string ContactEmail { get; set; }
        public string ContactPhone { get; set; }
        public string SpecialRequests { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; }
    }
}