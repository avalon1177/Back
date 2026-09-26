namespace Marketplace.Application.DTO;

public record RegisterRequest(string Email, string Password, string DisplayName, string Role, string? StoreName);
public record LoginRequest(string Email, string Password, string? DeviceId);
public record RefreshTokenRequest(string RefreshToken, string? DeviceId);
public record ExternalLoginRequest(string Provider, string? IdToken, string? AccessToken, string? DeviceId);
public record TwoFactorVerifyRequest(string TwoFactorToken, string Code, string? DeviceId);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Email, string Token, string NewPassword);
public record ConfirmEmailRequest(string Email, string Token);
public record ResendConfirmationRequest(string Email);

public record AuthUserDto(Guid Id, string Email, string Name, string Role, StoreProfile? Store);
public record AuthSessionResponse(AuthUserDto User, string AccessToken, int ExpiresIn);
public record TwoFactorRequiredResponse(string TwoFactorToken, string ErrorCode, string Message);
public record StandardMessageResponse(string Message, string? Code = null, string? Link = null);

public record UserProfile(Guid Id, string Email, string DisplayName, string Role, StoreProfile? Store);
public record StoreProfile(Guid Id, string Name, string Slug, string Description);

public record UserUpdateRequest(string? DisplayName, string? PhoneNumber, DateTime? Birthday);
public record UserResponse(Guid Id, string Email, string DisplayName, string? PhoneNumber, DateTime? Birthday);

public record AddressCreateRequest(string Name, string Phone, string Region, string City, string Line1, string? Line2, string PostalCode, bool IsDefault = false);
public record AddressUpdateRequest(string? Name, string? Phone, string? Region, string? City, string? Line1, string? Line2, string? PostalCode, bool IsDefault = false);

public record StoreCreateRequest(string Name, string? Description, string? ContactEmail, string? ContactPhone, string? Region, string? City, string? Street, string? PostalCode);
