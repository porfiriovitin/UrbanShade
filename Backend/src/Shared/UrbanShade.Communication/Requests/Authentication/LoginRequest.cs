namespace UrbanShade.Communication.Requests.Authentication;

public class LoginRequest
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}
