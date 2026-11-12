namespace UrbanShade.Domain.Repositories;

public interface IUnitOfWork
{
    Task SaveChanges(CancellationToken cancellationToken);

    Task<ITransaction> BeginTransaction(CancellationToken cancellationToken);
}

public interface ITransaction : IAsyncDisposable
{
    Task Commit(CancellationToken cancellationToken);
}
