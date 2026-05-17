namespace TripWise.Models.DTOs
{
    public class UnpinMessageRequest
    {
        public int ChatId { get; set; }
        public bool PinForAll { get; set; }
    }
}