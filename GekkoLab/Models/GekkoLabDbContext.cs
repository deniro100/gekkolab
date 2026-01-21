namespace GekkoLab.Models;

using Microsoft.EntityFrameworkCore;
public class GekkoLabDbContext : DbContext
{
    public GekkoLabDbContext(DbContextOptions<GekkoLabDbContext> options)
        : base(options)
    {
    }

    public DbSet<SensorReading> SensorReadings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SensorReading>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Timestamp);
            entity.Property(e => e.Temperature).IsRequired();
            entity.Property(e => e.Humidity).IsRequired();
            entity.Property(e => e.Pressure).IsRequired();
        });
    }
}