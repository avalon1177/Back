using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Marketplace.Application.DTO;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Marketplace.IntegrationTests.BugTests;

public sealed class CategoryAndAddressBugTests : IntegrationTestBase
{
    public CategoryAndAddressBugTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CategoryCreate_WithValidData_ShouldSucceed()
    {
        var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "cat-create-admin");

        using var client = await Scenario.CreateAuthenticatedClientAsync(admin);

        var createRequest = new CategoryCreateRequest("New Category", null);
        var response = await client.PostAsJsonAsync("/api/categories", createRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CategoryCreate_WithParent_ShouldSetParentRelationship()
    {
        var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "cat-parent-admin");
        var parent = await Scenario.SeedCategoryAsync("Parent Category");

        using var client = await Scenario.CreateAuthenticatedClientAsync(admin);

        var createRequest = new CategoryCreateRequest("Child Category", parent.Id);
        var response = await client.PostAsJsonAsync("/api/categories", createRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var childSlug = await Scenario.ExecuteDbAsync(async db =>
            (await db.Categories.FirstAsync(c => c.Name == "Child Category")).Slug);
        Assert.Equal("child-category", childSlug);
    }

    [Fact]
    public async Task CategoryTree_ReturnsHierarchicalStructure()
    {
        var parent = await Scenario.SeedCategoryAsync("Electronics");
        await Scenario.ExecuteDbAsync(async db =>
        {
            var child = new Category
            {
                Name = "Smartphones",
                Slug = "smartphones",
                ParentId = parent.Id
            };
            db.Categories.Add(child);
            await db.SaveChangesAsync();
            return 0;
        });

        using var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/categories/tree");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tree = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(tree.ValueKind == JsonValueKind.Array);
    }

    [Fact]
    public async Task CategoryUpdate_WithParent_ShouldUpdateParentRelationship()
    {
        var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "cat-upd-parent-admin");
        var category = await Scenario.SeedCategoryAsync("Update Category");
        var newParent = await Scenario.SeedCategoryAsync("New Parent");

        using var client = await Scenario.CreateAuthenticatedClientAsync(admin);

        var updateRequest = new CategoryUpdateRequest(category.Name, newParent.Id);
        var response = await client.PutAsJsonAsync($"/api/categories/{category.Id}", updateRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CategoryDelete_WithChildren_ShouldFail()
    {
        var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "cat-del-children-admin");
        var parent = await Scenario.SeedCategoryAsync("Parent To Delete");

        await Scenario.ExecuteDbAsync(async db =>
        {
            var child = new Category
            {
                Name = "Child",
                Slug = "child-of-parent",
                ParentId = parent.Id
            };
            db.Categories.Add(child);
            await db.SaveChangesAsync();
            return 0;
        });

        using var client = await Scenario.CreateAuthenticatedClientAsync(admin);

        var response = await client.DeleteAsync($"/api/categories/{parent.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CategoryDelete_WithoutChildren_ShouldSucceed()
    {
        var admin = await Scenario.CreateActorAsync(UserRole.Admin, emailPrefix: "cat-del-no-children-admin");
        var category = await Scenario.SeedCategoryAsync("Category To Delete");

        using var client = await Scenario.CreateAuthenticatedClientAsync(admin);

        var response = await client.DeleteAsync($"/api/categories/{category.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task AddressCreate_ValidData_ShouldSucceed()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "addr-create-buyer");

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var createRequest = new AddressCreateRequest("Home", "+10000000000", "Region", "City", "Street 123", "Apt 4B", "12345");
        var response = await client.PostAsJsonAsync("/api/addresses", createRequest);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task AddressSetDefault_ChangesDefaultAddress()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "addr-default-buyer");

        var address1Id = await Scenario.ExecuteDbAsync(async db =>
        {
            var addr = new Address
            {
                UserId = buyer.UserId,
                Name = "Home",
                Phone = "+10000000000",
                Region = "Region",
                City = "City",
                Line1 = "Street 1",
                PostalCode = "12345",
                IsDefault = true
            };
            db.Addresses.Add(addr);
            await db.SaveChangesAsync();
            return addr.Id;
        });

        var address2Id = await Scenario.ExecuteDbAsync(async db =>
        {
            var addr = new Address
            {
                UserId = buyer.UserId,
                Name = "Work",
                Phone = "+10000000001",
                Region = "Region",
                City = "City",
                Line1 = "Street 2",
                PostalCode = "12345",
                IsDefault = false
            };
            db.Addresses.Add(addr);
            await db.SaveChangesAsync();
            return addr.Id;
        });

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var setDefaultResponse = await client.PostAsync($"/api/addresses/{address2Id}/default", null);

        Assert.Equal(HttpStatusCode.OK, setDefaultResponse.StatusCode);

        var defaultChanged = await Scenario.ExecuteDbAsync(async db =>
        {
            var addr1 = await db.Addresses.FirstAsync(a => a.Id == address1Id);
            var addr2 = await db.Addresses.FirstAsync(a => a.Id == address2Id);
            return !addr1.IsDefault && addr2.IsDefault;
        });
        Assert.True(defaultChanged);
    }

    [Fact]
    public async Task AddressUpdate_ChangesAddressData()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "addr-update-buyer");

        var addressId = await Scenario.ExecuteDbAsync(async db =>
        {
            var addr = new Address
            {
                UserId = buyer.UserId,
                Name = "Home",
                Phone = "+10000000000",
                Region = "Region",
                City = "City",
                Line1 = "Street 1",
                PostalCode = "12345",
                IsDefault = false
            };
            db.Addresses.Add(addr);
            await db.SaveChangesAsync();
            return addr.Id;
        });

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var updateRequest = new AddressUpdateRequest("Work", "+20000000000", "New Region", "New City", "New Street", null, "54321");
        var response = await client.PutAsJsonAsync($"/api/addresses/{addressId}", updateRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AddressDelete_RemovesAddress()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "addr-delete-buyer");

        var addressId = await Scenario.ExecuteDbAsync(async db =>
        {
            var addr = new Address
            {
                UserId = buyer.UserId,
                Name = "Home",
                Phone = "+10000000000",
                Region = "Region",
                City = "City",
                Line1 = "Street 1",
                PostalCode = "12345",
                IsDefault = false
            };
            db.Addresses.Add(addr);
            await db.SaveChangesAsync();
            return addr.Id;
        });

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var response = await client.DeleteAsync($"/api/addresses/{addressId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var exists = await Scenario.ExecuteDbAsync(async db =>
            await db.Addresses.AnyAsync(a => a.Id == addressId));
        Assert.False(exists);
    }
}
