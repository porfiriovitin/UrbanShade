using FluentValidation;
using UrbanShade.Communication.Requests.Authentication;

namespace UrbanShade.Application.UseCases.Authentication.Logout;

public sealed class LogoutValidator : AbstractValidator<RefreshTokenRequest>
{
    public LogoutValidator()
    {
        RuleFor(request => request.RefreshToken)
            .Must(token => !string.IsNullOrWhiteSpace(token)
                && token.Length == 86
                && token.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_'))
            .WithMessage("Invalid refresh token.");
    }
}
