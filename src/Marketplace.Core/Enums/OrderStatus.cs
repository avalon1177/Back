namespace Marketplace.Core.Enums;

public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    Shipped = 2,
    Completed = 3,
    Cancelled = 4,
    ReturnRequested = 5,
    ReturnApproved = 6,
    ReturnRejected = 7,
    Refunded = 8
}
