using Marketplace.API.Common;
using Marketplace.Application.DTO;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.API.Features.Products.GetBySlug;

public static class GetProductBySlug
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/products/{slug}", async (string slug, AppDbContext db, IStorageService storage, CancellationToken ct) =>
        {
            var p = await db.Products
                .AsNoTracking()
                .Include(x => x.Store)
                .Include(x => x.Category)
                .Include(x => x.Images)
                .Include(x => x.Reviews)
                .FirstOrDefaultAsync(x => x.Slug == slug && x.IsPublished, ct);

            if (p is null) return Results.NotFound(ApiErrors.Problem("Product not found", 404));

            var ratingAvg = p.Reviews.Any() ? p.Reviews.Average(r => r.Rating) : 0;
            var images = p.Images.OrderBy(i => i.SortOrder).Select(i => new ProductImageDto(i.Id, storage.GetPublicUrl(i.FileName), i.SortOrder, i.ContentType)).ToList();

            return Results.Ok(new ProductDetails(p.Id, p.StoreId, p.Store.Name, p.CategoryId, p.Category.Name, p.Title, p.Slug, p.Description, p.Price, p.Stock, p.IsPublished, ratingAvg, p.Reviews.Count, images));
        }).WithTags("Products").AllowAnonymous();
    }
}
