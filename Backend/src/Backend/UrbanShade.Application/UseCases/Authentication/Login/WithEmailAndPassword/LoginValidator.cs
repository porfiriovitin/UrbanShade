using System.Net.Mail;
using System.Text;
using FluentValidation;
using UrbanShade.Communication.Requests.Authentication;

namespace UrbanShade.Application.UseCases.Authentication.Login.WithEmailAndPassword;

public sealed class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(request => request.Email).Must(BeAValidEmail) .WithMessage("A valid email is required.");
        RuleFor(request => request.Password) .Must(password => !string.IsNullOrEmpty(password) && Encoding.UTF8.GetByteCount(password) <= 1024) .WithMessage("Password is required and must contain at most 1024 UTF-8 bytes.");
    }

    private static bool BeAValidEmail(string? email)
    {
        var normalized = email?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized.Length <= 254 && MailAddress.TryCreate(normalized, out var address) && address.Address == normalized;
    }
}
