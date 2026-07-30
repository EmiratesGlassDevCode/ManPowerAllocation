namespace ManpowerAllocation.Application.Abstractions;

/// <summary>
/// Provides the current time in the factory's local time zone. Shift start times are stored as
/// local wall-clock values, so deciding which shift is currently live needs local time, not UTC.
/// </summary>
public interface IFactoryClock
{
    /// <summary>The current local (factory) time.</summary>
    DateTime LocalNow { get; }
}
