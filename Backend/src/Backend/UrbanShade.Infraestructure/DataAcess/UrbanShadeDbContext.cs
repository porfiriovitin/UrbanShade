using Microsoft.EntityFrameworkCore;
using UrbanShade.Domain.Entities;

namespace UrbanShade.Infraestructure.DataAcess;

internal class UrbanShadeDbContext : DbContext
{

    public UrbanShadeDbContext(DbContextOptions<UrbanShadeDbContext> options) : base(options){ }

    public DbSet<User> Users { get; set; }
    public DbSet<Sensor> Sensors { get; set; }
    public DbSet<SensorReading> SensorReadings { get; set; }
    public DbSet<RefreshTokens> RefreshTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SensorReading>().Property(reading => reading.Temperature).HasPrecision(18, 6);
        modelBuilder.Entity<SensorReading>().Property(reading => reading.Humidity).HasPrecision(18, 6);
        modelBuilder.Entity<SensorReading>().HasIndex(reading => new { reading.SensorId, reading.MeasuredAt });
        modelBuilder.Entity<SensorReading>().HasOne<Sensor>().WithMany().HasForeignKey(reading => reading.SensorId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Sensor>().HasQueryFilter(sensor => sensor.DeletedAt == null);
        modelBuilder.Entity<Sensor>().HasIndex(sensor => sensor.Code).IsUnique();
        modelBuilder.Entity<Sensor>().Property(sensor => sensor.Code).HasMaxLength(50);
        modelBuilder.Entity<Sensor>().Property(sensor => sensor.Name).HasMaxLength(150);
        modelBuilder.Entity<Sensor>().Property(sensor => sensor.Description).HasMaxLength(500);
        modelBuilder.Entity<Sensor>().Property(sensor => sensor.Status).HasConversion<string>();

        modelBuilder.Entity<User>()
            .HasIndex(user => user.Email)
            .IsUnique();

        modelBuilder.Entity<User>().Property(user => user.Role).HasConversion<string>();
        modelBuilder.Entity<RefreshTokens>().HasIndex(token => token.Token).IsUnique();
        modelBuilder.Entity<RefreshTokens>().HasIndex(token => token.FamilyId);

        modelBuilder.Entity<RefreshTokens>().HasOne<User>().WithMany().HasForeignKey(rt => rt.UserId).OnDelete(DeleteBehavior.Cascade);
    }

}
