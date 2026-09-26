using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Marketplace.Application.DTO;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Marketplace.API.Features.Extras.ExtrasEndpoints;

namespace Marketplace.IntegrationTests.BugTests;

public sealed class CartAndWishlistBugTests : IntegrationTestBase
{
    public CartAndWishlistBugTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CartClear_RemovesAllItems()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "cart-clear-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "cart-clear-seller");

        var product1 = await Scenario.SeedProductAsync(seller, title: "Cart Clear Product 1", price: 25m);
        var product2 = await Scenario.SeedProductAsync(seller, title: "Cart Clear Product 2", price: 50m);

        await Scenario.SeedCartItemAsync(buyer, product1, quantity: 2);
        await Scenario.SeedCartItemAsync(buyer, product2, quantity: 1);

        var cartCountBefore = await Scenario.ExecuteDbAsync(async db =>
            await db.CartItems.CountAsync(c => c.BuyerUserId == buyer.UserId));
        Assert.Equal(2, cartCountBefore);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var clearResponse = await client.DeleteAsync("/api/cart");

        Assert.Equal(HttpStatusCode.NoContent, clearResponse.StatusCode);

        var cartCountAfter = await Scenario.ExecuteDbAsync(async db =>
            await db.CartItems.CountAsync(c => c.BuyerUserId == buyer.UserId));
        Assert.Equal(0, cartCountAfter);
    }

    [Fact]
    public async Task CartUpdate_ZeroQuantity_ShouldRemoveItem()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "cart-zero-qty-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "cart-zero-qty-seller");
        var category = await Scenario.SeedCategoryAsync("Zero Qty Category");
        var product = await Scenario.SeedProductAsync(seller, category: category, title: "Zero Qty Product", stock: 10);
        await Scenario.SeedCartItemAsync(buyer, product, quantity: 5);

        var cartItemId = await Scenario.ExecuteDbAsync(async db =>
            (await db.CartItems.FirstAsync(c => c.BuyerUserId == buyer.UserId)).Id);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var updateResponse = await client.PutAsJsonAsync($"/api/cart/{cartItemId}",
            new CartUpdateRequest(0));

        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
    }

    [Fact]
    public async Task CartUpdate_NegativeQuantity_ShouldReturnBadRequest()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "cart-neg-qty-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "cart-neg-qty-seller");
        var category = await Scenario.SeedCategoryAsync("Neg Qty Category");
        var product = await Scenario.SeedProductAsync(seller, category: category, title: "Neg Qty Product", stock: 10);
        await Scenario.SeedCartItemAsync(buyer, product, quantity: 5);

        var cartItemId = await Scenario.ExecuteDbAsync(async db =>
            (await db.CartItems.FirstAsync(c => c.BuyerUserId == buyer.UserId)).Id);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var updateResponse = await client.PutAsJsonAsync($"/api/cart/{cartItemId}",
            new CartUpdateRequest(-1));

        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
    }

    [Fact]
    public async Task CartAdd_ExceedsStock_ShouldReturnBadRequest()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "cart-exceed-stock-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "cart-exceed-stock-seller");
        var category = await Scenario.SeedCategoryAsync("Exceed Stock Category");
        var product = await Scenario.SeedProductAsync(seller, category: category, title: "Exceed Stock Product", stock: 5);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var addResponse = await client.PostAsJsonAsync("/api/cart",
            new CartAddRequest(product.Id, 10));

        Assert.Equal(HttpStatusCode.BadRequest, addResponse.StatusCode);
    }

    [Fact]
    public async Task WishlistDelete_ByOwner_ShouldSucceed()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "wishlist-del-buyer");
        var wishlist = await Scenario.SeedWishlistAsync(buyer);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var deleteResponse = await client.DeleteAsync($"/api/wishlists/{wishlist.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var exists = await Scenario.ExecuteDbAsync(async db =>
            await db.Wishlists.AnyAsync(w => w.Id == wishlist.Id));
        Assert.False(exists);
    }

    [Fact]
    public async Task WishlistDelete_ByNonOwner_ShouldReturnForbidden()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "wishlist-del-owner-buyer");
        var otherBuyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "wishlist-del-other-buyer");
        var wishlist = await Scenario.SeedWishlistAsync(buyer);

        using var client = await Scenario.CreateAuthenticatedClientAsync(otherBuyer);

        var deleteResponse = await client.DeleteAsync($"/api/wishlists/{wishlist.Id}");

        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task WishlistAddItem_ProductAlreadyInWishlist_ShouldReturnConflict()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "wishlist-add-dup-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "wishlist-add-dup-seller");
        var wishlist = await Scenario.SeedWishlistAsync(buyer);
        var product = await Scenario.SeedProductAsync(seller, title: "Wishlist Add Dup Product");
        await Scenario.SeedWishlistItemAsync(wishlist, product);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var addResponse = await client.PostAsJsonAsync($"/api/wishlists/{wishlist.Id}/items",
            new ProductRefRequest(product.Id));

        Assert.Equal(HttpStatusCode.Conflict, addResponse.StatusCode);
    }

    [Fact]
    public async Task WishlistRemoveItem_ShouldRemoveFromWishlist()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "wishlist-rem-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "wishlist-rem-seller");
        var wishlist = await Scenario.SeedWishlistAsync(buyer);
        var product = await Scenario.SeedProductAsync(seller, title: "Wishlist Remove Product");
        await Scenario.SeedWishlistItemAsync(wishlist, product);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var removeResponse = await client.DeleteAsync($"/api/wishlists/{wishlist.Id}/items/{product.Id}");

        Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);

        var itemExists = await Scenario.ExecuteDbAsync(async db =>
            await db.WishlistItems.AnyAsync(wi => wi.WishlistId == wishlist.Id && wi.ProductId == product.Id));
        Assert.False(itemExists);
    }
}
