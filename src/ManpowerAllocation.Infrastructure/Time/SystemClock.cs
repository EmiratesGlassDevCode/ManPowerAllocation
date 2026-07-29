using ManpowerAllocation.Application.Abstractions;

namespace ManpowerAllocation.Infrastructure.Time;

/// <summary>Default <see cref="IClock"/> backed by the system UTC clock.</summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTime UtcNow => DateTime.UtcNow;
}
