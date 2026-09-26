namespace Marketplace.Infrastructure.Services;

public class LiqPayOptions
{
    public string PublicKey { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public string CheckoutUrl { get; set; } = "https://www.liqpay.ua/api/3/checkout";
    public string ApiUrl { get; set; } = "https://www.liqpay.ua/api/request";
    public string Currency { get; set; } = "UAH";
    public string ServerUrl { get; set; } = "http://localhost:5000/api/payments/liqpay/callback";
    public string ResultUrl { get; set; } = "http://localhost:5000/payment-result";
}
