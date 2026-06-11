using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWise.Models;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;

namespace TripWise.Controllers
{
    public class DocumentsController : Controller
    {
        private readonly TripWiseContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<DocumentsController> _logger;

        public DocumentsController(TripWiseContext context, IWebHostEnvironment environment, ILogger<DocumentsController> logger)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
        }

        // GET: /Account/MyDocuments
        public IActionResult MyDocuments()
        {
            return View();
        }

        // GET: /Documents/GetUserFolders
        [HttpGet]
        public async Task<IActionResult> GetUserFolders()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return Unauthorized();

            var folders = await _context.DocumentFolders
                .Where(f => f.UserId == userId)
                .Select(f => new
                {
                    f.IdFolder,
                    f.Name,
                    f.Description,
                    f.Color,
                    DocumentCount = f.Documents.Count
                })
                .OrderBy(f => f.Name)
                .ToListAsync();

            return Json(folders);
        }

        // POST: /Documents/CreateFolder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFolder([FromBody] CreateFolderRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                    return Json(new { success = false, message = "Сессия истекла" });

                // Проверяем, нет ли уже папки с таким именем
                var existingFolder = await _context.DocumentFolders
                    .FirstOrDefaultAsync(f => f.UserId == userId && f.Name == request.Name);

                if (existingFolder != null)
                    return Json(new { success = false, message = "Папка с таким именем уже существует" });

                var folder = new DocumentFolder
                {
                    Name = request.Name,
                    Description = request.Description,
                    Color = request.Color,
                    UserId = userId.Value,
                    CreatedAt = DateTime.UtcNow
                };

                _context.DocumentFolders.Add(folder);
                await _context.SaveChangesAsync();

                return Json(new { success = true, folderId = folder.IdFolder });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании папки");
                return Json(new { success = false, message = "Ошибка при создании папки" });
            }
        }

        // GET: /Documents/GetUserDocuments
        [HttpGet]
        public async Task<IActionResult> GetUserDocuments(int? folderId, string search = "", string filterType = "", string sortBy = "newest")
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                    return Unauthorized();

                var query = _context.UserDocuments
                    .Where(d => d.UserId == userId)
                    .Include(d => d.Folder)
                    .AsQueryable();

                // Фильтрация по папке
                if (folderId.HasValue && folderId > 0)
                {
                    query = query.Where(d => d.FolderId == folderId);
                }

                // Поиск по названию
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(d => d.Name.Contains(search) ||
                                           d.Description.Contains(search) ||
                                           d.DocumentNumber.Contains(search));
                }

                // Фильтрация по типу файла
                if (!string.IsNullOrEmpty(filterType))
                {
                    switch (filterType.ToLower())
                    {
                        case "pdf":
                            query = query.Where(d => d.FileType.ToLower() == ".pdf");
                            break;
                        case "doc":
                            query = query.Where(d => d.FileType.ToLower() == ".doc" || d.FileType.ToLower() == ".docx");
                            break;
                        case "image":
                            query = query.Where(d => d.FileType.ToLower() == ".jpg" ||
                                                   d.FileType.ToLower() == ".jpeg" ||
                                                   d.FileType.ToLower() == ".png" ||
                                                   d.FileType.ToLower() == ".gif");
                            break;
                    }
                }

                // Сортировка
                switch (sortBy.ToLower())
                {
                    case "oldest":
                        query = query.OrderBy(d => d.CreatedAt);
                        break;
                    case "name_asc":
                        query = query.OrderBy(d => d.Name);
                        break;
                    case "name_desc":
                        query = query.OrderByDescending(d => d.Name);
                        break;
                    case "size_asc":
                        query = query.OrderBy(d => d.FileSize);
                        break;
                    case "size_desc":
                        query = query.OrderByDescending(d => d.FileSize);
                        break;
                    default: // newest
                        query = query.OrderByDescending(d => d.CreatedAt);
                        break;
                }

                var documents = await query
                    .Select(d => new
                    {
                        d.IdDocument,
                        d.Name,
                        d.Description,
                        d.FileType,
                        d.FileSize,
                        d.FilePath,
                        d.DocumentType,
                        d.DocumentNumber,
                        d.DocumentDate,
                        d.CreatedAt,
                        FolderId = d.FolderId,
                        FolderName = d.Folder != null ? d.Folder.Name : null
                    })
                    .ToListAsync();

                return Json(documents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении документов");
                return StatusCode(500, new { error = "Ошибка при получении документов" });
            }
        }

        // POST: /Documents/UploadDocument
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadDocument([FromForm] UploadDocumentRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                    return Json(new { success = false, message = "Сессия истекла. Пожалуйста, войдите заново." });

                // ========== ВАЛИДАЦИЯ С ПОНЯТНЫМИ СООБЩЕНИЯМИ ==========

                // Проверка названия документа
                if (string.IsNullOrWhiteSpace(request.Name))
                    return Json(new { success = false, message = "Введите название документа" });

                if (request.Name.Length > 255)
                    return Json(new { success = false, message = "Название документа не должно превышать 255 символов" });

                // Проверка файла
                if (request.File == null)
                    return Json(new { success = false, message = "Выберите файл для загрузки" });

                if (request.File.Length == 0)
                    return Json(new { success = false, message = "Файл пуст. Выберите другой файл" });

                // Проверка размера файла (10MB максимум)
                const long maxFileSize = 10 * 1024 * 1024; // 10MB
                if (request.File.Length > maxFileSize)
                    return Json(new { success = false, message = $"Размер файла не должен превышать 10 МБ. Ваш файл: {FormatFileSize(request.File.Length)}" });

                // Проверка расширения файла
                var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png", ".txt", ".xls", ".xlsx" };
                var fileExtension = Path.GetExtension(request.File.FileName).ToLower();

                if (!allowedExtensions.Contains(fileExtension))
                    return Json(new { success = false, message = $"Недопустимый тип файла. Разрешены: {string.Join(", ", allowedExtensions)}" });

                // Проверка папки (если указана)
                if (request.FolderId.HasValue && request.FolderId.Value > 0)
                {
                    var folderExists = await _context.DocumentFolders
                        .AnyAsync(f => f.IdFolder == request.FolderId && f.UserId == userId);

                    if (!folderExists)
                        return Json(new { success = false, message = "Выбранная папка не найдена" });
                }

                // Создаем директорию для документов пользователя, если ее нет
                var userFolder = Path.Combine(_environment.WebRootPath, "documents", userId.ToString());
                if (!Directory.Exists(userFolder))
                    Directory.CreateDirectory(userFolder);

                // Генерируем уникальное имя файла
                var fileName = Guid.NewGuid().ToString() + fileExtension;
                var filePath = Path.Combine(userFolder, fileName);

                // Сохраняем файл
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await request.File.CopyToAsync(stream);
                }

                var document = new UserDocument
                {
                    Name = request.Name.Trim(),
                    Description = request.Description ?? "",
                    FileType = fileExtension,
                    FileSize = request.File.Length,
                    FilePath = $"/documents/{userId}/{fileName}",
                    DocumentType = request.DocumentType ?? "other",
                    DocumentNumber = request.DocumentNumber ?? "",
                    DocumentDate = request.DocumentDate,
                    FolderId = request.FolderId > 0 ? request.FolderId : null,
                    UserId = userId.Value,
                    CreatedAt = DateTime.UtcNow
                };

                _context.UserDocuments.Add(document);
                await _context.SaveChangesAsync();

                // Обновляем количество документов в папке (если папка выбрана)
                if (document.FolderId.HasValue)
                {
                    // Можно добавить логику обновления счетчика, если нужно
                }

                return Json(new
                {
                    success = true,
                    documentId = document.IdDocument,
                    message = "Документ успешно загружен"
                });
            }
            catch (PathTooLongException)
            {
                return Json(new { success = false, message = "Путь к файлу слишком длинный" });
            }
            catch (UnauthorizedAccessException)
            {
                return Json(new { success = false, message = "Нет прав доступа для сохранения файла" });
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Ошибка ввода-вывода при загрузке документа");
                return Json(new { success = false, message = "Ошибка при сохранении файла. Попробуйте позже." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке документа");
                return Json(new { success = false, message = "Произошла ошибка при загрузке документа: " + ex.Message });
            }
        }

        // Вспомогательный метод для форматирования размера файла
        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "Б", "КБ", "МБ", "ГБ" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        // GET: /Documents/GetDocument/{id}
        [HttpGet]
        public async Task<IActionResult> GetDocument(int id)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new { success = false, message = "Пользователь не авторизован" });
                }

                var document = await _context.UserDocuments
                    .Include(d => d.Folder)
                    .FirstOrDefaultAsync(d => d.IdDocument == id && d.UserId == userId);

                if (document == null)
                {
                    return Json(new { success = false, message = "Документ не найден" });
                }

                // Форматируем даты для безопасной передачи
                string formattedDocumentDate = null;
                if (document.DocumentDate.HasValue)
                {
                    formattedDocumentDate = document.DocumentDate.Value.ToString("yyyy-MM-dd");
                }

                // Возвращаем ВСЕ поля документа
                return Json(new
                {
                    success = true,
                    idDocument = document.IdDocument,
                    name = document.Name ?? "",
                    description = document.Description ?? "",
                    fileType = document.FileType ?? "",
                    fileSize = document.FileSize,
                    filePath = document.FilePath ?? "",
                    documentType = document.DocumentType ?? "other",
                    documentNumber = document.DocumentNumber ?? "",
                    documentDate = formattedDocumentDate,
                    createdAt = document.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    folderId = document.FolderId,
                    folderName = document.Folder?.Name ?? "Без папки"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении документа {DocumentId}", id);
                return Json(new { success = false, message = "Ошибка при получении документа: " + ex.Message });
            }
        }

        // GET: /Documents/Download/{id}
        [HttpGet]
        public async Task<IActionResult> Download(int id)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                    return Unauthorized();

                var document = await _context.UserDocuments
                    .FirstOrDefaultAsync(d => d.IdDocument == id && d.UserId == userId);

                if (document == null)
                    return NotFound();

                var filePath = Path.Combine(_environment.WebRootPath, document.FilePath.TrimStart('/'));

                if (!System.IO.File.Exists(filePath))
                    return NotFound();

                var memory = new MemoryStream();
                using (var stream = new FileStream(filePath, FileMode.Open))
                {
                    await stream.CopyToAsync(memory);
                }
                memory.Position = 0;

                return File(memory, GetContentType(document.FileType), $"{document.Name}{document.FileType}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при скачивании документа");
                return StatusCode(500);
            }
        }

        // GET: /Documents/GetFile/{id} (для превью изображений)
        [HttpGet]
        public async Task<IActionResult> GetFile(int id)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                    return Unauthorized();

                var document = await _context.UserDocuments
                    .FirstOrDefaultAsync(d => d.IdDocument == id && d.UserId == userId);

                if (document == null)
                    return NotFound();

                var filePath = Path.Combine(_environment.WebRootPath, document.FilePath.TrimStart('/'));

                if (!System.IO.File.Exists(filePath))
                    return NotFound();

                return PhysicalFile(filePath, GetContentType(document.FileType));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении файла");
                return StatusCode(500);
            }
        }

        // DELETE: /Documents/DeleteDocument/{id}
        [HttpDelete]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDocument(int id)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                    return Json(new { success = false, message = "Сессия истекла" });

                var document = await _context.UserDocuments
                    .FirstOrDefaultAsync(d => d.IdDocument == id && d.UserId == userId);

                if (document == null)
                    return Json(new { success = false, message = "Документ не найден" });

                // Удаляем физический файл
                var filePath = Path.Combine(_environment.WebRootPath, document.FilePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                // Удаляем запись из базы данных
                _context.UserDocuments.Remove(document);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении документа");
                return Json(new { success = false, message = "Ошибка при удалении документа" });
            }
        }

        // Вспомогательный метод для определения Content-Type
        private string GetContentType(string fileType)
        {
            return fileType.ToLower() switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".txt" => "text/plain",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _ => "application/octet-stream",
            };
        }
        // POST: /Documents/DeleteFolder/{id}
        [HttpPost]
        [Route("DeleteFolder/{id}")]
        public async Task<IActionResult> DeleteFolder(int id)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                    return Json(new { success = false, message = "Сессия истекла" });

                var folder = await _context.DocumentFolders
                    .FirstOrDefaultAsync(f => f.IdFolder == id && f.UserId == userId);

                if (folder == null)
                    return Json(new { success = false, message = "Папка не найдена" });

                // Удаляем папку (документы остаются, но с FolderId = null)
                _context.DocumentFolders.Remove(folder);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении папки");
                return Json(new { success = false, message = "Ошибка при удалении папки" });
            }
        }

        // POST: /Documents/MoveDocument
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MoveDocument([FromBody] MoveDocumentRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                    return Json(new { success = false, message = "Сессия истекла. Пожалуйста, войдите заново." });

                if (request == null || request.DocumentId <= 0)
                    return Json(new { success = false, message = "Неверный идентификатор документа" });

                var document = await _context.UserDocuments
                    .FirstOrDefaultAsync(d => d.IdDocument == request.DocumentId && d.UserId == userId);

                if (document == null)
                    return Json(new { success = false, message = "Документ не найден" });

                // Проверяем, существует ли папка (если указана)
                if (request.FolderId.HasValue && request.FolderId.Value > 0)
                {
                    var folderExists = await _context.DocumentFolders
                        .AnyAsync(f => f.IdFolder == request.FolderId && f.UserId == userId);

                    if (!folderExists)
                        return Json(new { success = false, message = "Выбранная папка не найдена" });

                    document.FolderId = request.FolderId.Value;
                }
                else
                {
                    document.FolderId = null;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Документ {DocumentId} перемещен в папку {FolderId}", request.DocumentId, request.FolderId);

                return Json(new { success = true, message = "Документ успешно перемещен" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при перемещении документа {DocumentId}", request?.DocumentId);
                return Json(new { success = false, message = "Произошла ошибка при перемещении документа: " + ex.Message });
            }
        }
        // POST: /Documents/QuickUpload
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickUpload([FromForm] QuickUploadRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                    return Json(new { success = false, message = "Сессия истекла. Пожалуйста, войдите заново." });

                // Валидация названия
                if (string.IsNullOrWhiteSpace(request.DocumentName))
                    return Json(new { success = false, message = "Введите название документа" });

                if (request.DocumentName.Length > 255)
                    return Json(new { success = false, message = "Название документа не должно превышать 255 символов" });

                // Валидация файла
                if (request.DocumentFile == null)
                    return Json(new { success = false, message = "Выберите файл для загрузки" });

                if (request.DocumentFile.Length == 0)
                    return Json(new { success = false, message = "Файл пуст. Выберите другой файл" });

                // Проверка размера (10MB)
                const long maxFileSize = 10 * 1024 * 1024;
                if (request.DocumentFile.Length > maxFileSize)
                    return Json(new { success = false, message = $"Размер файла не должен превышать 10 МБ" });

                // Проверка расширения
                var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png", ".txt", ".xls", ".xlsx" };
                var fileExtension = Path.GetExtension(request.DocumentFile.FileName).ToLower();

                if (!allowedExtensions.Contains(fileExtension))
                    return Json(new { success = false, message = $"Недопустимый тип файла. Разрешены: {string.Join(", ", allowedExtensions)}" });

                // Проверка папки (если указана)
                if (request.FolderId.HasValue && request.FolderId.Value > 0)
                {
                    var folderExists = await _context.DocumentFolders
                        .AnyAsync(f => f.IdFolder == request.FolderId && f.UserId == userId);

                    if (!folderExists)
                        return Json(new { success = false, message = "Выбранная папка не найдена" });
                }

                // Создаем директорию пользователя
                var userFolder = Path.Combine(_environment.WebRootPath, "documents", userId.ToString());
                if (!Directory.Exists(userFolder))
                    Directory.CreateDirectory(userFolder);

                // Генерируем уникальное имя файла
                var fileName = Guid.NewGuid().ToString() + fileExtension;
                var filePath = Path.Combine(userFolder, fileName);

                // Сохраняем файл
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await request.DocumentFile.CopyToAsync(stream);
                }

                // Создаем запись в БД
                var document = new UserDocument
                {
                    Name = request.DocumentName.Trim(),
                    Description = "",
                    FileType = fileExtension,
                    FileSize = request.DocumentFile.Length,
                    FilePath = $"/documents/{userId}/{fileName}",
                    DocumentType = "other",
                    DocumentNumber = "",
                    DocumentDate = null,
                    FolderId = request.FolderId > 0 ? request.FolderId : null,
                    UserId = userId.Value,
                    CreatedAt = DateTime.UtcNow
                };

                _context.UserDocuments.Add(document);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Документ успешно загружен" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при быстрой загрузке документа");
                return Json(new { success = false, message = "Произошла ошибка при загрузке документа: " + ex.Message });
            }
        }
        // POST: /Documents/DeleteDocument/{id}
        [HttpPost]
        [Route("DeleteDocument/{id}")]
        public async Task<IActionResult> DeleteDocumentPost(int id)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                    return Json(new { success = false, message = "Сессия истекла" });

                var document = await _context.UserDocuments
                    .FirstOrDefaultAsync(d => d.IdDocument == id && d.UserId == userId);

                if (document == null)
                    return Json(new { success = false, message = "Документ не найден" });

                // Удаляем физический файл
                var filePath = Path.Combine(_environment.WebRootPath, document.FilePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                // Удаляем запись из базы данных
                _context.UserDocuments.Remove(document);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении документа");
                return Json(new { success = false, message = "Ошибка при удалении документа" });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFolder([FromBody] DeleteFolderRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                    return Json(new { success = false, message = "Сессия истекла" });

                var folder = await _context.DocumentFolders
                    .FirstOrDefaultAsync(f => f.IdFolder == request.Id && f.UserId == userId);

                if (folder == null)
                    return Json(new { success = false, message = "Папка не найдена" });

                _context.DocumentFolders.Remove(folder);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении папки");
                return Json(new { success = false, message = "Ошибка при удалении папки" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDocument([FromBody] DeleteDocumentRequest request)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                    return Json(new { success = false, message = "Сессия истекла" });

                var document = await _context.UserDocuments
                    .FirstOrDefaultAsync(d => d.IdDocument == request.Id && d.UserId == userId);

                if (document == null)
                    return Json(new { success = false, message = "Документ не найден" });

                var filePath = Path.Combine(_environment.WebRootPath, document.FilePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                _context.UserDocuments.Remove(document);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении документа");
                return Json(new { success = false, message = "Ошибка при удалении документа" });
            }
        }
    }

    // Модели запросов
    public class CreateFolderRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Color { get; set; }
    }

    public class UploadDocumentRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public IFormFile File { get; set; }
        public int? FolderId { get; set; }
        public string DocumentType { get; set; }
        public string DocumentNumber { get; set; }
        public DateTime? DocumentDate { get; set; }
    }
    public class MoveDocumentRequest
    {
        public int DocumentId { get; set; }
        public int? FolderId { get; set; }
    }
    public class DeleteFolderRequest
    {
        public int Id { get; set; }
    }

    public class DeleteDocumentRequest
    {
        public int Id { get; set; }
    }
    public class QuickUploadRequest
    {
        public string DocumentName { get; set; }
        public IFormFile DocumentFile { get; set; }
        public int? FolderId { get; set; }
    }
}