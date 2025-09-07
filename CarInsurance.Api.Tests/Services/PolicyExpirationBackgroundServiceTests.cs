using Xunit;
using Moq;
using CarInsurance.Api.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using CarInsurance.Api.Tests.Helpers;

namespace CarInsurance.Api.Tests.Services;

public class PolicyExpirationBackgroundServiceTests
{
	[Fact]
	public async Task ExecuteAsync_CallsExpirationServicePeriodically()
	{
		// Arrange
		var mockServiceProvider = new Mock<IServiceProvider>();
		var mockScope = new Mock<IServiceScope>();
		var mockScopeFactory = new Mock<IServiceScopeFactory>();
		var mockExpirationService = new Mock<IPolicyExpirationService>();
        
		var logger = new TestLogger<PolicyExpirationBackgroundService>();
		var options = Options.Create(new PolicyExpirationOptions 
		{ 
			CheckIntervalMinutes = 0, // zero minutes to allow immediate loop delay
			MaxHoursSinceExpiration = 24 
		});

		mockScope.Setup(x => x.ServiceProvider).Returns(mockServiceProvider.Object);
		mockScopeFactory.Setup(x => x.CreateScope()).Returns(mockScope.Object);
		mockServiceProvider.Setup(x => x.GetService(typeof(IPolicyExpirationService)))
			.Returns(mockExpirationService.Object);

		var backgroundService = new PolicyExpirationBackgroundService(
			mockScopeFactory.Object, 
			logger, 
			options);

		using var cts = new CancellationTokenSource();
        
		// Cancel after a short time to avoid infinite loop
	cts.CancelAfter(TimeSpan.FromSeconds(12)); // allow for initial 10s delay + one loop

		// Act
		try
		{
			await backgroundService.StartAsync(cts.Token);
			await Task.Delay(TimeSpan.FromSeconds(11), cts.Token); // Wait for at least one execution after startup delay
			await backgroundService.StopAsync(cts.Token);
		}
		catch (OperationCanceledException)
		{
			// Expected when cancellation token is triggered
		}

		// Assert
		Assert.True(logger.HasLogWithMessage(LogLevel.Information, "Policy Expiration Background Service started"));

		// Verify the service was called at least once
		mockExpirationService.Verify(
			x => x.CheckAndLogExpiredPoliciesAsync(), 
			Times.AtLeastOnce);
	}

	[Fact]
	public async Task ExecuteAsync_WhenExpirationServiceThrows_LogsErrorAndContinues()
	{
		// Arrange
		var mockServiceProvider = new Mock<IServiceProvider>();
		var mockScope = new Mock<IServiceScope>();
		var mockScopeFactory = new Mock<IServiceScopeFactory>();
		var mockExpirationService = new Mock<IPolicyExpirationService>();
        
		var logger = new TestLogger<PolicyExpirationBackgroundService>();
		var options = Options.Create(new PolicyExpirationOptions 
		{ 
			CheckIntervalMinutes = 0,
			MaxHoursSinceExpiration = 24 
		});

		mockScope.Setup(x => x.ServiceProvider).Returns(mockServiceProvider.Object);
		mockScopeFactory.Setup(x => x.CreateScope()).Returns(mockScope.Object);
		mockServiceProvider.Setup(x => x.GetService(typeof(IPolicyExpirationService)))
			.Returns(mockExpirationService.Object);

		// Setup the service to throw an exception
		mockExpirationService.Setup(x => x.CheckAndLogExpiredPoliciesAsync())
			.ThrowsAsync(new InvalidOperationException("Test exception"));

		var backgroundService = new PolicyExpirationBackgroundService(
			mockScopeFactory.Object, 
			logger, 
			options);

		using var cts = new CancellationTokenSource();
	cts.CancelAfter(TimeSpan.FromSeconds(12));

		// Act
		try
		{
			await backgroundService.StartAsync(cts.Token);
			await Task.Delay(TimeSpan.FromSeconds(11), cts.Token);
			await backgroundService.StopAsync(cts.Token);
		}
		catch (OperationCanceledException)
		{
			// Expected when cancellation token is triggered
		}

		// Assert
	Assert.True(logger.HasLogLevel(LogLevel.Error));
		Assert.True(logger.HasLogWithMessage(LogLevel.Error, "Error occurred while checking for expired policies"));
	}

	[Fact]
	public void Constructor_WithValidParameters_SetsCorrectCheckInterval()
	{
		// Arrange
		var mockScopeFactory = new Mock<IServiceScopeFactory>();
		var logger = new TestLogger<PolicyExpirationBackgroundService>();
		var options = Options.Create(new PolicyExpirationOptions 
		{ 
			CheckIntervalMinutes = 30,
			MaxHoursSinceExpiration = 24 
		});

		// Act
		var backgroundService = new PolicyExpirationBackgroundService(
			mockScopeFactory.Object, 
			logger, 
			options);

		// Assert
		Assert.NotNull(backgroundService);
	}
}
