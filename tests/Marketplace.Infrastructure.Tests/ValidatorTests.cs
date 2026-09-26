using FluentAssertions;
using FluentValidation.TestHelper;
using Marketplace.Application.DTO;
using Marketplace.Application.Validation;
using Marketplace.Core.Enums;
using Xunit;

namespace Marketplace.Infrastructure.Tests;

public class AuthValidatorTests
{
    private readonly RegisterRequestValidator _registerValidator = new();
    private readonly LoginRequestValidator _loginValidator = new();
    private readonly ExternalLoginRequestValidator _externalLoginValidator = new();
    private readonly TwoFactorVerifyRequestValidator _twoFactorValidator = new();
    private readonly ResetPasswordRequestValidator _resetPasswordValidator = new();

    [Fact]
    public void RegisterRequest_WithValidData_PassesValidation()
    {
        var request = new RegisterRequest("test@test.com", "Password123!", "Test User", UserRole.Buyer, null);

        var result = _registerValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RegisterRequest_AsSeller_WithStoreName_PassesValidation()
    {
        var request = new RegisterRequest("seller@test.com", "Password123!", "Seller User", UserRole.Seller, "My Store");

        var result = _registerValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-email")]
    [InlineData("@test.com")]
    [InlineData("test@")]
    public void RegisterRequest_WithInvalidEmail_FailsValidation(string invalidEmail)
    {
        var request = new RegisterRequest(invalidEmail, "Password123!", "Test User", UserRole.Buyer, null);

        var result = _registerValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("1234567")]
    public void RegisterRequest_WithShortPassword_FailsValidation(string shortPassword)
    {
        var request = new RegisterRequest("test@test.com", shortPassword, "Test User", UserRole.Buyer, null);

        var result = _registerValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("")]
    public void RegisterRequest_WithInvalidDisplayName_FailsValidation(string invalidName)
    {
        var request = new RegisterRequest("test@test.com", "Password123!", invalidName, UserRole.Buyer, null);

        var result = _registerValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public void RegisterRequest_AsSeller_WithoutStoreName_FailsValidation()
    {
        var request = new RegisterRequest("seller@test.com", "Password123!", "Seller User", UserRole.Seller, null);

        var result = _registerValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.StoreName);
    }

    [Fact]
    public void RegisterRequest_WithInvalidRole_FailsValidation()
    {
        var request = new RegisterRequest("test@test.com", "Password123!", "Test User", "Admin", null);

        var result = _registerValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Role);
    }

    [Fact]
    public void LoginRequest_WithValidData_PassesValidation()
    {
        var request = new LoginRequest("test@test.com", "password123", null);

        var result = _loginValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void LoginRequest_WithInvalidEmail_FailsValidation()
    {
        var request = new LoginRequest("not-an-email", "password123", null);

        var result = _loginValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void LoginRequest_WithEmptyPassword_FailsValidation()
    {
        var request = new LoginRequest("test@test.com", "", null);

        var result = _loginValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void ExternalLoginRequest_WithIdToken_PassesValidation()
    {
        var request = new ExternalLoginRequest("Google", "some-id-token", null, null);

        var result = _externalLoginValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ExternalLoginRequest_WithAccessToken_PassesValidation()
    {
        var request = new ExternalLoginRequest("Google", null, "some-access-token", null);

        var result = _externalLoginValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ExternalLoginRequest_WithNoTokens_FailsValidation()
    {
        var request = new ExternalLoginRequest("Google", null, null, null);

        var result = _externalLoginValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x);
    }

    [Fact]
    public void TwoFactorVerifyRequest_WithValidCode_PassesValidation()
    {
        var request = new TwoFactorVerifyRequest("token123", "123456", null);

        var result = _twoFactorValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("123456789")]
    [InlineData("abc")]
    public void TwoFactorVerifyRequest_WithInvalidCodeLength_FailsValidation(string invalidCode)
    {
        var request = new TwoFactorVerifyRequest("token123", invalidCode, null);

        var result = _twoFactorValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Code);
    }

    [Fact]
    public void ResetPasswordRequest_WithValidData_PassesValidation()
    {
        var request = new ResetPasswordRequest("test@test.com", "reset-token", "NewPassword123!");

        var result = _resetPasswordValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ResetPasswordRequest_WithShortPassword_FailsValidation()
    {
        var request = new ResetPasswordRequest("test@test.com", "reset-token", "short");

        var result = _resetPasswordValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.NewPassword);
    }
}

public class ProductValidatorTests
{
    private readonly ProductCreateValidator _createValidator = new();
    private readonly ProductUpdateValidator _updateValidator = new();
    private static readonly Guid ValidCategoryId = Guid.NewGuid();

    [Fact]
    public void ProductCreateRequest_WithValidData_PassesValidation()
    {
        var request = new ProductCreateRequest(ValidCategoryId, "Test Product", "Valid description", 99.99m, 10, true);

        var result = _createValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    public void ProductCreateRequest_WithTooShortTitle_FailsValidation(string shortTitle)
    {
        var request = new ProductCreateRequest(ValidCategoryId, shortTitle, "Valid description", 99.99m, 10, true);

        var result = _createValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void ProductCreateRequest_WithTooLongTitle_FailsValidation()
    {
        var request = new ProductCreateRequest(ValidCategoryId, new string('a', 141), "Valid description", 99.99m, 10, true);

        var result = _createValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("a")]
    public void ProductCreateRequest_WithTooShortDescription_FailsValidation(string shortDescription)
    {
        var request = new ProductCreateRequest(ValidCategoryId, "Valid Title", shortDescription, 99.99m, 10, true);

        var result = _createValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void ProductCreateRequest_WithTooLongDescription_FailsValidation()
    {
        var request = new ProductCreateRequest(ValidCategoryId, "Valid Title", new string('a', 6001), 99.99m, 10, true);

        var result = _createValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void ProductCreateRequest_WithZeroOrNegativePrice_FailsValidation(decimal invalidPrice)
    {
        var request = new ProductCreateRequest(ValidCategoryId, "Valid Title", "Valid description", invalidPrice, 10, true);

        var result = _createValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Price);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void ProductCreateRequest_WithNegativeStock_FailsValidation(int invalidStock)
    {
        var request = new ProductCreateRequest(ValidCategoryId, "Valid Title", "Valid description", 99.99m, invalidStock, true);

        var result = _createValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Stock);
    }

    [Fact]
    public void ProductCreateRequest_WithZeroStock_PassesValidation()
    {
        var request = new ProductCreateRequest(ValidCategoryId, "Valid Title", "Valid description", 99.99m, 0, true);

        var result = _createValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ProductUpdateRequest_WithValidData_PassesValidation()
    {
        var request = new ProductUpdateRequest(ValidCategoryId, "Updated Product", "Updated description", 149.99m, 20, true);

        var result = _updateValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class CartValidatorTests
{
    private readonly CartAddValidator _addValidator = new();
    private readonly CartUpdateValidator _updateValidator = new();

    [Fact]
    public void CartAddRequest_WithValidData_PassesValidation()
    {
        var request = new CartAddRequest(Guid.NewGuid(), 5);

        var result = _addValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CartAddRequest_WithZeroQuantity_FailsValidation()
    {
        var request = new CartAddRequest(Guid.NewGuid(), 0);

        var result = _addValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Quantity);
    }

    [Fact]
    public void CartAddRequest_WithNegativeQuantity_FailsValidation()
    {
        var request = new CartAddRequest(Guid.NewGuid(), -1);

        var result = _addValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Quantity);
    }

    [Fact]
    public void CartAddRequest_WithQuantityOver100_FailsValidation()
    {
        var request = new CartAddRequest(Guid.NewGuid(), 101);

        var result = _addValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Quantity);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void CartAddRequest_WithValidQuantityRange_PassesValidation(int validQuantity)
    {
        var request = new CartAddRequest(Guid.NewGuid(), validQuantity);

        var result = _addValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CartUpdateRequest_WithValidQuantity_PassesValidation()
    {
        var request = new CartUpdateRequest(5);

        var result = _updateValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void CartUpdateRequest_WithInvalidQuantity_FailsValidation(int invalidQuantity)
    {
        var request = new CartUpdateRequest(invalidQuantity);

        var result = _updateValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Quantity);
    }
}

public class CheckoutValidatorTests
{
    private readonly CheckoutValidator _validator = new();

    [Fact]
    public void CheckoutRequest_WithValidData_PassesValidation()
    {
        var request = new CheckoutRequest("John Doe", "+380501234567", "Kyiv", "123 Main Street", "Please deliver after 5pm");

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CheckoutRequest_WithValidDataAndCoupon_PassesValidation()
    {
        var request = new CheckoutRequest("John Doe", "+380501234567", "Kyiv", "123 Main Street", null, "SAVE10");

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    public void CheckoutRequest_WithTooShortBuyerName_FailsValidation(string shortName)
    {
        var request = new CheckoutRequest(shortName, "+380501234567", "Kyiv", "123 Main Street", null);

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.BuyerName);
    }

    [Fact]
    public void CheckoutRequest_WithTooLongBuyerName_FailsValidation()
    {
        var request = new CheckoutRequest(new string('a', 81), "+380501234567", "Kyiv", "123 Main Street", null);

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.BuyerName);
    }

    [Theory]
    [InlineData("123456")]
    [InlineData("12345678901234567890123456789012345")]
    public void CheckoutRequest_WithTooShortPhone_FailsValidation(string shortPhone)
    {
        var request = new CheckoutRequest("John Doe", shortPhone, "Kyiv", "123 Main Street", null);

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Phone);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    public void CheckoutRequest_WithTooShortCity_FailsValidation(string shortCity)
    {
        var request = new CheckoutRequest("John Doe", "+380501234567", shortCity, "123 Main Street", null);

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.City);
    }

    [Fact]
    public void CheckoutRequest_WithTooShortDeliveryAddress_FailsValidation()
    {
        var request = new CheckoutRequest("John Doe", "+380501234567", "Kyiv", "1234", null);

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.DeliveryAddress);
    }

    [Fact]
    public void CheckoutRequest_WithTooLongComment_FailsValidation()
    {
        var request = new CheckoutRequest("John Doe", "+380501234567", "Kyiv", "123 Main Street", new string('a', 501));

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Comment);
    }

    [Fact]
    public void CheckoutRequest_WithNullComment_PassesValidation()
    {
        var request = new CheckoutRequest("John Doe", "+380501234567", "Kyiv", "123 Main Street", null);

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class CategoryValidatorTests
{
    private readonly CategoryCreateValidator _createValidator = new();
    private readonly CategoryUpdateValidator _updateValidator = new();

    [Fact]
    public void CategoryCreateRequest_WithValidData_PassesValidation()
    {
        var request = new CategoryCreateRequest("Electronics", null);

        var result = _createValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CategoryCreateRequest_WithParentId_PassesValidation()
    {
        var request = new CategoryCreateRequest("Smartphones", Guid.NewGuid());

        var result = _createValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    public void CategoryCreateRequest_WithTooShortName_FailsValidation(string shortName)
    {
        var request = new CategoryCreateRequest(shortName, null);

        var result = _createValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void CategoryCreateRequest_WithTooLongName_FailsValidation()
    {
        var request = new CategoryCreateRequest(new string('a', 81), null);

        var result = _createValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void CategoryUpdateRequest_WithValidData_PassesValidation()
    {
        var request = new CategoryUpdateRequest("Updated Category", null);

        var result = _updateValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class AddressValidatorTests
{
    private readonly AddressCreateRequestValidator _createValidator = new();
    private readonly AddressUpdateRequestValidator _updateValidator = new();

    [Fact]
    public void AddressCreateRequest_WithValidData_PassesValidation()
    {
        var request = new AddressCreateRequest("Home", "+380501234567", "Kyiv Region", "Kyiv", "123 Main Street", "Apt 5", "01001");

        var result = _createValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AddressCreateRequest_WithNullLine2_PassesValidation()
    {
        var request = new AddressCreateRequest("Home", "+380501234567", "Kyiv Region", "Kyiv", "123 Main Street", null, "01001");

        var result = _createValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    public void AddressCreateRequest_WithTooShortName_FailsValidation(string shortName)
    {
        var request = new AddressCreateRequest(shortName, "+380501234567", "Region", "City", "Line1", null, "01001");

        var result = _createValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData("123456")]
    [InlineData("12345678901234567890123456789012345")]
    public void AddressCreateRequest_WithTooShortPhone_FailsValidation(string shortPhone)
    {
        var request = new AddressCreateRequest("Home", shortPhone, "Region", "City", "Line1", null, "01001");

        var result = _createValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Phone);
    }

    [Fact]
    public void AddressCreateRequest_WithTooLongLine2_FailsValidation()
    {
        var request = new AddressCreateRequest("Home", "+380501234567", "Region", "City", "Line1", new string('a', 201), "01001");

        var result = _createValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Line2);
    }

    [Fact]
    public void AddressUpdateRequest_WithPartialData_PassesValidation()
    {
        var request = new AddressUpdateRequest("New Name", null, null, null, null, null, null, false);

        var result = _updateValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AddressUpdateRequest_WithWhitespaceOnlyName_FailsValidation()
    {
        var request = new AddressUpdateRequest("   ", null, null, null, null, null, null, false);

        var result = _updateValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }
}

public class StoreValidatorTests
{
    private readonly StoreCreateRequestValidator _createValidator = new();
    private readonly StoreProfileValidator _profileValidator = new();

    [Fact]
    public void StoreCreateRequest_WithValidData_PassesValidation()
    {
        var request = new StoreCreateRequest("My Store", "A great store", "contact@store.com", "+380501234567", "Kyiv", "Kyiv", "Main St", "01001");

        var result = _createValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void StoreCreateRequest_WithMinimalData_PassesValidation()
    {
        var request = new StoreCreateRequest("Store", null, null, null, null, null, null, null);

        var result = _createValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void StoreCreateRequest_WithInvalidEmail_FailsValidation()
    {
        var request = new StoreCreateRequest("Store", null, "not-an-email", null, null, null, null, null);

        var result = _createValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.ContactEmail);
    }

    [Fact]
    public void StoreCreateRequest_WithTooLongDescription_FailsValidation()
    {
        var request = new StoreCreateRequest("Store", new string('a', 4001), null, null, null, null, null, null);

        var result = _createValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void StoreProfile_WithValidData_PassesValidation()
    {
        var profile = new StoreProfile(Guid.NewGuid(), "My Store", "my-store", "Description");

        var result = _profileValidator.TestValidate(profile);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    public void StoreProfile_WithTooShortName_FailsValidation(string shortName)
    {
        var profile = new StoreProfile(Guid.NewGuid(), shortName, "my-store", "Description");

        var result = _profileValidator.TestValidate(profile);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }
}

public class ReviewValidatorTests
{
    private readonly ReviewCreateValidator _validator = new();

    [Fact]
    public void ReviewCreateRequest_WithValidData_PassesValidation()
    {
        var request = new ReviewCreateRequest(Guid.NewGuid(), 5, "Great product!");

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void ReviewCreateRequest_WithValidRating_PassesValidation(int rating)
    {
        var request = new ReviewCreateRequest(Guid.NewGuid(), rating, "Review text");

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(10)]
    public void ReviewCreateRequest_WithInvalidRating_FailsValidation(int invalidRating)
    {
        var request = new ReviewCreateRequest(Guid.NewGuid(), invalidRating, "Review text");

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Rating);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    public void ReviewCreateRequest_WithTooShortText_FailsValidation(string shortText)
    {
        var request = new ReviewCreateRequest(Guid.NewGuid(), 5, shortText);

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Text);
    }

    [Fact]
    public void ReviewCreateRequest_WithTooLongText_FailsValidation()
    {
        var request = new ReviewCreateRequest(Guid.NewGuid(), 5, new string('a', 2001));

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Text);
    }
}

public class OrderShipValidatorTests
{
    private readonly OrderShipRequestValidator _validator = new();

    [Fact]
    public void OrderShipRequest_WithNullTrackingNumber_PassesValidation()
    {
        var request = new OrderShipRequest(null);

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void OrderShipRequest_WithValidTrackingNumber_PassesValidation()
    {
        var request = new OrderShipRequest("TRACK123456789");

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void OrderShipRequest_WithTooLongTrackingNumber_FailsValidation()
    {
        var request = new OrderShipRequest(new string('a', 121));

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.TrackingNumber);
    }
}
