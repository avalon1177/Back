using System.Net;
using System.Net.Http.Json;
using Marketplace.Application.DTO;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Marketplace.IntegrationTests.BugTests;

public sealed class StateMachineBugTests : IntegrationTestBase
{
    public StateMachineBugTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task OrderCancel_AfterReturnRequested_ShouldFail()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "cancel-return-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "cancel-return-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Cancel Return Product", price: 100m);

        var orderId = await Scenario.ExecuteDbAsync(async db =>
        {
            var order = new Order
            {
                BuyerUserId = buyer.UserId,
                BuyerName = buyer.DisplayName,
                Phone = "+10000000000",
                City = "City",
                DeliveryAddress = "Address",
                Status = OrderStatus.ReturnRequested,
                Total = 100m
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            return order.Id;
        });

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var cancelResponse = await client.PostAsync($"/api/orders/{orderId}/cancel", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, cancelResponse.StatusCode);
    }

    [Fact]
    public async Task OrderCancel_AfterRefunded_ShouldFail()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "cancel-refund-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "cancel-refund-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Cancel Refund Product", price: 100m);

        var orderId = await Scenario.ExecuteDbAsync(async db =>
        {
            var order = new Order
            {
                BuyerUserId = buyer.UserId,
                BuyerName = buyer.DisplayName,
                Phone = "+10000000000",
                City = "City",
                DeliveryAddress = "Address",
                Status = OrderStatus.Refunded,
                Total = 100m
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            return order.Id;
        });

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var cancelResponse = await client.PostAsync($"/api/orders/{orderId}/cancel", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, cancelResponse.StatusCode);
    }

    [Fact]
    public async Task OrderStatus_CancelledToConfirmed_ShouldFail()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "cancelled-confirm-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "cancelled-confirm-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Cancelled Confirm Product", price: 100m);

        var (orderId, storeId) = await Scenario.SeedOrderWithStatusAsync(buyer, product, OrderStatus.Cancelled);

        using var sellerClient = await Scenario.CreateAuthenticatedClientAsync(seller);

        var confirmResponse = await sellerClient.PutAsync($"/api/orders/{orderId}/status?status=Confirmed", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, confirmResponse.StatusCode);
    }

    [Fact]
    public async Task OrderStatus_RefundedToAnyTransition_ShouldFail()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "refunded-any-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "refunded-any-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Refunded Any Product", price: 100m);

        var (orderId, storeId) = await Scenario.SeedOrderWithStatusAsync(buyer, product, OrderStatus.Refunded);

        using var sellerClient = await Scenario.CreateAuthenticatedClientAsync(seller);

        var confirmResponse = await sellerClient.PutAsync($"/api/orders/{orderId}/status?status=Confirmed", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, confirmResponse.StatusCode);
    }

    [Fact]
    public async Task ReturnRequested_AfterReturnRejected_ShouldFail()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "retry-return-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "retry-return-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Retry Return Product", price: 100m);

        var orderId = await Scenario.ExecuteDbAsync(async db =>
        {
            var order = new Order
            {
                BuyerUserId = buyer.UserId,
                BuyerName = buyer.DisplayName,
                Phone = "+10000000000",
                City = "City",
                DeliveryAddress = "Address",
                Status = OrderStatus.ReturnRejected,
                Total = 100m
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            return order.Id;
        });

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var returnResponse = await client.PostAsync($"/api/orders/{orderId}/return", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, returnResponse.StatusCode);
    }

    [Fact]
    public async Task ProductUpdate_ApprovedProduct_RemainsApproved()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "update-approve-seller");
        var category = await Scenario.SeedCategoryAsync("Update Approve Category");
        var product = await Scenario.SeedProductAsync(seller, category: category, title: "Update Approve Product", status: ProductStatus.Approved);

        using var client = await Scenario.CreateAuthenticatedClientAsync(seller);

        var updateRequest = new ProductUpdateRequest(category.Id, "Updated Title", "Description", 50m, 10, true);
        var response = await client.PutAsJsonAsync($"/api/products/{product.Id}", updateRequest);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var status = await Scenario.ExecuteDbAsync(async db =>
            (await db.Products.AsNoTracking().FirstAsync(p => p.Id == product.Id)).Status);

        Assert.Equal(ProductStatus.Approved, status);
    }
}
