using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TripWise.Models;
using TripWise.Models.DTOs;

namespace TripWise.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PlannedActivitiesController : ControllerBase
    {
        private readonly TripWiseContext _context;

        public PlannedActivitiesController(TripWiseContext context)
        {
            _context = context;
        }

        // GET: api/PlannedActivities
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetUserActivities()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return Unauthorized();
            }

            // Возвращаем объект с полем City
            var activities = await _context.PlannedActivities
                .Where(a => a.UserId == userId)
                .OrderBy(a => a.Date)
                .ThenBy(a => a.Time)
                .Select(a => new
                {
                    a.Id,
                    a.ActivityId,
                    a.Name,
                    a.Date,
                    a.Time,
                    a.Description,
                    a.Category,
                    a.Tags,
                    a.Latitude,
                    a.Longitude,
                    a.Address,
                    a.CreatedAt,
                    a.City  // ДОБАВЬТЕ ЭТО ПОЛЕ
                })
                .ToListAsync();

            return Ok(activities);
        }

        // GET: api/PlannedActivities/5
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetActivity(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return Unauthorized();
            }

            var activity = await _context.PlannedActivities
                .Where(a => a.UserId == userId && a.Id == id)
                .Select(a => new
                {
                    a.Id,
                    a.ActivityId,
                    a.Name,
                    a.Date,
                    a.Time,
                    a.Description,
                    a.Category,
                    a.Tags,
                    a.Latitude,
                    a.Longitude,
                    a.Address,
                    a.CreatedAt,
                    a.City  // ДОБАВЬТЕ ЭТО ПОЛЕ
                })
                .FirstOrDefaultAsync();

            if (activity == null)
            {
                return NotFound();
            }

            return Ok(activity);
        }

        // POST: api/PlannedActivities
        [HttpPost]
        public async Task<ActionResult<PlannedActivity>> CreateActivity([FromBody] PlannedActivityDto activityDto)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return Unauthorized();
            }

            // Преобразуем строку времени в TimeSpan
            TimeSpan time = TimeSpan.Zero;
            if (!string.IsNullOrEmpty(activityDto.Time))
            {
                TimeSpan.TryParse(activityDto.Time, out time);
            }

            var activity = new PlannedActivity
            {
                UserId = userId.Value,
                ActivityId = activityDto.ActivityId ?? Guid.NewGuid().ToString(),
                Name = activityDto.Name,
                Date = activityDto.Date,
                Time = time,
                Description = activityDto.Description ?? "",
                Category = activityDto.Category ?? "Другое",
                Tags = activityDto.Tags,
                Latitude = activityDto.Latitude,
                Longitude = activityDto.Longitude,
                Address = activityDto.Address ?? "",
                City = activityDto.City ?? "", // ДОБАВЬТЕ ЭТУ СТРОКУ
                CreatedAt = DateTime.UtcNow
            };

            _context.PlannedActivities.Add(activity);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Активность добавлена", id = activity.Id, city = activity.City });
        }

        // PUT: api/PlannedActivities/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateActivity(int id, [FromBody] PlannedActivityDto activityDto)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return Unauthorized();
            }

            var existingActivity = await _context.PlannedActivities
                .Where(a => a.UserId == userId && a.Id == id)
                .FirstOrDefaultAsync();

            if (existingActivity == null)
            {
                return NotFound();
            }

            // Обновляем поля
            if (!string.IsNullOrEmpty(activityDto.Name))
                existingActivity.Name = activityDto.Name;

            if (activityDto.Date != default)
                existingActivity.Date = activityDto.Date;

            // Преобразуем строку времени в TimeSpan
            if (!string.IsNullOrEmpty(activityDto.Time))
            {
                if (TimeSpan.TryParse(activityDto.Time, out TimeSpan parsedTime))
                {
                    existingActivity.Time = parsedTime;
                }
            }

            existingActivity.Description = activityDto.Description ?? existingActivity.Description;
            existingActivity.Category = activityDto.Category ?? existingActivity.Category;

            // Обновляем остальные поля, если они переданы
            if (activityDto.Tags != null)
                existingActivity.Tags = activityDto.Tags;

            if (activityDto.Latitude.HasValue)
                existingActivity.Latitude = activityDto.Latitude;

            if (activityDto.Longitude.HasValue)
                existingActivity.Longitude = activityDto.Longitude;

            if (!string.IsNullOrEmpty(activityDto.Address))
                existingActivity.Address = activityDto.Address;

            // ДОБАВЬТЕ ОБНОВЛЕНИЕ ГОРОДА
            if (!string.IsNullOrEmpty(activityDto.City))
                existingActivity.City = activityDto.City;

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Активность обновлена", city = existingActivity.City });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // DELETE: api/PlannedActivities/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteActivity(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return Unauthorized();
            }

            var activity = await _context.PlannedActivities
                .Where(a => a.UserId == userId && a.Id == id)
                .FirstOrDefaultAsync();

            if (activity == null)
            {
                return NotFound();
            }

            _context.PlannedActivities.Remove(activity);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/PlannedActivities/clear
        [HttpDelete("clear")]
        public async Task<IActionResult> ClearAllActivities()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return Unauthorized();
            }

            var activities = await _context.PlannedActivities
                .Where(a => a.UserId == userId)
                .ToListAsync();

            _context.PlannedActivities.RemoveRange(activities);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}