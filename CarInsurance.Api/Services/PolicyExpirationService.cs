using CarInsurance.Api.Configuration;
using CarInsurance.Api.Data;
using CarInsurance.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CarInsurance.Api.Services;

public interface IPolicyExpirationService
{
    Task CheckAndLogExpiredPoliciesAsync();
}

public class PolicyExpirationService : IPolicyExpirationService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<PolicyExpirationService> _logger;
    private readonly PolicyExpirationOptions _options;

    public PolicyExpirationService(
        AppDbContext dbContext,
        ILogger<PolicyExpirationService> logger,
        IOptions<PolicyExpirationOptions> options)
    {
        _dbContext = dbContext;
        _logger = logger;
        _options = options.Value;
    }

    public async Task CheckAndLogExpiredPoliciesAsync()
    {
        var now = DateTime.Now;
        var currentDate = DateOnly.FromDateTime(now);

        var expiredPolicies = await _dbContext.Policies
            .Where(p => p.EndDate < currentDate)
            .Where(p => !_dbContext.ProcessedExpirations.Any(pe => pe.PolicyId == p.Id))
            .Include(p => p.Car)
            .ThenInclude(c => c.Owner)
            .ToListAsync();

        foreach (var policy in expiredPolicies)
        {
            var policyEndDateTime = policy.EndDate.ToDateTime(TimeOnly.MinValue);
            var timeSinceExpiration = now - policyEndDateTime;

            if (timeSinceExpiration <= TimeSpan.FromHours(_options.MaxHoursSinceExpiration))
            {
                _logger.LogWarning(
                    "POLICY EXPIRED: Policy ID {PolicyId} for Car {CarVin} (Owner: {OwnerName}) expired on {ExpirationDate}. " +
                    "Provider: {Provider}. Time since expiration: {TimeSinceExpiration:hh\\:mm\\:ss}",
                    policy.Id,
                    policy.Car.Vin,
                    policy.Car.Owner.Name,
                    policy.EndDate,
                    policy.Provider ?? "Unknown",
                    timeSinceExpiration);

                var processedExpiration = new ProcessedExpiration
                {
                    PolicyId = policy.Id,
                    ExpirationDate = policy.EndDate,
                    ProcessedAt = now
                };

                _dbContext.ProcessedExpirations.Add(processedExpiration);
            }
        }

        if (expiredPolicies.Any())
        {
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Processed {Count} expired policies.", expiredPolicies.Count);
        }
        else
        {
            _logger.LogDebug("No expired policies found to process.");
        }
    }
}