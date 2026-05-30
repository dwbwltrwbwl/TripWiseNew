// Models/ViewModels/TrainBookingViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace TripWise.Models.ViewModels
{
    public class TrainBookingViewModel
    {
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

        // Убираем Required, так как прибытие может быть вычислено
        public DateTime? ArrivalDateTime { get; set; }  // Сделал nullable

        public DateTime? ReturnDepartureDateTime { get; set; }

        public DateTime? ReturnArrivalDateTime { get; set; }

        [Required]
        public decimal Price { get; set; }

        [Required]
        public int Passengers { get; set; } = 1;

        public string CarType { get; set; } = "coupe";

        public string CarClass { get; set; } = "2К";

        public int Duration { get; set; }

        public int? ReturnDuration { get; set; }

        public bool IsRoundTrip { get; set; }

        public string? TrainBrand { get; set; }

        public string? Carrier { get; set; }
    }

    public class PassengerInfoViewModel
    {
        [Required(ErrorMessage = "Укажите фамилию")]
        [Display(Name = "Фамилия")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Фамилия должна содержать от 2 до 100 символов")]  // ← изменил 50 на 100
        [RegularExpression(@"^[a-zA-Zа-яА-ЯёЁ\s\-]+$", ErrorMessage = "Фамилия может содержать только буквы, пробелы и дефисы")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Укажите имя")]
        [Display(Name = "Имя")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Имя должно содержать от 2 до 100 символов")]  // ← изменил 50 на 100
        [RegularExpression(@"^[a-zA-Zа-яА-ЯёЁ\s\-]+$", ErrorMessage = "Имя может содержать только буквы, пробелы и дефисы")]
        public string FirstName { get; set; }

        [Display(Name = "Отчество")]
        [StringLength(100, ErrorMessage = "Отчество должно содержать до 100 символов")]  // ← изменил 50 на 100
        [RegularExpression(@"^[a-zA-Zа-яА-ЯёЁ\s\-]*$", ErrorMessage = "Отчество может содержать только буквы, пробелы и дефисы")]
        public string? MiddleName { get; set; }

        [Required(ErrorMessage = "Укажите дату рождения")]
        [Display(Name = "Дата рождения")]
        [DataType(DataType.Date)]
        public DateTime DateOfBirth { get; set; }

        [Required(ErrorMessage = "Укажите пол")]
        [Display(Name = "Пол")]
        public string Gender { get; set; }

        [Required(ErrorMessage = "Укажите тип документа")]
        [Display(Name = "Тип документа")]
        public string DocumentType { get; set; } = "passport";

        [Required(ErrorMessage = "Укажите номер документа")]
        [Display(Name = "Номер документа")]
        [StringLength(20, MinimumLength = 4, ErrorMessage = "Номер документа должен содержать от 4 до 20 символов")]
        [RegularExpression(@"^[0-9]+$", ErrorMessage = "Номер документа может содержать только цифры")]
        public string DocumentNumber { get; set; }

        [Display(Name = "Гражданство")]
        [StringLength(50, ErrorMessage = "Гражданство должно содержать до 50 символов")]
        [RegularExpression(@"^[a-zA-Zа-яА-ЯёЁ\s\-]+$", ErrorMessage = "Гражданство может содержать только буквы, пробелы и дефисы")]
        public string Citizenship { get; set; } = "РФ";
    }

    public class ContactInfoViewModel
    {
        [Required(ErrorMessage = "Укажите email")]
        [EmailAddress(ErrorMessage = "Введите корректный email")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Укажите телефон")]
        [Phone(ErrorMessage = "Введите корректный номер телефона")]
        [Display(Name = "Телефон")]
        public string Phone { get; set; }

        [Display(Name = "Согласен с условиями")]
        [Range(typeof(bool), "true", "true", ErrorMessage = "Необходимо согласие с условиями")]
        public bool AgreeToTerms { get; set; }
    }

    public class CompleteBookingViewModel
    {
        public TrainBookingViewModel TrainInfo { get; set; }
        public List<PassengerInfoViewModel> Passengers { get; set; } = new List<PassengerInfoViewModel>();
        public ContactInfoViewModel Contact { get; set; }

        // ✅ ИСПРАВЛЕНО: Price уже содержит стоимость туда+обратно (если IsRoundTrip = true)
        // Не нужно умножать на 2 еще раз!
        public decimal TotalPrice => (TrainInfo?.Price ?? 0) * (TrainInfo?.Passengers ?? 0);
    }
}