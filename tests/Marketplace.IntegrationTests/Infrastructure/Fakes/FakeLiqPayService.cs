using System.Text;
using System.Text.Json;
using Marketplace.Infrastructure.Services;

namespace Marketplace.IntegrationTests.Infrastructure;

public sealed class FakeLiqPayService : ILiqPayService
{
    public LiqPayCheckoutPayload CreateCheckoutPayload(decimal amount, string description, string orderId, string? resultUrl = null, string? serverUrl = null)
    {
        var json = JsonSerializer.Serialize(new
        {
            amount,
            description,
            order_id = orderId,
            result_url = resultUrl ?? "https://frontend.test/payment-result",
            server_url = serverUrl ?? "https://api.test/api/payments/liqpay/callback"
        });

        var data = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        return new LiqPayCheckoutPayload(
            "https://liqpay.test/checkout",
            data,
            "test-signature",
            orderId,
            resultUrl ?? "https://frontend.test/payment-result",
            serverUrl ?? "https://api.test/api/payments/liqpay/callback");
    }

    public bool IsConfigured() => true;

    public bool ValidateSignature(string data, string signature) => signature == "test-signature";

    public LiqPayCallbackData ParseCallback(string data)
    {
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(data));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        return new LiqPayCallbackData(
            root.TryGetProperty("order_id", out var orderId) ? orderId.GetString() : null,
            root.TryGetProperty("payment_id", out var paymentId) ? paymentId.GetString() : null,
            root.TryGetProperty("status", out var status) ? status.GetString() : null,
            root.TryGetProperty("transaction_id", out var transactionId) ? transactionId.GetString() : null,
            root.TryGetProperty("amount", out var amount) ? amount.GetString() : null,
            root.TryGetProperty("currency", out var currency) ? currency.GetString() : null,
            root.TryGetProperty("description", out var description) ? description.GetString() : null,
            root.TryGetProperty("sender_phone", out var senderPhone) ? senderPhone.GetString() : null,
            root.TryGetProperty("err_code", out var errCode) ? errCode.GetString() : null,
            root.TryGetProperty("err_description", out var errDescription) ? errDescription.GetString() : null,
            root.TryGetProperty("public_key", out var publicKey) ? publicKey.GetString() : null,
            root.TryGetProperty("create_date", out var createDate) ? createDate.GetString() : null,
            root.TryGetProperty("end_date", out var endDate) ? endDate.GetString() : null,
            root.TryGetProperty("acq_id", out var acqId) ? acqId.GetString() : null,
            root.TryGetProperty("action", out var action) ? action.GetString() : null,
            root.TryGetProperty("type", out var type) ? type.GetString() : null,
            root.TryGetProperty("version", out var version) ? version.GetString() : null);
    }
}
