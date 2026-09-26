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

public sealed class ProductOperationsBugTests : IntegrationTestBase
{
    public ProductOperationsBugTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task ProductDelete_WithPendingOrder_ShouldPreventDeletion()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "delete-ordered-seller");
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "delete-ordered-buyer");
        var product = await Scenario.SeedProductAsync(seller, title: "Ordered Product", price: 100m);

        await Scenario.ExecuteDbAsync(async db =>
        {
            var order = new Order
            {
                BuyerUserId = buyer.UserId,
                BuyerName = buyer.DisplayName,
                Phone = "+10000000000",
                City = "City",
                DeliveryAddress = "Address",
                Status = OrderStatus.Pending,
                Total = 100m
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            db.OrderItems.Add(new OrderItem
            {
                OrderId = order.Id,
                ProductId = product.Id,
                StoreId = product.StoreId,
                ProductTitleSnapshot = product.Title,
                UnitPrice = product.Price,
                Quantity = 1,
                LineTotal = 100m
            });
            await db.SaveChangesAsync();
            return 0;
        });

        using var client = await Scenario.CreateAuthenticatedClientAsync(seller);

        var deleteResponse = await client.DeleteAsync($"/api/products/{product.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task ProductDelete_WithNoOrders_ShouldSucceed()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "delete-noorder-seller");
        var category = await Scenario.SeedCategoryAsync("Delete Category");
        var product = await Scenario.SeedProductAsync(seller, category: category, title: "No Order Product");

        using var client = await Scenario.CreateAuthenticatedClientAsync(seller);

        var deleteResponse = await client.DeleteAsync($"/api/products/{product.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task ProductImageDelete_ShouldRemoveImage()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "delete-img-seller");
        var category = await Scenario.SeedCategoryAsync("Image Category");
        var product = await Scenario.SeedProductAsync(seller, category: category, title: "Image Product");

        var imageId = await Scenario.ExecuteDbAsync(async db =>
        {
            var image = new ProductImage
            {
                ProductId = product.Id,
                FileName = "test-image.jpg",
                ContentType = "image/jpeg",
                SortOrder = 1
            };
            db.ProductImages.Add(image);
            await db.SaveChangesAsync();
            return image.Id;
        });

        using var client = await Scenario.CreateAuthenticatedClientAsync(seller);

        var deleteResponse = await client.DeleteAsync($"/api/products/images/{imageId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var imageExists = await Scenario.ExecuteDbAsync(async db =>
            await db.ProductImages.AnyAsync(i => i.Id == imageId));
        Assert.False(imageExists);
    }

    [Fact]
    public async Task ProductImageDelete_ByNonOwner_ShouldReturnForbidden()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "delete-img-owner");
        var otherSeller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "delete-img-other");
        var category = await Scenario.SeedCategoryAsync("Image Owner Category");
        var product = await Scenario.SeedProductAsync(seller, category: category, title: "Image Owner Product");

        var imageId = await Scenario.ExecuteDbAsync(async db =>
        {
            var image = new ProductImage
            {
                ProductId = product.Id,
                FileName = "test-image.jpg",
                ContentType = "image/jpeg",
                SortOrder = 1
            };
            db.ProductImages.Add(image);
            await db.SaveChangesAsync();
            return image.Id;
        });

        using var client = await Scenario.CreateAuthenticatedClientAsync(otherSeller);

        var deleteResponse = await client.DeleteAsync($"/api/products/images/{imageId}");

        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task ProductSearch_WithCategoryFilter_ReturnsFilteredResults()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "search-cat-seller");
        var categoryA = await Scenario.SeedCategoryAsync("Electronics");
        var categoryB = await Scenario.SeedCategoryAsync("Clothing");

        await Scenario.SeedProductAsync(seller, category: categoryA, title: "Phone", price: 500m, status: ProductStatus.Approved, isPublished: true);
        await Scenario.SeedProductAsync(seller, category: categoryA, title: "Laptop", price: 1000m, status: ProductStatus.Approved, isPublished: true);
        await Scenario.SeedProductAsync(seller, category: categoryB, title: "Shirt", price: 50m, status: ProductStatus.Approved, isPublished: true);

        using var client = Factory.CreateClient();

        var response = await client.GetAsync($"/api/products?categoryId={categoryA.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var products = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(products.TryGetProperty("items", out var items));
        Assert.True(items.GetArrayLength() >= 2);
    }

    [Fact]
    public async Task ProductSearch_WithPriceRange_ReturnsFilteredResults()
    {
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "search-price-seller");
        var category = await Scenario.SeedCategoryAsync("Price Category");

        await Scenario.SeedProductAsync(seller, category: category, title: "Cheap", price: 10m, status: ProductStatus.Approved, isPublished: true);
        await Scenario.SeedProductAsync(seller, category: category, title: "Medium", price: 50m, status: ProductStatus.Approved, isPublished: true);
        await Scenario.SeedProductAsync(seller, category: category, title: "Expensive", price: 100m, status: ProductStatus.Approved, isPublished: true);

        using var client = Factory.CreateClient();

        var response = await client.GetAsync($"/api/products?minPrice=20&maxPrice=75");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var products = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(products.TryGetProperty("items", out var items));
        Assert.Equal(1, items.GetArrayLength());
    }
}
