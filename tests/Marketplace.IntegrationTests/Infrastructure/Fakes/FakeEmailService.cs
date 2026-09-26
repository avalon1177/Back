using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.Infrastructure.Services;

namespace Marketplace.IntegrationTests.Infrastructure;

public sealed class FakeEmailService : IEmailService
{
    private readonly List<SentEmail> _messages = [];
    private readonly object _lock = new();

    public void Reset()
    {
        lock (_lock)
        {
            _messages.Clear();
        }
    }

    public SentEmail GetLatest(string email, FakeEmailKind kind)
    {
        lock (_lock)
        {
            return _messages.Last(x =>
                string.Equals(x.To, email, StringComparison.OrdinalIgnoreCase) &&
                x.Kind == kind);
        }
    }

    public Task SendEmailAsync(string to, string subject, string htmlBody, EmailType type, Guid? userId = null)
    {
        Add(new SentEmail(to, FakeEmailKind.Generic, subject, htmlBody, null, null, null, userId));
        return Task.CompletedTask;
    }

    public Task SendConfirmationEmailAsync(AppUser user, string token, string baseUrl)
    {
        Add(new SentEmail(user.Email ?? string.Empty, FakeEmailKind.Confirmation, "Confirm email", null, token, baseUrl, null, user.Id));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetEmailAsync(AppUser user, string token, string baseUrl)
    {
        Add(new SentEmail(user.Email ?? string.Empty, FakeEmailKind.PasswordReset, "Reset password", null, token, baseUrl, null, user.Id));
        return Task.CompletedTask;
    }

    public Task SendTwoFactorCodeAsync(AppUser user, string code)
    {
        Add(new SentEmail(user.Email ?? string.Empty, FakeEmailKind.TwoFactor, "Two-factor code", null, null, null, code, user.Id));
        return Task.CompletedTask;
    }

    public Task SendOrderCreatedNotificationAsync(AppUser user, string orderNumber, decimal total)
    {
        Add(new SentEmail(user.Email ?? string.Empty, FakeEmailKind.OrderCreated, "Order created", null, null, null, orderNumber, user.Id));
        return Task.CompletedTask;
    }

    public Task SendOrderStatusChangedNotificationAsync(AppUser user, string orderNumber, string status)
    {
        Add(new SentEmail(user.Email ?? string.Empty, FakeEmailKind.OrderStatusChanged, "Order status changed", null, null, null, $"{orderNumber}:{status}", user.Id));
        return Task.CompletedTask;
    }

    public Task SendNewMessageNotificationAsync(AppUser user, string senderName, string preview)
    {
        Add(new SentEmail(user.Email ?? string.Empty, FakeEmailKind.ChatMessage, "New message", preview, null, null, senderName, user.Id));
        return Task.CompletedTask;
    }

    public Task SendProductApprovedNotificationAsync(AppUser user, string productTitle)
    {
        Add(new SentEmail(user.Email ?? string.Empty, FakeEmailKind.ProductApproved, "Product approved", null, null, null, productTitle, user.Id));
        return Task.CompletedTask;
    }

    public Task SendProductRejectedNotificationAsync(AppUser user, string productTitle, string reason)
    {
        Add(new SentEmail(user.Email ?? string.Empty, FakeEmailKind.ProductRejected, "Product rejected", reason, null, null, productTitle, user.Id));
        return Task.CompletedTask;
    }

    private void Add(SentEmail message)
    {
        lock (_lock)
        {
            _messages.Add(message);
        }
    }
}

public enum FakeEmailKind
{
    Generic,
    Confirmation,
    PasswordReset,
    TwoFactor,
    OrderCreated,
    OrderStatusChanged,
    ChatMessage,
    ProductApproved,
    ProductRejected
}

public sealed record SentEmail(
    string To,
    FakeEmailKind Kind,
    string Subject,
    string? Body,
    string? Token,
    string? BaseUrl,
    string? Metadata,
    Guid? UserId);
