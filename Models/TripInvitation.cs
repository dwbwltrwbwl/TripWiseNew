// Models/TripInvitation.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWise.Models
{
    [Table("TripInvitations")]
    public class TripInvitation
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("idInvitation")]
        public int IdInvitation { get; set; }

        [Column("idTrip")]
        public int IdTrip { get; set; }

        [Column("inviterId")]
        public int InviterId { get; set; }

        [Column("invitedId")]
        public int InvitedId { get; set; }

        [Column("message")]
        [StringLength(500)]
        public string? Message { get; set; }

        [Column("invitedAt")]
        public DateTime InvitedAt { get; set; }

        [Column("respondedAt")]
        public DateTime? RespondedAt { get; set; }

        [Column("status")]
        [StringLength(20)]
        public string Status { get; set; } = "pending";

        // Навигационные свойства
        public virtual Trip? Trip { get; set; }
        public virtual User? Inviter { get; set; }
        public virtual User? Invited { get; set; }
    }
}