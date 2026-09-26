using System.Net.Http.Headers;
using System.Net.Http.Json;
using Marketplace.Application.DTO;
using Xunit;

namespace Marketplace.IntegrationTests.Infrastructure;

public static class AuthTestClientExtensions
{
    public static async Task<string> LoginAsAdminAsync(this HttpClient client)
    {
        var session = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@integration.test", "Admin123!", "admin-device"));
        session.EnsureSuccessStatusCode();

        var payload = await session.Content.ReadFromJsonAsync<AuthSessionResponse>();
        Assert.NotNull(payload);
        return payload.AccessToken;
    }

    public static async Task<AuthSessionResponse> RegisterConfirmAndLoginSellerAsync(
        this HttpClient client,
        MarketplaceApiFactory factory,
        string email,
        string password,
        string displayName,
        string storeName)
    {
        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password, displayName, "Seller", storeName));
        register.EnsureSuccessStatusCode();

        var confirmation = factory.EmailService.GetLatest(email, FakeEmailKind.Confirmation);
        var confirm = await client.PostAsJsonAsync("/api/auth/confirm-email", new ConfirmEmailRequest(email, confirmation.Token!));
        confirm.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password, "seller-device"));
        login.EnsureSuccessStatusCode();

        var session = await login.Content.ReadFromJsonAsync<AuthSessionResponse>();
        Assert.NotNull(session);
        return session;
    }

    public static async Task<T> PostJsonAndReadAsync<T>(this HttpClient client, string uri, object body)
    {
        var response = await client.PostAsJsonAsync(uri, body);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(payload);
        return payload;
    }

    public static void SetBearerToken(this HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }
}
