using UrbanShade.Domain.Entities;

namespace UrbanShade.Domain.Repositories;

public interface IAccountRepository
{
    Task<User?> FindForUpdate(Guid userId, CancellationToken cancellationToken);
    Task<User?> FindByEmailForUpdate(string email, CancellationToken cancellationToken);
    Task RevokeSessions(Guid userId, DateTime now, CancellationToken cancellationToken);
}
