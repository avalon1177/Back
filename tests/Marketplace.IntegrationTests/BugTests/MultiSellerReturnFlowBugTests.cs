using System.Net;
using System.Net.Http.Json;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Marketplace.IntegrationTests.BugTests;

public sealed class MultiSellerReturnFlowBugTests : IntegrationTestBase
{
    public MultiSellerReturnFlowBugTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task MultiSellerOrder_ReturnApprovalShouldRequireAllSellersToApprove()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "multi-seller-buyer");
        var sellerA = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "multi-seller-a");
        var sellerB = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "multi-seller-b");

        var productA = await Scenario.SeedProductAsync(sellerA, title: "Seller A Product", price: 50m);
        var productB = await Scenario.SeedProductAsync(sellerB, title: "Seller B Product", price: 100m);

        await Scenario.ExecuteDbAsync(async db =>
        {
            var order = new Order
            {
                BuyerUserId = buyer.UserId,
                BuyerName = buyer.DisplayName,
                Phone = "+10000000000",
                City = "Test City",
                DeliveryAddress = "Test Address",
                Status = OrderStatus.Shipped,
                Total = 150m
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            db.OrderItems.AddRange(
                new OrderItem
                {
                    OrderId = order.Id,
                    ProductId = productA.Id,
                    StoreId = productA.StoreId,
                    ProductTitleSnapshot = productA.Title,
                    UnitPrice = productA.Price,
                    Quantity = 1,
                    LineTotal = 50m
                },
                new OrderItem
                {
                    OrderId = order.Id,
                    ProductId = productB.Id,
                    StoreId = productB.StoreId,
                    ProductTitleSnapshot = productB.Title,
                    UnitPrice = productB.Price,
                    Quantity = 1,
                    LineTotal = 100m
                });
            await db.SaveChangesAsync();
            return 0;
        });

        var orderId = await Scenario.ExecuteDbAsync(async db =>
            (await db.Orders.FirstAsync(o => o.BuyerUserId == buyer.UserId)).Id);

        using var sellerAClient = await Scenario.CreateAuthenticatedClientAsync(sellerA);
        using var sellerBClient = await Scenario.CreateAuthenticatedClientAsync(sellerB);

        await Scenario.ExecuteDbAsync(async db =>
        {
            var order = await db.Orders.FirstAsync(o => o.Id == orderId);
            order.Status = OrderStatus.Completed;
            await db.SaveChangesAsync();
            return 0;
        });

        using var buyerClient = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var returnRequest = await buyerClient.PostAsync($"/api/orders/{orderId}/return", content: null);
        Assert.Equal(HttpStatusCode.OK, returnRequest.StatusCode);

        var orderAfterReturn = await Scenario.ExecuteDbAsync(async db =>
            await db.Orders.AsNoTracking().FirstAsync(o => o.Id == orderId));
        Assert.Equal(OrderStatus.ReturnRequested, orderAfterReturn.Status);

        var approveA = await sellerAClient.PostAsync($"/api/orders/{orderId}/return/approve", content: null);
        Assert.Equal(HttpStatusCode.OK, approveA.StatusCode);

        var orderAfterSellerAApprove = await Scenario.ExecuteDbAsync(async db =>
            await db.Orders.AsNoTracking().FirstAsync(o => o.Id == orderId));

        Assert.Equal(OrderStatus.ReturnRequested, orderAfterSellerAApprove.Status);

        var approveB = await sellerBClient.PostAsync($"/api/orders/{orderId}/return/approve", content: null);
        Assert.Equal(HttpStatusCode.OK, approveB.StatusCode);

        var orderAfterAllApprove = await Scenario.ExecuteDbAsync(async db =>
            await db.Orders.AsNoTracking().FirstAsync(o => o.Id == orderId));

        Assert.Equal(OrderStatus.ReturnApproved, orderAfterAllApprove.Status);
    }
}
