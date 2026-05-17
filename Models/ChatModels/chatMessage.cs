using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWise.Models
{
    [Table("ChatMessages")]
    public class ChatMessage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("idMessage")]
        public int IdMessage { get; set; }

        [Required]
        [Column("message")]
        public string Message { get; set; } = null!;

        [Column("sentAt")]
        public DateTime SentAt { get; set; }

        [Column("idTrip")]
        public int? IdTrip { get; set; }

        [Column("idUser")]
        public int SenderId { get; set; }

        [Column("idPoint")]
        public int? IdPoint { get; set; }

        [Column("attachmentsJson")]
        public string? AttachmentsJson { get; set; } // JSON с массивом файлов

        // Старые поля оставляем для обратной совместимости
        [Column("attachmentName")]
        [StringLength(255)]
        public string? AttachmentName { get; set; }

        [Column("attachmentSize")]
        public long? AttachmentSize { get; set; }

        [Column("attachmentType")]
        [StringLength(50)]
        public string? AttachmentType { get; set; }

        [Column("attachmentUrl")]
        [StringLength(500)]
        public string? AttachmentUrl { get; set; }

        [Column("editedAt")]
        public DateTime? EditedAt { get; set; }

        [Column("idChat")]
        public int ChatId { get; set; }

        [Column("replyToId")]
        public int? ReplyToId { get; set; }

        // ТОЛЬКО эти навигационные свойства
        public virtual Chat? Chat { get; set; }
        public virtual User? Sender { get; set; }
        public virtual ChatMessage? ReplyTo { get; set; }

        // Этих свойств НЕ ДОЛЖНО БЫТЬ:
        // public virtual Trip? Trip { get; set; }
        // public virtual PointsOfInterest? Point { get; set; }

        public virtual ICollection<ChatMessage> Replies { get; set; } = new List<ChatMessage>();
        public virtual ICollection<ChatMessageRead> Reads { get; set; } = new List<ChatMessageRead>();
    }
}