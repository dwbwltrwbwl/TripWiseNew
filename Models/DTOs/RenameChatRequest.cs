namespace TripWise.Models.DTOs
{
    public class RenameChatRequest
    {
        public int ChatId { get; set; }
        public string NewName { get; set; } = "";
    }
}