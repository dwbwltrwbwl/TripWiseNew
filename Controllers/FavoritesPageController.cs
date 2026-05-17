using Microsoft.AspNetCore.Mvc;
using TripWise.Services;
using Microsoft.AspNetCore.Http;
using TripWise.Models;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace TripWise.Controllers
{
    public class FavoritesPageController : Controller
    {
        private readonly IFavoriteService _favoriteService;
        private readonly ILogger<FavoritesPageController> _logger;

        public FavoritesPageController(IFavoriteService favoriteService, ILogger<FavoritesPageController> logger)
        {
            _favoriteService = favoriteService;
            _logger = logger;
        }

        [HttpGet]
        [Route("Favorites")]
        [Route("Home/Favorites")]
        public async Task<IActionResult> Index()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (!userId.HasValue)
            {
                return View("~/Views/Home/Favorites.cshtml",
                    new Tuple<List<FavoriteFlight>, List<FavoriteTrain>, List<FavoriteHotel>>(
                        new List<FavoriteFlight>(),
                        new List<FavoriteTrain>(),
                        new List<FavoriteHotel>()));
            }

            try
            {
                var flights = await _favoriteService.GetUserFavoriteFlightsAsync(userId.Value);
                var trains = await _favoriteService.GetUserFavoriteTrainsAsync(userId.Value);
                var hotels = await _favoriteService.GetUserFavoriteHotelsAsync(userId.Value);

                return View("~/Views/Home/Favorites.cshtml",
                    new Tuple<List<FavoriteFlight>, List<FavoriteTrain>, List<FavoriteHotel>>(
                        flights, trains, hotels));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении избранного");
                return View("~/Views/Home/Favorites.cshtml",
                    new Tuple<List<FavoriteFlight>, List<FavoriteTrain>, List<FavoriteHotel>>(
                        new List<FavoriteFlight>(),
                        new List<FavoriteTrain>(),
                        new List<FavoriteHotel>()));
            }
        }
    }
}