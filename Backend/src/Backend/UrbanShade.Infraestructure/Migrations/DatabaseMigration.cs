using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UrbanShade.Infraestructure.DataAcess;

namespace UrbanShade.Infraestructure.Migrations;

public class DatabaseMigration
{
    public static async Task ExecuteMigrations(IServiceProvider serviceProvider)
    {
        var dbContext = serviceProvider.GetRequiredService<UrbanShadeDbContext>();

        await dbContext.Database.MigrateAsync();
    }
}
