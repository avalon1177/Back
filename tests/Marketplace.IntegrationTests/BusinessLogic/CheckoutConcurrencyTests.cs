using System.Net;
using System.Net.Http.Json;
using Marketplace.Application.DTO;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Marketplace.IntegrationTests.BusinessLogic;

public sealed class CheckoutConcurrencyTests : IntegrationTestBase
{
    public CheckoutConcurrencyTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Checkout_SequentialCheckouts_MaintainConsistency()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "seq-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Sequential Product", stock: 3, price: 50m);

        for (int i = 0; i < 3; i++)
        {
            var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: $"seq-buyer-{i}");
            await Scenario.SeedCartItemAsync(buyer, product, quantity: 1);

            using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
            var response = await client.PostAsJsonAsync("/api/orders/checkout",
                new CheckoutRequest(buyer.DisplayName, "+10000000000", "City", "Address", null));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var finalStock = await Scenario.ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == product.Id).Select(p => p.Stock).SingleAsync());
        Assert.Equal(0, finalStock);

        var orderCount = await Scenario.ExecuteDbAsync(db =>
            db.Orders.CountAsync(o => o.Status == OrderStatus.Pending));
        Assert.Equal(3, orderCount);
    }

    [Fact]
    public async Task Checkout_MultiItemCart_AtomicOperation()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "multi-seller");
        var product1 = await Scenario.SeedProductAsync(seller, title: "Multi 1", stock: 5, price: 30m);
        var product2 = await Scenario.SeedProductAsync(seller, title: "Multi 2", stock: 5, price: 70m);
        
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "multi-buyer");
        await Scenario.SeedCartItemAsync(buyer, product1, quantity: 2);
        await Scenario.SeedCartItemAsync(buyer, product2, quantity: 1);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var response = await client.PostAsJsonAsync("/api/orders/checkout",
            new CheckoutRequest(buyer.DisplayName, "+10000000000", "City", "Address", null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var orderResponse = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(orderResponse);
        Assert.Equal(130m, orderResponse.Total);

        var stock1 = await Scenario.ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == product1.Id).Select(p => p.Stock).SingleAsync());
        var stock2 = await Scenario.ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == product2.Id).Select(p => p.Stock).SingleAsync());

        Assert.Equal(3, stock1);
        Assert.Equal(4, stock2);
    }

    [Fact]
    public async Task Checkout_StockDecrementedOnCheckout()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "stock-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Stock Product", stock: 5, price: 100m);
        
        var initialStock = await Scenario.ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == product.Id).Select(p => p.Stock).SingleAsync());
        Assert.Equal(5, initialStock);

        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "stock-buyer");
        await Scenario.SeedCartItemAsync(buyer, product, quantity: 2);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var response = await client.PostAsJsonAsync("/api/orders/checkout",
            new CheckoutRequest(buyer.DisplayName, "+10000000000", "City", "Address", null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var finalStock = await Scenario.ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == product.Id).Select(p => p.Stock).SingleAsync());
        Assert.Equal(3, finalStock);
    }

    [Fact]
    public async Task Checkout_EmptyCart_ShouldFail()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "empty-buyer");

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var response = await client.PostAsJsonAsync("/api/orders/checkout",
            new CheckoutRequest(buyer.DisplayName, "+10000000000", "City", "Address", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
