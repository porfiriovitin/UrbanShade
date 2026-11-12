using Microsoft.EntityFrameworkCore.Storage;
using UrbanShade.Domain.Repositories;

namespace UrbanShade.Infraestructure.DataAcess;

internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly UrbanShadeDbContext _context;

    public UnitOfWork(UrbanShadeDbContext context)
    {
        _context = context;
    }

    public async Task SaveChanges(CancellationToken cancellationToken) => await _context.SaveChangesAsync(cancellationToken);

    public async Task<ITransaction> BeginTransaction(CancellationToken cancellationToken) => new Transaction(await _context.Database.BeginTransactionAsync(cancellationToken));

    private sealed class Transaction : ITransaction
    {
        private readonly IDbContextTransaction _transaction;

        public Transaction(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public Task Commit(CancellationToken cancellationToken) => _transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => _transaction.DisposeAsync();
    }
}
