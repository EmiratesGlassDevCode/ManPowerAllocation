namespace ManpowerAllocation.Application.Email;

/// <summary>Parses recipient lists that may be separated by comma, semicolon, whitespace or new lines.</summary>
public static class EmailRecipients
{
    private static readonly char[] Separators = { ',', ';', '\n', '\r', '\t', ' ' };

    /// <summary>Splits a raw recipient string into distinct, trimmed, non-empty addresses.</summary>
    /// <param name="raw">The raw recipient text (e.g. "a@x.com, b@y.com; c@z.com").</param>
    public static IReadOnlyList<string> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<string>();
        }

        return raw
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
