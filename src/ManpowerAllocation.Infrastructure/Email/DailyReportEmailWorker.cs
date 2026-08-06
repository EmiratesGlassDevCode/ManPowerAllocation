using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Email;
using ManpowerAllocation.Application.Exports;
using ManpowerAllocation.Application.Snapshots;
using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Domain.Enums;
using ManpowerAllocation.Infrastructure.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ManpowerAllocation.Infrastructure.Email;

/// <summary>
/// Background worker that emails the captured day-shift report once per operational day, after the
/// configured local send time (which sits after the 10:00 day cut-off). It polls once a minute and,
/// when the send time has passed and today's report has not yet been emailed, finds today's captured
/// day snapshot, builds the report PDF and sends it to the configured recipients. Polling makes it
/// self-healing (a send missed while the app was down goes out on the next start) and the
/// per-operational-date guard keeps it idempotent. A failed run is logged and retried on the next
/// tick; it never throws or stops the worker.
/// </summary>
public sealed class DailyReportEmailWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AttendanceOptions _options;
    private readonly EmailSendStatus _status;
    private readonly ILogger<DailyReportEmailWorker> _logger;

    /// <summary>Initialises the worker.</summary>
    /// <param name="scopeFactory">Factory used to create a scope per poll for scoped services.</param>
    /// <param name="options">Attendance options, used only for the factory time zone.</param>
    /// <param name="status">Shared status record updated on every poll and send.</param>
    /// <param name="logger">Logger for non-sensitive diagnostics.</param>
    public DailyReportEmailWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<AttendanceOptions> options,
        EmailSendStatus status,
        ILogger<DailyReportEmailWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _status = status;
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
                _logger.LogError(ex, "Daily report email poll failed; will retry on the next interval.");
                _status.MarkFailure(DateTime.UtcNow, ex.Message);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var utcNow = clock.UtcNow;
        _status.MarkPoll(utcNow);

        var settings = await dbContext.EmailSettings
            .FirstOrDefaultAsync(e => e.Id == EmailSettings.SingletonId, cancellationToken);

        if (settings is null || !settings.Enabled)
        {
            return;
        }

        var localNow = ToLocal(utcNow);
        var today = localNow.Date;

        // Wait until the configured send time and only send once per operational day.
        if (localNow.TimeOfDay < settings.SendAtLocal)
        {
            return;
        }

        if (settings.LastSentOperationalDate.HasValue && settings.LastSentOperationalDate.Value.Date >= today)
        {
            return;
        }

        var history = scope.ServiceProvider.GetRequiredService<IAllocationHistoryService>();
        var snapshots = await history.ListAsync(today, today, cancellationToken);
        var daySnapshot = snapshots.FirstOrDefault(s => s.Shift == ShiftType.Day);

        if (daySnapshot is null)
        {
            // The day snapshot is captured at the 10:00 cut-off; if it is not present yet, retry
            // on the next tick rather than sending an empty report.
            _logger.LogInformation("Daily report email: no day snapshot captured yet for {Date:yyyy-MM-dd}; will retry.", today);
            return;
        }

        var attachments = new List<EmailAttachment>();
        if (settings.AttachPdf)
        {
            var exports = scope.ServiceProvider.GetRequiredService<IReportExportService>();
            var pdf = await exports.BuildDailyReportPdfAsync(daySnapshot.Id, cancellationToken);
            attachments.Add(new EmailAttachment(pdf.FileName, pdf.ContentType, pdf.Content));
        }

        var email = BuildEmail(daySnapshot, attachments);

        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        await sender.SendAsync(email, cancellationToken);

        // Record success only after the send returns; the per-date guard prevents duplicates.
        settings.LastSentOperationalDate = today;
        settings.UpdatedAtUtc = utcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        _status.MarkSuccess(utcNow, today);
        _logger.LogInformation("Emailed the day report for {Date:yyyy-MM-dd} to the configured recipients.", today);
    }

    private static OutgoingEmail BuildEmail(SnapshotSummaryDto snapshot, IReadOnlyList<EmailAttachment> attachments)
    {
        var date = snapshot.OperationalDate.ToString("dddd, dd MMMM yyyy");
        var subject = $"Manpower Allocation — Day report {snapshot.OperationalDate:yyyy-MM-dd}";

        var body =
            $"Daily manpower allocation report for {date} (day shift).\r\n\r\n" +
            $"On roll:      {snapshot.OnRoll}\r\n" +
            $"Present:      {snapshot.Present}\r\n" +
            $"Absent:       {snapshot.Absent}\r\n" +
            $"On vacation:  {snapshot.OnVacation}\r\n" +
            $"Outsourced present: {snapshot.SupplyPresent}\r\n" +
            $"Total present: {snapshot.TotalPresent} of {snapshot.Required} required (variance {snapshot.Variance:+#;-#;0}).\r\n" +
            $"Departments below requirement: {snapshot.ShortageDepartmentCount}.\r\n\r\n" +
            (attachments.Count > 0
                ? "The full report is attached as a PDF.\r\n"
                : string.Empty) +
            "\r\nThis is an automated message from the Manpower Allocation system.";

        return new OutgoingEmail(subject, body, attachments.Count > 0 ? attachments : null);
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
            _logger.LogWarning(ex, "Email time zone '{TimeZoneId}' could not be resolved; using UTC.", _options.TimeZoneId);
            return utcNow;
        }
    }
}
