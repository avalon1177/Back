using FluentValidation;
using Marketplace.Application.DTO;

namespace Marketplace.Application.Validation;

public class AddressCreateRequestValidator : AbstractValidator<AddressCreateRequest>
{
    public AddressCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(120);
        RuleFor(x => x.Phone).NotEmpty().MinimumLength(7).MaximumLength(30);
        RuleFor(x => x.Region).NotEmpty().MinimumLength(2).MaximumLength(120);
        RuleFor(x => x.City).NotEmpty().MinimumLength(2).MaximumLength(120);
        RuleFor(x => x.Line1).NotEmpty().MinimumLength(2).MaximumLength(200);
        RuleFor(x => x.Line2).MaximumLength(200).When(x => x.Line2 is not null);
        RuleFor(x => x.PostalCode).NotEmpty().MinimumLength(2).MaximumLength(20);
    }
}

public class AddressUpdateRequestValidator : AbstractValidator<AddressUpdateRequest>
{
    public AddressUpdateRequestValidator()
    {
        RuleFor(x => x.Name)
            .Must(HaveTextWhenProvided)
            .WithMessage("Name must not be empty")
            .MinimumLength(2)
            .MaximumLength(120)
            .When(x => x.Name is not null);

        RuleFor(x => x.Phone)
            .Must(HaveTextWhenProvided)
            .WithMessage("Phone must not be empty")
            .MinimumLength(7)
            .MaximumLength(30)
            .When(x => x.Phone is not null);

        RuleFor(x => x.Region)
            .Must(HaveTextWhenProvided)
            .WithMessage("Region must not be empty")
            .MinimumLength(2)
            .MaximumLength(120)
            .When(x => x.Region is not null);

        RuleFor(x => x.City)
            .Must(HaveTextWhenProvided)
            .WithMessage("City must not be empty")
            .MinimumLength(2)
            .MaximumLength(120)
            .When(x => x.City is not null);

        RuleFor(x => x.Line1)
            .Must(HaveTextWhenProvided)
            .WithMessage("Line1 must not be empty")
            .MinimumLength(2)
            .MaximumLength(200)
            .When(x => x.Line1 is not null);

        RuleFor(x => x.Line2).MaximumLength(200).When(x => x.Line2 is not null);

        RuleFor(x => x.PostalCode)
            .Must(HaveTextWhenProvided)
            .WithMessage("PostalCode must not be empty")
            .MinimumLength(2)
            .MaximumLength(20)
            .When(x => x.PostalCode is not null);
    }

    private static bool HaveTextWhenProvided(string? value) => !string.IsNullOrWhiteSpace(value);
}

public class StoreCreateRequestValidator : AbstractValidator<StoreCreateRequest>
{
    public StoreCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(4000).When(x => x.Description is not null);
        RuleFor(x => x.ContactEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
        RuleFor(x => x.ContactPhone).MaximumLength(30).When(x => x.ContactPhone is not null);
        RuleFor(x => x.Region).MaximumLength(120).When(x => x.Region is not null);
        RuleFor(x => x.City).MaximumLength(120).When(x => x.City is not null);
        RuleFor(x => x.Street).MaximumLength(200).When(x => x.Street is not null);
        RuleFor(x => x.PostalCode).MaximumLength(20).When(x => x.PostalCode is not null);
    }
}

public class StoreProfileValidator : AbstractValidator<StoreProfile>
{
    public StoreProfileValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(4000);
    }
}

public class OrderShipRequestValidator : AbstractValidator<OrderShipRequest>
{
    public OrderShipRequestValidator()
    {
        RuleFor(x => x.TrackingNumber).MaximumLength(120).When(x => x.TrackingNumber is not null);
    }
}
