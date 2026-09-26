using Marketplace.Core.Enums;

namespace Marketplace.Application.DTO;

public record CheckoutRequest(string BuyerName, string Phone, string City, string DeliveryAddress, string? Comment, string? CouponCode = null);
public record OrderItemResponse(Guid ProductId, string ProductTitle, Guid StoreId, string StoreName, decimal UnitPrice, int Quantity, decimal LineTotal);

public record OrderResponse(
    Guid Id,
    OrderStatus Status,
    decimal Total,
    DateTime CreatedAtUtc,
    string BuyerName,
    string Phone,
    string City,
    string DeliveryAddress,
    string? Comment,
    string? TrackingNumber,
    IReadOnlyList<OrderItemResponse> Items);

public record OrderListResponse(IReadOnlyList<OrderResponse> Items, int Page, int PageSize, int Total);
public record OrderShipRequest(string? TrackingNumber);
