using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using TripWise.Models;
using TripWise.Services;

var builder = WebApplication.CreateBuilder(args);

// Конфигурация логгирования
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Добавление контроллеров (убрал дублирующий AddControllers)
builder.Services.AddControllersWithViews();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
    options.Cookie.Name = "X-CSRF-TOKEN";
    options.Cookie.HttpOnly = false;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// HTTP клиент
builder.Services.AddHttpClient();

// Кэширование
builder.Services.AddMemoryCache();

// Сессии - исправлены настройки для разработки
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.None; // <-- ИЗМЕНЕНО для разработки
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// База данных
builder.Services.AddDbContext<TripWiseContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("DefaultConnection"), new MySqlServerVersion(new Version(8, 0, 0))));

// Регистрация сервисов
builder.Services.AddScoped<IHotelService, HotelService>();
builder.Services.AddScoped<ICacheService, MemoryCacheService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
builder.Services.AddScoped<IFavoriteService, FavoriteService>();
builder.Services.AddScoped<IFileService, FileService>();

// Аутентификация - исправлены настройки для разработки
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.None; // <-- ИЗМЕНЕНО для разработки
    });

// API сервисы
builder.Services.AddScoped<RzdApiService>();
builder.Services.AddHttpClient<RzdApiService>();

// Авиабилеты
builder.Services.AddScoped<IFlightService, RealisticFlightService>();

var app = builder.Build();

// Конфигурация пайплайна
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseCors("AllowAll");

// ВАЖНО: правильный порядок middleware
app.UseSession();
app.UseAuthentication(); // <-- ДОБАВЛЕНО!
app.UseAuthorization();

// Middleware для автоматического входа
app.Use(async (context, next) =>
{
    // Безопасная проверка сессии
    if (context.Session != null && context.Session.GetInt32("UserId") == null)
    {
        var authToken = context.Request.Cookies["AuthToken"];
        var rememberMe = context.Request.Cookies["RememberMe"];

        if (rememberMe == "true" && !string.IsNullOrEmpty(authToken))
        {
            try
            {
                using var scope = context.RequestServices.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<TripWiseContext>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

                var userToken = await dbContext.UserAuthTokens
                    .Include(t => t.User)
                    .ThenInclude(u => u.IdRoleNavigation)
                    .FirstOrDefaultAsync(t =>
                        t.Token == authToken &&
                        t.ExpiresAt > DateTime.UtcNow);

                if (userToken?.User != null)
                {
                    context.Session.SetInt32("UserId", userToken.User.IdUser);
                    context.Session.SetString("UserName", $"{userToken.User.LastName} {userToken.User.FirstName}");
                    context.Session.SetString("UserEmail", userToken.User.Email);
                    context.Session.SetInt32("UserRole", userToken.User.IdRole);

                    context.Response.Cookies.Append("UserEmail", userToken.User.Email,
                        new CookieOptions
                        {
                            Expires = DateTime.Now.AddDays(30),
                            HttpOnly = true,
                            IsEssential = true // для разработки
                        });
                }
            }
            catch (Exception ex)
            {
                var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "Ошибка автоматического входа");
            }
        }
    }

    await next();
});

// Маршруты
app.MapControllerRoute(
    name: "flightBooking",
    pattern: "FlightBooking/{action=Index}/{id?}",
    defaults: new { controller = "FlightBooking", action = "Index" });

app.MapControllerRoute(
    name: "chats",
    pattern: "Chats/{action=Index}/{id?}",
    defaults: new { controller = "Chats", action = "Index" });

app.MapControllerRoute(
    name: "favorites",
    pattern: "Favorites",
    defaults: new { controller = "FavoritesPage", action = "Index" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

app.MapGet("/TrainBooking/MyTickets", context =>
{
    context.Response.Redirect("/Home/MyOrders");
    return Task.CompletedTask;
});

app.Run();