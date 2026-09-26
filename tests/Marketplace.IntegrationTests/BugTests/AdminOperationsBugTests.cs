using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Marketplace.IntegrationTests.BugTests;

public sealed class AdminOperationsBugTests : IntegrationTestBase
{
    public AdminOperationsBugTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task AdminSetRole_WithValidUser_ShouldAssignRole()
    {
        var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "set-role-admin");
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "set-role-buyer");

        using var adminClient = await Scenario.CreateAuthenticatedClientAsync(admin);

        var response = await adminClient.PostAsync($"/api/admin/set-role?userId={buyer.UserId}&role={UserRole.Seller}", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task AdminSetRole_WithNonExistentUser_ShouldReturnNotFound()
    {
        var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "set-role-admin");

        using var adminClient = await Scenario.CreateAuthenticatedClientAsync(admin);

        var nonExistentUserId = Guid.NewGuid();
        var response = await adminClient.PostAsync($"/api/admin/set-role?userId={nonExistentUserId}&role={UserRole.Seller}", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AdminSetRole_WithoutAdminRole_ShouldReturnForbidden()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "set-role-seller");
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "set-role-forbidden-buyer");

        using var sellerClient = await Scenario.CreateAuthenticatedClientAsync(seller);

        var response = await sellerClient.PostAsync($"/api/admin/set-role?userId={buyer.UserId}&role={UserRole.Admin}", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminStats_ReturnsCorrectStatistics()
    {
        var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "stats-admin");

        await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "stats-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "stats-seller");
        await Scenario.SeedProductAsync(seller, title: "Stats Product");

        using var adminClient = await Scenario.CreateAuthenticatedClientAsync(admin);

        var response = await adminClient.GetAsync("/api/admin/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdminStats_WithoutAdminRole_ShouldReturnForbidden()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "stats-forbidden");

        using var buyerClient = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var response = await buyerClient.GetAsync("/api/admin/stats");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminUsers_ReturnsPaginatedUsers()
    {
        var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "users-admin");

        for (int i = 0; i < 5; i++)
        {
            await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: $"users-buyer-{i}");
        }

        using var adminClient = await Scenario.CreateAuthenticatedClientAsync(admin);

        var response = await adminClient.GetAsync("/api/admin/users?take=3");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var users = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(users.ValueKind == JsonValueKind.Array);
        Assert.True(users.GetArrayLength() <= 3);
    }

    [Fact]
    public async Task AdminUsers_WithoutAdminRole_ShouldReturnForbidden()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "users-forbidden");

        using var sellerClient = await Scenario.CreateAuthenticatedClientAsync(seller);

        var response = await sellerClient.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminProductsPending_ReturnsPendingProducts()
    {
        var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "pending-admin");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "pending-seller");

        await Scenario.SeedProductAsync(seller, title: "Pending Product 1", status: ProductStatus.Pending);
        await Scenario.SeedProductAsync(seller, title: "Pending Product 2", status: ProductStatus.Pending);
        await Scenario.SeedProductAsync(seller, title: "Approved Product", status: ProductStatus.Approved);

        using var adminClient = await Scenario.CreateAuthenticatedClientAsync(admin);

        var response = await adminClient.GetAsync("/api/admin/products/pending");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdminProductApprove_ChangesProductStatus()
    {
        var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "approve-admin");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "approve-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Approve Product", status: ProductStatus.Pending);

        using var adminClient = await Scenario.CreateAuthenticatedClientAsync(admin);

        var response = await adminClient.PostAsync($"/api/admin/products/{product.Id}/approve", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var approvedStatus = await Scenario.ExecuteDbAsync(async db =>
            await db.Products.AsNoTracking().Where(x => x.Id == product.Id).Select(x => x.Status).SingleAsync());
        Assert.Equal(ProductStatus.Approved, approvedStatus);
    }

    [Fact]
    public async Task AdminProductReject_ChangesProductStatus()
    {
        var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "reject-admin");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "reject-seller");
        var product = await Scenario.SeedProductAsync(seller, title: "Reject Product", status: ProductStatus.Pending);

        using var adminClient = await Scenario.CreateAuthenticatedClientAsync(admin);

        var response = await adminClient.PostAsJsonAsync($"/api/admin/products/{product.Id}/reject",
            new { reason = "Product does not meet guidelines" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rejectedStatus = await Scenario.ExecuteDbAsync(async db =>
            await db.Products.AsNoTracking().Where(x => x.Id == product.Id).Select(x => x.Status).SingleAsync());
        Assert.Equal(ProductStatus.Rejected, rejectedStatus);
    }

    [Fact]
    public async Task AdminProductApprove_WithoutAdminRole_ShouldReturnForbidden()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "approve-forbidden-seller");
        var otherSeller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "approve-forbidden-other");
        var product = await Scenario.SeedProductAsync(otherSeller, title: "Approve Forbidden Product", status: ProductStatus.Pending);

        using var sellerClient = await Scenario.CreateAuthenticatedClientAsync(seller);

        var response = await sellerClient.PostAsync($"/api/admin/products/{product.Id}/approve", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
