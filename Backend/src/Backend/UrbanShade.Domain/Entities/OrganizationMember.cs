namespace UrbanShade.Domain.Entities;

public class OrganizationMember : EntityBase
{
    private OrganizationMember() { }

    public OrganizationMember(Guid organizationId, Guid userId)
    {
        OrganizationId = organizationId;
        UserId = userId;
    }

    public Guid OrganizationId { get; private set; }
    public Guid UserId { get; private set; }

    public void Activate() => Active = true;
    public void Deactivate() => Active = false;
}
