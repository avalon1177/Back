using FluentValidation;
using Marketplace.Application.DTO;

namespace Marketplace.Application.Validation;

public class ReviewCreateValidator : AbstractValidator<ReviewCreateRequest>
{
    public ReviewCreateValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Text).NotEmpty().MinimumLength(3).MaximumLength(2000);
    }
}
