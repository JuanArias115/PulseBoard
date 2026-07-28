using Microsoft.EntityFrameworkCore;
using PulseBoard.Api.Models;

namespace PulseBoard.Api.Data;

public sealed class PulseBoardDbContext(DbContextOptions<PulseBoardDbContext> options) : DbContext(options)
{
    public DbSet<CheckIn> CheckIns => Set<CheckIn>();

    public DbSet<Habit> Habits => Set<Habit>();

    public DbSet<HabitCompletion> HabitCompletions => Set<HabitCompletion>();

    public DbSet<BodyMeasurement> BodyMeasurements => Set<BodyMeasurement>();

    public DbSet<Meal> Meals => Set<Meal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CheckIn>(entity =>
        {
            entity.HasKey(checkIn => checkIn.Id);
            entity.Property(checkIn => checkIn.UserId).HasMaxLength(80).IsRequired();
            entity.Property(checkIn => checkIn.LocalDate).HasMaxLength(10).IsRequired();
            entity.Property(checkIn => checkIn.TimeZoneId).HasMaxLength(80).IsRequired();
            entity.Property(checkIn => checkIn.Note).HasMaxLength(1000);
            entity.HasIndex(checkIn => new { checkIn.UserId, checkIn.LocalDate }).IsUnique();
        });

        modelBuilder.Entity<Habit>(entity =>
        {
            entity.HasKey(habit => habit.Id);
            entity.Property(habit => habit.UserId).HasMaxLength(80).IsRequired();
            entity.Property(habit => habit.Name).HasMaxLength(120).IsRequired();
            entity.Property(habit => habit.Category).HasMaxLength(60).IsRequired();
            entity.Property(habit => habit.Frequency).HasMaxLength(40).IsRequired();
            entity.Property(habit => habit.Unit).HasMaxLength(40);
            entity.Property(habit => habit.Notes).HasMaxLength(1000);
        });

        modelBuilder.Entity<HabitCompletion>(entity =>
        {
            entity.HasKey(completion => completion.Id);
            entity.Property(completion => completion.UserId).HasMaxLength(80).IsRequired();
            entity.Property(completion => completion.LocalDate).HasMaxLength(10).IsRequired();
            entity.Property(completion => completion.TimeZoneId).HasMaxLength(80).IsRequired();
            entity.Property(completion => completion.Notes).HasMaxLength(1000);
            entity.Property(completion => completion.Amount).HasPrecision(8, 2);
            entity.HasOne(completion => completion.Habit)
                .WithMany()
                .HasForeignKey(completion => completion.HabitId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(completion => new { completion.UserId, completion.HabitId, completion.LocalDate }).IsUnique();
        });

        modelBuilder.Entity<BodyMeasurement>(entity =>
        {
            entity.HasKey(measurement => measurement.Id);
            entity.Property(measurement => measurement.UserId).HasMaxLength(80).IsRequired();
            entity.Property(measurement => measurement.Source).HasMaxLength(40).IsRequired();
            entity.Property(measurement => measurement.TimeZoneId).HasMaxLength(80).IsRequired();
            entity.Property(measurement => measurement.Notes).HasMaxLength(1000);
            entity.Property(measurement => measurement.WeightKg).HasPrecision(6, 2);
            entity.Property(measurement => measurement.BodyFatPercentage).HasPrecision(5, 2);
            entity.Property(measurement => measurement.MusclePercentage).HasPrecision(5, 2);
            entity.Property(measurement => measurement.BodyWaterPercentage).HasPrecision(5, 2);
            entity.Property(measurement => measurement.BodyMassIndex).HasPrecision(5, 2);
            entity.HasIndex(measurement => new { measurement.UserId, measurement.MeasuredAtUtc }).IsUnique();
        });

        modelBuilder.Entity<Meal>(entity =>
        {
            entity.HasKey(meal => meal.Id);
            entity.Property(meal => meal.UserId).HasMaxLength(80).IsRequired();
            entity.Property(meal => meal.LocalDate).HasMaxLength(10).IsRequired();
            entity.Property(meal => meal.TimeZoneId).HasMaxLength(80).IsRequired();
            entity.Property(meal => meal.Name).HasMaxLength(160).IsRequired();
            entity.Property(meal => meal.MealType).HasMaxLength(40).IsRequired();
            entity.Property(meal => meal.ProteinGrams).HasPrecision(7, 2);
            entity.Property(meal => meal.CarbohydrateGrams).HasPrecision(7, 2);
            entity.Property(meal => meal.FatGrams).HasPrecision(7, 2);
            entity.Property(meal => meal.Notes).HasMaxLength(1000);
            entity.HasIndex(meal => new { meal.UserId, meal.LocalDate });
            entity.HasIndex(meal => new { meal.UserId, meal.IsFavorite });
        });
    }
}
