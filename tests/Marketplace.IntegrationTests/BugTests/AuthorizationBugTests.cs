using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Marketplace.Application.DTO;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Marketplace.API.Features.Extras.ExtrasEndpoints;

namespace Marketplace.IntegrationTests.BugTests;

public sealed class AuthorizationBugTests : IntegrationTestBase
{
    public AuthorizationBugTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task SellerCannotCreatePaymentForAnotherSellersOrder()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "payment-auth-buyer");
        var sellerA = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "payment-auth-seller-a");
        var sellerB = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "payment-auth-seller-b");
        var product = await Scenario.SeedProductAsync(sellerA, title: "Seller A Product", price: 50m);
        var order = await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.Pending);

        using var sellerBClient = await Scenario.CreateAuthenticatedClientAsync(sellerB);

        var createPayment = await sellerBClient.PostAsJsonAsync($"/api/orders/{order.Id}/payments",
            new CreatePaymentRequest(PaymentMethod.Card, order.Total));

        Assert.Equal(HttpStatusCode.Forbidden, createPayment.StatusCode);
    }

    [Fact]
    public async Task LiqPayCallback_RepeatedCallbackWithDifferentStatus_UpdatesIncorrectly()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "liqpay-idemp-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "liqpay-idemp-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "LiqPay Idemp Product");
        var order = await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.Pending);
        var payment = await Scenario.SeedPaymentAsync(order, PaymentMethod.LiqPay, PaymentStatus.Pending, externalReference: "LP-IDEMP-REF");

        using var client = Factory.CreateClient();

        var successCallback = CreateLiqPayCallback("LP-IDEMP-REF", "success", "TRX-1");
        var successResponse = await client.PostAsync("/api/payments/liqpay/callback", successCallback);
        Assert.Equal(HttpStatusCode.OK, successResponse.StatusCode);

        var failureCallback = CreateLiqPayCallback("LP-IDEMP-REF", "failure", "TRX-2");
        var failureResponse = await client.PostAsync("/api/payments/liqpay/callback", failureCallback);
        
        var paymentStatus = await Scenario.ExecuteDbAsync(async db =>
            await db.Payments.AsNoTracking().Where(x => x.Id == payment.Id).Select(x => x.Status).SingleAsync());
        
        var orderStatus = await Scenario.ExecuteDbAsync(async db =>
            await db.Orders.AsNoTracking().Where(x => x.Id == order.Id).Select(x => x.Status).SingleAsync());

        Assert.Equal(PaymentStatus.Completed, paymentStatus);
        Assert.Equal(OrderStatus.Confirmed, orderStatus);
    }

    [Fact]
    public async Task BuyerCannotCancelAnotherBuyersOrder()
    {
        var buyer1 = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "cancel-auth-b1");
        var buyer2 = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "cancel-auth-b2");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "cancel-auth-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Cancel Auth Product");
        var order = await Scenario.SeedOrderAsync(buyer1, product, status: OrderStatus.Pending);

        using var buyer2Client = await Scenario.CreateAuthenticatedClientAsync(buyer2);

        var cancel = await buyer2Client.PostAsync($"/api/orders/{order.Id}/cancel", content: null);
        Assert.Equal(HttpStatusCode.NotFound, cancel.StatusCode);
    }

    [Fact]
    public async Task SellerCannotApproveReturnForOrderWithoutTheirItems()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "return-auth-buyer");
        var sellerA = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "return-auth-seller-a");
        var sellerB = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "return-auth-seller-b");
        var productA = await Scenario.SeedProductAsync(sellerA, title: "Seller A Return Product");
        
        await Scenario.ExecuteDbAsync(async db =>
        {
            var order = new Order
            {
                BuyerUserId = buyer.UserId,
                BuyerName = buyer.DisplayName,
                Phone = "+10000000000",
                City = "Test City",
                DeliveryAddress = "Test Address",
                Status = OrderStatus.Completed,
                Total = 50m
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            db.OrderItems.Add(new OrderItem
            {
                OrderId = order.Id,
                ProductId = productA.Id,
                StoreId = productA.StoreId,
                ProductTitleSnapshot = productA.Title,
                UnitPrice = productA.Price,
                Quantity = 1,
                LineTotal = 50m
            });
            await db.SaveChangesAsync();
            return 0;
        });

        var orderId = await Scenario.ExecuteDbAsync(async db =>
            (await db.Orders.FirstAsync(o => o.BuyerUserId == buyer.UserId)).Id);

        using var buyerClient = await Scenario.CreateAuthenticatedClientAsync(buyer);
        await buyerClient.PostAsync($"/api/orders/{orderId}/return", content: null);

        using var sellerBClient = await Scenario.CreateAuthenticatedClientAsync(sellerB);
        var approve = await sellerBClient.PostAsync($"/api/orders/{orderId}/return/approve", content: null);
        
        Assert.Equal(HttpStatusCode.NotFound, approve.StatusCode);
    }

    [Fact]
    public async Task SellerCannotRejectReturnForOrderWithoutTheirItems()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "reject-auth-buyer");
        var sellerA = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "reject-auth-seller-a");
        var sellerB = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "reject-auth-seller-b");
        var productA = await Scenario.SeedProductAsync(sellerA, title: "Seller A Reject Product");
        
        await Scenario.ExecuteDbAsync(async db =>
        {
            var order = new Order
            {
                BuyerUserId = buyer.UserId,
                BuyerName = buyer.DisplayName,
                Phone = "+10000000000",
                City = "Test City",
                DeliveryAddress = "Test Address",
                Status = OrderStatus.Completed,
                Total = 50m
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            db.OrderItems.Add(new OrderItem
            {
                OrderId = order.Id,
                ProductId = productA.Id,
                StoreId = productA.StoreId,
                ProductTitleSnapshot = productA.Title,
                UnitPrice = productA.Price,
                Quantity = 1,
                LineTotal = 50m
            });
            await db.SaveChangesAsync();
            return 0;
        });

        var orderId = await Scenario.ExecuteDbAsync(async db =>
            (await db.Orders.FirstAsync(o => o.BuyerUserId == buyer.UserId)).Id);

        using var buyerClient = await Scenario.CreateAuthenticatedClientAsync(buyer);
        await buyerClient.PostAsync($"/api/orders/{orderId}/return", content: null);

        using var sellerBClient = await Scenario.CreateAuthenticatedClientAsync(sellerB);
        var reject = await sellerBClient.PostAsync($"/api/orders/{orderId}/return/reject", content: null);
        
        Assert.Equal(HttpStatusCode.NotFound, reject.StatusCode);
    }

    private static FormUrlEncodedContent CreateLiqPayCallback(string orderReference, string status, string transactionId)
    {
        var data = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            order_id = orderReference,
            status,
            transaction_id = transactionId
        })));

        return new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["data"] = data,
            ["signature"] = "test-signature"
        });
    }
}
