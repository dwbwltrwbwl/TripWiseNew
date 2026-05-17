using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWise.Models;

[Table("ChatMessageReads")]
public partial class ChatMessageRead
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("idChatMessageRead")]
    public int Id { get; set; }

    [Column("idMessage")]
    public int MessageId { get; set; }

    [Column("idUser")]
    public int UserId { get; set; }

    [Column("readAt")]
    public DateTime ReadAt { get; set; }

    // Навигационные свойства
    [ForeignKey("MessageId")]
    public virtual ChatMessage Message { get; set; } = null!;

    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;
}