using CarInsurance.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Owner> Owners => Set<Owner>();
    public DbSet<Car> Cars => Set<Car>();
    public DbSet<InsurancePolicy> Policies => Set<InsurancePolicy>();
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<ProcessedExpiration> ProcessedExpirations => Set<ProcessedExpiration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Car>()
            .HasIndex(c => c.Vin)
            .IsUnique(false); // TODO: set true and handle conflicts

        modelBuilder.Entity<InsurancePolicy>()
            .Property(p => p.StartDate)
            .IsRequired();

        modelBuilder.Entity<InsurancePolicy>()
            .Property(p => p.EndDate)
            .IsRequired(); // EndDate is now required at database level

        modelBuilder.Entity<Claim>()
            .Property(c => c.Description)
            .IsRequired()
            .HasMaxLength(500);

        modelBuilder.Entity<Claim>()
            .Property(c => c.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<ProcessedExpiration>()
            .HasIndex(p => p.PolicyId)
            .IsUnique(); // Ensure we can't process the same policy expiration twice

        modelBuilder.Entity<ProcessedExpiration>()
            .Property(p => p.ProcessedAt)
            .IsRequired();
    }
}

public static class SeedData
{
    public static void EnsureSeeded(AppDbContext db)
    {
        if (db.Owners.Any()) return;

        var ana = new Owner { Name = "Ana Pop", Email = "ana.pop@example.com" };
        var bogdan = new Owner { Name = "Bogdan Ionescu", Email = "bogdan.ionescu@example.com" };
        db.Owners.AddRange(ana, bogdan);
        db.SaveChanges();

        var car1 = new Car { Vin = "VIN12345", Make = "Dacia", Model = "Logan", YearOfManufacture = 2018, OwnerId = ana.Id };
        var car2 = new Car { Vin = "VIN67890", Make = "VW", Model = "Golf", YearOfManufacture = 2021, OwnerId = bogdan.Id };
        db.Cars.AddRange(car1, car2);
        db.SaveChanges();

        db.Policies.AddRange(
            new InsurancePolicy { CarId = car1.Id, Provider = "Allianz", StartDate = new DateOnly(2024,1,1), EndDate = new DateOnly(2024,12,31) },
            new InsurancePolicy { CarId = car1.Id, Provider = "Groupama", StartDate = new DateOnly(2025,1,1), EndDate = new DateOnly(2025,12,31) },
            new InsurancePolicy { CarId = car2.Id, Provider = "Allianz", StartDate = new DateOnly(2025,3,1), EndDate = new DateOnly(2025,9,30) },
            // Add a policy that expired recently for testing the background service
            new InsurancePolicy { CarId = car2.Id, Provider = "Test Insurance", StartDate = new DateOnly(2024,1,1), EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-1)) }
        );
        db.SaveChanges();

        db.Claims.AddRange(
            new Claim { CarId = car1.Id, ClaimDate = new DateOnly(2024,6,15), Description = "Minor fender bender", Amount = 1250.50m },
            new Claim { CarId = car1.Id, ClaimDate = new DateOnly(2024,9,3), Description = "Windshield replacement", Amount = 350.00m },
            new Claim { CarId = car2.Id, ClaimDate = new DateOnly(2025,5,20), Description = "Hail damage repair", Amount = 2100.75m }
        );
        db.SaveChanges();
    }
}
