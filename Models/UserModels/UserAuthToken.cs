using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWise.Models
{
    [Table("UserAuthTokens")]
    public class UserAuthToken
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [MaxLength(500)]
        public string Token { get; set; } = null!;

        public DateTime CreatedAt { get; set; }

        public DateTime ExpiresAt { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;
    }
}