// DTOs/ChecklistDto.cs
namespace TripWise.Models.DTOs
{
    public class ChecklistItemDto
    {
        public int Id { get; set; }
        public string Text { get; set; } = "";
        public bool IsCompleted { get; set; }
        public int OrderIndex { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class CreateChecklistItemRequest
    {
        public int NoteId { get; set; }
        public string Text { get; set; } = "";
    }

    public class UpdateChecklistItemRequest
    {
        public int Id { get; set; }
        public string? Text { get; set; }
        public bool? IsCompleted { get; set; }
        public int? OrderIndex { get; set; }
    }
}