using System.Text;
using FluentValidation;
using UrbanShade.Communication.Requests.Authentication;

namespace UrbanShade.Application.UseCases.Authentication.ChangePassword;

public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordValidator(bool firstAccess = false)
    {
        RuleFor(request => request.NewPassword).Must(password => !string.IsNullOrEmpty(password) && password.Length >= 12 && Encoding.UTF8.GetByteCount(password) <= 1024).WithMessage("NewPassword must contain at least 12 characters and at most 1024 UTF-8 bytes.");
        if (!firstAccess)
            RuleFor(request => request.CurrentPassword).Must(password => !string.IsNullOrEmpty(password) && Encoding.UTF8.GetByteCount(password) <= 1024).WithMessage("CurrentPassword is required.");
    }
}
