namespace UrbanShade.Domain.Security;

public interface IPasswordHasher
{
    Task<string> Hash(string password);
    Task<bool> Verify(string password, string encodedHash);
}
