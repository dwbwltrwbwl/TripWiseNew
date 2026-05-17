using System;

namespace TripWise.Models
{
    public class UserDocument
    {
        public int IdDocument { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string FileType { get; set; }
        public long FileSize { get; set; }
        public string FilePath { get; set; }
        public string DocumentType { get; set; }
        public string DocumentNumber { get; set; }
        public DateTime? DocumentDate { get; set; }
        public int? FolderId { get; set; }
        public int UserId { get; set; }
        public DateTime CreatedAt { get; set; }

        // Навигационные свойства
        public virtual DocumentFolder Folder { get; set; }
        public virtual User User { get; set; }
    }
}