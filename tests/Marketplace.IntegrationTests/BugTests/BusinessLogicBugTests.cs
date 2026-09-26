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

public sealed class BusinessLogicBugTests : IntegrationTestBase
{
    public BusinessLogicBugTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task MultiSellerOrder_SellerOnlySeesTheirItems()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "multi-order-buyer");
        var sellerA = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "multi-order-seller-a");
        var sellerB = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "multi-order-seller-b");

        var productA = await Scenario.SeedProductAsync(sellerA, title: "Seller A Product", price: 100m);
        var productB = await Scenario.SeedProductAsync(sellerB, title: "Seller B Product", price: 200m);

        await Scenario.ExecuteDbAsync(async db =>
        {
            var order = new Order
            {
                BuyerUserId = buyer.UserId,
                BuyerName = buyer.DisplayName,
                Phone = "+10000000000",
                City = "Test City",
                DeliveryAddress = "Test Address",
                Status = OrderStatus.Pending,
                Total = 300m
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
                    LineTotal = 100m
                },
                new OrderItem
                {
                    OrderId = order.Id,
                    ProductId = productB.Id,
                    StoreId = productB.StoreId,
                    ProductTitleSnapshot = productB.Title,
                    UnitPrice = productB.Price,
                    Quantity = 1,
                    LineTotal = 200m
                });
            await db.SaveChangesAsync();
            return 0;
        });

        var orderId = await Scenario.ExecuteDbAsync(async db =>
            (await db.Orders.FirstAsync(o => o.BuyerUserId == buyer.UserId)).Id);

        using var sellerAClient = await Scenario.CreateAuthenticatedClientAsync(sellerA);

        var response = await sellerAClient.GetAsync($"/api/orders/{orderId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var orderData = await response.ReadJsonAsync();
        var items = orderData.RootElement.GetProperty("items");
        var sellerItems = items.EnumerateArray().Where(i => 
            i.GetProperty("storeId").GetGuid() == sellerA.StoreId).ToList();

        Assert.Single(sellerItems);
        Assert.Equal(productA.Id, sellerItems.First().GetProperty("productId").GetGuid());
    }

    [Fact]
    public async Task OrderStatusTransition_InvalidPathShouldBeBlocked()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "status-transition-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "status-transition-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Status Transition Product");
        var order = await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.Pending);

        using var sellerClient = await Scenario.CreateAuthenticatedClientAsync(seller);

        var sellerRestrictedStatuses = new[]
        {
            OrderStatus.Cancelled,
            OrderStatus.Refunded,
            OrderStatus.ReturnApproved,
            OrderStatus.ReturnRejected
        };

        foreach (var restrictedStatus in sellerRestrictedStatuses)
        {
            var response = await sellerClient.PutAsync($"/api/orders/{order.Id}/status?status={restrictedStatus}", content: null);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        var confirmResponse = await sellerClient.PutAsync($"/api/orders/{order.Id}/status?status=Confirmed", content: null);
        Assert.Equal(HttpStatusCode.NoContent, confirmResponse.StatusCode);

        var status = await Scenario.ExecuteDbAsync(async db =>
            await db.Orders.Where(x => x.Id == order.Id).Select(x => x.Status).SingleAsync());
        Assert.Equal(OrderStatus.Confirmed, status);
    }

    [Fact]
    public async Task ReviewCreate_RequiresCompletedOrder()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "review-check-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "review-check-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Review Check Product");

        var invalidStatuses = new[] { OrderStatus.Pending, OrderStatus.Confirmed, OrderStatus.Shipped, OrderStatus.Cancelled };

        var orderIds = new List<Guid>();
        foreach (var status in invalidStatuses)
        {
            var orderId = await Scenario.ExecuteDbAsync(async db =>
            {
                var order = new Order
                {
                    BuyerUserId = buyer.UserId,
                    BuyerName = buyer.DisplayName,
                    Phone = "+10000000000",
                    City = "City",
                    DeliveryAddress = "Address",
                    Status = status,
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
                return order.Id;
            });
            orderIds.Add(orderId);
        }

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        foreach (var (orderId, status) in orderIds.Zip(invalidStatuses))
        {
            var review = await client.PostAsJsonAsync("/api/reviews", 
                new ReviewCreateRequest(product.Id, 5, "Great product"));
            
            Assert.Equal(HttpStatusCode.BadRequest, review.StatusCode);
        }
    }

    [Fact]
    public async Task CategorySlug_UpdateDoesNotChangeSlug()
    {
        var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "slug-admin");
        var category = await Scenario.SeedCategoryAsync("Original Category");
        
        var originalSlug = await Scenario.ExecuteDbAsync(async db =>
            (await db.Categories.FirstAsync(c => c.Id == category.Id)).Slug);

        using var adminClient = await Scenario.CreateAuthenticatedClientAsync(admin);

        var update = await adminClient.PutAsJsonAsync($"/api/categories/{category.Id}",
            new CategoryUpdateRequest("Updated Category Name", null));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var updatedSlug = await Scenario.ExecuteDbAsync(async db =>
            (await db.Categories.FirstAsync(c => c.Id == category.Id)).Slug);

        Assert.Equal(originalSlug, updatedSlug);
    }

    [Fact]
    public async Task Checkout_RequiresAllProductsPublished()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "checkout-published-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "checkout-published-seller");
        
        var publishedProduct = await Scenario.SeedProductAsync(seller, title: "Published", price: 50m, isPublished: true, status: ProductStatus.Approved);
        var unpublishedProduct = await Scenario.SeedProductAsync(seller, title: "Unpublished", price: 50m, isPublished: false, status: ProductStatus.Pending);

        await Scenario.SeedCartItemAsync(buyer, publishedProduct);
        await Scenario.SeedCartItemAsync(buyer, unpublishedProduct);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var checkout = await client.PostAsJsonAsync("/api/orders/checkout",
            new CheckoutRequest("Buyer", "+10000000000", "City", "Address", null));

        Assert.Equal(HttpStatusCode.BadRequest, checkout.StatusCode);
    }

    [Fact]
    public async Task Checkout_ValidatesStockAvailability()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "checkout-stock-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "checkout-stock-seller");
        
        var product = await Scenario.SeedProductAsync(seller, title: "Low Stock", price: 50m, stock: 1);
        await Scenario.SeedCartItemAsync(buyer, product, 2);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var checkout = await client.PostAsJsonAsync("/api/orders/checkout",
            new CheckoutRequest("Buyer", "+10000000000", "City", "Address", null));

        Assert.Equal(HttpStatusCode.BadRequest, checkout.StatusCode);
    }

    [Fact]
    public async Task CouponUsageLimit_SecondCheckoutFailsAfterFirstSucceeds()
    {
        var buyerA = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "coupon-race-a");
        var buyerB = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "coupon-race-b");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "coupon-race-seller");

        var productA = await Scenario.SeedProductAsync(seller, title: "Race Product A", price: 100m);
        var productB = await Scenario.SeedProductAsync(seller, title: "Race Product B", price: 100m);

        await Scenario.SeedCartItemAsync(buyerA, productA, quantity: 1);
        await Scenario.SeedCartItemAsync(buyerB, productB, quantity: 1);

        await Scenario.ExecuteDbAsync(async db =>
        {
            var coupon = new Coupon
            {
                Code = "SINGLEUSE",
                Discount = 10m,
                DiscountType = DiscountType.Fixed,
                UsageLimit = 1,
                UsedCount = 0,
                IsActive = true
            };
            db.Coupons.Add(coupon);
            await db.SaveChangesAsync();
            return coupon;
        });

        using var clientA = await Scenario.CreateAuthenticatedClientAsync(buyerA);
        using var clientB = await Scenario.CreateAuthenticatedClientAsync(buyerB);

        var checkoutA = new CheckoutRequest(
            BuyerName: buyerA.DisplayName,
            Phone: "+10000000001",
            City: "Test City",
            DeliveryAddress: "Test Address",
            Comment: null,
            CouponCode: "SINGLEUSE"
        );

        var checkoutB = new CheckoutRequest(
            BuyerName: buyerB.DisplayName,
            Phone: "+10000000002",
            City: "Test City",
            DeliveryAddress: "Test Address",
            Comment: null,
            CouponCode: "SINGLEUSE"
        );

        var responseA = await clientA.PostAsJsonAsync("/api/orders/checkout", checkoutA);
        Assert.Equal(HttpStatusCode.OK, responseA.StatusCode);

        var responseB = await clientB.PostAsJsonAsync("/api/orders/checkout", checkoutB);

        var finalUsedCount = await Scenario.ExecuteDbAsync(async db =>
            (await db.Coupons.FirstAsync(c => c.Code == "SINGLEUSE")).UsedCount);

        Assert.Equal(HttpStatusCode.BadRequest, responseB.StatusCode);
        Assert.Equal(1, finalUsedCount);
    }

    [Fact]
    public async Task StoreCreation_DuplicateNameShouldBeRejected()
    {
        var sellerA = await Scenario.CreateActorAsync(UserRole.Seller, emailPrefix: "store-race-a");
        var sellerB = await Scenario.CreateActorAsync(UserRole.Seller, emailPrefix: "store-race-b");

        using var clientA = await Scenario.CreateAuthenticatedClientAsync(sellerA);
        using var clientB = await Scenario.CreateAuthenticatedClientAsync(sellerB);

        var createRequestA = new StoreCreateRequest(
            "My Amazing Store",
            "Best store ever",
            "contact@test.com",
            "+10000000000",
            "Region",
            "City",
            "Street 1",
            "12345"
        );

        var createRequestB = new StoreCreateRequest(
            "My Amazing Store",
            "Another great store",
            "contact2@test.com",
            "+10000000001",
            "Region",
            "City",
            "Street 2",
            "12345"
        );

        var responseA = await clientA.PostAsJsonAsync("/api/stores", createRequestA);
        Assert.Equal(HttpStatusCode.Created, responseA.StatusCode);

        var responseB = await clientB.PostAsJsonAsync("/api/stores", createRequestB);
        Assert.Equal(HttpStatusCode.BadRequest, responseB.StatusCode);

        var storeCount = await Scenario.ExecuteDbAsync(async db =>
            await db.Stores.CountAsync(s => s.Name == "My Amazing Store"));

        Assert.Equal(1, storeCount);
    }

    [Fact]
    public async Task ChatCreation_ExistingChatBetweenBuyerSeller_CreatesDuplicate()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "chat-dup-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "chat-dup-seller");

        await Scenario.SeedChatAsync(buyer, seller);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var createChatRequest = new CreateChatRequest(seller.UserId, null);

        var firstChat = await client.PostAsJsonAsync("/api/chats", createChatRequest);
        var secondChat = await client.PostAsJsonAsync("/api/chats", createChatRequest);

        var chatCount = await Scenario.ExecuteDbAsync(async db =>
            await db.Chats.CountAsync(c => c.BuyerId == buyer.UserId && c.SellerId == seller.UserId));

        Assert.Equal(HttpStatusCode.OK, firstChat.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondChat.StatusCode);
        Assert.Equal(1, chatCount);
    }

    [Fact]
    public async Task Checkout_StockDeduction_OnlyOneSucceedsWithLimitedStock()
    {
        var buyerA = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "stock-race-a");
        var buyerB = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "stock-race-b");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "stock-race-seller");

        var product = await Scenario.SeedProductAsync(seller, title: "Limited Stock Product", price: 100m, stock: 2);

        await Scenario.SeedCartItemAsync(buyerA, product, quantity: 2);
        await Scenario.SeedCartItemAsync(buyerB, product, quantity: 2);

        using var clientA = await Scenario.CreateAuthenticatedClientAsync(buyerA);
        using var clientB = await Scenario.CreateAuthenticatedClientAsync(buyerB);

        var checkoutA = new CheckoutRequest(
            BuyerName: buyerA.DisplayName,
            Phone: "+10000000001",
            City: "Test City",
            DeliveryAddress: "Test Address",
            Comment: null,
            CouponCode: null
        );

        var checkoutB = new CheckoutRequest(
            BuyerName: buyerB.DisplayName,
            Phone: "+10000000002",
            City: "Test City",
            DeliveryAddress: "Test Address",
            Comment: null,
            CouponCode: null
        );

        var responseA = await clientA.PostAsJsonAsync("/api/orders/checkout", checkoutA);
        Assert.Equal(HttpStatusCode.OK, responseA.StatusCode);

        var responseB = await clientB.PostAsJsonAsync("/api/orders/checkout", checkoutB);

        var finalStock = await Scenario.ExecuteDbAsync(async db =>
            (await db.Products.FirstAsync(p => p.Id == product.Id)).Stock);

        Assert.Equal(HttpStatusCode.BadRequest, responseB.StatusCode);
        Assert.True(finalStock >= 0, $"Stock should not be negative. Actual: {finalStock}");
    }

    [Fact]
    public async Task NotificationEndpoint_ReturnsAllWithoutPagination()
    {
        var actor = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "notify-pagination-buyer");

        for (int i = 0; i < 50; i++)
        {
            await Scenario.SeedNotificationAsync(actor, $"Notification {i}", $"Text {i}");
        }

        using var client = await Scenario.CreateAuthenticatedClientAsync(actor);

        var response = await client.GetAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var notifications = await response.Content.ReadFromJsonAsync<List<object>>();

        Assert.NotNull(notifications);
        Assert.Equal(50, notifications.Count);
    }

    [Fact]
    public async Task MultiSellerReturn_OneSellerRejects_StockShouldNotBeRestored()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "reject-buyer");
        var sellerA = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "reject-seller-a");
        var sellerB = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "reject-seller-b");

        var productA = await Scenario.SeedProductAsync(sellerA, title: "Reject Product A", price: 50m, stock: 10);
        var productB = await Scenario.SeedProductAsync(sellerB, title: "Reject Product B", price: 100m, stock: 10);

        var orderId = await Scenario.ExecuteDbAsync(async db =>
        {
            var order = new Order
            {
                BuyerUserId = buyer.UserId,
                BuyerName = buyer.DisplayName,
                Phone = "+10000000000",
                City = "Test City",
                DeliveryAddress = "Test Address",
                Status = OrderStatus.Completed,
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
                    Quantity = 2,
                    LineTotal = 100m
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

            var product = await db.Products.FirstAsync(p => p.Id == productA.Id);
            product.Stock -= 2;
            var productBEntity = await db.Products.FirstAsync(p => p.Id == productB.Id);
            productBEntity.Stock -= 1;
            await db.SaveChangesAsync();

            return order.Id;
        });

        using var buyerClient = await Scenario.CreateAuthenticatedClientAsync(buyer);
        using var sellerAClient = await Scenario.CreateAuthenticatedClientAsync(sellerA);
        using var sellerBClient = await Scenario.CreateAuthenticatedClientAsync(sellerB);

        var returnRequest = await buyerClient.PostAsync($"/api/orders/{orderId}/return", content: null);
        Assert.Equal(HttpStatusCode.OK, returnRequest.StatusCode);

        var rejectA = await sellerAClient.PostAsync($"/api/orders/{orderId}/return/reject", content: null);
        Assert.Equal(HttpStatusCode.OK, rejectA.StatusCode);

        var orderAfterReject = await Scenario.ExecuteDbAsync(async db =>
            await db.Orders.AsNoTracking().FirstAsync(o => o.Id == orderId));

        var stockA = await Scenario.ExecuteDbAsync(async db =>
            (await db.Products.FirstAsync(p => p.Id == productA.Id)).Stock);

        var stockB = await Scenario.ExecuteDbAsync(async db =>
            (await db.Products.FirstAsync(p => p.Id == productB.Id)).Stock);

        Assert.Equal(OrderStatus.ReturnRejected, orderAfterReject.Status);
        Assert.Equal(8, stockA);
        Assert.Equal(9, stockB);
    }
}
