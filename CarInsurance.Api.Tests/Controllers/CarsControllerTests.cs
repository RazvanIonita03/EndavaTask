using Microsoft.AspNetCore.Mvc;
using CarInsurance.Api.Tests.Helpers;

public class CarsControllerTests
{
	private static CarInsurance.Api.Controllers.CarsController CreateController(AppDbContext? db = null)
	{
		db ??= TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
		var service = new CarService(db);
		var exp = new StubExpirationService();
		return new CarInsurance.Api.Controllers.CarsController(service, exp);
	}

	[Fact]
	public async Task InsuranceValid_InvalidDateFormat_ReturnsBadRequest()
	{
		var controller = CreateController();
		var result = await controller.IsInsuranceValid(1, "not-a-date");
		var badReq = Assert.IsType<BadRequestObjectResult>(result.Result);
		Assert.Contains("Invalid date format", badReq.Value!.ToString());
	}

	[Fact]
	public async Task InsuranceValid_CarNotFound_ReturnsNotFound()
	{
		using var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
		var controller = CreateController(db);
		var result = await controller.IsInsuranceValid(999, "2025-01-01");
		Assert.IsType<NotFoundObjectResult>(result.Result);
	}

	[Fact]
	public async Task InsuranceValid_InvalidArgument_ReturnsBadRequest()
	{
		var controller = CreateController();
		var result = await controller.IsInsuranceValid(1, "1800-01-01");
		Assert.IsType<BadRequestObjectResult>(result.Result);
	}

	[Fact]
	public async Task InsuranceValid_Valid_ReturnsOkWithPayload()
	{
		using var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
		// Seed owner, car, and policy
		var owner = new Owner { Id = 1, Name = "Owner", Email = "owner@example.com" };
		var car = new Car { Id = 1, Vin = "VALVINVALVINVALV", Make = "M", Model = "X", YearOfManufacture = 2020, OwnerId = 1, Owner = owner };
		var today = DateOnly.FromDateTime(DateTime.Today);
		db.Owners.Add(owner);
		db.Cars.Add(car);
		db.Policies.Add(new InsurancePolicy { CarId = 1, Car = car, Provider = "P1", StartDate = today.AddDays(-1), EndDate = today.AddDays(1) });
		await db.SaveChangesAsync();

		var controller = CreateController(db);
		var result = await controller.IsInsuranceValid(1, today.ToString("yyyy-MM-dd"));
		var ok = Assert.IsType<OkObjectResult>(result.Result);
		var payload = Assert.IsType<InsuranceValidityResponse>(ok.Value);
		Assert.True(payload.Valid);
		Assert.Equal(1, payload.CarId);
	}

	private class StubExpirationService : IPolicyExpirationService
	{
		public Task CheckAndLogExpiredPoliciesAsync() => Task.CompletedTask;
	}
}

