using System.Net;
using System.Net.Http.Json;
using Marketplace.Application.DTO;
using Marketplace.Core.Enums;
using Marketplace.IntegrationTests.Infrastructure;
using Xunit;

namespace Marketplace.IntegrationTests.Coverage;

public sealed class AddressCartCategoryEndpointsTests : IntegrationTestBase
{
    public AddressCartCategoryEndpointsTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Theory]
    [InlineData("addresses-get")]
    [InlineData("addresses-post")]
    [InlineData("addresses-put")]
    [InlineData("addresses-delete")]
    [InlineData("addresses-default")]
    [InlineData("cart-get")]
    [InlineData("cart-post")]
    [InlineData("cart-put")]
    [InlineData("cart-delete")]
    [InlineData("cart-clear")]
    [InlineData("categories-get")]
    [InlineData("categories-tree")]
    [InlineData("categories-post")]
    [InlineData("categories-put")]
    [InlineData("categories-delete")]
    public async Task SuccessCases(string endpointId)
    {
        switch (endpointId)
        {
            case "addresses-get":
            {
                var actor = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "addresses-get-success");
                await Scenario.SeedAddressAsync(actor, isDefault: true);
                using var client = await Scenario.CreateAuthenticatedClientAsync(actor);
                var response = await client.GetAsync("/api/addresses");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "addresses-post":
            {
                var actor = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "addresses-post-success");
                using var client = await Scenario.CreateAuthenticatedClientAsync(actor);
                var response = await client.PostAsJsonAsync("/api/addresses", new AddressCreateRequest("Home", "+10000000000", "Region", "City", "Line 1", null, "10000", true));
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                break;
            }
            case "addresses-put":
            {
                var actor = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "addresses-put-success");
                var address = await Scenario.SeedAddressAsync(actor);
                using var client = await Scenario.CreateAuthenticatedClientAsync(actor);
                var response = await client.PutAsJsonAsync($"/api/addresses/{address.Id}", new AddressUpdateRequest("Office", null, null, "Updated City", null, null, null, false));
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "addresses-delete":
            {
                var actor = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "addresses-delete-success");
                var address = await Scenario.SeedAddressAsync(actor);
                using var client = await Scenario.CreateAuthenticatedClientAsync(actor);
                var response = await client.DeleteAsync($"/api/addresses/{address.Id}");
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                break;
            }
            case "addresses-default":
            {
                var actor = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "addresses-default-success");
                var address = await Scenario.SeedAddressAsync(actor);
                using var client = await Scenario.CreateAuthenticatedClientAsync(actor);
                var response = await client.PostAsync($"/api/addresses/{address.Id}/default", content: null);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "cart-get":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "cart-get-success");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "cart-get-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Cart Get Product");
                await Scenario.SeedCartItemAsync(buyer, product, 2);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.GetAsync("/api/cart");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "cart-post":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "cart-post-success");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "cart-post-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Cart Add Product", stock: 5);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.PostAsJsonAsync("/api/cart", new CartAddRequest(product.Id, 2));
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                break;
            }
            case "cart-put":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "cart-put-success");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "cart-put-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Cart Update Product", stock: 5);
                var item = await Scenario.SeedCartItemAsync(buyer, product, 1);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.PutAsJsonAsync($"/api/cart/{item.Id}", new CartUpdateRequest(3));
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                break;
            }
            case "cart-delete":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "cart-delete-success");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "cart-delete-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Cart Delete Product");
                var item = await Scenario.SeedCartItemAsync(buyer, product, 1);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.DeleteAsync($"/api/cart/{item.Id}");
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                break;
            }
            case "cart-clear":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "cart-clear-success");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "cart-clear-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Cart Clear Product");
                await Scenario.SeedCartItemAsync(buyer, product, 1);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.DeleteAsync("/api/cart");
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                break;
            }
            case "categories-get":
            {
                await Scenario.SeedCategoryAsync("Public Category");
                using var client = Factory.CreateClient();
                var response = await client.GetAsync("/api/categories");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "categories-tree":
            {
                var parent = await Scenario.SeedCategoryAsync("Parent Category");
                await Scenario.SeedCategoryAsync("Child Category", parent.Id);
                using var client = Factory.CreateClient();
                var response = await client.GetAsync("/api/categories/tree");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "categories-post":
            {
                var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "categories-post-admin");
                using var client = await Scenario.CreateAuthenticatedClientAsync(admin);
                var response = await client.PostAsJsonAsync("/api/categories", new CategoryCreateRequest("Created Category", null));
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "categories-put":
            {
                var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "categories-put-admin");
                var category = await Scenario.SeedCategoryAsync("Editable Category");
                using var client = await Scenario.CreateAuthenticatedClientAsync(admin);
                var response = await client.PutAsJsonAsync($"/api/categories/{category.Id}", new CategoryUpdateRequest("Updated Category", null));
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "categories-delete":
            {
                var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "categories-delete-admin");
                var category = await Scenario.SeedCategoryAsync("Delete Category");
                using var client = await Scenario.CreateAuthenticatedClientAsync(admin);
                var response = await client.DeleteAsync($"/api/categories/{category.Id}");
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                break;
            }
            default:
                throw new NotSupportedException(endpointId);
        }
    }

    [Theory]
    [InlineData("addresses-get")]
    [InlineData("addresses-post")]
    [InlineData("addresses-put")]
    [InlineData("addresses-delete")]
    [InlineData("addresses-default")]
    [InlineData("cart-get")]
    [InlineData("cart-post")]
    [InlineData("cart-put")]
    [InlineData("cart-delete")]
    [InlineData("cart-clear")]
    [InlineData("categories-get")]
    [InlineData("categories-tree")]
    [InlineData("categories-post")]
    [InlineData("categories-put")]
    [InlineData("categories-delete")]
    public async Task InvalidCases(string endpointId)
    {
        switch (endpointId)
        {
            case "addresses-get":
            {
                using var client = Factory.CreateClient();
                var response = await client.GetAsync("/api/addresses");
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                break;
            }
            case "addresses-post":
            {
                using var client = Factory.CreateClient();
                var response = await client.PostAsJsonAsync("/api/addresses", new AddressCreateRequest("Home", "+10000000000", "Region", "City", "Line 1", null, "10000", true));
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                break;
            }
            case "addresses-put":
            {
                var actor = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "addresses-put-invalid");
                using var client = await Scenario.CreateAuthenticatedClientAsync(actor);
                var response = await client.PutAsJsonAsync($"/api/addresses/{Guid.NewGuid()}", new AddressUpdateRequest("Office", null, null, "Updated City", null, null, null, false));
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                break;
            }
            case "addresses-delete":
            {
                var actor = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "addresses-delete-invalid");
                using var client = await Scenario.CreateAuthenticatedClientAsync(actor);
                var response = await client.DeleteAsync($"/api/addresses/{Guid.NewGuid()}");
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                break;
            }
            case "addresses-default":
            {
                var actor = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "addresses-default-invalid");
                using var client = await Scenario.CreateAuthenticatedClientAsync(actor);
                var response = await client.PostAsync($"/api/addresses/{Guid.NewGuid()}/default", content: null);
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                break;
            }
            case "cart-get":
            {
                using var client = Factory.CreateClient();
                var response = await client.GetAsync("/api/cart");
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                break;
            }
            case "cart-post":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "cart-post-invalid");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "cart-post-invalid-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Limited Product", stock: 1);
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.PostAsJsonAsync("/api/cart", new CartAddRequest(product.Id, 5));
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                break;
            }
            case "cart-put":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "cart-put-invalid");
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.PutAsJsonAsync($"/api/cart/{Guid.NewGuid()}", new CartUpdateRequest(3));
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                break;
            }
            case "cart-delete":
            {
                var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "cart-delete-invalid");
                using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);
                var response = await client.DeleteAsync($"/api/cart/{Guid.NewGuid()}");
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                break;
            }
            case "cart-clear":
            {
                using var client = Factory.CreateClient();
                var response = await client.DeleteAsync("/api/cart");
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                break;
            }
            case "categories-get":
            {
                using var client = Factory.CreateClient();
                var response = await client.PostAsync("/api/categories", content: null);
                Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest });
                break;
            }
            case "categories-tree":
            {
                using var client = Factory.CreateClient();
                var response = await client.PostAsync("/api/categories/tree", content: null);
                Assert.Contains(response.StatusCode, new[] { HttpStatusCode.MethodNotAllowed, HttpStatusCode.NotFound });
                break;
            }
            case "categories-post":
            {
                var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "categories-post-invalid-admin");
                using var client = await Scenario.CreateAuthenticatedClientAsync(admin);
                var response = await client.PostAsJsonAsync("/api/categories", new CategoryCreateRequest("Created Category", Guid.NewGuid()));
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                break;
            }
            case "categories-put":
            {
                var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "categories-put-invalid-admin");
                var category = await Scenario.SeedCategoryAsync("Editable Invalid Category");
                using var client = await Scenario.CreateAuthenticatedClientAsync(admin);
                var response = await client.PutAsJsonAsync($"/api/categories/{category.Id}", new CategoryUpdateRequest("Updated Category", category.Id));
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                break;
            }
            case "categories-delete":
            {
                var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "categories-delete-invalid-admin");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "categories-delete-invalid-seller");
                var category = await Scenario.SeedCategoryAsync("Used Category");
                await Scenario.SeedProductAsync(seller, category, "Bound Product");
                using var client = await Scenario.CreateAuthenticatedClientAsync(admin);
                var response = await client.DeleteAsync($"/api/categories/{category.Id}");
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                break;
            }
            default:
                throw new NotSupportedException(endpointId);
        }
    }
}
