using System.Globalization;
using System.Net;
using System.Text.Json;
using NetTopologySuite.Geometries;
using UrbanShade.Application.UseCases.GeoRegions;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.GreenAreas.Query;

public sealed class QueryGreenAreasUseCase : IQueryGreenAreasUseCase
{
    private readonly IGreenAreaRepository _areas;
    public QueryGreenAreasUseCase(IGreenAreaRepository areas) { _areas = areas; }
    public async Task<JsonElement> Execute(string bbox, int page, int pageSize, CancellationToken cancellationToken)
    {
        GeoRegionValidator.Page(page, pageSize);
        var values = bbox?.Split(',');
        var bounds = new double[4];
        if (values?.Length != 4 || values.Where((value, index) => !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out bounds[index]) || !double.IsFinite(bounds[index])).Any() ||
            bounds[0] < -180 || bounds[2] > 180 || bounds[1] < -90 || bounds[3] > 90 || bounds[0] >= bounds[2] || bounds[1] >= bounds[3] || bounds[2] - bounds[0] > 1 || bounds[3] - bounds[1] > 1)
            throw new GeoException(HttpStatusCode.BadRequest, "Provide bbox=minLon,minLat,maxLon,maxLat within a maximum one-degree extent per axis.");
        var geometry = new GeometryFactory(new PrecisionModel(), 4326).ToGeometry(new Envelope(bounds[0], bounds[2], bounds[1], bounds[3]));
        var areas = await _areas.GetByBounds(geometry, page, pageSize, cancellationToken);
        return JsonSerializer.SerializeToElement(new
        {
            type = "FeatureCollection",
            page,
            pageSize,
            sources = new
            {
                openStreetMap = new
                {
                    attribution = "\u00a9 OpenStreetMap contributors",
                    attributionUrl = "https://www.openstreetmap.org/copyright",
                    license = "ODbL-1.0",
                    licenseUrl = "https://opendatacommons.org/licenses/odbl/1-0/"
                }
            },
            features = areas.Select(area => new
            {
                type = "Feature",
                id = area.Id,
                geometry = GeoJsonGeometry.Write(area.Geometry),
                properties = new
                {
                    name = area.Name,
                    source = area.Source.ToString().ToUpperInvariant(),
                    estimatedAreaSquareMeters = area.EstimatedAreaSquareMeters,
                    referenceDate = area.ReferenceDate,
                    importedAt = area.ImportedAt,
                    regionId = area.GeoRegionId,
                    osmElementType = area.OsmElementType,
                    osmElementId = area.OsmElementId,
                    osmUrl = area.OsmElementId.HasValue ? $"https://www.openstreetmap.org/{area.OsmElementType}/{area.OsmElementId}" : null,
                    sourceTags = area.Source == GreenAreaSource.OpenStreetMap ? JsonSerializer.Deserialize<JsonElement>(area.SourceMetadata) : (JsonElement?)null
                }
            })
        });
    }
}
