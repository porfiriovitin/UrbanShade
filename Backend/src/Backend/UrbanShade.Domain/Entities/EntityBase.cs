namespace UrbanShade.Domain.Entities;

public abstract class EntityBase
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public bool Active { get; protected set; } = true;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
}
