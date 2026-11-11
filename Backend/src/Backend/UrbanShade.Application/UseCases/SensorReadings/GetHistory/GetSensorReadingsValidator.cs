using FluentValidation;
using UrbanShade.Communication.Requests.SensorReadings;

namespace UrbanShade.Application.UseCases.SensorReadings.GetHistory;

public sealed class GetSensorReadingsValidator : AbstractValidator<GetSensorReadingsRequest>
{
    public GetSensorReadingsValidator()
    {
        RuleFor(request => request.Page).GreaterThan(0).WithMessage("Page must be greater than zero.");
        RuleFor(request => request.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
        RuleFor(request => request).Must(request => ((long)request.Page - 1) * request.PageSize <= int.MaxValue).WithMessage("Requested page is too large.");
        RuleFor(request => request.EndDate).Must((request, endDate) => !request.StartDate.HasValue || !endDate.HasValue || request.StartDate.Value <= endDate.Value).WithMessage("EndDate must be greater than or equal to StartDate.");
    }
}
