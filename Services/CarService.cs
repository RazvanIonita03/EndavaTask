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
        var carExists = await _db.Cars.AnyAsync(c => c.Id == carId);
        if (!carExists) throw new KeyNotFoundException($"Car {carId} not found");

        return await _db.Policies.AnyAsync(p =>
            p.CarId == carId &&
            p.StartDate <= date &&
            p.EndDate >= date  // EndDate is now always present, no null check needed
        );
    }

    public async Task<ClaimResponse> RegisterClaimAsync(long carId, CreateClaimRequest request)
    {
        var carExists = await _db.Cars.AnyAsync(c => c.Id == carId);
        if (!carExists) throw new KeyNotFoundException($"Car {carId} not found");

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
        var carExists = await _db.Cars.AnyAsync(c => c.Id == carId);
        if (!carExists) throw new KeyNotFoundException($"Car {carId} not found");

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
}
