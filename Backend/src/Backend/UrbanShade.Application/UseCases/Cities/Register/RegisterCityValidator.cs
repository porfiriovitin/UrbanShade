using FluentValidation;
using UrbanShade.Communication.Requests.Cities;
using UrbanShade.Domain.Entities;

namespace UrbanShade.Application.UseCases.Cities.Register;

public sealed class RegisterCityValidator : AbstractValidator<RegisterCityRequest>
{
    public RegisterCityValidator()
    {
        RuleFor(request => request.Name).Must(name => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 150).WithMessage("Name is required and must have at most 150 characters.");
        RuleFor(request => request).Must(request => City.IsValidIbgeCode(request.State, request.IbgeCode)).WithMessage("State must be a Brazilian UF and IbgeCode must contain seven digits matching that state.");
    }
}
