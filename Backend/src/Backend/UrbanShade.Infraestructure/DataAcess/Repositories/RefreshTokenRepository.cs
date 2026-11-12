using Microsoft.EntityFrameworkCore;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Repositories;

namespace UrbanShade.Infraestructure.DataAcess.Repositories;

internal sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly UrbanShadeDbContext _context;

    public RefreshTokenRepository(UrbanShadeDbContext context)
    {
        _context = context;
    }

    public void Add(RefreshTokens token) => _context.RefreshTokens.Add(token);

    public Task<bool> IsSessionActive(Guid familyId, Guid userId, int securityVersion, DateTime now, CancellationToken cancellationToken) =>
        _context.RefreshTokens.AsNoTracking().AnyAsync(token => token.FamilyId == familyId && token.UserId == userId &&
            token.SecurityVersion == securityVersion && token.RevokedAt == null && token.ExpiresAt > now, cancellationToken);

    public async Task<RefreshTokens?> FindForUpdate(string tokenHash, CancellationToken cancellationToken)
    {
        /// :: Serialize the entire family, including attempts using different generations of its token.
        var family = await _context.RefreshTokens.AsNoTracking().Where(t => t.Token == tokenHash).Select(t => (Guid?)t.FamilyId).SingleOrDefaultAsync(cancellationToken);

        if (family is null) return null;

        /// :: Lock the family to prevent concurrent attempts using different generations of its token.
        await _context.Database.ExecuteSqlInterpolatedAsync( $"SELECT pg_advisory_xact_lock(hashtextextended({family.Value.ToString()}, 0))", cancellationToken);

        /// :: Lock the token to prevent concurrent attempts using the same generation of its token.
        return await _context.RefreshTokens.FromSqlInterpolated( $"SELECT * FROM \"RefreshTokens\" WHERE \"Token\" = {tokenHash} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
    }

    public Task RevokeFamily(Guid familyId, DateTime now, CancellationToken cancellationToken) => _context.RefreshTokens.Where(t => t.FamilyId == familyId && t.RevokedAt == null).ExecuteUpdateAsync(update => update.SetProperty(t => t.RevokedAt, now), cancellationToken);
}
