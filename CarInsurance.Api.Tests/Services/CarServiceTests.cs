using Xunit;
using Moq;
using CarInsurance.Api.Tests.Helpers;

namespace CarInsurance.Api.Tests.Services;

public class CarServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly CarService _carService;

    public CarServiceTests()
    {
        _context = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
        _carService = new CarService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task IsInsuranceValidAsync_WithValidCarAndValidDate_ReturnsTrue()
    {
        // Arrange
        await TestDbContextFactory.SeedTestDataAsync(_context);
        var carId = 1L;
        var validDate = DateOnly.FromDateTime(DateTime.Today);

        // Act
        var result = await _carService.IsInsuranceValidAsync(carId, validDate);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsInsuranceValidAsync_WithValidCarAndExpiredDate_ReturnsFalse()
    {
        // Arrange
        await TestDbContextFactory.SeedTestDataAsync(_context);
        var carId = 2L; // This car has an expired policy
        var validDate = DateOnly.FromDateTime(DateTime.Today);

        // Act
        var result = await _carService.IsInsuranceValidAsync(carId, validDate);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsInsuranceValidAsync_WithInvalidCarId_ThrowsKeyNotFoundException()
    {
        // Arrange
        await TestDbContextFactory.SeedTestDataAsync(_context);
        var invalidCarId = 999L;
        var validDate = DateOnly.FromDateTime(DateTime.Today);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _carService.IsInsuranceValidAsync(invalidCarId, validDate));
        
        Assert.Contains("Car with ID 999 not found", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task IsInsuranceValidAsync_WithNonPositiveCarId_ThrowsArgumentException(long invalidCarId)
    {
        // Arrange
        var validDate = DateOnly.FromDateTime(DateTime.Today);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _carService.IsInsuranceValidAsync(invalidCarId, validDate));
        
        Assert.Contains("Car ID must be a positive number", exception.Message);
        Assert.Equal("carId", exception.ParamName);
    }

    [Fact]
    public async Task IsInsuranceValidAsync_WithInvalidFutureDate_ThrowsArgumentException()
    {
        // Arrange
        await TestDbContextFactory.SeedTestDataAsync(_context);
        var carId = 1L;
        var invalidDate = DateOnly.FromDateTime(DateTime.Today).AddYears(51);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _carService.IsInsuranceValidAsync(carId, invalidDate));
        
        Assert.Contains("Date must be between", exception.Message);
    Assert.Equal("date", exception.ParamName);
    }

    [Fact]
    public async Task IsInsuranceValidAsync_WithBoundaryDateMinimum_DoesNotThrow()
    {
        // Arrange
        await TestDbContextFactory.SeedTestDataAsync(_context);
        var carId = 1L;
        var boundaryDate = new DateOnly(1900, 1, 1);

        // Act & Assert
        // Should not throw exception
        var result = await _carService.IsInsuranceValidAsync(carId, boundaryDate);
        Assert.False(result); // No policy covers this old date
    }

    [Fact]
    public async Task IsInsuranceValidAsync_WithBoundaryDateMaximum_DoesNotThrow()
    {
        // Arrange
        await TestDbContextFactory.SeedTestDataAsync(_context);
        var carId = 1L;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var boundaryDate = today.AddYears(50);

        // Act & Assert
        // Should not throw exception
        var result = await _carService.IsInsuranceValidAsync(carId, boundaryDate);
        Assert.False(result); // No policy covers this future date
    }

    [Fact]
    public async Task CreateCarAsync_WithValidData_ReturnsCarDto()
    {
        // Arrange
        await TestDbContextFactory.SeedTestDataAsync(_context);
        var request = new CreateCarRequest("VIN11111111111111", "Ford", "Focus", 2022, 1);

        // Act
        var result = await _carService.CreateCarAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("VIN11111111111111", result.Vin);
        Assert.Equal("Ford", result.Make);
        Assert.Equal("Focus", result.Model);
        Assert.Equal(2022, result.Year);
        Assert.Equal(1, result.OwnerId);
    }

    [Fact]
    public async Task CreateCarAsync_WithDuplicateVin_ThrowsInvalidOperationException()
    {
        // Arrange
        await TestDbContextFactory.SeedTestDataAsync(_context);
        var request = new CreateCarRequest("VIN12345678901234", "Ford", "Focus", 2022, 1); // Duplicate VIN

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _carService.CreateCarAsync(request));
        
        Assert.Contains("A car with VIN 'VIN12345678901234' already exists", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CreateCarAsync_WithInvalidVin_ThrowsArgumentException(string invalidVin)
    {
        // Arrange
        var request = new CreateCarRequest(invalidVin!, "Ford", "Focus", 2022, 1);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _carService.CreateCarAsync(request));
        
        Assert.Contains("VIN is required", exception.Message);
        Assert.Equal("Vin", exception.ParamName);
    }

    [Theory]
    [InlineData("SHORT")]
    [InlineData("VIN123456789012345678")] // Too long
    public async Task CreateCarAsync_WithInvalidVinLength_ThrowsArgumentException(string invalidVin)
    {
        // Arrange
        var request = new CreateCarRequest(invalidVin, "Ford", "Focus", 2022, 1);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _carService.CreateCarAsync(request));
        
        Assert.Contains("VIN must be exactly 17 characters long", exception.Message);
        Assert.Equal("Vin", exception.ParamName);
    }

    [Fact]
    public async Task CreateCarAsync_WithNonExistentOwner_ThrowsKeyNotFoundException()
    {
        // Arrange
        await TestDbContextFactory.SeedTestDataAsync(_context);
        var request = new CreateCarRequest("VIN11111111111111", "Ford", "Focus", 2022, 999);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _carService.CreateCarAsync(request));
        
        Assert.Contains("Owner with ID 999 not found", exception.Message);
    }

    [Theory]
    [InlineData(1899)]
    [InlineData(10000)]
    public async Task CreateCarAsync_WithInvalidYear_ThrowsArgumentException(int invalidYear)
    {
        // Arrange
        var request = new CreateCarRequest("VIN11111111111111", "Ford", "Focus", invalidYear, 1);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _carService.CreateCarAsync(request));
        
        Assert.Contains("Year of manufacture must be between 1900 and 9999", exception.Message);
        Assert.Equal("YearOfManufacture", exception.ParamName);
    }

    [Fact]
    public async Task CreateInsurancePolicyAsync_WithValidData_ReturnsInsurancePolicyDto()
    {
        // Arrange
        await TestDbContextFactory.SeedTestDataAsync(_context);
        var carId = 1L;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var request = new CreateInsurancePolicyRequest("NewInsurance", today.AddDays(100), today.AddDays(200));

        // Act
        var result = await _carService.CreateInsurancePolicyAsync(carId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(carId, result.CarId);
        Assert.Equal("NewInsurance", result.Provider);
        Assert.Equal(today.AddDays(100), result.StartDate);
        Assert.Equal(today.AddDays(200), result.EndDate);
    }

    [Fact]
    public async Task CreateInsurancePolicyAsync_WithOverlappingDates_ThrowsInvalidOperationException()
    {
        // Arrange
        await TestDbContextFactory.SeedTestDataAsync(_context);
        var carId = 1L;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var request = new CreateInsurancePolicyRequest("OverlapInsurance", today.AddDays(-10), today.AddDays(10)); // Overlaps with existing policy

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _carService.CreateInsurancePolicyAsync(carId, request));
        
        Assert.Contains("Policy dates overlap with existing policy", exception.Message);
    }

    [Fact]
    public async Task CreateInsurancePolicyAsync_WithStartDateAfterEndDate_ThrowsArgumentException()
    {
        // Arrange
        await TestDbContextFactory.SeedTestDataAsync(_context);
        var carId = 1L;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var request = new CreateInsurancePolicyRequest("InvalidInsurance", today.AddDays(10), today.AddDays(5)); // Start after end

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _carService.CreateInsurancePolicyAsync(carId, request));
        
        Assert.Contains("Start date must be before end date", exception.Message);
        Assert.Equal("StartDate", exception.ParamName);
    }

    [Fact]
    public async Task CreateInsurancePolicyAsync_WithEndDateInPast_ThrowsArgumentException()
    {
        // Arrange
        await TestDbContextFactory.SeedTestDataAsync(_context);
        var carId = 1L;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var request = new CreateInsurancePolicyRequest("PastInsurance", today.AddDays(-10), today.AddDays(-5)); // End date in past

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _carService.CreateInsurancePolicyAsync(carId, request));
        
        Assert.Contains("End date cannot be in the past", exception.Message);
        Assert.Equal("EndDate", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CreateInsurancePolicyAsync_WithInvalidProvider_ThrowsArgumentException(string invalidProvider)
    {
        // Arrange
        await TestDbContextFactory.SeedTestDataAsync(_context);
        var carId = 1L;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var request = new CreateInsurancePolicyRequest(invalidProvider!, today.AddDays(100), today.AddDays(200));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _carService.CreateInsurancePolicyAsync(carId, request));
        
        Assert.Contains("Provider is required", exception.Message);
        Assert.Equal("Provider", exception.ParamName);
    }
}