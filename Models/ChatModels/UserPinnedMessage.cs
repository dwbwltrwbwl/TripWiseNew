using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWise.Models
{
    [Table("UserPinnedMessages")]
    public class UserPinnedMessage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }

        [Column("userId")]
        public int UserId { get; set; }

        [Column("chatId")]
        public int ChatId { get; set; }

        [Column("messageId")]
        public int MessageId { get; set; }

        [Column("pinnedAt")]
        public DateTime PinnedAt { get; set; }

        // Навигационные свойства
        [ForeignKey(nameof(UserId))]
        public virtual User? User { get; set; }

        [ForeignKey(nameof(ChatId))]
        public virtual Chat? Chat { get; set; }

        [ForeignKey(nameof(MessageId))]
        public virtual ChatMessage? Message { get; set; }
    }
}