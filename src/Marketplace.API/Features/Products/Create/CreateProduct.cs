using FluentValidation;
using Marketplace.API.Common;
using Marketplace.Application.DTO;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.Core.Utils;
using Marketplace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.API.Features.Products.Create;

public static class CreateProduct
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/products", async (ProductCreateRequest req, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, IValidator<ProductCreateRequest> validator, CancellationToken ct) =>
        {
            var vr = await validator.ValidateAsync(req, ct);
            if (!vr.IsValid) return Results.BadRequest(vr.Errors.Select(e => e.ErrorMessage));
            var userId = user.RequireUserId();
            var store = await db.Stores.FirstOrDefaultAsync(s => s.OwnerUserId == userId, ct);
            if (store is null) return Results.NotFound(ApiErrors.Problem("Store not found", 404));
            if (!await db.Categories.AnyAsync(c => c.Id == req.CategoryId, ct)) return Results.BadRequest(ApiErrors.Problem("Category not found"));

            var slugBase = Slug.From(req.Title);
            var slug = slugBase;
            var exists = await db.Products.AnyAsync(p => p.Slug == slug, ct);
            if (exists) slug = (slugBase + "-" + Guid.NewGuid().ToString("N")).Substring(0, Math.Min(160, slugBase.Length + 33));

            var product = new Product
            {
                StoreId = store.Id,
                CategoryId = req.CategoryId,
                Title = req.Title.Trim(),
                Slug = slug,
                Description = req.Description.Trim(),
                Price = req.Price,
                Stock = req.Stock,
                IsPublished = false,
                Status = ProductStatus.Pending
            };

            db.Products.Add(product);
            await db.SaveChangesAsync(ct);
            var category = await db.Categories.AsNoTracking().FirstAsync(c => c.Id == product.CategoryId, ct);
            return Results.Ok(new ProductDetails(product.Id, store.Id, store.Name, category.Id, category.Name, product.Title, product.Slug, product.Description, product.Price, product.Stock, product.IsPublished, 0, 0, new List<ProductImageDto>()));
        }).WithTags("Products").RequireAuthorization(new AuthorizeAttribute { Roles = UserRole.Seller });
    }
}
