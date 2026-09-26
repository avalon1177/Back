namespace Marketplace.Application.DTO;

public record ReviewCreateRequest(Guid ProductId, int Rating, string Text);
public record ReviewResponse(Guid Id, Guid BuyerUserId, string BuyerName, int Rating, string Text, DateTime CreatedAtUtc);
