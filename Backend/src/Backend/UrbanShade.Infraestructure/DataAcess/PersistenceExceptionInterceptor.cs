using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using UrbanShade.Exceptions;

namespace UrbanShade.Infraestructure.DataAcess;

internal sealed class PersistenceExceptionInterceptor : SaveChangesInterceptor
{
    public override void SaveChangesFailed(DbContextErrorEventData eventData) =>
        TranslateException(eventData.Exception);

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        TranslateException(eventData.Exception);
        return Task.CompletedTask;
    }

    private static void TranslateException(Exception exception)
    {
        if (exception is DbUpdateException
            {
                InnerException: PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: "IX_Users_Email"
                }
            })
        {
            throw new EmailAlreadyRegisteredException(exception);
        }
    }
}
