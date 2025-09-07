using Xunit;
using CarInsurance.Api.Tests.Helpers;
using CarInsurance.Api.Configuration;
using Microsoft.Extensions.Options;

namespace CarInsurance.Api.Tests.Services;

public class PolicyExpirationServiceTests : IDisposable
{
	private readonly AppDbContext _context;
	private readonly TestLogger<PolicyExpirationService> _logger;

	public PolicyExpirationServiceTests()
	{
		_context = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
		_logger = new TestLogger<PolicyExpirationService>();
	}

	private PolicyExpirationService CreateService(int maxHours)
		=> new PolicyExpirationService(_context, _logger, Options.Create(new PolicyExpirationOptions { MaxHoursSinceExpiration = maxHours }));

	public void Dispose()
	{
		_context.Dispose();
	}

	[Fact]
	public async Task CheckAndLogExpiredPoliciesAsync_WithRecentlyExpiredPolicy_LogsWarningAndMarksProcessed()
	{
		// EndDate is yesterday to be considered expired; set a large threshold so it logs
		await SeedDataWithExpiredPolicyAsync(daysAgo: 1);

		var svc = CreateService(maxHours: 1000);
		await svc.CheckAndLogExpiredPoliciesAsync();

		Assert.True(_logger.HasLogLevel(LogLevel.Warning));
		Assert.True(_logger.HasLogWithMessage(LogLevel.Warning, "POLICY EXPIRED"));
		Assert.True(_logger.HasLogWithMessage(LogLevel.Warning, "Provider: TestProvider"));

	var processedExpiration = await _context.ProcessedExpirations.FirstOrDefaultAsync();
		Assert.NotNull(processedExpiration);
		Assert.Equal(1, processedExpiration.PolicyId);
	}

	[Fact]
	public async Task CheckAndLogExpiredPoliciesAsync_WithOldExpiredPolicy_DoesNotLogWarning()
	{
		await SeedDataWithExpiredPolicyAsync(daysAgo: 10);

		var svc = CreateService(maxHours: 1); // too small threshold
		await svc.CheckAndLogExpiredPoliciesAsync();

		Assert.False(_logger.HasLogLevel(LogLevel.Warning));
		var processedExpiration = await _context.ProcessedExpirations.FirstOrDefaultAsync();
		Assert.Null(processedExpiration);
	}

	[Fact]
	public async Task CheckAndLogExpiredPoliciesAsync_WithAlreadyProcessedPolicy_DoesNotLogAgain()
	{
		await SeedDataWithExpiredPolicyAsync(daysAgo: 1);
		_context.ProcessedExpirations.Add(new ProcessedExpiration
		{
			PolicyId = 1,
			ExpirationDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-1)),
			ProcessedAt = DateTime.Now.AddMinutes(-10)
		});
		await _context.SaveChangesAsync();

		var svc = CreateService(maxHours: 1000);
		await svc.CheckAndLogExpiredPoliciesAsync();

		Assert.False(_logger.HasLogLevel(LogLevel.Warning));
	}

	[Fact]
	public async Task CheckAndLogExpiredPoliciesAsync_WithNoExpiredPolicies_LogsDebugMessage()
	{
		await SeedDataWithActivePolicy();

		var svc = CreateService(maxHours: 1000);
		await svc.CheckAndLogExpiredPoliciesAsync();

		Assert.True(_logger.HasLogLevel(LogLevel.Debug));
		Assert.True(_logger.HasLogWithMessage(LogLevel.Debug, "No expired policies found to process"));
	}

	[Fact]
	public async Task CheckAndLogExpiredPoliciesAsync_WithMultipleExpiredPolicies_ProcessesAll()
	{
		await SeedMultipleExpiredPoliciesAsync();

		var svc = CreateService(maxHours: 1000);
		await svc.CheckAndLogExpiredPoliciesAsync();

		var warningLogs = _logger.GetLogs(LogLevel.Warning).ToList();
		Assert.Equal(2, warningLogs.Count);
		Assert.True(_logger.HasLogWithMessage(LogLevel.Information, "Processed 2 expired policies"));

		var processedCount = await _context.ProcessedExpirations.CountAsync();
		Assert.Equal(2, processedCount);
	}

	[Fact]
	public async Task CheckAndLogExpiredPoliciesAsync_WithMissingProvider_UsesUnknownProvider()
	{
		await SeedDataWithExpiredPolicyAsync(daysAgo: 1, provider: null);

		var svc = CreateService(maxHours: 1000);
		await svc.CheckAndLogExpiredPoliciesAsync();

		Assert.True(_logger.HasLogLevel(LogLevel.Warning));
		Assert.True(_logger.HasLogWithMessage(LogLevel.Warning, "Provider: Unknown"));
	}

	[Fact]
	public async Task CheckAndLogExpiredPoliciesAsync_WithinLargeThreshold_LogsWarning()
	{
		await SeedDataWithExpiredPolicyAsync(daysAgo: 1);
		var svc = CreateService(maxHours: 1000);
		await svc.CheckAndLogExpiredPoliciesAsync();
		Assert.True(_logger.HasLogLevel(LogLevel.Warning));
	}

	[Fact]
	public async Task CheckAndLogExpiredPoliciesAsync_BeyondSmallThreshold_DoesNotLogWarning()
	{
		await SeedDataWithExpiredPolicyAsync(daysAgo: 2);
		var svc = CreateService(maxHours: 1);
		await svc.CheckAndLogExpiredPoliciesAsync();
		Assert.False(_logger.HasLogLevel(LogLevel.Warning));
	}

	private async Task SeedDataWithExpiredPolicyAsync(int daysAgo, string? provider = "TestProvider")
	{
		var owner = new Owner { Id = 1, Name = "Test Owner", Email = "test@example.com" };
		var car = new Car { Id = 1, Vin = "VIN12345678901234", Make = "Toyota", Model = "Camry", YearOfManufacture = 2020, OwnerId = 1, Owner = owner };
		var expiredDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-Math.Max(daysAgo, 1)));
		var policy = new InsurancePolicy
		{
			Id = 1,
			CarId = 1,
			Car = car,
			Provider = provider,
			StartDate = expiredDate.AddDays(-30),
			EndDate = expiredDate
		};

		_context.Owners.Add(owner);
		_context.Cars.Add(car);
		_context.Policies.Add(policy);
		await _context.SaveChangesAsync();
	}

	private async Task SeedDataWithActivePolicy()
	{
		var owner = new Owner { Id = 1, Name = "Test Owner", Email = "test@example.com" };
		var car = new Car { Id = 1, Vin = "VIN12345678901234", Make = "Toyota", Model = "Camry", YearOfManufacture = 2020, OwnerId = 1, Owner = owner };
		var today = DateOnly.FromDateTime(DateTime.Today);
		var policy = new InsurancePolicy
		{
			Id = 1,
			CarId = 1,
			Car = car,
			Provider = "ActiveProvider",
			StartDate = today.AddDays(-30),
			EndDate = today.AddDays(30)
		};

		_context.Owners.Add(owner);
		_context.Cars.Add(car);
		_context.Policies.Add(policy);
		await _context.SaveChangesAsync();
	}

	private async Task SeedMultipleExpiredPoliciesAsync()
	{
		var owner1 = new Owner { Id = 1, Name = "Owner 1", Email = "owner1@example.com" };
		var owner2 = new Owner { Id = 2, Name = "Owner 2", Email = "owner2@example.com" };
        
		var car1 = new Car { Id = 1, Vin = "VIN11111111111111", Make = "Toyota", Model = "Camry", YearOfManufacture = 2020, OwnerId = 1, Owner = owner1 };
		var car2 = new Car { Id = 2, Vin = "VIN22222222222222", Make = "Honda", Model = "Civic", YearOfManufacture = 2021, OwnerId = 2, Owner = owner2 };
        
		var expiredDate1 = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
		var expiredDate2 = DateOnly.FromDateTime(DateTime.Today.AddDays(-2));
        
		var policy1 = new InsurancePolicy
		{
			Id = 1,
			CarId = 1,
			Car = car1,
			Provider = "Provider1",
			StartDate = expiredDate1.AddDays(-30),
			EndDate = expiredDate1
		};
        
		var policy2 = new InsurancePolicy
		{
			Id = 2,
			CarId = 2,
			Car = car2,
			Provider = "Provider2",
			StartDate = expiredDate2.AddDays(-30),
			EndDate = expiredDate2
		};

		_context.Owners.AddRange(owner1, owner2);
		_context.Cars.AddRange(car1, car2);
		_context.Policies.AddRange(policy1, policy2);
		await _context.SaveChangesAsync();
	}
}
