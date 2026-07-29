namespace ManpowerAllocation.Web.Common;

/// <summary>Helpers for the small circular/rounded avatar tiles (initials).</summary>
public static class Avatar
{
    /// <summary>Up-to-two-letter initials from a name.</summary>
    /// <param name="name">The person or department name.</param>
    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }

        var parts = name.Split(new[] { ' ', '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "?";
        }

        var first = char.ToUpperInvariant(parts[0][0]);
        return parts.Length == 1 ? first.ToString() : $"{first}{char.ToUpperInvariant(parts[1][0])}";
    }
}
