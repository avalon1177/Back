using System.Net;
using System.Net.Http.Json;
using Marketplace.Application.DTO;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Marketplace.IntegrationTests.Coverage;

public sealed class SystemAuthUserEndpointsTests : IntegrationTestBase
{
    public SystemAuthUserEndpointsTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Theory]
    [InlineData("health")]
    [InlineData("root")]
    [InlineData("auth-register")]
    [InlineData("auth-login")]
    [InlineData("auth-refresh")]
    [InlineData("auth-2fa-verify")]
    [InlineData("auth-external-login")]
    [InlineData("auth-confirm-email")]
    [InlineData("auth-resend-confirmation")]
    [InlineData("auth-forgot-password")]
    [InlineData("auth-reset-password")]
    [InlineData("auth-me")]
    [InlineData("users-me")]
    public async Task SuccessCases(string endpointId)
    {
        switch (endpointId)
        {
            case "health":
            {
                using var client = Factory.CreateClient();
                var response = await client.GetAsync("/health");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "root":
            {
                using var client = Factory.CreateClient();
                var response = await client.GetAsync("/");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "auth-register":
            {
                using var client = Factory.CreateClient();
                var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("register-success@test.local", "Register123!", "Register User", UserRole.Buyer, null));
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var email = Factory.EmailService.GetLatest("register-success@test.local", FakeEmailKind.Confirmation);
                Assert.NotNull(email.Token);
                break;
            }
            case "auth-login":
            {
                var actor = await Scenario.CreateActorAsync(UserRole.Buyer, emailConfirmed: true, emailPrefix: "login-success");
                using var client = Factory.CreateClient();
                var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(actor.Email, actor.Password, "device-1"));
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "auth-refresh":
            {
                var actor = await Scenario.CreateActorAsync(UserRole.Buyer, emailConfirmed: true, emailPrefix: "refresh-success");
                using var client = Factory.CreateClient();
                var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(actor.Email, actor.Password, "device-1"));
                Assert.Equal(HttpStatusCode.OK, login.StatusCode);

                using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh")
                {
                    Content = JsonContent.Create(new RefreshTokenRequest("ignored", "device-2"))
                };
                refreshRequest.Headers.Add("Cookie", login.GetRefreshTokenCookie());

                var refresh = await client.SendAsync(refreshRequest);
                Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
                break;
            }
            case "auth-2fa-verify":
            {
                var actor = await Scenario.CreateActorAsync(UserRole.Buyer, emailConfirmed: true, twoFactorEnabled: true, emailPrefix: "2fa-success");
                using var client = Factory.CreateClient();
                var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(actor.Email, actor.Password, "device-1"));
                Assert.Equal(HttpStatusCode.Accepted, login.StatusCode);

                var pending = await login.Content.ReadFromJsonAsync<TwoFactorRequiredResponse>();
                Assert.NotNull(pending);

                var code = await Scenario.GenerateAuthenticatorCodeAsync(actor);
                var verify = await client.PostAsJsonAsync("/api/auth/2fa/verify", new TwoFactorVerifyRequest(pending.TwoFactorToken, code, "device-1"));
                Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
                break;
            }
            case "auth-external-login":
            {
                using var client = Factory.CreateClient();
                var response = await client.PostAsJsonAsync("/api/auth/external-login", new ExternalLoginRequest("google", "test-google-id-token", null, "device-1"));
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "auth-confirm-email":
            {
                var actor = await Scenario.CreateActorAsync(UserRole.Buyer, emailConfirmed: false, emailPrefix: "confirm-success");
                using var scope = Factory.Services.CreateScope();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
                var user = await userManager.FindByIdAsync(actor.UserId.ToString());
                Assert.NotNull(user);
                var token = await userManager.GenerateEmailConfirmationTokenAsync(user!);

                using var client = Factory.CreateClient();
                var response = await client.PostAsJsonAsync("/api/auth/confirm-email", new ConfirmEmailRequest(actor.Email, token));
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "auth-resend-confirmation":
            {
                var actor = await Scenario.CreateActorAsync(UserRole.Buyer, emailConfirmed: false, emailPrefix: "resend-success");
                using var client = Factory.CreateClient();
                var response = await client.PostAsJsonAsync("/api/auth/resend-confirmation", new ResendConfirmationRequest(actor.Email));
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var email = Factory.EmailService.GetLatest(actor.Email, FakeEmailKind.Confirmation);
                Assert.NotNull(email.Token);
                break;
            }
            case "auth-forgot-password":
            {
                var actor = await Scenario.CreateActorAsync(UserRole.Buyer, emailConfirmed: true, emailPrefix: "forgot-success");
                using var client = Factory.CreateClient();
                var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(actor.Email));
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var email = Factory.EmailService.GetLatest(actor.Email, FakeEmailKind.PasswordReset);
                Assert.NotNull(email.Token);
                break;
            }
            case "auth-reset-password":
            {
                var actor = await Scenario.CreateActorAsync(UserRole.Buyer, emailConfirmed: true, emailPrefix: "reset-success");
                using var scope = Factory.Services.CreateScope();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
                var user = await userManager.FindByIdAsync(actor.UserId.ToString());
                Assert.NotNull(user);
                var token = await userManager.GeneratePasswordResetTokenAsync(user!);

                using var client = Factory.CreateClient();
                var response = await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest(actor.Email, token, "NewPass123!"));
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "auth-me":
            {
                var (actor, client) = await Scenario.CreateAuthenticatedActorAsync(UserRole.Buyer, emailPrefix: "me-success");
                using (client)
                {
                    var response = await client.GetAsync("/api/auth/me");
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                }
                break;
            }
            case "users-me":
            {
                var (_, client) = await Scenario.CreateAuthenticatedActorAsync(UserRole.Buyer, emailPrefix: "user-update-success");
                using (client)
                {
                    var response = await client.PutAsJsonAsync("/api/users/me", new UserUpdateRequest("Updated Name", "+10000000001", new DateTime(2000, 1, 1)));
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                }
                break;
            }
            default:
                throw new NotSupportedException(endpointId);
        }
    }

    [Theory]
    [InlineData("health")]
    [InlineData("root")]
    [InlineData("auth-register")]
    [InlineData("auth-login")]
    [InlineData("auth-refresh")]
    [InlineData("auth-2fa-verify")]
    [InlineData("auth-external-login")]
    [InlineData("auth-confirm-email")]
    [InlineData("auth-resend-confirmation")]
    [InlineData("auth-forgot-password")]
    [InlineData("auth-reset-password")]
    [InlineData("auth-me")]
    [InlineData("users-me")]
    public async Task InvalidCases(string endpointId)
    {
        switch (endpointId)
        {
            case "health":
            {
                using var client = Factory.CreateClient();
                var response = await client.PostAsync("/health", content: null);
                AssertInvalidMethod(response.StatusCode);
                break;
            }
            case "root":
            {
                using var client = Factory.CreateClient();
                var response = await client.PostAsync("/", content: null);
                AssertInvalidMethod(response.StatusCode);
                break;
            }
            case "auth-register":
            {
                using var client = Factory.CreateClient();
                var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("bad", "123", "A", "Nope", null));
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                break;
            }
            case "auth-login":
            {
                var actor = await Scenario.CreateActorAsync(UserRole.Buyer, emailConfirmed: true, emailPrefix: "login-invalid");
                using var client = Factory.CreateClient();
                var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(actor.Email, "WrongPassword!", "device-1"));
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                break;
            }
            case "auth-refresh":
            {
                using var client = Factory.CreateClient();
                var response = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest("bogus-token", "device-1"));
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                break;
            }
            case "auth-2fa-verify":
            {
                using var client = Factory.CreateClient();
                var response = await client.PostAsJsonAsync("/api/auth/2fa/verify", new TwoFactorVerifyRequest("invalid", "123456", "device-1"));
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                break;
            }
            case "auth-external-login":
            {
                using var client = Factory.CreateClient();
                var response = await client.PostAsJsonAsync("/api/auth/external-login", new ExternalLoginRequest("github", "token", null, "device-1"));
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                break;
            }
            case "auth-confirm-email":
            {
                var actor = await Scenario.CreateActorAsync(UserRole.Buyer, emailConfirmed: false, emailPrefix: "confirm-invalid");
                using var client = Factory.CreateClient();
                var response = await client.PostAsJsonAsync("/api/auth/confirm-email", new ConfirmEmailRequest(actor.Email, "bad-token"));
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                break;
            }
            case "auth-resend-confirmation":
            {
                var actor = await Scenario.CreateActorAsync(UserRole.Buyer, emailConfirmed: true, emailPrefix: "resend-invalid");
                using var client = Factory.CreateClient();
                var response = await client.PostAsJsonAsync("/api/auth/resend-confirmation", new ResendConfirmationRequest(actor.Email));
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                break;
            }
            case "auth-forgot-password":
            {
                using var client = Factory.CreateClient();
                var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest("invalid-email"));
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                break;
            }
            case "auth-reset-password":
            {
                using var client = Factory.CreateClient();
                var response = await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest("missing@test.local", "bad-token", "NewPass123!"));
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                break;
            }
            case "auth-me":
            {
                using var client = Factory.CreateClient();
                var response = await client.GetAsync("/api/auth/me");
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                break;
            }
            case "users-me":
            {
                using var client = Factory.CreateClient();
                var response = await client.PutAsJsonAsync("/api/users/me", new UserUpdateRequest("No User", null, null));
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                break;
            }
            default:
                throw new NotSupportedException(endpointId);
        }
    }

    private static void AssertInvalidMethod(HttpStatusCode statusCode)
    {
        Assert.Contains(statusCode, new[] { HttpStatusCode.MethodNotAllowed, HttpStatusCode.NotFound });
    }
}
