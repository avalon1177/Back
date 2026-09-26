using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.Core.Utils;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Marketplace.IntegrationTests.Infrastructure;

public sealed class TestScenarioBuilder
{
    private const string DefaultPassword = "Pass123!";
    private readonly MarketplaceApiFactory _factory;

    public TestScenarioBuilder(MarketplaceApiFactory factory)
    {
        _factory = factory;
    }

    public async Task<TestActor> CreateActorAsync(
        string role,
        bool withStore = false,
        bool emailConfirmed = true,
        bool twoFactorEnabled = false,
        string? emailPrefix = null,
        string? displayName = null,
        string? storeName = null)
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var db = services.GetRequiredService<AppDbContext>();

        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"{emailPrefix ?? role.ToLowerInvariant()}-{suffix}@test.local";
        var user = new AppUser
        {
            Email = email,
            UserName = email,
            DisplayName = displayName ?? $"{role} {suffix}",
            EmailConfirmed = emailConfirmed
        };

        var createResult = await userManager.CreateAsync(user, DefaultPassword);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException("Failed to create test user.");
        }

        await userManager.AddToRoleAsync(user, role);

        if (twoFactorEnabled)
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            user.TwoFactorEnabled = true;
            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                throw new InvalidOperationException("Failed to enable two-factor authentication for test user.");
            }
        }

        Store? store = null;
        if (withStore)
        {
            store = new Store
            {
                OwnerUserId = user.Id,
                Name = storeName ?? $"Store {suffix}",
                Slug = Slug.From(storeName ?? $"store-{suffix}"),
                Description = $"Store for {role} integration tests",
                ContactEmail = email,
                ContactPhone = "+10000000000",
                Region = "Test Region",
                City = "Test City",
                Street = "Test Street 1",
                PostalCode = "10000",
                IsApproved = true
            };
            db.Stores.Add(store);
            await db.SaveChangesAsync();
        }

        return new TestActor(user.Id, email, DefaultPassword, role, user.DisplayName, store?.Id, store?.Slug, store?.Name);
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(TestActor actor)
    {
        string token;
        using (var scope = _factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var userManager = services.GetRequiredService<UserManager<AppUser>>();
            var tokenService = services.GetRequiredService<ITokenService>();
            var user = await userManager.Users.Include(x => x.Store).FirstAsync(x => x.Id == actor.UserId);
            (token, _) = await tokenService.CreateTokenAsync(user);
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task<(TestActor Actor, HttpClient Client)> CreateAuthenticatedActorAsync(
        string role,
        bool withStore = false,
        bool emailConfirmed = true,
        bool twoFactorEnabled = false,
        string? emailPrefix = null)
    {
        var actor = await CreateActorAsync(role, withStore, emailConfirmed, twoFactorEnabled, emailPrefix);
        var client = await CreateAuthenticatedClientAsync(actor);
        return (actor, client);
    }

    public async Task<string> GenerateAuthenticatorCodeAsync(TestActor actor)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.FindByIdAsync(actor.UserId.ToString()) ?? throw new InvalidOperationException("User not found.");
        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("Authenticator key was not initialized.");
        }

        return GenerateTotpCode(key);
    }

    public async Task<TestCategory> SeedCategoryAsync(string? name = null, Guid? parentId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var entity = new Category
        {
            Name = name ?? $"Category {suffix}",
            Slug = Slug.From(name ?? $"category-{suffix}"),
            ParentId = parentId
        };
        db.Categories.Add(entity);
        await db.SaveChangesAsync();
        return new TestCategory(entity.Id, entity.Name, entity.Slug, entity.ParentId);
    }

    public async Task<TestProduct> SeedProductAsync(
        TestActor seller,
        TestCategory? category = null,
        string? title = null,
        bool isPublished = true,
        ProductStatus status = ProductStatus.Approved,
        decimal price = 100m,
        int stock = 10,
        string? description = null,
        string? slug = null)
    {
        if (seller.StoreId is null)
        {
            throw new InvalidOperationException("Seller must have a store to seed products.");
        }

        category ??= await SeedCategoryAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var resolvedTitle = title ?? $"Product {suffix}";
        var resolvedSlug = slug ?? Slug.From($"{resolvedTitle}-{suffix}");

        var entity = new Product
        {
            StoreId = seller.StoreId.Value,
            CategoryId = category.Id,
            Title = resolvedTitle,
            Slug = resolvedSlug,
            Description = description ?? $"{resolvedTitle} description",
            Price = price,
            Stock = stock,
            Status = status,
            IsPublished = isPublished,
            ApprovedAtUtc = isPublished ? DateTime.UtcNow : null
        };

        db.Products.Add(entity);
        await db.SaveChangesAsync();
        return new TestProduct(entity.Id, entity.StoreId, entity.CategoryId, entity.Title, entity.Slug, entity.Price, entity.Stock, entity.IsPublished, entity.Status);
    }

    public async Task<TestProductImage> SeedProductImageAsync(TestProduct product, int sortOrder = 0, string contentType = "image/png")
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();
        var storage = services.GetRequiredService<IStorageService>();

        await using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        var (fileName, savedContentType) = await storage.SaveProductImageAsync(stream, contentType, "seed.png");
        var image = new ProductImage
        {
            ProductId = product.Id,
            FileName = fileName,
            ContentType = savedContentType,
            SortOrder = sortOrder
        };

        db.ProductImages.Add(image);
        await db.SaveChangesAsync();
        return new TestProductImage(image.Id, product.Id, image.FileName, image.ContentType, image.SortOrder);
    }

    public async Task<TestAddress> SeedAddressAsync(TestActor actor, bool isDefault = false, string? city = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var address = new Address
        {
            UserId = actor.UserId,
            Name = actor.DisplayName,
            Phone = "+10000000000",
            Region = "Test Region",
            City = city ?? "Test City",
            Line1 = "Test Line 1",
            PostalCode = "10000",
            IsDefault = isDefault
        };
        db.Addresses.Add(address);
        await db.SaveChangesAsync();
        return new TestAddress(address.Id, actor.UserId, address.City, address.IsDefault);
    }

    public async Task<TestCartItem> SeedCartItemAsync(TestActor buyer, TestProduct product, int quantity = 1)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = new CartItem
        {
            BuyerUserId = buyer.UserId,
            ProductId = product.Id,
            Quantity = quantity
        };
        db.CartItems.Add(entity);
        await db.SaveChangesAsync();
        return new TestCartItem(entity.Id, entity.BuyerUserId, entity.ProductId, entity.Quantity);
    }

    public async Task<TestOrder> SeedOrderAsync(TestActor buyer, TestProduct product, int quantity = 1, OrderStatus status = OrderStatus.Pending)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var total = product.Price * quantity;
        var order = new Order
        {
            BuyerUserId = buyer.UserId,
            Status = status,
            BuyerName = buyer.DisplayName,
            Phone = "+10000000000",
            City = "Test City",
            DeliveryAddress = "Test Delivery Address",
            Total = total
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var item = new OrderItem
        {
            OrderId = order.Id,
            ProductId = product.Id,
            StoreId = product.StoreId,
            ProductTitleSnapshot = product.Title,
            UnitPrice = product.Price,
            Quantity = quantity,
            LineTotal = total
        };
        db.OrderItems.Add(item);
        await db.SaveChangesAsync();

        return new TestOrder(order.Id, buyer.UserId, product.StoreId, order.Status, order.Total);
    }

    public async Task<(Guid OrderId, Guid StoreId)> SeedOrderWithStatusAsync(TestActor buyer, TestProduct product, OrderStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var order = new Order
        {
            BuyerUserId = buyer.UserId,
            Status = status,
            BuyerName = buyer.DisplayName,
            Phone = "+10000000000",
            City = "Test City",
            DeliveryAddress = "Test Delivery Address",
            Total = product.Price
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var item = new OrderItem
        {
            OrderId = order.Id,
            ProductId = product.Id,
            StoreId = product.StoreId,
            ProductTitleSnapshot = product.Title,
            UnitPrice = product.Price,
            Quantity = 1,
            LineTotal = product.Price
        };
        db.OrderItems.Add(item);
        await db.SaveChangesAsync();

        return (order.Id, product.StoreId);
    }

    public async Task<TestReview> SeedReviewAsync(TestActor buyer, TestProduct product, int rating = 5, string? text = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = new Review
        {
            BuyerUserId = buyer.UserId,
            ProductId = product.Id,
            Rating = rating,
            Text = text ?? "Great product"
        };
        db.Reviews.Add(entity);
        await db.SaveChangesAsync();
        return new TestReview(entity.Id, entity.ProductId, entity.BuyerUserId);
    }

    public async Task<TestNotification> SeedNotificationAsync(TestActor actor, string title = "Hello", string text = "World", bool isRead = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = new Notification
        {
            UserId = actor.UserId,
            Title = title,
            Text = text,
            IsRead = isRead
        };
        db.Notifications.Add(entity);
        await db.SaveChangesAsync();
        return new TestNotification(entity.Id, entity.UserId, entity.IsRead);
    }

    public async Task<TestShippingMethod> SeedShippingMethodAsync(ShippingMethodType name = ShippingMethodType.Courier, decimal price = 50m, int estimatedDays = 3, bool isActive = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = new ShippingMethod
        {
            Name = name,
            Price = price,
            EstimatedDays = estimatedDays,
            IsActive = isActive
        };
        db.ShippingMethods.Add(entity);
        await db.SaveChangesAsync();
        return new TestShippingMethod(entity.Id, entity.Name, entity.IsActive);
    }

    public async Task<TestCoupon> SeedCouponAsync(string code = "SAVE10", decimal discount = 10m, DiscountType type = DiscountType.Fixed, bool isActive = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = new Coupon
        {
            Code = code,
            Discount = discount,
            DiscountType = type,
            IsActive = isActive
        };
        db.Coupons.Add(entity);
        await db.SaveChangesAsync();
        return new TestCoupon(entity.Id, entity.Code, entity.IsActive);
    }

    public async Task<TestWishlist> SeedWishlistAsync(TestActor actor, string name = "Favorites")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = new Wishlist
        {
            UserId = actor.UserId,
            Name = name
        };
        db.Wishlists.Add(entity);
        await db.SaveChangesAsync();
        return new TestWishlist(entity.Id, entity.UserId, entity.Name);
    }

    public async Task SeedWishlistItemAsync(TestWishlist wishlist, TestProduct product)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.WishlistItems.Add(new WishlistItem { WishlistId = wishlist.Id, ProductId = product.Id });
        await db.SaveChangesAsync();
    }

    public async Task<TestChat> SeedChatAsync(TestActor buyer, TestActor seller, Guid? storeId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = new Chat
        {
            BuyerId = buyer.UserId,
            SellerId = seller.UserId,
            StoreId = storeId ?? seller.StoreId
        };
        db.Chats.Add(entity);
        await db.SaveChangesAsync();
        return new TestChat(entity.Id, entity.BuyerId, entity.SellerId, entity.StoreId);
    }

    public async Task<TestMessage> SeedMessageAsync(TestChat chat, TestActor sender, string text = "Hello there", bool isRead = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = new Message
        {
            ChatId = chat.Id,
            SenderId = sender.UserId,
            MessageText = text,
            IsRead = isRead
        };
        db.Messages.Add(entity);
        await db.SaveChangesAsync();
        return new TestMessage(entity.Id, entity.ChatId, entity.SenderId, entity.IsRead);
    }

    public async Task<TestSellerRequest> SeedSellerRequestAsync(TestActor actor, Guid? storeId = null, string additionalInformation = "Please approve")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = new SellerRequest
        {
            UserId = actor.UserId,
            StoreId = storeId,
            AdditionalInformation = additionalInformation
        };
        db.SellerRequests.Add(entity);
        await db.SaveChangesAsync();
        return new TestSellerRequest(entity.Id, entity.UserId, entity.StoreId, entity.Status);
    }

    public async Task<TestCompanyFinance> SeedCompanyFinanceAsync(TestActor seller)
    {
        if (seller.StoreId is null)
        {
            throw new InvalidOperationException("Seller must have a store to seed finance.");
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = new CompanyFinance
        {
            StoreId = seller.StoreId.Value,
            BankAccount = "UA123",
            BankName = "Test Bank",
            BankCode = "300001",
            TaxId = "1234567890",
            PaymentDetails = "Test details"
        };
        db.CompanyFinances.Add(entity);
        await db.SaveChangesAsync();
        return new TestCompanyFinance(entity.Id, entity.StoreId);
    }

    public async Task SeedCompanySchedulesAsync(TestActor seller, params UpsertScheduleSeed[] rows)
    {
        if (seller.StoreId is null)
        {
            throw new InvalidOperationException("Seller must have a store to seed schedules.");
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var row in rows)
        {
            db.CompanySchedules.Add(new CompanySchedule
            {
                StoreId = seller.StoreId.Value,
                Day = row.Day,
                OpenTime = row.OpenTime,
                CloseTime = row.CloseTime,
                IsClosed = row.IsClosed
            });
        }

        await db.SaveChangesAsync();
    }

    public async Task<TestPayment> SeedPaymentAsync(TestOrder order, PaymentMethod method = PaymentMethod.Card, PaymentStatus status = PaymentStatus.Pending, decimal? amount = null, string? externalReference = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = new Payment
        {
            OrderId = order.Id,
            PaymentMethod = method,
            Amount = amount ?? order.Total,
            Status = status,
            ExternalReference = externalReference ?? $"PAY-{Guid.NewGuid():N}"
        };
        db.Payments.Add(entity);
        await db.SaveChangesAsync();
        return new TestPayment(entity.Id, entity.OrderId, entity.ExternalReference, entity.Status);
    }

    public async Task<T> ExecuteDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await action(db);
    }

    public static MultipartFormDataContent CreateImageUpload(string fieldName = "file", string fileName = "image.png", string contentType = "image/png")
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(new byte[] { 137, 80, 78, 71 });
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, fieldName, fileName);
        return content;
    }

    private static string GenerateTotpCode(string unformattedKey)
    {
        var key = Base32Decode(unformattedKey);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var counter = BitConverter.GetBytes(timestamp);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counter);
        }

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counter);
        var offset = hash[^1] & 0x0F;
        var binaryCode =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        return (binaryCode % 1_000_000).ToString("D6");
    }

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var clean = input.Replace(" ", string.Empty, StringComparison.Ordinal).TrimEnd('=').ToUpperInvariant();
        var output = new List<byte>();
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var c in clean)
        {
            var value = alphabet.IndexOf(c);
            if (value < 0)
            {
                throw new FormatException("Authenticator key contains invalid Base32 characters.");
            }

            buffer = (buffer << 5) | value;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                output.Add((byte)(buffer >> (bitsLeft - 8)));
                bitsLeft -= 8;
            }
        }

        return output.ToArray();
    }
}

public sealed record TestActor(Guid UserId, string Email, string Password, string Role, string DisplayName, Guid? StoreId, string? StoreSlug, string? StoreName);
public sealed record TestCategory(Guid Id, string Name, string Slug, Guid? ParentId);
public sealed record TestProduct(Guid Id, Guid StoreId, Guid CategoryId, string Title, string Slug, decimal Price, int Stock, bool IsPublished, ProductStatus Status);
public sealed record TestProductImage(Guid Id, Guid ProductId, string FileName, string ContentType, int SortOrder);
public sealed record TestAddress(Guid Id, Guid UserId, string City, bool IsDefault);
public sealed record TestCartItem(Guid Id, Guid BuyerUserId, Guid ProductId, int Quantity);
public sealed record TestOrder(Guid Id, Guid BuyerUserId, Guid StoreId, OrderStatus Status, decimal Total);
public sealed record TestReview(Guid Id, Guid ProductId, Guid BuyerUserId);
public sealed record TestNotification(Guid Id, Guid UserId, bool IsRead);
public sealed record TestShippingMethod(Guid Id, ShippingMethodType Name, bool IsActive);
public sealed record TestCoupon(Guid Id, string Code, bool IsActive);
public sealed record TestWishlist(Guid Id, Guid UserId, string Name);
public sealed record TestChat(Guid Id, Guid BuyerId, Guid SellerId, Guid? StoreId);
public sealed record TestMessage(Guid Id, Guid ChatId, Guid SenderId, bool IsRead);
public sealed record TestSellerRequest(Guid Id, Guid UserId, Guid? StoreId, SellerRequestStatus Status);
public sealed record TestCompanyFinance(Guid Id, Guid StoreId);
public sealed record TestPayment(Guid Id, Guid OrderId, string ExternalReference, PaymentStatus Status);
public sealed record UpsertScheduleSeed(DayOfWeek Day, TimeSpan? OpenTime, TimeSpan? CloseTime, bool IsClosed);
