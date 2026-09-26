using System.Net;
using System.Net.Http.Json;
using Marketplace.Application.DTO;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Marketplace.API.Features.Extras.ExtrasEndpoints;

namespace Marketplace.IntegrationTests.Scenarios;

public sealed class RealWorldScenarioTests : IntegrationTestBase
{
    public RealWorldScenarioTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task OrderCancellation_BuyerCancelsDuringProcessing_StockRestored()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "cancel-processing-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "cancel-processing-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Processing Cancel Product", stock: 10, price: 50m);
        var order = await Scenario.SeedOrderAsync(buyer, product, quantity: 3, status: OrderStatus.Pending);

        using var sellerClient = await Scenario.CreateAuthenticatedClientAsync(seller);
        var confirm = await sellerClient.PutAsync($"/api/orders/{order.Id}/status?status=Confirmed", content: null);
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);

        var initialStock = await Scenario.ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == product.Id).Select(p => p.Stock).SingleAsync());

        using var buyerClient = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var cancel = await buyerClient.PostAsync($"/api/orders/{order.Id}/cancel", content: null);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        var finalStock = await Scenario.ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == product.Id).Select(p => p.Stock).SingleAsync());

        Assert.Equal(initialStock + 3, finalStock);
    }

    [Fact]
    public async Task OrderCancellation_SellerCannotShipAfterCancellation()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "ship-after-cancel-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "ship-after-cancel-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Ship After Cancel Product", price: 50m);
        var order = await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.Pending);

        using var buyerClient = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var cancel = await buyerClient.PostAsync($"/api/orders/{order.Id}/cancel", content: null);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        using var sellerClient = await Scenario.CreateAuthenticatedClientAsync(seller);
        var ship = await sellerClient.PostAsJsonAsync($"/api/orders/{order.Id}/ship", new OrderShipRequest("TRACK-123"));
        
        Assert.Equal(HttpStatusCode.BadRequest, ship.StatusCode);
    }

    [Fact]
    public async Task Checkout_LastItemPurchasedByAnother_BuyerGetsError()
    {
        var buyer1 = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "last-item-b1");
        var buyer2 = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "last-item-b2");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "last-item-seller");
        
        var product = await Scenario.SeedProductAsync(seller, title: "Last Item Product", stock: 1, price: 100m);
        
        await Scenario.SeedCartItemAsync(buyer1, product, 1);
        await Scenario.SeedCartItemAsync(buyer2, product, 1);

        using var client1 = await Scenario.CreateAuthenticatedClientAsync(buyer1);
        var checkout1 = await client1.PostAsJsonAsync("/api/orders/checkout",
            new CheckoutRequest("Buyer1", "+10000000001", "City", "Address", null));
        Assert.Equal(HttpStatusCode.OK, checkout1.StatusCode);

        using var client2 = await Scenario.CreateAuthenticatedClientAsync(buyer2);
        var checkout2 = await client2.PostAsJsonAsync("/api/orders/checkout",
            new CheckoutRequest("Buyer2", "+10000000002", "City", "Address", null));
        
        Assert.Equal(HttpStatusCode.BadRequest, checkout2.StatusCode);
    }

    [Fact]
    public async Task Review_DuplicateReviewNotAllowed()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "dup-review-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "dup-review-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Review Product");
        
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

        var first = await client.PostAsJsonAsync("/api/reviews",
            new ReviewCreateRequest(product.Id, 5, "Great product!"));
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/reviews",
            new ReviewCreateRequest(product.Id, 4, "Actually, 4 stars"));
        
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Review_CannotReviewProductWithoutPurchase()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "no-purchase-review-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "no-purchase-review-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "No Purchase Review Product");

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var review = await client.PostAsJsonAsync("/api/reviews",
            new ReviewCreateRequest(product.Id, 5, "Never bought this"));
        
        Assert.Equal(HttpStatusCode.BadRequest, review.StatusCode);
    }

    [Fact]
    public async Task Cart_ProductDeletedFromSystem_CheckoutFails()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "deleted-product-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "deleted-product-seller");
        
        var product = await Scenario.SeedProductAsync(seller, title: "Will Be Deleted", stock: 5, price: 50m);
        await Scenario.SeedCartItemAsync(buyer, product, 1);

        using var sellerClient = await Scenario.CreateAuthenticatedClientAsync(seller);
        var delete = await sellerClient.DeleteAsync($"/api/products/{product.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        using var buyerClient = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var checkout = await buyerClient.PostAsJsonAsync("/api/orders/checkout",
            new CheckoutRequest("Buyer", "+10000000000", "City", "Address", null));
        
        Assert.Equal(HttpStatusCode.BadRequest, checkout.StatusCode);
    }

    [Fact]
    public async Task SellerRequest_BuyerAppliesToBecomeSeller_AdminApproves()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "seller-request-buyer");
        var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "seller-request-admin");

        using var buyerClient = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var request = await buyerClient.PostAsJsonAsync("/api/seller-requests",
            new CreateSellerRequestBody(null, "I want to sell my handmade crafts"));
        Assert.Equal(HttpStatusCode.NoContent, request.StatusCode);

        var requestId = await Scenario.ExecuteDbAsync(async db =>
            (await db.SellerRequests.FirstAsync(r => r.UserId == buyer.UserId)).Id);

        using var adminClient = await Scenario.CreateAuthenticatedClientAsync(admin);
        var approve = await adminClient.PostAsync($"/api/admin/seller-requests/{requestId}/approve", content: null);
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);

        var requestStatus = await Scenario.ExecuteDbAsync(async db =>
            (await db.SellerRequests.FirstAsync(r => r.Id == requestId)).Status);
        Assert.Equal(SellerRequestStatus.Approved, requestStatus);
    }

    [Fact]
    public async Task SellerRequest_AdminRejectsBuyer_BuyerCanApplyAgain()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "reject-seller-buyer");
        var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "reject-seller-admin");

        using var buyerClient = await Scenario.CreateAuthenticatedClientAsync(buyer);
        await buyerClient.PostAsJsonAsync("/api/seller-requests",
            new CreateSellerRequestBody(null, "Please reject me"));

        var requestId = await Scenario.ExecuteDbAsync(async db =>
            (await db.SellerRequests.FirstAsync(r => r.UserId == buyer.UserId)).Id);

        using var adminClient = await Scenario.CreateAuthenticatedClientAsync(admin);
        await adminClient.PostAsync($"/api/admin/seller-requests/{requestId}/reject", 
            new JsonContent(new { reason = "Invalid information" }));

        var reject = await buyerClient.PostAsJsonAsync("/api/seller-requests",
            new CreateSellerRequestBody(null, "Second attempt with correct info"));
        
        Assert.Equal(HttpStatusCode.NoContent, reject.StatusCode);
    }

    [Fact]
    public async Task Product_StockBecomesZeroAfterAddToCart_CheckoutFails()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "zero-stock-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "zero-stock-seller");
        
        var product = await Scenario.SeedProductAsync(seller, title: "Zero Stock Product", stock: 2, price: 30m);
        await Scenario.SeedCartItemAsync(buyer, product, 2);

        await Scenario.ExecuteDbAsync(async db =>
        {
            var p = await db.Products.FirstAsync(x => x.Id == product.Id);
            p.Stock = 0;
            await db.SaveChangesAsync();
            return 0;
        });

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var checkout = await client.PostAsJsonAsync("/api/orders/checkout",
            new CheckoutRequest("Buyer", "+10000000000", "City", "Address", null));
        
        Assert.Equal(HttpStatusCode.BadRequest, checkout.StatusCode);
    }

    [Fact]
    public async Task Order_BuyerCannotCancelShippedOrder()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "cancel-shipped-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "cancel-shipped-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Cancel Shipped Product", price: 50m);
        var order = await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.Shipped);

        using var buyerClient = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var cancel = await buyerClient.PostAsync($"/api/orders/{order.Id}/cancel", content: null);
        
        Assert.Equal(HttpStatusCode.BadRequest, cancel.StatusCode);
    }

    [Fact]
    public async Task Order_ReturnOnlyForCompletedOrShipped()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "return-eligibility-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "return-eligibility-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Return Eligible Product", price: 50m);

        var pendingOrder = await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.Pending);
        var confirmedOrder = await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.Confirmed);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var returnPending = await client.PostAsync($"/api/orders/{pendingOrder.Id}/return", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, returnPending.StatusCode);

        var returnConfirmed = await client.PostAsync($"/api/orders/{confirmedOrder.Id}/return", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, returnConfirmed.StatusCode);
    }

    [Fact]
    public async Task Chat_CannotMessageYourself()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "self-chat-buyer");

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
        var chat = await client.PostAsJsonAsync("/api/chats", new CreateChatRequest(buyer.UserId, null));
        
        Assert.Equal(HttpStatusCode.BadRequest, chat.StatusCode);
    }

    [Fact]
    public async Task Wishlist_ProductAlreadyPurchased_CanStillReview()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "wishlist-review-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "wishlist-review-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Wishlist Review Product");
        
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

        var wishlist = await client.PostAsJsonAsync("/api/wishlists", new WishlistCreateRequest("My Wishlist"));
        Assert.Equal(HttpStatusCode.OK, wishlist.StatusCode);

        var review = await client.PostAsJsonAsync("/api/reviews",
            new ReviewCreateRequest(product.Id, 5, "Loved it!"));
        Assert.Equal(HttpStatusCode.NoContent, review.StatusCode);
    }

    [Fact]
    public async Task Product_PriceChanged_OrderKeepsOriginalPrice()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "price-change-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "price-change-seller");
        
        var product = await Scenario.SeedProductAsync(seller, title: "Price Change Product", price: 100m);
        var order = await Scenario.SeedOrderAsync(buyer, product, quantity: 1, status: OrderStatus.Pending);

        await Scenario.ExecuteDbAsync(async db =>
        {
            var p = await db.Products.FirstAsync(x => x.Id == product.Id);
            p.Price = 150m;
            await db.SaveChangesAsync();
            return 0;
        });

        var orderTotal = await Scenario.ExecuteDbAsync(async db =>
            (await db.Orders.FirstAsync(x => x.Id == order.Id)).Total);

        Assert.Equal(100m, orderTotal);
    }

    [Fact]
    public async Task Address_OnlyOwnerCanDeleteAddress()
    {
        var buyer1 = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "address-owner-b1");
        var buyer2 = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "address-owner-b2");

        using var client1 = await Scenario.CreateAuthenticatedClientAsync(buyer1);
        var address = await client1.PostAsJsonAsync("/api/addresses",
            new AddressCreateRequest("Home", "+10000000000", "Region", "City", "Street", null, "12345", true));
        Assert.Equal(HttpStatusCode.Created, address.StatusCode);

        var addressId = await Scenario.ExecuteDbAsync(async db =>
            (await db.Addresses.FirstAsync(a => a.UserId == buyer1.UserId)).Id);

        using var client2 = await Scenario.CreateAuthenticatedClientAsync(buyer2);
        var delete = await client2.DeleteAsync($"/api/addresses/{addressId}");
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);

        var stillExists = await Scenario.ExecuteDbAsync(db =>
            db.Addresses.AnyAsync(a => a.Id == addressId));
        Assert.True(stillExists);
    }

    [Fact]
    public async Task Notification_MarkAllAsRead_ClearUnreadCount()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "notifications-buyer");
        
        for (int i = 0; i < 3; i++)
        {
            await Scenario.SeedNotificationAsync(buyer, isRead: false);
        }

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
        
        var unreadBefore = await client.GetAsync("/api/notifications");
        Assert.Equal(HttpStatusCode.OK, unreadBefore.StatusCode);

        foreach (var n in await Scenario.ExecuteDbAsync(async db => 
            await db.Notifications.Where(x => x.UserId == buyer.UserId && !x.IsRead).Select(x => x.Id).ToListAsync()))
        {
            await client.PostAsync($"/api/notifications/{n}/read", content: null);
        }

        var unreadAfter = await Scenario.ExecuteDbAsync(db =>
            db.Notifications.CountAsync(x => x.UserId == buyer.UserId && !x.IsRead));
        
        Assert.Equal(0, unreadAfter);
    }

    [Fact]
    public async Task Product_RejectedProduct_CannotBePublishedBySeller()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "rejected-publish-seller");
        var category = await Scenario.SeedCategoryAsync("Rejected Category");
        var product = await Scenario.SeedProductAsync(seller, category, "Rejected Product", 
            isPublished: false, status: ProductStatus.Rejected);

        using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
        var update = await client.PutAsJsonAsync($"/api/products/{product.Id}",
            new ProductUpdateRequest(category.Id, "Rejected Product", "Updated Description", 100m, 10, true));
        
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var stillUnpublished = await Scenario.ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == product.Id).Select(p => p.IsPublished).SingleAsync());
        
        Assert.False(stillUnpublished);
        Assert.Equal("Rejected Product", await Scenario.ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == product.Id).Select(p => p.Title).SingleAsync()));
    }

    [Fact]
    public async Task Order_AdminCanViewAnyOrder()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "admin-view-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "admin-view-seller");
        var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "admin-view-admin");
        var product = await Scenario.SeedProductAsync(seller, title: "Admin View Product", price: 50m);
        var order = await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.Pending);

        using var adminClient = await Scenario.CreateAuthenticatedClientAsync(admin);
        var view = await adminClient.GetAsync($"/api/orders/{order.Id}");
        Assert.Equal(HttpStatusCode.OK, view.StatusCode);
    }

    [Fact]
    public async Task Store_CannotHaveDuplicateNames()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: false, emailPrefix: "dup-store-seller");

        using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
        
        var create1 = await client.PostAsJsonAsync("/api/stores",
            new StoreCreateRequest("My Cool Store", "Desc", "store@test.local", "+10000000000", "Region", "City", "Street", "12345"));
        Assert.Equal(HttpStatusCode.Created, create1.StatusCode);

        var create2 = await client.PostAsJsonAsync("/api/stores",
            new StoreCreateRequest("My Cool Store", "Another Desc", "store2@test.local", "+10000000001", "Region", "City", "Street", "12346"));
        
        Assert.Equal(HttpStatusCode.BadRequest, create2.StatusCode);
    }

    private class JsonContent : HttpContent
    {
        private readonly byte[] _content;

        public JsonContent(object value)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(value);
            _content = System.Text.Encoding.UTF8.GetBytes(json);
            Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            await stream.WriteAsync(_content);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _content.Length;
            return true;
        }
    }
}
