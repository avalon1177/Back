using System.Net;
using System.Net.Http.Json;
using Marketplace.Application.DTO;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Marketplace.API.Features.Legacy.LegacyEndpoints;

namespace Marketplace.IntegrationTests.BugTests;

public sealed class OrderStateMachineBugTests : IntegrationTestBase
{
    public OrderStateMachineBugTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task OrderStatus_PendingToCompleted_ShouldReject()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "pending-complete-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "pending-complete-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Pending Complete Product", price: 100m);
        var order = await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.Pending);

        using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
        var response = await client.PutAsync($"/api/orders/{order.Id}/status?status=Completed", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OrderStatus_PendingToShipped_ShouldReject()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "pending-ship-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "pending-ship-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Pending Ship Product", price: 100m);
        var order = await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.Pending);

        using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
        var response = await client.PutAsync($"/api/orders/{order.Id}/status?status=Shipped", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OrderStatus_ValidTransitions_ShouldAccept()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "valid-transition-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "valid-transition-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Valid Transition Product", price: 100m);
        var order = await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.Pending);

        using var client = await Scenario.CreateAuthenticatedClientAsync(seller);

        var confirm = await client.PutAsync($"/api/orders/{order.Id}/status?status=Confirmed", content: null);
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);

        var ship = await client.PutAsync($"/api/orders/{order.Id}/status?status=Shipped", content: null);
        Assert.Equal(HttpStatusCode.NoContent, ship.StatusCode);

        var complete = await client.PutAsync($"/api/orders/{order.Id}/status?status=Completed", content: null);
        Assert.Equal(HttpStatusCode.NoContent, complete.StatusCode);
    }
}

public sealed class SellerOrderTotalBugTests : IntegrationTestBase
{
    public SellerOrderTotalBugTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task MultiSellerOrder_SellerSeesOnlyTheirTotal()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "multi-seller-buyer");
        var sellerA = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "multi-seller-a");
        var sellerB = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "multi-seller-b");

        var productA = await Scenario.SeedProductAsync(sellerA, title: "Seller A Product", price: 100m);
        var productB = await Scenario.SeedProductAsync(sellerB, title: "Seller B Product", price: 200m);

        var order = await Scenario.SeedOrderAsync(buyer, productA, quantity: 1, status: OrderStatus.Pending);
        await Scenario.ExecuteDbAsync(async db =>
        {
            var itemB = new OrderItem
            {
                OrderId = order.Id,
                ProductId = productB.Id,
                StoreId = productB.StoreId,
                ProductTitleSnapshot = productB.Title,
                UnitPrice = productB.Price,
                Quantity = 1,
                LineTotal = productB.Price
            };
            db.OrderItems.Add(itemB);
            await db.SaveChangesAsync();
            return 0;
        });

        using var clientA = await Scenario.CreateAuthenticatedClientAsync(sellerA);
        var responseA = await clientA.GetAsync($"/api/orders/{order.Id}");

        Assert.Equal(HttpStatusCode.OK, responseA.StatusCode);
        var orderResponse = await responseA.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(orderResponse);
        Assert.Equal(100m, orderResponse.Total);
    }

    [Fact]
    public async Task MultiSellerOrder_OtherSellerSeesTheirTotal()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "multi-seller-buyer2");
        var sellerA = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "multi-seller-a2");
        var sellerB = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "multi-seller-b2");

        var productA = await Scenario.SeedProductAsync(sellerA, title: "Seller A Product 2", price: 100m);
        var productB = await Scenario.SeedProductAsync(sellerB, title: "Seller B Product 2", price: 200m);

        var order = await Scenario.SeedOrderAsync(buyer, productA, quantity: 1, status: OrderStatus.Pending);
        await Scenario.ExecuteDbAsync(async db =>
        {
            var itemB = new OrderItem
            {
                OrderId = order.Id,
                ProductId = productB.Id,
                StoreId = productB.StoreId,
                ProductTitleSnapshot = productB.Title,
                UnitPrice = productB.Price,
                Quantity = 1,
                LineTotal = productB.Price
            };
            db.OrderItems.Add(itemB);
            await db.SaveChangesAsync();
            return 0;
        });

        using var clientB = await Scenario.CreateAuthenticatedClientAsync(sellerB);
        var responseB = await clientB.GetAsync($"/api/orders/{order.Id}");

        Assert.Equal(HttpStatusCode.OK, responseB.StatusCode);
        var orderResponse = await responseB.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(orderResponse);
        Assert.Equal(200m, orderResponse.Total);
    }
}

public sealed class OrderCancellationPaymentBugTests : IntegrationTestBase
{
    public OrderCancellationPaymentBugTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task OrderCancel_AfterPaymentCompleted_ShouldRejectOrHandleRefund()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "paid-cancel-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "paid-cancel-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Paid Cancel Product", price: 100m);
        var order = await Scenario.SeedOrderAsync(buyer, product, quantity: 1, status: OrderStatus.Pending);

        await Scenario.ExecuteDbAsync(async db =>
        {
            var payment = new Payment
            {
                OrderId = order.Id,
                PaymentMethod = PaymentMethod.Card,
                Amount = 100m,
                Status = PaymentStatus.Completed
            };
            db.Payments.Add(payment);
            await db.SaveChangesAsync();
            return 0;
        });

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var cancel = await client.PostAsync($"/api/orders/{order.Id}/cancel", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, cancel.StatusCode);
    }
}
