using Microsoft.EntityFrameworkCore;
using WeightApiService.Core.Models;

namespace WeightApiService.Infrastructure.Persistence;

public class TgDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Measurement> Measurement { get; set; }

    public TgDbContext(DbContextOptions<TgDbContext> options ) :
        base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Measurement>()
            .HasOne(m => m.User)
            .WithMany(u => u.Measurements)
            .HasForeignKey(m => m.UserId);
    }
}