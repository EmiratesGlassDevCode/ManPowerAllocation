using System.Security.Claims;
using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Domain.Enums;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Identity.Web;

namespace ManpowerAllocation.Web.Security;

/// <summary>
/// Resolves the current principal for the request or Blazor circuit. This is the single
/// server-side source of truth for who is acting and whether the session is an emergency
/// break-glass session; nothing here is taken from client-controlled input.
/// <para>
/// It reads the principal from <c>HttpContext</c> when one is available (Minimal API requests
/// and the initial server render) and falls back to the <see cref="AuthenticationStateProvider"/>
/// inside an interactive Blazor circuit, where <c>HttpContext</c> is intentionally null.
/// </para>
/// </summary>
public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    /// <summary>Initialises the accessor.</summary>
    /// <param name="httpContextAccessor">Accessor for the current HTTP context (may be absent in a circuit).</param>
    /// <param name="authenticationStateProvider">Provider used to resolve the principal inside a Blazor circuit.</param>
    public CurrentUser(IHttpContextAccessor httpContextAccessor, AuthenticationStateProvider authenticationStateProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _authenticationStateProvider = authenticationStateProvider;
    }

    private ClaimsPrincipal? Principal
    {
        get
        {
            var httpUser = _httpContextAccessor.HttpContext?.User;
            if (httpUser?.Identity?.IsAuthenticated == true)
            {
                return httpUser;
            }

            // Interactive Blazor circuit: resolve from the auth state provider. The provider's
            // task is already completed on the server, so this does not block.
            try
            {
                return _authenticationStateProvider.GetAuthenticationStateAsync().GetAwaiter().GetResult().User;
            }
            catch (InvalidOperationException)
            {
                return httpUser;
            }
        }
    }

    /// <inheritdoc />
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    /// <inheritdoc />
    public bool IsBreakGlassSession =>
        string.Equals(Principal?.FindFirstValue(AppClaimTypes.BreakGlass), "true", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public string UserId
    {
        get
        {
            var principal = Principal;
            if (principal is null)
            {
                return string.Empty;
            }

            if (IsBreakGlassSession)
            {
                return principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "breakglass";
            }

            // For Entra ID users the stable identifier is the object id ("oid") claim.
            return principal.GetObjectId()
                   ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? string.Empty;
        }
    }

    /// <inheritdoc />
    public string? DisplayName =>
        Principal?.FindFirstValue("name")
        ?? Principal?.FindFirstValue(ClaimTypes.Name);

    /// <inheritdoc />
    public UserRole Role =>
        Enum.TryParse<UserRole>(Principal?.FindFirstValue(AppClaimTypes.AppRole), out var role)
            ? role
            : UserRole.Viewer;

    /// <inheritdoc />
    public bool HasAtLeast(UserRole minimumRole)
    {
        // Absence of a valid application-role claim means no access, regardless of authentication.
        if (!Enum.TryParse<UserRole>(Principal?.FindFirstValue(AppClaimTypes.AppRole), out var role))
        {
            return false;
        }

        return RoleRank.RankOf(role) >= RoleRank.RankOf(minimumRole);
    }
}
