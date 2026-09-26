using System.Net;
using System.Net.Http.Json;
using Marketplace.Application.DTO;
using Marketplace.Core.Enums;
using Marketplace.IntegrationTests.Infrastructure;
using Xunit;

namespace Marketplace.IntegrationTests.Coverage;

public sealed class OrderReviewStoreAdminEndpointsTests : IntegrationTestBase
{
    public OrderReviewStoreAdminEndpointsTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Theory]
    [InlineData("orders-checkout")]
    [InlineData("orders-my")]
    [InlineData("orders-get")]
    [InlineData("orders-seller")]
    [InlineData("orders-seller-stats")]
    [InlineData("orders-status")]
    [InlineData("orders-cancel")]
    [InlineData("orders-ship")]
    [InlineData("orders-return")]
    [InlineData("orders-return-approve")]
    [InlineData("orders-return-reject")]
    [InlineData("orders-refund")]
    [InlineData("reviews-product-get")]
    [InlineData("reviews-post")]
    [InlineData("reviews-delete")]
    [InlineData("stores-get")]
    [InlineData("stores-get-slug")]
    [InlineData("stores-post")]
    [InlineData("stores-put")]
    [InlineData("admin-stats")]
    [InlineData("admin-users")]
    [InlineData("admin-set-role")]
    public async Task SuccessCases(string endpointId)
    {
        switch (endpointId)
        {
            case "orders-checkout":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "checkout-success-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "checkout-success-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Checkout Product", stock: 5);
                await Scenario.SeedCartItemAsync(buyer, product, 2);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.PostAsJsonAsync("/api/orders/checkout", new CheckoutRequest("Buyer", "+10000000000", "City", "Address", "Comment"));
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "orders-my":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "orders-my-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "orders-my-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Orders My Product");
                await Scenario.SeedOrderAsync(buyer, product);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.GetAsync("/api/orders/my");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "orders-get":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "orders-get-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "orders-get-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Orders Get Product");
                var order = await Scenario.SeedOrderAsync(buyer, product);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.GetAsync($"/api/orders/{order.Id}");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "orders-seller":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "orders-seller-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "orders-seller-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Orders Seller Product");
                await Scenario.SeedOrderAsync(buyer, product);
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.GetAsync("/api/orders/seller");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "orders-seller-stats":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "orders-stats-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "orders-stats-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Orders Stats Product");
                await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.Completed);
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.GetAsync("/api/orders/seller/stats");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "orders-status":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "orders-status-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "orders-status-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Orders Status Product");
                var order = await Scenario.SeedOrderAsync(buyer, product);
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.PutAsync($"/api/orders/{order.Id}/status?status=Confirmed", content: null);
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                break;
            }
            case "orders-cancel":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "orders-cancel-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "orders-cancel-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Orders Cancel Product");
                var order = await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.Pending);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.PostAsync($"/api/orders/{order.Id}/cancel", content: null);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "orders-ship":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "orders-ship-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "orders-ship-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Orders Ship Product");
                var order = await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.Confirmed);
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.PostAsJsonAsync($"/api/orders/{order.Id}/ship", new OrderShipRequest("TRACK123"));
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "orders-return":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "orders-return-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "orders-return-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Orders Return Product");
                var order = await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.Shipped);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.PostAsync($"/api/orders/{order.Id}/return", content: null);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "orders-return-approve":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "orders-return-approve-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "orders-return-approve-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Orders Return Approve Product");
                var order = await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.ReturnRequested);
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.PostAsync($"/api/orders/{order.Id}/return/approve", content: null);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "orders-return-reject":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "orders-return-reject-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "orders-return-reject-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Orders Return Reject Product");
                var order = await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.ReturnRequested);
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.PostAsync($"/api/orders/{order.Id}/return/reject", content: null);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "orders-refund":
            {
                var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "orders-refund-admin");
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "orders-refund-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "orders-refund-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Orders Refund Product");
                var order = await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.ReturnApproved);
                using var client = await Scenario.CreateAuthenticatedClientAsync(admin);
                var response = await client.PostAsync($"/api/orders/{order.Id}/refund", content: null);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "reviews-product-get":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "reviews-get-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "reviews-get-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Reviews Get Product");
                await Scenario.SeedReviewAsync(buyer, product);
                using var client = Factory.CreateClient();
                var response = await client.GetAsync($"/api/reviews/product/{product.Id}");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "reviews-post":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "reviews-post-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "reviews-post-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Reviews Post Product");
                await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.Completed);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.PostAsJsonAsync("/api/reviews", new ReviewCreateRequest(product.Id, 5, "Excellent"));
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                break;
            }
            case "reviews-delete":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "reviews-delete-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "reviews-delete-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Reviews Delete Product");
                var review = await Scenario.SeedReviewAsync(buyer, product);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.DeleteAsync($"/api/reviews/{review.Id}");
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                break;
            }
            case "stores-get":
            {
                await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "stores-get-seller");
                using var client = Factory.CreateClient();
                var response = await client.GetAsync("/api/stores");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "stores-get-slug":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "stores-slug-seller");
                using var client = Factory.CreateClient();
                var response = await client.GetAsync($"/api/stores/{seller.StoreSlug}");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "stores-post":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: false, emailPrefix: "stores-post-seller");
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.PostAsJsonAsync("/api/stores", new StoreCreateRequest("New Store", "Desc", "store@test.local", "+10000000000", "Region", "City", "Street", "10000"));
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                break;
            }
            case "stores-put":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "stores-put-seller");
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.PutAsJsonAsync("/api/stores/me", new StoreProfile(seller.StoreId!.Value, "Updated Store", seller.StoreSlug!, "Updated description"));
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "admin-stats":
            {
                var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "admin-stats-admin");
                using var client = await Scenario.CreateAuthenticatedClientAsync(admin);
                var response = await client.GetAsync("/api/admin/stats");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "admin-users":
            {
                var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "admin-users-admin");
                await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "admin-users-target");
                using var client = await Scenario.CreateAuthenticatedClientAsync(admin);
                var response = await client.GetAsync("/api/admin/users");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "admin-set-role":
            {
                var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "admin-role-admin");
                var target = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "admin-role-target");
                using var client = await Scenario.CreateAuthenticatedClientAsync(admin);
                var response = await client.PostAsync($"/api/admin/set-role?userId={target.UserId}&role=Seller", content: null);
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                break;
            }
            default:
                throw new NotSupportedException(endpointId);
        }
    }

    [Theory]
    [InlineData("orders-checkout")]
    [InlineData("orders-my")]
    [InlineData("orders-get")]
    [InlineData("orders-seller")]
    [InlineData("orders-seller-stats")]
    [InlineData("orders-status")]
    [InlineData("orders-cancel")]
    [InlineData("orders-ship")]
    [InlineData("orders-return")]
    [InlineData("orders-return-approve")]
    [InlineData("orders-return-reject")]
    [InlineData("orders-refund")]
    [InlineData("reviews-product-get")]
    [InlineData("reviews-post")]
    [InlineData("reviews-delete")]
    [InlineData("stores-get")]
    [InlineData("stores-get-slug")]
    [InlineData("stores-post")]
    [InlineData("stores-put")]
    [InlineData("admin-stats")]
    [InlineData("admin-users")]
    [InlineData("admin-set-role")]
    public async Task InvalidCases(string endpointId)
    {
        switch (endpointId)
        {
            case "orders-checkout":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "checkout-invalid-buyer");
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.PostAsJsonAsync("/api/orders/checkout", new CheckoutRequest("Buyer", "+10000000000", "City", "Address", "Comment"));
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                break;
            }
            case "orders-my":
            {
                using var client = Factory.CreateClient();
                var response = await client.GetAsync("/api/orders/my");
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                break;
            }
            case "orders-get":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "orders-get-invalid-buyer");
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.GetAsync($"/api/orders/{Guid.NewGuid()}");
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                break;
            }
            case "orders-seller":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: false, emailPrefix: "orders-seller-invalid");
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.GetAsync("/api/orders/seller");
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                break;
            }
            case "orders-seller-stats":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: false, emailPrefix: "orders-stats-invalid");
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.GetAsync("/api/orders/seller/stats");
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                break;
            }
            case "orders-status":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "orders-status-invalid-buyer");
                var seller1 = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "orders-status-invalid-seller1");
                var seller2 = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "orders-status-invalid-seller2");
                var product = await Scenario.SeedProductAsync(seller1, title: "Orders Status Invalid Product");
                var order = await Scenario.SeedOrderAsync(buyer, product);
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller2);
                var response = await client.PutAsync($"/api/orders/{order.Id}/status?status=Completed", content: null);
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                break;
            }
            case "orders-cancel":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "orders-cancel-invalid-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "orders-cancel-invalid-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Orders Cancel Invalid Product");
                var order = await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.Shipped);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.PostAsync($"/api/orders/{order.Id}/cancel", content: null);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                break;
            }
            case "orders-ship":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "orders-ship-invalid-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "orders-ship-invalid-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Orders Ship Invalid Product");
                var order = await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.Pending);
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.PostAsJsonAsync($"/api/orders/{order.Id}/ship", new OrderShipRequest("TRACK123"));
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                break;
            }
            case "orders-return":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "orders-return-invalid-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "orders-return-invalid-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Orders Return Invalid Product");
                var order = await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.Pending);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.PostAsync($"/api/orders/{order.Id}/return", content: null);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                break;
            }
            case "orders-return-approve":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "orders-return-approve-invalid-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "orders-return-approve-invalid-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Orders Return Approve Invalid Product");
                var order = await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.Confirmed);
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.PostAsync($"/api/orders/{order.Id}/return/approve", content: null);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                break;
            }
            case "orders-return-reject":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "orders-return-reject-invalid-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "orders-return-reject-invalid-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Orders Return Reject Invalid Product");
                var order = await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.Confirmed);
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.PostAsync($"/api/orders/{order.Id}/return/reject", content: null);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                break;
            }
            case "orders-refund":
            {
                var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "orders-refund-invalid-admin");
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "orders-refund-invalid-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "orders-refund-invalid-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Orders Refund Invalid Product");
                var order = await Scenario.SeedOrderAsync(buyer, product, status: OrderStatus.Confirmed);
                using var client = await Scenario.CreateAuthenticatedClientAsync(admin);
                var response = await client.PostAsync($"/api/orders/{order.Id}/refund", content: null);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                break;
            }
            case "reviews-product-get":
            {
                using var client = Factory.CreateClient();
                var response = await client.GetAsync($"/api/reviews/product/{Guid.NewGuid()}");
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                break;
            }
            case "reviews-post":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "reviews-post-invalid-buyer");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "reviews-post-invalid-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Reviews Invalid Product");
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.PostAsJsonAsync("/api/reviews", new ReviewCreateRequest(product.Id, 5, "Excellent"));
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                break;
            }
            case "reviews-delete":
            {
                var buyer1 = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "reviews-delete-invalid-buyer1");
                var buyer2 = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "reviews-delete-invalid-buyer2");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "reviews-delete-invalid-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Reviews Delete Invalid Product");
                var review = await Scenario.SeedReviewAsync(buyer1, product);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer2);
                var response = await client.DeleteAsync($"/api/reviews/{review.Id}");
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                break;
            }
            case "stores-get":
            {
                using var client = Factory.CreateClient();
                var response = await client.DeleteAsync("/api/stores");
                Assert.Contains(response.StatusCode, new[] { HttpStatusCode.MethodNotAllowed, HttpStatusCode.NotFound });
                break;
            }
            case "stores-get-slug":
            {
                using var client = Factory.CreateClient();
                var response = await client.GetAsync("/api/stores/missing-slug");
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                break;
            }
            case "stores-post":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "stores-post-invalid-seller");
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.PostAsJsonAsync("/api/stores", new StoreCreateRequest("New Store", "Desc", "store@test.local", "+10000000000", "Region", "City", "Street", "10000"));
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                break;
            }
            case "stores-put":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: false, emailPrefix: "stores-put-invalid-seller");
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.PutAsJsonAsync("/api/stores/me", new StoreProfile(Guid.NewGuid(), "Updated Store", "slug", "Updated description"));
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                break;
            }
            case "admin-stats":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "admin-stats-invalid-seller");
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.GetAsync("/api/admin/stats");
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                break;
            }
            case "admin-users":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "admin-users-invalid-seller");
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.GetAsync("/api/admin/users");
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                break;
            }
            case "admin-set-role":
            {
                var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "admin-role-invalid-admin");
                var target = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "admin-role-invalid-target");
                using var client = await Scenario.CreateAuthenticatedClientAsync(admin);
                var response = await client.PostAsync($"/api/admin/set-role?userId={target.UserId}&role=Nope", content: null);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                break;
            }
            default:
                throw new NotSupportedException(endpointId);
        }
    }
}
