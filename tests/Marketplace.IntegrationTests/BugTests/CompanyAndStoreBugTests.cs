using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Marketplace.Application.DTO;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Marketplace.API.Features.Extras.ExtrasEndpoints;

namespace Marketplace.IntegrationTests.BugTests;

public sealed class CompanyAndStoreBugTests : IntegrationTestBase
{
    public CompanyAndStoreBugTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CompanyFinance_Get_ReturnsExistingData()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "finance-get-seller");
        
        await Scenario.SeedCompanyFinanceAsync(seller);

        using var client = await Scenario.CreateAuthenticatedClientAsync(seller);

        var response = await client.GetAsync("/api/company/me/finance");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var finance = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(finance.TryGetProperty("bankAccount", out _));
        Assert.True(finance.TryGetProperty("bankName", out _));
    }

    [Fact]
    public async Task CompanyFinance_Update_ChangesData()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "finance-update-seller");

        using var client = await Scenario.CreateAuthenticatedClientAsync(seller);

        var updateRequest = new UpsertCompanyFinanceRequest(
            "UA123456789012345678901234",
            "PrivatBank",
            "300001",
            "1234567890",
            "Payment details"
        );

        var response = await client.PutAsJsonAsync("/api/company/me/finance", updateRequest);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await client.GetAsync("/api/company/me/finance");
        var finance = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("UA123456789012345678901234", finance.GetProperty("bankAccount").GetString());
    }

    [Fact]
    public async Task CompanySchedules_Get_ReturnsExistingSchedules()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "schedules-get-seller");

        await Scenario.SeedCompanySchedulesAsync(seller, new UpsertScheduleSeed(DayOfWeek.Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(18), false));

        using var client = await Scenario.CreateAuthenticatedClientAsync(seller);

        var response = await client.GetAsync("/api/company/me/schedules");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CompanySchedules_Update_ChangesSchedules()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "schedules-update-seller");

        using var client = await Scenario.CreateAuthenticatedClientAsync(seller);

        var updateRequest = new[] 
        { 
            new UpsertCompanyScheduleRequest(DayOfWeek.Monday, TimeSpan.FromHours(8), TimeSpan.FromHours(20), false),
            new UpsertCompanyScheduleRequest(DayOfWeek.Saturday, null, null, true)
        };

        var response = await client.PutAsJsonAsync("/api/company/me/schedules", updateRequest);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task CompanyFinance_WithoutStore_ShouldReturnNotFound()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, emailPrefix: "finance-no-store-seller");

        using var client = await Scenario.CreateAuthenticatedClientAsync(seller);

        var response = await client.GetAsync("/api/company/me/finance");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task StoreUpdate_ChangesStoreData()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "store-update-seller");

        using var client = await Scenario.CreateAuthenticatedClientAsync(seller);

        var updateRequest = new StoreProfile(
            seller.StoreId!.Value,
            "Updated Store Name",
            seller.StoreSlug!,
            "Updated Description"
        );

        var response = await client.PutAsJsonAsync("/api/stores/me", updateRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var store = await Scenario.ExecuteDbAsync(async db =>
            await db.Stores.AsNoTracking().FirstAsync(s => s.OwnerUserId == seller.UserId));

        Assert.Equal("Updated Store Name", store.Name);
        Assert.Equal("Updated Description", store.Description);
    }

    [Fact]
    public async Task StoreUpdate_WithDuplicateSlug_ShouldSucceed()
    {
        var sellerA = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "store-update-a");
        var sellerB = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "store-update-b");

        using var clientA = await Scenario.CreateAuthenticatedClientAsync(sellerA);

        var updateRequest = new StoreProfile(
            sellerA.StoreId!.Value,
            "New Name A",
            sellerB.StoreSlug!,
            "Description"
        );

        var response = await clientA.PutAsJsonAsync("/api/stores/me", updateRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task StoreGetBySlug_ReturnsStoreProfile()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "store-get-seller");

        using var client = Factory.CreateClient();

        var response = await client.GetAsync($"/api/stores/{seller.StoreSlug}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var store = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(seller.StoreName, store.GetProperty("name").GetString());
    }

    [Fact]
    public async Task StoreGetBySlug_NonExistent_ShouldReturnNotFound()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/stores/non-existent-store-slug");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
