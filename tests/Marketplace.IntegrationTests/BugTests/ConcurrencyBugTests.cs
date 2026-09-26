using System.Net;
using System.Net.Http.Json;
using Marketplace.Application.DTO;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Marketplace.API.Features.Extras.ExtrasEndpoints;

namespace Marketplace.IntegrationTests.BugTests;

public sealed class ConcurrencyBugTests : IntegrationTestBase
{
    public ConcurrencyBugTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task OrderCancel_RestoringStockTwice_IncreasesStockIncorrectly()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "cancel-twice-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "cancel-twice-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Cancel Twice Product", stock: 5, price: 25m);
        var order = await Scenario.SeedOrderAsync(buyer, product, quantity: 2, status: OrderStatus.Pending);

        var initialStock = await Scenario.ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == product.Id).Select(p => p.Stock).SingleAsync());

        using var buyerClient = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var cancel1 = await buyerClient.PostAsync($"/api/orders/{order.Id}/cancel", content: null);
        Assert.Equal(HttpStatusCode.OK, cancel1.StatusCode);

        var stockAfterFirstCancel = await Scenario.ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == product.Id).Select(p => p.Stock).SingleAsync());
        Assert.Equal(initialStock + 2, stockAfterFirstCancel);

        var cancel2 = await buyerClient.PostAsync($"/api/orders/{order.Id}/cancel", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, cancel2.StatusCode);

        var finalStock = await Scenario.ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == product.Id).Select(p => p.Stock).SingleAsync());

        Assert.Equal(stockAfterFirstCancel, finalStock);
    }

    [Fact]
    public async Task CartUpdate_StockCanExceedAvailableDueToRace()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "cart-race-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "cart-race-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Race Product", stock: 2);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        await client.PostAsJsonAsync("/api/cart", new CartAddRequest(product.Id, 1));
        
        var cartItem = await Scenario.ExecuteDbAsync(async db =>
            (await db.CartItems.FirstAsync(c => c.BuyerUserId == buyer.UserId)).Id);

        var update = await client.PutAsJsonAsync($"/api/cart/{cartItem}", new CartUpdateRequest(10));
        
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);

        var quantity = await Scenario.ExecuteDbAsync(async db =>
            (await db.CartItems.FirstAsync(c => c.Id == cartItem)).Quantity);
        
        Assert.Equal(1, quantity);
    }

    [Fact]
    public async Task WishlistAdd_SameProductSimultaneously_ReturnsConflict()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "wishlist-dup-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "wishlist-dup-seller");
        var wishlist = await Scenario.SeedWishlistAsync(buyer);
        var product = await Scenario.SeedProductAsync(seller, title: "Duplicate Wishlist Product");

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var first = await client.PostAsJsonAsync($"/api/wishlists/{wishlist.Id}/items", new ProductRefRequest(product.Id));
        var second = await client.PostAsJsonAsync($"/api/wishlists/{wishlist.Id}/items", new ProductRefRequest(product.Id));

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var count = await Scenario.ExecuteDbAsync(async db =>
            await db.WishlistItems.CountAsync(w => w.WishlistId == wishlist.Id && w.ProductId == product.Id));

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ProductDelete_CannotDeleteWhenHasChildren()
    {
        var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "delete-child-admin");
        var parentCategory = await Scenario.SeedCategoryAsync("Parent Category");
        var childCategory = await Scenario.SeedCategoryAsync("Child Category", parentCategory.Id);

        using var adminClient = await Scenario.CreateAuthenticatedClientAsync(admin);

        var deleteParent = await adminClient.DeleteAsync($"/api/categories/{parentCategory.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, deleteParent.StatusCode);

        var deleteChild = await adminClient.DeleteAsync($"/api/categories/{childCategory.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteChild.StatusCode);

        var deleteParentAfter = await adminClient.DeleteAsync($"/api/categories/{parentCategory.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteParentAfter.StatusCode);
    }
}
