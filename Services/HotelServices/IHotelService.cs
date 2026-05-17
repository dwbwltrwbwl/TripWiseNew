using TripWise.Models;

namespace TripWise.Services
{
    public interface IHotelService
    {
        Task<List<Hotel>> SearchHotelsAsync(HotelSearchRequest request);
    }
}