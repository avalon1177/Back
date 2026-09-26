using FluentValidation;
using Marketplace.Application.DTO;

namespace Marketplace.Application.Validation;

public class CartAddValidator : AbstractValidator<CartAddRequest>
{
    public CartAddValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(100);
    }
}

public class CartUpdateValidator : AbstractValidator<CartUpdateRequest>
{
    public CartUpdateValidator()
    {
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(100);
    }
}
