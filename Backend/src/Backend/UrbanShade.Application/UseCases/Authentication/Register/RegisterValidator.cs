using System.Net.Mail;
using System.Text;
using FluentValidation;
using UrbanShade.Communication.Requests.Authentication;

namespace UrbanShade.Application.UseCases.Authentication.Register;

public sealed class RegisterValidator : AbstractValidator<RegisterRequest>
{
    public RegisterValidator()
    {
        RuleFor(request => request.Name).Must(name => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 150).WithMessage("Name is required and must have at most 150 characters.");

        RuleFor(request => request.Email).Must(BeAValidEmail) .WithMessage("A valid email is required.");

        RuleFor(request => request.Password).Must(password => !string.IsNullOrEmpty(password) && password.Length >= 12 && Encoding.UTF8.GetByteCount(password) <= 1024) .WithMessage("Password must contain at least 12 characters.");
    }

    private static bool BeAValidEmail(string? email)
    {
        var normalized = email?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized.Length <= 254 && MailAddress.TryCreate(normalized, out var address) && address.Address == normalized;
    }
}
