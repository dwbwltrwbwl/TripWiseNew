// Models/ChecklistItem.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TripWise.Models
{
    public class ChecklistItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int NoteId { get; set; }

        [Required]
        [StringLength(500)]
        public string Text { get; set; } = "";

        public bool IsCompleted { get; set; }

        public int OrderIndex { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        [ForeignKey("NoteId")]
        public virtual Note? Note { get; set; }
    }
}