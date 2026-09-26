using Microsoft.AspNetCore.Http;

namespace Marketplace.API.Common;

public static class HttpResultsExtensions
{
    private const string RefreshTokenCookieName = "refreshToken";

    public static void SetRefreshTokenCookie(this HttpResponse response, string refreshToken, DateTime expiresAtUtc)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = expiresAtUtc,
            Path = "/"
        };
        response.Cookies.Append(RefreshTokenCookieName, refreshToken, cookieOptions);
    }

    public static void DeleteRefreshTokenCookie(this HttpResponse response)
    {
        response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions { Path = "/" });
    }

    public static string? GetRefreshTokenCookie(this HttpRequest request)
    {
        return request.Cookies[RefreshTokenCookieName];
    }
}
