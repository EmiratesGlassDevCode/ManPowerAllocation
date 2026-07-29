using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Application.Abstractions;

/// <summary>
/// Describes the principal making the current request. Implemented in the web layer
/// from the authenticated <c>HttpContext</c>. Application services use this to record
/// who performed an action and to enforce authorisation, never trusting the client.
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// The stable identity of the actor: the Entra object id for a normal session, or
    /// the break-glass account identifier during an emergency session.
    /// </summary>
    string UserId { get; }

    /// <summary>The actor's display name, when available.</summary>
    string? DisplayName { get; }

    /// <summary>True when the request is authenticated at all.</summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// True when the request is running inside an emergency break-glass session
    /// (i.e. authenticated by the local emergency account rather than Entra ID).
    /// Every audit entry written during the request inherits this flag.
    /// </summary>
    bool IsBreakGlassSession { get; }

    /// <summary>The application role resolved for the current actor.</summary>
    UserRole Role { get; }

    /// <summary>Returns true when the actor holds at least the supplied role.</summary>
    /// <param name="minimumRole">The lowest role that satisfies the check.</param>
    bool HasAtLeast(UserRole minimumRole);
}
