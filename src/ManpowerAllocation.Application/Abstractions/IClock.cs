namespace ManpowerAllocation.Application.Abstractions;

/// <summary>
/// Abstraction over the system clock so time-dependent logic (audit timestamps,
/// the four-hour break-glass auto-disable window) is testable and consistent.
/// </summary>
public interface IClock
{
    /// <summary>The current UTC time.</summary>
    DateTime UtcNow { get; }
}
