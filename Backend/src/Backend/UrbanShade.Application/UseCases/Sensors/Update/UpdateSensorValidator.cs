using FluentValidation;
using UrbanShade.Communication.Requests.Sensors;

namespace UrbanShade.Application.UseCases.Sensors.Update;

public sealed class UpdateSensorValidator : AbstractValidator<UpdateSensorRequest>
{
    public UpdateSensorValidator()
    {
        RuleFor(request => request.Code).Must(code => !string.IsNullOrWhiteSpace(code) && code.Trim().Length <= 50).When(request => request.HasCode).WithMessage("Code is required and must have at most 50 characters.");
        RuleFor(request => request.Name).Must(name => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 150).When(request => request.HasName).WithMessage("Name is required and must have at most 150 characters.");
        RuleFor(request => request.Description).Must(description => description is null || description.Trim().Length <= 500).WithMessage("Description must have at most 500 characters.");
        RuleFor(request => request.Latitude).Must(latitude => latitude.HasValue && double.IsFinite(latitude.Value) && latitude.Value >= -90 && latitude.Value <= 90).When(request => request.HasLatitude).WithMessage("Latitude must be between -90 and 90.");
        RuleFor(request => request.Longitude).Must(longitude => longitude.HasValue && double.IsFinite(longitude.Value) && longitude.Value >= -180 && longitude.Value <= 180).When(request => request.HasLongitude).WithMessage("Longitude must be between -180 and 180.");
        RuleFor(request => request.InstalledAt).Must(date => !date.HasValue || date.Value.Kind == DateTimeKind.Utc).WithMessage("InstalledAt must be a UTC date.");
        RuleFor(request => request.CityId).Must(id => id != Guid.Empty).WithMessage("CityId must be a valid city identifier.");
    }
}
