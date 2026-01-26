using Microsoft.EntityFrameworkCore;
using JournalApp.Models;

namespace JournalApp.Data
{
    /// <summary>
    /// Database context for the Journal application using SQLite with EF Core
    /// </summary>
    public class JournalDbContext : DbContext
    {
        public DbSet<JournalEntry> JournalEntries { get; set; }
        public DbSet<AppSettings> AppSettings { get; set; }

        public JournalDbContext(DbContextOptions<JournalDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure JournalEntry
            modelBuilder.Entity<JournalEntry>(entity =>
            {
                entity.HasKey(e => e.Id);

                // Create unique index on EntryDate to ensure one entry per day
                entity.HasIndex(e => e.EntryDate)
                      .IsUnique()
                      .HasDatabaseName("IX_JournalEntries_EntryDate");

                // Additional indexes for performance
                entity.HasIndex(e => e.MoodCategory)
                      .HasDatabaseName("IX_JournalEntries_MoodCategory");

                entity.HasIndex(e => e.Category)
                      .HasDatabaseName("IX_JournalEntries_Category");

                entity.Property(e => e.Content)
                      .IsRequired();

                entity.Property(e => e.PrimaryMood)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(e => e.MoodCategory)
                      .IsRequired()
                      .HasMaxLength(20);

                entity.Property(e => e.Category)
                      .HasMaxLength(50);

                entity.Property(e => e.Tags)
                      .HasMaxLength(500);

                entity.Property(e => e.EntryDate)
                      .HasColumnType("DATE");

                entity.Property(e => e.CreatedAt)
                      .HasDefaultValueSql("datetime('now', 'localtime')");

                entity.Property(e => e.UpdatedAt)
                      .HasDefaultValueSql("datetime('now', 'localtime')");
            });

            // Configure AppSettings
            modelBuilder.Entity<AppSettings>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Theme)
                      .IsRequired()
                      .HasMaxLength(20)
                      .HasDefaultValue("Light");

                entity.Property(e => e.HashedPin)
                      .HasMaxLength(256);

                // Seed default settings
                entity.HasData(new AppSettings
                {
                    Id = 1,
                    Theme = "Light",
                    CurrentStreak = 0,
                    LongestStreak = 0
                });
            });
        }

        /// <summary>
        /// Ensures the database is created and migrations are applied
        /// </summary>
        public async Task InitializeDatabaseAsync()
        {
            try
            {
                // Create database if it doesn't exist
                await Database.EnsureCreatedAsync();

                // Ensure there's at least one AppSettings record
                if (!await AppSettings.AnyAsync())
                {
                    AppSettings.Add(new AppSettings
                    {
                        Theme = "Light",
                        CurrentStreak = 0,
                        LongestStreak = 0
                    });
                    await SaveChangesAsync();
                }

                System.Diagnostics.Debug.WriteLine($"Database initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Database initialization error: {ex.Message}");
                throw;
            }
        }
    }
}