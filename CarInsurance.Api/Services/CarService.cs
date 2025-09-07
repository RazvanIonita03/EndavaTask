using CarInsurance.Api.Data;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Services;

public class CarService(AppDbContext db)
{
    private readonly AppDbContext _db = db;

    public async Task<List<CarDto>> ListCarsAsync()
    {
        return await _db.Cars.Include(c => c.Owner)
            .Select(c => new CarDto(c.Id, c.Vin, c.Make, c.Model, c.YearOfManufacture,
                                    c.OwnerId, c.Owner.Name, c.Owner.Email))
            .ToListAsync();
    }

    public async Task<bool> IsInsuranceValidAsync(long carId, DateOnly date)
    {
        if (carId <= 0)
            throw new ArgumentException("Car ID must be a positive number.", nameof(carId));

        var today = DateOnly.FromDateTime(DateTime.Today);
        var minDate = new DateOnly(1900, 1, 1);
        var maxDate = today.AddYears(50);

        if (date < minDate || date > maxDate)
            throw new ArgumentException($"Date must be between {minDate:yyyy-MM-dd} and {maxDate:yyyy-MM-dd}.", nameof(date));

        var carExists = await _db.Cars.AnyAsync(c => c.Id == carId);
        if (!carExists) throw new KeyNotFoundException($"Car with ID {carId} not found.");

        return await _db.Policies.AnyAsync(p =>
            p.CarId == carId &&
            p.StartDate <= date &&
            p.EndDate >= date  // EndDate is now always present, no null check needed
        );
    }

    public async Task<ClaimResponse> RegisterClaimAsync(long carId, CreateClaimRequest request)
    {
        if (carId <= 0)
            throw new ArgumentException("Car ID must be a positive number.", nameof(carId));

        if (string.IsNullOrWhiteSpace(request.Description))
            throw new ArgumentException("Claim description is required.", nameof(request.Description));

        if (request.Amount <= 0)
            throw new ArgumentException("Claim amount must be greater than zero.", nameof(request.Amount));

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (request.ClaimDate > today)
            throw new ArgumentException("Claim date cannot be in the future.", nameof(request.ClaimDate));

        var carExists = await _db.Cars.AnyAsync(c => c.Id == carId);
        if (!carExists) throw new KeyNotFoundException($"Car with ID {carId} not found.");

        var claim = new Claim
        {
            CarId = carId,
            ClaimDate = request.ClaimDate,
            Description = request.Description,
            Amount = request.Amount
        };

        _db.Claims.Add(claim);
        await _db.SaveChangesAsync();

        return new ClaimResponse(claim.Id, claim.CarId, claim.ClaimDate, claim.Description, claim.Amount);
    }

    public async Task<CarHistoryResponse> GetCarHistoryAsync(long carId)
    {
        if (carId <= 0)
            throw new ArgumentException("Car ID must be a positive number.", nameof(carId));

        var carExists = await _db.Cars.AnyAsync(c => c.Id == carId);
        if (!carExists) throw new KeyNotFoundException($"Car with ID {carId} not found.");

        var policies = await _db.Policies
            .Where(p => p.CarId == carId)
            .ToListAsync();

        var claims = await _db.Claims
            .Where(c => c.CarId == carId)
            .ToListAsync();

        var events = new List<CarHistoryEvent>();

        foreach (var policy in policies)
        {
            events.Add(new PolicyStartEvent(policy.StartDate, policy.Provider ?? "Unknown"));
            events.Add(new PolicyEndEvent(policy.EndDate, policy.Provider ?? "Unknown"));
        }

        foreach (var claim in claims)
        {
            events.Add(new ClaimEvent(claim.ClaimDate, claim.Description, claim.Amount));
        }

        var sortedEvents = events.OrderBy(e => e.Date).ToList();

        return new CarHistoryResponse(carId, sortedEvents);
    }

    public async Task<CarDto> CreateCarAsync(CreateCarRequest request)
    {
        // Validate VIN
        if (string.IsNullOrWhiteSpace(request.Vin))
            throw new ArgumentException("VIN is required.", nameof(request.Vin));

        if (request.Vin.Length != 17)
            throw new ArgumentException("VIN must be exactly 17 characters long.", nameof(request.Vin));

        // Check for duplicate VIN
        var existingCar = await _db.Cars.FirstOrDefaultAsync(c => c.Vin == request.Vin);
        if (existingCar != null)
            throw new InvalidOperationException($"A car with VIN '{request.Vin}' already exists.");

        // Validate year
        if (request.YearOfManufacture < 1900 || request.YearOfManufacture > 9999)
            throw new ArgumentException("Year of manufacture must be between 1900 and 9999.", nameof(request.YearOfManufacture));

        // Validate owner exists
        var ownerExists = await _db.Owners.AnyAsync(o => o.Id == request.OwnerId);
        if (!ownerExists)
            throw new KeyNotFoundException($"Owner with ID {request.OwnerId} not found.");

        var car = new Car
        {
            Vin = request.Vin,
            Make = request.Make,
            Model = request.Model,
            YearOfManufacture = request.YearOfManufacture,
            OwnerId = request.OwnerId
        };

        _db.Cars.Add(car);
        await _db.SaveChangesAsync();

        var owner = await _db.Owners.FindAsync(request.OwnerId);
        return new CarDto(car.Id, car.Vin, car.Make, car.Model, car.YearOfManufacture, 
                         car.OwnerId, owner!.Name, owner.Email);
    }

    public async Task<InsurancePolicyDto> CreateInsurancePolicyAsync(long carId, CreateInsurancePolicyRequest request)
    {
        // Validate car exists
        if (carId <= 0)
            throw new ArgumentException("Car ID must be a positive number.", nameof(carId));

        var carExists = await _db.Cars.AnyAsync(c => c.Id == carId);
        if (!carExists)
            throw new KeyNotFoundException($"Car with ID {carId} not found.");

        // Validate provider
        if (string.IsNullOrWhiteSpace(request.Provider))
            throw new ArgumentException("Provider is required.", nameof(request.Provider));

        // Validate dates
        if (request.StartDate >= request.EndDate)
            throw new ArgumentException("Start date must be before end date.", nameof(request.StartDate));

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (request.EndDate < today)
            throw new ArgumentException("End date cannot be in the past.", nameof(request.EndDate));

        // Check for overlapping policies
        var hasOverlap = await _db.Policies.AnyAsync(p => 
            p.CarId == carId &&
            ((request.StartDate >= p.StartDate && request.StartDate <= p.EndDate) ||
             (request.EndDate >= p.StartDate && request.EndDate <= p.EndDate) ||
             (request.StartDate <= p.StartDate && request.EndDate >= p.EndDate)));

        if (hasOverlap)
            throw new InvalidOperationException("Policy dates overlap with existing policy.");

        var policy = new InsurancePolicy
        {
            CarId = carId,
            Provider = request.Provider,
            StartDate = request.StartDate,
            EndDate = request.EndDate
        };

        _db.Policies.Add(policy);
        await _db.SaveChangesAsync();

        return new InsurancePolicyDto(policy.Id, policy.CarId, policy.Provider, 
                                    policy.StartDate, policy.EndDate);
    }
}
