namespace TripWise.Models.DTOs
{
    public class FileUploadResponse
    {
        public string FileName { get; set; } = "";
        public string FileUrl { get; set; } = "";
        public long FileSize { get; set; }
        public string FileType { get; set; } = "";
    }
}