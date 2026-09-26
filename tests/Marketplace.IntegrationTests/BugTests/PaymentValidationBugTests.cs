using System.Net;
using System.Net.Http.Json;
using Marketplace.Application.DTO;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Marketplace.API.Features.Extras.ExtrasEndpoints;

namespace Marketplace.IntegrationTests.BugTests;

public sealed class PaymentValidationBugTests : IntegrationTestBase
{
    public PaymentValidationBugTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Payment_AmountExceedsOrderTotal_ShouldReject()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "overpay-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "overpay-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Overpay Product", price: 100m);
        var order = await Scenario.SeedOrderAsync(buyer, product, quantity: 1, status: OrderStatus.Pending);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var payment = new CreatePaymentRequest(PaymentMethod.Card, 150m, PaymentStatus.Pending);

        var response = await client.PostAsJsonAsync($"/api/orders/{order.Id}/payments", payment);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Payment_AmountLessThanOrderTotal_ShouldReject()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "underpay-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "underpay-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Underpay Product", price: 100m);
        var order = await Scenario.SeedOrderAsync(buyer, product, quantity: 1, status: OrderStatus.Pending);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var payment = new CreatePaymentRequest(PaymentMethod.Card, 50m, PaymentStatus.Pending);

        var response = await client.PostAsJsonAsync($"/api/orders/{order.Id}/payments", payment);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Payment_AmountEqualsOrderTotal_ShouldAccept()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "exactpay-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "exactpay-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Exact Pay Product", price: 100m);
        var order = await Scenario.SeedOrderAsync(buyer, product, quantity: 1, status: OrderStatus.Pending);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var payment = new CreatePaymentRequest(PaymentMethod.Card, 100m, PaymentStatus.Pending);

        var response = await client.PostAsJsonAsync($"/api/orders/{order.Id}/payments", payment);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

public sealed class CouponExpiryBugTests : IntegrationTestBase
{
    public CouponExpiryBugTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Coupon_ExpiredCouponLookup_ShouldReturnNotFound()
    {
        await Scenario.ExecuteDbAsync(async db =>
        {
            var coupon = new Coupon
            {
                Code = "PASTDATE",
                Discount = 10m,
                DiscountType = DiscountType.Percentage,
                IsActive = true,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(-1)
            };
            db.Coupons.Add(coupon);
            await db.SaveChangesAsync();
            return 0;
        });

        using var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/coupons/PASTDATE");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Coupon_ValidCouponLookup_ShouldReturnCoupon()
    {
        await Scenario.ExecuteDbAsync(async db =>
        {
            var coupon = new Coupon
            {
                Code = "STILLGOOD",
                Discount = 10m,
                DiscountType = DiscountType.Percentage,
                IsActive = true,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(30)
            };
            db.Coupons.Add(coupon);
            await db.SaveChangesAsync();
            return 0;
        });

        using var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/coupons/STILLGOOD");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Coupon_NoExpiryCoupon_ShouldBeValid()
    {
        await Scenario.ExecuteDbAsync(async db =>
        {
            var coupon = new Coupon
            {
                Code = "NOEXPIRY",
                Discount = 10m,
                DiscountType = DiscountType.Percentage,
                IsActive = true,
                ExpiresAtUtc = null
            };
            db.Coupons.Add(coupon);
            await db.SaveChangesAsync();
            return 0;
        });

        using var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/coupons/NOEXPIRY");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
