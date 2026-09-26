using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Marketplace.Infrastructure.Services;

public interface ILiqPayService
{
    LiqPayCheckoutPayload CreateCheckoutPayload(decimal amount, string description, string orderId, string? resultUrl = null, string? serverUrl = null);
    bool IsConfigured();
    bool ValidateSignature(string data, string signature);
    LiqPayCallbackData ParseCallback(string data);
}

public class LiqPayService : ILiqPayService
{
    private readonly LiqPayOptions _options;

    public LiqPayService(IOptions<LiqPayOptions> options)
    {
        _options = options.Value;
    }

    public bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(_options.PublicKey) &&
               !string.IsNullOrWhiteSpace(_options.PrivateKey);
    }

    public LiqPayCheckoutPayload CreateCheckoutPayload(decimal amount, string description, string orderId, string? resultUrl = null, string? serverUrl = null)
    {
        var request = new LiqPayCheckoutRequest(
            Action: "pay",
            Amount: amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            Currency: _options.Currency,
            Description: description,
            OrderId: orderId,
            Version: "3",
            PublicKey: _options.PublicKey,
            ResultUrl: string.IsNullOrWhiteSpace(resultUrl) ? _options.ResultUrl : resultUrl!,
            ServerUrl: string.IsNullOrWhiteSpace(serverUrl) ? _options.ServerUrl : serverUrl!);

        var json = JsonSerializer.Serialize(request, JsonOptions);
        var data = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        var signature = CreateSignature(data);
        return new LiqPayCheckoutPayload(_options.CheckoutUrl, data, signature, request.OrderId, request.ResultUrl, request.ServerUrl);
    }

    public bool ValidateSignature(string data, string signature)
    {
        var expected = CreateSignature(data);
        return string.Equals(expected, signature, StringComparison.Ordinal);
    }

    public LiqPayCallbackData ParseCallback(string data)
    {
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(data));
        var payload = JsonSerializer.Deserialize<LiqPayCallbackData>(json, JsonOptions);
        if (payload is null)
        {
            throw new InvalidOperationException("Invalid LiqPay callback payload");
        }

        return payload;
    }

    private string CreateSignature(string data)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(_options.PrivateKey + data + _options.PrivateKey);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };
}

public record LiqPayCheckoutRequest(
    string Action,
    string Amount,
    string Currency,
    string Description,
    string OrderId,
    string Version,
    string PublicKey,
    string ResultUrl,
    string ServerUrl);

public record LiqPayCheckoutPayload(
    string CheckoutUrl,
    string Data,
    string Signature,
    string OrderId,
    string ResultUrl,
    string ServerUrl);

public record LiqPayCallbackData(
    string? OrderId,
    string? PaymentId,
    string? Status,
    string? TransactionId,
    string? Amount,
    string? Currency,
    string? Description,
    string? SenderPhone,
    string? ErrCode,
    string? ErrDescription,
    string? PublicKey,
    string? CreateDate,
    string? EndDate,
    string? AcqId,
    string? Action,
    string? Type,
    string? Version);
