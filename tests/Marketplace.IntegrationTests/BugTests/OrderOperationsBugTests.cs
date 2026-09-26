using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Marketplace.Application.DTO;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Marketplace.IntegrationTests.BugTests;

public sealed class OrderOperationsBugTests : IntegrationTestBase
{
    public OrderOperationsBugTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task OrderRefund_WithValidOrder_ShouldProcessRefund()
    {
        var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "refund-admin");
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "refund-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "refund-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Refund Product", price: 100m);

        var orderId = await Scenario.ExecuteDbAsync(async db =>
        {
            var order = new Order
            {
                BuyerUserId = buyer.UserId,
                BuyerName = buyer.DisplayName,
                Phone = "+10000000000",
                City = "City",
                DeliveryAddress = "Address",
                Status = OrderStatus.ReturnApproved,
                Total = 100m
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            db.OrderItems.Add(new OrderItem
            {
                OrderId = order.Id,
                ProductId = product.Id,
                StoreId = product.StoreId,
                ProductTitleSnapshot = product.Title,
                UnitPrice = product.Price,
                Quantity = 1,
                LineTotal = 100m
            });
            await db.SaveChangesAsync();
            return order.Id;
        });

        using var adminClient = await Scenario.CreateAuthenticatedClientAsync(admin);

        var refundResponse = await adminClient.PostAsync($"/api/orders/{orderId}/refund", content: null);

        Assert.Equal(HttpStatusCode.OK, refundResponse.StatusCode);

        var order = await Scenario.ExecuteDbAsync(async db =>
            await db.Orders.AsNoTracking().FirstAsync(o => o.Id == orderId));
        Assert.Equal(OrderStatus.Refunded, order.Status);
    }

    [Fact]
    public async Task OrderRefund_WithInvalidStatus_ShouldReturnBadRequest()
    {
        var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "refund-invalid-admin");
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "refund-invalid-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "refund-invalid-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Refund Invalid Product", price: 100m);

        var orderId = await Scenario.ExecuteDbAsync(async db =>
        {
            var order = new Order
            {
                BuyerUserId = buyer.UserId,
                BuyerName = buyer.DisplayName,
                Phone = "+10000000000",
                City = "City",
                DeliveryAddress = "Address",
                Status = OrderStatus.Completed,
                Total = 100m
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            db.OrderItems.Add(new OrderItem
            {
                OrderId = order.Id,
                ProductId = product.Id,
                StoreId = product.StoreId,
                ProductTitleSnapshot = product.Title,
                UnitPrice = product.Price,
                Quantity = 1,
                LineTotal = 100m
            });
            await db.SaveChangesAsync();
            return order.Id;
        });

        using var adminClient = await Scenario.CreateAuthenticatedClientAsync(admin);

        var refundResponse = await adminClient.PostAsync($"/api/orders/{orderId}/refund", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, refundResponse.StatusCode);
    }

    [Fact]
    public async Task OrderRefund_WithoutAdminRole_ShouldReturnForbidden()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "refund-forbidden-seller");
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "refund-forbidden-buyer");
        var otherSeller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "refund-forbidden-other");
        var product = await Scenario.SeedProductAsync(seller, title: "Refund Forbidden Product", price: 100m);

        var orderId = await Scenario.ExecuteDbAsync(async db =>
        {
            var order = new Order
            {
                BuyerUserId = buyer.UserId,
                BuyerName = buyer.DisplayName,
                Phone = "+10000000000",
                City = "City",
                DeliveryAddress = "Address",
                Status = OrderStatus.ReturnApproved,
                Total = 100m
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            db.OrderItems.Add(new OrderItem
            {
                OrderId = order.Id,
                ProductId = product.Id,
                StoreId = product.StoreId,
                ProductTitleSnapshot = product.Title,
                UnitPrice = product.Price,
                Quantity = 1,
                LineTotal = 100m
            });
            await db.SaveChangesAsync();
            return order.Id;
        });

        using var otherSellerClient = await Scenario.CreateAuthenticatedClientAsync(otherSeller);

        var refundResponse = await otherSellerClient.PostAsync($"/api/orders/{orderId}/refund", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, refundResponse.StatusCode);
    }

    [Fact]
    public async Task OrderShip_WithTrackingNumber_ShouldSucceed()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "ship-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "ship-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Ship Product", price: 100m);

        var orderId = await Scenario.ExecuteDbAsync(async db =>
        {
            var order = new Order
            {
                BuyerUserId = buyer.UserId,
                BuyerName = buyer.DisplayName,
                Phone = "+10000000000",
                City = "City",
                DeliveryAddress = "Address",
                Status = OrderStatus.Confirmed,
                Total = 100m
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            db.OrderItems.Add(new OrderItem
            {
                OrderId = order.Id,
                ProductId = product.Id,
                StoreId = product.StoreId,
                ProductTitleSnapshot = product.Title,
                UnitPrice = product.Price,
                Quantity = 1,
                LineTotal = 100m
            });
            await db.SaveChangesAsync();
            return order.Id;
        });

        using var sellerClient = await Scenario.CreateAuthenticatedClientAsync(seller);

        var shipRequest = new OrderShipRequest("TRACK123456");
        var shipResponse = await sellerClient.PostAsJsonAsync($"/api/orders/{orderId}/ship", shipRequest);

        Assert.Equal(HttpStatusCode.OK, shipResponse.StatusCode);

        var order = await Scenario.ExecuteDbAsync(async db =>
            await db.Orders.AsNoTracking().FirstAsync(o => o.Id == orderId));
        Assert.Equal(OrderStatus.Shipped, order.Status);
    }

    [Fact]
    public async Task OrderShip_WithInvalidOrderStatus_ShouldReturnBadRequest()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "ship-invalid-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "ship-invalid-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Ship Invalid Product", price: 100m);

        var orderId = await Scenario.ExecuteDbAsync(async db =>
        {
            var order = new Order
            {
                BuyerUserId = buyer.UserId,
                BuyerName = buyer.DisplayName,
                Phone = "+10000000000",
                City = "City",
                DeliveryAddress = "Address",
                Status = OrderStatus.Pending,
                Total = 100m
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            db.OrderItems.Add(new OrderItem
            {
                OrderId = order.Id,
                ProductId = product.Id,
                StoreId = product.StoreId,
                ProductTitleSnapshot = product.Title,
                UnitPrice = product.Price,
                Quantity = 1,
                LineTotal = 100m
            });
            await db.SaveChangesAsync();
            return order.Id;
        });

        using var sellerClient = await Scenario.CreateAuthenticatedClientAsync(seller);

        var shipRequest = new OrderShipRequest("TRACK123456");
        var shipResponse = await sellerClient.PostAsJsonAsync($"/api/orders/{orderId}/ship", shipRequest);

        Assert.Equal(HttpStatusCode.BadRequest, shipResponse.StatusCode);
    }

    [Fact]
    public async Task SellerOrderStats_ReturnsCorrectStatistics()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "stats-seller");
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "stats-buyer");

        var product = await Scenario.SeedProductAsync(seller, title: "Stats Product", price: 50m);

        await Scenario.ExecuteDbAsync(async db =>
        {
            var orders = new[]
            {
                new Order { BuyerUserId = buyer.UserId, BuyerName = "Buyer", Phone = "+1", City = "City", DeliveryAddress = "Addr", Status = OrderStatus.Completed, Total = 50m },
                new Order { BuyerUserId = buyer.UserId, BuyerName = "Buyer", Phone = "+1", City = "City", DeliveryAddress = "Addr", Status = OrderStatus.Completed, Total = 100m },
                new Order { BuyerUserId = buyer.UserId, BuyerName = "Buyer", Phone = "+1", City = "City", DeliveryAddress = "Addr", Status = OrderStatus.Pending, Total = 75m }
            };
            db.Orders.AddRange(orders);
            await db.SaveChangesAsync();

            foreach (var order in orders)
            {
                db.OrderItems.Add(new OrderItem
                {
                    OrderId = order.Id,
                    ProductId = product.Id,
                    StoreId = product.StoreId,
                    ProductTitleSnapshot = product.Title,
                    UnitPrice = product.Price,
                    Quantity = 1,
                    LineTotal = product.Price
                });
            }
            await db.SaveChangesAsync();
            return 0;
        });

        using var sellerClient = await Scenario.CreateAuthenticatedClientAsync(seller);

        var statsResponse = await sellerClient.GetAsync("/api/orders/seller/stats");

        Assert.Equal(HttpStatusCode.OK, statsResponse.StatusCode);

        var stats = await statsResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(stats.TryGetProperty("totalOrders", out _));
        Assert.True(stats.TryGetProperty("totalRevenue", out _));
    }

    [Fact]
    public async Task SellerOrderStats_OnlyIncludesOwnOrders()
    {
        var sellerA = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "stats-a-seller");
        var sellerB = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "stats-b-seller");
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "stats-compare-buyer");

        var productA = await Scenario.SeedProductAsync(sellerA, title: "Stats A Product", price: 100m);
        await Scenario.SeedProductAsync(sellerB, title: "Stats B Product", price: 200m);

        await Scenario.ExecuteDbAsync(async db =>
        {
            db.Orders.Add(new Order
            {
                BuyerUserId = buyer.UserId, BuyerName = "Buyer", Phone = "+1", City = "City",
                DeliveryAddress = "Addr", Status = OrderStatus.Completed, Total = 300m
            });
            await db.SaveChangesAsync();

            var order = db.Orders.Local.First();
            db.OrderItems.AddRange(
                new OrderItem { OrderId = order.Id, ProductId = productA.Id, StoreId = productA.StoreId, ProductTitleSnapshot = productA.Title, UnitPrice = 100m, Quantity = 1, LineTotal = 100m }
            );
            await db.SaveChangesAsync();
            return 0;
        });

        using var sellerAClient = await Scenario.CreateAuthenticatedClientAsync(sellerA);

        var statsResponse = await sellerAClient.GetAsync("/api/orders/seller/stats");
        var stats = await statsResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, statsResponse.StatusCode);
        Assert.True(stats.TryGetProperty("totalOrders", out var orders));
        Assert.Equal(1, orders.GetInt32());
    }
}
