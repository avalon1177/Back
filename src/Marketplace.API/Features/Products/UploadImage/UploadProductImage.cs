using Marketplace.API.Common;
using Marketplace.Application.DTO;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.API.Features.Products.UploadImage;

public static class UploadProductImage
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/products/{id:guid}/images", async (Guid id, IFormFile file, [FromQuery] int sortOrder, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, IStorageService storage, CancellationToken ct) =>
        {
            if (file is null || file.Length == 0) return Results.BadRequest(ApiErrors.Problem("File is required"));
            var allowed = new[] { "image/jpeg", "image/png", "image/webp" };
            if (!allowed.Contains(file.ContentType)) return Results.BadRequest(ApiErrors.Problem("Only jpeg/png/webp allowed"));

            var userId = user.RequireUserId();
            var store = await db.Stores.FirstOrDefaultAsync(s => s.OwnerUserId == userId, ct);
            if (store is null) return Results.NotFound(ApiErrors.Problem("Store not found", 404));
            var product = await db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id && p.StoreId == store.Id, ct);
            if (product is null) return Results.NotFound(ApiErrors.Problem("Product not found", 404));

            await using var stream = file.OpenReadStream();
            var (fileName, contentType) = await storage.SaveProductImageAsync(stream, file.ContentType, file.FileName, ct);
            db.ProductImages.Add(new ProductImage { ProductId = product.Id, FileName = fileName, ContentType = contentType, SortOrder = sortOrder });
            await db.SaveChangesAsync(ct);

            var images = await db.ProductImages.Where(i => i.ProductId == product.Id).OrderBy(i => i.SortOrder).Select(i => new ProductImageDto(i.Id, storage.GetPublicUrl(i.FileName), i.SortOrder, i.ContentType)).ToListAsync(ct);
            return Results.Ok(images);
        }).DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(10_000_000)).WithTags("Products").RequireAuthorization(new AuthorizeAttribute { Roles = UserRole.Seller });
    }
}
