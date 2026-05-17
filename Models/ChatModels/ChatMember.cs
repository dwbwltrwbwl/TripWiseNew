using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWise.Models;

[Table("ChatMembers")]
public class ChatMember
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("idChatMember")]
    public int Id { get; set; }

    [Required]
    [Column("idChat")]
    public int ChatId { get; set; }

    [Required]
    [Column("idUser")]
    public int UserId { get; set; }

    [Column("joinedAt")]
    public DateTime JoinedAt { get; set; }

    [Column("lastReadAt")]
    public DateTime? LastReadAt { get; set; }

    [Required]
    [StringLength(20)]
    [Column("role")]
    public string Role { get; set; } = "member";

    // =====================
    // Навигационные свойства
    // =====================

    [ForeignKey(nameof(ChatId))]
    public virtual Chat? Chat { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual User? User { get; set; }
}