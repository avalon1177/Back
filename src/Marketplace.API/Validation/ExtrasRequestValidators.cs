using FluentValidation;
using Marketplace.API.Features.Extras;
using Marketplace.Core.Enums;

namespace Marketplace.API.Validation;

public class WishlistCreateRequestValidator : AbstractValidator<ExtrasEndpoints.WishlistCreateRequest>
{
    public WishlistCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(80);
    }
}

public class ProductRefRequestValidator : AbstractValidator<ExtrasEndpoints.ProductRefRequest>
{
    public ProductRefRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
    }
}

public class CreateChatRequestValidator : AbstractValidator<ExtrasEndpoints.CreateChatRequest>
{
    public CreateChatRequestValidator()
    {
        RuleFor(x => x.SellerId).NotEmpty();
    }
}

public class CreateMessageRequestValidator : AbstractValidator<ExtrasEndpoints.CreateMessageRequest>
{
    public CreateMessageRequestValidator()
    {
        RuleFor(x => x.Text).NotEmpty().MinimumLength(1).MaximumLength(4000);
    }
}

public class CreateSellerRequestBodyValidator : AbstractValidator<ExtrasEndpoints.CreateSellerRequestBody>
{
    public CreateSellerRequestBodyValidator()
    {
        RuleFor(x => x.AdditionalInformation).NotEmpty().MinimumLength(3).MaximumLength(2000);
    }
}

public class CreateShippingMethodRequestValidator : AbstractValidator<ExtrasEndpoints.CreateShippingMethodRequest>
{
    public CreateShippingMethodRequestValidator()
    {
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.EstimatedDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Name).IsInEnum();
    }
}

public class CreateCouponRequestValidator : AbstractValidator<ExtrasEndpoints.CreateCouponRequest>
{
    public CreateCouponRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MinimumLength(3).MaximumLength(64);
        RuleFor(x => x.Discount).GreaterThan(0);
        RuleFor(x => x.DiscountType).IsInEnum();
        RuleFor(x => x.UsageLimit).GreaterThan(0).When(x => x.UsageLimit.HasValue);
    }
}

public class UpsertCompanyFinanceRequestValidator : AbstractValidator<ExtrasEndpoints.UpsertCompanyFinanceRequest>
{
    public UpsertCompanyFinanceRequestValidator()
    {
        RuleFor(x => x.BankAccount).NotEmpty().MaximumLength(100);
        RuleFor(x => x.BankName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.BankCode).NotEmpty().MaximumLength(40);
        RuleFor(x => x.TaxId).NotEmpty().MaximumLength(40);
        RuleFor(x => x.PaymentDetails).NotEmpty().MaximumLength(500);
    }
}

public class UpsertCompanyScheduleRequestValidator : AbstractValidator<ExtrasEndpoints.UpsertCompanyScheduleRequest>
{
    public UpsertCompanyScheduleRequestValidator()
    {
        RuleFor(x => x.Day).IsInEnum();
        RuleFor(x => x).Must(x => x.IsClosed || (x.OpenTime.HasValue && x.CloseTime.HasValue))
            .WithMessage("OpenTime and CloseTime are required when schedule is open");
    }
}

public class CreatePaymentRequestValidator : AbstractValidator<ExtrasEndpoints.CreatePaymentRequest>
{
    public CreatePaymentRequestValidator()
    {
        RuleFor(x => x.PaymentMethod).IsInEnum();
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Status).Equal(PaymentStatus.Pending).WithMessage("Payment status must be Pending when creating");
        RuleFor(x => x.ExternalReference).MaximumLength(120).When(x => x.ExternalReference is not null);
    }
}
