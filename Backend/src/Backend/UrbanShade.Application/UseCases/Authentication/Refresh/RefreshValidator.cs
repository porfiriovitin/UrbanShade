using FluentValidation;
using UrbanShade.Communication.Requests.Authentication;

namespace UrbanShade.Application.UseCases.Authentication.Refresh;

public sealed class RefreshValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshValidator()
    {
        RuleFor(request => request.RefreshToken) .Must(token => !string.IsNullOrWhiteSpace(token) && token.Length == 86 && token.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_')) .WithMessage("Invalid refresh token.");
    }
}
