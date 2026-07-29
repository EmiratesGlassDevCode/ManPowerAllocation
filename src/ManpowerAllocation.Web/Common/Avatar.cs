namespace ManpowerAllocation.Web.Common;

/// <summary>Helpers for the small circular/rounded avatar tiles (initials + a stable colour).</summary>
public static class Avatar
{
    private static readonly string[] Palette =
    {
        "#0f6cbd", "#038387", "#5c2e91", "#c19c00", "#0e700e", "#b10e1c", "#8764b8", "#005b70"
    };

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

    /// <summary>A stable palette colour derived from the key, so the same name always gets the same tile.</summary>
    /// <param name="key">The value to hash (name or department).</param>
    public static string Color(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return Palette[0];
        }

        var sum = 0;
        foreach (var c in key)
        {
            sum = (sum + c) % Palette.Length;
        }

        return Palette[sum];
    }
}
