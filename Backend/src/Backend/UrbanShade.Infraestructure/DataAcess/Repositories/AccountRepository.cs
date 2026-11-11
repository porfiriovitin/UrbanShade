using Microsoft.EntityFrameworkCore;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Repositories;

namespace UrbanShade.Infraestructure.DataAcess.Repositories;

internal sealed class AccountRepository : IAccountRepository
{
    private readonly UrbanShadeDbContext _context;

    public AccountRepository(UrbanShadeDbContext context)
    {
        _context = context;
    }

    public Task<User?> FindForUpdate(Guid userId, CancellationToken cancellationToken) =>
        _context.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {userId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);

    public async Task<User?> FindByEmailForUpdate(string email, CancellationToken cancellationToken)
    {
        /// :: Serialize invitations for the same email, including accounts that do not exist yet.
        await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({email}, 0))", cancellationToken);
        return await _context.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Email\" = {email} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
    }

    public Task RevokeSessions(Guid userId, DateTime now, CancellationToken cancellationToken) =>
        _context.RefreshTokens.Where(token => token.UserId == userId && token.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(token => token.RevokedAt, now), cancellationToken);
}
