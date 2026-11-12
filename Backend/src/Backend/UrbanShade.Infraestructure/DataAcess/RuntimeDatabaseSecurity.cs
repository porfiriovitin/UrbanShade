using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace UrbanShade.Infraestructure.DataAcess;

public static class RuntimeDatabaseSecurity
{
    public static async Task Validate(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UrbanShadeDbContext>();
        var unsafeRole = await context.Database.SqlQueryRaw<bool>("""
            SELECT (r.rolsuper OR r.rolcreatedb OR r.rolcreaterole OR r.rolreplication OR r.rolbypassrls
                OR has_schema_privilege(current_user, 'public', 'CREATE')
                OR EXISTS (SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                    WHERE n.nspname = 'public' AND c.relkind IN ('r','p') AND pg_has_role(current_user, c.relowner, 'USAGE'))
                ) AS "Value" FROM pg_roles r WHERE r.rolname = current_user
            """).SingleAsync(cancellationToken);
        if (unsafeRole)
            throw new InvalidOperationException("Production database identity must not administer roles, own application tables or create schema objects. Apply migrations using a separate deployment identity.");
    }
}
