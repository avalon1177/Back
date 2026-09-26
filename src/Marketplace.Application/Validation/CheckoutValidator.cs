using FluentValidation;
using Marketplace.Application.DTO;

namespace Marketplace.Application.Validation;

public class CheckoutValidator : AbstractValidator<CheckoutRequest>
{
    public CheckoutValidator()
    {
        RuleFor(x => x.BuyerName).NotEmpty().MinimumLength(2).MaximumLength(80);
        RuleFor(x => x.Phone).NotEmpty().MinimumLength(7).MaximumLength(30);
        RuleFor(x => x.City).NotEmpty().MinimumLength(2).MaximumLength(80);
        RuleFor(x => x.DeliveryAddress).NotEmpty().MinimumLength(5).MaximumLength(200);
        RuleFor(x => x.Comment).MaximumLength(500).When(x => x.Comment is not null);
    }
}
