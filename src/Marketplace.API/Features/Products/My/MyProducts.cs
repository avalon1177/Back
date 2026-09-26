using Marketplace.API.Common;
using Marketplace.Application.DTO;
using Marketplace.Core.Enums;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.API.Features.Products.My;

public static class MyProducts
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/products/my", async (System.Security.Claims.ClaimsPrincipal user, AppDbContext db, IStorageService storage, CancellationToken ct) =>
        {
            var userId = user.RequireUserId();
            var store = await db.Stores.FirstOrDefaultAsync(s => s.OwnerUserId == userId, ct);
            if (store is null) return Results.NotFound(ApiErrors.Problem("Store not found", 404));

            var items = await db.Products
                .AsNoTracking()
                .Where(p => p.StoreId == store.Id)
                .Include(p => p.Category)
                .Include(p => p.Images)
                .Include(p => p.Reviews)
                .OrderByDescending(p => p.CreatedAtUtc)
                .Select(p => new { p, RatingAvg = p.Reviews.Any() ? p.Reviews.Average(r => r.Rating) : 0, ReviewsCount = p.Reviews.Count })
                .Select(x => new ProductListItem(x.p.Id, x.p.StoreId, store.Name, x.p.CategoryId, x.p.Category.Name, x.p.Title, x.p.Slug, x.p.Price, x.p.Stock, x.p.IsPublished, x.RatingAvg, x.ReviewsCount, x.p.Images.OrderBy(i => i.SortOrder).Select(i => storage.GetPublicUrl(i.FileName)).FirstOrDefault()))
                .ToListAsync(ct);

            return Results.Ok(items);
        }).WithTags("Products").RequireAuthorization(new AuthorizeAttribute { Roles = UserRole.Seller });
    }
}
