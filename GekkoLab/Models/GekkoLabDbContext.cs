﻿namespace GekkoLab.Models;

using Microsoft.EntityFrameworkCore;
public class GekkoLabDbContext : DbContext
{
    public GekkoLabDbContext(DbContextOptions<GekkoLabDbContext> options)
        : base(options)
    {
    }

    public DbSet<SensorReading> SensorReadings { get; set; }
    public DbSet<SystemMetrics> SystemMetrics { get; set; }
    public DbSet<WeatherReading> WeatherReadings { get; set; }
    public DbSet<GekkoDetectionResult> GekkoDetections { get; set; }
    public DbSet<GekkoSighting> GekkoSightings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SensorReading>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Timestamp);
            entity.Property(e => e.Temperature).IsRequired();
            entity.Property(e => e.Humidity).IsRequired();
            entity.Property(e => e.Pressure).IsRequired();
            entity.OwnsOne(e => e.Metadata, metadata =>
            {
                metadata.Property(m => m.ReaderType)
                    .HasDefaultValue("unknown");
            });
        });

        modelBuilder.Entity<SystemMetrics>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Timestamp);
            entity.Property(e => e.CpuUsagePercent).IsRequired();
            entity.Property(e => e.MemoryUsagePercent).IsRequired();
            entity.Property(e => e.DiskUsagePercent).IsRequired();
        });

        modelBuilder.Entity<WeatherReading>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Timestamp);
            entity.Property(e => e.Temperature).IsRequired();
            entity.Property(e => e.Humidity).IsRequired();
            entity.Property(e => e.Location).HasMaxLength(100);
            entity.Property(e => e.Source).HasMaxLength(50);
        });

        modelBuilder.Entity<GekkoDetectionResult>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.GekkoDetected);
            entity.HasIndex(e => new { e.Timestamp, e.GekkoDetected });
            entity.Property(e => e.ImagePath).HasMaxLength(500);
            entity.Property(e => e.Label).HasMaxLength(100);
            entity.Property(e => e.Confidence).IsRequired();
        });

        modelBuilder.Entity<GekkoSighting>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Timestamp);
            entity.Property(e => e.ImagePath).HasMaxLength(500);
            entity.Property(e => e.Confidence).IsRequired();
            entity.Property(e => e.Label).HasMaxLength(100);
            entity.Property(e => e.Notes).HasMaxLength(1000);
        });
    }
}