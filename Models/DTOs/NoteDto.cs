// DTOs/NoteDto.cs
namespace TripWise.Models.DTOs
{
    public class NoteDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string? Color { get; set; }
        public bool IsPinned { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string Preview { get; set; } = "";
    }

    // DTOs/NoteDto.cs
    public class CreateNoteRequest
    {
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string? Color { get; set; }
        public bool IsPinned { get; set; }
        public List<ChecklistItemDto>? ChecklistItems { get; set; } // Добавляем чек-лист
    }

    public class UpdateNoteRequest
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string? Color { get; set; }
        public bool IsPinned { get; set; }
        public List<ChecklistItemDto>? ChecklistItems { get; set; } // Добавляем чек-лист
    }
}