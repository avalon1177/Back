using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Marketplace.Application.DTO;
using Marketplace.Core.Enums;
using Marketplace.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Marketplace.API.Features.Extras.ExtrasEndpoints;

namespace Marketplace.IntegrationTests.Database;

public sealed class MutationDatabaseAssertionsTests : IntegrationTestBase
{
    public MutationDatabaseAssertionsTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task AddressMutations_PersistChanges_AndRejectInvalidPayloads()
    {
        var actor = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "address-db");
        using var client = await Scenario.CreateAuthenticatedClientAsync(actor);

        var invalidCreate = await client.PostAsJsonAsync("/api/addresses", new AddressCreateRequest(" ", "+10000000000", "Region", "City", "Line 1", null, "10000"));
        Assert.Equal(HttpStatusCode.BadRequest, invalidCreate.StatusCode);
        Assert.Equal(0, await Scenario.ExecuteDbAsync(db => db.Addresses.CountAsync(x => x.UserId == actor.UserId)));

        var create = await client.PostAsJsonAsync("/api/addresses", new AddressCreateRequest("Home", "+10000000000", "Region", "City", "Line 1", null, "10000", true));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var createdId = await ReadGuidAsync(create);

        var created = await Scenario.ExecuteDbAsync(db => db.Addresses.AsNoTracking().SingleAsync(x => x.Id == createdId));
        Assert.True(created.IsDefault);
        Assert.Equal("Home", created.Name);

        var invalidUpdate = await client.PutAsJsonAsync($"/api/addresses/{createdId}", new AddressUpdateRequest(" ", null, null, null, null, null, null, false));
        Assert.Equal(HttpStatusCode.BadRequest, invalidUpdate.StatusCode);

        var update = await client.PutAsJsonAsync($"/api/addresses/{createdId}", new AddressUpdateRequest("Office", null, null, "Updated City", null, null, null, false));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var second = await Scenario.SeedAddressAsync(actor, isDefault: false, city: "Backup City");
        var setDefault = await client.PostAsync($"/api/addresses/{second.Id}/default", content: null);
        Assert.Equal(HttpStatusCode.OK, setDefault.StatusCode);

        var defaults = await Scenario.ExecuteDbAsync(async db => await db.Addresses.AsNoTracking()
            .Where(x => x.UserId == actor.UserId)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new { x.Id, x.Name, x.City, x.IsDefault })
            .ToListAsync());

        Assert.Equal(2, defaults.Count);
        Assert.Equal("Office", defaults.Single(x => x.Id == createdId).Name);
        Assert.Equal("Updated City", defaults.Single(x => x.Id == createdId).City);
        Assert.False(defaults.Single(x => x.Id == createdId).IsDefault);
        Assert.True(defaults.Single(x => x.Id == second.Id).IsDefault);

        var delete = await client.DeleteAsync($"/api/addresses/{second.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.False(await Scenario.ExecuteDbAsync(db => db.Addresses.AnyAsync(x => x.Id == second.Id)));
    }

    [Fact]
    public async Task CartMutations_PersistChanges_AndRejectInvalidPayloads()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "cart-db-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "cart-db-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Cart DB Product", stock: 5);
        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var invalidAdd = await client.PostAsJsonAsync("/api/cart", new CartAddRequest(product.Id, 0));
        Assert.Equal(HttpStatusCode.BadRequest, invalidAdd.StatusCode);
        Assert.Equal(0, await Scenario.ExecuteDbAsync(db => db.CartItems.CountAsync(x => x.BuyerUserId == buyer.UserId)));

        var add = await client.PostAsJsonAsync("/api/cart", new CartAddRequest(product.Id, 2));
        Assert.Equal(HttpStatusCode.NoContent, add.StatusCode);
        var itemId = await Scenario.ExecuteDbAsync(async db => await db.CartItems.AsNoTracking()
            .Where(x => x.BuyerUserId == buyer.UserId && x.ProductId == product.Id)
            .Select(x => x.Id)
            .SingleAsync());

        var update = await client.PutAsJsonAsync($"/api/cart/{itemId}", new CartUpdateRequest(3));
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var invalidUpdate = await client.PutAsJsonAsync($"/api/cart/{itemId}", new CartUpdateRequest(6));
        Assert.Equal(HttpStatusCode.BadRequest, invalidUpdate.StatusCode);

        var quantity = await Scenario.ExecuteDbAsync(async db => await db.CartItems.AsNoTracking()
            .Where(x => x.Id == itemId)
            .Select(x => x.Quantity)
            .SingleAsync());
        Assert.Equal(3, quantity);

        var delete = await client.DeleteAsync($"/api/cart/{itemId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.False(await Scenario.ExecuteDbAsync(db => db.CartItems.AnyAsync(x => x.Id == itemId)));

        var otherProduct = await Scenario.SeedProductAsync(seller, title: "Cart DB Product 2", stock: 3);
        await client.PostAsJsonAsync("/api/cart", new CartAddRequest(product.Id, 1));
        await client.PostAsJsonAsync("/api/cart", new CartAddRequest(otherProduct.Id, 1));

        var clear = await client.DeleteAsync("/api/cart");
        Assert.Equal(HttpStatusCode.NoContent, clear.StatusCode);
        Assert.Equal(0, await Scenario.ExecuteDbAsync(db => db.CartItems.CountAsync(x => x.BuyerUserId == buyer.UserId)));
    }

    [Fact]
    public async Task CategoryMutations_PersistChanges_AndProtectParentCategories()
    {
        var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "category-db-admin");
        using var client = await Scenario.CreateAuthenticatedClientAsync(admin);

        var invalidCreate = await client.PostAsJsonAsync("/api/categories", new CategoryCreateRequest(" ", null));
        Assert.Equal(HttpStatusCode.BadRequest, invalidCreate.StatusCode);
        Assert.Equal(0, await Scenario.ExecuteDbAsync(db => db.Categories.CountAsync()));

        var create = await client.PostAsJsonAsync("/api/categories", new CategoryCreateRequest("Root Category", null));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var rootId = await ReadGuidAsync(create);

        var childCreate = await client.PostAsJsonAsync("/api/categories", new CategoryCreateRequest("Child Category", rootId));
        Assert.Equal(HttpStatusCode.OK, childCreate.StatusCode);
        var childId = await ReadGuidAsync(childCreate);

        var blockedDelete = await client.DeleteAsync($"/api/categories/{rootId}");
        Assert.Equal(HttpStatusCode.BadRequest, blockedDelete.StatusCode);
        Assert.True(await Scenario.ExecuteDbAsync(db => db.Categories.AnyAsync(x => x.Id == rootId)));

        var update = await client.PutAsJsonAsync($"/api/categories/{rootId}", new CategoryUpdateRequest("Updated Root Category", null));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("Updated Root Category", await Scenario.ExecuteDbAsync(async db => await db.Categories.AsNoTracking().Where(x => x.Id == rootId).Select(x => x.Name).SingleAsync()));

        var deleteChild = await client.DeleteAsync($"/api/categories/{childId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteChild.StatusCode);
        Assert.False(await Scenario.ExecuteDbAsync(db => db.Categories.AnyAsync(x => x.Id == childId)));

        var deleteRoot = await client.DeleteAsync($"/api/categories/{rootId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteRoot.StatusCode);
        Assert.False(await Scenario.ExecuteDbAsync(db => db.Categories.AnyAsync(x => x.Id == rootId)));
    }

    [Fact]
    public async Task ProductMutations_PersistChanges_AndRejectInvalidPayloads()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "product-db-seller");
        var category = await Scenario.SeedCategoryAsync("Product DB Category");
        var otherCategory = await Scenario.SeedCategoryAsync("Product DB Category 2");
        using var client = await Scenario.CreateAuthenticatedClientAsync(seller);

        var invalidCreate = await client.PostAsJsonAsync("/api/products", new ProductCreateRequest(category.Id, " ", "short", -1m, -1, true));
        Assert.Equal(HttpStatusCode.BadRequest, invalidCreate.StatusCode);
        Assert.Equal(0, await Scenario.ExecuteDbAsync(db => db.Products.CountAsync(x => x.StoreId == seller.StoreId)));

        var create = await client.PostAsJsonAsync("/api/products", new ProductCreateRequest(category.Id, "Created Product", "Valid description for product create", 55m, 3, true));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<ProductDetails>();
        Assert.NotNull(created);

        var persisted = await Scenario.ExecuteDbAsync(db => db.Products.AsNoTracking().SingleAsync(x => x.Id == created.Id));
        Assert.Equal(ProductStatus.Pending, persisted.Status);
        Assert.False(persisted.IsPublished);

        var invalidUpdate = await client.PutAsJsonAsync($"/api/products/{created.Id}", new ProductUpdateRequest(otherCategory.Id, " ", "short", 0m, -1, true));
        Assert.Equal(HttpStatusCode.BadRequest, invalidUpdate.StatusCode);

        var update = await client.PutAsJsonAsync($"/api/products/{created.Id}", new ProductUpdateRequest(otherCategory.Id, "Updated Product", "Updated description for persistence checks", 60m, 4, true));
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var updated = await Scenario.ExecuteDbAsync(db => db.Products.AsNoTracking().SingleAsync(x => x.Id == created.Id));
        Assert.Equal(otherCategory.Id, updated.CategoryId);
        Assert.Equal("Updated Product", updated.Title);
        Assert.Equal(4, updated.Stock);
        Assert.False(updated.IsPublished);

        using var upload = TestScenarioBuilder.CreateImageUpload();
        var uploadResponse = await client.PostAsync($"/api/products/{created.Id}/images?sortOrder=1", upload);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

        var image = await Scenario.ExecuteDbAsync(async db => await db.ProductImages.AsNoTracking().SingleAsync(x => x.ProductId == created.Id));
        Assert.Equal(1, image.SortOrder);

        var deleteImage = await client.DeleteAsync($"/api/products/images/{image.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteImage.StatusCode);
        Assert.False(await Scenario.ExecuteDbAsync(db => db.ProductImages.AnyAsync(x => x.Id == image.Id)));

        var deleteProduct = await client.DeleteAsync($"/api/products/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteProduct.StatusCode);
        Assert.False(await Scenario.ExecuteDbAsync(db => db.Products.AnyAsync(x => x.Id == created.Id)));
    }

    [Fact]
    public async Task AdminProductModeration_PersistsApprovalAndRejection()
    {
        var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "moderation-db-admin");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "moderation-db-seller");
        var approveProduct = await Scenario.SeedProductAsync(seller, title: "Approve Me", isPublished: false, status: ProductStatus.Pending);
        var rejectProduct = await Scenario.SeedProductAsync(seller, title: "Reject Me", isPublished: false, status: ProductStatus.Pending);
        using var client = await Scenario.CreateAuthenticatedClientAsync(admin);

        var approve = await client.PostAsync($"/api/admin/products/{approveProduct.Id}/approve", content: null);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var approved = await Scenario.ExecuteDbAsync(db => db.Products.AsNoTracking().SingleAsync(x => x.Id == approveProduct.Id));
        Assert.Equal(ProductStatus.Approved, approved.Status);
        Assert.True(approved.IsPublished);
        Assert.Equal(admin.UserId, approved.ApprovedByUserId);
        Assert.NotNull(approved.ApprovedAtUtc);

        var reject = await client.PostAsJsonAsync($"/api/admin/products/{rejectProduct.Id}/reject", new ProductRejectRequest("Not compliant"));
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);

        var rejected = await Scenario.ExecuteDbAsync(db => db.Products.AsNoTracking().SingleAsync(x => x.Id == rejectProduct.Id));
        Assert.Equal(ProductStatus.Rejected, rejected.Status);
        Assert.Equal(admin.UserId, rejected.ApprovedByUserId);
        Assert.NotNull(rejected.ApprovedAtUtc);
    }

    [Fact]
    public async Task OrderAndReviewMutations_PersistLifecycleChanges()
    {
        var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "order-db-admin");
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "order-db-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "order-db-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Order Lifecycle Product", stock: 6, price: 25m);

        using var buyerClient = await Scenario.CreateAuthenticatedClientAsync(buyer);
        using var sellerClient = await Scenario.CreateAuthenticatedClientAsync(seller);
        using var adminClient = await Scenario.CreateAuthenticatedClientAsync(admin);

        await Scenario.SeedCartItemAsync(buyer, product, 2);
        var checkout = await buyerClient.PostAsJsonAsync("/api/orders/checkout", new CheckoutRequest("Buyer", "+10000000000", "City", "Address 1", "Comment"));
        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);
        var order = await checkout.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);

        var afterCheckout = await Scenario.ExecuteDbAsync(async db => new
        {
            OrderStatus = await db.Orders.AsNoTracking().Where(x => x.Id == order.Id).Select(x => x.Status).SingleAsync(),
            OrderItems = await db.OrderItems.CountAsync(x => x.OrderId == order.Id),
            CartCount = await db.CartItems.CountAsync(x => x.BuyerUserId == buyer.UserId),
            Stock = await db.Products.Where(x => x.Id == product.Id).Select(x => x.Stock).SingleAsync()
        });

        Assert.Equal(OrderStatus.Pending, afterCheckout.OrderStatus);
        Assert.Equal(1, afterCheckout.OrderItems);
        Assert.Equal(0, afterCheckout.CartCount);
        Assert.Equal(4, afterCheckout.Stock);

        var confirm = await sellerClient.PutAsync($"/api/orders/{order.Id}/status?status=Confirmed", content: null);
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);
        Assert.Equal(OrderStatus.Confirmed, await GetOrderStatusAsync(order.Id));

        var ship = await sellerClient.PostAsJsonAsync($"/api/orders/{order.Id}/ship", new OrderShipRequest("TRACK-123"));
        Assert.Equal(HttpStatusCode.OK, ship.StatusCode);
        var afterShip = await Scenario.ExecuteDbAsync(async db => await db.Orders.AsNoTracking()
            .Where(x => x.Id == order.Id)
            .Select(x => new { x.Status, x.TrackingNumber })
            .SingleAsync());
        Assert.Equal(OrderStatus.Shipped, afterShip.Status);
        Assert.Equal("TRACK-123", afterShip.TrackingNumber);

        var requestReturn = await buyerClient.PostAsync($"/api/orders/{order.Id}/return", content: null);
        Assert.Equal(HttpStatusCode.OK, requestReturn.StatusCode);
        Assert.Equal(OrderStatus.ReturnRequested, await GetOrderStatusAsync(order.Id));

        var approveReturn = await sellerClient.PostAsync($"/api/orders/{order.Id}/return/approve", content: null);
        Assert.Equal(HttpStatusCode.OK, approveReturn.StatusCode);
        Assert.Equal(OrderStatus.ReturnApproved, await GetOrderStatusAsync(order.Id));

        var refund = await adminClient.PostAsync($"/api/orders/{order.Id}/refund", content: null);
        Assert.Equal(HttpStatusCode.OK, refund.StatusCode);
        Assert.Equal(OrderStatus.Refunded, await GetOrderStatusAsync(order.Id));

        var cancelProduct = await Scenario.SeedProductAsync(seller, title: "Cancel Product", stock: 3, price: 15m);
        var cancelOrder = await Scenario.SeedOrderAsync(buyer, cancelProduct, quantity: 1, status: OrderStatus.Pending);
        var cancel = await buyerClient.PostAsync($"/api/orders/{cancelOrder.Id}/cancel", content: null);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(OrderStatus.Cancelled, await GetOrderStatusAsync(cancelOrder.Id));
        Assert.Equal(4, await Scenario.ExecuteDbAsync(async db => await db.Products.AsNoTracking().Where(x => x.Id == cancelProduct.Id).Select(x => x.Stock).SingleAsync()));

        var rejectOrder = await Scenario.SeedOrderAsync(buyer, product, quantity: 1, status: OrderStatus.ReturnRequested);
        var rejectReturn = await sellerClient.PostAsync($"/api/orders/{rejectOrder.Id}/return/reject", content: null);
        Assert.Equal(HttpStatusCode.OK, rejectReturn.StatusCode);
        Assert.Equal(OrderStatus.ReturnRejected, await GetOrderStatusAsync(rejectOrder.Id));

        var reviewProduct = await Scenario.SeedProductAsync(seller, title: "Review Product");
        await Scenario.SeedOrderAsync(buyer, reviewProduct, status: OrderStatus.Completed);

        var createReview = await buyerClient.PostAsJsonAsync("/api/reviews", new ReviewCreateRequest(reviewProduct.Id, 5, "Excellent"));
        Assert.Equal(HttpStatusCode.NoContent, createReview.StatusCode);
        var reviewId = await Scenario.ExecuteDbAsync(async db => await db.Reviews.AsNoTracking()
            .Where(x => x.ProductId == reviewProduct.Id && x.BuyerUserId == buyer.UserId)
            .Select(x => x.Id)
            .SingleAsync());

        var deleteReview = await buyerClient.DeleteAsync($"/api/reviews/{reviewId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteReview.StatusCode);
        Assert.False(await Scenario.ExecuteDbAsync(db => db.Reviews.AnyAsync(x => x.Id == reviewId)));
    }

    [Fact]
    public async Task StoreAndRoleMutations_PersistChanges_AndRejectInvalidPayloads()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: false, emailPrefix: "store-db-seller");
        var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "role-db-admin");
        var target = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "role-db-target");

        using var sellerClient = await Scenario.CreateAuthenticatedClientAsync(seller);
        using var adminClient = await Scenario.CreateAuthenticatedClientAsync(admin);

        var invalidCreate = await sellerClient.PostAsJsonAsync("/api/stores", new StoreCreateRequest(" ", "Desc", "store@test.local", "+10000000000", "Region", "City", "Street", "10000"));
        Assert.Equal(HttpStatusCode.BadRequest, invalidCreate.StatusCode);
        Assert.False(await Scenario.ExecuteDbAsync(db => db.Stores.AnyAsync(x => x.OwnerUserId == seller.UserId)));

        var create = await sellerClient.PostAsJsonAsync("/api/stores", new StoreCreateRequest("New Store", "Desc", "store@test.local", "+10000000000", "Region", "City", "Street", "10000"));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var storeId = await ReadGuidAsync(create);

        var invalidUpdate = await sellerClient.PutAsJsonAsync("/api/stores/me", new StoreProfile(storeId, " ", "ignored", "Updated description"));
        Assert.Equal(HttpStatusCode.BadRequest, invalidUpdate.StatusCode);

        var update = await sellerClient.PutAsJsonAsync("/api/stores/me", new StoreProfile(storeId, "Updated Store", "ignored", "Updated description"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var store = await Scenario.ExecuteDbAsync(async db => await db.Stores.AsNoTracking().SingleAsync(x => x.Id == storeId));
        Assert.Equal("Updated Store", store.Name);
        Assert.Equal("Updated description", store.Description);

        var setRole = await adminClient.PostAsync($"/api/admin/set-role?userId={target.UserId}&role=Seller", content: null);
        Assert.Equal(HttpStatusCode.NoContent, setRole.StatusCode);

        var role = await Scenario.ExecuteDbAsync(async db => await db.UserRoles
            .Where(x => x.UserId == target.UserId)
            .Join(db.Roles, userRole => userRole.RoleId, dbRole => dbRole.Id, (_, dbRole) => dbRole.Name)
            .SingleAsync());
        Assert.Equal(UserRole.Seller, role);
    }

    [Fact]
    public async Task WishlistChatAndNotificationMutations_PersistChanges_AndRejectInvalidPayloads()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "wishlist-db-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "wishlist-db-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Wishlist DB Product");
        using var buyerClient = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var invalidWishlist = await buyerClient.PostAsJsonAsync("/api/wishlists", new WishlistCreateRequest(" "));
        Assert.Equal(HttpStatusCode.BadRequest, invalidWishlist.StatusCode);
        Assert.Equal(0, await Scenario.ExecuteDbAsync(db => db.Wishlists.CountAsync(x => x.UserId == buyer.UserId)));

        var createWishlist = await buyerClient.PostAsJsonAsync("/api/wishlists", new WishlistCreateRequest("Summer"));
        Assert.Equal(HttpStatusCode.OK, createWishlist.StatusCode);
        var wishlistId = await ReadGuidAsync(createWishlist);

        var invalidWishlistItem = await buyerClient.PostAsJsonAsync($"/api/wishlists/{wishlistId}/items", new ProductRefRequest(Guid.Empty));
        Assert.Equal(HttpStatusCode.BadRequest, invalidWishlistItem.StatusCode);
        Assert.Equal(0, await Scenario.ExecuteDbAsync(db => db.WishlistItems.CountAsync(x => x.WishlistId == wishlistId)));

        var addWishlistItem = await buyerClient.PostAsJsonAsync($"/api/wishlists/{wishlistId}/items", new ProductRefRequest(product.Id));
        Assert.Equal(HttpStatusCode.NoContent, addWishlistItem.StatusCode);
        Assert.True(await Scenario.ExecuteDbAsync(db => db.WishlistItems.AnyAsync(x => x.WishlistId == wishlistId && x.ProductId == product.Id)));

        var deleteWishlistItem = await buyerClient.DeleteAsync($"/api/wishlists/{wishlistId}/items/{product.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteWishlistItem.StatusCode);
        Assert.False(await Scenario.ExecuteDbAsync(db => db.WishlistItems.AnyAsync(x => x.WishlistId == wishlistId && x.ProductId == product.Id)));

        var deleteWishlist = await buyerClient.DeleteAsync($"/api/wishlists/{wishlistId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteWishlist.StatusCode);
        Assert.False(await Scenario.ExecuteDbAsync(db => db.Wishlists.AnyAsync(x => x.Id == wishlistId)));

        var selfChat = await buyerClient.PostAsJsonAsync("/api/chats", new CreateChatRequest(buyer.UserId, null));
        Assert.Equal(HttpStatusCode.BadRequest, selfChat.StatusCode);
        Assert.Equal(0, await Scenario.ExecuteDbAsync(db => db.Chats.CountAsync(x => x.BuyerId == buyer.UserId)));

        var createChat = await buyerClient.PostAsJsonAsync("/api/chats", new CreateChatRequest(seller.UserId, seller.StoreId));
        Assert.Equal(HttpStatusCode.OK, createChat.StatusCode);
        var chatId = await ReadGuidAsync(createChat);

        var invalidMessage = await buyerClient.PostAsJsonAsync($"/api/chats/{chatId}/messages", new CreateMessageRequest(" "));
        Assert.Equal(HttpStatusCode.BadRequest, invalidMessage.StatusCode);
        Assert.Equal(0, await Scenario.ExecuteDbAsync(db => db.Messages.CountAsync(x => x.ChatId == chatId)));

        var createMessage = await buyerClient.PostAsJsonAsync($"/api/chats/{chatId}/messages", new CreateMessageRequest("Hello there"));
        Assert.Equal(HttpStatusCode.NoContent, createMessage.StatusCode);
        Assert.True(await Scenario.ExecuteDbAsync(db => db.Messages.AnyAsync(x => x.ChatId == chatId && x.SenderId == buyer.UserId && x.MessageText == "Hello there")));

        var sellerMessage = await Scenario.SeedMessageAsync(new TestChat(chatId, buyer.UserId, seller.UserId, seller.StoreId), seller, "Reply");
        var readMessage = await buyerClient.PostAsync($"/api/chats/{chatId}/messages/{sellerMessage.Id}/read", content: null);
        Assert.Equal(HttpStatusCode.NoContent, readMessage.StatusCode);
        Assert.True(await Scenario.ExecuteDbAsync(db => db.Messages.AsNoTracking().Where(x => x.Id == sellerMessage.Id).Select(x => x.IsRead).SingleAsync()));

        var notification = await Scenario.SeedNotificationAsync(buyer, isRead: false);
        var markNotificationRead = await buyerClient.PostAsync($"/api/notifications/{notification.Id}/read", content: null);
        Assert.Equal(HttpStatusCode.NoContent, markNotificationRead.StatusCode);
        Assert.True(await Scenario.ExecuteDbAsync(db => db.Notifications.AsNoTracking().Where(x => x.Id == notification.Id).Select(x => x.IsRead).SingleAsync()));
    }

    [Fact]
    public async Task ShippingCouponSellerRequestAndCompanyMutations_PersistChanges_AndRejectInvalidPayloads()
    {
        var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "extras-db-admin");
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "extras-db-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "extras-db-seller");

        using var adminClient = await Scenario.CreateAuthenticatedClientAsync(admin);
        using var buyerClient = await Scenario.CreateAuthenticatedClientAsync(buyer);
        using var sellerClient = await Scenario.CreateAuthenticatedClientAsync(seller);

        var invalidShipping = await adminClient.PostAsJsonAsync("/api/shipping-methods", new CreateShippingMethodRequest(ShippingMethodType.Courier, -1m, 2));
        Assert.Equal(HttpStatusCode.BadRequest, invalidShipping.StatusCode);
        Assert.Equal(0, await Scenario.ExecuteDbAsync(db => db.ShippingMethods.CountAsync()));

        var createShipping = await adminClient.PostAsJsonAsync("/api/shipping-methods", new CreateShippingMethodRequest(ShippingMethodType.Courier, 50m, 2));
        Assert.Equal(HttpStatusCode.OK, createShipping.StatusCode);
        Assert.True(await Scenario.ExecuteDbAsync(db => db.ShippingMethods.AnyAsync(x => x.Name == ShippingMethodType.Courier && x.Price == 50m)));

        var invalidCoupon = await adminClient.PostAsJsonAsync("/api/coupons", new CreateCouponRequest(" ", 10m, DiscountType.Fixed, null, null));
        Assert.Equal(HttpStatusCode.BadRequest, invalidCoupon.StatusCode);
        Assert.Equal(0, await Scenario.ExecuteDbAsync(db => db.Coupons.CountAsync()));

        var createCoupon = await adminClient.PostAsJsonAsync("/api/coupons", new CreateCouponRequest("save10", 10m, DiscountType.Fixed, null, null));
        Assert.Equal(HttpStatusCode.OK, createCoupon.StatusCode);
        Assert.True(await Scenario.ExecuteDbAsync(db => db.Coupons.AnyAsync(x => x.Code == "SAVE10")));

        var invalidSellerRequest = await buyerClient.PostAsJsonAsync("/api/seller-requests", new CreateSellerRequestBody(null, " "));
        Assert.Equal(HttpStatusCode.BadRequest, invalidSellerRequest.StatusCode);
        Assert.Equal(0, await Scenario.ExecuteDbAsync(db => db.SellerRequests.CountAsync(x => x.UserId == buyer.UserId)));

        var createSellerRequest = await buyerClient.PostAsJsonAsync("/api/seller-requests", new CreateSellerRequestBody(null, "Please approve seller account"));
        Assert.Equal(HttpStatusCode.NoContent, createSellerRequest.StatusCode);
        var sellerRequestId = await Scenario.ExecuteDbAsync(async db => await db.SellerRequests.AsNoTracking()
            .Where(x => x.UserId == buyer.UserId)
            .Select(x => x.Id)
            .SingleAsync());

        var approveSellerRequest = await adminClient.PostAsync($"/api/admin/seller-requests/{sellerRequestId}/approve", content: null);
        Assert.Equal(HttpStatusCode.NoContent, approveSellerRequest.StatusCode);

        var approvedRequest = await Scenario.ExecuteDbAsync(async db => await db.SellerRequests.AsNoTracking().SingleAsync(x => x.Id == sellerRequestId));
        Assert.Equal(SellerRequestStatus.Approved, approvedRequest.Status);
        Assert.Equal(admin.UserId, approvedRequest.ApprovedByUserId);

        var secondApproval = await adminClient.PostAsync($"/api/admin/seller-requests/{sellerRequestId}/approve", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, secondApproval.StatusCode);

        var invalidFinance = await sellerClient.PutAsJsonAsync("/api/company/me/finance", new UpsertCompanyFinanceRequest("UA123", " ", "300001", "1234567890", "Details"));
        Assert.Equal(HttpStatusCode.BadRequest, invalidFinance.StatusCode);
        Assert.False(await Scenario.ExecuteDbAsync(db => db.CompanyFinances.AnyAsync(x => x.StoreId == seller.StoreId)));

        var createFinance = await sellerClient.PutAsJsonAsync("/api/company/me/finance", new UpsertCompanyFinanceRequest("UA123", "Test Bank", "300001", "1234567890", "Details"));
        Assert.Equal(HttpStatusCode.NoContent, createFinance.StatusCode);

        var updateFinance = await sellerClient.PutAsJsonAsync("/api/company/me/finance", new UpsertCompanyFinanceRequest("UA999", "Updated Bank", "300002", "9999999999", "Updated details"));
        Assert.Equal(HttpStatusCode.NoContent, updateFinance.StatusCode);

        var finance = await Scenario.ExecuteDbAsync(async db => await db.CompanyFinances.AsNoTracking().SingleAsync(x => x.StoreId == seller.StoreId));
        Assert.Equal("UA999", finance.BankAccount);
        Assert.Equal("Updated Bank", finance.BankName);

        var invalidSchedules = await sellerClient.PutAsJsonAsync("/api/company/me/schedules", new[]
        {
            new UpsertCompanyScheduleRequest(DayOfWeek.Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(18), false),
            new UpsertCompanyScheduleRequest(DayOfWeek.Monday, TimeSpan.FromHours(10), TimeSpan.FromHours(19), false)
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidSchedules.StatusCode);
        Assert.Equal(0, await Scenario.ExecuteDbAsync(db => db.CompanySchedules.CountAsync(x => x.StoreId == seller.StoreId)));

        await Scenario.SeedCompanySchedulesAsync(seller, new UpsertScheduleSeed(DayOfWeek.Sunday, null, null, true));
        var updateSchedules = await sellerClient.PutAsJsonAsync("/api/company/me/schedules", new[]
        {
            new UpsertCompanyScheduleRequest(DayOfWeek.Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(18), false),
            new UpsertCompanyScheduleRequest(DayOfWeek.Tuesday, null, null, true)
        });
        Assert.Equal(HttpStatusCode.NoContent, updateSchedules.StatusCode);

        var schedules = await Scenario.ExecuteDbAsync(async db => await db.CompanySchedules.AsNoTracking()
            .Where(x => x.StoreId == seller.StoreId)
            .OrderBy(x => x.Day)
            .Select(x => new { x.Day, x.IsClosed, x.OpenTime, x.CloseTime })
            .ToListAsync());

        Assert.Equal(2, schedules.Count);
        Assert.DoesNotContain(schedules, x => x.Day == DayOfWeek.Sunday);
        Assert.Contains(schedules, x => x.Day == DayOfWeek.Monday && !x.IsClosed && x.OpenTime == TimeSpan.FromHours(9));
        Assert.Contains(schedules, x => x.Day == DayOfWeek.Tuesday && x.IsClosed);
    }

    [Fact]
    public async Task PaymentMutations_PersistChanges_AndRejectInvalidPayloads()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "payment-db-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "payment-db-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Payment Product", price: 75m);
        var order = await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.Pending);

        using var buyerClient = await Scenario.CreateAuthenticatedClientAsync(buyer);
        using var callbackClient = Factory.CreateClient();

        var invalidPayment = await buyerClient.PostAsJsonAsync($"/api/orders/{order.Id}/payments", new CreatePaymentRequest(PaymentMethod.Card, -5m));
        Assert.Equal(HttpStatusCode.BadRequest, invalidPayment.StatusCode);
        Assert.Equal(0, await Scenario.ExecuteDbAsync(db => db.Payments.CountAsync(x => x.OrderId == order.Id)));

        var createPayment = await buyerClient.PostAsJsonAsync($"/api/orders/{order.Id}/payments", new CreatePaymentRequest(PaymentMethod.Card, order.Total, PaymentStatus.Pending, "CARD-REF-1"));
        Assert.Equal(HttpStatusCode.OK, createPayment.StatusCode);
        Assert.True(await Scenario.ExecuteDbAsync(db => db.Payments.AnyAsync(x => x.OrderId == order.Id && x.ExternalReference == "CARD-REF-1" && x.PaymentMethod == PaymentMethod.Card)));

        var checkout = await buyerClient.PostAsync($"/api/orders/{order.Id}/payments/liqpay/checkout", content: null);
        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);

        var liqPayPayment = await Scenario.ExecuteDbAsync(async db => await db.Payments.AsNoTracking()
            .Where(x => x.OrderId == order.Id && x.PaymentMethod == PaymentMethod.LiqPay)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new { x.Id, x.ExternalReference, x.Status })
            .FirstAsync());

        Assert.Equal(PaymentStatus.Pending, liqPayPayment.Status);

        var invalidCallback = await callbackClient.PostAsync("/api/payments/liqpay/callback", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["data"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { order_id = liqPayPayment.ExternalReference, status = "success" }))),
            ["signature"] = "bad-signature"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, invalidCallback.StatusCode);
        Assert.Equal(PaymentStatus.Pending, await Scenario.ExecuteDbAsync(async db => await db.Payments.AsNoTracking().Where(x => x.Id == liqPayPayment.Id).Select(x => x.Status).SingleAsync()));

        var validCallback = await callbackClient.PostAsync("/api/payments/liqpay/callback", CreateLiqPayCallback(liqPayPayment.ExternalReference, "success", "TRX-123"));
        Assert.Equal(HttpStatusCode.OK, validCallback.StatusCode);

        var state = await Scenario.ExecuteDbAsync(async db => new
        {
            PaymentStatus = await db.Payments.AsNoTracking().Where(x => x.Id == liqPayPayment.Id).Select(x => x.Status).SingleAsync(),
            OrderStatus = await db.Orders.AsNoTracking().Where(x => x.Id == order.Id).Select(x => x.Status).SingleAsync()
        });

        Assert.Equal(PaymentStatus.Completed, state.PaymentStatus);
        Assert.Equal(OrderStatus.Confirmed, state.OrderStatus);
    }

    private Task<OrderStatus> GetOrderStatusAsync(Guid orderId)
    {
        return Scenario.ExecuteDbAsync(async db => await db.Orders.AsNoTracking()
            .Where(x => x.Id == orderId)
            .Select(x => x.Status)
            .SingleAsync());
    }

    private static async Task<Guid> ReadGuidAsync(HttpResponseMessage response, string propertyName = "id")
    {
        using var payload = await response.ReadJsonAsync();
        return payload.RootElement.GetProperty(propertyName).GetGuid();
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
