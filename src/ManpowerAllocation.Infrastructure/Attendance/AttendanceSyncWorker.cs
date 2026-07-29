using ManpowerAllocation.Application.Attendance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ManpowerAllocation.Infrastructure.Attendance;

/// <summary>
/// Periodically synchronises employee attendance from the external view. A failed run is logged
/// and retried on the next tick; it never stops the worker or throws, so a transient outage of
/// the attendance database does not take the application down.
/// </summary>
public sealed class AttendanceSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AttendanceOptions _options;
    private readonly ILogger<AttendanceSyncWorker> _logger;

    /// <summary>Initialises the worker.</summary>
    /// <param name="scopeFactory">Factory used to create a scope per run for scoped services.</param>
    /// <param name="options">Attendance options (enabled flag and interval).</param>
    /// <param name="logger">Logger for non-sensitive diagnostics.</param>
    public AttendanceSyncWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<AttendanceOptions> options,
        ILogger<AttendanceSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Attendance synchronisation is disabled by configuration.");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.SyncIntervalMinutes));
        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IAttendanceSyncService>();
                var result = await service.SyncAsync("scheduled", stoppingToken);

                if (result.Success)
                {
                    _logger.LogInformation(
                        "Attendance sync: {Present} present, {Absent} absent, {OnVacation} vacation, {Changed} changed.",
                        result.Present, result.Absent, result.OnVacation, result.Changed);
                }
                else
                {
                    _logger.LogWarning("Attendance sync did not complete: {Error}", result.Error);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Attendance sync tick failed; will retry on the next interval.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
