using System.Net;
using System.Net.Http.Json;
using Marketplace.Application.DTO;
using Marketplace.Core.Enums;
using Marketplace.IntegrationTests.Infrastructure;
using Xunit;

namespace Marketplace.IntegrationTests.Coverage;

public sealed class ProductEndpointsCoverageTests : IntegrationTestBase
{
    public ProductEndpointsCoverageTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Theory]
    [InlineData("products-create")]
    [InlineData("products-search")]
    [InlineData("products-get-by-slug")]
    [InlineData("products-my")]
    [InlineData("products-update")]
    [InlineData("products-delete")]
    [InlineData("products-upload-image")]
    [InlineData("products-delete-image")]
    [InlineData("admin-products-pending")]
    [InlineData("admin-products-approve")]
    [InlineData("admin-products-reject")]
    [InlineData("images-get")]
    public async Task SuccessCases(string endpointId)
    {
        switch (endpointId)
        {
            case "products-create":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "create-success");
                var category = await Scenario.SeedCategoryAsync("Create Category");
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.PostAsJsonAsync("/api/products", new ProductCreateRequest(category.Id, "Created Product", "Created product description", 55m, 3, true));
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "products-search":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "search-success");
                var category = await Scenario.SeedCategoryAsync("Search Category");
                await Scenario.SeedProductAsync(seller, category, "Searchable Product", true, ProductStatus.Approved);
                using var client = Factory.CreateClient();
                var response = await client.GetAsync("/api/products?q=Searchable");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "products-get-by-slug":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "slug-success");
                var product = await Scenario.SeedProductAsync(seller, title: "Slug Product", isPublished: true, status: ProductStatus.Approved, slug: "slug-product");
                using var client = Factory.CreateClient();
                var response = await client.GetAsync($"/api/products/{product.Slug}");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "products-my":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "my-success");
                await Scenario.SeedProductAsync(seller, title: "Mine", isPublished: false, status: ProductStatus.Pending);
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.GetAsync("/api/products/my");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "products-update":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "update-success");
                var oldCategory = await Scenario.SeedCategoryAsync("Old Category");
                var newCategory = await Scenario.SeedCategoryAsync("New Category");
                var product = await Scenario.SeedProductAsync(seller, oldCategory, "Update Me", true, ProductStatus.Approved);
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.PutAsJsonAsync($"/api/products/{product.Id}", new ProductUpdateRequest(newCategory.Id, "Updated Product", "Updated product description", 60m, 4, true));
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                break;
            }
            case "products-delete":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "delete-success");
                var product = await Scenario.SeedProductAsync(seller, title: "Delete Me");
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.DeleteAsync($"/api/products/{product.Id}");
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                break;
            }
            case "products-upload-image":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "upload-success");
                var product = await Scenario.SeedProductAsync(seller, title: "Image Product");
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                using var form = TestScenarioBuilder.CreateImageUpload();
                var response = await client.PostAsync($"/api/products/{product.Id}/images?sortOrder=1", form);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "products-delete-image":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "delete-image-success");
                var product = await Scenario.SeedProductAsync(seller, title: "Delete Image Product");
                var image = await Scenario.SeedProductImageAsync(product);
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.DeleteAsync($"/api/products/images/{image.Id}");
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                break;
            }
            case "admin-products-pending":
            {
                var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "pending-admin-success");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "pending-seller-success");
                await Scenario.SeedProductAsync(seller, title: "Pending Product", isPublished: false, status: ProductStatus.Pending);
                using var client = await Scenario.CreateAuthenticatedClientAsync(admin);
                var response = await client.GetAsync("/api/admin/products/pending");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "admin-products-approve":
            {
                var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "approve-admin-success");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "approve-seller-success");
                var product = await Scenario.SeedProductAsync(seller, title: "Approve Product", isPublished: false, status: ProductStatus.Pending);
                using var client = await Scenario.CreateAuthenticatedClientAsync(admin);
                var response = await client.PostAsync($"/api/admin/products/{product.Id}/approve", content: null);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "admin-products-reject":
            {
                var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "reject-admin-success");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "reject-seller-success");
                var product = await Scenario.SeedProductAsync(seller, title: "Reject Product", isPublished: false, status: ProductStatus.Pending);
                using var client = await Scenario.CreateAuthenticatedClientAsync(admin);
                var response = await client.PostAsJsonAsync($"/api/admin/products/{product.Id}/reject", new ProductRejectRequest("Not compliant"));
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            case "images-get":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "image-get-success");
                var product = await Scenario.SeedProductAsync(seller, title: "Public Image Product");
                var image = await Scenario.SeedProductImageAsync(product);
                using var client = Factory.CreateClient();
                var response = await client.GetAsync($"/api/images/{image.FileName}");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                break;
            }
            default:
                throw new NotSupportedException(endpointId);
        }
    }

    [Theory]
    [InlineData("products-create")]
    [InlineData("products-search")]
    [InlineData("products-get-by-slug")]
    [InlineData("products-my")]
    [InlineData("products-update")]
    [InlineData("products-delete")]
    [InlineData("products-upload-image")]
    [InlineData("products-delete-image")]
    [InlineData("admin-products-pending")]
    [InlineData("admin-products-approve")]
    [InlineData("admin-products-reject")]
    [InlineData("images-get")]
    public async Task InvalidCases(string endpointId)
    {
        switch (endpointId)
        {
            case "products-create":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "create-invalid");
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.PostAsJsonAsync("/api/products", new ProductCreateRequest(Guid.NewGuid(), "Created Product", "Created product description", 55m, 3, true));
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                break;
            }
            case "products-search":
            {
                using var client = Factory.CreateClient();
                var response = await client.GetAsync("/api/products?categoryId=not-a-guid");
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                break;
            }
            case "products-get-by-slug":
            {
                using var client = Factory.CreateClient();
                var response = await client.GetAsync("/api/products/missing-slug");
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                break;
            }
            case "products-my":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: false, emailPrefix: "my-invalid");
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.GetAsync("/api/products/my");
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                break;
            }
            case "products-update":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "update-invalid");
                var category = await Scenario.SeedCategoryAsync("Update Invalid Category");
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.PutAsJsonAsync($"/api/products/{Guid.NewGuid()}", new ProductUpdateRequest(category.Id, "Updated Product", "Updated product description", 60m, 4, true));
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                break;
            }
            case "products-delete":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "delete-invalid");
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.DeleteAsync($"/api/products/{Guid.NewGuid()}");
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                break;
            }
            case "products-upload-image":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "upload-invalid");
                var product = await Scenario.SeedProductAsync(seller, title: "Bad Upload Product");
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                using var form = TestScenarioBuilder.CreateImageUpload(contentType: "text/plain", fileName: "bad.txt");
                var response = await client.PostAsync($"/api/products/{product.Id}/images?sortOrder=1", form);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                break;
            }
            case "products-delete-image":
            {
                var seller1 = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "delete-image-owner");
                var seller2 = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "delete-image-other");
                var product = await Scenario.SeedProductAsync(seller1, title: "Image Owner Product");
                var image = await Scenario.SeedProductImageAsync(product);
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller2);
                var response = await client.DeleteAsync($"/api/products/images/{image.Id}");
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                break;
            }
            case "admin-products-pending":
            {
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "pending-invalid");
                using var client = await Scenario.CreateAuthenticatedClientAsync(seller);
                var response = await client.GetAsync("/api/admin/products/pending");
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                break;
            }
            case "admin-products-approve":
            {
                var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "approve-invalid-admin");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "approve-invalid-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Already Approved Product", isPublished: true, status: ProductStatus.Approved);
                using var client = await Scenario.CreateAuthenticatedClientAsync(admin);
                var response = await client.PostAsync($"/api/admin/products/{product.Id}/approve", content: null);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                break;
            }
            case "admin-products-reject":
            {
                var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "reject-invalid-admin");
                var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "reject-invalid-seller");
                var product = await Scenario.SeedProductAsync(seller, title: "Already Approved Product", isPublished: true, status: ProductStatus.Approved);
                using var client = await Scenario.CreateAuthenticatedClientAsync(admin);
                var response = await client.PostAsJsonAsync($"/api/admin/products/{product.Id}/reject", new ProductRejectRequest("Still rejecting"));
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                break;
            }
            case "images-get":
            {
                using var client = Factory.CreateClient();
                var response = await client.GetAsync("/api/images/missing-file.png");
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                break;
            }
            default:
                throw new NotSupportedException(endpointId);
        }
    }
}
