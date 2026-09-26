using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Marketplace.Core.Enums;
using Marketplace.IntegrationTests.Infrastructure;
using Xunit;
using static Marketplace.API.Features.Extras.ExtrasEndpoints;

namespace Marketplace.IntegrationTests.Coverage;

public sealed class ExtrasEndpointsCoverageTests : IntegrationTestBase
{
    public ExtrasEndpointsCoverageTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Theory]
    [InlineData("shipping-get")]
    [InlineData("shipping-post")]
    [InlineData("coupons-get")]
    [InlineData("coupons-get-code")]
    [InlineData("coupons-post")]
    [InlineData("wishlists-get")]
    [InlineData("wishlists-post")]
    [InlineData("wishlists-delete")]
    [InlineData("wishlists-post-item")]
    [InlineData("wishlists-delete-item")]
    [InlineData("chats-get")]
    [InlineData("chats-post")]
    [InlineData("chats-get-messages")]
    [InlineData("chats-post-message")]
    [InlineData("chats-read")]
    [InlineData("notifications-get")]
    [InlineData("notifications-read")]
    [InlineData("seller-requests-post")]
    [InlineData("admin-seller-requests-get")]
    [InlineData("admin-seller-requests-approve")]
    [InlineData("company-finance-get")]
    [InlineData("company-finance-put")]
    [InlineData("company-schedules-get")]
    [InlineData("company-schedules-put")]
    [InlineData("payments-get")]
    [InlineData("payments-post")]
    [InlineData("payments-liqpay-checkout")]
    [InlineData("payments-liqpay-callback")]
    public async Task SuccessCases(string endpointId)
    {
        switch (endpointId)
        {
            case "shipping-get":
            {
                await Scenario.SeedShippingMethodAsync();
                using var client = Factory.CreateClient();
                var response = await client.GetAsync("/api/shipping-methods");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "shipping-post":
            {
                var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "shipping-post-admin");
                using var client = await Scenario.CreateAuthenticatedClientAsync(admin);
                var response = await client.PostAsJsonAsync("/api/shipping-methods", new CreateShippingMethodRequest(ShippingMethodType.Courier, 50m, 2));
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "coupons-get":
            {
                var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "coupons-get-admin");
                await Scenario.SeedCouponAsync("LIST10");
                using var client = await Scenario.CreateAuthenticatedClientAsync(admin);
                var response = await client.GetAsync("/api/coupons");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "coupons-get-code":
            {
                await Scenario.SeedCouponAsync("PUBLIC10");
                using var client = Factory.CreateClient();
                var response = await client.GetAsync("/api/coupons/PUBLIC10");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "coupons-post":
            {
                var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "coupons-post-admin");
                using var client = await Scenario.CreateAuthenticatedClientAsync(admin);
                var response = await client.PostAsJsonAsync("/api/coupons", new CreateCouponRequest("NEW10", 10m, DiscountType.Fixed, null, null));
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "wishlists-get":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "wishlists-get-buyer");
                await Scenario.SeedWishlistAsync(buyer);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.GetAsync("/api/wishlists");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "wishlists-post":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "wishlists-post-buyer");
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.PostAsJsonAsync("/api/wishlists", new WishlistCreateRequest("Summer"));
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "wishlists-delete":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "wishlists-delete-buyer");
                var wishlist = await Scenario.SeedWishlistAsync(buyer);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.DeleteAsync($"/api/wishlists/{wishlist.Id}");
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                break;
            }
            case "wishlists-post-item":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "wishlists-item-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "wishlists-item-seller");
                var wishlist = await Scenario.SeedWishlistAsync(buyer);
                var product = await Scenario.SeedProductAsync(seller, title: "Wishlist Product");
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.PostAsJsonAsync($"/api/wishlists/{wishlist.Id}/items", new ProductRefRequest(product.Id));
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                break;
            }
            case "wishlists-delete-item":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "wishlists-delete-item-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "wishlists-delete-item-seller");
                var wishlist = await Scenario.SeedWishlistAsync(buyer);
                var product = await Scenario.SeedProductAsync(seller, title: "Wishlist Delete Product");
                await Scenario.SeedWishlistItemAsync(wishlist, product);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.DeleteAsync($"/api/wishlists/{wishlist.Id}/items/{product.Id}");
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                break;
            }
            case "chats-get":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "chats-get-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "chats-get-seller");
                await Scenario.SeedChatAsync(buyer, seller);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.GetAsync("/api/chats");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "chats-post":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "chats-post-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "chats-post-seller");
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.PostAsJsonAsync("/api/chats", new CreateChatRequest(seller.UserId, seller.StoreId));
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "chats-get-messages":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "chats-get-messages-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "chats-get-messages-seller");
                var chat = await Scenario.SeedChatAsync(buyer, seller);
                await Scenario.SeedMessageAsync(chat, buyer);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.GetAsync($"/api/chats/{chat.Id}/messages");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "chats-post-message":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "chats-post-message-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "chats-post-message-seller");
                var chat = await Scenario.SeedChatAsync(buyer, seller);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.PostAsJsonAsync($"/api/chats/{chat.Id}/messages", new CreateMessageRequest("Hello"));
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                break;
            }
            case "chats-read":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "chats-read-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "chats-read-seller");
                var chat = await Scenario.SeedChatAsync(buyer, seller);
                var message = await Scenario.SeedMessageAsync(chat, seller);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.PostAsync($"/api/chats/{chat.Id}/messages/{message.Id}/read", content: null);
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                break;
            }
            case "notifications-get":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "notifications-get-buyer");
                await Scenario.SeedNotificationAsync(buyer);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.GetAsync("/api/notifications");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "notifications-read":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "notifications-read-buyer");
                var notification = await Scenario.SeedNotificationAsync(buyer);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.PostAsync($"/api/notifications/{notification.Id}/read", content: null);
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                break;
            }
            case "seller-requests-post":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "seller-requests-post-buyer");
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.PostAsJsonAsync("/api/seller-requests", new CreateSellerRequestBody(null, "Please approve seller account"));
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                break;
            }
            case "admin-seller-requests-get":
            {
                var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "seller-requests-get-admin");
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "seller-requests-get-buyer");
                await Scenario.SeedSellerRequestAsync(buyer);
                using var client = await Scenario.CreateAuthenticatedClientAsync(admin);
                var response = await client.GetAsync("/api/admin/seller-requests");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "admin-seller-requests-approve":
            {
                var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "seller-requests-approve-admin");
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "seller-requests-approve-buyer");
                var request = await Scenario.SeedSellerRequestAsync(buyer);
                using var client = await Scenario.CreateAuthenticatedClientAsync(admin);
                var response = await client.PostAsync($"/api/admin/seller-requests/{request.Id}/approve", content: null);
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                break;
            }
            case "company-finance-get":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "company-finance-get-seller");
                await Scenario.SeedCompanyFinanceAsync(seller);
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.GetAsync("/api/company/me/finance");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "company-finance-put":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "company-finance-put-seller");
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.PutAsJsonAsync("/api/company/me/finance", new UpsertCompanyFinanceRequest("UA123", "Test Bank", "300001", "1234567890", "Details"));
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                break;
            }
            case "company-schedules-get":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "company-schedules-get-seller");
                await Scenario.SeedCompanySchedulesAsync(seller, new UpsertScheduleSeed(DayOfWeek.Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(18), false));
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.GetAsync("/api/company/me/schedules");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "company-schedules-put":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "company-schedules-put-seller");
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.PutAsJsonAsync("/api/company/me/schedules", new[] { new UpsertCompanyScheduleRequest(DayOfWeek.Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(18), false) });
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                break;
            }
            case "payments-get":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "payments-get-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "payments-get-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Payments Get Product");
                var order = await Scenario.SeedOrderAsync(buyer, product);
                await Scenario.SeedPaymentAsync(order);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.GetAsync($"/api/orders/{order.Id}/payments");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "payments-post":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "payments-post-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "payments-post-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Payments Post Product");
                var order = await Scenario.SeedOrderAsync(buyer, product);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.PostAsJsonAsync($"/api/orders/{order.Id}/payments", new CreatePaymentRequest(PaymentMethod.Card, order.Total));
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "payments-liqpay-checkout":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "payments-liqpay-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "payments-liqpay-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Payments LiqPay Product");
                var order = await Scenario.SeedOrderAsync(buyer, product);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.PostAsync($"/api/orders/{order.Id}/payments/liqpay/checkout", content: null);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "payments-liqpay-callback":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "payments-callback-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "payments-callback-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Payments Callback Product");
                var order = await Scenario.SeedOrderAsync(buyer, product);
                var payment = await Scenario.SeedPaymentAsync(order, PaymentMethod.LiqPay, PaymentStatus.Pending, externalReference: "LP-CALLBACK-1");

                using var client = Factory.CreateClient();
                var data = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                {
                    order_id = payment.ExternalReference,
                    status = "success",
                    transaction_id = "TRX-1"
                })));
                var response = await client.PostAsync("/api/payments/liqpay/callback", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["data"] = data,
                    ["signature"] = "test-signature"
                }));
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            default:
                throw new NotSupportedException(endpointId);
        }
    }

    [Theory]
    [InlineData("shipping-get")]
    [InlineData("shipping-post")]
    [InlineData("coupons-get")]
    [InlineData("coupons-get-code")]
    [InlineData("coupons-post")]
    [InlineData("wishlists-get")]
    [InlineData("wishlists-post")]
    [InlineData("wishlists-delete")]
    [InlineData("wishlists-post-item")]
    [InlineData("wishlists-delete-item")]
    [InlineData("chats-get")]
    [InlineData("chats-post")]
    [InlineData("chats-get-messages")]
    [InlineData("chats-post-message")]
    [InlineData("chats-read")]
    [InlineData("notifications-get")]
    [InlineData("notifications-read")]
    [InlineData("seller-requests-post")]
    [InlineData("admin-seller-requests-get")]
    [InlineData("admin-seller-requests-approve")]
    [InlineData("company-finance-get")]
    [InlineData("company-finance-put")]
    [InlineData("company-schedules-get")]
    [InlineData("company-schedules-put")]
    [InlineData("payments-get")]
    [InlineData("payments-post")]
    [InlineData("payments-liqpay-checkout")]
    [InlineData("payments-liqpay-callback")]
    public async Task InvalidCases(string endpointId)
    {
        switch (endpointId)
        {
            case "shipping-get":
            {
                using var client = Factory.CreateClient();
                var response = await client.PutAsync("/api/shipping-methods", content: null);
                Assert.Contains(response.StatusCode, new[] { HttpStatusCode.MethodNotAllowed, HttpStatusCode.NotFound });
                break;
            }
            case "shipping-post":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "shipping-post-invalid-seller");
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.PostAsJsonAsync("/api/shipping-methods", new CreateShippingMethodRequest(ShippingMethodType.Courier, 50m, 2));
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                break;
            }
            case "coupons-get":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "coupons-get-invalid-seller");
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.GetAsync("/api/coupons");
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                break;
            }
            case "coupons-get-code":
            {
                using var client = Factory.CreateClient();
                var response = await client.GetAsync("/api/coupons/MISSING");
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                break;
            }
            case "coupons-post":
            {
                var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "coupons-post-invalid-admin");
                await Scenario.SeedCouponAsync("DUPLICATE10");
                using var client = await Scenario.CreateAuthenticatedClientAsync(admin);
                var response = await client.PostAsJsonAsync("/api/coupons", new CreateCouponRequest("DUPLICATE10", 10m, DiscountType.Fixed, null, null));
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                break;
            }
            case "wishlists-get":
            {
                using var client = Factory.CreateClient();
                var response = await client.GetAsync("/api/wishlists");
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                break;
            }
            case "wishlists-post":
            {
                using var client = Factory.CreateClient();
                var response = await client.PostAsJsonAsync("/api/wishlists", new WishlistCreateRequest("Summer"));
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                break;
            }
            case "wishlists-delete":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "wishlists-delete-invalid-buyer");
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.DeleteAsync($"/api/wishlists/{Guid.NewGuid()}");
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                break;
            }
            case "wishlists-post-item":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "wishlists-post-item-invalid-buyer");
                var wishlist = await Scenario.SeedWishlistAsync(buyer);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.PostAsJsonAsync($"/api/wishlists/{wishlist.Id}/items", new ProductRefRequest(Guid.NewGuid()));
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                break;
            }
            case "wishlists-delete-item":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "wishlists-delete-item-invalid-buyer");
                var wishlist = await Scenario.SeedWishlistAsync(buyer);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.DeleteAsync($"/api/wishlists/{wishlist.Id}/items/{Guid.NewGuid()}");
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                break;
            }
            case "chats-get":
            {
                using var client = Factory.CreateClient();
                var response = await client.GetAsync("/api/chats");
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                break;
            }
            case "chats-post":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "chats-post-invalid-seller");
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.PostAsJsonAsync("/api/chats", new CreateChatRequest(Guid.NewGuid(), seller.StoreId));
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                break;
            }
            case "chats-get-messages":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "chats-get-msg-invalid-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "chats-get-msg-invalid-seller");
                var intruder = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "chats-get-msg-intruder");
                var chat = await Scenario.SeedChatAsync(buyer, seller);
                using var client = await Scenario.CreateAuthenticatedClientAsync(intruder);
                var response = await client.GetAsync($"/api/chats/{chat.Id}/messages");
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                break;
            }
            case "chats-post-message":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "chats-post-msg-invalid-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "chats-post-msg-invalid-seller");
                var intruder = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "chats-post-msg-intruder");
                var chat = await Scenario.SeedChatAsync(buyer, seller);
                using var client = await Scenario.CreateAuthenticatedClientAsync(intruder);
                var response = await client.PostAsJsonAsync($"/api/chats/{chat.Id}/messages", new CreateMessageRequest("Hello"));
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                break;
            }
            case "chats-read":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "chats-read-invalid-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "chats-read-invalid-seller");
                var chat = await Scenario.SeedChatAsync(buyer, seller);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.PostAsync($"/api/chats/{chat.Id}/messages/{Guid.NewGuid()}/read", content: null);
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                break;
            }
            case "notifications-get":
            {
                using var client = Factory.CreateClient();
                var response = await client.GetAsync("/api/notifications");
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                break;
            }
            case "notifications-read":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "notifications-read-invalid-buyer");
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.PostAsync($"/api/notifications/{Guid.NewGuid()}/read", content: null);
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                break;
            }
            case "seller-requests-post":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "seller-requests-post-invalid-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "seller-requests-post-invalid-seller");
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.PostAsJsonAsync("/api/seller-requests", new CreateSellerRequestBody(seller.StoreId, "Please approve seller account"));
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                break;
            }
            case "admin-seller-requests-get":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "seller-requests-get-invalid-seller");
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.GetAsync("/api/admin/seller-requests");
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                break;
            }
            case "admin-seller-requests-approve":
            {
                var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "seller-requests-approve-invalid-admin");
                using var client = await Scenario.CreateAuthenticatedClientAsync(admin);
                var response = await client.PostAsync($"/api/admin/seller-requests/{Guid.NewGuid()}/approve", content: null);
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                break;
            }
            case "company-finance-get":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: false, emailPrefix: "company-finance-get-invalid-seller");
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.GetAsync("/api/company/me/finance");
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                break;
            }
            case "company-finance-put":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: false, emailPrefix: "company-finance-put-invalid-seller");
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.PutAsJsonAsync("/api/company/me/finance", new UpsertCompanyFinanceRequest("UA123", "Test Bank", "300001", "1234567890", "Details"));
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                break;
            }
            case "company-schedules-get":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: false, emailPrefix: "company-schedules-get-invalid-seller");
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.GetAsync("/api/company/me/schedules");
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                break;
            }
            case "company-schedules-put":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: false, emailPrefix: "company-schedules-put-invalid-seller");
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.PutAsJsonAsync("/api/company/me/schedules", new[] { new UpsertCompanyScheduleRequest(DayOfWeek.Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(18), false) });
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                break;
            }
            case "payments-get":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "payments-get-invalid-buyer");
                var otherBuyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "payments-get-invalid-other");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "payments-get-invalid-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Payments Get Invalid Product");
                var order = await Scenario.SeedOrderAsync(buyer, product);
                using var client = await Scenario.CreateAuthenticatedClientAsync(otherBuyer);
                var response = await client.GetAsync($"/api/orders/{order.Id}/payments");
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                break;
            }
            case "payments-post":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "payments-post-invalid-buyer");
                var otherSeller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "payments-post-invalid-seller");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "payments-post-invalid-owner");
                var product = await Scenario.SeedProductAsync(seller, title: "Payments Post Invalid Product");
                var order = await Scenario.SeedOrderAsync(buyer, product);
                using var client = await Scenario.CreateAuthenticatedClientAsync(otherSeller);
                var response = await client.PostAsJsonAsync($"/api/orders/{order.Id}/payments", new CreatePaymentRequest(PaymentMethod.Card, order.Total));
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                break;
            }
            case "payments-liqpay-checkout":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "payments-liqpay-invalid-buyer");
                var otherSeller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "payments-liqpay-invalid-seller");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "payments-liqpay-invalid-owner");
                var product = await Scenario.SeedProductAsync(seller, title: "Payments LiqPay Invalid Product");
                var order = await Scenario.SeedOrderAsync(buyer, product);
                using var client = await Scenario.CreateAuthenticatedClientAsync(otherSeller);
                var response = await client.PostAsync($"/api/orders/{order.Id}/payments/liqpay/checkout", content: null);
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                break;
            }
            case "payments-liqpay-callback":
            {
                using var client = Factory.CreateClient();
                var data = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { order_id = "missing", status = "success" })));
                var response = await client.PostAsync("/api/payments/liqpay/callback", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["data"] = data,
                    ["signature"] = "bad-signature"
                }));
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                break;
            }
            default:
                throw new NotSupportedException(endpointId);
        }
    }
}
