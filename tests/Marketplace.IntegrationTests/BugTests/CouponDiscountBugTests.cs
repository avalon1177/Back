using System.Net;
using System.Net.Http.Json;
using Marketplace.Application.DTO;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Marketplace.IntegrationTests.BugTests;

public sealed class CouponDiscountBugTests : IntegrationTestBase
{
    public CouponDiscountBugTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Checkout_WithFixedCouponDiscountExceedsTotal_MakesOrderFree()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "fixed-coupon-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "fixed-coupon-seller");

        var product = await Scenario.SeedProductAsync(seller, title: "Fixed Coupon Product", price: 50m);
        await Scenario.SeedCartItemAsync(buyer, product, quantity: 1);

        await Scenario.ExecuteDbAsync(async db =>
        {
            var c = new Coupon
            {
                Code = "BIGFIXED",
                Discount = 100m,
                DiscountType = DiscountType.Fixed,
                UsageLimit = 100,
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
            CouponCode: "BIGFIXED"
        );

        var response = await client.PostAsJsonAsync("/api/orders/checkout", checkoutRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var orderResponse = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(orderResponse);
        Assert.Equal(0m, orderResponse.Total);
    }

    [Fact]
    public async Task Checkout_WithFixedCouponDiscountLessThanTotal_ShouldApplyCorrectDiscount()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "fixed-coupon-less-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "fixed-coupon-less-seller");

        var product = await Scenario.SeedProductAsync(seller, title: "Fixed Coupon Less Product", price: 100m);
        await Scenario.SeedCartItemAsync(buyer, product, quantity: 1);

        await Scenario.ExecuteDbAsync(async db =>
        {
            var c = new Coupon
            {
                Code = "FIXED20",
                Discount = 20m,
                DiscountType = DiscountType.Fixed,
                UsageLimit = 100,
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
            CouponCode: "FIXED20"
        );

        var response = await client.PostAsJsonAsync("/api/orders/checkout", checkoutRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var orderResponse = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(orderResponse);
        Assert.Equal(80m, orderResponse.Total);
    }

    [Fact]
    public async Task Checkout_WithPercentageCouponDiscountExceeds100_ShouldCapAt100()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "pct-coupon-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "pct-coupon-seller");

        var product = await Scenario.SeedProductAsync(seller, title: "Pct Coupon Product", price: 100m);
        await Scenario.SeedCartItemAsync(buyer, product, quantity: 1);

        await Scenario.ExecuteDbAsync(async db =>
        {
            var c = new Coupon
            {
                Code = "HUGE200",
                Discount = 200m,
                DiscountType = DiscountType.Percentage,
                UsageLimit = 100,
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
            CouponCode: "HUGE200"
        );

        var response = await client.PostAsJsonAsync("/api/orders/checkout", checkoutRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var orderResponse = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(orderResponse);
        Assert.Equal(0m, orderResponse.Total);
    }

    [Fact]
    public async Task Checkout_CouponUsageLimit_CannotBeExceeded()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "limit-exceed-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "limit-exceed-seller");

        var product = await Scenario.SeedProductAsync(seller, title: "Limit Exceed Product", price: 100m);

        await Scenario.ExecuteDbAsync(async db =>
        {
            var c = new Coupon
            {
                Code = "ONETIME",
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

        await Scenario.SeedCartItemAsync(buyer, product, quantity: 1);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var checkoutRequest = new CheckoutRequest(
            BuyerName: buyer.DisplayName,
            Phone: "+10000000001",
            City: "Test City",
            DeliveryAddress: "Test Address",
            Comment: null,
            CouponCode: "ONETIME"
        );

        var response = await client.PostAsJsonAsync("/api/orders/checkout", checkoutRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
