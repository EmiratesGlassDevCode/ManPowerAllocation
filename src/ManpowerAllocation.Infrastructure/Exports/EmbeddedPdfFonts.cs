using PdfSharp.Drawing;
using PdfSharp.Fonts;

namespace ManpowerAllocation.Infrastructure.Exports;

/// <summary>
/// Shared font infrastructure for the branded PDF reports. Serves Liberation Sans from fonts
/// embedded in this assembly so PDF generation never depends on which fonts happen to be
/// installed on the server, and force-installs the resolver (rather than <c>??=</c>) so
/// PDFsharp's lazily-created platform resolver — which cannot resolve "Liberation Sans" on a
/// Windows server — never wins first.
/// </summary>
internal static class EmbeddedPdfFonts
{
    /// <summary>The single font family every report draws with.</summary>
    public const string Family = "Liberation Sans";

    static EmbeddedPdfFonts()
    {
        // The static constructor runs once per process, before any XFont is created, so the
        // setter is still in its assignable window; the catch only guards a redundant re-set.
        try { GlobalFontSettings.FontResolver = new Resolver(); }
        catch { /* a resolver is already installed for this process — it is ours */ }
    }

    /// <summary>Touching any member runs the static constructor, guaranteeing the resolver is set.</summary>
    public static void Ensure()
    {
    }

    /// <summary>Builds a report font of the shared family.</summary>
    public static XFont Font(double size, XFontStyleEx style) => new(Family, size, style);

    /// <summary>Reads an embedded asset by matching the resource-name suffix (prefix-agnostic).</summary>
    public static byte[] ReadEmbedded(string fileName)
    {
        var assembly = typeof(EmbeddedPdfFonts).Assembly;
        var name = Array.Find(assembly.GetManifestResourceNames(),
            n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (name is null)
        {
            return Array.Empty<byte>();
        }

        using var stream = assembly.GetManifestResourceStream(name);
        if (stream is null)
        {
            return Array.Empty<byte>();
        }

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>Resolves the report faces to the embedded Liberation Sans TTFs.</summary>
    private sealed class Resolver : IFontResolver
    {
        public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
            => new FontResolverInfo(bold ? "libsans-bold" : "libsans-regular");

        public byte[]? GetFont(string faceName)
        {
            var file = faceName == "libsans-bold" ? "LiberationSans-Bold.ttf" : "LiberationSans-Regular.ttf";
            var bytes = ReadEmbedded(file);
            if (bytes.Length == 0)
            {
                // Make a missing embedded font an unambiguous, actionable error instead of a
                // cryptic PDFsharp parse failure. Lists what actually shipped in the assembly.
                var available = string.Join(", ", typeof(EmbeddedPdfFonts).Assembly.GetManifestResourceNames());
                throw new InvalidOperationException(
                    $"Embedded report font '{file}' was not found in the assembly. Embedded resources present: [{available}].");
            }

            return bytes;
        }
    }
}
