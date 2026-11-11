using UrbanShade.Communication.Responses.Authentication;

namespace UrbanShade.Communication.Responses.Organizations;

public record OrganizationMemberResponse
{
    public Guid OrganizationId { get; init; }
    public UserResponse User { get; init; } = null!;
    public string? TemporaryPassword { get; init; }
    public string? TemporaryPasswordExpiresAt { get; init; }
}
