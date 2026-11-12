using NetTopologySuite.Geometries;

namespace UrbanShade.Domain.Geospatial;

public interface IOpenStreetMapService
{
    Task<OpenStreetMapSnapshot> GetGreenAreas(Geometry boundary, OpenStreetMapParameters parameters, CancellationToken cancellationToken);
}

public sealed record OpenStreetMapParameters(int MaximumElements, int MaximumVertices, int MaximumResponseBytes);
public sealed record OpenStreetMapArea(string ElementType, long ElementId, string? Name, Geometry Geometry, string Tags);
public sealed record OpenStreetMapSnapshot(DateTime ReferenceDate, IReadOnlyList<OpenStreetMapArea> Areas);
public sealed class OpenStreetMapException : Exception
{
    public OpenStreetMapException(string message) : base(message)
    {
    }
}
