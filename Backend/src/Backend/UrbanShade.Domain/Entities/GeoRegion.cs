using NetTopologySuite.Geometries;

namespace UrbanShade.Domain.Entities;

public class GeoRegion : EntityBase
{
    private GeoRegion()
    {
    }

    public GeoRegion(Guid cityId, string name, string? description, Geometry boundary)
    {
        CityId = cityId;
        Update(name, description, boundary);
    }

    public Guid CityId { get; private set; }
    public Guid? ReferenceGeoRegionId { get; private set; }

    public void UpdateReference(Guid? referenceRegionId)
    {
        if (referenceRegionId == Id || referenceRegionId == Guid.Empty)
            throw new ArgumentException("A different reference region is required.", nameof(referenceRegionId));

        ReferenceGeoRegionId = referenceRegionId;
        UpdatedAt = DateTime.UtcNow;
    }

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    private Geometry _boundary = null!;
    public Geometry Boundary { get => (Geometry)_boundary.Copy(); private set => _boundary = (Geometry)value.Copy(); }
    public int BoundaryVersion { get; private set; }
    public DateTime? OpenStreetMapReferenceDate { get; private set; }
    public DateTime? OpenStreetMapRequestedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? WeatherRequestedAt { get; private set; }
    public Guid? WeatherLeaseId { get; private set; }
    public DateTime? WeatherLeaseExpiresAt { get; private set; }
    public DateTime? WeatherRetryAfter { get; private set; }

    public void Update(string name, string? description, Geometry boundary)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 150 || description?.Length > 500)
            throw new ArgumentException("Invalid region name or description.");
        if (boundary.IsEmpty || !boundary.IsValid || boundary.SRID != 4326 || boundary is not (Polygon or MultiPolygon))
            throw new ArgumentException("A valid polygon in SRID 4326 is required.");
        if (_boundary is null || !_boundary.EqualsExact(boundary))
            BoundaryVersion++;

        Name = name.Trim();
        Description = description?.Trim();
        Boundary = boundary;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        Active = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
