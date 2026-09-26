using Marketplace.Core.Entities;
using Marketplace.Infrastructure.Persistence;

namespace Marketplace.Infrastructure.Services;

public interface INotificationService
{
    Task CreateNotificationAsync(Guid userId, string title, string text);
    Task NotifyProductApprovedAsync(Guid userId, string productTitle);
    Task NotifyProductRejectedAsync(Guid userId, string productTitle, string reason);
}

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;

    public NotificationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task CreateNotificationAsync(Guid userId, string title, string text)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Text = text,
            IsRead = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();
    }

    public async Task NotifyProductApprovedAsync(Guid userId, string productTitle)
    {
        await CreateNotificationAsync(
            userId,
            "Товар схвалено",
            $"Ваш товар \"{productTitle}\" успішно пройшов модерацію та опублікований на маркетплейсі."
        );
    }

    public async Task NotifyProductRejectedAsync(Guid userId, string productTitle, string reason)
    {
        await CreateNotificationAsync(
            userId,
            "Товар відхилено",
            $"Ваш товар \"{productTitle}\" не пройшов модерацію. Причина: {reason}"
        );
    }
}
