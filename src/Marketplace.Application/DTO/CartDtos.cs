namespace Marketplace.Application.DTO;

public record CartAddRequest(Guid ProductId, int Quantity);
public record CartUpdateRequest(int Quantity);

public record CartItemResponse(
    Guid Id,
    Guid ProductId,
    string Title,
    decimal Price,
    int Quantity,
    int Stock,
    string? ImageUrl,
    decimal LineTotal);

public record CartResponse(IReadOnlyList<CartItemResponse> Items, decimal Total);
