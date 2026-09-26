using Marketplace.Application.DTO;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.API.Features.Products.Search;

public static class SearchProducts
{
    public record Query(
        [FromQuery(Name = "q")] string? Q,
        [FromQuery] Guid? CategoryId,
        [FromQuery] Guid? StoreId,
        [FromQuery] decimal? MinPrice,
        [FromQuery] decimal? MaxPrice,
        [FromQuery] string? Sort,
        [FromQuery] int Page = 1,
        [FromQuery] int PageSize = 20);

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/products", async ([AsParameters] Query query, AppDbContext db, IStorageService storage, CancellationToken ct) =>
        {
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);

            var dbQuery = db.Products
                .AsNoTracking()
                .Include(p => p.Store)
                .Include(p => p.Category)
                .Include(p => p.Images)
                .Include(p => p.Reviews)
                .Where(p => p.IsPublished);

            if (!string.IsNullOrWhiteSpace(query.Q))
            {
                var term = query.Q.Trim().ToLowerInvariant();
                dbQuery = dbQuery.Where(p => p.Title.ToLower().Contains(term) || p.Description.ToLower().Contains(term));
            }

            if (query.CategoryId is not null) dbQuery = dbQuery.Where(p => p.CategoryId == query.CategoryId);
            if (query.StoreId is not null) dbQuery = dbQuery.Where(p => p.StoreId == query.StoreId);
            if (query.MinPrice is not null) dbQuery = dbQuery.Where(p => p.Price >= query.MinPrice);
            if (query.MaxPrice is not null) dbQuery = dbQuery.Where(p => p.Price <= query.MaxPrice);

            dbQuery = query.Sort switch
            {
                "price_asc" => dbQuery.OrderBy(p => p.Price),
                "price_desc" => dbQuery.OrderByDescending(p => p.Price),
                "rating" => dbQuery.OrderByDescending(p => p.Reviews.Any() ? p.Reviews.Average(r => r.Rating) : 0),
                _ => dbQuery.OrderByDescending(p => p.CreatedAtUtc)
            };

            var total = await dbQuery.CountAsync(ct);
            var items = await dbQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    p,
                    RatingAvg = p.Reviews.Any() ? p.Reviews.Average(r => r.Rating) : 0,
                    ReviewsCount = p.Reviews.Count
                })
                .Select(x => new ProductListItem(
                    x.p.Id,
                    x.p.StoreId,
                    x.p.Store.Name,
                    x.p.CategoryId,
                    x.p.Category.Name,
                    x.p.Title,
                    x.p.Slug,
                    x.p.Price,
                    x.p.Stock,
                    x.p.IsPublished,
                    x.RatingAvg,
                    x.ReviewsCount,
                    x.p.Images.OrderBy(i => i.SortOrder).Select(i => storage.GetPublicUrl(i.FileName)).FirstOrDefault()))
                .ToListAsync(ct);

            return Results.Ok(new ProductSearchResponse(items, page, pageSize, total));
        }).WithTags("Products").AllowAnonymous();
    }
}
