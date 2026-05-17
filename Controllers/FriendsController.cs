// Controllers/FriendsController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWise.Models;
using TripWise.Models.DTOs;
using System.Text.Json;

namespace TripWise.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FriendsController : ControllerBase
    {
        private readonly TripWiseContext _context;
        private readonly ILogger<FriendsController> _logger;

        public FriendsController(TripWiseContext context, ILogger<FriendsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/Friends/GetFriends
        [HttpGet("GetFriends")]
        public async Task<ActionResult<ApiResponse<List<FriendDto>>>> GetFriends()
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Unauthorized(new ApiResponse<List<FriendDto>>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                var friends = await _context.Friends
                    .Include(f => f.FriendUser)
                    .Where(f => f.UserId == userId && f.Status == "accepted")
                    .Select(f => new FriendDto
                    {
                        Id = f.Id,
                        FriendId = f.FriendId,
                        FullName = f.FriendUser.LastName + " " + f.FriendUser.FirstName +
                                  (string.IsNullOrEmpty(f.FriendUser.MiddleName) ? "" : " " + f.FriendUser.MiddleName),
                        FirstName = f.FriendUser.FirstName,
                        LastName = f.FriendUser.LastName,
                        MiddleName = f.FriendUser.MiddleName,
                        Email = f.FriendUser.Email,
                        AvatarPath = f.FriendUser.AvatarPath,
                        Status = f.Status,
                        CreatedAt = f.CreatedAt,
                        AcceptedAt = f.AcceptedAt
                    })
                    .OrderBy(f => f.FullName)
                    .ToListAsync();

                return Ok(new ApiResponse<List<FriendDto>>
                {
                    Success = true,
                    Data = friends
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка друзей");
                return StatusCode(500, new ApiResponse<List<FriendDto>>
                {
                    Success = false,
                    Message = "Ошибка при загрузке друзей"
                });
            }
        }

        // GET: api/Friends/GetFriendRequests
        [HttpGet("GetFriendRequests")]
        public async Task<ActionResult<ApiResponse<List<FriendRequestDto>>>> GetFriendRequests()
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Unauthorized(new ApiResponse<List<FriendRequestDto>>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                var requests = await _context.FriendRequests
                    .Include(r => r.Sender)
                    .Where(r => r.ReceiverId == userId && r.Status == "pending")
                    .OrderByDescending(r => r.SentAt)
                    .Select(r => new FriendRequestDto
                    {
                        Id = r.Id,
                        SenderId = r.SenderId,
                        SenderName = r.Sender.LastName + " " + r.Sender.FirstName,
                        SenderAvatar = r.Sender.AvatarPath,
                        Message = r.Message,
                        SentAt = r.SentAt,
                        Status = r.Status
                    })
                    .ToListAsync();

                return Ok(new ApiResponse<List<FriendRequestDto>>
                {
                    Success = true,
                    Data = requests
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении запросов в друзья");
                return StatusCode(500, new ApiResponse<List<FriendRequestDto>>
                {
                    Success = false,
                    Message = "Ошибка при загрузке запросов"
                });
            }
        }

        // POST: api/Friends/SendFriendRequest
        [HttpPost("SendFriendRequest")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult<ApiResponse<object>>> SendFriendRequest([FromBody] int friendId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                if (friendId == userId)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Нельзя добавить себя в друзья"
                    });
                }

                // Проверяем, не друзья ли уже
                var existingFriend = await _context.Friends
                    .FirstOrDefaultAsync(f =>
                        (f.UserId == userId && f.FriendId == friendId) ||
                        (f.UserId == friendId && f.FriendId == userId));

                if (existingFriend != null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Вы уже друзья"
                    });
                }

                // Проверяем, есть ли уже запрос (только pending)
                var existingRequest = await _context.FriendRequests
                    .FirstOrDefaultAsync(r =>
                        (r.SenderId == userId && r.ReceiverId == friendId) ||
                        (r.SenderId == friendId && r.ReceiverId == userId));

                if (existingRequest != null)
                {
                    // Если запрос от другого пользователя к вам - автоматически принимаем
                    if (existingRequest.SenderId == friendId && existingRequest.ReceiverId == userId)
                    {
                        // Автоматически принимаем запрос
                        return await AcceptFriendRequest(existingRequest.Id);
                    }

                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Запрос уже существует"
                    });
                }

                // Создаем запрос
                var request = new FriendRequest
                {
                    SenderId = userId.Value,
                    ReceiverId = friendId,
                    SentAt = DateTime.UtcNow
                };

                _context.FriendRequests.Add(request);
                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Запрос отправлен"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отправке запроса в друзья");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при отправке запроса"
                });
            }
        }

        // POST: api/Friends/AcceptFriendRequest
        [HttpPost("AcceptFriendRequest")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult<ApiResponse<object>>> AcceptFriendRequest([FromBody] int requestId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                var request = await _context.FriendRequests
                    .FirstOrDefaultAsync(r => r.Id == requestId && r.ReceiverId == userId);

                if (request == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Запрос не найден"
                    });
                }

                // Создаем двустороннюю дружбу
                _context.Friends.Add(new Friend
                {
                    UserId = request.SenderId,
                    FriendId = request.ReceiverId,
                    Status = "accepted",
                    CreatedAt = DateTime.UtcNow,
                    AcceptedAt = DateTime.UtcNow
                });

                _context.Friends.Add(new Friend
                {
                    UserId = request.ReceiverId,
                    FriendId = request.SenderId,
                    Status = "accepted",
                    CreatedAt = DateTime.UtcNow,
                    AcceptedAt = DateTime.UtcNow
                });

                // УДАЛЯЕМ ЗАПРОС (вместо обновления статуса)
                _context.FriendRequests.Remove(request);

                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Запрос принят"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при принятии запроса");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при принятии запроса"
                });
            }
        }

        // POST: api/Friends/RejectFriendRequest
        [HttpPost("RejectFriendRequest")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult<ApiResponse<object>>> RejectFriendRequest([FromBody] int requestId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                var request = await _context.FriendRequests
                    .FirstOrDefaultAsync(r => r.Id == requestId && r.ReceiverId == userId);

                if (request == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Запрос не найден"
                    });
                }

                // УДАЛЯЕМ ЗАПРОС (вместо обновления статуса)
                _context.FriendRequests.Remove(request);

                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Запрос отклонен"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отклонении запроса");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при отклонении запроса"
                });
            }
        }

        // POST: api/Friends/RemoveFriend
        [HttpPost("RemoveFriend")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult<ApiResponse<object>>> RemoveFriend([FromBody] int friendId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                // Удаляем записи о дружбе
                var friendships = await _context.Friends
                    .Where(f => (f.UserId == userId && f.FriendId == friendId) ||
                               (f.UserId == friendId && f.FriendId == userId))
                    .ToListAsync();

                if (friendships.Any())
                {
                    _context.Friends.RemoveRange(friendships);
                }

                // НАХОДИМ И УДАЛЯЕМ ЗАПРОС В ДРУЗЬЯ (если есть)
                var friendRequest = await _context.FriendRequests
                    .FirstOrDefaultAsync(r =>
                        (r.SenderId == userId && r.ReceiverId == friendId) ||
                        (r.SenderId == friendId && r.ReceiverId == userId));

                if (friendRequest != null)
                {
                    _context.FriendRequests.Remove(friendRequest);
                }

                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Друг удален"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении друга");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при удалении друга"
                });
            }
        }

        // GET: api/Friends/SearchUsers
        [HttpGet("SearchUsers")]
        public async Task<ActionResult<ApiResponse<List<SearchUsersResponse>>>> SearchUsers(string term)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Unauthorized(new ApiResponse<List<SearchUsersResponse>>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
                {
                    return Ok(new ApiResponse<List<SearchUsersResponse>>
                    {
                        Success = true,
                        Data = new List<SearchUsersResponse>()
                    });
                }

                // Получаем всех друзей пользователя
                var friendIds = await _context.Friends
                    .Where(f => f.UserId == userId && f.Status == "accepted")
                    .Select(f => f.FriendId)
                    .ToListAsync();

                // Получаем отправленные запросы
                var sentRequests = await _context.FriendRequests
                    .Where(r => r.SenderId == userId && r.Status == "pending")
                    .Select(r => r.ReceiverId)
                    .ToListAsync();

                // Получаем полученные запросы
                var receivedRequests = await _context.FriendRequests
                    .Where(r => r.ReceiverId == userId && r.Status == "pending")
                    .Select(r => r.SenderId)
                    .ToListAsync();

                var users = await _context.Users
                    .Where(u => u.IdUser != userId &&
                        (u.Email.Contains(term) ||
                         u.FirstName.Contains(term) ||
                         u.LastName.Contains(term) ||
                         (u.FirstName + " " + u.LastName).Contains(term)))
                    .Select(u => new SearchUsersResponse
                    {
                        Id = u.IdUser,
                        FullName = u.LastName + " " + u.FirstName +
                            (string.IsNullOrEmpty(u.MiddleName) ? "" : " " + u.MiddleName),
                        FirstName = u.FirstName ?? "",
                        LastName = u.LastName ?? "",
                        Email = u.Email ?? "",
                        AvatarPath = u.AvatarPath,
                        IsFriend = friendIds.Contains(u.IdUser),
                        FriendStatus = friendIds.Contains(u.IdUser) ? "friend" :
                                       sentRequests.Contains(u.IdUser) ? "pending_sent" :
                                       receivedRequests.Contains(u.IdUser) ? "pending_received" : "none"
                    })
                    .Take(20)
                    .ToListAsync();

                return Ok(new ApiResponse<List<SearchUsersResponse>>
                {
                    Success = true,
                    Data = users
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске пользователей");
                return StatusCode(500, new ApiResponse<List<SearchUsersResponse>>
                {
                    Success = false,
                    Message = "Ошибка при поиске пользователей"
                });
            }
        }
    }
}