namespace ManpowerAllocation.Infrastructure;

/// <summary>
/// Derives the daily-report generation and send times from the single configured day send time.
/// The night report is sent 12 hours after the day send time, and each report is generated
/// (its snapshot captured) a fixed lead — 15 minutes — before it is sent. Centralised here so the
/// snapshot worker and the email worker stay in lock-step.
/// </summary>
public static class ReportSchedule
{
    /// <summary>How long before the send time the report is generated (snapshot captured).</summary>
    public static readonly TimeSpan Lead = TimeSpan.FromMinutes(15);

    /// <summary>Gap between the day and night sends.</summary>
    private static readonly TimeSpan ShiftGap = TimeSpan.FromHours(12);

    /// <summary>Fallback day send time when none is configured — keeps the historic 10:00 / 22:00 cut-offs.</summary>
    private static readonly TimeSpan DefaultDaySend = new(10, 15, 0);

    /// <summary>The configured (or default) day send time.</summary>
    public static TimeSpan DaySend(TimeSpan? sendAtLocal) => Normalize(sendAtLocal ?? DefaultDaySend);

    /// <summary>The night send time = day send + 12 hours.</summary>
    public static TimeSpan NightSend(TimeSpan? sendAtLocal) => DaySend(sendAtLocal) + ShiftGap;

    /// <summary>The day report generation (snapshot) time = day send − lead.</summary>
    public static TimeSpan DayCutoff(TimeSpan? sendAtLocal) => Clamp(DaySend(sendAtLocal) - Lead);

    /// <summary>The night report generation (snapshot) time = night send − lead.</summary>
    public static TimeSpan NightCutoff(TimeSpan? sendAtLocal) => Clamp(NightSend(sendAtLocal) - Lead);

    private static TimeSpan Normalize(TimeSpan t) => t < TimeSpan.Zero ? TimeSpan.Zero : t;

    // Keep every derived instant within the same calendar day; the settings validator already
    // constrains the day send to before noon, but clamp defensively for any legacy value.
    private static TimeSpan Clamp(TimeSpan t) =>
        t < TimeSpan.Zero ? TimeSpan.Zero : (t >= TimeSpan.FromHours(24) ? new TimeSpan(23, 59, 0) : t);
}
