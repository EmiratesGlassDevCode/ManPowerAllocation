namespace ManpowerAllocation.Domain;

/// <summary>
/// Helpers enforcing the invariant that a department name is always stored in a
/// single canonical form (trimmed, upper-cased, internal whitespace collapsed).
/// The department name is the join key used across the domain, so normalising it
/// in one place prevents subtle duplicate-department bugs.
/// </summary>
public static class DepartmentName
{
    /// <summary>
    /// Returns the canonical form of a department name: trimmed, upper-cased using the
    /// invariant culture, with runs of internal whitespace collapsed to a single space.
    /// </summary>
    /// <param name="raw">The raw department name from user input or an imported file.</param>
    /// <returns>The canonical department name, or an empty string when the input is null or blank.</returns>
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        // Collapse internal whitespace so that, for example, "DOUBLE  EDGER" and
        // "DOUBLE EDGER" are treated as the same department rather than two.
        var collapsed = string.Join(' ', raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.ToUpperInvariant();
    }
}
