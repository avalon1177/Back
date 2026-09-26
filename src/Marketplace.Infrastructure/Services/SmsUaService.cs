using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using Serilog;

namespace Marketplace.Infrastructure.Services;

public interface ISmsService
{
    Task SendSmsAsync(string phone, string message);
    Task SendTwoFactorCodeAsync(string phone, string code);
}

public class SmsUaService : ISmsService
{
    private readonly SmsUaOptions _options;
    private readonly AppDbContext _db;
    private readonly ILogger _logger;
    private readonly HttpClient _http;

    public SmsUaService(IOptions<SmsUaOptions> options, AppDbContext db, IHttpClientFactory httpFactory)
    {
        _options = options.Value;
        _db = db;
        _logger = Log.ForContext<SmsUaService>();
        _http = httpFactory.CreateClient();
    }

    public async Task SendSmsAsync(string phone, string message)
    {
        try
        {
            var cleanPhone = CleanPhoneNumber(phone);
            var url = "https://api.sms.ua/sendsms/json";

            var payload = new
            {
                auth = new { api_key = _options.ApiKey },
                messages = new[]
                {
                    new
                    {
                        sender = _options.Sender,
                        recipient = cleanPhone,
                        text = message
                    }
                }
            };

            var response = await _http.PostAsJsonAsync(url, payload);
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();

            if (result.TryGetProperty("success", out var success) && success.GetBoolean())
            {
                _logger.Information("SMS sent successfully to {Phone}", cleanPhone);
            }
            else
            {
                var error = result.TryGetProperty("error", out var errorProp) ? errorProp.GetString() : "Unknown error";
                _logger.Error("SMS sending failed: {Error}", error);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to send SMS to {Phone}", phone);
        }
    }

    public async Task SendTwoFactorCodeAsync(string phone, string code)
    {
        var message = $"Promka: Your verification code is {code}";
        await SendSmsAsync(phone, message);
    }

    public static string CleanPhoneNumber(string phone)
    {
        var sb = new StringBuilder();
        foreach (var c in phone)
        {
            if (char.IsDigit(c)) sb.Append(c);
        }
        var cleaned = sb.ToString();
        if (cleaned.StartsWith("380")) return cleaned;
        if (cleaned.StartsWith("0")) return "38" + cleaned;
        if (cleaned.StartsWith("80")) return "3" + cleaned;
        return cleaned.Length == 9 ? "38" + cleaned : cleaned;
    }
}
