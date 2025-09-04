using CarInsurance.Api.Configuration;
using CarInsurance.Api.Services;
using Microsoft.Extensions.Options;

namespace CarInsurance.Api.Services;

public class PolicyExpirationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PolicyExpirationBackgroundService> _logger;
    private readonly TimeSpan _checkInterval;

    public PolicyExpirationBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<PolicyExpirationBackgroundService> logger,
        IOptions<PolicyExpirationOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _checkInterval = TimeSpan.FromMinutes(options.Value.CheckIntervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Policy Expiration Background Service started at {StartTime}. Check interval: {CheckInterval}",
            DateTime.Now,
            _checkInterval);

        // Small delay to allow the application to fully start
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("Checking for expired policies at {CheckTime}", DateTime.Now);

                using var scope = _scopeFactory.CreateScope();
                var expirationService = scope.ServiceProvider.GetRequiredService<IPolicyExpirationService>();

                await expirationService.CheckAndLogExpiredPoliciesAsync();

                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while checking for expired policies.");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("Policy Expiration Background Service stopped at {StopTime}.", DateTime.Now);
    }
}