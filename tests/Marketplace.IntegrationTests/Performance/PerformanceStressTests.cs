using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Marketplace.Application.DTO;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Marketplace.IntegrationTests.Performance;

public sealed class PerformanceStressTests : IntegrationTestBase
{
    public PerformanceStressTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Performance_ProductSearch_CompletesInReasonableTime()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "perf-search-seller");
        await Scenario.SeedProductAsync(seller, title: "Performance Product 1", price: 10m);
        await Scenario.SeedProductAsync(seller, title: "Performance Product 2", price: 20m);
        await Scenario.SeedProductAsync(seller, title: "Performance Product 3", price: 30m);

        using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
        var sw = Stopwatch.StartNew();
        var response = await client.GetAsync("/api/products?q=Performance");
        sw.Stop();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(sw.ElapsedMilliseconds < 5000, $"Search should complete within 5 seconds, was {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task Performance_OrderList_RespondsInReasonableTime()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "perf-list-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Perf List Product", price: 10m);

        var orderCount = 10;
        for (int i = 0; i < orderCount; i++)
        {
            var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: $"perf-list-buyer-{i}");
            await Scenario.SeedOrderAsync(buyer, product, quantity: 1, status: OrderStatus.Pending);
        }

        using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
        var sw = Stopwatch.StartNew();
        var response = await client.GetAsync("/api/orders/seller?page=1&pageSize=10");
        sw.Stop();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(sw.ElapsedMilliseconds < 5000, $"Order list should respond within 5 seconds, was {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task Performance_CartOperations_CompletesSuccessfully()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "perf-cart-seller");
        var product1 = await Scenario.SeedProductAsync(seller, title: "Perf Cart 1", stock: 100, price: 10m);
        var product2 = await Scenario.SeedProductAsync(seller, title: "Perf Cart 2", stock: 100, price: 20m);

        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "perf-cart-buyer");

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var sw = Stopwatch.StartNew();
        await client.PostAsJsonAsync("/api/cart/items", new { productId = product1.Id, quantity = 2 });
        await client.PostAsJsonAsync("/api/cart/items", new { productId = product2.Id, quantity = 1 });
        var response = await client.GetAsync("/api/cart");
        sw.Stop();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(sw.ElapsedMilliseconds < 5000, $"Cart operations should complete within 5 seconds, was {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task Performance_CartCheckout_Atomicity()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "perf-atomic-seller");
        var product1 = await Scenario.SeedProductAsync(seller, title: "Perf Atomic 1", stock: 10, price: 30m);
        var product2 = await Scenario.SeedProductAsync(seller, title: "Perf Atomic 2", stock: 10, price: 70m);

        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "perf-atomic-buyer");
        await Scenario.SeedCartItemAsync(buyer, product1, quantity: 2);
        await Scenario.SeedCartItemAsync(buyer, product2, quantity: 1);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var sw = Stopwatch.StartNew();
        var response = await client.PostAsJsonAsync("/api/orders/checkout",
            new CheckoutRequest(buyer.DisplayName, "+10000000000", "City", "Address", null));
        sw.Stop();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(sw.ElapsedMilliseconds < 5000, $"Checkout should complete within 5 seconds, was {sw.ElapsedMilliseconds}ms");

        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);
        Assert.Equal(130m, order.Total);
        Assert.Equal(2, order.Items.Count);

        var stock1 = await Scenario.ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == product1.Id).Select(p => p.Stock).SingleAsync());
        var stock2 = await Scenario.ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == product2.Id).Select(p => p.Stock).SingleAsync());

        Assert.Equal(8, stock1);
        Assert.Equal(9, stock2);
    }

    [Fact]
    public async Task Performance_SearchWithFilters_CompletesQuickly()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "perf-filter-seller");
        
        for (int i = 0; i < 5; i++)
        {
            await Scenario.SeedProductAsync(seller, title: $"Perf Filter {i}", price: 10m * (i + 1));
        }

        using var client = await Scenario.CreateAuthenticatedClientAsync(seller);

        var sw = Stopwatch.StartNew();
        var response = await client.GetAsync("/api/products?page=1&pageSize=20&sortBy=price&sortOrder=asc");
        sw.Stop();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(sw.ElapsedMilliseconds < 3000, $"Search should complete within 3 seconds, was {sw.ElapsedMilliseconds}ms");
    }
}
