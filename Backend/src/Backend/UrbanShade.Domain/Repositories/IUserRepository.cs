using UrbanShade.Domain.Entities;

namespace UrbanShade.Domain.Repositories;

public interface IUserRepository
{
    Task<User?> FindByEmail(string email, CancellationToken cancellationToken);
    Task<User?> FindById(Guid id, CancellationToken cancellationToken);
    void Add(User user);
}
