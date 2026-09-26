namespace Marketplace.Infrastructure.Services;

public class JwtOptions
{
    public string Issuer { get; set; } = "Marketplace";
    public string Audience { get; set; } = "Marketplace";
    public string Key { get; set; } = "CHANGE_ME_SUPER_SECRET_KEY_32_CHARS_MIN";
    public int ExpiresMinutes { get; set; } = 180;
}
