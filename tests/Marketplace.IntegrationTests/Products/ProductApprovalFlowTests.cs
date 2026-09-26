using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Marketplace.Application.DTO;
using Marketplace.IntegrationTests.Infrastructure;
using Xunit;

namespace Marketplace.IntegrationTests.Products;

public sealed class ProductApprovalFlowTests : IClassFixture<MarketplaceApiFactory>, IAsyncLifetime
{
    private readonly MarketplaceApiFactory _factory;

    public ProductApprovalFlowTests(MarketplaceApiFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetStateAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SellerProduct_BecomesVisibleOnlyAfterAdminApproval()
    {
        using var adminClient = await CreateAdminClientAsync();
        using var sellerClient = _factory.CreateClient();
        using var anonymousClient = _factory.CreateClient();

        var category = await CreateCategoryAsync(adminClient, "Laptops");
        await LoginSellerAsync(
            sellerClient,
            "seller-product@test.local",
            "Seller123!",
            "Seller Product",
            "Seller Product Store");

        var created = await sellerClient.PostJsonAndReadAsync<ProductDetails>(
            "/api/products",
            new ProductCreateRequest(category.Id, "ThinkPad X1 Carbon", "Top-level integration test product", 1999.99m, 3, true));

        Assert.False(created.IsPublished);

        var beforeApproval = await anonymousClient.GetFromJsonAsync<ProductSearchResponse>("/api/products?q=ThinkPad");
        Assert.NotNull(beforeApproval);
        Assert.Empty(beforeApproval.Items);

        var approve = await adminClient.PostAsync($"/api/admin/products/{created.Id}/approve", content: null);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var afterApproval = await anonymousClient.GetFromJsonAsync<ProductSearchResponse>("/api/products?q=ThinkPad");
        Assert.NotNull(afterApproval);
        var listed = Assert.Single(afterApproval.Items);
        Assert.Equal(created.Id, listed.Id);
        Assert.True(listed.IsPublished);

        var details = await anonymousClient.GetFromJsonAsync<ProductDetails>($"/api/products/{created.Slug}");
        Assert.NotNull(details);
        Assert.Equal(created.Id, details.Id);
        Assert.True(details.IsPublished);
        Assert.Equal("ThinkPad X1 Carbon", details.Title);
        Assert.Equal("Laptops", details.CategoryName);
    }

    [Fact]
    public async Task Seller_CannotApprovePendingProduct()
    {
        using var adminClient = await CreateAdminClientAsync();
        using var sellerClient = _factory.CreateClient();

        var category = await CreateCategoryAsync(adminClient, "Monitors");
        await LoginSellerAsync(
            sellerClient,
            "seller-approval@test.local",
            "Seller123!",
            "Seller Approval",
            "Seller Approval Store");

        var created = await sellerClient.PostJsonAndReadAsync<ProductDetails>(
            "/api/products",
            new ProductCreateRequest(category.Id, "UltraSharp 32", "4K display", 899.99m, 8, true));

        var approveAttempt = await sellerClient.PostAsync($"/api/admin/products/{created.Id}/approve", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, approveAttempt.StatusCode);
    }

    [Fact]
    public async Task RejectedProduct_StaysHidden_AndLeavesPendingQueue()
    {
        using var adminClient = await CreateAdminClientAsync();
        using var sellerClient = _factory.CreateClient();
        using var anonymousClient = _factory.CreateClient();

        const string sellerEmail = "seller-reject@test.local";

        var category = await CreateCategoryAsync(adminClient, "Accessories");
        await LoginSellerAsync(
            sellerClient,
            sellerEmail,
            "Seller123!",
            "Seller Reject",
            "Seller Reject Store");

        var created = await sellerClient.PostJsonAndReadAsync<ProductDetails>(
            "/api/products",
            new ProductCreateRequest(category.Id, "Laptop Dock", "Thunderbolt docking station", 249.50m, 5, true));

        var pendingBefore = await GetPendingProductsAsync(adminClient);
        Assert.Equal(1, pendingBefore.Total);
        Assert.Contains(pendingBefore.ProductIds, id => id == created.Id);

        var reject = await adminClient.PostAsJsonAsync(
            $"/api/admin/products/{created.Id}/reject",
            new ProductRejectRequest("Missing compliance documents"));

        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);

        var pendingAfter = await GetPendingProductsAsync(adminClient);
        Assert.Equal(0, pendingAfter.Total);
        Assert.DoesNotContain(pendingAfter.ProductIds, id => id == created.Id);

        var search = await anonymousClient.GetFromJsonAsync<ProductSearchResponse>("/api/products?q=Laptop%20Dock");
        Assert.NotNull(search);
        Assert.Empty(search.Items);

        var details = await anonymousClient.GetAsync($"/api/products/{created.Slug}");
        Assert.Equal(HttpStatusCode.NotFound, details.StatusCode);

        var rejectionEmail = _factory.EmailService.GetLatest(sellerEmail, FakeEmailKind.ProductRejected);
        Assert.Equal("Laptop Dock", rejectionEmail.Metadata);
        Assert.Equal("Missing compliance documents", rejectionEmail.Body);
    }

    [Fact]
    public async Task ProductsWithSameTitle_GetUniqueSlugs()
    {
        using var adminClient = await CreateAdminClientAsync();
        using var sellerClient = _factory.CreateClient();

        var category = await CreateCategoryAsync(adminClient, "Keyboards");
        await LoginSellerAsync(
            sellerClient,
            "seller-slugs@test.local",
            "Seller123!",
            "Seller Slugs",
            "Seller Slugs Store");

        var first = await sellerClient.PostJsonAndReadAsync<ProductDetails>(
            "/api/products",
            new ProductCreateRequest(category.Id, "Mechanical Keyboard", "Tactile switches", 120m, 10, true));

        var second = await sellerClient.PostJsonAndReadAsync<ProductDetails>(
            "/api/products",
            new ProductCreateRequest(category.Id, "Mechanical Keyboard", "Linear switches", 140m, 7, true));

        Assert.Equal("mechanical-keyboard", first.Slug);
        Assert.StartsWith("mechanical-keyboard-", second.Slug);
        Assert.NotEqual(first.Slug, second.Slug);
        Assert.False(first.IsPublished);
        Assert.False(second.IsPublished);
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var adminClient = _factory.CreateClient();
        adminClient.SetBearerToken(await adminClient.LoginAsAdminAsync());
        return adminClient;
    }

    private static Task<CategoryResponse> CreateCategoryAsync(HttpClient adminClient, string name) =>
        adminClient.PostJsonAndReadAsync<CategoryResponse>("/api/categories", new CategoryCreateRequest(name, null));

    private async Task LoginSellerAsync(
        HttpClient sellerClient,
        string email,
        string password,
        string displayName,
        string storeName)
    {
        var sellerSession = await sellerClient.RegisterConfirmAndLoginSellerAsync(
            _factory,
            email,
            password,
            displayName,
            storeName);

        sellerClient.SetBearerToken(sellerSession.AccessToken);
    }

    private static async Task<PendingProductsResponse> GetPendingProductsAsync(HttpClient adminClient)
    {
        var response = await adminClient.GetAsync("/api/admin/products/pending");
        response.EnsureSuccessStatusCode();

        await using var content = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(content);

        var root = document.RootElement;
        var total = root.GetProperty("total").GetInt32();
        var ids = root.GetProperty("products")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToList();

        return new PendingProductsResponse(total, ids);
    }

    private sealed record PendingProductsResponse(int Total, IReadOnlyList<Guid> ProductIds);
}
