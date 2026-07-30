using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Snapshots;
using ManpowerAllocation.Domain.Enums;
using ManpowerAllocation.Infrastructure.Attendance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ManpowerAllocation.Infrastructure.Snapshots;

/// <summary>
/// Background worker that captures the daily staffing report at the shift cut-offs — 10:00 for the
/// day shift and 22:00 for the night shift, in the factory time zone. It polls once a minute and,
/// once a cut-off has passed for the current local date, captures that shift if it has not been
/// captured yet. Polling (rather than sleeping until the exact instant) makes it self-healing: a
/// cut-off missed because the app was down is captured as soon as it starts, and the existence
/// check keeps re-captures idempotent.
/// </summary>
public sealed class AllocationSnapshotWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan DayCutoff = new(10, 0, 0);
    private static readonly TimeSpan NightCutoff = new(22, 0, 0);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AttendanceOptions _options;
    private readonly ILogger<AllocationSnapshotWorker> _logger;

    /// <summary>Initialises the worker.</summary>
    /// <param name="scopeFactory">Factory used to create a scope per poll for scoped services.</param>
    /// <param name="options">Attendance options, used only for the factory time zone.</param>
    /// <param name="logger">Logger for non-sensitive diagnostics.</param>
    public AllocationSnapshotWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<AttendanceOptions> options,
        ILogger<AllocationSnapshotWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
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
                await PollAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A transient failure must not stop the worker; log and retry on the next tick.
                _logger.LogError(ex, "Daily snapshot capture failed; will retry on the next interval.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var snapshots = scope.ServiceProvider.GetRequiredService<IAllocationSnapshotService>();

        var localNow = ToLocal(clock.UtcNow);
        var today = localNow.Date;

        if (localNow.TimeOfDay >= DayCutoff)
        {
            await CaptureIfMissing(snapshots, today, ShiftType.Day, "auto-10:00", cancellationToken);
        }

        if (localNow.TimeOfDay >= NightCutoff)
        {
            await CaptureIfMissing(snapshots, today, ShiftType.Night, "auto-22:00", cancellationToken);
        }
    }

    private async Task CaptureIfMissing(IAllocationSnapshotService snapshots, DateTime date, ShiftType shift, string source, CancellationToken cancellationToken)
    {
        if (await snapshots.ExistsAsync(date, shift, cancellationToken))
        {
            return;
        }

        await snapshots.CaptureAsync(shift, date, source, cancellationToken);
        _logger.LogInformation("Captured {Shift} shift snapshot for {Date:yyyy-MM-dd} ({Source}).", shift, date, source);
    }

    /// <summary>Converts a UTC instant to the factory-local time, falling back to UTC on a bad time-zone id.</summary>
    private DateTime ToLocal(DateTime utcNow)
    {
        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            _logger.LogWarning(ex, "Snapshot time zone '{TimeZoneId}' could not be resolved; using UTC.", _options.TimeZoneId);
            return utcNow;
        }
    }
}
