using System.Net.Mail;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Marketplace.Infrastructure.Tests;

public static class TestDbContextFactory
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        var context = new AppDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }
}

public sealed class FakeSmtpClient : ISmtpClientWrapper
{
    public int SendCount { get; private set; }
    public List<MailMessage> SentMessages { get; } = new();
    public bool ShouldFail { get; set; }
    public Exception? FailureException { get; set; } = new("SMTP failure");

    public Task SendAsync(MailMessage message, CancellationToken cancellationToken = default)
    {
        SendCount++;
        SentMessages.Add(message);
        if (ShouldFail)
        {
            throw FailureException!;
        }
        return Task.CompletedTask;
    }
}

public sealed class EmailServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly FakeSmtpClient _smtpClient;

    public EmailServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _smtpClient = new FakeSmtpClient();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task SendEmailAsync_WithValidInput_LogsEmail()
    {
        var service = new EmailService(_smtpClient, _db);

        await service.SendEmailAsync("user@test.com", "Test Subject", "<p>Test Body</p>", EmailType.Confirmation);

        var log = await _db.EmailLogs.FirstOrDefaultAsync();
        Assert.NotNull(log);
        Assert.Equal("user@test.com", log.To);
        Assert.Equal("Test Subject", log.Subject);
        Assert.Equal(EmailType.Confirmation, log.Type);
    }

    [Fact]
    public async Task SendEmailAsync_WithNullUserId_LogsNullUserId()
    {
        var service = new EmailService(_smtpClient, _db);

        await service.SendEmailAsync("user@test.com", "Test", "Body", EmailType.Confirmation, null);

        var log = await _db.EmailLogs.FirstOrDefaultAsync();
        Assert.NotNull(log);
        Assert.Null(log.UserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendEmailAsync_WithEmptyTo_DoesNotLog(string emptyTo)
    {
        var service = new EmailService(_smtpClient, _db);

        await service.SendEmailAsync(emptyTo, "Subject", "Body", EmailType.Confirmation);

        var log = await _db.EmailLogs.FirstOrDefaultAsync();
        Assert.Null(log);
    }

    [Fact]
    public async Task SendConfirmationEmailAsync_WithValidUser_SendsCorrectly()
    {
        var service = new EmailService(_smtpClient, _db);
        var user = new AppUser { Id = Guid.NewGuid(), Email = "user@test.com", DisplayName = "Test User" };

        await service.SendConfirmationEmailAsync(user, "test-token", "https://frontend.test");

        var log = await _db.EmailLogs.FirstOrDefaultAsync(x => x.Type == EmailType.Confirmation);
        Assert.NotNull(log);
        Assert.Contains("test-token", log.Body ?? "");
        Assert.Contains("https://frontend.test", log.Body ?? "");
    }

    [Fact]
    public async Task SendOrderCreatedNotificationAsync_IncludesCorrectDetails()
    {
        var service = new EmailService(_smtpClient, _db);
        var user = new AppUser { Id = Guid.NewGuid(), Email = "user@test.com", DisplayName = "Test User" };

        await service.SendOrderCreatedNotificationAsync(user, "ORD-123", 150.50m);

        var log = await _db.EmailLogs.FirstOrDefaultAsync(x => x.Type == EmailType.OrderCreated);
        Assert.NotNull(log);
        Assert.Contains("ORD-123", log.Body ?? "");
        Assert.Contains("150", log.Body ?? "");
    }

    [Fact]
    public async Task SendEmailAsync_WhenSmtpFails_LogsFailedStatus()
    {
        _smtpClient.ShouldFail = true;
        var service = new EmailService(_smtpClient, _db);

        await service.SendEmailAsync("user@test.com", "Test", "Body", EmailType.Confirmation);

        var log = await _db.EmailLogs.FirstOrDefaultAsync();
        Assert.NotNull(log);
        Assert.Equal(EmailStatus.Failed, log.Status);
        Assert.NotNull(log.ErrorMessage);
    }
}

public sealed class NotificationServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly NotificationService _service;
    private readonly Guid _testUserId;

    public NotificationServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _service = new NotificationService(_db);
        
        var user = new AppUser 
        { 
            Id = Guid.NewGuid(), 
            Email = "test@test.com", 
            UserName = "test@test.com",
            DisplayName = "Test User"
        };
        _db.Users.Add(user);
        _db.SaveChanges();
        _testUserId = user.Id;
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task CreateNotificationAsync_WithValidInput_CreatesNotification()
    {
        await _service.CreateNotificationAsync(_testUserId, "Test Title", "Test Text");

        var notification = await _db.Notifications.FirstOrDefaultAsync();
        Assert.NotNull(notification);
        Assert.Equal(_testUserId, notification.UserId);
        Assert.Equal("Test Title", notification.Title);
        Assert.Equal("Test Text", notification.Text);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public async Task NotifyProductApprovedAsync_CreatesCorrectNotification()
    {
        await _service.NotifyProductApprovedAsync(_testUserId, "Test Product");

        var notification = await _db.Notifications.FirstOrDefaultAsync();
        Assert.NotNull(notification);
        Assert.Equal(_testUserId, notification.UserId);
        Assert.Equal("Товар схвалено", notification.Title);
        Assert.Contains("Test Product", notification.Text);
    }

    [Fact]
    public async Task NotifyProductRejectedAsync_CreatesCorrectNotification()
    {
        await _service.NotifyProductRejectedAsync(_testUserId, "Test Product", "Invalid description");

        var notification = await _db.Notifications.FirstOrDefaultAsync();
        Assert.NotNull(notification);
        Assert.Equal(_testUserId, notification.UserId);
        Assert.Equal("Товар відхилено", notification.Title);
        Assert.Contains("Test Product", notification.Text);
        Assert.Contains("Invalid description", notification.Text);
    }
}

public sealed class SmsServiceTests : IDisposable
{
    private readonly Mock<IOptions<SmsUaOptions>> _optionsMock;
    private readonly AppDbContext _db;

    public SmsServiceTests()
    {
        _db = TestDbContextFactory.Create();
        var options = new SmsUaOptions { ApiKey = "test-api-key", Sender = "Promka" };
        _optionsMock = new Mock<IOptions<SmsUaOptions>>();
        _optionsMock.Setup(x => x.Value).Returns(options);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Theory]
    [InlineData("+380501234567", "380501234567")]
    [InlineData("0501234567", "380501234567")]
    [InlineData("80501234567", "380501234567")]
    [InlineData("380501234567", "380501234567")]
    public void CleanPhoneNumber_VariousFormats_NormalizesCorrectly(string input, string expected)
    {
        var result = SmsUaService.CleanPhoneNumber(input);
        Assert.Equal(expected, result);
    }
}

public sealed class LiqPayServiceTests
{
    private readonly Mock<IOptions<LiqPayOptions>> _optionsMock;
    private readonly LiqPayOptions _options;

    public LiqPayServiceTests()
    {
        _options = new LiqPayOptions
        {
            PublicKey = "test-public-key",
            PrivateKey = "test-private-key",
            CheckoutUrl = "https://liqpay.ua/checkout",
            ServerUrl = "https://api.test/api/payments/liqpay/callback",
            ResultUrl = "https://frontend.test/payment-result",
            Currency = "UAH"
        };
        _optionsMock = new Mock<IOptions<LiqPayOptions>>();
        _optionsMock.Setup(x => x.Value).Returns(_options);
    }

    [Fact]
    public void IsConfigured_WithValidKeys_ReturnsTrue()
    {
        var service = new LiqPayService(_optionsMock.Object);
        Assert.True(service.IsConfigured());
    }

    [Fact]
    public void IsConfigured_WithEmptyPublicKey_ReturnsFalse()
    {
        var options = new LiqPayOptions
        {
            PublicKey = "",
            PrivateKey = "test-private-key"
        };
        var mock = new Mock<IOptions<LiqPayOptions>>();
        mock.Setup(x => x.Value).Returns(options);

        var service = new LiqPayService(mock.Object);
        Assert.False(service.IsConfigured());
    }

    [Fact]
    public void IsConfigured_WithEmptyPrivateKey_ReturnsFalse()
    {
        var options = new LiqPayOptions
        {
            PublicKey = "test-public-key",
            PrivateKey = ""
        };
        var mock = new Mock<IOptions<LiqPayOptions>>();
        mock.Setup(x => x.Value).Returns(options);

        var service = new LiqPayService(mock.Object);
        Assert.False(service.IsConfigured());
    }

    [Fact]
    public void CreateCheckoutPayload_WithValidInput_ReturnsPayload()
    {
        var service = new LiqPayService(_optionsMock.Object);

        var payload = service.CreateCheckoutPayload(100.50m, "Test Order", "order-123");

        Assert.NotNull(payload);
        Assert.Equal("https://liqpay.ua/checkout", payload.CheckoutUrl);
        Assert.Equal("order-123", payload.OrderId);
        Assert.NotEmpty(payload.Data);
        Assert.NotEmpty(payload.Signature);
    }

    [Fact]
    public void CreateCheckoutPayload_WithCustomUrls_UsesCustomUrls()
    {
        var service = new LiqPayService(_optionsMock.Object);

        var payload = service.CreateCheckoutPayload(100m, "Test", "order-1", "https://custom.result", "https://custom.server");

        Assert.Equal("https://custom.result", payload.ResultUrl);
        Assert.Equal("https://custom.server", payload.ServerUrl);
    }

    [Fact]
    public void ValidateSignature_WithValidSignature_ReturnsTrue()
    {
        var service = new LiqPayService(_optionsMock.Object);
        var payload = service.CreateCheckoutPayload(100m, "Test", "order-1");

        Assert.True(service.ValidateSignature(payload.Data, payload.Signature));
    }

    [Fact]
    public void ValidateSignature_WithInvalidSignature_ReturnsFalse()
    {
        var service = new LiqPayService(_optionsMock.Object);

        Assert.False(service.ValidateSignature("some-data", "invalid-signature"));
    }

    [Fact]
    public void ParseCallback_WithValidData_ReturnsCallbackData()
    {
        var service = new LiqPayService(_optionsMock.Object);
        var callbackJson = @"{
            ""order_id"": ""order-123"",
            ""status"": ""success"",
            ""payment_id"": ""PAY-123"",
            ""amount"": ""100.50""
        }";
        var data = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(callbackJson));

        var result = service.ParseCallback(data);

        Assert.Equal("order-123", result.OrderId);
        Assert.Equal("success", result.Status);
        Assert.Equal("PAY-123", result.PaymentId);
    }

    [Fact]
    public void ParseCallback_WithInvalidBase64_ThrowsException()
    {
        var service = new LiqPayService(_optionsMock.Object);

        Assert.Throws<FormatException>(() => service.ParseCallback("not-valid-base64!!!"));
    }
}

public class MockHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new HttpClient();
}
