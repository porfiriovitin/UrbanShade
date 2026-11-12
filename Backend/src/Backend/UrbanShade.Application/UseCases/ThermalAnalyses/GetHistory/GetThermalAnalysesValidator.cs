using FluentValidation;
using UrbanShade.Communication.Requests.ThermalAnalyses;
using UrbanShade.Domain.Enums;

namespace UrbanShade.Application.UseCases.ThermalAnalyses.GetHistory;

public sealed class GetThermalAnalysesValidator : AbstractValidator<GetThermalAnalysesRequest>
{
    public GetThermalAnalysesValidator()
    {
        RuleFor(request => request.Page).GreaterThan(0).WithMessage("Page must be greater than zero.");
        RuleFor(request => request.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
        RuleFor(request => request).Must(request => ((long)request.Page - 1) * request.PageSize <= int.MaxValue).WithMessage("Requested page is too large.");
        RuleFor(request => request.EndDate).Must((request, endDate) => !request.StartDate.HasValue || !endDate.HasValue || request.StartDate.Value <= endDate.Value).WithMessage("EndDate must be greater than or equal to StartDate.");
        RuleFor(request => request.RiskLevel).Must(value => value is null || Enum.GetNames<ThermalRiskLevel>().Contains(value, StringComparer.OrdinalIgnoreCase)).WithMessage("RiskLevel must be LOW, MODERATE, HIGH or CRITICAL.");
        RuleFor(request => request.AlgorithmVersion).Must(value => value is null || !string.IsNullOrWhiteSpace(value) && value.Length <= 32 && value == value.Trim()).WithMessage("AlgorithmVersion must contain between 1 and 32 characters without surrounding whitespace.");
    }
}
