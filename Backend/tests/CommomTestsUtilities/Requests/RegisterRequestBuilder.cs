using UrbanShade.Communication.Requests.Authentication;

namespace CommomTestsUtilities.Requests;

public static class RegisterRequestBuilder
{
    public static RegisterRequest Build() => new()
    {
        Name = "Test User", Email = $"user-{Guid.NewGuid():N}@example.com", Password = "Strong-password-123!"
    };
}
