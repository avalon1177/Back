using FluentValidation;
using Marketplace.API.Common;
using Marketplace.Application.DTO;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.Core.Utils;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace Marketplace.API.Features.Legacy;

public static class LegacyEndpoints
{
    public static IEndpointRouteBuilder MapLegacyEndpoints(this IEndpointRouteBuilder app)
    {
        var buyer = new AuthorizeAttribute { Roles = UserRole.Buyer };
        var seller = new AuthorizeAttribute { Roles = UserRole.Seller };
        var admin = new AuthorizeAttribute { Roles = UserRole.Admin };
        var sellerOrAdmin = new AuthorizeAttribute { Roles = $"{UserRole.Seller},{UserRole.Admin}" };

        static AuthUserDto ToAuthUser(AppUser user, string role)
        {
            StoreProfile? store = user.Store is null ? null : new StoreProfile(user.Store.Id, user.Store.Name, user.Store.Slug, user.Store.Description);
            return new AuthUserDto(user.Id, user.Email ?? string.Empty, user.DisplayName, role, store);
        }

        app.MapPost("/api/auth/register", async (
            RegisterRequest req,
            HttpContext context,
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole<Guid>> roleManager,
            AppDbContext db,
            ITokenService tokenService,
            IEmailService emailService,
            IConfiguration configuration,
            IValidator<RegisterRequest> registerValidator) =>
        {
            var vr = await registerValidator.ValidateAsync(req);
            if (!vr.IsValid) return Results.BadRequest(vr.Errors.Select(e => e.ErrorMessage));
            if (!await roleManager.RoleExistsAsync(req.Role))
                return Results.BadRequest(new { code = "ROLE_NOT_FOUND", message = "Role does not exist" });
            var email = req.Email.Trim().ToLowerInvariant();
            var user = new AppUser
            {
                Email = email,
                UserName = email,
                DisplayName = req.DisplayName.Trim(),
                EmailConfirmed = false
            };
            var created = await userManager.CreateAsync(user, req.Password);
            if (!created.Succeeded) return Results.BadRequest(new { code = "REGISTER_FAILED", errors = created.Errors.Select(e => e.Description) });
            await userManager.AddToRoleAsync(user, req.Role);
            
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            var baseUrl = configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
            await emailService.SendConfirmationEmailAsync(user, token, baseUrl);
            
            if (req.Role == UserRole.Seller)
            {
                var storeName = req.StoreName!.Trim();
                var slug = Slug.From(storeName);
                if (await db.Stores.AnyAsync(s => s.Slug == slug)) slug = slug + "-" + Guid.NewGuid().ToString("N")[..8];
                db.Stores.Add(new Store { OwnerUserId = user.Id, Name = storeName, Slug = slug, Description = string.Empty });
                await db.SaveChangesAsync();
                user = await userManager.Users.Include(u => u.Store).FirstAsync(u => u.Id == user.Id);
            }
            
            return Results.Ok(new { message = "Registration successful. Please confirm your email.", email = user.Email });
        }).WithTags("Auth").AllowAnonymous();

        app.MapPost("/api/auth/login", async (
            LoginRequest req,
            HttpContext context,
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            ITokenService tokenService,
            IValidator<LoginRequest> loginValidator) =>
        {
            var vr = await loginValidator.ValidateAsync(req);
            if (!vr.IsValid) return Results.BadRequest(vr.Errors.Select(e => e.ErrorMessage));
            var email = req.Email.Trim().ToLowerInvariant();
            var user = await userManager.Users.Include(u => u.Store).FirstOrDefaultAsync(u => u.Email == email);
            if (user is null) return Results.Unauthorized();
            if (!user.EmailConfirmed) return Results.BadRequest(new { code = "EMAIL_NOT_CONFIRMED", message = "Email is not confirmed" });
            var signIn = await signInManager.CheckPasswordSignInAsync(user, req.Password, false);
            if (!signIn.Succeeded) return Results.Unauthorized();
            if (user.TwoFactorEnabled)
            {
                var (_, _, pendingToken) = await tokenService.CreateSessionAsync(user, req.DeviceId, "2fa");
                return Results.Accepted(value: new TwoFactorRequiredResponse(pendingToken, "TWO_FACTOR_REQUIRED", "Two-factor verification is required"));
            }
            var role = (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? UserRole.Buyer;
            var (accessToken, expiresAtUtc, refreshToken) = await tokenService.CreateSessionAsync(user, req.DeviceId);
            context.Response.SetRefreshTokenCookie(refreshToken, expiresAtUtc);
            return Results.Ok(new AuthSessionResponse(ToAuthUser(user, role), accessToken, (int)(expiresAtUtc - DateTime.UtcNow).TotalSeconds));
        }).WithTags("Auth").AllowAnonymous();

        app.MapPost("/api/auth/refresh", async (
            RefreshTokenRequest req,
            HttpContext context,
            UserManager<AppUser> userManager,
            ITokenService tokenService,
            IValidator<RefreshTokenRequest> validator) =>
        {
            var token = context.Request.GetRefreshTokenCookie() ?? req.RefreshToken;
            var vr = await validator.ValidateAsync(req);
            if (!vr.IsValid) return Results.BadRequest(vr.Errors.Select(e => e.ErrorMessage));
            var current = await tokenService.GetActiveTokenAsync(token, "refresh");
            if (current is null) return Results.BadRequest(new { code = "TOKEN_EXPIRED", message = "Refresh token is invalid or expired" });
            var user = await userManager.Users.Include(u => u.Store).FirstAsync(u => u.Id == current.UserId);
            var role = (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? UserRole.Buyer;
            var (accessToken, expiresAtUtc, refreshToken) = await tokenService.CreateSessionAsync(user, req.DeviceId ?? current.DeviceId);
            await tokenService.RevokeTokenAsync(current, refreshToken);
            context.Response.SetRefreshTokenCookie(refreshToken, expiresAtUtc);
            return Results.Ok(new AuthSessionResponse(ToAuthUser(user, role), accessToken, (int)(expiresAtUtc - DateTime.UtcNow).TotalSeconds));
        }).WithTags("Auth").AllowAnonymous();

        app.MapPost("/api/auth/2fa/verify", async (
            TwoFactorVerifyRequest req,
            HttpContext context,
            UserManager<AppUser> userManager,
            ITokenService tokenService,
            IValidator<TwoFactorVerifyRequest> validator) =>
        {
            var vr = await validator.ValidateAsync(req);
            if (!vr.IsValid) return Results.BadRequest(vr.Errors.Select(e => e.ErrorMessage));
            var pending = await tokenService.GetActiveTokenAsync(req.TwoFactorToken, "2fa");
            if (pending is null) return Results.BadRequest(new { code = "TOKEN_EXPIRED", message = "Two-factor token is invalid or expired" });
            var user = await userManager.Users.Include(u => u.Store).FirstAsync(u => u.Id == pending.UserId);
            var valid = await userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, req.Code);
            if (!valid) return Results.BadRequest(new { code = "INVALID_2FA_CODE", message = "Invalid two-factor code" });
            var role = (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? UserRole.Buyer;
            var (accessToken, expiresAtUtc, refreshToken) = await tokenService.CreateSessionAsync(user, req.DeviceId ?? pending.DeviceId);
            await tokenService.RevokeTokenAsync(pending);
            context.Response.SetRefreshTokenCookie(refreshToken, expiresAtUtc);
            return Results.Ok(new AuthSessionResponse(ToAuthUser(user, role), accessToken, (int)(expiresAtUtc - DateTime.UtcNow).TotalSeconds));
        }).WithTags("Auth").AllowAnonymous();

        app.MapPost("/api/auth/external-login", async (
            ExternalLoginRequest req,
            HttpContext context,
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole<Guid>> roleManager,
            ITokenService tokenService,
            IExternalAuthService externalAuthService,
            IValidator<ExternalLoginRequest> validator) =>
        {
            var vr = await validator.ValidateAsync(req);
            if (!vr.IsValid) return Results.BadRequest(vr.Errors.Select(e => e.ErrorMessage));
            if (!string.Equals(req.Provider, "google", StringComparison.OrdinalIgnoreCase)) return Results.BadRequest(new { code = "UNSUPPORTED_PROVIDER", message = "Only Google is supported" });
            var externalUser = await externalAuthService.ResolveUserAsync(req);
            if (externalUser is null) return Results.BadRequest(new { code = "INVALID_EXTERNAL_TOKEN", message = "External token validation failed" });
            var normalized = externalUser.Email.Trim().ToLowerInvariant();
            var user = await userManager.Users.Include(u => u.Store).FirstOrDefaultAsync(x => x.Email == normalized);
            if (user is null)
            {
                user = new AppUser { Email = normalized, UserName = normalized, DisplayName = externalUser.Name, EmailConfirmed = true };
                var created = await userManager.CreateAsync(user);
                if (!created.Succeeded) return Results.BadRequest(new { code = "EXTERNAL_LOGIN_FAILED", errors = created.Errors.Select(e => e.Description) });
                if (!await roleManager.RoleExistsAsync(UserRole.Buyer)) await roleManager.CreateAsync(new IdentityRole<Guid>(UserRole.Buyer));
                await userManager.AddToRoleAsync(user, UserRole.Buyer);
            }
            var role = (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? UserRole.Buyer;
            var (accessToken, expiresAtUtc, refreshToken) = await tokenService.CreateSessionAsync(user, req.DeviceId);
            context.Response.SetRefreshTokenCookie(refreshToken, expiresAtUtc);
            return Results.Ok(new AuthSessionResponse(ToAuthUser(user, role), accessToken, (int)(expiresAtUtc - DateTime.UtcNow).TotalSeconds));
        }).WithTags("Auth").AllowAnonymous();

        app.MapPost("/api/auth/confirm-email", async (
            ConfirmEmailRequest req,
            UserManager<AppUser> userManager,
            IValidator<ConfirmEmailRequest> validator) =>
        {
            var vr = await validator.ValidateAsync(req);
            if (!vr.IsValid) return Results.BadRequest(vr.Errors.Select(e => e.ErrorMessage));
            var user = await userManager.FindByEmailAsync(req.Email.Trim().ToLowerInvariant());
            if (user is null) return Results.BadRequest(new { code = "USER_NOT_FOUND", message = "User not found" });
            var decoded = Uri.UnescapeDataString(req.Token);
            var result = await userManager.ConfirmEmailAsync(user, decoded);
            if (!result.Succeeded) return Results.BadRequest(new { code = "INVALID_CONFIRM_TOKEN", errors = result.Errors.Select(e => e.Description) });
            return Results.Ok(new StandardMessageResponse("Email confirmed successfully"));
        }).WithTags("Auth").AllowAnonymous();

        app.MapPost("/api/auth/resend-confirmation", async (
            ResendConfirmationRequest req,
            UserManager<AppUser> userManager,
            IEmailService emailService,
            IConfiguration configuration,
            IValidator<ResendConfirmationRequest> validator) =>
        {
            var vr = await validator.ValidateAsync(req);
            if (!vr.IsValid) return Results.BadRequest(vr.Errors.Select(e => e.ErrorMessage));
            var user = await userManager.FindByEmailAsync(req.Email.Trim().ToLowerInvariant());
            if (user is null) return Results.Ok(new { message = "If the account exists, a confirmation email has been sent" });
            if (user.EmailConfirmed) return Results.BadRequest(new { code = "EMAIL_ALREADY_CONFIRMED", message = "Email is already confirmed" });
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            var baseUrl = configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
            await emailService.SendConfirmationEmailAsync(user, token, baseUrl);
            return Results.Ok(new { message = "Confirmation email has been sent" });
        }).WithTags("Auth").AllowAnonymous();

        app.MapPost("/api/auth/forgot-password", async (
            ForgotPasswordRequest req,
            UserManager<AppUser> userManager,
            IEmailService emailService,
            IConfiguration configuration,
            IValidator<ForgotPasswordRequest> validator) =>
        {
            var vr = await validator.ValidateAsync(req);
            if (!vr.IsValid) return Results.BadRequest(vr.Errors.Select(e => e.ErrorMessage));
            var user = await userManager.FindByEmailAsync(req.Email.Trim().ToLowerInvariant());
            if (user is null) return Results.Ok(new { message = "If the account exists, a reset link has been generated" });
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var baseUrl = configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
            await emailService.SendPasswordResetEmailAsync(user, token, baseUrl);
            return Results.Ok(new { message = "Password reset email has been sent" });
        }).WithTags("Auth").AllowAnonymous();

        app.MapPost("/api/auth/reset-password", async (
            ResetPasswordRequest req,
            UserManager<AppUser> userManager,
            IValidator<ResetPasswordRequest> validator) =>
        {
            var vr = await validator.ValidateAsync(req);
            if (!vr.IsValid) return Results.BadRequest(vr.Errors.Select(e => e.ErrorMessage));
            var user = await userManager.FindByEmailAsync(req.Email.Trim().ToLowerInvariant());
            if (user is null) return Results.BadRequest(new { code = "USER_NOT_FOUND", message = "User not found" });
            var decoded = Uri.UnescapeDataString(req.Token);
            var result = await userManager.ResetPasswordAsync(user, decoded, req.NewPassword);
            if (!result.Succeeded) return Results.BadRequest(new { code = "RESET_FAILED", errors = result.Errors.Select(e => e.Description) });
            return Results.Ok(new StandardMessageResponse("Password has been reset successfully"));
        }).WithTags("Auth").AllowAnonymous();

        app.MapGet("/api/auth/me", async (System.Security.Claims.ClaimsPrincipal user, UserManager<AppUser> userManager) =>
        {
            var id = user.RequireUserId();
            var entity = await userManager.Users.Include(u => u.Store).FirstOrDefaultAsync(u => u.Id == id);
            if (entity is null) return Results.NotFound(ApiErrors.Problem("User not found", 404));
            var roles = await userManager.GetRolesAsync(entity);
            var role = roles.FirstOrDefault() ?? UserRole.Buyer;
            return Results.Ok(ToAuthUser(entity, role));
        }).WithTags("Auth").RequireAuthorization();

        app.MapPut("/api/users/me", async (UserUpdateRequest req, System.Security.Claims.ClaimsPrincipal user, UserManager<AppUser> userManager) =>
        {
            var id = user.RequireUserId();
            var entity = await userManager.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (entity is null) return Results.NotFound(ApiErrors.Problem("User not found", 404));

            if (!string.IsNullOrWhiteSpace(req.DisplayName))
                entity.DisplayName = req.DisplayName.Trim();
            if (req.PhoneNumber is not null)
                entity.PhoneNumber = req.PhoneNumber;
            if (req.Birthday.HasValue)
                entity.Birthday = DateTime.SpecifyKind(req.Birthday.Value, DateTimeKind.Utc);

            await userManager.UpdateAsync(entity);
            return Results.Ok(new UserResponse(entity.Id, entity.Email ?? "", entity.DisplayName, entity.PhoneNumber, entity.Birthday));
        }).WithTags("Users").RequireAuthorization();

        app.MapGet("/api/addresses", async (System.Security.Claims.ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var userId = user.RequireUserId();
            var items = await db.Addresses.AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.IsDefault).ThenBy(x => x.CreatedAtUtc).ToListAsync(ct);
            return Results.Ok(items.Select(x => new { x.Id, x.Name, x.Phone, x.Region, x.City, x.Line1, x.Line2, x.PostalCode, x.IsDefault }));
        }).WithTags("Addresses").RequireAuthorization();

        app.MapPost("/api/addresses", async (AddressCreateRequest req, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, IValidator<AddressCreateRequest> validator, CancellationToken ct) =>
        {
            var vr = await validator.ValidateAsync(req, ct);
            if (!vr.IsValid) return Results.BadRequest(vr.Errors.Select(e => e.ErrorMessage));
            var userId = user.RequireUserId();
            var address = new Address
            {
                UserId = userId,
                Name = req.Name.Trim(),
                Phone = req.Phone.Trim(),
                Region = req.Region.Trim(),
                City = req.City.Trim(),
                Line1 = req.Line1.Trim(),
                Line2 = string.IsNullOrWhiteSpace(req.Line2) ? null : req.Line2.Trim(),
                PostalCode = req.PostalCode.Trim(),
                IsDefault = req.IsDefault,
                CreatedAtUtc = DateTime.UtcNow
            };
            if (req.IsDefault)
            {
                var existing = await db.Addresses.Where(x => x.UserId == userId && x.IsDefault).ToListAsync(ct);
                foreach (var e in existing) e.IsDefault = false;
            }
            db.Addresses.Add(address);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/addresses/{address.Id}", new { address.Id, address.Name, address.Phone, address.Region, address.City, address.Line1, address.Line2, address.PostalCode, address.IsDefault });
        }).WithTags("Addresses").RequireAuthorization();

        app.MapPut("/api/addresses/{id:guid}", async (Guid id, AddressUpdateRequest req, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, IValidator<AddressUpdateRequest> validator, CancellationToken ct) =>
        {
            var vr = await validator.ValidateAsync(req, ct);
            if (!vr.IsValid) return Results.BadRequest(vr.Errors.Select(e => e.ErrorMessage));
            var userId = user.RequireUserId();
            var address = await db.Addresses.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
            if (address is null) return Results.NotFound(ApiErrors.Problem("Address not found", 404));

            address.Name = req.Name?.Trim() ?? address.Name;
            address.Phone = req.Phone?.Trim() ?? address.Phone;
            address.Region = req.Region?.Trim() ?? address.Region;
            address.City = req.City?.Trim() ?? address.City;
            address.Line1 = req.Line1?.Trim() ?? address.Line1;
            address.Line2 = string.IsNullOrWhiteSpace(req.Line2) ? null : req.Line2.Trim();
            address.PostalCode = req.PostalCode?.Trim() ?? address.PostalCode;
            address.UpdatedAtUtc = DateTime.UtcNow;

            if (req.IsDefault && !address.IsDefault)
            {
                var existing = await db.Addresses.Where(x => x.UserId == userId && x.IsDefault && x.Id != id).ToListAsync(ct);
                foreach (var e in existing) e.IsDefault = false;
                address.IsDefault = true;
            }

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { address.Id, address.Name, address.Phone, address.Region, address.City, address.Line1, address.Line2, address.PostalCode, address.IsDefault });
        }).WithTags("Addresses").RequireAuthorization();

        app.MapDelete("/api/addresses/{id:guid}", async (Guid id, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var userId = user.RequireUserId();
            var address = await db.Addresses.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
            if (address is null) return Results.NotFound(ApiErrors.Problem("Address not found", 404));
            db.Addresses.Remove(address);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithTags("Addresses").RequireAuthorization();

        app.MapPost("/api/addresses/{id:guid}/default", async (Guid id, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var userId = user.RequireUserId();
            var address = await db.Addresses.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
            if (address is null) return Results.NotFound(ApiErrors.Problem("Address not found", 404));

            var existing = await db.Addresses.Where(x => x.UserId == userId && x.IsDefault).ToListAsync(ct);
            foreach (var e in existing) e.IsDefault = false;
            address.IsDefault = true;

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { address.Id, address.IsDefault });
        }).WithTags("Addresses").RequireAuthorization();

        app.MapGet("/api/cart", async (System.Security.Claims.ClaimsPrincipal user, AppDbContext db, IStorageService storage) =>
        {
            var userId = user.RequireUserId();
            var items = await db.CartItems
                .Where(c => c.BuyerUserId == userId)
                .Include(c => c.Product).ThenInclude(p => p.Images)
                .AsNoTracking()
                .OrderByDescending(c => c.CreatedAtUtc)
                .ToListAsync();

            var respItems = items.Select(c =>
            {
                var img = c.Product.Images.OrderBy(i => i.SortOrder).FirstOrDefault();
                var url = img is null ? null : storage.GetPublicUrl(img.FileName);
                var line = c.Product.Price * c.Quantity;
                return new CartItemResponse(c.Id, c.ProductId, c.Product.Title, c.Product.Price, c.Quantity, c.Product.Stock, url, line);
            }).ToList();

            return Results.Ok(new CartResponse(respItems, respItems.Sum(i => i.LineTotal)));
        }).WithTags("Cart").RequireAuthorization(buyer);

        app.MapPost("/api/cart", async (CartAddRequest req, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, IValidator<CartAddRequest> validator) =>
        {
            var vr = await validator.ValidateAsync(req);
            if (!vr.IsValid) return Results.BadRequest(vr.Errors.Select(e => e.ErrorMessage));
            var userId = user.RequireUserId();
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == req.ProductId && p.IsPublished);
            if (product is null) return Results.NotFound(ApiErrors.Problem("Product not found", 404));
            if (product.Stock <= 0) return Results.BadRequest(ApiErrors.Problem("Out of stock"));
            if (req.Quantity > product.Stock) return Results.BadRequest(ApiErrors.Problem("Quantity exceeds stock"));

            var item = await db.CartItems.FirstOrDefaultAsync(c => c.BuyerUserId == userId && c.ProductId == product.Id);
            if (item is null)
            {
                db.CartItems.Add(new CartItem { BuyerUserId = userId, ProductId = product.Id, Quantity = req.Quantity });
            }
            else
            {
                var newQty = item.Quantity + req.Quantity;
                if (newQty > product.Stock) return Results.BadRequest(ApiErrors.Problem("Quantity exceeds stock"));
                item.Quantity = newQty;
            }
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).WithTags("Cart").RequireAuthorization(buyer);

        app.MapPut("/api/cart/{id:guid}", async (Guid id, CartUpdateRequest req, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, IValidator<CartUpdateRequest> validator) =>
        {
            var vr = await validator.ValidateAsync(req);
            if (!vr.IsValid) return Results.BadRequest(vr.Errors.Select(e => e.ErrorMessage));
            var userId = user.RequireUserId();
            var item = await db.CartItems.Include(c => c.Product).FirstOrDefaultAsync(c => c.Id == id && c.BuyerUserId == userId);
            if (item is null) return Results.NotFound(ApiErrors.Problem("Cart item not found", 404));
            if (req.Quantity > item.Product.Stock) return Results.BadRequest(ApiErrors.Problem("Quantity exceeds stock"));
            item.Quantity = req.Quantity;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).WithTags("Cart").RequireAuthorization(buyer);

        app.MapDelete("/api/cart/{id:guid}", async (Guid id, System.Security.Claims.ClaimsPrincipal user, AppDbContext db) =>
        {
            var userId = user.RequireUserId();
            var item = await db.CartItems.FirstOrDefaultAsync(c => c.Id == id && c.BuyerUserId == userId);
            if (item is null) return Results.NotFound(ApiErrors.Problem("Cart item not found", 404));
            db.CartItems.Remove(item);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).WithTags("Cart").RequireAuthorization(buyer);

        app.MapDelete("/api/cart", async (System.Security.Claims.ClaimsPrincipal user, AppDbContext db) =>
        {
            var userId = user.RequireUserId();
            var items = await db.CartItems.Where(c => c.BuyerUserId == userId).ToListAsync();
            db.CartItems.RemoveRange(items);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).WithTags("Cart").RequireAuthorization(buyer);

        app.MapGet("/api/categories", async (AppDbContext db) =>
        {
            var items = await db.Categories.OrderBy(c => c.Name).Select(c => new CategoryResponse(c.Id, c.Name, c.Slug, c.ParentId)).ToListAsync();
            return Results.Ok(items);
        }).WithTags("Categories").AllowAnonymous();

        app.MapGet("/api/categories/tree", async (AppDbContext db) =>
        {
            var all = await db.Categories.OrderBy(c => c.Name).Select(c => new CategoryNode(c.Id, c.Name, c.Slug, c.ParentId, null)).ToListAsync();
            var roots = BuildTree(all);
            return Results.Ok(roots);
        }).WithTags("Categories").AllowAnonymous();

        app.MapPost("/api/categories", async (CategoryCreateRequest req, AppDbContext db, IValidator<CategoryCreateRequest> validator) =>
        {
            var vr = await validator.ValidateAsync(req);
            if (!vr.IsValid) return Results.BadRequest(vr.Errors.Select(e => e.ErrorMessage));
            var slug = Slug.From(req.Name);
            if (await db.Categories.AnyAsync(x => x.Slug == slug))
                slug = (slug + "-" + Guid.NewGuid().ToString("N")).Substring(0, Math.Min(100, slug.Length + 33));
            if (req.ParentId is not null && !await db.Categories.AnyAsync(c => c.Id == req.ParentId))
                return Results.BadRequest(ApiErrors.Problem("Parent category not found"));

            var cat = new Category { Name = req.Name.Trim(), Slug = slug, ParentId = req.ParentId };
            db.Categories.Add(cat);
            await db.SaveChangesAsync();
            return Results.Ok(new CategoryResponse(cat.Id, cat.Name, cat.Slug, cat.ParentId));
        }).WithTags("Categories").RequireAuthorization(admin);

        app.MapPut("/api/categories/{id:guid}", async (Guid id, CategoryUpdateRequest req, AppDbContext db, IValidator<CategoryUpdateRequest> validator) =>
        {
            var vr = await validator.ValidateAsync(req);
            if (!vr.IsValid) return Results.BadRequest(vr.Errors.Select(e => e.ErrorMessage));
            var cat = await db.Categories.FirstOrDefaultAsync(x => x.Id == id);
            if (cat is null) return Results.NotFound(ApiErrors.Problem("Category not found", 404));
            if (req.ParentId == id) return Results.BadRequest(ApiErrors.Problem("Category cannot be its own parent"));
            if (req.ParentId is not null && !await db.Categories.AnyAsync(c => c.Id == req.ParentId)) return Results.BadRequest(ApiErrors.Problem("Parent category not found"));
            cat.Name = req.Name.Trim();
            cat.ParentId = req.ParentId;
            await db.SaveChangesAsync();
            return Results.Ok(new CategoryResponse(cat.Id, cat.Name, cat.Slug, cat.ParentId));
        }).WithTags("Categories").RequireAuthorization(admin);

        app.MapDelete("/api/categories/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var cat = await db.Categories.Include(c => c.Products).FirstOrDefaultAsync(x => x.Id == id);
            if (cat is null) return Results.NotFound(ApiErrors.Problem("Category not found", 404));
            if (cat.Products.Any()) return Results.BadRequest(ApiErrors.Problem("Category has products"));
            if (await db.Categories.AnyAsync(x => x.ParentId == id)) return Results.BadRequest(ApiErrors.Problem("Category has child categories"));
            db.Categories.Remove(cat);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).WithTags("Categories").RequireAuthorization(admin);

        app.MapPost("/api/orders/checkout", async (CheckoutRequest req, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, IValidator<CheckoutRequest> validator) =>
        {
            var vr = await validator.ValidateAsync(req);
            if (!vr.IsValid) return Results.BadRequest(vr.Errors.Select(e => e.ErrorMessage));
            var userId = user.RequireUserId();
            var cart = await db.CartItems.Where(c => c.BuyerUserId == userId).Include(c => c.Product).ThenInclude(p => p.Store).ToListAsync();
            if (!cart.Any()) return Results.BadRequest(ApiErrors.Problem("Cart is empty"));
            foreach (var c in cart)
            {
                if (!c.Product.IsPublished) return Results.BadRequest(ApiErrors.Problem($"Product '{c.Product.Title}' is not available"));
                if (c.Quantity > c.Product.Stock) return Results.BadRequest(ApiErrors.Problem($"Not enough stock for '{c.Product.Title}'"));
            }

            Coupon? coupon = null;
            if (!string.IsNullOrWhiteSpace(req.CouponCode))
            {
                var normalizedCode = req.CouponCode.Trim().ToUpperInvariant();
                coupon = await db.Coupons.FirstOrDefaultAsync(c => 
                    c.Code == normalizedCode && 
                    c.IsActive && 
                    (!c.ExpiresAtUtc.HasValue || c.ExpiresAtUtc > DateTime.UtcNow) &&
                    (c.UsageLimit == null || c.UsedCount < c.UsageLimit));
                
                if (coupon is null) return Results.BadRequest(ApiErrors.Problem("Invalid or expired coupon code"));
            }

            var order = new Order
            {
                BuyerUserId = userId,
                Status = OrderStatus.Pending,
                BuyerName = req.BuyerName.Trim(),
                Phone = req.Phone.Trim(),
                City = req.City.Trim(),
                DeliveryAddress = req.DeliveryAddress.Trim(),
                Comment = string.IsNullOrWhiteSpace(req.Comment) ? null : req.Comment.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            };

            var items = new List<OrderItem>();
            decimal subtotal = 0;
            foreach (var c in cart)
            {
                var line = c.Product.Price * c.Quantity;
                subtotal += line;
                items.Add(new OrderItem
                {
                    Order = order,
                    ProductId = c.ProductId,
                    StoreId = c.Product.StoreId,
                    ProductTitleSnapshot = c.Product.Title,
                    UnitPrice = c.Product.Price,
                    Quantity = c.Quantity,
                    LineTotal = line
                });
                c.Product.Stock -= c.Quantity;
                if (c.Product.Stock < 0) c.Product.Stock = 0;
            }

            decimal discount = 0;
            if (coupon is not null)
            {
                if (coupon.DiscountType == DiscountType.Percentage)
                {
                    var cappedPercent = Math.Min(coupon.Discount, 100m);
                    discount = subtotal * (cappedPercent / 100m);
                }
                else
                {
                    discount = coupon.Discount;
                }
                discount = Math.Min(discount, subtotal);
                
                order.Coupons.Add(new OrderCoupon { Coupon = coupon });
                coupon.UsedCount++;
            }

            order.Total = subtotal - discount;
            order.Items = items;
            db.Orders.Add(order);
            db.CartItems.RemoveRange(cart);
            await db.SaveChangesAsync();
            return Results.Ok(await BuildOrderResponse(db, order.Id, userId, true));
        }).WithTags("Orders").RequireAuthorization(buyer);

        app.MapGet("/api/orders/my", async (System.Security.Claims.ClaimsPrincipal user, AppDbContext db, int page = 1, int pageSize = 20) =>
        {
            var userId = user.RequireUserId();
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var query = db.Orders.AsNoTracking().Where(o => o.BuyerUserId == userId).OrderByDescending(o => o.CreatedAtUtc);
            var total = await query.CountAsync();
            var ids = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(o => o.Id).ToListAsync();
            var items = new List<OrderResponse>();
            foreach (var id in ids) items.Add(await BuildOrderResponse(db, id, userId, true));
            return Results.Ok(new OrderListResponse(items, page, pageSize, total));
        }).WithTags("Orders").RequireAuthorization(buyer);

        app.MapGet("/api/orders/{id:guid}", async (Guid id, System.Security.Claims.ClaimsPrincipal user, AppDbContext db) =>
        {
            var userId = user.RequireUserId();
            var isBuyer = user.IsInRole(UserRole.Buyer);
            var isSeller = user.IsInRole(UserRole.Seller);
            var isAdmin = user.IsInRole(UserRole.Admin);
            if (isBuyer)
            {
                var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id && o.BuyerUserId == userId);
                if (order is null) return Results.NotFound(ApiErrors.Problem("Order not found", 404));
                return Results.Ok(await BuildOrderResponse(db, id, userId, true));
            }
            if (isSeller)
            {
                var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.OwnerUserId == userId);
                if (store is null) return Results.NotFound(ApiErrors.Problem("Store not found", 404));
                var hasAny = await db.OrderItems.AsNoTracking().AnyAsync(i => i.OrderId == id && i.StoreId == store.Id);
                if (!hasAny) return Results.NotFound(ApiErrors.Problem("Order not found", 404));
                return Results.Ok(await BuildOrderResponseForSeller(db, id, store.Id));
            }
            if (isAdmin)
            {
                var exists = await db.Orders.AsNoTracking().AnyAsync(o => o.Id == id);
                if (!exists) return Results.NotFound(ApiErrors.Problem("Order not found", 404));
                return Results.Ok(await BuildOrderResponseForAdmin(db, id));
            }
            return Results.Forbid();
        }).WithTags("Orders").RequireAuthorization();

        app.MapGet("/api/orders/seller", async (System.Security.Claims.ClaimsPrincipal user, AppDbContext db, int page = 1, int pageSize = 20) =>
        {
            var userId = user.RequireUserId();
            var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.OwnerUserId == userId);
            if (store is null) return Results.NotFound(ApiErrors.Problem("Store not found", 404));
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var query = db.Orders.AsNoTracking().Where(o => o.Items.Any(i => i.StoreId == store.Id)).OrderByDescending(o => o.CreatedAtUtc);
            var total = await query.CountAsync();
            var ids = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(o => o.Id).ToListAsync();
            var items = new List<OrderResponse>();
            foreach (var id in ids) items.Add(await BuildOrderResponseForSeller(db, id, store.Id));
            return Results.Ok(new OrderListResponse(items, page, pageSize, total));
        }).WithTags("Orders").RequireAuthorization(seller);

        app.MapGet("/api/orders/seller/stats", async (System.Security.Claims.ClaimsPrincipal user, AppDbContext db) =>
        {
            var userId = user.RequireUserId();
            var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.OwnerUserId == userId);
            if (store is null) return Results.NotFound(ApiErrors.Problem("Store not found", 404));

            var orders = db.Orders.Where(o => o.Items.Any(i => i.StoreId == store.Id));
            var totalOrders = await orders.CountAsync();
            var pendingOrders = await orders.CountAsync(o => o.Status == OrderStatus.Pending);
            var completedOrders = await orders.CountAsync(o => o.Status == OrderStatus.Completed);
            var cancelledOrders = await orders.CountAsync(o => o.Status == OrderStatus.Cancelled);
            var totalRevenue = await db.OrderItems
                .Where(i => i.StoreId == store.Id && i.Order.Status != OrderStatus.Cancelled && i.Order.Status != OrderStatus.Refunded)
                .Select(i => (decimal)i.LineTotal)
                .ToListAsync();
            var total = totalRevenue.Sum();

            return Results.Ok(new { totalOrders, pendingOrders, completedOrders, cancelledOrders, totalRevenue = total });
        }).WithTags("Orders").RequireAuthorization(seller);

        app.MapPut("/api/orders/{id:guid}/status", async (Guid id, OrderStatus status, System.Security.Claims.ClaimsPrincipal user, AppDbContext db) =>
        {
            var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
            if (order is null) return Results.NotFound(ApiErrors.Problem("Order not found", 404));
            if (user.IsInRole(UserRole.Seller))
            {
                var userId = user.RequireUserId();
                var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.OwnerUserId == userId);
                if (store is null) return Results.NotFound(ApiErrors.Problem("Store not found", 404));
                var hasItems = order.Items.Any(i => i.StoreId == store.Id);
                if (!hasItems) return Results.Forbid();

                if (status is OrderStatus.Cancelled or OrderStatus.Refunded or OrderStatus.ReturnApproved or OrderStatus.ReturnRejected)
                {
                    return Results.BadRequest(ApiErrors.Problem("Seller cannot set this status directly"));
                }
            }

            var validTransition = (order.Status, status) switch
            {
                (OrderStatus.Pending, OrderStatus.Confirmed) => true,
                (OrderStatus.Pending, OrderStatus.Cancelled) => true,
                (OrderStatus.Confirmed, OrderStatus.Shipped) => true,
                (OrderStatus.Confirmed, OrderStatus.Cancelled) => true,
                (OrderStatus.Shipped, OrderStatus.Completed) => true,
                (OrderStatus.Completed, OrderStatus.Refunded) => true,
                _ => false
            };

            if (!validTransition) return Results.BadRequest(ApiErrors.Problem("Invalid order status transition"));

            order.Status = status;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).WithTags("Orders").RequireAuthorization(sellerOrAdmin);

        app.MapPost("/api/orders/{id:guid}/cancel", async (Guid id, System.Security.Claims.ClaimsPrincipal user, AppDbContext db) =>
        {
            var userId = user.RequireUserId();
            var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id && o.BuyerUserId == userId);
            if (order is null) return Results.NotFound(ApiErrors.Problem("Order not found", 404));
            if (order.Status == OrderStatus.Cancelled)
                return Results.BadRequest(ApiErrors.Problem("Order is already cancelled"));
            if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.Confirmed)
                return Results.BadRequest(ApiErrors.Problem("Cannot cancel order in current status"));

            var hasCompletedPayment = await db.Payments.AnyAsync(p => p.OrderId == id && p.Status == PaymentStatus.Completed);
            if (hasCompletedPayment)
                return Results.BadRequest(ApiErrors.Problem("Cannot cancel order with completed payment. Please request a refund instead."));

            order.Status = OrderStatus.Cancelled;
            foreach (var item in order.Items)
            {
                var product = await db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId);
                if (product is not null) product.Stock += item.Quantity;
            }
            await db.SaveChangesAsync();
            return Results.Ok(new { order.Id, order.Status });
        }).WithTags("Orders").RequireAuthorization();

        app.MapPost("/api/orders/{id:guid}/ship", async (Guid id, OrderShipRequest req, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, IValidator<OrderShipRequest> validator) =>
        {
            var vr = await validator.ValidateAsync(req);
            if (!vr.IsValid) return Results.BadRequest(vr.Errors.Select(e => e.ErrorMessage));
            var userId = user.RequireUserId();
            var store = await db.Stores.FirstOrDefaultAsync(s => s.OwnerUserId == userId);
            if (store is null) return Results.Forbid();

            var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id && o.Items.Any(i => i.StoreId == store.Id));
            if (order is null) return Results.NotFound(ApiErrors.Problem("Order not found", 404));
            if (order.Status != OrderStatus.Confirmed)
                return Results.BadRequest(ApiErrors.Problem("Order must be confirmed before shipping"));

            order.Status = OrderStatus.Shipped;
            order.TrackingNumber = req.TrackingNumber?.Trim();
            await db.SaveChangesAsync();
            return Results.Ok(new { order.Id, order.Status, order.TrackingNumber });
        }).WithTags("Orders").RequireAuthorization(seller);

        app.MapPost("/api/orders/{id:guid}/return", async (Guid id, System.Security.Claims.ClaimsPrincipal user, AppDbContext db) =>
        {
            var userId = user.RequireUserId();
            var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id && o.BuyerUserId == userId);
            if (order is null) return Results.NotFound(ApiErrors.Problem("Order not found", 404));
            if (order.Status != OrderStatus.Completed && order.Status != OrderStatus.Shipped)
                return Results.BadRequest(ApiErrors.Problem("Cannot request return for this order"));

            order.Status = OrderStatus.ReturnRequested;
            await db.SaveChangesAsync();
            return Results.Ok(new { order.Id, order.Status });
        }).WithTags("Orders").RequireAuthorization();

        app.MapPost("/api/orders/{id:guid}/return/approve", async (Guid id, System.Security.Claims.ClaimsPrincipal user, AppDbContext db) =>
        {
            var userId = user.RequireUserId();
            var store = await db.Stores.FirstOrDefaultAsync(s => s.OwnerUserId == userId);
            if (store is null) return Results.Forbid();

            var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id && o.Items.Any(i => i.StoreId == store.Id));
            if (order is null) return Results.NotFound(ApiErrors.Problem("Order not found", 404));
            if (order.Status != OrderStatus.ReturnRequested)
                return Results.BadRequest(ApiErrors.Problem("No return request found"));

            var sellerItems = order.Items.Where(i => i.StoreId == store.Id).ToList();
            foreach (var item in sellerItems)
            {
                item.ReturnApprovedAtUtc = DateTime.UtcNow;
            }

            var allApproved = order.Items.All(i => i.ReturnApprovedAtUtc.HasValue);
            if (allApproved)
            {
                order.Status = OrderStatus.ReturnApproved;
                foreach (var item in order.Items)
                {
                    var product = await db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId);
                    if (product is not null) product.Stock += item.Quantity;
                }
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { order.Id, order.Status });
        }).WithTags("Orders").RequireAuthorization(seller);

        app.MapPost("/api/orders/{id:guid}/return/reject", async (Guid id, System.Security.Claims.ClaimsPrincipal user, AppDbContext db) =>
        {
            var userId = user.RequireUserId();
            var store = await db.Stores.FirstOrDefaultAsync(s => s.OwnerUserId == userId);
            if (store is null) return Results.Forbid();

            var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id && o.Items.Any(i => i.StoreId == store.Id));
            if (order is null) return Results.NotFound(ApiErrors.Problem("Order not found", 404));
            if (order.Status != OrderStatus.ReturnRequested)
                return Results.BadRequest(ApiErrors.Problem("No return request found"));

            order.Status = OrderStatus.ReturnRejected;
            await db.SaveChangesAsync();
            return Results.Ok(new { order.Id, order.Status });
        }).WithTags("Orders").RequireAuthorization(seller);

        app.MapPost("/api/orders/{id:guid}/refund", async (Guid id, System.Security.Claims.ClaimsPrincipal user, AppDbContext db) =>
        {
            var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
            if (order is null) return Results.NotFound(ApiErrors.Problem("Order not found", 404));
            if (order.Status != OrderStatus.ReturnApproved)
                return Results.BadRequest(ApiErrors.Problem("Order must be approved for return first"));

            order.Status = OrderStatus.Refunded;
            foreach (var item in order.Items)
            {
                var product = await db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId);
                if (product is not null) product.Stock += item.Quantity;
            }
            await db.SaveChangesAsync();
            return Results.Ok(new { order.Id, order.Status });
        }).WithTags("Orders").RequireAuthorization(admin);

        app.MapGet("/api/reviews/product/{productId:guid}", async (Guid productId, AppDbContext db) =>
        {
            var exists = await db.Products.AsNoTracking().AnyAsync(p => p.Id == productId);
            if (!exists) return Results.NotFound(ApiErrors.Problem("Product not found", 404));
            var items = await db.Reviews.AsNoTracking().Where(r => r.ProductId == productId).Include(r => r.BuyerUser).OrderByDescending(r => r.CreatedAtUtc)
                .Select(r => new ReviewResponse(r.Id, r.BuyerUserId, r.BuyerUser.DisplayName, r.Rating, r.Text, r.CreatedAtUtc)).ToListAsync();
            return Results.Ok(items);
        }).WithTags("Reviews").AllowAnonymous();

        app.MapPost("/api/reviews", async (ReviewCreateRequest req, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, IValidator<ReviewCreateRequest> validator) =>
        {
            var vr = await validator.ValidateAsync(req);
            if (!vr.IsValid) return Results.BadRequest(vr.Errors.Select(e => e.ErrorMessage));
            var userId = user.RequireUserId();
            var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == req.ProductId && p.IsPublished);
            if (product is null) return Results.NotFound(ApiErrors.Problem("Product not found", 404));
            var hasPurchased = await db.OrderItems.AsNoTracking()
                .AnyAsync(i => i.ProductId == req.ProductId && i.Order.BuyerUserId == userId && i.Order.Status == OrderStatus.Completed);
            if (!hasPurchased) return Results.BadRequest(ApiErrors.Problem("You can review only purchased products"));
            var exists = await db.Reviews.AnyAsync(r => r.ProductId == req.ProductId && r.BuyerUserId == userId);
            if (exists) return Results.BadRequest(ApiErrors.Problem("Review already exists"));
            db.Reviews.Add(new Review { ProductId = req.ProductId, BuyerUserId = userId, Rating = req.Rating, Text = req.Text.Trim(), CreatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).WithTags("Reviews").RequireAuthorization(buyer);

        app.MapDelete("/api/reviews/{id:guid}", async (Guid id, System.Security.Claims.ClaimsPrincipal user, AppDbContext db) =>
        {
            var review = await db.Reviews.FirstOrDefaultAsync(r => r.Id == id);
            if (review is null) return Results.NotFound(ApiErrors.Problem("Review not found", 404));
            if (user.IsInRole(UserRole.Buyer) && review.BuyerUserId != user.RequireUserId()) return Results.Forbid();
            db.Reviews.Remove(review);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).WithTags("Reviews").RequireAuthorization(new AuthorizeAttribute { Roles = $"{UserRole.Buyer},{UserRole.Admin}" });

        app.MapGet("/api/stores", async (AppDbContext db) =>
        {
            var items = await db.Stores.OrderByDescending(s => s.CreatedAtUtc).Select(s => new StoreProfile(s.Id, s.Name, s.Slug, s.Description)).ToListAsync();
            return Results.Ok(items);
        }).WithTags("Stores").AllowAnonymous();

        app.MapGet("/api/stores/{slug}", async (string slug, AppDbContext db) =>
        {
            var store = await db.Stores.FirstOrDefaultAsync(s => s.Slug == slug);
            return store is null ? Results.NotFound(ApiErrors.Problem("Store not found", 404)) : Results.Ok(new StoreProfile(store.Id, store.Name, store.Slug, store.Description));
        }).WithTags("Stores").AllowAnonymous();

        app.MapPost("/api/stores", async (StoreCreateRequest req, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, UserManager<AppUser> userManager, IValidator<StoreCreateRequest> validator) =>
        {
            var vr = await validator.ValidateAsync(req);
            if (!vr.IsValid) return Results.BadRequest(vr.Errors.Select(e => e.ErrorMessage));
            var userId = user.RequireUserId();
            var existingStore = await db.Stores.FirstOrDefaultAsync(s => s.OwnerUserId == userId);
            if (existingStore is not null) return Results.BadRequest(ApiErrors.Problem("Store already exists"));

            var trimmedName = req.Name.Trim();
            var nameExists = await db.Stores.AnyAsync(s => s.Name == trimmedName);
            if (nameExists) return Results.BadRequest(ApiErrors.Problem("A store with this name already exists"));

            var slug = Slug.From(trimmedName);
            var slugExists = await db.Stores.AnyAsync(s => s.Slug == slug);
            if (slugExists) slug = slug + "-" + Guid.NewGuid().ToString("N")[..8];

            var store = new Store
            {
                OwnerUserId = userId,
                Name = trimmedName,
                Slug = slug,
                Description = (req.Description ?? string.Empty).Trim(),
                ContactEmail = req.ContactEmail?.Trim(),
                ContactPhone = req.ContactPhone?.Trim(),
                Region = req.Region?.Trim(),
                City = req.City?.Trim(),
                Street = req.Street?.Trim(),
                PostalCode = req.PostalCode?.Trim(),
                IsApproved = true,
                IsFeatured = false,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Stores.Add(store);
            await db.SaveChangesAsync();

            return Results.Created($"/api/stores/{store.Slug}", new StoreProfile(store.Id, store.Name, store.Slug, store.Description));
        }).WithTags("Stores").RequireAuthorization(seller);

        app.MapPut("/api/stores/me", async (StoreProfile req, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, IValidator<StoreProfile> validator) =>
        {
            var vr = await validator.ValidateAsync(req);
            if (!vr.IsValid) return Results.BadRequest(vr.Errors.Select(e => e.ErrorMessage));
            var userId = user.RequireUserId();
            var store = await db.Stores.FirstOrDefaultAsync(s => s.OwnerUserId == userId);
            if (store is null) return Results.NotFound(ApiErrors.Problem("Store not found", 404));
            store.Name = req.Name.Trim();
            store.Description = (req.Description ?? string.Empty).Trim();
            await db.SaveChangesAsync();
            return Results.Ok(new StoreProfile(store.Id, store.Name, store.Slug, store.Description));
        }).WithTags("Stores").RequireAuthorization(seller);

        app.MapGet("/api/admin/stats", async (AppDbContext db, UserManager<AppUser> userManager) =>
        {
            var users = await userManager.Users.CountAsync();
            var stores = await db.Stores.CountAsync();
            var products = await db.Products.CountAsync();
            var orders = await db.Orders.CountAsync();
            var revenueQuery = await db.Orders.Where(o => o.Status != OrderStatus.Cancelled).Select(o => (decimal)o.Total).ToListAsync();
            var revenue = revenueQuery.Sum();
            return Results.Ok(new { users, stores, products, orders, revenue });
        }).WithTags("Admin").RequireAuthorization(admin);

        app.MapGet("/api/admin/users", async (UserManager<AppUser> userManager, int take = 200) =>
        {
            take = Math.Clamp(take, 1, 500);
            var list = await userManager.Users.AsNoTracking().OrderByDescending(u => u.CreatedAtUtc).Take(take).ToListAsync();
            var result = new List<object>();
            foreach (var u in list)
            {
                var roles = await userManager.GetRolesAsync(u);
                result.Add(new { u.Id, u.Email, u.DisplayName, roles });
            }
            return Results.Ok(result);
        }).WithTags("Admin").RequireAuthorization(admin);

        app.MapPost("/api/admin/set-role", async (Guid userId, string role, UserManager<AppUser> userManager) =>
        {
            role = role.Trim();
            if (role is not (UserRole.Admin or UserRole.Seller or UserRole.Buyer)) return Results.BadRequest(ApiErrors.Problem("Invalid role"));
            var user = await userManager.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null) return Results.NotFound(ApiErrors.Problem("User not found", 404));
            var roles = await userManager.GetRolesAsync(user);
            foreach (var r in roles) await userManager.RemoveFromRoleAsync(user, r);
            await userManager.AddToRoleAsync(user, role);
            return Results.NoContent();
        }).WithTags("Admin").RequireAuthorization(admin);

        app.MapGet("/api/admin/products/pending", async (AppDbContext db, CancellationToken ct, int take = 50, int skip = 0) =>
        {
            var products = await db.Products
                .AsNoTracking()
                .Include(p => p.Store).ThenInclude(s => s!.OwnerUser)
                .Where(p => p.Status == ProductStatus.Pending)
                .OrderBy(p => p.CreatedAtUtc)
                .Skip(skip)
                .Take(take)
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.Price,
                    p.Stock,
                    p.CreatedAtUtc,
                    StoreName = p.Store!.Name,
                    SellerEmail = p.Store.OwnerUser!.Email,
                    SellerName = p.Store.OwnerUser.DisplayName
                })
                .ToListAsync(ct);
            var total = await db.Products.CountAsync(p => p.Status == ProductStatus.Pending, ct);
            return Results.Ok(new { products, total });
        }).WithTags("Admin").RequireAuthorization(admin);

        app.MapPost("/api/admin/products/{id:guid}/approve", async (Guid id, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, IEmailService email, INotificationService notification, CancellationToken ct) =>
        {
            var adminId = user.RequireUserId();
            var product = await db.Products.Include(p => p.Store).ThenInclude(s => s!.OwnerUser).FirstOrDefaultAsync(p => p.Id == id, ct);
            if (product is null) return Results.NotFound(ApiErrors.Problem("Product not found", 404));
            if (product.Status != ProductStatus.Pending) return Results.BadRequest(ApiErrors.Problem("Product is not pending"));

            product.Status = ProductStatus.Approved;
            product.IsPublished = true;
            product.ApprovedByUserId = adminId;
            product.ApprovedAtUtc = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            if (product.Store?.OwnerUser != null)
            {
                await notification.NotifyProductApprovedAsync(product.Store.OwnerUser.Id, product.Title);
                await email.SendProductApprovedNotificationAsync(product.Store.OwnerUser, product.Title);
            }

            return Results.Ok(new { product.Id, product.Status });
        }).WithTags("Admin").RequireAuthorization(admin);

        app.MapPost("/api/admin/products/{id:guid}/reject", async (Guid id, ProductRejectRequest req, System.Security.Claims.ClaimsPrincipal user, AppDbContext db, IEmailService email, INotificationService notification, CancellationToken ct) =>
        {
            var adminId = user.RequireUserId();
            var product = await db.Products.Include(p => p.Store).ThenInclude(s => s!.OwnerUser).FirstOrDefaultAsync(p => p.Id == id, ct);
            if (product is null) return Results.NotFound(ApiErrors.Problem("Product not found", 404));
            if (product.Status != ProductStatus.Pending) return Results.BadRequest(ApiErrors.Problem("Product is not pending"));

            product.Status = ProductStatus.Rejected;
            product.ApprovedByUserId = adminId;
            product.ApprovedAtUtc = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            if (product.Store?.OwnerUser != null)
            {
                var reason = req.Reason?.Trim() ?? "Порушення правил маркетплейсу";
                await notification.NotifyProductRejectedAsync(product.Store.OwnerUser.Id, product.Title, reason);
                await email.SendProductRejectedNotificationAsync(product.Store.OwnerUser, product.Title, reason);
            }

            return Results.Ok(new { product.Id, product.Status });
        }).WithTags("Admin").RequireAuthorization(admin);

        app.MapGet("/api/images/{fileName}", async (string fileName, IStorageService storage, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(fileName)) return Results.BadRequest(ApiErrors.Problem("fileName required"));
            var file = await storage.OpenReadAsync(fileName, ct);
            if (file is null) return Results.NotFound(ApiErrors.Problem("File not found", 404));
            return Results.Stream(file.Value.stream, file.Value.contentType);
        }).WithTags("Images").AllowAnonymous();


        return app;
    }

    private static async Task<OrderResponse> BuildOrderResponse(AppDbContext db, Guid orderId, Guid userId, bool isBuyer)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.Product).ThenInclude(p => p!.Store)
            .FirstAsync(o => o.Id == orderId);

        var items = order.Items.Select(i => new OrderItemResponse(
            i.ProductId,
            i.Product?.Title ?? "",
            i.StoreId,
            i.Product?.Store?.Name ?? "",
            i.UnitPrice,
            i.Quantity,
            i.UnitPrice * i.Quantity
        )).ToList();

        return new OrderResponse(
            order.Id,
            order.Status,
            order.Total,
            order.CreatedAtUtc,
            order.BuyerName,
            order.Phone,
            order.City,
            order.DeliveryAddress,
            order.Comment,
            order.TrackingNumber,
            items);
    }

    private static async Task<OrderResponse> BuildOrderResponseForSeller(AppDbContext db, Guid orderId, Guid storeId)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.Product).ThenInclude(p => p!.Store)
            .FirstAsync(o => o.Id == orderId);

        var items = order.Items.Where(i => i.StoreId == storeId).Select(i => new OrderItemResponse(
            i.ProductId,
            i.Product?.Title ?? "",
            i.StoreId,
            i.Product?.Store?.Name ?? "",
            i.UnitPrice,
            i.Quantity,
            i.UnitPrice * i.Quantity
        )).ToList();

        var sellerTotal = items.Sum(i => i.LineTotal);

        return new OrderResponse(
            order.Id,
            order.Status,
            sellerTotal,
            order.CreatedAtUtc,
            order.BuyerName,
            order.Phone,
            order.City,
            order.DeliveryAddress,
            order.Comment,
            order.TrackingNumber,
            items);
    }

    private static async Task<OrderResponse> BuildOrderResponseForAdmin(AppDbContext db, Guid orderId)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.Product).ThenInclude(p => p!.Store)
            .FirstAsync(o => o.Id == orderId);

        var items = order.Items.Select(i => new OrderItemResponse(
            i.ProductId,
            i.Product?.Title ?? "",
            i.StoreId,
            i.Product?.Store?.Name ?? "",
            i.UnitPrice,
            i.Quantity,
            i.UnitPrice * i.Quantity
        )).ToList();

        return new OrderResponse(
            order.Id,
            order.Status,
            order.Total,
            order.CreatedAtUtc,
            order.BuyerName,
            order.Phone,
            order.City,
            order.DeliveryAddress,
            order.Comment,
            order.TrackingNumber,
            items);
    }
    private sealed record CategoryNode(Guid Id, string Name, string Slug, Guid? ParentId, List<CategoryNode>? Children = null);

    private static List<object> BuildTree(List<CategoryNode> all)
    {
        var lookup = all.ToDictionary(x => x.Id);
        var roots = all.Where(x => x.ParentId == null).ToList();
        return roots.Select(r => BuildNode(r, lookup)).ToList();
    }

    private static object BuildNode(CategoryNode cat, Dictionary<Guid, CategoryNode> lookup)
    {
        var children = lookup.Values.Where(x => x.ParentId == cat.Id).ToList();
        return new { cat.Id, cat.Name, cat.Slug, Children = children.Select(c => BuildNode(c, lookup)).ToList() };
    }
}
