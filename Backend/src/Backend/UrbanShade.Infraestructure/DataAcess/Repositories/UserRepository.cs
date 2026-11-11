using Microsoft.EntityFrameworkCore;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Repositories;

namespace UrbanShade.Infraestructure.DataAcess.Repositories;

internal sealed class UserRepository : IUserRepository
{
    private readonly UrbanShadeDbContext _context;

    public UserRepository(UrbanShadeDbContext context)
    {
        _context = context;
    }

    public Task<User?> FindByEmail(string email, CancellationToken cancellationToken) => _context.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<User?> FindById(Guid id, CancellationToken cancellationToken) => _context.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

    public void Add(User user) => _context.Users.Add(user);
}
