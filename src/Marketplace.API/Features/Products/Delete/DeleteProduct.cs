using Marketplace.API.Common;
using Marketplace.Core.Enums;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.API.Features.Products.Delete;

public static class DeleteProduct
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/products/{id:guid}", async (Guid id, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, IStorageService storage, CancellationToken ct) =>
        {
            var userId = user.RequireUserId();
            var store = await db.Stores.FirstOrDefaultAsync(s => s.OwnerUserId == userId, ct);
            if (store is null) return Results.NotFound(ApiErrors.Problem("Store not found", 404));
            var product = await db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id && p.StoreId == store.Id, ct);
            if (product is null) return Results.NotFound(ApiErrors.Problem("Product not found", 404));

            var fileNames = product.Images.Select(x => x.FileName).ToList();
            db.Products.Remove(product);
            await db.SaveChangesAsync(ct);

            foreach (var fileName in fileNames)
            {
                await storage.DeleteAsync(fileName, ct);
            }

            return Results.NoContent();
        }).WithTags("Products").RequireAuthorization(new AuthorizeAttribute { Roles = UserRole.Seller });
    }
}
