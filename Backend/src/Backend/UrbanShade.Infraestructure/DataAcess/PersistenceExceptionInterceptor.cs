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
        if (exception is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Cities_IbgeCode" } })
            throw new CityException(UrbanShade.Exceptions.ErrorKind.Conflict, "City IBGE code already registered.");

        if (exception is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres } &&
            postgres.ConstraintName is "IX_Organizations_Document" or "IX_OrganizationMembers_OrganizationId_UserId")
        {
            throw new OrganizationException(UrbanShade.Exceptions.ErrorKind.Conflict, "Organization document or membership already registered.");
        }

        if (exception is DbUpdateException
            {
                InnerException: PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: "IX_Sensors_Code"
                }
            })
        {
            throw new SensorException(UrbanShade.Exceptions.ErrorKind.Conflict, "Sensor code already registered.");
        }

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
