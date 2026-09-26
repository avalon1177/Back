using FluentValidation;
using Marketplace.API.Common;
using Marketplace.Application.DTO;
using Marketplace.Core.Enums;
using Marketplace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.API.Features.Products.Update;

public static class UpdateProduct
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/products/{id:guid}", async (Guid id, ProductUpdateRequest req, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, IValidator<ProductUpdateRequest> validator, CancellationToken ct) =>
        {
            var vr = await validator.ValidateAsync(req, ct);
            if (!vr.IsValid) return Results.BadRequest(vr.Errors.Select(e => e.ErrorMessage));
            var userId = user.RequireUserId();
            var store = await db.Stores.FirstOrDefaultAsync(s => s.OwnerUserId == userId, ct);
            if (store is null) return Results.NotFound(ApiErrors.Problem("Store not found", 404));
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id && p.StoreId == store.Id, ct);
            if (product is null) return Results.NotFound(ApiErrors.Problem("Product not found", 404));
            if (!await db.Categories.AnyAsync(c => c.Id == req.CategoryId, ct)) return Results.BadRequest(ApiErrors.Problem("Category not found"));

            product.CategoryId = req.CategoryId;
            product.Title = req.Title.Trim();
            product.Description = req.Description.Trim();
            product.Price = req.Price;
            product.Stock = req.Stock;
            product.IsPublished = product.Status == ProductStatus.Approved && req.IsPublished;
            product.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithTags("Products").RequireAuthorization(new AuthorizeAttribute { Roles = UserRole.Seller });
    }
}
