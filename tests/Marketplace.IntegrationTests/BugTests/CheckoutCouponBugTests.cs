using System.Net;
using System.Net.Http.Json;
using Marketplace.Application.DTO;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Marketplace.IntegrationTests.BugTests;

public sealed class CheckoutCouponBugTests : IntegrationTestBase
{
    public CheckoutCouponBugTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Checkout_WithCouponCode_ShouldApplyDiscount()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "coupon-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "coupon-seller");

        var product = await Scenario.SeedProductAsync(seller, title: "Test Product", price: 100m);
        await Scenario.SeedCartItemAsync(buyer, product, quantity: 1);

        var coupon = await Scenario.ExecuteDbAsync(async db =>
        {
            var c = new Coupon
            {
                Code = "DISCOUNT10",
                Discount = 10m,
                DiscountType = DiscountType.Percentage,
                UsageLimit = 100,
                IsActive = true
            };
            db.Coupons.Add(c);
            await db.SaveChangesAsync();
            return c;
        });

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var checkoutRequest = new CheckoutRequest(
            BuyerName: buyer.DisplayName,
            Phone: "+10000000001",
            City: "Test City",
            DeliveryAddress: "Test Address",
            Comment: null,
            CouponCode: "DISCOUNT10"
        );

        var response = await client.PostAsJsonAsync("/api/orders/checkout", checkoutRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var orderResponse = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(orderResponse);
        Assert.Equal(90m, orderResponse.Total);
    }

    [Fact]
    public async Task Checkout_WithExpiredCoupon_ShouldReject()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "expired-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "expired-seller");

        var product = await Scenario.SeedProductAsync(seller, title: "Test Product", price: 100m);
        await Scenario.SeedCartItemAsync(buyer, product, quantity: 1);

        await Scenario.ExecuteDbAsync(async db =>
        {
            var c = new Coupon
            {
                Code = "EXPIRED",
                Discount = 50m,
                DiscountType = DiscountType.Fixed,
                UsageLimit = 100,
                IsActive = true,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(-1)
            };
            db.Coupons.Add(c);
            await db.SaveChangesAsync();
            return 0;
        });

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var checkoutRequest = new CheckoutRequest(
            BuyerName: buyer.DisplayName,
            Phone: "+10000000001",
            City: "Test City",
            DeliveryAddress: "Test Address",
            Comment: null,
            CouponCode: "EXPIRED"
        );

        var response = await client.PostAsJsonAsync("/api/orders/checkout", checkoutRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_WithCouponUsageLimitReached_ShouldReject()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "limit-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "limit-seller");

        var product = await Scenario.SeedProductAsync(seller, title: "Test Product", price: 100m);
        await Scenario.SeedCartItemAsync(buyer, product, quantity: 1);

        await Scenario.ExecuteDbAsync(async db =>
        {
            var c = new Coupon
            {
                Code = "LIMITED",
                Discount = 10m,
                DiscountType = DiscountType.Percentage,
                UsageLimit = 1,
                UsedCount = 1,
                IsActive = true
            };
            db.Coupons.Add(c);
            await db.SaveChangesAsync();
            return 0;
        });

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var checkoutRequest = new CheckoutRequest(
            BuyerName: buyer.DisplayName,
            Phone: "+10000000001",
            City: "Test City",
            DeliveryAddress: "Test Address",
            Comment: null,
            CouponCode: "LIMITED"
        );

        var response = await client.PostAsJsonAsync("/api/orders/checkout", checkoutRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_WithInvalidCouponCode_ShouldReject()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "invalid-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "invalid-seller");

        var product = await Scenario.SeedProductAsync(seller, title: "Test Product", price: 100m);
        await Scenario.SeedCartItemAsync(buyer, product, quantity: 1);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var checkoutRequest = new CheckoutRequest(
            BuyerName: buyer.DisplayName,
            Phone: "+10000000001",
            City: "Test City",
            DeliveryAddress: "Test Address",
            Comment: null,
            CouponCode: "NONEXISTENT"
        );

        var response = await client.PostAsJsonAsync("/api/orders/checkout", checkoutRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
