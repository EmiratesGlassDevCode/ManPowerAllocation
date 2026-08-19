using ManpowerAllocation.Application.Allocation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ManpowerAllocation.Infrastructure.Allocation;

/// <summary>
/// Background worker that returns loaned employees to their home department at each shift changeover.
/// It polls once a minute and delegates to <see cref="IAllocationResetService.ProcessBoundaryAsync"/>,
/// which is a no-op unless the auto-reset is enabled and a new day/night boundary has been crossed.
/// </summary>
public sealed class ShiftResetWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ShiftResetWorker> _logger;

    /// <summary>Initialises the worker.</summary>
    public ShiftResetWorker(IServiceScopeFactory scopeFactory, ILogger<ShiftResetWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IAllocationResetService>();
                await service.ProcessBoundaryAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A transient failure must not stop the worker; log and retry on the next tick.
                _logger.LogError(ex, "Shift-reset processing failed; will retry on the next interval.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
