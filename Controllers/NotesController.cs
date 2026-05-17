using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWise.Models;
using TripWise.Models.DTOs;
using Microsoft.Extensions.Logging;

namespace TripWise.Controllers
{
    public class NotesController : Controller
    {
        private readonly TripWiseContext _context;
        private readonly ILogger<NotesController> _logger;

        public NotesController(TripWiseContext context, ILogger<NotesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: /Notes
        public IActionResult Index()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }
            return View();
        }

        // GET: /Notes/GetNotes
        [HttpGet]
        public async Task<IActionResult> GetNotes()
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<List<NoteDto>>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                var notes = await _context.Notes
                    .Where(n => n.UserId == userId)
                    .OrderByDescending(n => n.IsPinned)
                    .ThenByDescending(n => n.UpdatedAt ?? n.CreatedAt)
                    .Select(n => new NoteDto
                    {
                        Id = n.Id,
                        Title = n.Title,
                        Content = n.Content,
                        Color = n.Color,
                        IsPinned = n.IsPinned,
                        CreatedAt = n.CreatedAt,
                        UpdatedAt = n.UpdatedAt,
                        Preview = n.Content.Length > 100 ? n.Content.Substring(0, 100) + "..." : n.Content
                    })
                    .ToListAsync();

                return Json(new ApiResponse<List<NoteDto>>
                {
                    Success = true,
                    Data = notes
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении заметок");
                return Json(new ApiResponse<List<NoteDto>>
                {
                    Success = false,
                    Message = "Ошибка при загрузке заметок"
                });
            }
        }

        // GET: /Notes/GetChecklistItems?noteId=5
        [HttpGet]
        public async Task<IActionResult> GetChecklistItems(int noteId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new ApiResponse<List<ChecklistItemDto>>
                    {
                        Success = false,
                        Message = "Пользователь не авторизован"
                    });
                }

                var note = await _context.Notes
                    .FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == userId);

                if (note == null)
                {
                    return Json(new ApiResponse<List<ChecklistItemDto>>
                    {
                        Success = false,
                        Message = "Заметка не найдена"
                    });
                }

                var items = await _context.ChecklistItems
                    .Where(i => i.NoteId == noteId)
                    .OrderBy(i => i.OrderIndex)
                    .Select(i => new ChecklistItemDto
                    {
                        Id = i.Id,
                        Text = i.Text,
                        IsCompleted = i.IsCompleted,
                        OrderIndex = i.OrderIndex,
                        CompletedAt = i.CompletedAt
                    })
                    .ToListAsync();

                return Json(new ApiResponse<List<ChecklistItemDto>>
                {
                    Success = true,
                    Data = items
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении пунктов чек-листа");
                return Json(new ApiResponse<List<ChecklistItemDto>>
                {
                    Success = false,
                    Message = "Ошибка при загрузке чек-листа"
                });
            }
        }

        // POST: /Notes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] CreateNoteRequest request)
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

                if (string.IsNullOrWhiteSpace(request.Title))
                {
                    request.Title = "Новая заметка";
                }

                var note = new Note
                {
                    UserId = userId.Value,
                    Title = request.Title.Trim(),
                    Content = request.Content ?? "",
                    Color = request.Color,
                    IsPinned = request.IsPinned,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Notes.Add(note);
                await _context.SaveChangesAsync();

                // Сохраняем пункты чек-листа
                if (request.ChecklistItems != null && request.ChecklistItems.Any())
                {
                    var items = request.ChecklistItems.Select((item, index) => new ChecklistItem
                    {
                        NoteId = note.Id,
                        Text = item.Text,
                        IsCompleted = item.IsCompleted,
                        OrderIndex = index,
                        CreatedAt = DateTime.UtcNow,
                        CompletedAt = item.IsCompleted ? DateTime.UtcNow : null
                    });

                    _context.ChecklistItems.AddRange(items);
                    await _context.SaveChangesAsync();
                }

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Data = new { id = note.Id },
                    Message = "Заметка создана"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании заметки");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при создании заметки"
                });
            }
        }

        // PUT: /Notes/Update
        [HttpPut]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update([FromBody] UpdateNoteRequest request)
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

                var note = await _context.Notes
                    .Include(n => n.ChecklistItems)
                    .FirstOrDefaultAsync(n => n.Id == request.Id && n.UserId == userId);

                if (note == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Заметка не найдена"
                    });
                }

                note.Title = request.Title.Trim();
                note.Content = request.Content ?? "";
                note.Color = request.Color;
                note.IsPinned = request.IsPinned;
                note.UpdatedAt = DateTime.UtcNow;

                // Обновляем чек-лист
                if (request.ChecklistItems != null)
                {
                    // Удаляем старые пункты
                    if (note.ChecklistItems != null && note.ChecklistItems.Any())
                    {
                        _context.ChecklistItems.RemoveRange(note.ChecklistItems);
                    }

                    // Добавляем новые
                    var newItems = request.ChecklistItems.Select((item, index) => new ChecklistItem
                    {
                        NoteId = note.Id,
                        Text = item.Text,
                        IsCompleted = item.IsCompleted,
                        OrderIndex = index,
                        CreatedAt = DateTime.UtcNow,
                        CompletedAt = item.IsCompleted ? DateTime.UtcNow : null
                    });

                    _context.ChecklistItems.AddRange(newItems);
                }

                await _context.SaveChangesAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Заметка обновлена"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении заметки");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при обновлении заметки"
                });
            }
        }

        // DELETE: /Notes/Delete/5
        [HttpDelete]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
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

                var note = await _context.Notes
                    .Include(n => n.ChecklistItems)
                    .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

                if (note == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Заметка не найдена"
                    });
                }

                // Удаляем связанные пункты чек-листа
                if (note.ChecklistItems != null && note.ChecklistItems.Any())
                {
                    _context.ChecklistItems.RemoveRange(note.ChecklistItems);
                }

                _context.Notes.Remove(note);
                await _context.SaveChangesAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Заметка удалена"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении заметки");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при удалении заметки"
                });
            }
        }

        // POST: /Notes/AddChecklistItem
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddChecklistItem([FromBody] CreateChecklistItemRequest request)
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

                var note = await _context.Notes
                    .FirstOrDefaultAsync(n => n.Id == request.NoteId && n.UserId == userId);

                if (note == null)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Заметка не найдена"
                    });
                }

                var maxOrder = await _context.ChecklistItems
                    .Where(i => i.NoteId == request.NoteId)
                    .MaxAsync(i => (int?)i.OrderIndex) ?? 0;

                var item = new ChecklistItem
                {
                    NoteId = request.NoteId,
                    Text = request.Text.Trim(),
                    IsCompleted = false,
                    OrderIndex = maxOrder + 1,
                    CreatedAt = DateTime.UtcNow
                };

                _context.ChecklistItems.Add(item);
                await _context.SaveChangesAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Data = new { id = item.Id },
                    Message = "Пункт добавлен"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении пункта чек-листа");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при добавлении пункта"
                });
            }
        }

        // PUT: /Notes/UpdateChecklistItem
        [HttpPut]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateChecklistItem([FromBody] UpdateChecklistItemRequest request)
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

                var item = await _context.ChecklistItems
                    .Include(i => i.Note)
                    .FirstOrDefaultAsync(i => i.Id == request.Id);

                if (item == null || item.Note?.UserId != userId)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пункт не найден"
                    });
                }

                if (request.Text != null)
                {
                    item.Text = request.Text.Trim();
                }

                if (request.IsCompleted.HasValue)
                {
                    item.IsCompleted = request.IsCompleted.Value;
                    item.CompletedAt = request.IsCompleted.Value ? DateTime.UtcNow : null;
                }

                if (request.OrderIndex.HasValue)
                {
                    item.OrderIndex = request.OrderIndex.Value;
                }

                item.Note.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Пункт обновлен"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении пункта чек-листа");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при обновлении"
                });
            }
        }

        // DELETE: /Notes/DeleteChecklistItem/5
        [HttpDelete]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteChecklistItem(int id)
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

                var item = await _context.ChecklistItems
                    .Include(i => i.Note)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (item == null || item.Note?.UserId != userId)
                {
                    return Json(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Пункт не найден"
                    });
                }

                _context.ChecklistItems.Remove(item);
                await _context.SaveChangesAsync();

                return Json(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Пункт удален"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении пункта чек-листа");
                return Json(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Ошибка при удалении"
                });
            }
        }
    }
}