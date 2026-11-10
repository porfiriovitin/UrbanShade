using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using UrbanShade.Domain.Security;

namespace UrbanShade.Infraestructure.Security;

public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    private const int DEGREE_OF_PARALLELISM = 1;
    private const int ITERATIONS = 2;
    private const int MEMORY_SIZE = 20 * 1024;
    private const int SALT_SIZE = 16;
    private const int HASH_SIZE = 32;

    public async Task<string> Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SALT_SIZE);
        var hash = await HashPassword(salt, password);

        var combinedBytes = new byte[salt.Length + hash.Length];
        salt.CopyTo(combinedBytes.AsSpan(0, salt.Length));
        hash.CopyTo(combinedBytes.AsSpan(salt.Length, hash.Length));

        return Convert.ToBase64String(combinedBytes);
    }

    public async Task<bool> Verify(string password, string encodedHash)
    {
        try
        {
            var hashedPasswordBytes = Convert.FromBase64String(encodedHash);
            if (hashedPasswordBytes.Length != SALT_SIZE + HASH_SIZE) return false;

            var salt = new byte[SALT_SIZE];
            var hash = new byte[HASH_SIZE];

            Array.Copy(hashedPasswordBytes, 0, salt, 0, SALT_SIZE);
            Array.Copy(hashedPasswordBytes, SALT_SIZE, hash, 0, HASH_SIZE);

            var newHash = await HashPassword(salt, password);
            return CryptographicOperations.FixedTimeEquals(hash, newHash);
        }
        catch (FormatException) { return false; }
    }

    private static async Task<byte[]> HashPassword(byte[] salt, string password)
    {
        /// :: Hash the password with the given salt.
        var bytes = Encoding.UTF8.GetBytes(password);

        try
        {
            using var argon = new Argon2id(bytes)
            {
                DegreeOfParallelism = DEGREE_OF_PARALLELISM,
                Iterations = ITERATIONS,
                MemorySize = MEMORY_SIZE,
                Salt = salt
            };

            return await argon.GetBytesAsync(HASH_SIZE);
        }

        finally 
        {
            /// :: Release the memory used by the password bytes.
            CryptographicOperations.ZeroMemory(bytes); 
        }
    }
}
