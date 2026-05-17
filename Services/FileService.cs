// Services/FileService.cs
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace TripWise.Services
{
    public interface IFileService
    {
        Task<string> SaveAvatarAsync(IFormFile file, int userId);
        void DeleteAvatar(string avatarPath);
        string GetDefaultAvatar(string initials);
    }

    public class FileService : IFileService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileService> _logger;
        private const string AVATARS_FOLDER = "avatars";

        public FileService(IWebHostEnvironment environment, ILogger<FileService> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        // Services/FileService.cs
        public async Task<string> SaveAvatarAsync(IFormFile file, int userId)
        {
            try
            {
                // Создаем папку для аватарок, если её нет
                var avatarsPath = Path.Combine(_environment.WebRootPath, "uploads", "avatars");
                if (!Directory.Exists(avatarsPath))
                {
                    Directory.CreateDirectory(avatarsPath);
                    _logger.LogInformation("Создана папка для аватарок: {Path}", avatarsPath);
                }

                // Генерируем уникальное имя файла
                var fileExtension = Path.GetExtension(file.FileName).ToLower();
                var fileName = $"avatar_{userId}_{DateTime.Now.Ticks}{fileExtension}";
                var filePath = Path.Combine(avatarsPath, fileName);

                _logger.LogInformation("Сохраняем аватарку: {FilePath}", filePath);

                // Сохраняем файл
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Возвращаем относительный путь для сохранения в БД
                var relativePath = $"/uploads/avatars/{fileName}";
                _logger.LogInformation("Аватарка сохранена, путь: {Path}", relativePath);

                return relativePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при сохранении аватарки для пользователя {UserId}", userId);
                throw;
            }
        }

        public void DeleteAvatar(string avatarPath)
        {
            if (string.IsNullOrEmpty(avatarPath))
                return;

            try
            {
                var fullPath = Path.Combine(_environment.WebRootPath, avatarPath.TrimStart('/'));
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    _logger.LogInformation("Аватарка удалена: {AvatarPath}", avatarPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении аватарки: {AvatarPath}", avatarPath);
            }
        }

        public string GetDefaultAvatar(string initials)
        {
            // Генерируем SVG с инициалами
            var svg = $@"
                <svg width='100' height='100' viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'>
                    <circle cx='50' cy='50' r='50' fill='#0379D9'/>
                    <text x='50' y='50' font-family='Nunito, Arial' font-size='40' 
                          font-weight='bold' fill='white' text-anchor='middle' 
                          dominant-baseline='middle'>
                        {initials}
                    </text>
                </svg>";

            // Можно сохранить SVG как файл или вернуть data URL
            return $"data:image/svg+xml;utf8,{Uri.EscapeDataString(svg)}";
        }
    }
}