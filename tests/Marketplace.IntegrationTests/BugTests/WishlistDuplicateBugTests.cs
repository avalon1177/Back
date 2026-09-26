using System.Net;
using System.Net.Http.Json;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Marketplace.IntegrationTests.BugTests;

public sealed class WishlistDuplicateBugTests : IntegrationTestBase
{
    public WishlistDuplicateBugTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Wishlist_AddSameProductTwice_ReturnsConflict()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "wishlist-dup-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "wishlist-dup-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Wishlist Dup Product");

        await Scenario.ExecuteDbAsync(async db =>
        {
            var wishlist = new Wishlist { UserId = buyer.UserId, Name = "My Wishlist" };
            db.Wishlists.Add(wishlist);
            await db.SaveChangesAsync();
            return 0;
        });

        var wishlistId = await Scenario.ExecuteDbAsync(db =>
            db.Wishlists.Where(w => w.UserId == buyer.UserId).Select(w => w.Id).SingleAsync());

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var req = new { productId = product.Id };

        var add1 = await client.PostAsJsonAsync($"/api/wishlists/{wishlistId}/items", req);
        Assert.Equal(HttpStatusCode.NoContent, add1.StatusCode);

        var add2 = await client.PostAsJsonAsync($"/api/wishlists/{wishlistId}/items", req);
        Assert.Equal(HttpStatusCode.Conflict, add2.StatusCode);

        var count = await Scenario.ExecuteDbAsync(db =>
            db.WishlistItems.Where(w => w.WishlistId == wishlistId && w.ProductId == product.Id).CountAsync());

        Assert.Equal(1, count);
    }
}
