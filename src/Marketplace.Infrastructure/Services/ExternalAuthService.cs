using System.Text.Encodings.Web;
using System.Net.Http.Json;
using Google.Apis.Auth;
using Marketplace.Application.DTO;

namespace Marketplace.Infrastructure.Services;

public interface IExternalAuthService
{
    Task<ExternalUserInfo?> ResolveUserAsync(ExternalLoginRequest request, CancellationToken ct = default);
}

public sealed record ExternalUserInfo(string Email, string Name);

public sealed class GoogleExternalAuthService : IExternalAuthService
{
    public async Task<ExternalUserInfo?> ResolveUserAsync(ExternalLoginRequest request, CancellationToken ct = default)
    {
        string? email = null;
        string? name = null;

        if (!string.IsNullOrWhiteSpace(request.IdToken))
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken);
            email = payload.Email;
            name = payload.Name ?? payload.GivenName ?? payload.Email;
        }
        else if (!string.IsNullOrWhiteSpace(request.AccessToken))
        {
            using var http = new HttpClient();
            var url = "https://www.googleapis.com/oauth2/v3/userinfo?access_token=" + UrlEncoder.Default.Encode(request.AccessToken);
            var info = await http.GetFromJsonAsync<GoogleUserInfo>(url, ct);
            email = info?.Email;
            name = info?.Name ?? info?.GivenName ?? info?.Email;
        }

        return string.IsNullOrWhiteSpace(email)
            ? null
            : new ExternalUserInfo(email.Trim(), string.IsNullOrWhiteSpace(name) ? email.Trim() : name.Trim());
    }

    private sealed record GoogleUserInfo(string? Email, string? Name, string? GivenName);
}
