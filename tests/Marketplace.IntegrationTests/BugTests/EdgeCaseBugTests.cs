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

public sealed class EdgeCaseBugTests : IntegrationTestBase
{
    public EdgeCaseBugTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task OrderCancel_WhenOrderAlreadyCancelled_ShouldFail()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "cancel-twice-order-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "cancel-twice-order-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Cancel Twice Order Product", stock: 5, price: 25m);
        var order = await Scenario.SeedOrderAsync(buyer, product, quantity: 2, status: OrderStatus.Pending);

        await Scenario.ExecuteDbAsync(async db =>
        {
            var orderEntity = await db.Orders.FirstAsync(o => o.Id == order.Id);
            orderEntity.Status = OrderStatus.Cancelled;
            await db.SaveChangesAsync();
            return 0;
        });

        using var buyerClient = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var cancelResponse = await buyerClient.PostAsync($"/api/orders/{order.Id}/cancel", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, cancelResponse.StatusCode);
    }

    [Fact]
    public async Task OrderCancel_WhenOrderAlreadyCompleted_ShouldFail()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "cancel-completed-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "cancel-completed-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Cancel Completed Product", stock: 5, price: 25m);
        var order = await Scenario.SeedOrderAsync(buyer, product, quantity: 2, status: OrderStatus.Completed);

        using var buyerClient = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var cancelResponse = await buyerClient.PostAsync($"/api/orders/{order.Id}/cancel", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, cancelResponse.StatusCode);
    }

    [Fact]
    public async Task Review_CannotBeCreatedForUnpurchasedProduct()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "no-purchase-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "no-purchase-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "No Purchase Product", price: 100m);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var createReview = await client.PostAsJsonAsync("/api/reviews",
            new ReviewCreateRequest(product.Id, 5, "Great product!"));

        Assert.Equal(HttpStatusCode.BadRequest, createReview.StatusCode);
    }

    [Fact]
    public async Task OrderTotal_ZeroAmountDueToExactCouponDiscount()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "exact-discount-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "exact-discount-seller");

        var product = await Scenario.SeedProductAsync(seller, title: "Exact Discount Product", price: 100m);
        await Scenario.SeedCartItemAsync(buyer, product, quantity: 1);

        await Scenario.ExecuteDbAsync(async db =>
        {
            var coupon = new Coupon
            {
                Code = "FREE100",
                Discount = 100m,
                DiscountType = DiscountType.Fixed,
                UsageLimit = 100,
                IsActive = true
            };
            db.Coupons.Add(coupon);
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
            CouponCode: "FREE100"
        );

        var response = await client.PostAsJsonAsync("/api/orders/checkout", checkoutRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var orderResponse = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(orderResponse);
        Assert.Equal(0m, orderResponse.Total);
    }

    [Fact]
    public async Task Coupon_DiscountCappedAtSubtotal_NotNegative()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "coupon-cap-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "coupon-cap-seller");

        var product = await Scenario.SeedProductAsync(seller, title: "Coupon Cap Product", price: 50m);
        await Scenario.SeedCartItemAsync(buyer, product, quantity: 1);

        await Scenario.ExecuteDbAsync(async db =>
        {
            var coupon = new Coupon
            {
                Code = "SUPER200",
                Discount = 200m,
                DiscountType = DiscountType.Fixed,
                UsageLimit = 100,
                IsActive = true
            };
            db.Coupons.Add(coupon);
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
            CouponCode: "SUPER200"
        );

        var response = await client.PostAsJsonAsync("/api/orders/checkout", checkoutRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var orderResponse = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(orderResponse);
        Assert.True(orderResponse.Total >= 0, "Order total should never be negative");
        Assert.Equal(0m, orderResponse.Total);
    }

    [Fact]
    public async Task OrderStatus_TransitionShippedToReturnRequested_ShouldSucceed()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "shipped-return-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "shipped-return-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Shipped Product", price: 100m);

        var orderId = await Scenario.ExecuteDbAsync(async db =>
        {
            var order = new Order
            {
                BuyerUserId = buyer.UserId,
                BuyerName = buyer.DisplayName,
                Phone = "+10000000000",
                City = "City",
                DeliveryAddress = "Address",
                Status = OrderStatus.Shipped,
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

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var returnResponse = await client.PostAsync($"/api/orders/{orderId}/return", content: null);

        Assert.Equal(HttpStatusCode.OK, returnResponse.StatusCode);

        var orderAfter = await Scenario.ExecuteDbAsync(async db =>
            await db.Orders.AsNoTracking().FirstAsync(o => o.Id == orderId));
        Assert.Equal(OrderStatus.ReturnRequested, orderAfter.Status);
    }

    [Fact]
    public async Task OrderStatus_TransitionPendingToReturnRequested_ShouldFail()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "pending-return-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "pending-return-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Pending Product", price: 100m);

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

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var returnResponse = await client.PostAsync($"/api/orders/{orderId}/return", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, returnResponse.StatusCode);
    }

    [Fact]
    public async Task CartUpdate_WithZeroQuantity_ShouldSucceed()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "zero-qty-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "zero-qty-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Zero Qty Product", stock: 10);
        await Scenario.SeedCartItemAsync(buyer, product, quantity: 5);

        var cartItemId = await Scenario.ExecuteDbAsync(async db =>
            (await db.CartItems.FirstAsync(c => c.BuyerUserId == buyer.UserId)).Id);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var updateResponse = await client.PutAsJsonAsync($"/api/cart/{cartItemId}",
            new CartUpdateRequest(0));

        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
    }

    [Fact]
    public async Task Payment_AmountMustEqualOrderTotal()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "payment-amount-eq-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "payment-amount-eq-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Payment Amount Eq Product", price: 100m);
        var order = await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.Pending);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var wrongAmount = await client.PostAsJsonAsync($"/api/orders/{order.Id}/payments",
            new CreatePaymentRequest(PaymentMethod.Card, 50m));

        Assert.Equal(HttpStatusCode.BadRequest, wrongAmount.StatusCode);

        var correctAmount = await client.PostAsJsonAsync($"/api/orders/{order.Id}/payments",
            new CreatePaymentRequest(PaymentMethod.Card, 100m));

        Assert.Equal(HttpStatusCode.OK, correctAmount.StatusCode);
    }

    [Fact]
    public async Task Payment_CannotAddToNonPendingOrder()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "payment-status-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "payment-status-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Payment Status Product", price: 100m);
        var order = await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.Shipped);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var paymentResponse = await client.PostAsJsonAsync($"/api/orders/{order.Id}/payments",
            new CreatePaymentRequest(PaymentMethod.Card, 100m));

        Assert.Equal(HttpStatusCode.BadRequest, paymentResponse.StatusCode);
    }

    [Fact]
    public async Task StoreSlug_GeneratedFromName_IsUnique()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, emailPrefix: "slug-gen-seller");

        using var client = await Scenario.CreateAuthenticatedClientAsync(seller);

        var createRequest = new StoreCreateRequest(
            "My Store Name",
            "Description",
            "contact@test.com",
            "+10000000000",
            "Region",
            "City",
            "Street",
            "12345"
        );

        var response = await client.PostAsJsonAsync("/api/stores", createRequest);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var storeData = await response.Content.ReadFromJsonAsync<StoreProfile>();
        Assert.NotNull(storeData);
        Assert.Contains("my-store-name", storeData.Slug);
    }

    [Fact]
    public async Task Wishlist_CannotAddSameProductTwice()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "wishlist-twice-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "wishlist-twice-seller");
        var wishlist = await Scenario.SeedWishlistAsync(buyer);
        var product = await Scenario.SeedProductAsync(seller, title: "Wishlist Twice Product");

        await Scenario.SeedWishlistItemAsync(wishlist, product);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var addResponse = await client.PostAsJsonAsync($"/api/wishlists/{wishlist.Id}/items",
            new ProductRefRequest(product.Id));

        Assert.Equal(HttpStatusCode.Conflict, addResponse.StatusCode);
    }

    [Fact]
    public async Task Checkout_WithPercentageCoupon_AppliesCorrectDiscount()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "pct-coupon-apply-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "pct-coupon-apply-seller");

        var product = await Scenario.SeedProductAsync(seller, title: "Pct Coupon Apply Product", price: 100m);
        await Scenario.SeedCartItemAsync(buyer, product, quantity: 2);

        await Scenario.ExecuteDbAsync(async db =>
        {
            var coupon = new Coupon
            {
                Code = "SAVE20PCT",
                Discount = 20m,
                DiscountType = DiscountType.Percentage,
                UsageLimit = 100,
                IsActive = true
            };
            db.Coupons.Add(coupon);
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
            CouponCode: "SAVE20PCT"
        );

        var response = await client.PostAsJsonAsync("/api/orders/checkout", checkoutRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var orderResponse = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(orderResponse);
        Assert.Equal(160m, orderResponse.Total);
    }

    [Fact]
    public async Task Checkout_WithPercentageCouponOver100_CapsAt100Percent()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "pct-over-100-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "pct-over-100-seller");

        var product = await Scenario.SeedProductAsync(seller, title: "Pct Over 100 Product", price: 100m);
        await Scenario.SeedCartItemAsync(buyer, product, quantity: 1);

        await Scenario.ExecuteDbAsync(async db =>
        {
            var coupon = new Coupon
            {
                Code = "SAVE150PCT",
                Discount = 150m,
                DiscountType = DiscountType.Percentage,
                UsageLimit = 100,
                IsActive = true
            };
            db.Coupons.Add(coupon);
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
            CouponCode: "SAVE150PCT"
        );

        var response = await client.PostAsJsonAsync("/api/orders/checkout", checkoutRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var orderResponse = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(orderResponse);
        Assert.Equal(0m, orderResponse.Total);
    }
}
