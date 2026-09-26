using Marketplace.Application.DTO;
using Marketplace.Infrastructure.Services;

namespace Marketplace.IntegrationTests.Infrastructure;

public sealed class FakeExternalAuthService : IExternalAuthService
{
    public Task<ExternalUserInfo?> ResolveUserAsync(ExternalLoginRequest request, CancellationToken ct = default)
    {
        if (string.Equals(request.IdToken, "test-google-id-token", StringComparison.Ordinal))
        {
            return Task.FromResult<ExternalUserInfo?>(new("google-user@test.local", "Google Test User"));
        }

        if (string.Equals(request.AccessToken, "test-google-access-token", StringComparison.Ordinal))
        {
            return Task.FromResult<ExternalUserInfo?>(new("google-access@test.local", "Google Access User"));
        }

        return Task.FromResult<ExternalUserInfo?>(null);
    }
}
