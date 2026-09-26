using Marketplace.API.Common;
using Marketplace.Core.Enums;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.API.Features.Products.DeleteImage;

public static class DeleteProductImage
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/products/images/{imageId:guid}", async (Guid imageId, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, IStorageService storage, CancellationToken ct) =>
        {
            var userId = user.RequireUserId();
            var store = await db.Stores.FirstOrDefaultAsync(s => s.OwnerUserId == userId, ct);
            if (store is null) return Results.NotFound(ApiErrors.Problem("Store not found", 404));
            var img = await db.ProductImages.Include(i => i.Product).FirstOrDefaultAsync(i => i.Id == imageId, ct);
            if (img is null) return Results.NotFound(ApiErrors.Problem("Image not found", 404));
            if (img.Product.StoreId != store.Id) return Results.Forbid();
            db.ProductImages.Remove(img);
            await db.SaveChangesAsync(ct);
            await storage.DeleteAsync(img.FileName, ct);
            return Results.NoContent();
        }).WithTags("Products").RequireAuthorization(new AuthorizeAttribute { Roles = UserRole.Seller });
    }
}
