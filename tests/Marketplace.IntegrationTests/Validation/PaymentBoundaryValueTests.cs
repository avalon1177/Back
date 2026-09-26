using System.Net;
using System.Net.Http.Json;
using Marketplace.Application.DTO;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.IntegrationTests.Infrastructure;
using Xunit;
using static Marketplace.API.Features.Extras.ExtrasEndpoints;

namespace Marketplace.IntegrationTests.Validation;

public sealed class PaymentBoundaryValueTests : IntegrationTestBase
{
    public PaymentBoundaryValueTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(0.99)]
    [InlineData(100.00)]
    [InlineData(999999.99)]
    public async Task Payment_AmountAtLowerBoundary_ShouldAccept(decimal amount)
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: $"boundary-lower-{uniqueId}");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: $"boundary-lower-{uniqueId}-seller");
        var product = await Scenario.SeedProductAsync(seller, title: $"Boundary Lower {uniqueId}", price: amount);
        var order = await Scenario.SeedOrderAsync(buyer, product, quantity: 1, status: OrderStatus.Pending);

        await Scenario.ExecuteDbAsync(async db =>
        {
            db.Orders.First(o => o.Id == order.Id).Total = amount;
            await db.SaveChangesAsync();
            return 0;
        });

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var payment = new CreatePaymentRequest(PaymentMethod.Card, amount, PaymentStatus.Pending);
        var response = await client.PostAsJsonAsync($"/api/orders/{order.Id}/payments", payment);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Payment_AmountZero_ShouldReject()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "boundary-zero-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "boundary-zero-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Boundary Zero", price: 100m);
        var order = await Scenario.SeedOrderAsync(buyer, product, quantity: 1, status: OrderStatus.Pending);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var payment = new CreatePaymentRequest(PaymentMethod.Card, 0m, PaymentStatus.Pending);
        var response = await client.PostAsJsonAsync($"/api/orders/{order.Id}/payments", payment);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Payment_AmountNegative_ShouldReject()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "boundary-negative-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "boundary-negative-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Boundary Negative", price: 100m);
        var order = await Scenario.SeedOrderAsync(buyer, product, quantity: 1, status: OrderStatus.Pending);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var payment = new CreatePaymentRequest(PaymentMethod.Card, -0.01m, PaymentStatus.Pending);
        var response = await client.PostAsJsonAsync($"/api/orders/{order.Id}/payments", payment);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Payment_AmountOneCentOver_ShouldReject()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "boundary-over-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "boundary-over-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Boundary Over", price: 100m);
        var order = await Scenario.SeedOrderAsync(buyer, product, quantity: 1, status: OrderStatus.Pending);

        await Scenario.ExecuteDbAsync(async db =>
        {
            db.Orders.First(o => o.Id == order.Id).Total = 100m;
            await db.SaveChangesAsync();
            return 0;
        });

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var payment = new CreatePaymentRequest(PaymentMethod.Card, 100.01m, PaymentStatus.Pending);
        var response = await client.PostAsJsonAsync($"/api/orders/{order.Id}/payments", payment);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Payment_AmountOneCentUnder_ShouldReject()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "boundary-under-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "boundary-under-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Boundary Under", price: 100m);
        var order = await Scenario.SeedOrderAsync(buyer, product, quantity: 1, status: OrderStatus.Pending);

        await Scenario.ExecuteDbAsync(async db =>
        {
            db.Orders.First(o => o.Id == order.Id).Total = 100m;
            await db.SaveChangesAsync();
            return 0;
        });

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var payment = new CreatePaymentRequest(PaymentMethod.Card, 99.99m, PaymentStatus.Pending);
        var response = await client.PostAsJsonAsync($"/api/orders/{order.Id}/payments", payment);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Payment_OrderCancelled_ShouldReject()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "cancelled-pay-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "cancelled-pay-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Cancelled Pay", price: 100m);
        var order = await Scenario.SeedOrderAsync(buyer, product, quantity: 1, status: OrderStatus.Cancelled);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var payment = new CreatePaymentRequest(PaymentMethod.Card, 100m, PaymentStatus.Pending);
        var response = await client.PostAsJsonAsync($"/api/orders/{order.Id}/payments", payment);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Payment_OrderCompleted_ShouldReject()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "completed-pay-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "completed-pay-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Completed Pay", price: 100m);
        var order = await Scenario.SeedOrderAsync(buyer, product, quantity: 1, status: OrderStatus.Completed);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var payment = new CreatePaymentRequest(PaymentMethod.Card, 100m, PaymentStatus.Pending);
        var response = await client.PostAsJsonAsync($"/api/orders/{order.Id}/payments", payment);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Payment_OrderRefunded_ShouldReject()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "refunded-pay-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "refunded-pay-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Refunded Pay", price: 100m);
        var order = await Scenario.SeedOrderAsync(buyer, product, quantity: 1, status: OrderStatus.Refunded);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var payment = new CreatePaymentRequest(PaymentMethod.Card, 100m, PaymentStatus.Pending);
        var response = await client.PostAsJsonAsync($"/api/orders/{order.Id}/payments", payment);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
