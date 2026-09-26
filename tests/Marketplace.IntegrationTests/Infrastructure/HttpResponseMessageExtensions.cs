using System.Text.Json;
using Xunit;

namespace Marketplace.IntegrationTests.Infrastructure;

public static class HttpResponseMessageExtensions
{
    public static async Task<JsonDocument> ReadJsonAsync(this HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    public static async Task<JsonElement> ReadJsonRootAsync(this HttpResponseMessage response)
    {
        var document = await response.ReadJsonAsync();
        return document.RootElement.Clone();
    }

    public static string GetRefreshTokenCookie(this HttpResponseMessage response)
    {
        var cookie = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(x => x.StartsWith("refreshToken=", StringComparison.OrdinalIgnoreCase))
            : null;

        Assert.False(string.IsNullOrWhiteSpace(cookie));
        return cookie!.Split(';', 2)[0];
    }
}
