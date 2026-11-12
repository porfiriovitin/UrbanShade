namespace UrbanShade.Domain.Entities;

public sealed class OrganizationRegion : EntityBase
{
    private OrganizationRegion()
    {
    }

    public OrganizationRegion(Guid organizationId, Guid geoRegionId)
    {
        if (organizationId == Guid.Empty || geoRegionId == Guid.Empty)
            throw new ArgumentException("An organization and a region are required.");

        OrganizationId = organizationId;
        GeoRegionId = geoRegionId;
    }

    public Guid OrganizationId { get; private set; }
    public Guid GeoRegionId { get; private set; }

    public void Activate() => Active = true;

    public void Deactivate() => Active = false;
}
