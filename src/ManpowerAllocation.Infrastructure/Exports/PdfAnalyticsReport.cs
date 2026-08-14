using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace ManpowerAllocation.Infrastructure.Exports;

/// <summary>
/// Generic branded PDF renderer for the analytics reports: an Emirates Glass header, an optional KPI
/// tile row, and one or more titled tables that paginate. Kept separate from the daily-report layout
/// so both can evolve independently. Pure PDFsharp; fonts served from the assembly.
/// </summary>
internal static class PdfAnalyticsReport
{
    private static readonly XColor Navy = XColor.FromArgb(0x12, 0x44, 0x6B);
    private static readonly XColor Brand = XColor.FromArgb(0x0F, 0x6C, 0xBD);
    private static readonly XColor Gold = XColor.FromArgb(0xB8, 0x93, 0x5A);
    private static readonly XColor Ink = XColor.FromArgb(0x22, 0x2A, 0x33);
    private static readonly XColor Muted = XColor.FromArgb(0x6B, 0x75, 0x80);
    private static readonly XColor Line = XColor.FromArgb(0xE2, 0xE6, 0xEA);
    private static readonly XColor TileBg = XColor.FromArgb(0xF5, 0xF8, 0xFC);
    private static readonly XColor White = XColors.White;

    private const double Margin = 36;
    private const double BandH = 88;

    /// <summary>A KPI tile.</summary>
    internal sealed record Kpi(string Label, string Value);

    /// <summary>A titled table section.</summary>
    internal sealed record Section(string Title, string[] Headers, IReadOnlyList<string[]> Rows);

    public static byte[] Render(byte[] logo, string title, string subtitle, IReadOnlyList<Kpi> kpis, IReadOnlyList<Section> sections)
    {
        EmbeddedPdfFonts.Ensure();

        var doc = new PdfDocument();
        doc.Info.Title = title;
        doc.Info.Author = "Emirates Glass — Manpower Allocation";

        var page = doc.AddPage();
        page.Size = PageSize.A4;
        var gfx = XGraphics.FromPdfPage(page);
        double w = page.Width.Point;
        double h = page.Height.Point;
        double contentW = w - 2 * Margin;

        DrawHeader(gfx, w, logo, title, subtitle);
        double y = BandH + 18;

        // KPI tiles.
        if (kpis.Count > 0)
        {
            var gap = 8.0;
            var tileW = (contentW - gap * (kpis.Count - 1)) / kpis.Count;
            var tileH = 54.0;
            for (var i = 0; i < kpis.Count; i++)
            {
                var x = Margin + i * (tileW + gap);
                gfx.DrawRoundedRectangle(new XPen(Line, 0.75), new XSolidBrush(TileBg), x, y, tileW, tileH, 8, 8);
                gfx.DrawString(kpis[i].Value, Bold(17), new XSolidBrush(Navy), new XRect(x, y + 8, tileW, 22), XStringFormats.Center);
                gfx.DrawString(kpis[i].Label, Bold(7.5), new XSolidBrush(Muted), new XRect(x, y + 34, tileW, 12), XStringFormats.Center);
            }
            y += tileH + 20;
        }

        foreach (var section in sections)
        {
            y = EnsureSpace(doc, ref page, ref gfx, y, h, 60);

            gfx.DrawString(section.Title.ToUpperInvariant(), Bold(11), new XSolidBrush(Navy),
                new XRect(Margin, y, contentW, 14), XStringFormats.TopLeft);
            y += 20;

            var cols = ColumnWidths(section.Headers.Length, contentW);

            void Header(ref double ty)
            {
                gfx.DrawRoundedRectangle(new XSolidBrush(Navy), Margin, ty, contentW, 20, 4, 4);
                var hx = Margin;
                for (var c = 0; c < section.Headers.Length; c++)
                {
                    gfx.DrawString(section.Headers[c], Bold(8), new XSolidBrush(White),
                        new XRect(hx + 6, ty, cols[c] - 8, 20), XStringFormats.CenterLeft);
                    hx += cols[c];
                }
                ty += 20;
            }

            Header(ref y);

            if (section.Rows.Count == 0)
            {
                gfx.DrawString("No data.", Regular(9), new XSolidBrush(Muted), new XRect(Margin, y, contentW, 14), XStringFormats.TopLeft);
                y += 18;
                continue;
            }

            var rowH = 16.0;
            var alt = false;
            foreach (var row in section.Rows)
            {
                if (y > h - Margin - 26)
                {
                    gfx.Dispose();
                    page = doc.AddPage();
                    page.Size = PageSize.A4;
                    gfx = XGraphics.FromPdfPage(page);
                    y = Margin;
                    gfx.DrawString(section.Title.ToUpperInvariant() + " (cont.)", Bold(11), new XSolidBrush(Navy),
                        new XRect(Margin, y, contentW, 14), XStringFormats.TopLeft);
                    y += 20;
                    Header(ref y);
                }

                if (alt)
                {
                    gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(0xFA, 0xFB, 0xFD)), Margin, y, contentW, rowH);
                }
                alt = !alt;

                var rx = Margin;
                for (var c = 0; c < row.Length && c < cols.Length; c++)
                {
                    var font = c == 0 ? Bold(8.5) : Regular(8.5);
                    gfx.DrawString(Clip(gfx, row[c] ?? string.Empty, cols[c] - 8, font), font, new XSolidBrush(c == 0 ? Ink : Muted),
                        new XRect(rx + 6, y, cols[c] - 8, rowH), XStringFormats.CenterLeft);
                    rx += cols[c];
                }
                gfx.DrawLine(new XPen(Line, 0.5), Margin, y + rowH, Margin + contentW, y + rowH);
                y += rowH;
            }

            y += 14;
        }

        gfx.Dispose();
        DrawFooters(doc);

        using var ms = new MemoryStream();
        doc.Save(ms);
        return ms.ToArray();
    }

    /// <summary>Ensures at least <paramref name="needed"/> vertical space remains, adding a page if not.</summary>
    private static double EnsureSpace(PdfDocument doc, ref PdfPage page, ref XGraphics gfx, double y, double h, double needed)
    {
        if (y <= h - Margin - needed)
        {
            return y;
        }

        gfx.Dispose();
        page = doc.AddPage();
        page.Size = PageSize.A4;
        gfx = XGraphics.FromPdfPage(page);
        return Margin;
    }

    /// <summary>Distributes the content width across columns: the first is wider, the rest even.</summary>
    private static double[] ColumnWidths(int count, double contentW)
    {
        var widths = new double[count];
        if (count == 1)
        {
            widths[0] = contentW;
            return widths;
        }

        var first = contentW * 0.26;
        var rest = (contentW - first) / (count - 1);
        widths[0] = first;
        for (var i = 1; i < count; i++)
        {
            widths[i] = rest;
        }
        return widths;
    }

    private static void DrawHeader(XGraphics gfx, double w, byte[] logo, string title, string subtitle)
    {
        var band = new XRect(0, 0, w, BandH);
        gfx.DrawRectangle(new XLinearGradientBrush(band, Navy, Brand, XLinearGradientMode.Horizontal), band);
        gfx.DrawRectangle(new XSolidBrush(Gold), 0, BandH - 4, w, 4);

        var titleX = Margin;
        try
        {
            if (logo.Length > 0)
            {
                using var ls = new MemoryStream(logo);
                using var img = XImage.FromStream(ls);
                var logoH = 44.0;
                var lw = logoH * img.PixelWidth / Math.Max(1, img.PixelHeight);
                gfx.DrawRoundedRectangle(new XSolidBrush(White), Margin, 22, lw + 20, logoH + 10, 8, 8);
                gfx.DrawImage(img, Margin + 10, 27, lw, logoH);
                titleX = Margin + lw + 34;
            }
        }
        catch
        {
            titleX = Margin;
        }

        gfx.DrawString(title, Bold(17), new XSolidBrush(White), new XRect(titleX, 24, w - titleX - Margin, 22), XStringFormats.TopLeft);
        gfx.DrawString(subtitle, Regular(9.5), new XSolidBrush(XColor.FromArgb(0xD8, 0xE6, 0xF4)),
            new XRect(titleX, 50, w - titleX - Margin, 14), XStringFormats.TopLeft);
    }

    private static void DrawFooters(PdfDocument doc)
    {
        for (var p = 0; p < doc.PageCount; p++)
        {
            var fp = doc.Pages[p];
            using var fg = XGraphics.FromPdfPage(fp);
            var fw = fp.Width.Point;
            var fh = fp.Height.Point;
            fg.DrawLine(new XPen(Line, 0.75), Margin, fh - 30, fw - Margin, fh - 30);
            fg.DrawString($"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC · Emirates Glass Manpower Allocation",
                Regular(7.5), new XSolidBrush(Muted), new XRect(Margin, fh - 26, fw - 2 * Margin, 12), XStringFormats.CenterLeft);
            fg.DrawString($"Page {p + 1} of {doc.PageCount}", Regular(7.5), new XSolidBrush(Muted),
                new XRect(Margin, fh - 26, fw - 2 * Margin, 12), XStringFormats.CenterRight);
        }
    }

    private static string Clip(XGraphics gfx, string text, double maxWidth, XFont font)
    {
        text ??= string.Empty;
        if (maxWidth <= 0 || gfx.MeasureString(text, font).Width <= maxWidth)
        {
            return text;
        }

        var s = text;
        while (s.Length > 1 && gfx.MeasureString(s + "…", font).Width > maxWidth)
        {
            s = s[..^1];
        }
        return s + "…";
    }

    private static XFont Bold(double size) => EmbeddedPdfFonts.Font(size, XFontStyleEx.Bold);

    private static XFont Regular(double size) => EmbeddedPdfFonts.Font(size, XFontStyleEx.Regular);
}
