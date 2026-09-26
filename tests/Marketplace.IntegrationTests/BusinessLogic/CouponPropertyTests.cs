using System.Net;
using System.Net.Http.Json;
using Marketplace.Application.DTO;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Marketplace.IntegrationTests.BusinessLogic;

public sealed class CouponPropertyTests : IntegrationTestBase
{
    public CouponPropertyTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Theory]
    [InlineData(10, 100, 10)]   // 10% off 100 = 10 discount
    [InlineData(25, 200, 50)]   // 25% off 200 = 50 discount
    [InlineData(50, 150, 75)]   // 50% off 150 = 75 discount
    [InlineData(100, 100, 100)]  // 100% off 100 = 100 discount
    public async Task Checkout_PercentageCoupon_CorrectCalculation(decimal percentOff, decimal subtotal, decimal expectedDiscount)
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: $"pct-{percentOff}-{subtotal}");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: $"pct-seller-{percentOff}");

        var product = await Scenario.SeedProductAsync(seller, title: $"Pct Product {percentOff}", price: subtotal);
        await Scenario.SeedCartItemAsync(buyer, product, quantity: 1);

        await Scenario.ExecuteDbAsync(async db =>
        {
            db.Coupons.Add(new Coupon
            {
                Code = $"PCT{percentOff}",
                Discount = percentOff,
                DiscountType = DiscountType.Percentage,
                IsActive = true
            });
            await db.SaveChangesAsync();
            return 0;
        });

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var response = await client.PostAsJsonAsync("/api/orders/checkout",
            new CheckoutRequest(buyer.DisplayName, "+10000000000", "City", "Address", null, $"PCT{percentOff}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);
        Assert.Equal(subtotal - expectedDiscount, order.Total);
    }

    [Theory]
    [InlineData(10, 100, 10)]   // 10 fixed off 100 = 10 discount
    [InlineData(50, 200, 50)]   // 50 fixed off 200 = 50 discount
    [InlineData(100, 150, 100)]  // 100 fixed off 150 = 100 discount
    public async Task Checkout_FixedCoupon_CorrectCalculation(decimal fixedDiscount, decimal subtotal, decimal expectedDiscount)
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: $"fixed-{fixedDiscount}-{subtotal}");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: $"fixed-seller-{fixedDiscount}");

        var product = await Scenario.SeedProductAsync(seller, title: $"Fixed Product {fixedDiscount}", price: subtotal);
        await Scenario.SeedCartItemAsync(buyer, product, quantity: 1);

        await Scenario.ExecuteDbAsync(async db =>
        {
            db.Coupons.Add(new Coupon
            {
                Code = $"FIXED{fixedDiscount}",
                Discount = fixedDiscount,
                DiscountType = DiscountType.Fixed,
                IsActive = true
            });
            await db.SaveChangesAsync();
            return 0;
        });

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var response = await client.PostAsJsonAsync("/api/orders/checkout",
            new CheckoutRequest(buyer.DisplayName, "+10000000000", "City", "Address", null, $"FIXED{fixedDiscount}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);
        Assert.Equal(subtotal - expectedDiscount, order.Total);
    }

    [Fact]
    public async Task Checkout_FixedCouponExceedsSubtotal_OrderIsFree()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "free-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "free-seller");

        var product = await Scenario.SeedProductAsync(seller, title: "Free Product", price: 50m);
        await Scenario.SeedCartItemAsync(buyer, product, quantity: 1);

        await Scenario.ExecuteDbAsync(async db =>
        {
            db.Coupons.Add(new Coupon
            {
                Code = "BIGFIXED",
                Discount = 100m,
                DiscountType = DiscountType.Fixed,
                IsActive = true
            });
            await db.SaveChangesAsync();
            return 0;
        });

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var response = await client.PostAsJsonAsync("/api/orders/checkout",
            new CheckoutRequest(buyer.DisplayName, "+10000000000", "City", "Address", null, "BIGFIXED"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);
        Assert.Equal(0m, order.Total);
    }

    [Theory]
    [InlineData(150, 100)]  // 150% capped at 100% = 0
    [InlineData(200, 100)]  // 200% capped at 100% = 0
    [InlineData(500, 100)]  // 500% capped at 100% = 0
    public async Task Checkout_PercentageOver100_CappedAt100(decimal percentOff, decimal expectedDiscountPercent)
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: $"over-{percentOff}");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: $"over-seller-{percentOff}");

        var product = await Scenario.SeedProductAsync(seller, title: $"Over Product {percentOff}", price: 100m);
        await Scenario.SeedCartItemAsync(buyer, product, quantity: 1);

        await Scenario.ExecuteDbAsync(async db =>
        {
            db.Coupons.Add(new Coupon
            {
                Code = $"OVER{percentOff}",
                Discount = percentOff,
                DiscountType = DiscountType.Percentage,
                IsActive = true
            });
            await db.SaveChangesAsync();
            return 0;
        });

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var response = await client.PostAsJsonAsync("/api/orders/checkout",
            new CheckoutRequest(buyer.DisplayName, "+10000000000", "City", "Address", null, $"OVER{percentOff}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);
        var expectedTotal = 100m * (1 - expectedDiscountPercent / 100m);
        Assert.Equal(expectedTotal, order.Total);
    }

    [Theory]
    [InlineData(2, 50, 1)]   // 2 items * 50 = 100 - 10% = 90
    [InlineData(3, 33.33, 1)]  // 3 * 33.33 = 99.99 - 10% = 89.991
    public async Task Checkout_MultiItemWithCoupon_AppliesToSubtotal(decimal quantity, decimal unitPrice, decimal discountPercent)
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: $"multi-{quantity}");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: $"multi-seller-{quantity}");

        var product = await Scenario.SeedProductAsync(seller, title: $"Multi Product", price: unitPrice);
        await Scenario.SeedCartItemAsync(buyer, product, quantity: (int)quantity);

        await Scenario.ExecuteDbAsync(async db =>
        {
            db.Coupons.Add(new Coupon
            {
                Code = "MULTIPCT",
                Discount = discountPercent,
                DiscountType = DiscountType.Percentage,
                IsActive = true
            });
            await db.SaveChangesAsync();
            return 0;
        });

        var subtotal = unitPrice * quantity;

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var response = await client.PostAsJsonAsync("/api/orders/checkout",
            new CheckoutRequest(buyer.DisplayName, "+10000000000", "City", "Address", null, "MULTIPCT"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);

        var expectedDiscount = subtotal * (discountPercent / 100m);
        var expectedTotal = subtotal - expectedDiscount;
        Assert.Equal(expectedTotal, order.Total);
    }

    [Fact]
    public async Task Checkout_CouponUsageCount_IncrementedCorrectly()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "count-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Count Product", price: 100m);

        await Scenario.ExecuteDbAsync(async db =>
        {
            db.Coupons.Add(new Coupon
            {
                Code = "COUNTER",
                Discount = 10m,
                DiscountType = DiscountType.Percentage,
                UsageLimit = 5,
                UsedCount = 0,
                IsActive = true
            });
            await db.SaveChangesAsync();
            return 0;
        });

        for (int i = 0; i < 3; i++)
        {
            var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: $"count-buyer-{i}");
            await Scenario.SeedCartItemAsync(buyer, product, quantity: 1);

            using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
            var response = await client.PostAsJsonAsync("/api/orders/checkout",
                new CheckoutRequest(buyer.DisplayName, "+10000000000", "City", "Address", null, "COUNTER"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var usedCount = await Scenario.ExecuteDbAsync(db =>
            db.Coupons.Where(c => c.Code == "COUNTER").Select(c => c.UsedCount).SingleAsync());

        Assert.Equal(3, usedCount);
    }
}
