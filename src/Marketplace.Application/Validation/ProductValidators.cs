using FluentValidation;
using Marketplace.Application.DTO;

namespace Marketplace.Application.Validation;

public class ProductCreateValidator : AbstractValidator<ProductCreateRequest>
{
    public ProductCreateValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MinimumLength(2).MaximumLength(140);
        RuleFor(x => x.Description).NotEmpty().MinimumLength(10).MaximumLength(6000);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.Stock).GreaterThanOrEqualTo(0);
    }
}

public class ProductUpdateValidator : AbstractValidator<ProductUpdateRequest>
{
    public ProductUpdateValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MinimumLength(2).MaximumLength(140);
        RuleFor(x => x.Description).NotEmpty().MinimumLength(10).MaximumLength(6000);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.Stock).GreaterThanOrEqualTo(0);
    }
}
