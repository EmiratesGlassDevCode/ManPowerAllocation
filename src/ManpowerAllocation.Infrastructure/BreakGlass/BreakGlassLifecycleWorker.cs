using ManpowerAllocation.Application.BreakGlass;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ManpowerAllocation.Infrastructure.BreakGlass;

/// <summary>
/// Background worker that periodically advances the break-glass lifecycle: it detects a
/// manual enable (to alert and start the countdown) and auto-disables the account once the
/// four-hour window elapses, even when no one logs in during that period.
/// </summary>
public sealed class BreakGlassLifecycleWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BreakGlassLifecycleWorker> _logger;

    /// <summary>Initialises the worker.</summary>
    /// <param name="scopeFactory">Factory used to create a scope per poll for scoped services.</param>
    /// <param name="logger">Logger for non-sensitive diagnostics.</param>
    public BreakGlassLifecycleWorker(IServiceScopeFactory scopeFactory, ILogger<BreakGlassLifecycleWorker> logger)
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
                var service = scope.ServiceProvider.GetRequiredService<IBreakGlassService>();
                await service.ProcessLifecycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown.
                break;
            }
            catch (Exception ex)
            {
                // A transient failure (for example the database being briefly unavailable) must
                // not stop the worker; log and retry on the next tick.
                _logger.LogError(ex, "Break-glass lifecycle processing failed; will retry on the next interval.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
