using Marketplace.Core.Common;
using Marketplace.Core.Enums;

namespace Marketplace.Core.Entities;

public class EmailLog : Entity<Guid>
{
    public Guid? UserId { get; set; }
    public string To { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public EmailType Type { get; set; }
    public EmailStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
}

public enum EmailType
{
    Confirmation,
    PasswordReset,
    TwoFactorCode,
    OrderCreated,
    OrderStatusChanged,
    ChatMessage,
    ReviewReply,
    ProductApproved,
    ProductRejected
}

public enum EmailStatus
{
    Pending,
    Sent,
    Failed
}
