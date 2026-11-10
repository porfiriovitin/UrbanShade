using CommomTestsUtilities.Requests;
using UrbanShade.Application.UseCases.Authentication;
using UrbanShade.Application.UseCases.Authentication.Register;
using UrbanShade.Communication.Requests.Authentication;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.Security;
using UrbanShade.Exceptions;
using UrbanShade.Infraestructure.Security;

namespace UseCases.Tests.Authentication;

public sealed class RegisterUseCaseTests
{
    [Fact]
    public async Task Register_CreatesUserAndHashedSession_ThenSaves()
    {
        var store = new TestStore();
        var hasher = new Argon2idPasswordHasher();
        var tokens = new JwtTokenService(new JwtSettings { SigningKey = new string('k', 64) });
        var useCase = new RegisterUseCase(store, hasher, new TokenIssuer(tokens, store), store);
        var request = RegisterRequestBuilder.Build();
        var result = await useCase.Execute(request);
        Assert.Equal(request.Email, store.User!.Email);
        Assert.True(await hasher.Verify(request.Password, store.User.HashedPassword));
        Assert.Equal("Citizen", result.User.Role);
        Assert.Equal(tokens.HashRefreshToken(result.RefreshToken), store.Token!.Token);
        Assert.True(store.Saved);
    }

    [Fact]
    public async Task Register_RejectsDuplicateEmail()
    {
        var store = new TestStore { User = new User("Existing", "existing@example.com", "hash") };
        var tokens = new JwtTokenService(new JwtSettings());
        var useCase = new RegisterUseCase(store, new Argon2idPasswordHasher(), new TokenIssuer(tokens, store), store);
        var exception = await Assert.ThrowsAsync<EmailAlreadyRegisteredException>(() => useCase.Execute(RegisterRequestBuilder.Build()));
        Assert.Equal("Email already registered.", exception.Message);
        Assert.False(store.Saved);
    }

    [Fact]
    public async Task Register_NormalizesEmailBeforeLookupAndPersistence()
    {
        var store = new TestStore();
        var tokens = new JwtTokenService(new JwtSettings { SigningKey = new string('k', 64) });
        var useCase = new RegisterUseCase(store, new Argon2idPasswordHasher(), new TokenIssuer(tokens, store), store);
        var request = new RegisterRequest { Name = "Test User", Email = " TEST@example.com ", Password = "Strong-password-123!" };

        await useCase.Execute(request);

        Assert.Equal("test@example.com", store.LastEmailLookup);
        Assert.Equal("test@example.com", store.User!.Email);
    }

    private sealed class TestStore : IUserRepository, IRefreshTokenRepository, IUnitOfWork
    {
        public User? User { get; set; }
        public RefreshTokens? Token { get; private set; }
        public bool Saved { get; private set; }
        public string? LastEmailLookup { get; private set; }
        public Task<User?> FindByEmail(string email, CancellationToken cancellationToken)
        {
            LastEmailLookup = email;
            return Task.FromResult(User);
        }
        public Task<User?> FindById(Guid id, CancellationToken cancellationToken) => Task.FromResult(User);
        public void Add(User user) => User = user;
        public void Add(RefreshTokens token) => Token = token;
        public Task SaveChanges(CancellationToken cancellationToken) { Saved = true; return Task.CompletedTask; }
        public Task<RefreshTokens?> FindForUpdate(string tokenHash, CancellationToken cancellationToken) => Task.FromResult(Token);
        public Task RevokeFamily(Guid familyId, DateTime now, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ITransaction> BeginTransaction(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
