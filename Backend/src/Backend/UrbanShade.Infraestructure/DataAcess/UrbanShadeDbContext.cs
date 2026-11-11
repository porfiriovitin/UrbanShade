using Microsoft.EntityFrameworkCore;
using UrbanShade.Domain.Entities;

namespace UrbanShade.Infraestructure.DataAcess;

internal class UrbanShadeDbContext : DbContext
{

    public UrbanShadeDbContext(DbContextOptions<UrbanShadeDbContext> options) : base(options){ }

    public DbSet<User> Users { get; set; }
    public DbSet<Organization> Organizations { get; set; }
    public DbSet<OrganizationMember> OrganizationMembers { get; set; }
    public DbSet<Sensor> Sensors { get; set; }
    public DbSet<City> Cities { get; set; }
    public DbSet<SensorReading> SensorReadings { get; set; }
    public DbSet<ThermalAnalysis> ThermalAnalyses { get; set; }
    public DbSet<RefreshTokens> RefreshTokens { get; set; }

    public DbSet<GeoRegion> GeoRegions { get; set; }
    public DbSet<SatelliteAnalysis> SatelliteAnalyses { get; set; }
    public DbSet<GeoAnalysisJob> GeoAnalysisJobs { get; set; }
    public DbSet<GreenArea> GreenAreas { get; set; }
    public DbSet<GreenAreaRegion> GreenAreaRegions { get; set; }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { return await base.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException exception) when (exception.Entries.Any(entry => entry.Entity is GeoRegion))
        {
            throw new UrbanShade.Exceptions.GeoException(System.Net.HttpStatusCode.Conflict, "The region changed during this request. Reload it and try again.");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.Entity<GeoRegion>().Property(region => region.BoundaryVersion).IsConcurrencyToken();
        modelBuilder.Entity<GeoRegion>().Property(region => region.Name).HasMaxLength(150);
        modelBuilder.Entity<GeoRegion>().Property(region => region.Description).HasMaxLength(500);
        modelBuilder.Entity<GeoRegion>().Property(region => region.Boundary).HasColumnType("geometry(Geometry,4326)");
        modelBuilder.Entity<GeoRegion>().HasIndex(region => region.Boundary).HasMethod("gist");
        modelBuilder.Entity<GeoRegion>().HasOne<City>().WithMany().HasForeignKey(region => region.CityId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<GeoAnalysisJob>().Property(job => job.Boundary).HasColumnType("geometry(Geometry,4326)");
        modelBuilder.Entity<GeoAnalysisJob>().Property(job => job.JobType).HasConversion<string>().HasDefaultValue(UrbanShade.Domain.Enums.GeoJobType.Satellite);
        modelBuilder.Entity<GeoAnalysisJob>().Property(job => job.Status).HasConversion<string>();
        modelBuilder.Entity<GeoAnalysisJob>().Property(job => job.AlgorithmVersion).HasMaxLength(32);
        modelBuilder.Entity<GeoAnalysisJob>().Property(job => job.ErrorMessage).HasMaxLength(500);
        modelBuilder.Entity<GeoAnalysisJob>().Property(job => job.ResultSummary).HasMaxLength(500);
        modelBuilder.Entity<GeoAnalysisJob>().HasOne<GeoRegion>().WithMany().HasForeignKey(job => job.GeoRegionId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<GeoAnalysisJob>().HasIndex(job => new { job.GeoRegionId, job.BoundaryVersion, job.ObservationStartAt, job.ObservationEndAt, job.AlgorithmVersion, job.Parameters, job.JobType }).IsUnique();
        modelBuilder.Entity<GeoAnalysisJob>().HasIndex(job => new { job.JobType, job.Status, job.LeaseExpiresAt, job.CreatedAt });
        modelBuilder.Entity<SatelliteAnalysis>().Property(analysis => analysis.DataSource).HasConversion<string>();
        modelBuilder.Entity<SatelliteAnalysis>().Property(analysis => analysis.AnalysisType).HasConversion<string>();
        modelBuilder.Entity<SatelliteAnalysis>().Property(analysis => analysis.Metadata).HasColumnType("jsonb");
        modelBuilder.Entity<SatelliteAnalysis>().Property(analysis => analysis.AlgorithmVersion).HasMaxLength(32);
        modelBuilder.Entity<SatelliteAnalysis>().HasIndex(analysis => new { analysis.GeoAnalysisJobId, analysis.AnalysisType }).IsUnique();
        modelBuilder.Entity<SatelliteAnalysis>().HasIndex(analysis => new { analysis.GeoRegionId, analysis.BoundaryVersion, analysis.AnalysisType, analysis.ObservationEndAt });
        modelBuilder.Entity<SatelliteAnalysis>().HasOne<GeoRegion>().WithMany().HasForeignKey(analysis => analysis.GeoRegionId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SatelliteAnalysis>().HasOne<GeoAnalysisJob>().WithMany().HasForeignKey(analysis => analysis.GeoAnalysisJobId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<GreenArea>().Property(area => area.Geometry).HasColumnType("geometry(Geometry,4326)");
        modelBuilder.Entity<GreenArea>().Property(area => area.OsmElementType).HasMaxLength(16);
        modelBuilder.Entity<GreenArea>().Property(area => area.SourceMetadata).HasColumnType("jsonb").HasDefaultValue("{}");
        modelBuilder.Entity<GreenArea>().HasIndex(area => new { area.OsmElementType, area.OsmElementId }).IsUnique()
            .HasFilter("\"OsmElementType\" IS NOT NULL AND \"OsmElementId\" IS NOT NULL");
        modelBuilder.Entity<GreenAreaRegion>().HasKey(link => new { link.GreenAreaId, link.GeoRegionId });
        modelBuilder.Entity<GreenAreaRegion>().HasOne<GreenArea>().WithMany().HasForeignKey(link => link.GreenAreaId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<GreenAreaRegion>().HasOne<GeoRegion>().WithMany().HasForeignKey(link => link.GeoRegionId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<GreenArea>().Property(area => area.Source).HasConversion<string>();
        modelBuilder.Entity<GreenArea>().HasIndex(area => area.Geometry).HasMethod("gist");
        modelBuilder.Entity<GreenArea>().HasOne<GeoRegion>().WithMany().HasForeignKey(area => area.GeoRegionId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<GreenArea>().HasOne<SatelliteAnalysis>().WithMany().HasForeignKey(area => area.SatelliteAnalysisId).OnDelete(DeleteBehavior.Restrict);


        modelBuilder.Entity<City>().Property(city => city.Name).HasMaxLength(150);
        modelBuilder.Entity<City>().Property(city => city.State).HasMaxLength(2);
        modelBuilder.Entity<City>().Property(city => city.IbgeCode).HasMaxLength(7);
        modelBuilder.Entity<City>().HasIndex(city => city.IbgeCode).IsUnique();
        modelBuilder.Entity<Sensor>().HasOne<City>().WithMany().HasForeignKey(sensor => sensor.CityId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Sensor>().HasIndex(sensor => new { sensor.CityId, sensor.OrganizationId });

        modelBuilder.Entity<ThermalAnalysis>().Property(analysis => analysis.HeatIndex).HasPrecision(18, 2);
        modelBuilder.Entity<ThermalAnalysis>().Property(analysis => analysis.RiskLevel).HasConversion<string>();
        modelBuilder.Entity<ThermalAnalysis>().Property(analysis => analysis.AlgorithmVersion).HasMaxLength(32);
        modelBuilder.Entity<ThermalAnalysis>().HasIndex(analysis => new { analysis.SensorReadingId, analysis.AlgorithmVersion }).IsUnique();
        modelBuilder.Entity<ThermalAnalysis>().HasOne<SensorReading>().WithMany().HasForeignKey(analysis => analysis.SensorReadingId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Organization>().Property(organization => organization.Name).HasMaxLength(150);
        modelBuilder.Entity<Organization>().Property(organization => organization.Document).HasMaxLength(30);
        modelBuilder.Entity<Organization>().Property(organization => organization.ContactEmail).HasMaxLength(254);
        modelBuilder.Entity<Organization>().Property(organization => organization.ContactPhone).HasMaxLength(30);
        modelBuilder.Entity<Organization>().Property(organization => organization.Type).HasConversion<string>();
        modelBuilder.Entity<Organization>().HasIndex(organization => organization.Document).IsUnique();
        modelBuilder.Entity<OrganizationMember>().HasIndex(member => new { member.OrganizationId, member.UserId }).IsUnique();
        modelBuilder.Entity<OrganizationMember>().HasOne<Organization>().WithMany().HasForeignKey(member => member.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OrganizationMember>().HasOne<User>().WithMany().HasForeignKey(member => member.UserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Sensor>().HasOne<Organization>().WithMany().HasForeignKey(sensor => sensor.OrganizationId).OnDelete(DeleteBehavior.Restrict);

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
