namespace Marketplace.Application.DTO;

public record ProductCreateRequest(Guid CategoryId, string Title, string Description, decimal Price, int Stock, bool IsPublished);
public record ProductUpdateRequest(Guid CategoryId, string Title, string Description, decimal Price, int Stock, bool IsPublished);

public record ProductListItem(
    Guid Id,
    Guid StoreId,
    string StoreName,
    Guid CategoryId,
    string CategoryName,
    string Title,
    string Slug,
    decimal Price,
    int Stock,
    bool IsPublished,
    double RatingAvg,
    int ReviewsCount,
    string? MainImageUrl);

public record ProductDetails(
    Guid Id,
    Guid StoreId,
    string StoreName,
    Guid CategoryId,
    string CategoryName,
    string Title,
    string Slug,
    string Description,
    decimal Price,
    int Stock,
    bool IsPublished,
    double RatingAvg,
    int ReviewsCount,
    IReadOnlyList<ProductImageDto> Images);

public record ProductImageDto(Guid Id, string Url, int SortOrder, string ContentType);

public record ProductSearchResponse(
    IReadOnlyList<ProductListItem> Items,
    int Page,
    int PageSize,
    int Total);

public record ProductRejectRequest(string? Reason);
