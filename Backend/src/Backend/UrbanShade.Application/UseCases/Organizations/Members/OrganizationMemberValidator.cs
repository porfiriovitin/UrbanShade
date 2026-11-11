using FluentValidation;
using UrbanShade.Communication.Requests.Organizations;

namespace UrbanShade.Application.UseCases.Organizations.Members;

public sealed class OrganizationMemberValidator : AbstractValidator<AddOrganizationMemberRequest>
{
    public OrganizationMemberValidator()
    {
        RuleFor(request => request.Name).Must(name => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 150).WithMessage("Name is required and must have at most 150 characters.");
        RuleFor(request => request.Email).Must(OrganizationValidator.BeAValidEmail).WithMessage("A valid email is required.");
    }
}
