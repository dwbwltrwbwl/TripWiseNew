using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace TripWise.Models.ViewModels
{
    public class EditProfileViewModel
    {
        [Required(ErrorMessage = "Укажите фамилию")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Фамилия должна содержать от 2 до 100 символов")]
        [Display(Name = "Фамилия")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Укажите имя")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Имя должно содержать от 2 до 100 символов")]
        [Display(Name = "Имя")]
        public string FirstName { get; set; }

        [Display(Name = "Отчество")]
        [StringLength(100, ErrorMessage = "Отчество должно содержать до 100 символов")]
        public string? MiddleName { get; set; }

        [Required(ErrorMessage = "Укажите email")]
        [EmailAddress(ErrorMessage = "Введите корректный email")]
        [StringLength(255, ErrorMessage = "Email не может превышать 255 символов")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Display(Name = "Возраст")]
        [Range(1, 120, ErrorMessage = "Возраст должен быть от 1 до 120 лет")]
        public int? Age { get; set; }

        [Display(Name = "Аватар")]
        public IFormFile? Avatar { get; set; }

        [Display(Name = "Текущий аватар")]
        public string? CurrentAvatarPath { get; set; }

        [Display(Name = "Удалить текущий аватар")]
        public bool RemoveAvatar { get; set; }
    }
}