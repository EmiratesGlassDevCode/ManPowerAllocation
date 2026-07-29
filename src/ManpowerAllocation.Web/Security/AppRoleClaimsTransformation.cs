using System.Security.Claims;
using ManpowerAllocation.Application.Roles;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Identity.Web;

namespace ManpowerAllocation.Web.Security;

/// <summary>
/// Enriches an authenticated Entra ID principal with the application role resolved from the
/// Role Assignment table (Entra object id → role). Break-glass principals already carry their
/// role claim from sign-in and are left untouched. This is how a role stored only in the
/// application database becomes an authorization claim without ever storing a credential.
/// </summary>
public sealed class AppRoleClaimsTransformation : IClaimsTransformation
{
    private readonly IRoleService _roleService;

    /// <summary>Initialises the transformation.</summary>
    /// <param name="roleService">Service used to look up the role for an Entra object id.</param>
    public AppRoleClaimsTransformation(IRoleService roleService)
    {
        _roleService = roleService;
    }

    /// <inheritdoc />
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // Already resolved (this runs on every authenticate call) or an emergency session: leave as-is.
        if (principal.HasClaim(c => c.Type == AppClaimTypes.AppRole))
        {
            return principal;
        }

        var objectId = principal.GetObjectId();
        if (string.IsNullOrEmpty(objectId))
        {
            return principal;
        }

        var role = await _roleService.GetRoleAsync(objectId);
        if (role is null)
        {
            // No assignment means no application access; no role claim is added.
            return principal;
        }

        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(AppClaimTypes.AppRole, role.Value.ToString()));
        principal.AddIdentity(identity);
        return principal;
    }
}
