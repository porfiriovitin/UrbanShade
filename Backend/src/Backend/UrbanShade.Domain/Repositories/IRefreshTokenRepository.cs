using UrbanShade.Domain.Entities;

namespace UrbanShade.Domain.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshTokens?> FindForUpdate(string tokenHash, CancellationToken cancellationToken);
    Task RevokeFamily(Guid familyId, DateTime now, CancellationToken cancellationToken);
    Task<bool> IsSessionActive(Guid familyId, Guid userId, int securityVersion, DateTime now, CancellationToken cancellationToken);
    void Add(RefreshTokens token);
}
