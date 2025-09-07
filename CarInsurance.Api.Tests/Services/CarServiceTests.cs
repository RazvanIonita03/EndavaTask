using CarInsurance.Api.Tests.Helpers;

namespace CarInsurance.Api.Tests.Services;

public class CarServiceTests : IDisposable
{
	private readonly AppDbContext _db;
	private readonly CarService _service;

	public CarServiceTests()
	{
		_db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
		SeedBasic();
		_service = new CarService(_db);
	}

	public void Dispose() => _db.Dispose();

	private void SeedBasic()
	{
		var owner = new Owner { Id = 1, Name = "Owner", Email = "owner@example.com" };
		var car = new Car { Id = 1, Vin = "TESTVIN000000000", Make = "Make", Model = "Model", YearOfManufacture = 2020, OwnerId = 1, Owner = owner };
		var start = DateOnly.FromDateTime(DateTime.Today.AddDays(-10));
		var end = DateOnly.FromDateTime(DateTime.Today.AddDays(10));
		var policy = new InsurancePolicy { Id = 1, CarId = 1, Car = car, Provider = "P1", StartDate = start, EndDate = end };
		_db.Owners.Add(owner);
		_db.Cars.Add(car);
		_db.Policies.Add(policy);
		_db.SaveChanges();
	}

	[Fact]
	public async Task IsInsuranceValidAsync_ValidDateWithinPolicy_ReturnsTrue()
	{
		var date = DateOnly.FromDateTime(DateTime.Today);
		var result = await _service.IsInsuranceValidAsync(1, date);
		Assert.True(result);
	}

	[Fact]
	public async Task IsInsuranceValidAsync_DateTooEarly_Throws()
	{
		var tooEarly = new DateOnly(1899, 12, 31);
		var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.IsInsuranceValidAsync(1, tooEarly));
		Assert.Contains("Date must be between", ex.Message);
	}

	[Fact]
	public async Task IsInsuranceValidAsync_DateTooFarInFuture_Throws()
	{
		var future = DateOnly.FromDateTime(DateTime.Today.AddYears(51));
		var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.IsInsuranceValidAsync(1, future));
		Assert.Contains("Date must be between", ex.Message);
	}

	[Fact]
	public async Task IsInsuranceValidAsync_CarNotFound_Throws()
	{
		var date = DateOnly.FromDateTime(DateTime.Today);
		await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.IsInsuranceValidAsync(999, date));
	}

	[Fact]
	public async Task RegisterClaimAsync_HappyPath_ReturnsClaim()
	{
		var req = new CreateClaimRequest(DateOnly.FromDateTime(DateTime.Today.AddDays(-1)), "Scratched", 100m);
		var resp = await _service.RegisterClaimAsync(1, req);
		Assert.Equal(1, resp.CarId);
		Assert.Equal("Scratched", resp.Description);
		Assert.Equal(100m, resp.Amount);
	}

	[Fact]
	public async Task RegisterClaimAsync_InvalidAmount_Throws()
	{
		var req = new CreateClaimRequest(DateOnly.FromDateTime(DateTime.Today.AddDays(-1)), "Scratched", 0m);
		await Assert.ThrowsAsync<ArgumentException>(() => _service.RegisterClaimAsync(1, req));
	}

	[Fact]
	public async Task RegisterClaimAsync_EmptyDescription_Throws()
	{
		var req = new CreateClaimRequest(DateOnly.FromDateTime(DateTime.Today.AddDays(-1)), " ", 10m);
		await Assert.ThrowsAsync<ArgumentException>(() => _service.RegisterClaimAsync(1, req));
	}

	[Fact]
	public async Task RegisterClaimAsync_FutureDate_Throws()
	{
		var req = new CreateClaimRequest(DateOnly.FromDateTime(DateTime.Today.AddDays(1)), "Ok", 10m);
		await Assert.ThrowsAsync<ArgumentException>(() => _service.RegisterClaimAsync(1, req));
	}

	[Fact]
	public async Task RegisterClaimAsync_CarNotFound_Throws()
	{
		var req = new CreateClaimRequest(DateOnly.FromDateTime(DateTime.Today.AddDays(-1)), "Ok", 10m);
		await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.RegisterClaimAsync(999, req));
	}

	[Fact]
	public async Task GetCarHistoryAsync_ReturnsChronologicalEvents()
	{
		// add second policy and two claims out of order
		var today = DateOnly.FromDateTime(DateTime.Today);
		_db.Policies.Add(new InsurancePolicy { CarId = 1, Provider = "P2", StartDate = today.AddDays(20), EndDate = today.AddDays(30) });
		_db.Claims.Add(new Claim { CarId = 1, ClaimDate = today.AddDays(5), Description = "A", Amount = 50 });
		_db.Claims.Add(new Claim { CarId = 1, ClaimDate = today.AddDays(-1), Description = "B", Amount = 75 });
		await _db.SaveChangesAsync();

		var history = await _service.GetCarHistoryAsync(1);
		Assert.Equal(1, history.CarId);
		Assert.True(history.Events.Count >= 4);
		// Ensure sorted ascending
		var dates = history.Events.Select(e => e.Date).ToList();
		Assert.True(dates.SequenceEqual(dates.OrderBy(d => d)));
		// Ensure claim descriptions present
		Assert.Contains(history.Events, e => e is ClaimEvent ce && ce.Description == "B");
		Assert.Contains(history.Events, e => e is ClaimEvent ce && ce.Description == "A");
	}

	[Fact]
	public async Task GetCarHistoryAsync_CarNotFound_Throws()
	{
		await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.GetCarHistoryAsync(999));
	}
}

