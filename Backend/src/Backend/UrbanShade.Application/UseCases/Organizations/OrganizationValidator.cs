using System.Net.Mail;
using FluentValidation;
using UrbanShade.Communication.Requests.Organizations;
using UrbanShade.Domain.Enums;

namespace UrbanShade.Application.UseCases.Organizations;

public sealed class OrganizationValidator : AbstractValidator<OrganizationRequest>
{
    public OrganizationValidator()
    {
        RuleFor(request => request.Name).Must(name => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 150).WithMessage("Name is required and must have at most 150 characters.");
        RuleFor(request => request.Type).Must(type => Enum.GetNames<OrganizationType>().Contains(type)).WithMessage("A valid organization type is required.");
        RuleFor(request => request.Document).Must(document => !string.IsNullOrWhiteSpace(document) && document.Trim().Length <= 30).WithMessage("Document is required and must have at most 30 characters.");
        RuleFor(request => request.ContactEmail).Must(email => BeAValidEmail(email)).WithMessage("A valid contact email is required.");
        RuleFor(request => request.ContactPhone).MaximumLength(30);
    }

    internal static bool BeAValidEmail(string? email)
    {
        var normalized = email?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized.Length <= 254 && MailAddress.TryCreate(normalized, out var address) && address.Address == normalized;
    }
}
