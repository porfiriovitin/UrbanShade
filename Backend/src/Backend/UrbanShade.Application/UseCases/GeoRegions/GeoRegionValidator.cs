using System.Text.Json;
using FluentValidation;
using NetTopologySuite.Geometries;
using UrbanShade.Communication.Requests.Geo;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.GeoRegions;

public sealed class GeoRegionValidator : AbstractValidator<RegisterGeoRegionRequest>
{
    public GeoRegionValidator()
    {
        RuleFor(request => request.CityId).NotEmpty();
        RuleFor(request => request.Name).Must(name => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 150).WithMessage("Name is required and must have at most 150 characters.");
        RuleFor(request => request.Description).MaximumLength(500);
    }
    public static Geometry ReadBoundary(JsonElement json, GeoAnalysisSettings settings)
    {
        try
        {
            if (json.ValueKind != JsonValueKind.Object || json.GetRawText().Length > settings.MaximumVertices * 100)
                throw new ArgumentException();
            var boundary = GeoJsonGeometry.Read(json);
            if (boundary.IsEmpty || boundary.NumPoints > settings.MaximumVertices || !boundary.IsValid ||
                boundary.EnvelopeInternal.Width > 180 || boundary.EnvelopeInternal.Height > 90)
                throw new ArgumentException();
            return boundary;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException or JsonException or FormatException)
        {
            throw new GeoException(ErrorKind.Validation, "Boundary must be a valid GeoJSON polygon within geographic and complexity limits.");
        }
    }
    public static void Page(int page, int pageSize)
    {
        if (page is < 1 or > 10000 || pageSize is < 1 or > 100)
            throw new GeoException(ErrorKind.Validation, "Page must be between 1 and 10000; page size between 1 and 100.");
    }
}
