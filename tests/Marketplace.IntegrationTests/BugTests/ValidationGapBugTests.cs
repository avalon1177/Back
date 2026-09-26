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

public sealed class ValidationGapBugTests : IntegrationTestBase
{
    public ValidationGapBugTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CouponCode_CaseInsensitiveLookup()
    {
        var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "coupon-case-admin");
        await Scenario.SeedCouponAsync("SAVE10");

        using var adminClient = await Scenario.CreateAuthenticatedClientAsync(admin);

        var getUppercase = await adminClient.GetAsync("/api/coupons/SAVE10");
        Assert.Equal(HttpStatusCode.OK, getUppercase.StatusCode);

        var getLowercase = await adminClient.GetAsync("/api/coupons/save10");
        Assert.Equal(HttpStatusCode.OK, getLowercase.StatusCode);

        var getMixed = await adminClient.GetAsync("/api/coupons/SaVe10");
        Assert.Equal(HttpStatusCode.OK, getMixed.StatusCode);
    }

    [Fact]
    public async Task PaymentAmount_InvalidAmountShouldBeRejected()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "payment-amount-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "payment-amount-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Payment Amount Product", price: 100m);
        var order = await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.Pending);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var zeroAmount = await client.PostAsJsonAsync($"/api/orders/{order.Id}/payments",
            new CreatePaymentRequest(PaymentMethod.Card, 0m));
        
        Assert.Equal(HttpStatusCode.BadRequest, zeroAmount.StatusCode);

        var negativeAmount = await client.PostAsJsonAsync($"/api/orders/{order.Id}/payments",
            new CreatePaymentRequest(PaymentMethod.Card, -50m));
        
        Assert.Equal(HttpStatusCode.BadRequest, negativeAmount.StatusCode);
    }

    [Fact]
    public async Task ProductPrice_ZeroPriceShouldBeRejected()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "price-zero-seller");
        var category = await Scenario.SeedCategoryAsync("Price Zero Category");

        using var client = await Scenario.CreateAuthenticatedClientAsync(seller);

        var createZeroPrice = await client.PostAsJsonAsync("/api/products",
            new ProductCreateRequest(category.Id, "Zero Price Product", "Description", 0m, 10, true));
        
        Assert.Equal(HttpStatusCode.BadRequest, createZeroPrice.StatusCode);
    }

    [Fact]
    public async Task ProductStock_NegativeStockShouldBeRejected()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "stock-neg-seller");
        var category = await Scenario.SeedCategoryAsync("Stock Neg Category");

        using var client = await Scenario.CreateAuthenticatedClientAsync(seller);

        var createNegativeStock = await client.PostAsJsonAsync("/api/products",
            new ProductCreateRequest(category.Id, "Negative Stock Product", "Description", 10m, -5, true));
        
        Assert.Equal(HttpStatusCode.BadRequest, createNegativeStock.StatusCode);
    }

    [Fact]
    public async Task AddressValidation_EmptyFieldsShouldBeRejected()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "address-validation-buyer");

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var emptyName = await client.PostAsJsonAsync("/api/addresses",
            new AddressCreateRequest(" ", "+10000000000", "Region", "City", "Street", null, "12345"));
        Assert.Equal(HttpStatusCode.BadRequest, emptyName.StatusCode);

        var shortPhone = await client.PostAsJsonAsync("/api/addresses",
            new AddressCreateRequest("Home", "123456", "Region", "City", "Street", null, "12345"));
        Assert.Equal(HttpStatusCode.BadRequest, shortPhone.StatusCode);
    }

    [Fact]
    public async Task ReviewRating_OutOfRangeShouldBeRejected()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "review-rating-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "review-rating-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Review Rating Product");

        await Scenario.ExecuteDbAsync(async db =>
        {
            var order = new Order
            {
                BuyerUserId = buyer.UserId,
                BuyerName = buyer.DisplayName,
                Phone = "+10000000000",
                City = "City",
                DeliveryAddress = "Address",
                Status = OrderStatus.Completed,
                Total = 10m
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
                LineTotal = 10m
            });
            await db.SaveChangesAsync();
            return 0;
        });

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var rating0 = await client.PostAsJsonAsync("/api/reviews",
            new ReviewCreateRequest(product.Id, 0, "Zero stars"));
        Assert.Equal(HttpStatusCode.BadRequest, rating0.StatusCode);

        var rating6 = await client.PostAsJsonAsync("/api/reviews",
            new ReviewCreateRequest(product.Id, 6, "Six stars"));
        Assert.Equal(HttpStatusCode.BadRequest, rating6.StatusCode);
    }

    [Fact]
    public async Task ShippingMethod_InvalidPriceShouldBeRejected()
    {
        var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "shipping-validation-admin");

        using var client = await Scenario.CreateAuthenticatedClientAsync(admin);

        var negativePrice = await client.PostAsJsonAsync("/api/shipping-methods",
            new CreateShippingMethodRequest(ShippingMethodType.Courier, -10m, 2));
        Assert.Equal(HttpStatusCode.BadRequest, negativePrice.StatusCode);
    }
}
