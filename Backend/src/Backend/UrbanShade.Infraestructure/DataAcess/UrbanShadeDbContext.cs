using Microsoft.EntityFrameworkCore;
using UrbanShade.Domain.Entities;

namespace UrbanShade.Infraestructure.DataAcess;

internal class UrbanShadeDbContext : DbContext
{

    public UrbanShadeDbContext(DbContextOptions<UrbanShadeDbContext> options) : base(options){ }

    public DbSet<User> Users { get; set; }
    public DbSet<RefreshTokens> RefreshTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasIndex(user => user.Email)
            .IsUnique();

        modelBuilder.Entity<RefreshTokens>().HasOne<User>().WithMany().HasForeignKey(rt => rt.UserId).OnDelete(DeleteBehavior.Cascade);
    }

}
