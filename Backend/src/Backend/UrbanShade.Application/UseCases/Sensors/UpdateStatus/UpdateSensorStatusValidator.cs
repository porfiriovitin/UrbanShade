using FluentValidation;
using UrbanShade.Communication.Requests.Sensors;
using UrbanShade.Domain.Enums;

namespace UrbanShade.Application.UseCases.Sensors.UpdateStatus;

public sealed class UpdateSensorStatusValidator : AbstractValidator<UpdateSensorStatusRequest>
{
    public UpdateSensorStatusValidator()
    {
        RuleFor(request => request.Status).Must(status => Enum.GetNames<SensorStatus>().Contains(status)).WithMessage("Status must be ACTIVE, INACTIVE or MAINTENANCE.");
    }
}
