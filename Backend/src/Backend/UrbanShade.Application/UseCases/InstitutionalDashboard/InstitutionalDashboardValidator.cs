using System.Globalization;
using FluentValidation;
using UrbanShade.Communication.Requests.InstitutionalDashboard;
using UrbanShade.Domain.InstitutionalDashboard;

namespace UrbanShade.Application.UseCases.InstitutionalDashboard;

public sealed class InstitutionalDashboardValidator : AbstractValidator<InstitutionalDashboardRequest>
{
    public InstitutionalDashboardValidator(InstitutionalDashboardSettings settings)
    {
        RuleFor(request => request.OrganizationId).NotEmpty();
        RuleFor(request => request.RegionId).Must(id => id != Guid.Empty).WithMessage("A valid region is required.");
        RuleFor(request => request.RegionIds).Must(ids => ids is not null && ids.Length <= 10 && ids.All(id => id != Guid.Empty) && ids.Distinct().Count() == ids.Length)
            .WithMessage("Provide at most ten distinct region IDs.");
        RuleFor(request => request.Page).InclusiveBetween(1, 100000);
        RuleFor(request => request.PageSize).Must(size => size is null || size >= 1 && size <= settings.MaxPageSize).WithMessage("Invalid page size.");
        RuleFor(request => request.Limit).InclusiveBetween(1, settings.MaxPageSize);
        RuleFor(request => request.Sort).Must(value => value is "ASC" or "DESC").WithMessage("Sort must be ASC or DESC.");
        RuleFor(request => request.ExposureLevel).Must(value => value is null or "LOW" or "MODERATE" or "HIGH" or "CRITICAL" or "INSUFFICIENT_DATA").WithMessage("Invalid exposure level.");
        RuleFor(request => request.MinExposureIndex).Must(value => value is null || value is >= 0 and <= 100).WithMessage("Exposure index must be between 0 and 100.");
        RuleFor(request => request.Interval).Must(value => value is "HOUR" or "DAY" or "WEEK").WithMessage("Interval must be HOUR, DAY or WEEK.");
        RuleFor(request => request.Metric).Must(value => value is "EXPOSURE_INDEX" or "HEAT_INDEX" or "TEMPERATURE" or "SURFACE_TEMPERATURE" or "VEGETATION_COVERAGE").WithMessage("Invalid metric.");
        RuleFor(request => request.Format).Equal("csv");
        RuleFor(request => request).Must(request => request.StartDate.HasValue == request.EndDate.HasValue).WithMessage("Provide both startDate and endDate.");
        RuleFor(request => request).Must(request => !request.StartDate.HasValue || !request.EndDate.HasValue || request.StartDate.Value.Kind == DateTimeKind.Utc && request.EndDate!.Value.Kind == DateTimeKind.Utc &&
            request.StartDate <= request.EndDate && request.EndDate.Value - request.StartDate.Value <= TimeSpan.FromDays(settings.MaxHistoryDays))
            .WithMessage("Dates must be UTC, ordered, and within the maximum history range.");
        RuleFor(request => request.Bbox).Must(value => value is null || ParseBounds(value, settings.MaxMapSpanDegrees) is not null).WithMessage("Invalid or oversized bbox; use west,south,east,north.");
    }

    internal static double[]? ParseBounds(string value, double maximumSpan)
    {
        var parts = value.Split(',');
        if (parts.Length != 4) return null;
        var bounds = new double[4];
        for (var index = 0; index < 4; index++)
            if (!double.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out bounds[index]) || !double.IsFinite(bounds[index])) return null;
        return bounds[0] >= -180 && bounds[2] <= 180 && bounds[1] >= -90 && bounds[3] <= 90 &&
            bounds[0] < bounds[2] && bounds[1] < bounds[3] && bounds[2] - bounds[0] <= maximumSpan && bounds[3] - bounds[1] <= maximumSpan ? bounds : null;
    }
}
