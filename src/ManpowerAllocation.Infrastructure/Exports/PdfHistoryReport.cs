using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace ManpowerAllocation.Infrastructure.Exports;

/// <summary>
/// Renders the archived daily-report history as a branded, laid-out PDF (Emirates Glass logo,
/// summary tiles, a fill-vs-required bar chart and a paginated data table) using PDFsharp (MIT).
/// </summary>
internal static class PdfHistoryReport
{
    // Emirates Glass palette.
    private static readonly XColor Navy = XColor.FromArgb(0x0F, 0x54, 0x8C);
    private static readonly XColor Brand = XColor.FromArgb(0x0F, 0x6C, 0xBD);
    private static readonly XColor Ink = XColor.FromArgb(0x24, 0x24, 0x24);
    private static readonly XColor Muted = XColor.FromArgb(0x61, 0x61, 0x61);
    private static readonly XColor Line = XColor.FromArgb(0xE0, 0xE0, 0xE0);
    private static readonly XColor Track = XColor.FromArgb(0xED, 0xED, 0xED);
    private static readonly XColor Ok = XColor.FromArgb(0x0E, 0x70, 0x0E);
    private static readonly XColor Danger = XColor.FromArgb(0xB1, 0x0E, 0x1C);
    private static readonly XColor TileBg = XColor.FromArgb(0xF5, 0xF8, 0xFC);

    private const double Margin = 40;

    /// <summary>A single captured snapshot summarised for the report.</summary>
    internal sealed record Row(DateTime Date, string Shift, int Present, int Required, int Variance, int ShortDepts);

    private const string FontFamily = "Liberation Sans";

    static PdfHistoryReport()
    {
        // Serve the report's fonts from fonts embedded in this assembly, so PDF generation never
        // depends on which fonts happen to be installed on the server.
        //
        // Force-set (NOT ??=): PDFsharp 6.x may lazily install a platform font resolver, and on
        // Windows Server that resolver cannot resolve "Liberation Sans" and throws — the exact
        // 500 seen in the field. Assigning unconditionally guarantees our resolver wins. The
        // static constructor runs once per process, before any XFont is created, so the setter
        // is still in its assignable window; the catch only guards a redundant re-assignment.
        try { GlobalFontSettings.FontResolver = new EmbeddedFontResolver(); }
        catch { /* a resolver is already installed for this process — it is ours */ }
    }

    /// <summary>Resolves this report's faces to the embedded Liberation Sans TTFs.</summary>
    private sealed class EmbeddedFontResolver : IFontResolver
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
                var available = string.Join(", ", typeof(PdfHistoryReport).Assembly.GetManifestResourceNames());
                throw new InvalidOperationException(
                    $"Embedded report font '{file}' was not found in the assembly. Embedded resources present: [{available}].");
            }

            return bytes;
        }
    }

    /// <summary>Reads an embedded asset by matching the resource name suffix (prefix-agnostic).</summary>
    internal static byte[] ReadEmbedded(string fileName)
    {
        var assembly = typeof(PdfHistoryReport).Assembly;
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

    public static byte[] Render(byte[] logo, DateTime from, DateTime to, IReadOnlyList<Row> rows)
    {
        var doc = new PdfDocument();
        doc.Info.Title = "Daily Report History";
        doc.Info.Author = "Emirates Glass — Manpower Allocation";

        var titleFont = Font(20, XFontStyleEx.Bold);
        var h2Font = Font(12, XFontStyleEx.Bold);
        var labelFont = Font(8, XFontStyleEx.Bold);
        var valueFont = Font(20, XFontStyleEx.Bold);
        var bodyFont = Font(9, XFontStyleEx.Regular);
        var bodyBold = Font(9, XFontStyleEx.Bold);
        var footFont = Font(8, XFontStyleEx.Regular);

        var page = doc.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        var gfx = XGraphics.FromPdfPage(page);
        double w = page.Width.Point;
        double contentW = w - 2 * Margin;
        double y = Margin;

        // ── Header: logo + title + brand rule ────────────────────────────────
        try
        {
            using var logoStream = new MemoryStream(logo);
            using var img = XImage.FromStream(logoStream);
            var logoH = 34.0;
            var logoW = logoH * img.PixelWidth / Math.Max(1, img.PixelHeight);
            gfx.DrawImage(img, Margin, y, logoW, logoH);
        }
        catch { /* logo optional */ }

        gfx.DrawString("DAILY REPORT HISTORY", titleFont, new XSolidBrush(Navy),
            new XRect(Margin, y, contentW, 34), XStringFormats.CenterRight);
        y += 40;
        gfx.DrawString($"{from:dd MMM yyyy}  –  {to:dd MMM yyyy}", bodyFont, new XSolidBrush(Muted),
            new XRect(Margin, y, contentW, 14), XStringFormats.CenterRight);
        gfx.DrawString("Emirates Glass · Manpower Allocation", bodyFont, new XSolidBrush(Muted),
            new XRect(Margin, y, contentW, 14), XStringFormats.CenterLeft);
        y += 20;
        gfx.DrawRectangle(new XSolidBrush(Brand), Margin, y, contentW, 3);
        y += 18;

        // ── KPI tiles ────────────────────────────────────────────────────────
        var required = rows.Sum(r => r.Required);
        var present = rows.Sum(r => r.Present);
        var fill = required > 0 ? (int)Math.Round(100.0 * present / required) : 0;
        var incidents = rows.Sum(r => r.ShortDepts);
        var under = rows.Count(r => r.Variance < 0);

        var tiles = new (string Label, string Value, XColor Color)[]
        {
            ("REPORTS CAPTURED", rows.Count.ToString(), Ink),
            ("AVG FILL VS REQUIRED", fill + "%", fill >= 100 ? Ok : Ink),
            ("SHORTAGE INCIDENTS", incidents.ToString(), incidents > 0 ? Danger : Ink),
            ("SHIFTS UNDER TARGET", under.ToString(), under > 0 ? Danger : Ink),
        };
        var gap = 10.0;
        var tileW = (contentW - gap * (tiles.Length - 1)) / tiles.Length;
        var tileH = 56.0;
        for (var i = 0; i < tiles.Length; i++)
        {
            var tx = Margin + i * (tileW + gap);
            gfx.DrawRectangle(new XSolidBrush(TileBg), tx, y, tileW, tileH);
            gfx.DrawRectangle(new XSolidBrush(Brand), tx, y, tileW, 2);
            gfx.DrawString(tiles[i].Value, valueFont, new XSolidBrush(tiles[i].Color),
                new XRect(tx + 10, y + 10, tileW - 20, 26), XStringFormats.TopLeft);
            gfx.DrawString(tiles[i].Label, labelFont, new XSolidBrush(Muted),
                new XRect(tx + 10, y + 36, tileW - 20, 12), XStringFormats.TopLeft);
        }
        y += tileH + 22;

        // ── Bar chart: fill vs required per captured report (most recent first) ─
        gfx.DrawString("FILL VS REQUIRED", h2Font, new XSolidBrush(Navy),
            new XRect(Margin, y, contentW, 14), XStringFormats.TopLeft);
        y += 20;

        var chartRows = rows.OrderByDescending(r => r.Date).ThenBy(r => r.Shift).Take(14).ToList();
        var labelW = 110.0;
        var barMax = contentW - labelW - 60;
        var rowH = 16.0;
        foreach (var r in chartRows)
        {
            var pct = r.Required > 0 ? Math.Min(1.0, (double)r.Present / r.Required) : 0;
            var barColor = r.Present >= r.Required ? Ok : Danger;
            gfx.DrawString($"{r.Date:dd MMM} {r.Shift}", bodyFont, new XSolidBrush(Muted),
                new XRect(Margin, y, labelW - 6, rowH), XStringFormats.CenterLeft);
            gfx.DrawRectangle(new XSolidBrush(Track), Margin + labelW, y + 3, barMax, rowH - 6);
            gfx.DrawRectangle(new XSolidBrush(barColor), Margin + labelW, y + 3, Math.Max(1, barMax * pct), rowH - 6);
            gfx.DrawString($"{r.Present}/{r.Required}", bodyFont, new XSolidBrush(Ink),
                new XRect(Margin + labelW + barMax + 6, y, 54, rowH), XStringFormats.CenterLeft);
            y += rowH;
        }
        y += 14;

        // ── Data table (paginates) ───────────────────────────────────────────
        double[] cols = { 70, 60, 70, 70, 70, 0 }; // last col fills remainder
        cols[5] = contentW - cols.Take(5).Sum();
        string[] heads = { "Date", "Shift", "Present", "Required", "Variance", "Short depts" };

        void DrawTableHeader(ref double ty)
        {
            gfx.DrawRectangle(new XSolidBrush(TileBg), Margin, ty, contentW, 18);
            var cx = Margin;
            for (var c = 0; c < heads.Length; c++)
            {
                gfx.DrawString(heads[c], bodyBold, new XSolidBrush(Muted),
                    new XRect(cx + 6, ty, cols[c] - 8, 18), XStringFormats.CenterLeft);
                cx += cols[c];
            }
            ty += 18;
        }

        gfx.DrawString("CAPTURED REPORTS", h2Font, new XSolidBrush(Navy),
            new XRect(Margin, y, contentW, 14), XStringFormats.TopLeft);
        y += 18;
        DrawTableHeader(ref y);

        var alt = false;
        foreach (var r in rows.OrderByDescending(r => r.Date).ThenBy(r => r.Shift))
        {
            if (y > page.Height.Point - Margin - 24)
            {
                gfx.Dispose();
                page = doc.AddPage();
                page.Size = PdfSharp.PageSize.A4;
                gfx = XGraphics.FromPdfPage(page);
                y = Margin;
                DrawTableHeader(ref y);
            }

            if (alt)
            {
                gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(0xFA, 0xFA, 0xFA)), Margin, y, contentW, 16);
            }
            alt = !alt;

            var cells = new (string Text, XColor Color)[]
            {
                ($"{r.Date:dd MMM yyyy}", Ink),
                (r.Shift, Ink),
                (r.Present.ToString(), Ink),
                (r.Required.ToString(), Ink),
                (Signed(r.Variance), r.Variance < 0 ? Danger : Ok),
                (r.ShortDepts.ToString(), r.ShortDepts > 0 ? Danger : Ink),
            };
            var cx = Margin;
            for (var c = 0; c < cells.Length; c++)
            {
                gfx.DrawString(cells[c].Text, bodyFont, new XSolidBrush(cells[c].Color),
                    new XRect(cx + 6, y, cols[c] - 8, 16), XStringFormats.CenterLeft);
                cx += cols[c];
            }
            gfx.DrawLine(new XPen(Line, 0.5), Margin, y + 16, Margin + contentW, y + 16);
            y += 16;
        }

        // Release the live XGraphics for the current (last) page before the footer pass —
        // PDFsharp permits only one XGraphics per page, and the footer loop opens a fresh one
        // for every page, including this one.
        gfx.Dispose();

        // ── Footer on every page ─────────────────────────────────────────────
        for (var p = 0; p < doc.PageCount; p++)
        {
            var fp = doc.Pages[p];
            using var fg = XGraphics.FromPdfPage(fp);
            fg.DrawString($"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC", footFont, new XSolidBrush(Muted),
                new XRect(Margin, fp.Height.Point - 28, fp.Width.Point - 2 * Margin, 12), XStringFormats.CenterLeft);
            fg.DrawString($"Page {p + 1} of {doc.PageCount}", footFont, new XSolidBrush(Muted),
                new XRect(Margin, fp.Height.Point - 28, fp.Width.Point - 2 * Margin, 12), XStringFormats.CenterRight);
        }

        using var ms = new MemoryStream();
        doc.Save(ms);
        return ms.ToArray();
    }

    private static XFont Font(double size, XFontStyleEx style) => new(FontFamily, size, style);

    private static string Signed(int value) => value > 0 ? $"+{value}" : value.ToString();
}
