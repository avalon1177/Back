using Marketplace.Infrastructure.Services;

namespace Marketplace.IntegrationTests.Infrastructure;

public sealed class FakeSmsService : ISmsService
{
    public Task SendSmsAsync(string phone, string message) => Task.CompletedTask;

    public Task SendTwoFactorCodeAsync(string phone, string code) => Task.CompletedTask;
}
