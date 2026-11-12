using FluentValidation;
using UrbanShade.Communication.Requests.Sensors;
using UrbanShade.Domain.Enums;

namespace UrbanShade.Application.UseCases.Sensors.GetAll;

public sealed class GetSensorsValidator : AbstractValidator<GetSensorsRequest>
{
    public GetSensorsValidator()
    {
        RuleFor(request => request.Page).InclusiveBetween(1, 10000);
        RuleFor(request => request.PageSize).InclusiveBetween(1, 100);
        RuleFor(request => request.Status).Must(status => status is null || Enum.GetNames<SensorStatus>().Contains(status)).WithMessage("Status must be ACTIVE, INACTIVE or MAINTENANCE.");
        RuleFor(request => request.Code).MaximumLength(50);
        RuleFor(request => request.Name).MaximumLength(150);
    }
}
