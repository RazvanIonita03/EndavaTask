using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CarInsurance.Api.Tests.Integration;

public class CarsControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
	private readonly WebApplicationFactory<Program> _factory;
	private readonly JsonSerializerOptions _json;

	public CarsControllerIntegrationTests(WebApplicationFactory<Program> factory)
	{
		_factory = factory.WithWebHostBuilder(builder =>
		{
			builder.UseSetting("environment", "Testing");
		});

		_json = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
			Converters = { new DateOnlyJsonConverter() }
		};
	}

	private HttpClient CreateClient()
	{
		var client = _factory.CreateClient();
		return client;
	}

	[Fact]
	public async Task CreateCar_WithValidData_ReturnsCreated()
	{
		var client = CreateClient();
		var body = new { Vin = "VIN00000000000001", Make = "Ford", Model = "Focus", YearOfManufacture = 2022, OwnerId = 1 };
		var resp = await client.PostAsJsonAsync("/api/cars", body);
		Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
	}

	[Fact]
	public async Task CreateCar_WithDuplicateVin_ReturnsConflict()
	{
		var client = CreateClient();
		// Seed an initial car with VIN to trigger conflict
	var first = new { Vin = "DUPLICATEVIN00001", Make = "A", Model = "B", YearOfManufacture = 2020, OwnerId = 1 };
		var firstResp = await client.PostAsJsonAsync("/api/cars", first);
		Assert.Equal(HttpStatusCode.Created, firstResp.StatusCode);

	var body = new { Vin = "DUPLICATEVIN00001", Make = "X", Model = "Y", YearOfManufacture = 2020, OwnerId = 1 };
		var resp = await client.PostAsJsonAsync("/api/cars", body);
		Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
	}

	[Fact]
	public async Task CreateInsurancePolicy_WithValidData_ReturnsCreated()
	{
		var client = CreateClient();
		var today = DateOnly.FromDateTime(DateTime.Today);
		var body = new { Provider = "ProviderZ", StartDate = today.AddDays(400), EndDate = today.AddDays(500) };
		// Use car 1 but ensure no overlap by pushing far in future
		var resp = await client.PostAsJsonAsync("/api/cars/1/policies", body);
		Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
	}

	[Fact]
	public async Task CreateInsurancePolicy_WithOverlappingDates_ReturnsConflict()
	{
		var client = CreateClient();
		var today = DateOnly.FromDateTime(DateTime.Today);
		var body = new { Provider = "Overlap", StartDate = today.AddDays(-10), EndDate = today.AddDays(10) };
		var resp = await client.PostAsJsonAsync("/api/cars/1/policies", body);
		Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
	}

	private class DateOnlyJsonConverter : JsonConverter<DateOnly>
	{
		public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var value = reader.GetString();
			return DateOnly.Parse(value!);
		}

		public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
		{
			writer.WriteStringValue(value.ToString("yyyy-MM-dd"));
		}
	}
}
