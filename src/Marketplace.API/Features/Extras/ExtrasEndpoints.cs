using FluentValidation;
using Marketplace.API.Common;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.API.Features.Extras;

public static class ExtrasEndpoints
{
    public static IEndpointRouteBuilder MapExtrasEndpoints(this IEndpointRouteBuilder app)
    {
        var buyer = new AuthorizeAttribute { Roles = UserRole.Buyer };
        var seller = new AuthorizeAttribute { Roles = UserRole.Seller };
        var admin = new AuthorizeAttribute { Roles = UserRole.Admin };
        var sellerOrAdmin = new AuthorizeAttribute { Roles = $"{UserRole.Seller},{UserRole.Admin}" };

        app.MapGet("/api/shipping-methods", async (AppDbContext db, CancellationToken ct) =>
        {
            var items = await db.ShippingMethods.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(ct);
            return Results.Ok(items.Select(x => new { x.Id, Name = x.Name.ToString(), x.Price, x.EstimatedDays }));
        }).WithTags("Shipping").AllowAnonymous();

        app.MapPost("/api/shipping-methods", async (CreateShippingMethodRequest req, AppDbContext db, IValidator<CreateShippingMethodRequest> validator, CancellationToken ct) =>
        {
            var validationResult = await ValidateAsync(req, validator, ct);
            if (validationResult is not null) return validationResult;
            var entity = new ShippingMethod
            {
                Name = req.Name,
                Price = req.Price,
                EstimatedDays = req.EstimatedDays,
                IsActive = req.IsActive
            };
            db.ShippingMethods.Add(entity);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { entity.Id });
        }).WithTags("Shipping").RequireAuthorization(admin);

        app.MapGet("/api/coupons", async (AppDbContext db, CancellationToken ct) =>
        {
            var items = await db.Coupons.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
            return Results.Ok(items.Select(x => new { x.Id, x.Code, x.Discount, Type = x.DiscountType.ToString(), x.IsActive, x.UsedCount, x.ExpiresAtUtc }));
        }).WithTags("Coupons").RequireAuthorization(admin);

        app.MapGet("/api/coupons/{code}", async (string code, AppDbContext db, CancellationToken ct) =>
        {
            var normalizedCode = code.Trim().ToUpperInvariant();
            var coupon = await db.Coupons.AsNoTracking().FirstOrDefaultAsync(x => 
                x.Code == normalizedCode && 
                x.IsActive && 
                (!x.ExpiresAtUtc.HasValue || x.ExpiresAtUtc > DateTime.UtcNow), ct);
            return coupon is null
                ? Results.NotFound(ApiErrors.Problem("Coupon not found", 404))
                : Results.Ok(new { coupon.Id, coupon.Code, coupon.Discount, Type = coupon.DiscountType.ToString(), coupon.ExpiresAtUtc });
        }).WithTags("Coupons").AllowAnonymous();

        app.MapPost("/api/coupons", async (CreateCouponRequest req, AppDbContext db, IValidator<CreateCouponRequest> validator, CancellationToken ct) =>
        {
            var validationResult = await ValidateAsync(req, validator, ct);
            if (validationResult is not null) return validationResult;
            var code = req.Code.Trim().ToUpperInvariant();
            if (await db.Coupons.AnyAsync(x => x.Code == code, ct))
                return Results.BadRequest(ApiErrors.Problem("Coupon code already exists"));

            var coupon = new Coupon
            {
                Code = code,
                Discount = req.Discount,
                DiscountType = req.DiscountType,
                UsageLimit = req.UsageLimit,
                ExpiresAtUtc = req.ExpiresAtUtc,
                IsActive = req.IsActive
            };
            db.Coupons.Add(coupon);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { coupon.Id, coupon.Code });
        }).WithTags("Coupons").RequireAuthorization(admin);

        app.MapGet("/api/wishlists", async (System.Security.Claims.ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var userId = user.RequireUserId();
            var items = await db.Wishlists.AsNoTracking().Where(x => x.UserId == userId).Select(x => new { x.Id, x.Name, Count = x.Items.Count }).ToListAsync(ct);
            return Results.Ok(items);
        }).WithTags("Wishlists").RequireAuthorization(buyer);

        app.MapPost("/api/wishlists", async (System.Security.Claims.ClaimsPrincipal user, AppDbContext db, WishlistCreateRequest req, IValidator<WishlistCreateRequest> validator, CancellationToken ct) =>
        {
            var validationResult = await ValidateAsync(req, validator, ct);
            if (validationResult is not null) return validationResult;
            var userId = user.RequireUserId();
            var entity = new Wishlist { UserId = userId, Name = req.Name.Trim() };
            db.Wishlists.Add(entity);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { entity.Id, entity.Name });
        }).WithTags("Wishlists").RequireAuthorization(buyer);

        app.MapDelete("/api/wishlists/{wishlistId:guid}", async (Guid wishlistId, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var userId = user.RequireUserId();
            var wishlist = await db.Wishlists.FirstOrDefaultAsync(x => x.Id == wishlistId && x.UserId == userId, ct);
            if (wishlist is null) return Results.NotFound(ApiErrors.Problem("Wishlist not found", 404));
            db.Wishlists.Remove(wishlist);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithTags("Wishlists").RequireAuthorization(buyer);

        app.MapPost("/api/wishlists/{wishlistId:guid}/items", async (Guid wishlistId, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, ProductRefRequest req, IValidator<ProductRefRequest> validator, CancellationToken ct) =>
        {
            var validationResult = await ValidateAsync(req, validator, ct);
            if (validationResult is not null) return validationResult;
            var userId = user.RequireUserId();
            var wishlist = await db.Wishlists.FirstOrDefaultAsync(x => x.Id == wishlistId && x.UserId == userId, ct);
            if (wishlist is null) return Results.NotFound(ApiErrors.Problem("Wishlist not found", 404));
            if (!await db.Products.AnyAsync(x => x.Id == req.ProductId, ct)) return Results.NotFound(ApiErrors.Problem("Product not found", 404));
            var exists = await db.WishlistItems.AnyAsync(x => x.WishlistId == wishlistId && x.ProductId == req.ProductId, ct);
            if (exists) return Results.Conflict(ApiErrors.Problem("Product already in wishlist"));
            db.WishlistItems.Add(new WishlistItem { WishlistId = wishlistId, ProductId = req.ProductId });
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithTags("Wishlists").RequireAuthorization(buyer);

        app.MapDelete("/api/wishlists/{wishlistId:guid}/items/{productId:guid}", async (Guid wishlistId, Guid productId, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var userId = user.RequireUserId();
            var item = await db.WishlistItems.Include(x => x.Wishlist).FirstOrDefaultAsync(x => x.WishlistId == wishlistId && x.ProductId == productId && x.Wishlist.UserId == userId, ct);
            if (item is null) return Results.NotFound(ApiErrors.Problem("Wishlist item not found", 404));
            db.WishlistItems.Remove(item);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithTags("Wishlists").RequireAuthorization(buyer);

        app.MapGet("/api/chats", async (System.Security.Claims.ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var userId = user.RequireUserId();
            var chats = await db.Chats.AsNoTracking()
                .Where(x => x.BuyerId == userId || x.SellerId == userId)
                .Select(x => new { x.Id, x.BuyerId, x.SellerId, x.StoreId, x.CreatedAtUtc, Messages = x.Messages.Count })
                .ToListAsync(ct);
            return Results.Ok(chats);
        }).WithTags("Chats").RequireAuthorization();

        app.MapPost("/api/chats", async (System.Security.Claims.ClaimsPrincipal user, AppDbContext db, CreateChatRequest req, IValidator<CreateChatRequest> validator, CancellationToken ct) =>
        {
            var validationResult = await ValidateAsync(req, validator, ct);
            if (validationResult is not null) return validationResult;
            var userId = user.RequireUserId();
            if (req.SellerId == userId) return Results.BadRequest(ApiErrors.Problem("Cannot create chat with yourself"));
            var sellerExists = await db.Users.AnyAsync(x => x.Id == req.SellerId, ct);
            if (!sellerExists) return Results.NotFound(ApiErrors.Problem("Seller not found", 404));
            if (req.StoreId.HasValue && !await db.Stores.AnyAsync(x => x.Id == req.StoreId && x.OwnerUserId == req.SellerId, ct))
            {
                return Results.NotFound(ApiErrors.Problem("Store not found", 404));
            }
            var existingChat = await db.Chats.FirstOrDefaultAsync(x => x.BuyerId == userId && x.SellerId == req.SellerId, ct);
            if (existingChat is not null) return Results.Ok(new { existingChat.Id });
            var chat = new Chat { BuyerId = userId, SellerId = req.SellerId, StoreId = req.StoreId };
            db.Chats.Add(chat);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { chat.Id });
        }).WithTags("Chats").RequireAuthorization(buyer);

        app.MapGet("/api/chats/{chatId:guid}/messages", async (Guid chatId, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var userId = user.RequireUserId();
            var chat = await db.Chats.AsNoTracking().FirstOrDefaultAsync(x => x.Id == chatId, ct);
            if (chat is null) return Results.NotFound(ApiErrors.Problem("Chat not found", 404));
            if (chat.BuyerId != userId && chat.SellerId != userId && !user.IsInRole(UserRole.Admin)) return Results.Forbid();
            var items = await db.Messages.AsNoTracking().Where(x => x.ChatId == chatId).OrderBy(x => x.CreatedAtUtc).ToListAsync(ct);
            return Results.Ok(items.Select(x => new { x.Id, x.SenderId, x.MessageText, x.IsRead, x.CreatedAtUtc }));
        }).WithTags("Chats").RequireAuthorization();

        app.MapPost("/api/chats/{chatId:guid}/messages", async (Guid chatId, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, CreateMessageRequest req, IValidator<CreateMessageRequest> validator, CancellationToken ct) =>
        {
            var validationResult = await ValidateAsync(req, validator, ct);
            if (validationResult is not null) return validationResult;
            var userId = user.RequireUserId();
            var chat = await db.Chats.FirstOrDefaultAsync(x => x.Id == chatId, ct);
            if (chat is null) return Results.NotFound(ApiErrors.Problem("Chat not found", 404));
            if (chat.BuyerId != userId && chat.SellerId != userId && !user.IsInRole(UserRole.Admin)) return Results.Forbid();
            db.Messages.Add(new Message { ChatId = chatId, SenderId = userId, MessageText = req.Text.Trim() });
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithTags("Chats").RequireAuthorization();

        app.MapPost("/api/chats/{chatId:guid}/messages/{messageId:guid}/read", async (Guid chatId, Guid messageId, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var userId = user.RequireUserId();
            var chat = await db.Chats.FirstOrDefaultAsync(x => x.Id == chatId, ct);
            if (chat is null) return Results.NotFound(ApiErrors.Problem("Chat not found", 404));
            if (chat.BuyerId != userId && chat.SellerId != userId && !user.IsInRole(UserRole.Admin)) return Results.Forbid();
            var message = await db.Messages.FirstOrDefaultAsync(x => x.Id == messageId && x.ChatId == chatId, ct);
            if (message is null) return Results.NotFound(ApiErrors.Problem("Message not found", 404));
            message.IsRead = true;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithTags("Chats").RequireAuthorization();

        app.MapGet("/api/notifications", async (System.Security.Claims.ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var userId = user.RequireUserId();
            var items = await db.Notifications.AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
            return Results.Ok(items.Select(x => new { x.Id, x.Title, x.Text, x.IsRead, x.CreatedAtUtc }));
        }).WithTags("Notifications").RequireAuthorization();

        app.MapPost("/api/notifications/{id:guid}/read", async (Guid id, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var userId = user.RequireUserId();
            var item = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
            if (item is null) return Results.NotFound(ApiErrors.Problem("Notification not found", 404));
            item.IsRead = true;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithTags("Notifications").RequireAuthorization();

        app.MapPost("/api/seller-requests", async (System.Security.Claims.ClaimsPrincipal user, AppDbContext db, CreateSellerRequestBody req, IValidator<CreateSellerRequestBody> validator, CancellationToken ct) =>
        {
            var validationResult = await ValidateAsync(req, validator, ct);
            if (validationResult is not null) return validationResult;
            var userId = user.RequireUserId();
            if (req.StoreId.HasValue && !await db.Stores.AnyAsync(x => x.Id == req.StoreId && x.OwnerUserId == userId, ct)) return Results.NotFound(ApiErrors.Problem("Store not found", 404));
            db.SellerRequests.Add(new SellerRequest { UserId = userId, StoreId = req.StoreId, AdditionalInformation = req.AdditionalInformation.Trim() });
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithTags("SellerRequests").RequireAuthorization();

        app.MapGet("/api/admin/seller-requests", async (AppDbContext db, CancellationToken ct) =>
        {
            var items = await db.SellerRequests.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).Select(x => new { x.Id, x.UserId, x.StoreId, Status = x.Status.ToString(), x.AdditionalInformation }).ToListAsync(ct);
            return Results.Ok(items);
        }).WithTags("Admin").RequireAuthorization(admin);

        app.MapPost("/api/admin/seller-requests/{id:guid}/approve", async (Guid id, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var userId = user.RequireUserId();
            var request = await db.SellerRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (request is null) return Results.NotFound(ApiErrors.Problem("Seller request not found", 404));
            if (request.Status != SellerRequestStatus.Pending) return Results.BadRequest(ApiErrors.Problem("Seller request has already been processed"));
            request.Status = SellerRequestStatus.Approved;
            request.ApprovedAtUtc = DateTime.UtcNow;
            request.ApprovedByUserId = userId;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithTags("Admin").RequireAuthorization(admin);

        app.MapGet("/api/company/me/finance", async (System.Security.Claims.ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var userId = user.RequireUserId();
            var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(x => x.OwnerUserId == userId, ct);
            if (store is null) return Results.NotFound(ApiErrors.Problem("Store not found", 404));
            var finance = await db.CompanyFinances.AsNoTracking().FirstOrDefaultAsync(x => x.StoreId == store.Id, ct);
            return finance is null ? Results.NotFound(ApiErrors.Problem("Finance not found", 404)) : Results.Ok(finance);
        }).WithTags("Company").RequireAuthorization(seller);

        app.MapPut("/api/company/me/finance", async (System.Security.Claims.ClaimsPrincipal user, AppDbContext db, UpsertCompanyFinanceRequest req, IValidator<UpsertCompanyFinanceRequest> validator, CancellationToken ct) =>
        {
            var validationResult = await ValidateAsync(req, validator, ct);
            if (validationResult is not null) return validationResult;
            var userId = user.RequireUserId();
            var store = await db.Stores.FirstOrDefaultAsync(x => x.OwnerUserId == userId, ct);
            if (store is null) return Results.NotFound(ApiErrors.Problem("Store not found", 404));
            var finance = await db.CompanyFinances.FirstOrDefaultAsync(x => x.StoreId == store.Id, ct);
            if (finance is null)
            {
                finance = new CompanyFinance { StoreId = store.Id };
                db.CompanyFinances.Add(finance);
            }
            finance.BankAccount = req.BankAccount.Trim();
            finance.BankName = req.BankName.Trim();
            finance.BankCode = req.BankCode.Trim();
            finance.TaxId = req.TaxId.Trim();
            finance.PaymentDetails = req.PaymentDetails.Trim();
            finance.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithTags("Company").RequireAuthorization(seller);

        app.MapGet("/api/company/me/schedules", async (System.Security.Claims.ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var userId = user.RequireUserId();
            var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(x => x.OwnerUserId == userId, ct);
            if (store is null) return Results.NotFound(ApiErrors.Problem("Store not found", 404));
            var schedules = await db.CompanySchedules.AsNoTracking().Where(x => x.StoreId == store.Id).OrderBy(x => x.Day).ToListAsync(ct);
            return Results.Ok(schedules.Select(x => new { Day = x.Day.ToString(), x.OpenTime, x.CloseTime, x.IsClosed }));
        }).WithTags("Company").RequireAuthorization(seller);

        app.MapPut("/api/company/me/schedules", async (System.Security.Claims.ClaimsPrincipal user, AppDbContext db, List<UpsertCompanyScheduleRequest> req, IValidator<UpsertCompanyScheduleRequest> validator, CancellationToken ct) =>
        {
            var errors = await ValidateCollectionAsync(req, validator, ct);
            if (req.GroupBy(x => x.Day).Any(g => g.Count() > 1))
            {
                errors.Add("Each day can only be provided once");
            }

            var invalidHours = req.Any(x => !x.IsClosed && x.OpenTime >= x.CloseTime);
            if (invalidHours)
            {
                errors.Add("OpenTime must be earlier than CloseTime when schedule is open");
            }

            if (errors.Count > 0) return Results.BadRequest(errors);

            var userId = user.RequireUserId();
            var store = await db.Stores.FirstOrDefaultAsync(x => x.OwnerUserId == userId, ct);
            if (store is null) return Results.NotFound(ApiErrors.Problem("Store not found", 404));
            var existing = await db.CompanySchedules.Where(x => x.StoreId == store.Id).ToListAsync(ct);
            db.CompanySchedules.RemoveRange(existing);
            foreach (var row in req)
            {
                db.CompanySchedules.Add(new CompanySchedule
                {
                    StoreId = store.Id,
                    Day = row.Day,
                    OpenTime = row.OpenTime,
                    CloseTime = row.CloseTime,
                    IsClosed = row.IsClosed
                });
            }
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithTags("Company").RequireAuthorization(seller);

        app.MapGet("/api/orders/{orderId:guid}/payments", async (Guid orderId, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == orderId, ct);
            if (order is null) return Results.NotFound(ApiErrors.Problem("Order not found", 404));
            var userId = user.RequireUserId();
            var isSeller = await db.OrderItems.AsNoTracking().AnyAsync(x => x.OrderId == orderId && x.Store.OwnerUserId == userId, ct);
            if (order.BuyerUserId != userId && !isSeller && !user.IsInRole(UserRole.Admin)) return Results.Forbid();
            var payments = await db.Payments.AsNoTracking().Where(x => x.OrderId == orderId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
            return Results.Ok(payments.Select(x => new { x.Id, Method = x.PaymentMethod.ToString(), x.Amount, Status = x.Status.ToString(), x.ExternalReference, x.CreatedAtUtc }));
        }).WithTags("Payments").RequireAuthorization();

        app.MapPost("/api/orders/{orderId:guid}/payments", async (Guid orderId, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, CreatePaymentRequest req, IValidator<CreatePaymentRequest> validator, CancellationToken ct) =>
        {
            var validationResult = await ValidateAsync(req, validator, ct);
            if (validationResult is not null) return validationResult;
            if (req.Amount <= 0) return Results.BadRequest(ApiErrors.Problem("Payment amount must be greater than zero"));
            var order = await db.Orders.FirstOrDefaultAsync(x => x.Id == orderId, ct);
            if (order is null) return Results.NotFound(ApiErrors.Problem("Order not found", 404));
            if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.Confirmed)
                return Results.BadRequest(ApiErrors.Problem("Cannot add payment to order in current status"));
            if (req.Amount != order.Total) return Results.BadRequest(ApiErrors.Problem("Payment amount must equal order total"));
            var userId = user.RequireUserId();
            if (order.BuyerUserId != userId && !user.IsInRole(UserRole.Admin)) return Results.Forbid();
            var payment = new Payment
            {
                OrderId = orderId,
                PaymentMethod = req.PaymentMethod,
                Amount = req.Amount,
                Status = req.Status,
                ExternalReference = string.IsNullOrWhiteSpace(req.ExternalReference) ? $"PAY-{DateTime.UtcNow:yyyyMMddHHmmss}" : req.ExternalReference.Trim()
            };
            db.Payments.Add(payment);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { payment.Id, payment.ExternalReference });
        }).WithTags("Payments").RequireAuthorization();

        app.MapPost("/api/orders/{orderId:guid}/payments/liqpay/checkout", async (Guid orderId, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, ILiqPayService liqPay, CancellationToken ct) =>
        {
            if (!liqPay.IsConfigured()) return Results.Problem("LiqPay is not configured", statusCode: 500);
            var order = await db.Orders.FirstOrDefaultAsync(x => x.Id == orderId, ct);
            if (order is null) return Results.NotFound(ApiErrors.Problem("Order not found", 404));
            var userId = user.RequireUserId();
            if (order.BuyerUserId != userId && !user.IsInRole(UserRole.Admin)) return Results.Forbid();

            var externalReference = $"LP-{order.Id:N}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            var payment = new Payment
            {
                OrderId = orderId,
                PaymentMethod = PaymentMethod.LiqPay,
                Amount = order.Total,
                Status = PaymentStatus.Pending,
                ExternalReference = externalReference
            };
            db.Payments.Add(payment);
            await db.SaveChangesAsync(ct);

            var payload = liqPay.CreateCheckoutPayload(order.Total, $"Order {order.Id}", externalReference);
            return Results.Ok(new
            {
                payment.Id,
                payment.OrderId,
                payment.Amount,
                payment.Status,
                payment.ExternalReference,
                provider = "LiqPay",
                checkoutUrl = payload.CheckoutUrl,
                data = payload.Data,
                signature = payload.Signature,
                form = new { action = payload.CheckoutUrl, method = "POST", data = payload.Data, signature = payload.Signature }
            });
        }).WithTags("Payments").RequireAuthorization();

        app.MapPost("/api/payments/liqpay/callback", async (HttpRequest request, AppDbContext db, ILiqPayService liqPay, CancellationToken ct) =>
        {
            if (!liqPay.IsConfigured()) return Results.Problem("LiqPay is not configured", statusCode: 500);
            var form = await request.ReadFormAsync(ct);
            var data = form["data"].ToString();
            var signature = form["signature"].ToString();
            if (string.IsNullOrWhiteSpace(data) || string.IsNullOrWhiteSpace(signature)) return Results.BadRequest(ApiErrors.Problem("Callback payload is invalid"));
            if (!liqPay.ValidateSignature(data, signature)) return Results.BadRequest(ApiErrors.Problem("Invalid LiqPay signature"));

            var callback = liqPay.ParseCallback(data);
            if (string.IsNullOrWhiteSpace(callback.OrderId)) return Results.BadRequest(ApiErrors.Problem("Order reference is missing"));

            var payment = await db.Payments.Include(x => x.Order).FirstOrDefaultAsync(x => x.ExternalReference == callback.OrderId, ct);
            if (payment is null) return Results.NotFound(ApiErrors.Problem("Payment not found", 404));
            if (payment.Order is null) return Results.NotFound(ApiErrors.Problem("Order not found", 404));

            if (payment.Status == PaymentStatus.Completed) return Results.Ok(new { status = payment.Status.ToString(), reference = callback.OrderId, providerStatus = callback.Status });

            payment.Status = MapLiqPayStatus(callback.Status);
            payment.UpdatedAtUtc = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(callback.TransactionId)) payment.ExternalReference = callback.OrderId!;

            if (payment.Status == PaymentStatus.Completed && payment.Order.Status == OrderStatus.Pending)
            {
                payment.Order.Status = OrderStatus.Confirmed;
                payment.Order.UpdatedAtUtc = DateTime.UtcNow;
            }

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { status = payment.Status.ToString(), reference = callback.OrderId, providerStatus = callback.Status });
        }).WithTags("Payments").AllowAnonymous();

        return app;
    }


    private static PaymentStatus MapLiqPayStatus(string? status)
    {
        return status?.ToLowerInvariant() switch
        {
            "success" => PaymentStatus.Completed,
            "sandbox" => PaymentStatus.Completed,
            "wait_accept" => PaymentStatus.Pending,
            "failure" => PaymentStatus.Failed,
            "error" => PaymentStatus.Failed,
            "reversed" => PaymentStatus.Failed,
            _ => PaymentStatus.Pending
        };
    }

    private static async Task<IResult?> ValidateAsync<T>(T request, IValidator<T> validator, CancellationToken ct)
    {
        var vr = await validator.ValidateAsync(request, ct);
        return vr.IsValid ? null : Results.BadRequest(vr.Errors.Select(e => e.ErrorMessage));
    }

    private static async Task<List<string>> ValidateCollectionAsync<T>(IReadOnlyList<T> items, IValidator<T> validator, CancellationToken ct)
    {
        var errors = new List<string>();
        for (var index = 0; index < items.Count; index++)
        {
            var vr = await validator.ValidateAsync(items[index], ct);
            errors.AddRange(vr.Errors.Select(e => $"[{index}] {e.ErrorMessage}"));
        }

        return errors;
    }

    public record WishlistCreateRequest(string Name);
    public record ProductRefRequest(Guid ProductId);
    public record CreateChatRequest(Guid SellerId, Guid? StoreId);
    public record CreateMessageRequest(string Text);
    public record CreateSellerRequestBody(Guid? StoreId, string AdditionalInformation);
    public record CreateShippingMethodRequest(ShippingMethodType Name, decimal Price, int EstimatedDays, bool IsActive = true);
    public record CreateCouponRequest(string Code, decimal Discount, DiscountType DiscountType, int? UsageLimit, DateTime? ExpiresAtUtc, bool IsActive = true);
    public record UpsertCompanyFinanceRequest(string BankAccount, string BankName, string BankCode, string TaxId, string PaymentDetails);
    public record UpsertCompanyScheduleRequest(DayOfWeek Day, TimeSpan? OpenTime, TimeSpan? CloseTime, bool IsClosed);
    public record CreatePaymentRequest(PaymentMethod PaymentMethod, decimal Amount, PaymentStatus Status = PaymentStatus.Pending, string? ExternalReference = null);
}
