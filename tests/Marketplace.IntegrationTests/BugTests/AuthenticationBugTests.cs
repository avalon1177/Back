using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Marketplace.Application.DTO;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Marketplace.IntegrationTests.BugTests;

public sealed class AuthenticationBugTests : IntegrationTestBase
{
    public AuthenticationBugTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task TokenRefresh_WithInvalidToken_ShouldReturnBadRequest()
    {
        using var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequest("invalid-token", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_WithNonexistentEmail_ShouldReturnOk()
    {
        using var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest("nonexistent@test.com"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WithInvalidToken_ShouldReturnBadRequest()
    {
        var actor = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "reset-pwd-invalid");

        using var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest($"{actor.Email}@test.com", "invalid-token", "NewTest123!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmEmail_WithInvalidToken_ShouldReturnBadRequest()
    {
        var actor = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "confirm-email-invalid");

        using var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/confirm-email",
            new ConfirmEmailRequest($"{actor.Email}@test.com", "invalid-token"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExternalLogin_WithUnsupportedProvider_ShouldReturnBadRequest()
    {
        using var client = Factory.CreateClient();
        
        var response = await client.PostAsJsonAsync("/api/auth/external-login",
            new ExternalLoginRequest("Facebook", null, "token", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExternalLogin_WithUnsupportedProviderMessage_ShouldContainUnsupportedProviderCode()
    {
        using var client = Factory.CreateClient();
        
        var response = await client.PostAsJsonAsync("/api/auth/external-login",
            new ExternalLoginRequest("Facebook", null, "token", null));
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(content.TryGetProperty("code", out var code));
        Assert.Equal("UNSUPPORTED_PROVIDER", code.GetString());
    }
}
