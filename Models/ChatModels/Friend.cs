// Models/Friend.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWise.Models
{
    [Table("Friends")]
    public class Friend
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int FriendId { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? AcceptedAt { get; set; } // Если null - запрос ожидает подтверждения

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "pending"; // pending, accepted, rejected, blocked

        // Навигационные свойства
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        [ForeignKey("FriendId")]
        public virtual User FriendUser { get; set; } = null!;
    }

    [Table("FriendRequests")]
    public class FriendRequest
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int SenderId { get; set; }

        [Required]
        public int ReceiverId { get; set; }

        public string? Message { get; set; }

        [Required]
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public DateTime? RespondedAt { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "pending"; // pending, accepted, rejected

        // Навигационные свойства
        [ForeignKey("SenderId")]
        public virtual User Sender { get; set; } = null!;

        [ForeignKey("ReceiverId")]
        public virtual User Receiver { get; set; } = null!;
    }
}