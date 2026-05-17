using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWise.Models;
using TripWise.Models.DTOs;
using System.Security.Claims;
using Microsoft.Data.SqlClient;

namespace TripWise.Controllers
{
    public class TripsController : Controller
    {
        private readonly TripWiseContext _context;
        private readonly ILogger<TripsController> _logger;

        public TripsController(TripWiseContext context, ILogger<TripsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Trips
        public IActionResult Index()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }
            return View();
        }

        // GET: /Trips/GetUserTrips
        [HttpGet]
        public async Task<IActionResult> GetUserTrips()
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<List<TripListDto>>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                _logger.LogInformation("GetUserTrips для пользователя {UserId}", userId);

                // Получаем все поездки, где пользователь является участником
                var userTripIds = await _context.TripParticipants
                    .Where(tp => tp.IdUser == userId)
                    .Select(tp => tp.IdTrip)
                    .ToListAsync();

                var now = DateTime.UtcNow;

                // Загружаем поездки с полной информацией
                var trips = await _context.Trips
                    .Where(t => userTripIds.Contains(t.IdTrip))
                    .Include(t => t.CreatedBy)
                    .Include(t => t.TripParticipants)
                        .ThenInclude(tp => tp.IdUserNavigation)
                    .Include(t => t.TripParticipants)
                        .ThenInclude(tp => tp.IdParticipantRoleNavigation)
                    .Include(t => t.PointsOfInterests)
                        .ThenInclude(p => p.IdInterestCategoryNavigation)
                    .Include(t => t.Expenses)
                    .ToListAsync();

                // Получаем чаты для поездок отдельным запросом
                var tripChats = await _context.Chats
                    .Where(c => c.IdTrip.HasValue && userTripIds.Contains(c.IdTrip.Value))
                    .Select(c => new { c.IdTrip, c.IdChat })
                    .ToDictionaryAsync(c => c.IdTrip.Value, c => c.IdChat);

                // Формируем DTO
                var tripDtos = trips.Select(t =>
                {
                    // Определяем статус поездки
                    string status;
                    if (t.EndDate < now)
                        status = "completed";
                    else if (t.StartDate <= now && t.EndDate >= now)
                        status = "active";
                    else
                        status = "upcoming";

                    // Получаем список участников с информацией о друзьях
                    var participants = t.TripParticipants.Select(tp => new TripParticipantDto
                    {
                        UserId = tp.IdUser,
                        FullName = $"{tp.IdUserNavigation?.LastName ?? ""} {tp.IdUserNavigation?.FirstName ?? ""}".Trim(),
                        AvatarPath = tp.IdUserNavigation?.AvatarPath,
                        Role = tp.IdParticipantRoleNavigation?.ParticipantRole1 ?? "Участник", // ИСПРАВЛЕНО: ParticipantRole1
                        IsFriend = _context.Friends.Any(f =>
                            (f.UserId == userId && f.FriendId == tp.IdUser && f.Status == "accepted") ||
                            (f.UserId == tp.IdUser && f.FriendId == userId && f.Status == "accepted"))
                    }).ToList();

                    return new TripListDto
                    {
                        Id = t.IdTrip,
                        Title = t.Title ?? "Без названия",
                        Description = t.Description,
                        StartDate = t.StartDate,
                        EndDate = t.EndDate,
                        TotalBudget = t.TotalBudget,
                        Status = status,
                        ParticipantCount = participants.Count(), // ИСПРАВЛЕНО: добавили ()
                        Participants = participants,
                        ChatId = tripChats.ContainsKey(t.IdTrip) ? tripChats[t.IdTrip] : (int?)null,
                        CoverImage = GetTripCoverImage(t),
                        CreatedAt = t.CreatedAt,
                        CreatedBy = new TripCreatorDto
                        {
                            Id = t.CreatedBy?.IdUser ?? 0,
                            FullName = t.CreatedBy != null
                                ? $"{t.CreatedBy.LastName} {t.CreatedBy.FirstName}".Trim()
                                : "Система",
                            AvatarPath = t.CreatedBy?.AvatarPath
                        },
                        PointsCount = t.PointsOfInterests?.Count() ?? 0, // ИСПРАВЛЕНО: добавили ()
                        SpentBudget = t.Expenses?.Sum(e => e.Amount) ?? 0
                    };
                }).ToList();

                // Разделяем на предстоящие и завершенные
                var upcomingTrips = tripDtos.Where(t => t.Status != "completed").OrderBy(t => t.StartDate).ToList();
                var completedTrips = tripDtos.Where(t => t.Status == "completed").OrderByDescending(t => t.EndDate).ToList();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Data = new
                    {
                        upcoming = upcomingTrips,
                        completed = completedTrips,
                        all = tripDtos
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении поездок пользователя");
                return Json(new ApiResponse<List<TripListDto>>
                {
                    Success = false,
                    Message = "Ошибка при загрузке поездок: " + ex.Message
                });
            }
        }

        // POST: /Trips/CreateTrip
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTrip([FromBody] CreateTripRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                // Валидация
                if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length < 3 || request.Title.Length > 50)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Название должно быть от 3 до 50 символов"
                    });
                }

                if (request.Description != null && request.Description.Length > 100)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Описание не может превышать 100 символов"
                    });
                }

                if (request.TotalBudget < 0)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Бюджет не может быть отрицательным"
                    });
                }

                if (request.TotalBudget.ToString().Length > 10)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Бюджет не может превышать 10 знаков"
                    });
                }

                _logger.LogInformation("CreateTrip: userId={UserId}, title={Title}", userId, request.Title);

                // Проверяем даты
                if (request.EndDate <= request.StartDate)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Дата окончания должна быть позже даты начала"
                    });
                }

                // Создаем поездку
                var trip = new Trip
                {
                    Title = request.Title,
                    Description = request.Description,
                    StartDate = request.StartDate.ToUniversalTime(),
                    EndDate = request.EndDate.ToUniversalTime(),
                    TotalBudget = request.TotalBudget,
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = userId.Value
                };

                _context.Trips.Add(trip);
                await _context.SaveChangesAsync();

                // Добавляем создателя как участника
                var participant = new TripParticipant
                {
                    IdTrip = trip.IdTrip,
                    IdUser = userId.Value,
                    IdParticipantRole = 1, // Организатор
                    JoinedAt = DateTime.UtcNow
                };
                _context.TripParticipants.Add(participant);

                // Если поездка публичная, создаем чат для нее
                Chat? chat = null;
                if (request.IsPublic)
                {
                    chat = new Chat
                    {
                        Name = $"Чат: {trip.Title}",
                        Type = "trip",
                        IdTrip = trip.IdTrip,
                        CreatedById = userId.Value,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Chats.Add(chat);
                    await _context.SaveChangesAsync();

                    // Добавляем создателя в чат
                    _context.ChatMembers.Add(new ChatMember
                    {
                        ChatId = chat.IdChat,
                        UserId = userId.Value,
                        Role = "admin",
                        JoinedAt = DateTime.UtcNow
                    });
                }

                // Приглашаем друзей, если указаны
                // В методе CreateTrip замените блок приглашения друзей на:

                // Приглашаем друзей, если указаны (отправляем приглашения, а не добавляем сразу)
                if (request.InvitedFriends != null && request.InvitedFriends.Any())
                {
                    foreach (var friendId in request.InvitedFriends.Distinct())
                    {
                        if (friendId != userId.Value)
                        {
                            // Создаем приглашение, а не добавляем сразу в участники
                            var invitation = new TripInvitation
                            {
                                IdTrip = trip.IdTrip,
                                InviterId = userId.Value,
                                InvitedId = friendId,
                                Message = $"Приглашаю вас в поездку \"{trip.Title}\"",
                                InvitedAt = DateTime.UtcNow,
                                Status = "pending"
                            };
                            _context.TripInvitations.Add(invitation);
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                await _context.SaveChangesAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Поездка успешно создана",
                    Data = new { tripId = trip.IdTrip }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании поездки");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при создании поездки: " + ex.Message
                });
            }
        }

        // POST: /Trips/InviteFriends (обновленный)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InviteFriends([FromBody] InviteFriendsRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                _logger.LogInformation("InviteFriends: tripId={TripId}, userId={UserId}", request.TripId, userId);

                // Проверяем, является ли пользователь организатором поездки
                var isOrganizer = await _context.TripParticipants
                    .AnyAsync(tp => tp.IdTrip == request.TripId &&
                                   tp.IdUser == userId &&
                                   tp.IdParticipantRole == 1);

                if (!isOrganizer)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Только организатор может приглашать друзей"
                    });
                }

                var results = new List<string>();
                var successCount = 0;

                foreach (var friendId in request.FriendIds.Distinct())
                {
                    // Проверяем, не является ли друг уже участником
                    var isParticipant = await _context.TripParticipants
                        .AnyAsync(tp => tp.IdTrip == request.TripId && tp.IdUser == friendId);

                    if (isParticipant)
                    {
                        results.Add($"Пользователь уже в поездке");
                        continue;
                    }

                    // Проверяем, нет ли уже активного приглашения
                    var existingInvitation = await _context.TripInvitations
                        .FirstOrDefaultAsync(i => i.IdTrip == request.TripId &&
                                                  i.InvitedId == friendId &&
                                                  i.Status == "pending");

                    if (existingInvitation != null)
                    {
                        results.Add($"Приглашение уже отправлено");
                        continue;
                    }

                    // Создаем приглашение
                    var invitation = new TripInvitation
                    {
                        IdTrip = request.TripId,
                        InviterId = userId.Value,
                        InvitedId = friendId,
                        Message = request.Message,
                        InvitedAt = DateTime.UtcNow,
                        Status = "pending"
                    };

                    _context.TripInvitations.Add(invitation);
                    successCount++;
                }

                await _context.SaveChangesAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = $"Приглашения отправлены {successCount} пользователям"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при приглашении друзей");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при приглашении друзей: " + ex.Message
                });
            }
        }

        // GET: /Trips/GetTripDetails/5
        [HttpGet]
        public async Task<IActionResult> GetTripDetails(int id)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<TripDetailDto>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                // Проверяем, является ли пользователь участником
                var isParticipant = await _context.TripParticipants
                    .AnyAsync(tp => tp.IdTrip == id && tp.IdUser == userId);

                if (!isParticipant)
                {
                    return Json(new ApiResponse<TripDetailDto>
                    {
                        Success = false,
                        Message = "У вас нет доступа к этой поездке"
                    });
                }

                var trip = await _context.Trips
                    .Include(t => t.CreatedBy)
                    .Include(t => t.TripParticipants)
                        .ThenInclude(tp => tp.IdUserNavigation)
                    .Include(t => t.TripParticipants)
                        .ThenInclude(tp => tp.IdParticipantRoleNavigation)
                    .Include(t => t.PointsOfInterests)
                        .ThenInclude(p => p.IdInterestCategoryNavigation)
                    .Include(t => t.Expenses)
                        .ThenInclude(e => e.IdExpenseCategoryNavigation)
                    .FirstOrDefaultAsync(t => t.IdTrip == id);

                if (trip == null)
                {
                    return Json(new ApiResponse<TripDetailDto>
                    {
                        Success = false,
                        Message = "Поездка не найдена"
                    });
                }

                // Получаем чат поездки
                var tripChat = await _context.Chats
                    .Include(c => c.Messages.OrderByDescending(m => m.SentAt).Take(5))
                    .FirstOrDefaultAsync(c => c.IdTrip == id && c.Type == "trip");

                var now = DateTime.UtcNow;
                string status;
                if (trip.EndDate < now)
                    status = "completed";
                else if (trip.StartDate <= now && trip.EndDate >= now)
                    status = "active";
                else
                    status = "upcoming";

                var participants = trip.TripParticipants.Select(tp => new TripParticipantDto
                {
                    UserId = tp.IdUser,
                    FullName = $"{tp.IdUserNavigation?.LastName ?? ""} {tp.IdUserNavigation?.FirstName ?? ""}".Trim(),
                    AvatarPath = tp.IdUserNavigation?.AvatarPath,
                    Role = tp.IdParticipantRoleNavigation?.ParticipantRole1 ?? "Участник", // ИСПРАВЛЕНО: ParticipantRole1
                    IsFriend = _context.Friends.Any(f =>
                        (f.UserId == userId && f.FriendId == tp.IdUser && f.Status == "accepted") ||
                        (f.UserId == tp.IdUser && f.FriendId == userId && f.Status == "accepted"))
                }).ToList();

                var points = trip.PointsOfInterests?.Select(p => new PointOfInterestDto
                {
                    Id = p.IdPoint,
                    Name = p.Name ?? "Без названия",
                    Description = p.Description,
                    Cost = p.Cost,
                    PlannedDate = p.PlannedDate,
                    Category = p.IdInterestCategoryNavigation?.InterestCategory1 ?? "Другое" // ИСПРАВЛЕНО: InterestCategory1
                }).ToList() ?? new List<PointOfInterestDto>();

                var expenses = trip.Expenses?.Select(e => new ExpenseDto
                {
                    Id = e.IdExpense,
                    Title = e.Title ?? "Без названия",
                    Amount = e.Amount,
                    Category = e.IdExpenseCategoryNavigation?.ExpenseCategoryName ?? "Другое",
                    Date = e.ExpenseDate,
                    PaidBy = _context.Users
                        .Where(u => u.IdUser == e.PaidById)
                        .Select(u => $"{u.LastName} {u.FirstName}".Trim())
                        .FirstOrDefault() ?? "Неизвестно"
                }).ToList() ?? new List<ExpenseDto>();

                var recentMessages = tripChat?.Messages?.Select(m => new TripMessageDto
                {
                    Id = m.IdMessage,
                    Text = m.Message ?? "",
                    SenderName = _context.Users
                        .Where(u => u.IdUser == m.SenderId)
                        .Select(u => $"{u.LastName} {u.FirstName}".Trim())
                        .FirstOrDefault() ?? "Пользователь",
                    SentAt = m.SentAt
                }).ToList() ?? new List<TripMessageDto>();

                var dto = new TripDetailDto
                {
                    Id = trip.IdTrip,
                    Title = trip.Title ?? "Без названия",
                    Description = trip.Description,
                    StartDate = trip.StartDate,
                    EndDate = trip.EndDate,
                    TotalBudget = trip.TotalBudget,
                    Status = status,
                    ParticipantCount = participants.Count(), // ИСПРАВЛЕНО: добавили ()
                    Participants = participants,
                    ChatId = tripChat?.IdChat,
                    CoverImage = GetTripCoverImage(trip),
                    CreatedAt = trip.CreatedAt,
                    CreatedBy = new TripCreatorDto
                    {
                        Id = trip.CreatedBy?.IdUser ?? 0,
                        FullName = trip.CreatedBy != null
                            ? $"{trip.CreatedBy.LastName} {trip.CreatedBy.FirstName}".Trim()
                            : "Система",
                        AvatarPath = trip.CreatedBy?.AvatarPath
                    },
                    PointsCount = points.Count(), // ИСПРАВЛЕНО: добавили ()
                    SpentBudget = expenses.Sum(e => e.Amount),
                    Points = points,
                    Expenses = expenses.OrderByDescending(e => e.Date).ToList(),
                    RecentMessages = recentMessages
                };

                return Json(new ApiResponse<TripDetailDto>
                {
                    Success = true,
                    Data = dto
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении деталей поездки {TripId}", id);
                return Json(new ApiResponse<TripDetailDto>
                {
                    Success = false,
                    Message = "Ошибка при загрузке деталей поездки: " + ex.Message
                });
            }
        }

        // GET: /Trips/GetFriendsForInvite
        [HttpGet]
        public async Task<IActionResult> GetFriendsForInvite(int tripId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                // Получаем текущих участников поездки
                var currentParticipants = await _context.TripParticipants
                    .Where(tp => tp.IdTrip == tripId)
                    .Select(tp => tp.IdUser)
                    .ToListAsync();

                // Получаем друзей, которые еще не в поездке
                var friends = await _context.Friends
                    .Include(f => f.FriendUser)
                    .Where(f => f.UserId == userId && f.Status == "accepted")
                    .Select(f => new
                    {
                        f.FriendId,
                        FullName = f.FriendUser.LastName + " " + f.FriendUser.FirstName,
                        f.FriendUser.AvatarPath,
                        IsInTrip = currentParticipants.Contains(f.FriendId)
                    })
                    .Where(f => !f.IsInTrip)
                    .OrderBy(f => f.FullName)
                    .ToListAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Data = friends
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении друзей для приглашения");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при загрузке друзей"
                });
            }
        }

        private string GetTripCoverImage(Trip trip)
        {
            // Здесь можно добавить логику для получения обложки поездки
            // Например, из первой точки интереса или загруженного изображения
            return null;
        }
        // POST: /Trips/DeleteTrip/5?deleteChat=true
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTrip(int id, [FromQuery] bool deleteChat = true)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                _logger.LogInformation("DeleteTrip: tripId={TripId}, userId={UserId}, deleteChat={DeleteChat}",
                    id, userId, deleteChat);

                // Находим поездку
                var trip = await _context.Trips
                    .Include(t => t.TripParticipants)
                    .FirstOrDefaultAsync(t => t.IdTrip == id);

                if (trip == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Поездка не найдена"
                    });
                }

                // Проверяем, является ли пользователь создателем поездки
                if (trip.CreatedById != userId)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Только создатель может удалить поездку"
                    });
                }

                // Получаем связанные чаты (типа "trip")
                var tripChats = await _context.Chats
                    .Where(c => c.IdTrip == id && c.Type == "trip")
                    .ToListAsync();

                // Удаляем чаты только если пользователь выбрал эту опцию
                if (deleteChat && tripChats != null && tripChats.Any())
                {
                    _context.Chats.RemoveRange(tripChats);
                    _logger.LogInformation("Удалено {Count} чатов для поездки {TripId}", tripChats.Count, id);
                }
                else if (tripChats != null && tripChats.Any())
                {
                    // Если чаты не удаляем, отвязываем их от поездки
                    foreach (var chat in tripChats)
                    {
                        chat.IdTrip = null;
                    }
                    _logger.LogInformation("Чаты отвязаны от поездки {TripId}", id);
                }

                // Удаляем поездку
                _context.Trips.Remove(trip);
                await _context.SaveChangesAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = deleteChat
                        ? "Поездка и связанный чат успешно удалены"
                        : "Поездка успешно удалена, чат сохранен"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении поездки {TripId}", id);
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при удалении поездки: " + ex.Message
                });
            }
        }
        // POST: /Trips/UpdateTrip
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateTrip([FromBody] UpdateTripRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                _logger.LogInformation("UpdateTrip: tripId={TripId}, userId={UserId}", request.Id, userId);

                // Находим поездку
                var trip = await _context.Trips
                    .FirstOrDefaultAsync(t => t.IdTrip == request.Id);

                if (trip == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Поездка не найдена"
                    });
                }

                // Проверяем, является ли пользователь создателем
                if (trip.CreatedById != userId)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Только создатель может редактировать поездку"
                    });
                }

                // Проверяем, что поездка не завершена
                var now = DateTime.UtcNow;
                if (trip.EndDate < now)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Нельзя редактировать завершенные поездки"
                    });
                }

                // Проверяем даты
                if (request.EndDate <= request.StartDate)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Дата окончания должна быть позже даты начала"
                    });
                }

                // Обновляем данные поездки
                trip.Title = request.Title;
                trip.Description = request.Description;
                trip.StartDate = request.StartDate.ToUniversalTime();
                trip.EndDate = request.EndDate.ToUniversalTime();
                trip.TotalBudget = request.TotalBudget;

                await _context.SaveChangesAsync();

                // Проверяем, есть ли уже чат у поездки
                var existingChat = await _context.Chats
                    .FirstOrDefaultAsync(c => c.IdTrip == request.Id && c.Type == "trip");

                // Если пользователь хочет публичный чат
                if (request.IsPublic)
                {
                    if (existingChat == null)
                    {
                        // Создаем новый чат
                        var newChat = new Chat
                        {
                            Name = $"Чат: {trip.Title}",
                            Type = "trip",
                            IdTrip = trip.IdTrip,
                            CreatedById = userId.Value,
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.Chats.Add(newChat);
                        await _context.SaveChangesAsync();

                        // Добавляем создателя в чат
                        _context.ChatMembers.Add(new ChatMember
                        {
                            ChatId = newChat.IdChat,
                            UserId = userId.Value,
                            Role = "admin",
                            JoinedAt = DateTime.UtcNow
                        });

                        // Добавляем всех участников поездки в чат
                        var participants = await _context.TripParticipants
                            .Where(tp => tp.IdTrip == trip.IdTrip && tp.IdUser != userId)
                            .Select(tp => tp.IdUser)
                            .ToListAsync();

                        foreach (var participantId in participants)
                        {
                            _context.ChatMembers.Add(new ChatMember
                            {
                                ChatId = newChat.IdChat,
                                UserId = participantId,
                                Role = "member",
                                JoinedAt = DateTime.UtcNow
                            });
                        }

                        await _context.SaveChangesAsync();

                        _logger.LogInformation("Создан новый чат для поездки {TripId}", trip.IdTrip);
                    }
                    else
                    {
                        // Обновляем название существующего чата
                        existingChat.Name = $"Чат: {trip.Title}";
                        await _context.SaveChangesAsync();
                    }
                }
                else
                {
                    // Если пользователь не хочет публичный чат, но чат существует - ничего не делаем
                    // Чат остается, но его можно будет удалить отдельно
                    if (existingChat != null)
                    {
                        _logger.LogInformation("Чат для поездки {TripId} существует, но оставлен по желанию пользователя", trip.IdTrip);
                    }
                }

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = existingChat == null && request.IsPublic
                        ? "Поездка обновлена и создан новый чат"
                        : "Поездка успешно обновлена"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении поездки {TripId}", request?.Id);
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при обновлении поездки: " + ex.Message
                });
            }
        }

        // GET: /Trips/GetTripForEdit/5
        [HttpGet]
        public async Task<IActionResult> GetTripForEdit(int id)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                var trip = await _context.Trips
                    .FirstOrDefaultAsync(t => t.IdTrip == id);

                if (trip == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Поездка не найдена"
                    });
                }

                // Проверяем, является ли пользователь создателем
                if (trip.CreatedById != userId)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Только создатель может редактировать поездку"
                    });
                }

                // Проверяем, что поездка не завершена
                var now = DateTime.UtcNow;
                if (trip.EndDate < now)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Нельзя редактировать завершенные поездки"
                    });
                }

                // Проверяем, есть ли у поездки чат
                var hasChat = await _context.Chats
                    .AnyAsync(c => c.IdTrip == id && c.Type == "trip");

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Data = new
                    {
                        trip.IdTrip,
                        trip.Title,
                        trip.Description,
                        StartDate = trip.StartDate.ToString("yyyy-MM-dd"),
                        EndDate = trip.EndDate.ToString("yyyy-MM-dd"),
                        trip.TotalBudget,
                        HasChat = hasChat,
                        IsPublic = hasChat
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении данных для редактирования поездки {TripId}", id);
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при загрузке данных: " + ex.Message
                });
            }
        }
        // GET: /Trips/GetTripParticipants/5
        [HttpGet]
        public async Task<IActionResult> GetTripParticipants(int tripId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                // Проверяем, является ли пользователь участником поездки
                var isParticipant = await _context.TripParticipants
                    .AnyAsync(tp => tp.IdTrip == tripId && tp.IdUser == userId);

                if (!isParticipant)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "У вас нет доступа к этой поездке"
                    });
                }

                // Получаем всех участников
                var participants = await _context.TripParticipants
                    .Include(tp => tp.IdUserNavigation)
                    .Include(tp => tp.IdParticipantRoleNavigation)
                    .Where(tp => tp.IdTrip == tripId)
                    .Select(tp => new TripParticipantManageDto
                    {
                        UserId = tp.IdUser,
                        FullName = (tp.IdUserNavigation.LastName + " " + tp.IdUserNavigation.FirstName).Trim(),
                        AvatarPath = tp.IdUserNavigation.AvatarPath,
                        Role = tp.IdParticipantRoleNavigation != null
                            ? tp.IdParticipantRoleNavigation.ParticipantRole1
                            : "Участник",
                        IsFriend = _context.Friends.Any(f =>
                            (f.UserId == userId && f.FriendId == tp.IdUser && f.Status == "accepted") ||
                            (f.UserId == tp.IdUser && f.FriendId == userId && f.Status == "accepted")),
                        IsCreator = tp.IdParticipantRole == 1, // 1 - организатор
                        IsCurrentUser = tp.IdUser == userId,
                        JoinedAt = tp.JoinedAt
                    })
                    .OrderByDescending(p => p.IsCreator)
                    .ThenBy(p => p.FullName)
                    .ToListAsync();

                // Получаем чат поездки
                var tripChat = await _context.Chats
                    .FirstOrDefaultAsync(c => c.IdTrip == tripId && c.Type == "trip");

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Data = new
                    {
                        participants = participants,
                        chatId = tripChat?.IdChat,
                        currentUserId = userId
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении участников поездки {TripId}", tripId);
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при загрузке участников: " + ex.Message
                });
            }
        }

        // POST: /Trips/RemoveParticipant
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveParticipant([FromBody] ManageParticipantRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                _logger.LogInformation("RemoveParticipant: tripId={TripId}, userIdToRemove={UserIdToRemove}, currentUser={CurrentUserId}",
                    request.TripId, request.UserId, userId);

                // Проверяем, существует ли поездка
                var trip = await _context.Trips
                    .FirstOrDefaultAsync(t => t.IdTrip == request.TripId);

                if (trip == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Поездка не найдена"
                    });
                }

                // Проверяем, является ли текущий пользователь организатором
                var isCreator = await _context.TripParticipants
                    .AnyAsync(tp => tp.IdTrip == request.TripId && tp.IdUser == userId && tp.IdParticipantRole == 1);

                // Если удаляемый пользователь - это текущий пользователь
                if (request.UserId == userId)
                {
                    // Организатор не может удалить сам себя
                    if (isCreator)
                    {
                        return Json(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Организатор не может покинуть поездку. Сначала назначьте нового организатора или удалите поездку."
                        });
                    }

                    // Удаляем участника из поездки
                    var participant = await _context.TripParticipants
                        .FirstOrDefaultAsync(tp => tp.IdTrip == request.TripId && tp.IdUser == userId);

                    if (participant != null)
                    {
                        _context.TripParticipants.Remove(participant);
                    }

                    // Удаляем из чата поездки
                    var chat = await _context.Chats
                        .FirstOrDefaultAsync(c => c.IdTrip == request.TripId && c.Type == "trip");

                    if (chat != null)
                    {
                        var chatMember = await _context.ChatMembers
                            .FirstOrDefaultAsync(cm => cm.ChatId == chat.IdChat && cm.UserId == userId);

                        if (chatMember != null)
                        {
                            _context.ChatMembers.Remove(chatMember);
                        }
                    }

                    await _context.SaveChangesAsync();

                    return Json(new ApiResponse<object>
                    {
                        Success = true,
                        Message = "Вы покинули поездку"
                    });
                }

                // Удаление другого участника (только для организатора)
                if (!isCreator)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Только организатор может удалять других участников"
                    });
                }

                // Проверяем, существует ли участник
                var participantToRemove = await _context.TripParticipants
                    .FirstOrDefaultAsync(tp => tp.IdTrip == request.TripId && tp.IdUser == request.UserId);

                if (participantToRemove == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Участник не найден"
                    });
                }

                // Нельзя удалить организатора
                if (participantToRemove.IdParticipantRole == 1)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Нельзя удалить организатора поездки"
                    });
                }

                // Удаляем участника из поездки
                _context.TripParticipants.Remove(participantToRemove);

                // Удаляем из чата поездки
                var chatToRemove = await _context.Chats
                    .FirstOrDefaultAsync(c => c.IdTrip == request.TripId && c.Type == "trip");

                if (chatToRemove != null)
                {
                    var chatMember = await _context.ChatMembers
                        .FirstOrDefaultAsync(cm => cm.ChatId == chatToRemove.IdChat && cm.UserId == request.UserId);

                    if (chatMember != null)
                    {
                        _context.ChatMembers.Remove(chatMember);
                    }
                }

                await _context.SaveChangesAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Участник удален из поездки"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении участника из поездки");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при удалении участника: " + ex.Message
                });
            }
        }
        // POST: /Trips/SendInvitation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendInvitation([FromBody] SendTripInvitationRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                _logger.LogInformation("SendInvitation: tripId={TripId}, friendId={FriendId}, userId={UserId}",
                    request.TripId, request.FriendId, userId);

                // Проверяем, существует ли поездка
                var trip = await _context.Trips
                    .FirstOrDefaultAsync(t => t.IdTrip == request.TripId);

                if (trip == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Поездка не найдена"
                    });
                }

                // Проверяем, является ли пользователь организатором поездки
                var isCreator = await _context.TripParticipants
                    .AnyAsync(tp => tp.IdTrip == request.TripId && tp.IdUser == userId && tp.IdParticipantRole == 1);

                if (!isCreator)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Только организатор может приглашать участников"
                    });
                }

                // Проверяем, не является ли друг уже участником
                var isAlreadyParticipant = await _context.TripParticipants
                    .AnyAsync(tp => tp.IdTrip == request.TripId && tp.IdUser == request.FriendId);

                if (isAlreadyParticipant)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь уже является участником поездки"
                    });
                }

                // Проверяем, нет ли уже активного приглашения
                var existingInvitation = await _context.TripInvitations
                    .FirstOrDefaultAsync(i => i.IdTrip == request.TripId &&
                                              i.InvitedId == request.FriendId &&
                                              i.Status == "pending");

                if (existingInvitation != null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Приглашение уже отправлено"
                    });
                }

                // Создаем приглашение
                var invitation = new TripInvitation
                {
                    IdTrip = request.TripId,
                    InviterId = userId.Value,
                    InvitedId = request.FriendId,
                    Message = request.Message,
                    InvitedAt = DateTime.UtcNow,
                    Status = "pending"
                };

                _context.TripInvitations.Add(invitation);
                await _context.SaveChangesAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Приглашение отправлено",
                    Data = new { invitationId = invitation.IdInvitation }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отправке приглашения");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при отправке приглашения: " + ex.Message
                });
            }
        }

        // GET: /Trips/GetMyInvitations
        [HttpGet]
        public async Task<IActionResult> GetMyInvitations()
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                var invitations = await _context.TripInvitations
                    .Include(i => i.Trip)
                    .Include(i => i.Inviter)
                    .Where(i => i.InvitedId == userId && i.Status == "pending")
                    .OrderByDescending(i => i.InvitedAt)
                    .Select(i => new TripInvitationDto
                    {
                        Id = i.IdInvitation,
                        TripId = i.IdTrip,
                        TripTitle = i.Trip.Title ?? "Без названия",
                        InviterId = i.InviterId,
                        InviterName = i.Inviter.LastName + " " + i.Inviter.FirstName,
                        InviterAvatar = i.Inviter.AvatarPath,
                        Message = i.Message,
                        InvitedAt = i.InvitedAt,
                        Status = i.Status,
                        ChatId = _context.Chats
                            .Where(c => c.IdTrip == i.IdTrip && c.Type == "trip")
                            .Select(c => (int?)c.IdChat)
                            .FirstOrDefault()
                    })
                    .ToListAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Data = invitations
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении приглашений");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при загрузке приглашений: " + ex.Message
                });
            }
        }

        // POST: /Trips/RespondToInvitation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RespondToInvitation([FromBody] RespondToInvitationRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                var invitation = await _context.TripInvitations
                    .Include(i => i.Trip)
                    .FirstOrDefaultAsync(i => i.IdInvitation == request.InvitationId && i.InvitedId == userId);

                if (invitation == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Приглашение не найдено"
                    });
                }

                if (invitation.Status != "pending")
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Это приглашение уже обработано"
                    });
                }

                invitation.RespondedAt = DateTime.UtcNow;

                if (request.Accept)
                {
                    invitation.Status = "accepted";

                    // Добавляем пользователя в участники поездки
                    var participant = new TripParticipant
                    {
                        IdTrip = invitation.IdTrip,
                        IdUser = userId.Value,
                        IdParticipantRole = 2, // Участник
                        JoinedAt = DateTime.UtcNow
                    };
                    _context.TripParticipants.Add(participant);

                    // Добавляем в чат поездки, если он есть
                    var chat = await _context.Chats
                        .FirstOrDefaultAsync(c => c.IdTrip == invitation.IdTrip && c.Type == "trip");

                    if (chat != null)
                    {
                        _context.ChatMembers.Add(new ChatMember
                        {
                            ChatId = chat.IdChat,
                            UserId = userId.Value,
                            Role = "member",
                            JoinedAt = DateTime.UtcNow
                        });
                    }
                }
                else
                {
                    invitation.Status = "declined";
                }

                await _context.SaveChangesAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = request.Accept ? "Вы присоединились к поездке" : "Приглашение отклонено"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при ответе на приглашение");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при ответе на приглашение: " + ex.Message
                });
            }
        }
    }
}