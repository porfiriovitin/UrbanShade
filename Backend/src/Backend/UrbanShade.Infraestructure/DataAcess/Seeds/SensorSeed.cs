using Microsoft.EntityFrameworkCore;

namespace UrbanShade.Infraestructure.DataAcess.Seeds;

internal static class SensorSeed
{
    public static async Task Execute(UrbanShadeDbContext context, CancellationToken cancellationToken = default)
    {
        /// :: Seed sensors without replacing changes or deleted records.
        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO "Sensors" ("Id", "Active", "CreatedAt", "Code", "Name", "Description", "Latitude", "Longitude", "Status", "InstalledAt", "UpdatedAt", "DeletedAt")
            VALUES
                ('ac000000-0000-0000-0000-000000000001', true, NOW(), 'URB-001', 'Sensor Praça Central', 'Monitoramento da região central', -22.2935, -44.9332, 'ACTIVE', NULL, NULL, NULL),
                ('ac000000-0000-0000-0000-000000000002', true, NOW(), 'URB-002', 'Sensor Avenida Principal', NULL, -22.2950, -44.9350, 'ACTIVE', NULL, NULL, NULL),
                ('ac000000-0000-0000-0000-000000000003', true, NOW(), 'URB-003', 'Sensor Parque Municipal', NULL, -22.2900, -44.9300, 'ACTIVE', NULL, NULL, NULL),
                ('ac000000-0000-0000-0000-000000000004', true, NOW(), 'URB-004', 'Sensor Região Comercial', NULL, -22.2970, -44.9370, 'MAINTENANCE', NULL, NULL, NULL),
                ('ac000000-0000-0000-0000-000000000005', true, NOW(), 'URB-005', 'Sensor Bairro Residencial', NULL, -22.2990, -44.9390, 'INACTIVE', NULL, NULL, NULL)
            ON CONFLICT DO NOTHING;
            """, cancellationToken);
    }
}
