namespace TripWise.Models.DTOs
{
    public class FileAttachmentDto
    {
        public string FileName { get; set; } = "";
        public string FileUrl { get; set; } = "";
        public long FileSize { get; set; }
        public string FileType { get; set; } = "";
    }
}