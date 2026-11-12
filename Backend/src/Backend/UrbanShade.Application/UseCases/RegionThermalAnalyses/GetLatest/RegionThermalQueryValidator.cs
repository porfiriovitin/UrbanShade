using System.Globalization;
using FluentValidation;
using NetTopologySuite.Geometries;
using UrbanShade.Communication.Requests.RegionThermalAnalyses;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.RegionThermalAnalyses.GetLatest;

public sealed class RegionThermalQueryValidator : AbstractValidator<RegionThermalQueryRequest>
{
    public RegionThermalQueryValidator()
    {
        RuleFor(request => request.Page).InclusiveBetween(1, 1000000);
        RuleFor(request => request.PageSize).InclusiveBetween(1, 100);
        RuleFor(request => request.Limit).InclusiveBetween(1, 500);
        RuleFor(request => request.MinExposureIndex).InclusiveBetween(0, 100).When(request => request.MinExposureIndex.HasValue);
        RuleFor(request => request.ExposureLevel).Must(value => value is null || Enum.TryParse<ExposureLevel>(value, false, out var level) && Enum.IsDefined(level));
        RuleFor(request => request).Must(request => !request.StartDate.HasValue || !request.EndDate.HasValue || request.StartDate <= request.EndDate).WithMessage("StartDate must not exceed EndDate.");
        RuleFor(request => request.StartDate).Must(value => !value.HasValue || value.Value.Kind == DateTimeKind.Utc).WithMessage("StartDate must be UTC.");
        RuleFor(request => request.EndDate).Must(value => !value.HasValue || value.Value.Kind == DateTimeKind.Utc).WithMessage("EndDate must be UTC.");
        RuleFor(request => request.Bbox).Must(value => value is null || ReadBounds(value) is not null).WithMessage("Bbox must contain west,south,east,north in valid coordinate order.");
        RuleFor(request => request.Ids).Must(value => value is null || ReadIds(value) is not null).WithMessage("Provide between 2 and 20 distinct region IDs.");
    }

    public static void ValidateRequest(RegionThermalQueryRequest request)
    {
        var result = new RegionThermalQueryValidator().Validate(request);
        if (!result.IsValid) throw new RegionThermalAnalysisException(ErrorKind.Validation, [.. result.Errors.Select(error => error.ErrorMessage)]);
    }

    public static Geometry? ReadBounds(string? value)
    {
        if (value is null) return null;
        var parts = value.Split(',');
        if (parts.Length != 4) return null;
        var numbers = new List<double>();
        foreach (var part in parts)
        {
            if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number)) return null;
            numbers.Add(number);
        }
        if (numbers[0] < -180 || numbers[2] > 180 || numbers[1] < -90 || numbers[3] > 90 || numbers[0] >= numbers[2] || numbers[1] >= numbers[3]) return null;
        return new GeometryFactory(new PrecisionModel(), 4326).ToGeometry(new Envelope(numbers[0], numbers[2], numbers[1], numbers[3]));
    }

    public static List<Guid>? ReadIds(string? value)
    {
        if (value is null) return null;
        var parts = value.Split(',');
        if (parts.Length is < 2 or > 20) return null;
        var ids = new List<Guid>();
        foreach (var part in parts)
        {
            if (!Guid.TryParse(part, out var id) || id == Guid.Empty || ids.Contains(id)) return null;
            ids.Add(id);
        }
        return ids;
    }

    public static async Task<GeoRegion> FindRegion(IGeoRegionRepository regions, Guid id, bool includeInactive, CancellationToken cancellationToken)
    {
        var region = includeInactive ? await regions.FindIncludingInactive(id, cancellationToken) : await regions.FindById(id, cancellationToken);
        return region ?? throw new RegionThermalAnalysisException(ErrorKind.NotFound, "Region not found.");
    }
}

