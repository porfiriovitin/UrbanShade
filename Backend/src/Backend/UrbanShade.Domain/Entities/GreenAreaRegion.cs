namespace UrbanShade.Domain.Entities;

public sealed class GreenAreaRegion
{
    public Guid GreenAreaId { get; private set; }
    public Guid GeoRegionId { get; private set; }
    public int BoundaryVersion { get; private set; }
}
