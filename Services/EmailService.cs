using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class EmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger = null)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string body)
    {
        try
        {
            _logger?.LogInformation($"Attempting to send email to: {toEmail}");

            var settings = _config.GetSection("EmailSettings");

            // Проверяем настройки
            var smtpServer = settings["SmtpServer"];
            var port = settings["Port"];
            var username = settings["Username"];
            var senderEmail = settings["SenderEmail"];

            _logger?.LogInformation($"SMTP Server: {smtpServer}:{port}");
            _logger?.LogInformation($"Username: {username}");
            _logger?.LogInformation($"Sender: {senderEmail}");

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                settings["SenderName"],
                senderEmail
            ));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;

            var builder = new BodyBuilder
            {
                HtmlBody = body
            };
            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();

            // Настройки для отладки
            client.Timeout = 10000;
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;

            // Пробуем разные варианты подключения

            // Вариант 1: StartTLS (порт 587)
            try
            {
                _logger?.LogInformation("Trying StartTLS on port 587...");
                await client.ConnectAsync(smtpServer, 587, SecureSocketOptions.StartTls);
                _logger?.LogInformation("Connected via StartTLS");
            }
            catch (Exception ex1)
            {
                _logger?.LogWarning($"StartTLS failed: {ex1.Message}");

                // Вариант 2: SSL (порт 465)
                try
                {
                    _logger?.LogInformation("Trying SSL on port 465...");
                    await client.ConnectAsync(smtpServer, 465, SecureSocketOptions.SslOnConnect);
                    _logger?.LogInformation("Connected via SSL");
                }
                catch (Exception ex2)
                {
                    _logger?.LogWarning($"SSL failed: {ex2.Message}");

                    // Вариант 3: Без шифрования (только для теста)
                    try
                    {
                        _logger?.LogInformation("Trying without encryption on port 25...");
                        await client.ConnectAsync(smtpServer, 25, SecureSocketOptions.None);
                        _logger?.LogInformation("Connected without encryption");
                    }
                    catch (Exception ex3)
                    {
                        throw new Exception($"All connection attempts failed. StartTLS: {ex1.Message}, SSL: {ex2.Message}, None: {ex3.Message}");
                    }
                }
            }

            _logger?.LogInformation($"Authenticating as: {username}");
            await client.AuthenticateAsync(username, settings["Password"]);
            _logger?.LogInformation("Authentication successful");

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger?.LogInformation($"Email successfully sent to {toEmail}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"Failed to send email to {toEmail}");

            // Для отладки пишем в консоль
            Console.WriteLine($"EMAIL ERROR: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");

            // Перебрасываем исключение дальше
            throw new Exception($"Failed to send email: {ex.Message}", ex);
        }
    }
    public async Task SendConfirmationCodeAsync(string toEmail, string code)
    {
        var subject = "Код подтверждения удаления аккаунта - Вместе В Путь";
        var body = $@"
        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
            <div style='background: #fff3cd; border: 1px solid #ffeaa7; border-radius: 10px; padding: 20px; margin-bottom: 20px;'>
                <h2 style='color: #856404; margin-top: 0;'>
                    <i class='fas fa-exclamation-triangle'></i> Подтверждение удаления аккаунта
                </h2>
                <p style='color: #856404;'>
                    Вы запросили удаление вашего аккаунта в <strong>Вместе В Путь</strong>.
                </p>
            </div>
            
            <div style='text-align: center; margin: 30px 0;'>
                <h3 style='color: #333;'>Ваш код подтверждения:</h3>
                <div style='background: #f8f9fa; padding: 25px; border-radius: 12px; border: 3px dashed #d32f2f; 
                            display: inline-block; margin: 20px 0;'>
                    <h1 style='color: #d32f2f; margin: 0; letter-spacing: 15px; font-size: 36px; font-weight: bold;'>
                        {code}
                    </h1>
                </div>
                <p style='color: #666;'>
                    Введите этот 6-значный код на странице подтверждения удаления.
                </p>
            </div>
            
            <div style='background: #e8f4fe; padding: 15px; border-radius: 8px; margin-bottom: 20px;'>
                <p style='margin: 0; color: #0379D9;'>
                    <strong><i class='fas fa-clock'></i> Код действителен 15 минут</strong>
                </p>
            </div>
            
            <div style='border-top: 1px solid #eee; padding-top: 20px; margin-top: 30px;'>
                <p style='color: #888; font-size: 14px;'>
                    <strong>Если вы не запрашивали удаление аккаунта:</strong><br>
                    Проигнорируйте это письмо или 
                    <a href='mailto:support@tripwise.ru' style='color: #0379D9;'>свяжитесь со службой поддержки</a>.
                </p>
            </div>
            
            <div style='text-align: center; margin-top: 30px;'>
                <p style='color: #aaa; font-size: 12px;'>
                    С уважением, команда <strong>Вместе В Путь</strong><br>
                    {DateTime.Now.Year} © Все права защищены
                </p>
            </div>
        </div>";

        await SendAsync(toEmail, subject, body);
    }
    public async Task SendPasswordChangeCodeAsync(string toEmail, string code)
    {
        var subject = "Код подтверждения смены пароля - Вместе В Путь";
        var body = $@"
    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
        <div style='background: #e8f4fe; border: 1px solid #b6d4fe; border-radius: 10px; padding: 20px; margin-bottom: 20px;'>
            <h2 style='color: #0379D9; margin-top: 0;'>
                <i class='fas fa-key'></i> Подтверждение смены пароля
            </h2>
            <p style='color: #0379D9;'>
                Вы запросили смену пароля для вашего аккаунта в <strong>Вместе В Путь</strong>.
            </p>
        </div>
        
        <div style='text-align: center; margin: 30px 0;'>
            <h3 style='color: #333;'>Ваш код подтверждения:</h3>
            <div style='background: #f8f9fa; padding: 25px; border-radius: 12px; border: 3px dashed #0379D9; 
                        display: inline-block; margin: 20px 0;'>
                <h1 style='color: #0379D9; margin: 0; letter-spacing: 15px; font-size: 36px; font-weight: bold;'>
                    {code}
                </h1>
            </div>
            <p style='color: #666;'>
                Введите этот 6-значный код для подтверждения смены пароля.
            </p>
        </div>
        
        <div style='background: #fff3cd; padding: 15px; border-radius: 8px; margin-bottom: 20px;'>
            <p style='margin: 0; color: #856404;'>
                <strong><i class='fas fa-shield-alt'></i> В целях безопасности</strong><br>
                Никогда никому не сообщайте этот код, даже сотрудникам поддержки.
            </p>
        </div>
        
        <div style='background: #e8f4fe; padding: 15px; border-radius: 8px; margin-bottom: 20px;'>
            <p style='margin: 0; color: #0379D9;'>
                <strong><i class='fas fa-clock'></i> Код действителен 15 минут</strong>
            </p>
        </div>
        
        <div style='border-top: 1px solid #eee; padding-top: 20px; margin-top: 30px;'>
            <p style='color: #888; font-size: 14px;'>
                <strong>Если вы не запрашивали смену пароля:</strong><br>
                Проигнорируйте это письмо, ваш текущий пароль остается действительным.
                Если вы подозреваете несанкционированный доступ, 
                <a href='mailto:support@tripwise.ru' style='color: #0379D9;'>свяжитесь со службой поддержки</a>.
            </p>
        </div>
        
        <div style='text-align: center; margin-top: 30px;'>
            <p style='color: #aaa; font-size: 12px;'>
                С уважением, команда <strong>Вместе В Путь</strong><br>
                {DateTime.Now.Year} © Все права защищены
            </p>
        </div>
    </div>";

        await SendAsync(toEmail, subject, body);
    }
    // Простой метод для теста
    public async Task<bool> TestConnection()
    {
        try
        {
            var settings = _config.GetSection("EmailSettings");
            var smtpServer = settings["SmtpServer"];
            var port = int.Parse(settings["Port"]);
            var username = settings["Username"];

            using var client = new SmtpClient();
            client.Timeout = 5000;

            await client.ConnectAsync(smtpServer, port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(username, settings["Password"]);
            await client.DisconnectAsync(true);

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SMTP connection test failed");
            return false;
        }
    }
}