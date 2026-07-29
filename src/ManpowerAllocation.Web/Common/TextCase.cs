namespace ManpowerAllocation.Web.Common;

/// <summary>
/// Display-only text casing. Department names are stored upper-cased (they are the matching key),
/// and many imported employee names are upper-cased too; this presents them in readable title
/// case without altering the stored data. Short all-caps tokens (acronyms such as HSE, BRG, IGU,
/// QC) and tokens containing digits (e.g. "IGU 1") are preserved as-is.
/// </summary>
public static class TextCase
{
    /// <summary>Converts a stored name to display title case, preserving acronyms and codes.</summary>
    /// <param name="value">The stored value.</param>
    public static string Title(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < words.Length; i++)
        {
            var word = words[i];

            // Keep tokens that contain a digit (codes like "IGU1", "LINE2").
            if (word.Any(char.IsDigit))
            {
                continue;
            }

            // Keep short all-caps tokens as acronyms (HSE, BRG, QC, OS, IGU).
            var letters = word.Where(char.IsLetter).ToArray();
            var isShortAcronym = letters.Length is > 0 and <= 3 && letters.All(char.IsUpper);
            if (isShortAcronym)
            {
                continue;
            }

            words[i] = char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant();
        }

        return string.Join(' ', words);
    }

    /// <summary>Human-readable label for an attendance status (e.g. "On vacation" not "OnVacation").</summary>
    /// <param name="status">The attendance status.</param>
    public static string Status(ManpowerAllocation.Domain.Enums.AttendanceStatus status) => status switch
    {
        ManpowerAllocation.Domain.Enums.AttendanceStatus.Present => "Present",
        ManpowerAllocation.Domain.Enums.AttendanceStatus.Absent => "Absent",
        ManpowerAllocation.Domain.Enums.AttendanceStatus.OnVacation => "On vacation",
        _ => status.ToString()
    };
}
