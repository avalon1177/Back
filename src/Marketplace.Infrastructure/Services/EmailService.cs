using System.Net;
using System.Net.Mail;
using System.Text;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using Serilog;

namespace Marketplace.Infrastructure.Services;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string htmlBody, EmailType type, Guid? userId = null);
    Task SendConfirmationEmailAsync(AppUser user, string token, string baseUrl);
    Task SendPasswordResetEmailAsync(AppUser user, string token, string baseUrl);
    Task SendTwoFactorCodeAsync(AppUser user, string code);
    Task SendOrderCreatedNotificationAsync(AppUser user, string orderNumber, decimal total);
    Task SendOrderStatusChangedNotificationAsync(AppUser user, string orderNumber, string status);
    Task SendNewMessageNotificationAsync(AppUser user, string senderName, string preview);
    Task SendProductApprovedNotificationAsync(AppUser user, string productTitle);
    Task SendProductRejectedNotificationAsync(AppUser user, string productTitle, string reason);
}

public interface ISmtpClientWrapper
{
    Task SendAsync(MailMessage message, CancellationToken cancellationToken = default);
}

public sealed class SmtpClientWrapper : ISmtpClientWrapper
{
    private readonly SmtpOptions _options;

    public SmtpClientWrapper(IOptions<SmtpOptions> options)
    {
        _options = options.Value;
    }

    public Task SendAsync(MailMessage message, CancellationToken cancellationToken = default)
    {
        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_options.User, _options.Password),
            Timeout = 10000
        };

        var from = new MailAddress(_options.User, "Promka", Encoding.UTF8);
        message.From = from;

        return client.SendMailAsync(message, cancellationToken);
    }
}

public class EmailService : IEmailService
{
    private readonly ISmtpClientWrapper _smtpClient;
    private readonly AppDbContext _db;
    private readonly ILogger _logger;

    public EmailService(ISmtpClientWrapper smtpClient, AppDbContext db)
    {
        _smtpClient = smtpClient;
        _db = db;
        _logger = Log.ForContext<EmailService>();
    }

    public async Task SendEmailAsync(string to, string subject, string htmlBody, EmailType type, Guid? userId = null)
    {
        if (string.IsNullOrWhiteSpace(to))
        {
            _logger.Warning("Email send attempted with empty recipient address");
            return;
        }

        var log = new EmailLog
        {
            UserId = userId,
            To = to,
            Subject = subject,
            Body = htmlBody,
            Type = type,
            Status = EmailStatus.Pending
        };

        try
        {
            var message = new MailMessage
            {
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true,
                SubjectEncoding = Encoding.UTF8,
                BodyEncoding = Encoding.UTF8
            };
            message.To.Add(new MailAddress(to));

            await _smtpClient.SendAsync(message);
            log.Status = EmailStatus.Sent;
            _logger.Information("Email sent successfully to {To}, type: {Type}", to, type);
        }
        catch (Exception ex)
        {
            log.Status = EmailStatus.Failed;
            log.ErrorMessage = ex.Message;
            _logger.Error(ex, "Failed to send email to {To}, type: {Type}", to, type);
        }

        _db.EmailLogs.Add(log);
        await _db.SaveChangesAsync();
    }

    public async Task SendConfirmationEmailAsync(AppUser user, string token, string baseUrl)
    {
        var link = $"{baseUrl.TrimEnd('/')}/confirm-email?email={Uri.EscapeDataString(user.Email ?? "")}&token={Uri.EscapeDataString(token)}";
        var html = GetConfirmationEmailTemplate(user.DisplayName, link);
        await SendEmailAsync(user.Email ?? "", "Підтвердження email - Promka", html, EmailType.Confirmation, user.Id);
    }

    public async Task SendPasswordResetEmailAsync(AppUser user, string token, string baseUrl)
    {
        var link = $"{baseUrl.TrimEnd('/')}/reset-password?email={Uri.EscapeDataString(user.Email ?? "")}&token={Uri.EscapeDataString(token)}";
        var html = GetPasswordResetEmailTemplate(user.DisplayName, link);
        await SendEmailAsync(user.Email ?? "", "Відновлення пароля - Promka", html, EmailType.PasswordReset, user.Id);
    }

    public async Task SendTwoFactorCodeAsync(AppUser user, string code)
    {
        var html = GetTwoFactorCodeTemplate(user.DisplayName, code);
        await SendEmailAsync(user.Email ?? "", "Код підтвердження - Promka", html, EmailType.TwoFactorCode, user.Id);
    }

    public async Task SendOrderCreatedNotificationAsync(AppUser user, string orderNumber, decimal total)
    {
        var html = GetOrderCreatedTemplate(user.DisplayName, orderNumber, total);
        await SendEmailAsync(user.Email ?? "", $"Замовлення #{orderNumber} оформлено - Promka", html, EmailType.OrderCreated, user.Id);
    }

    public async Task SendOrderStatusChangedNotificationAsync(AppUser user, string orderNumber, string status)
    {
        var html = GetOrderStatusChangedTemplate(user.DisplayName, orderNumber, status);
        await SendEmailAsync(user.Email ?? "", $"Замовлення #{orderNumber} - {status} - Promka", html, EmailType.OrderStatusChanged, user.Id);
    }

    public async Task SendNewMessageNotificationAsync(AppUser user, string senderName, string preview)
    {
        var html = GetNewMessageTemplate(user.DisplayName, senderName, preview);
        await SendEmailAsync(user.Email ?? "", $"Нове повідомлення від {senderName} - Promka", html, EmailType.ChatMessage, user.Id);
    }

    public async Task SendProductApprovedNotificationAsync(AppUser user, string productTitle)
    {
        var html = GetProductApprovedTemplate(user.DisplayName, productTitle);
        await SendEmailAsync(user.Email ?? "", "Ваш товар схвалено - Promka", html, EmailType.ProductApproved, user.Id);
    }

    public async Task SendProductRejectedNotificationAsync(AppUser user, string productTitle, string reason)
    {
        var html = GetProductRejectedTemplate(user.DisplayName, productTitle, reason);
        await SendEmailAsync(user.Email ?? "", "Ваш товар відхилено - Promka", html, EmailType.ProductRejected, user.Id);
    }

    private static string GetConfirmationEmailTemplate(string name, string link)
    {
        return $@"
<!DOCTYPE html>
<html lang='uk'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Підтвердження email</title>
</head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px; border-radius: 10px 10px 0 0;'>
        <h1 style='color: white; margin: 0; text-align: center;'>Promka</h1>
    </div>
    <div style='background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px;'>
        <h2>Вітаємо, {name}!</h2>
        <p>Дякуємо за реєстрацію на Promka. Для підтвердження вашого email, будь ласка, натисніть кнопку нижче:</p>
        <div style='text-align: center; margin: 30px 0;'>
            <a href='{link}' style='background: #667eea; color: white; padding: 15px 30px; text-decoration: none; border-radius: 5px; display: inline-block;'>Підтвердити email</a>
        </div>
        <p style='color: #666; font-size: 14px;'>Або скопіюйте це посилання в браузер:<br>{link}</p>
        <hr style='border: none; border-top: 1px solid #ddd; margin: 20px 0;'>
        <p style='color: #999; font-size: 12px;'>Якщо ви не реєструвалися на Promka, ігноруйте цей лист.</p>
    </div>
</body>
</html>";
    }

    private static string GetPasswordResetEmailTemplate(string name, string link)
    {
        return $@"
<!DOCTYPE html>
<html lang='uk'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Відновлення пароля</title>
</head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px; border-radius: 10px 10px 0 0;'>
        <h1 style='color: white; margin: 0; text-align: center;'>Promka</h1>
    </div>
    <div style='background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px;'>
        <h2>Вітаємо, {name}!</h2>
        <p>Ви запросили відновлення пароля. Натисніть кнопку нижче, щоб створити новий пароль:</p>
        <div style='text-align: center; margin: 30px 0;'>
            <a href='{link}' style='background: #667eea; color: white; padding: 15px 30px; text-decoration: none; border-radius: 5px; display: inline-block;'>Відновити пароль</a>
        </div>
        <p style='color: #666; font-size: 14px;'>Або скопіюйте це посилання в браузер:<br>{link}</p>
        <hr style='border: none; border-top: 1px solid #ddd; margin: 20px 0;'>
        <p style='color: #999; font-size: 12px;'>Посилання дійсне 24 години. Якщо ви не запитували відновлення пароля, ігноруйте цей лист.</p>
    </div>
</body>
</html>";
    }

    private static string GetTwoFactorCodeTemplate(string name, string code)
    {
        return $@"
<!DOCTYPE html>
<html lang='uk'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Код підтвердження</title>
</head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px; border-radius: 10px 10px 0 0;'>
        <h1 style='color: white; margin: 0; text-align: center;'>Promka</h1>
    </div>
    <div style='background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px;'>
        <h2>Вітаємо, {name}!</h2>
        <p>Ваш код підтвердження:</p>
        <div style='background: white; padding: 20px; text-align: center; border-radius: 10px; margin: 20px 0;'>
            <span style='font-size: 32px; font-weight: bold; letter-spacing: 10px; color: #667eea;'>{code}</span>
        </div>
        <p style='color: #666; font-size: 14px;'>Код дійсний 10 хвилин.</p>
        <hr style='border: none; border-top: 1px solid #ddd; margin: 20px 0;'>
        <p style='color: #999; font-size: 12px;'>Якщо це не ви, не передавайте код нікому.</p>
    </div>
</body>
</html>";
    }

    private static string GetOrderCreatedTemplate(string name, string orderNumber, decimal total)
    {
        return $@"
<!DOCTYPE html>
<html lang='uk'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Замовлення оформлено</title>
</head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px; border-radius: 10px 10px 0 0;'>
        <h1 style='color: white; margin: 0; text-align: center;'>Promka</h1>
    </div>
    <div style='background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px;'>
        <h2>Дякуємо за замовлення, {name}!</h2>
        <p>Ваше замовлення #{orderNumber} успішно оформлено.</p>
        <div style='background: white; padding: 20px; border-radius: 10px; margin: 20px 0;'>
            <p style='margin: 10px 0;'><strong>Номер замовлення:</strong> {orderNumber}</p>
            <p style='margin: 10px 0;'><strong>Сума:</strong> {total:N2} грн</p>
        </div>
        <p>Ми повідомимо вас про зміну статусу замовлення.</p>
        <hr style='border: none; border-top: 1px solid #ddd; margin: 20px 0;'>
        <p style='color: #999; font-size: 12px;'>З повагою, команда Promka</p>
    </div>
</body>
</html>";
    }

    private static string GetOrderStatusChangedTemplate(string name, string orderNumber, string status)
    {
        return $@"
<!DOCTYPE html>
<html lang='uk'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Зміна статусу замовлення</title>
</head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px; border-radius: 10px 10px 0 0;'>
        <h1 style='color: white; margin: 0; text-align: center;'>Promka</h1>
    </div>
    <div style='background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px;'>
        <h2>Вітаємо, {name}!</h2>
        <p>Статус вашого замовлення #{orderNumber} змінено:</p>
        <div style='background: white; padding: 20px; border-radius: 10px; margin: 20px 0; text-align: center;'>
            <span style='font-size: 24px; font-weight: bold; color: #667eea;'>{status}</span>
        </div>
        <p>Переглянути деталі замовлення ви можете в особистому кабінеті.</p>
        <hr style='border: none; border-top: 1px solid #ddd; margin: 20px 0;'>
        <p style='color: #999; font-size: 12px;'>З повагою, команда Promka</p>
    </div>
</body>
</html>";
    }

    private static string GetNewMessageTemplate(string name, string senderName, string preview)
    {
        return $@"
<!DOCTYPE html>
<html lang='uk'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Нове повідомлення</title>
</head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px; border-radius: 10px 10px 0 0;'>
        <h1 style='color: white; margin: 0; text-align: center;'>Promka</h1>
    </div>
    <div style='background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px;'>
        <h2>Вітаємо, {name}!</h2>
        <p>Ви отримали нове повідомлення від <strong>{senderName}</strong>:</p>
        <div style='background: white; padding: 20px; border-radius: 10px; margin: 20px 0; border-left: 4px solid #667eea;'>
            <p style='margin: 0; color: #666;'>{preview}</p>
        </div>
        <p>Відповісти на повідомлення ви можете в чаті на сайті.</p>
        <hr style='border: none; border-top: 1px solid #ddd; margin: 20px 0;'>
        <p style='color: #999; font-size: 12px;'>З повагою, команда Promka</p>
    </div>
</body>
</html>";
    }

    private static string GetProductApprovedTemplate(string name, string productTitle)
    {
        return $@"
<!DOCTYPE html>
<html lang='uk'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Товар схвалено</title>
</head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background: linear-gradient(135deg, #10b981 0%, #059669 100%); padding: 30px; border-radius: 10px 10px 0 0;'>
        <h1 style='color: white; margin: 0; text-align: center;'>Promka</h1>
    </div>
    <div style='background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px;'>
        <h2>Вітаємо, {name}!</h2>
        <p>Чудові новини! Ваш товар <strong>""{productTitle}""</strong> успішно пройшов модерацію і опублікований на маркетплейсі.</p>
        <div style='background: white; padding: 20px; border-radius: 10px; margin: 20px 0; text-align: center;'>
            <span style='color: #10b981; font-size: 48px;'>✓</span>
            <p style='margin: 10px 0;'><strong>Товар опубліковано</strong></p>
        </div>
        <p>Тепер ваш товар доступний покупцям. Ви можете переглянути його в особистому кабінеті.</p>
        <hr style='border: none; border-top: 1px solid #ddd; margin: 20px 0;'>
        <p style='color: #999; font-size: 12px;'>З повагою, команда Promka</p>
    </div>
</body>
</html>";
    }

    private static string GetProductRejectedTemplate(string name, string productTitle, string reason)
    {
        return $@"
<!DOCTYPE html>
<html lang='uk'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Товар відхилено</title>
</head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background: linear-gradient(135deg, #ef4444 0%, #dc2626 100%); padding: 30px; border-radius: 10px 10px 0 0;'>
        <h1 style='color: white; margin: 0; text-align: center;'>Promka</h1>
    </div>
    <div style='background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px;'>
        <h2>Вітаємо, {name}!</h2>
        <p>На жаль, ваш товар <strong>""{productTitle}""</strong> не пройшов модерацію.</p>
        <div style='background: white; padding: 20px; border-radius: 10px; margin: 20px 0;'>
            <p style='margin: 10px 0;'><strong>Причина відмови:</strong></p>
            <p style='margin: 10px 0; color: #666;'>{reason}</p>
        </div>
        <p>Ви можете виправити товар і відправити його на повторну модерацію в особистому кабінеті.</p>
        <hr style='border: none; border-top: 1px solid #ddd; margin: 20px 0;'>
        <p style='color: #999; font-size: 12px;'>З повагою, команда Promka</p>
    </div>
</body>
</html>";
    }
}
