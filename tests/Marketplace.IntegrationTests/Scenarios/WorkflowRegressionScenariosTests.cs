using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Marketplace.API.Features.Extras;
using Marketplace.Application.DTO;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.IntegrationTests.Infrastructure;
using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Marketplace.API.Features.Extras.ExtrasEndpoints;

namespace Marketplace.IntegrationTests.Scenarios;

public sealed class WorkflowRegressionScenariosTests : IntegrationTestBase
{
    public WorkflowRegressionScenariosTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task PendingProduct_CannotBeSelfPublishedViaUpdate()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "self-publish");
        var category = await Scenario.SeedCategoryAsync("Pending Category");
        var product = await Scenario.SeedProductAsync(seller, category, "Pending Product", isPublished: false, status: ProductStatus.Pending);

        using var sellerClient = await Scenario.CreateAuthenticatedClientAsync(seller);
        using var anonymousClient = Factory.CreateClient();

        var update = await sellerClient.PutAsJsonAsync(
            $"/api/products/{product.Id}",
            new ProductUpdateRequest(category.Id, product.Title, "updated description for pending product", product.Price, product.Stock, true));

        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var updated = await Scenario.ExecuteDbAsync(db => db.Products.AsNoTracking().FirstAsync(x => x.Id == product.Id));
        Assert.False(updated.IsPublished);

        var publicFetch = await anonymousClient.GetAsync($"/api/products/{product.Slug}");
        Assert.Equal(HttpStatusCode.NotFound, publicFetch.StatusCode);
    }

    [Fact]
    public async Task SellerStats_UseOnlyOwnOrderItemRevenue()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "stats-buyer");
        var sellerA = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "stats-seller-a");
        var sellerB = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "stats-seller-b");
        var productA = await Scenario.SeedProductAsync(sellerA, title: "Seller A Product", price: 100m);
        var productB = await Scenario.SeedProductAsync(sellerB, title: "Seller B Product", price: 250m);

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
                Total = 600m
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
                    Quantity = 2,
                    LineTotal = 500m
                });
            await db.SaveChangesAsync();
            return 0;
        });

        using var sellerClient = await Scenario.CreateAuthenticatedClientAsync(sellerA);
        var response = await sellerClient.GetAsync("/api/orders/seller/stats");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var payload = await response.ReadJsonAsync();
        Assert.Equal(100m, payload.RootElement.GetProperty("totalRevenue").GetDecimal());
    }

    [Fact]
    public async Task Seller_CannotSetRestrictedOrderStatusesDirectly()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "status-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "status-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Restricted Status Product");
        var order = await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.Confirmed);

        using var sellerClient = await Scenario.CreateAuthenticatedClientAsync(seller);

        foreach (var status in new[] { OrderStatus.Cancelled, OrderStatus.Refunded, OrderStatus.ReturnApproved })
        {
            var response = await sellerClient.PutAsync($"/api/orders/{order.Id}/status?status={status}", content: null);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task ReviewRequiresCompletedOrder_NotPendingOrCancelled()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "review-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "review-seller");
        var pendingProduct = await Scenario.SeedProductAsync(seller, title: "Pending Review Product");
        var cancelledProduct = await Scenario.SeedProductAsync(seller, title: "Cancelled Review Product");

        await Scenario.SeedOrderAsync(buyer, pendingProduct, status: OrderStatus.Pending);
        await Scenario.SeedOrderAsync(buyer, cancelledProduct, status: OrderStatus.Cancelled);

        using var buyerClient = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var pendingResponse = await buyerClient.PostAsJsonAsync("/api/reviews", new ReviewCreateRequest(pendingProduct.Id, 5, "Looks good"));
        var cancelledResponse = await buyerClient.PostAsJsonAsync("/api/reviews", new ReviewCreateRequest(cancelledProduct.Id, 5, "Looks good"));

        Assert.Equal(HttpStatusCode.BadRequest, pendingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, cancelledResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_CannotUseSellerOnlyCompanyMeEndpoints()
    {
        var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "company-admin");
        using var adminClient = await Scenario.CreateAuthenticatedClientAsync(admin);

        var finance = await adminClient.GetAsync("/api/company/me/finance");
        var schedules = await adminClient.GetAsync("/api/company/me/schedules");

        Assert.Equal(HttpStatusCode.Forbidden, finance.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, schedules.StatusCode);
    }

    [Fact]
    public async Task DeletingProduct_RemovesStoredImages()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "delete-storage");
        var product = await Scenario.SeedProductAsync(seller, title: "Delete Storage Product");
        var image = await Scenario.SeedProductImageAsync(product);

        using var sellerClient = await Scenario.CreateAuthenticatedClientAsync(seller);
        using var anonymousClient = Factory.CreateClient();

        var delete = await sellerClient.DeleteAsync($"/api/products/{product.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var imageResponse = await anonymousClient.GetAsync($"/api/images/{image.FileName}");
        Assert.Equal(HttpStatusCode.NotFound, imageResponse.StatusCode);
    }

    [Fact]
    public async Task ChatCreation_ValidatesSellerAndStoreRelationship()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "chat-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "chat-seller");
        var otherSeller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "chat-other");

        using var buyerClient = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var missingSeller = await buyerClient.PostAsJsonAsync("/api/chats", new CreateChatRequest(Guid.NewGuid(), null));
        var mismatchedStore = await buyerClient.PostAsJsonAsync("/api/chats", new CreateChatRequest(seller.UserId, otherSeller.StoreId));
        var valid = await buyerClient.PostAsJsonAsync("/api/chats", new CreateChatRequest(seller.UserId, seller.StoreId));

        Assert.Equal(HttpStatusCode.NotFound, missingSeller.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, mismatchedStore.StatusCode);
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_IsRotatedAndOldTokenCannotBeReused()
    {
        var actor = await Scenario.CreateActorAsync(UserRole.Buyer, emailConfirmed: true, emailPrefix: "refresh-rotate");
        using var client = Factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(actor.Email, actor.Password, "device-1"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var originalCookie = login.GetRefreshTokenCookie();

        using var firstRefreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh")
        {
            Content = JsonContent.Create(new RefreshTokenRequest("ignored", "device-2"))
        };
        firstRefreshRequest.Headers.Add("Cookie", originalCookie);

        var firstRefresh = await client.SendAsync(firstRefreshRequest);
        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);

        using var secondRefreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh")
        {
            Content = JsonContent.Create(new RefreshTokenRequest("ignored", "device-3"))
        };
        secondRefreshRequest.Headers.Add("Cookie", originalCookie);

        var secondRefresh = await client.SendAsync(secondRefreshRequest);
        Assert.Equal(HttpStatusCode.BadRequest, secondRefresh.StatusCode);
    }

    [Fact]
    public async Task TwoFactorPendingToken_CannotBeReusedAfterSuccess()
    {
        var actor = await Scenario.CreateActorAsync(UserRole.Buyer, emailConfirmed: true, twoFactorEnabled: true, emailPrefix: "2fa-reuse");
        using var client = Factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(actor.Email, actor.Password, "device-1"));
        Assert.Equal(HttpStatusCode.Accepted, login.StatusCode);

        var pending = await login.Content.ReadFromJsonAsync<TwoFactorRequiredResponse>();
        Assert.NotNull(pending);
        var code = await Scenario.GenerateAuthenticatorCodeAsync(actor);

        var first = await client.PostAsJsonAsync("/api/auth/2fa/verify", new TwoFactorVerifyRequest(pending.TwoFactorToken, code, "device-1"));
        var second = await client.PostAsJsonAsync("/api/auth/2fa/verify", new TwoFactorVerifyRequest(pending.TwoFactorToken, code, "device-1"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task AddressDefaultOperations_KeepSingleDefaultAddress()
    {
        var actor = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "address-default");
        using var client = await Scenario.CreateAuthenticatedClientAsync(actor);

        var firstCreate = await client.PostAsJsonAsync("/api/addresses", new AddressCreateRequest("Home", "+10000000000", "Region", "City A", "Line 1", null, "10000", true));
        var secondCreate = await client.PostAsJsonAsync("/api/addresses", new AddressCreateRequest("Office", "+10000000000", "Region", "City B", "Line 2", null, "10001", true));

        using var secondPayload = await secondCreate.ReadJsonAsync();
        var secondId = secondPayload.RootElement.GetProperty("id").GetGuid();

        var third = await Scenario.SeedAddressAsync(actor, isDefault: false, city: "City C");
        var updateThird = await client.PutAsJsonAsync($"/api/addresses/{third.Id}", new AddressUpdateRequest(null, null, null, null, null, null, null, true));
        Assert.Equal(HttpStatusCode.OK, updateThird.StatusCode);

        var defaults = await Scenario.ExecuteDbAsync(async db =>
            await db.Addresses.AsNoTracking().Where(x => x.UserId == actor.UserId && x.IsDefault).Select(x => x.Id).ToListAsync());

        Assert.Single(defaults);
        Assert.Contains(third.Id, defaults);

        var firstStatus = await Scenario.ExecuteDbAsync(async db => await db.Addresses.AsNoTracking().FirstAsync(x => x.Id == secondId));
        Assert.False(firstStatus.IsDefault);
    }

    [Fact]
    public async Task Cart_AddingSameProductMergesQuantityAndHonorsStockLimit()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "cart-merge-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "cart-merge-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Merge Product", stock: 3);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var firstAdd = await client.PostAsJsonAsync("/api/cart", new CartAddRequest(product.Id, 1));
        var secondAdd = await client.PostAsJsonAsync("/api/cart", new CartAddRequest(product.Id, 2));
        var overflowAdd = await client.PostAsJsonAsync("/api/cart", new CartAddRequest(product.Id, 1));

        Assert.Equal(HttpStatusCode.NoContent, firstAdd.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, secondAdd.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, overflowAdd.StatusCode);

        var quantity = await Scenario.ExecuteDbAsync(async db =>
            await db.CartItems.AsNoTracking().Where(x => x.BuyerUserId == buyer.UserId && x.ProductId == product.Id).Select(x => x.Quantity).SingleAsync());

        Assert.Equal(3, quantity);
    }

    [Fact]
    public async Task CheckoutClearsCart_DecrementsStock_AndCancelRestoresStock()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "checkout-state-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "checkout-state-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Checkout State Product", stock: 5, price: 25m);
        await Scenario.SeedCartItemAsync(buyer, product, 2);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var checkout = await client.PostAsJsonAsync("/api/orders/checkout", new CheckoutRequest("Buyer", "+10000000000", "City", "Address", null));
        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);

        var order = await checkout.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);

        var stateAfterCheckout = await Scenario.ExecuteDbAsync(async db => new
        {
            CartCount = await db.CartItems.CountAsync(x => x.BuyerUserId == buyer.UserId),
            Stock = await db.Products.Where(x => x.Id == product.Id).Select(x => x.Stock).SingleAsync()
        });

        Assert.Equal(0, stateAfterCheckout.CartCount);
        Assert.Equal(3, stateAfterCheckout.Stock);

        var cancel = await client.PostAsync($"/api/orders/{order.Id}/cancel", content: null);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        var stockAfterCancel = await Scenario.ExecuteDbAsync(async db =>
            await db.Products.Where(x => x.Id == product.Id).Select(x => x.Stock).SingleAsync());

        Assert.Equal(5, stockAfterCancel);
    }

    [Fact]
    public async Task WishlistAdd_DuplicateReturnsConflict()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "wishlist-idempotent-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "wishlist-idempotent-seller");
        var wishlist = await Scenario.SeedWishlistAsync(buyer);
        var product = await Scenario.SeedProductAsync(seller, title: "Wishlist Idempotent Product");

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var first = await client.PostAsJsonAsync($"/api/wishlists/{wishlist.Id}/items", new ProductRefRequest(product.Id));
        var second = await client.PostAsJsonAsync($"/api/wishlists/{wishlist.Id}/items", new ProductRefRequest(product.Id));

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var count = await Scenario.ExecuteDbAsync(async db =>
            await db.WishlistItems.CountAsync(x => x.WishlistId == wishlist.Id && x.ProductId == product.Id));

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task LiqPayCallback_ConfirmsPendingOrderOnSuccess_AndKeepsPendingOnFailure()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "liqpay-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "liqpay-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "LiqPay Product");
        var successOrder = await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.Pending);
        var failedOrder = await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.Pending);
        var successPayment = await Scenario.SeedPaymentAsync(successOrder, PaymentMethod.LiqPay, PaymentStatus.Pending, externalReference: "LP-SUCCESS-REF");
        var failedPayment = await Scenario.SeedPaymentAsync(failedOrder, PaymentMethod.LiqPay, PaymentStatus.Pending, externalReference: "LP-FAIL-REF");

        using var client = Factory.CreateClient();

        var successResponse = await client.PostAsync("/api/payments/liqpay/callback", CreateLiqPayCallback("LP-SUCCESS-REF", "success", "TRX-1"));
        var failedResponse = await client.PostAsync("/api/payments/liqpay/callback", CreateLiqPayCallback("LP-FAIL-REF", "failure", "TRX-2"));

        Assert.Equal(HttpStatusCode.OK, successResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, failedResponse.StatusCode);

        var state = await Scenario.ExecuteDbAsync(async db => new
        {
            SuccessOrder = await db.Orders.AsNoTracking().Where(x => x.Id == successOrder.Id).Select(x => x.Status).SingleAsync(),
            FailedOrder = await db.Orders.AsNoTracking().Where(x => x.Id == failedOrder.Id).Select(x => x.Status).SingleAsync(),
            SuccessPayment = await db.Payments.AsNoTracking().Where(x => x.Id == successPayment.Id).Select(x => x.Status).SingleAsync(),
            FailedPayment = await db.Payments.AsNoTracking().Where(x => x.Id == failedPayment.Id).Select(x => x.Status).SingleAsync()
        });

        Assert.Equal(OrderStatus.Confirmed, state.SuccessOrder);
        Assert.Equal(OrderStatus.Pending, state.FailedOrder);
        Assert.Equal(PaymentStatus.Completed, state.SuccessPayment);
        Assert.Equal(PaymentStatus.Failed, state.FailedPayment);
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
