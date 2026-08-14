using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace ManpowerAllocation.Infrastructure.Exports;

/// <summary>
/// Renders one captured daily report as a branded, dashboard-style management PDF: an Emirates
/// Glass header banner, KPI tiles, a workforce-composition donut, a per-division fill chart and a
/// colour-coded department table. Pure PDFsharp (MIT); fonts are served from the assembly.
/// </summary>
internal static class PdfDailyReport
{
    // Emirates Glass palette.
    private static readonly XColor Navy = XColor.FromArgb(0x12, 0x44, 0x6B);
    private static readonly XColor Brand = XColor.FromArgb(0x0F, 0x6C, 0xBD);
    private static readonly XColor Gold = XColor.FromArgb(0xB8, 0x93, 0x5A);
    private static readonly XColor Ink = XColor.FromArgb(0x22, 0x2A, 0x33);
    private static readonly XColor Muted = XColor.FromArgb(0x6B, 0x75, 0x80);
    private static readonly XColor Line = XColor.FromArgb(0xE2, 0xE6, 0xEA);
    private static readonly XColor Track = XColor.FromArgb(0xED, 0xF1, 0xF5);
    private static readonly XColor Ok = XColor.FromArgb(0x1E, 0x7D, 0x32);
    private static readonly XColor Amber = XColor.FromArgb(0xC7, 0x77, 0x00);
    private static readonly XColor Danger = XColor.FromArgb(0xC0, 0x32, 0x2B);
    private static readonly XColor Teal = XColor.FromArgb(0x0F, 0x7B, 0x8A);
    private static readonly XColor TileBg = XColor.FromArgb(0xF5, 0xF8, 0xFC);
    private static readonly XColor White = XColors.White;

    private const double Margin = 36;
    private const double BandH = 104;

    /// <summary>A department line on the report.</summary>
    internal sealed record DeptRow(
        string Division, string Department, int Required, int Present,
        int Absent, int OnVacation, int Supply, int Variance, string Status);

    /// <summary>A division rollup for the fill chart.</summary>
    internal sealed record DivisionRow(string Division, int Required, int Present);

    /// <summary>An absent (or on-vacation) employee and the reason recorded for the report date.</summary>
    internal sealed record AbsenteeRow(
        string Name, string? Badge, string Division, string Department,
        string StatusLabel, string ReasonKind, string ReasonCategory, string Detail);

    /// <summary>Everything the report needs for one captured (date, shift) snapshot.</summary>
    internal sealed record Model(
        DateTime OperationalDate,
        string Shift,
        DateTime CapturedAtUtc,
        int Required,
        int OnRoll,
        int Present,
        int Absent,
        int OnVacation,
        int Supply,
        int TotalPresent,
        int Variance,
        int ShortageDepartments,
        IReadOnlyList<DivisionRow> Divisions,
        IReadOnlyList<DeptRow> Departments,
        IReadOnlyList<AbsenteeRow> Absentees);

    public static byte[] Render(byte[] logo, Model m)
    {
        EmbeddedPdfFonts.Ensure();

        var doc = new PdfDocument();
        doc.Info.Title = $"Daily Manpower Report — {m.OperationalDate:dd MMM yyyy} {m.Shift}";
        doc.Info.Author = "Emirates Glass — Manpower Allocation";

        var page = doc.AddPage();
        page.Size = PageSize.A4;
        var gfx = XGraphics.FromPdfPage(page);
        double w = page.Width.Point;
        double h = page.Height.Point;
        double contentW = w - 2 * Margin;

        DrawHeaderBanner(gfx, w, logo, m);
        double y = BandH + 20;

        // ── KPI tiles ────────────────────────────────────────────────────────
        var fill = m.Required > 0 ? (int)Math.Round(100.0 * m.TotalPresent / m.Required) : 0;
        var fillColor = fill >= 100 ? Ok : fill >= 85 ? Amber : Danger;
        var varColor = m.Variance < 0 ? Danger : Ok;

        var tiles = new (string Label, string Value, XColor Accent)[]
        {
            ("REQUIRED", m.Required.ToString(), Navy),
            ("PRESENT", m.TotalPresent.ToString(), Ok),
            ("ABSENT", m.Absent.ToString(), m.Absent > 0 ? Danger : Ink),
            ("ON VACATION", m.OnVacation.ToString(), m.OnVacation > 0 ? Amber : Ink),
            ("SUPPLY (OS)", m.Supply.ToString(), m.Supply > 0 ? Teal : Ink),
            ("FILL", fill + "%", fillColor),
        };
        var gap = 8.0;
        var tileW = (contentW - gap * (tiles.Length - 1)) / tiles.Length;
        var tileH = 60.0;
        for (var i = 0; i < tiles.Length; i++)
        {
            DrawKpiTile(gfx, Margin + i * (tileW + gap), y, tileW, tileH, tiles[i].Label, tiles[i].Value, tiles[i].Accent);
        }
        y += tileH + 22;

        // ── Two-column analytics: donut (left) + division chart (right) ───────
        var colGap = 18.0;
        var leftW = contentW * 0.40;
        var rightW = contentW - leftW - colGap;
        var panelH = 188.0;
        var leftX = Margin;
        var rightX = Margin + leftW + colGap;

        DrawPanel(gfx, leftX, y, leftW, panelH, "WORKFORCE COMPOSITION");
        DrawDonut(gfx, leftX, y, leftW, panelH, m, fill, fillColor);

        DrawPanel(gfx, rightX, y, rightW, panelH, "DIVISION BREAKDOWN");
        DrawDivisionBars(gfx, rightX, y, rightW, panelH, m.Divisions);

        y += panelH + 22;

        // ── Department detail table (paginates) ───────────────────────────────
        gfx.DrawString("DEPARTMENT DETAIL", Bold(11), new XSolidBrush(Navy),
            new XRect(Margin, y, contentW, 14), XStringFormats.TopLeft);
        y += 20;

        double[] cols = { 66, 0, 42, 46, 42, 40, 40, 48, 74 }; // Department fills remainder
        string[] heads = { "Division", "Department", "Req", "Pres", "Abs", "Vac", "OS", "Var", "Status" };
        cols[1] = contentW - (cols[0] + cols.Skip(2).Sum());

        void TableHeader(ref double ty)
        {
            gfx.DrawRoundedRectangle(new XSolidBrush(Navy), Margin, ty, contentW, 20, 4, 4);
            var cx = Margin;
            for (var c = 0; c < heads.Length; c++)
            {
                var fmt = c is 0 or 1 ? XStringFormats.CenterLeft : XStringFormats.Center;
                gfx.DrawString(heads[c], Bold(8), new XSolidBrush(White),
                    new XRect(cx + 6, ty, cols[c] - 8, 20), fmt);
                cx += cols[c];
            }
            ty += 20;
        }

        TableHeader(ref y);

        var alt = false;
        var rowH = 17.0;
        foreach (var r in m.Departments)
        {
            if (y > h - Margin - 26)
            {
                gfx.Dispose();
                page = doc.AddPage();
                page.Size = PageSize.A4;
                gfx = XGraphics.FromPdfPage(page);
                y = Margin;
                gfx.DrawString($"DEPARTMENT DETAIL (cont.) · {m.OperationalDate:dd MMM yyyy} {m.Shift}",
                    Bold(11), new XSolidBrush(Navy), new XRect(Margin, y, contentW, 14), XStringFormats.TopLeft);
                y += 20;
                TableHeader(ref y);
            }

            if (alt)
            {
                gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(0xFA, 0xFB, 0xFD)), Margin, y, contentW, rowH);
            }
            alt = !alt;

            var cells = new (string Text, XColor Color, XStringFormat Fmt)[]
            {
                (r.Division, Muted, XStringFormats.CenterLeft),
                (r.Department, Ink, XStringFormats.CenterLeft),
                (r.Required.ToString(), Ink, XStringFormats.Center),
                (r.Present.ToString(), Ink, XStringFormats.Center),
                (r.Absent.ToString(), r.Absent > 0 ? Danger : Ink, XStringFormats.Center),
                (r.OnVacation.ToString(), r.OnVacation > 0 ? Amber : Ink, XStringFormats.Center),
                (r.Supply.ToString(), r.Supply > 0 ? Teal : Ink, XStringFormats.Center),
                (Signed(r.Variance), r.Variance < 0 ? Danger : Ok, XStringFormats.Center),
                ("", Ink, XStringFormats.Center), // status drawn as a pill below
            };
            var cx = Margin;
            for (var c = 0; c < cells.Length; c++)
            {
                if (c == cells.Length - 1)
                {
                    DrawStatusPill(gfx, cx, y, cols[c], rowH, r.Status);
                }
                else
                {
                    gfx.DrawString(cells[c].Text, c == 1 ? Bold(8.5) : Regular(8.5), new XSolidBrush(cells[c].Color),
                        new XRect(cx + 6, y, cols[c] - 8, rowH), cells[c].Fmt);
                }
                cx += cols[c];
            }
            gfx.DrawLine(new XPen(Line, 0.5), Margin, y + rowH, Margin + contentW, y + rowH);
            y += rowH;
        }

        // ── Absentees & reasons (paginates) ───────────────────────────────────
        y += 18;
        if (y > h - Margin - 80)
        {
            gfx.Dispose();
            page = doc.AddPage();
            page.Size = PageSize.A4;
            gfx = XGraphics.FromPdfPage(page);
            y = Margin;
        }

        gfx.DrawString("ABSENTEES & REASONS", Bold(11), new XSolidBrush(Navy),
            new XRect(Margin, y, contentW, 14), XStringFormats.TopLeft);
        y += 20;

        double[] aCols = { 138, 96, 58, 118, 0 };
        aCols[4] = contentW - (aCols[0] + aCols[1] + aCols[2] + aCols[3]);
        string[] aHeads = { "Employee", "Department", "Status", "Reason", "Detail" };

        void AbsHeader(ref double ty)
        {
            gfx.DrawRoundedRectangle(new XSolidBrush(Navy), Margin, ty, contentW, 20, 4, 4);
            var hx = Margin;
            for (var c = 0; c < aHeads.Length; c++)
            {
                gfx.DrawString(aHeads[c], Bold(8), new XSolidBrush(White),
                    new XRect(hx + 6, ty, aCols[c] - 8, 20), XStringFormats.CenterLeft);
                hx += aCols[c];
            }
            ty += 20;
        }

        if (m.Absentees.Count == 0)
        {
            gfx.DrawString("No absentees recorded for this shift.", Regular(9), new XSolidBrush(Muted),
                new XRect(Margin, y, contentW, 14), XStringFormats.TopLeft);
            y += 16;
        }
        else
        {
            AbsHeader(ref y);
            var altA = false;
            foreach (var a in m.Absentees)
            {
                if (y > h - Margin - 26)
                {
                    gfx.Dispose();
                    page = doc.AddPage();
                    page.Size = PageSize.A4;
                    gfx = XGraphics.FromPdfPage(page);
                    y = Margin;
                    gfx.DrawString($"ABSENTEES & REASONS (cont.) · {m.OperationalDate:dd MMM yyyy} {m.Shift}",
                        Bold(11), new XSolidBrush(Navy), new XRect(Margin, y, contentW, 14), XStringFormats.TopLeft);
                    y += 20;
                    AbsHeader(ref y);
                }

                if (altA)
                {
                    gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(0xFA, 0xFB, 0xFD)), Margin, y, contentW, rowH);
                }
                altA = !altA;

                var employee = string.IsNullOrWhiteSpace(a.Badge) ? a.Name : $"{a.Name}  ·  {a.Badge}";
                var reason = a.ReasonCategory == "—" ? a.ReasonKind : $"{a.ReasonKind} · {a.ReasonCategory}";
                var reasonColor = a.ReasonKind == "Informed" ? Ok
                    : a.ReasonKind is "Not Informed" or "Not recorded" ? Danger
                    : Muted;

                var acells = new (string Text, XColor Color)[]
                {
                    (employee, Ink),
                    (a.Department, Muted),
                    (a.StatusLabel, a.StatusLabel == "On vacation" ? Amber : Danger),
                    (reason, reasonColor),
                    (a.Detail, Muted),
                };
                var ax = Margin;
                for (var c = 0; c < acells.Length; c++)
                {
                    var font = c == 0 ? Bold(8.5) : Regular(8.5);
                    gfx.DrawString(Clip(gfx, acells[c].Text, aCols[c] - 8, font), font, new XSolidBrush(acells[c].Color),
                        new XRect(ax + 6, y, aCols[c] - 8, rowH), XStringFormats.CenterLeft);
                    ax += aCols[c];
                }
                gfx.DrawLine(new XPen(Line, 0.5), Margin, y + rowH, Margin + contentW, y + rowH);
                y += rowH;
            }
        }

        // Dispose the live page graphics before the footer pass — PDFsharp permits only one
        // XGraphics per page and the footer loop opens a fresh one for every page.
        gfx.Dispose();
        DrawFooters(doc);

        using var ms = new MemoryStream();
        doc.Save(ms);
        return ms.ToArray();
    }

    // ── Header banner ─────────────────────────────────────────────────────────
    private static void DrawHeaderBanner(XGraphics gfx, double w, byte[] logo, Model m)
    {
        var band = new XRect(0, 0, w, BandH);
        gfx.DrawRectangle(new XLinearGradientBrush(band, Navy, Brand, XLinearGradientMode.Horizontal), band);
        gfx.DrawRectangle(new XSolidBrush(Gold), 0, BandH - 4, w, 4);

        // Logo on a white rounded card so a coloured/transparent logo reads on the banner.
        var logoH = 52.0;
        var logoAreaX = Margin;
        var cardW = 150.0;
        try
        {
            if (logo.Length > 0)
            {
                using var ls = new MemoryStream(logo);
                using var img = XImage.FromStream(ls);
                var lw = logoH * img.PixelWidth / Math.Max(1, img.PixelHeight);
                cardW = lw + 24;
                gfx.DrawRoundedRectangle(new XSolidBrush(White), logoAreaX, 26, cardW, logoH + 12, 10, 10);
                gfx.DrawImage(img, logoAreaX + 12, 32, lw, logoH);
            }
            else
            {
                cardW = 0;
            }
        }
        catch
        {
            cardW = 0;
        }

        var titleX = cardW > 0 ? logoAreaX + cardW + 16 : Margin;
        gfx.DrawString("DAILY MANPOWER REPORT", Bold(20), new XSolidBrush(White),
            new XRect(titleX, 30, w - titleX - Margin, 26), XStringFormats.TopLeft);
        gfx.DrawString("Emirates Glass · Manpower Allocation", Regular(10), new XSolidBrush(XColor.FromArgb(0xD8, 0xE6, 0xF4)),
            new XRect(titleX, 58, w - titleX - Margin, 14), XStringFormats.TopLeft);

        // Right block: date + shift pill + captured time.
        var rightW = 190.0;
        var rightX = w - Margin - rightW;
        gfx.DrawString(m.OperationalDate.ToString("ddd dd MMM yyyy"), Bold(14), new XSolidBrush(White),
            new XRect(rightX, 28, rightW, 18), XStringFormats.TopRight);

        var shiftText = (m.Shift ?? string.Empty).ToUpperInvariant() + " SHIFT";
        var pillW = gfx.MeasureString(shiftText, Bold(8.5)).Width + 20;
        var pillX = rightX + rightW - pillW;
        gfx.DrawRoundedRectangle(new XSolidBrush(Gold), pillX, 50, pillW, 18, 9, 9);
        gfx.DrawString(shiftText, Bold(8.5), new XSolidBrush(White), new XRect(pillX, 50, pillW, 18), XStringFormats.Center);

        gfx.DrawString($"Captured {m.CapturedAtUtc:dd MMM HH:mm} UTC", Regular(8), new XSolidBrush(XColor.FromArgb(0xD8, 0xE6, 0xF4)),
            new XRect(rightX, 74, rightW, 12), XStringFormats.TopRight);
    }

    // ── KPI tile ────────────────────────────────────────────────────────────
    private static void DrawKpiTile(XGraphics gfx, double x, double y, double w, double h, string label, string value, XColor accent)
    {
        gfx.DrawRoundedRectangle(new XPen(Line, 0.75), new XSolidBrush(TileBg), x, y, w, h, 8, 8);
        gfx.DrawString(value, Bold(19), new XSolidBrush(accent), new XRect(x, y + 9, w, 24), XStringFormats.Center);
        gfx.DrawRectangle(new XSolidBrush(accent), x + w / 2 - 12, y + 35, 24, 2.5);
        gfx.DrawString(label, Bold(7.5), new XSolidBrush(Muted), new XRect(x, y + 41, w, 12), XStringFormats.Center);
    }

    // ── Panel frame + title ───────────────────────────────────────────────────
    private static void DrawPanel(XGraphics gfx, double x, double y, double w, double h, string title)
    {
        gfx.DrawRoundedRectangle(new XPen(Line, 0.75), new XSolidBrush(White), x, y, w, h, 8, 8);
        gfx.DrawString(title, Bold(9.5), new XSolidBrush(Navy), new XRect(x + 14, y + 12, w - 28, 14), XStringFormats.TopLeft);
        gfx.DrawLine(new XPen(Line, 0.75), x + 14, y + 30, x + w - 14, y + 30);
    }

    // ── Donut: on-roll present / absent / vacation, fill % in the centre ──────
    private static void DrawDonut(XGraphics gfx, double x, double y, double w, double h, Model m, int fill, XColor fillColor)
    {
        var cx = x + w * 0.34;
        var cy = y + 30 + (h - 30) / 2 - 6;
        var r = Math.Min(w * 0.28, (h - 40) / 2);
        var box = new XRect(cx - r, cy - r, 2 * r, 2 * r);

        var segs = new (int Value, XColor Color)[]
        {
            (m.Present, Ok),
            (m.Absent, Danger),
            (m.OnVacation, Amber),
        };
        var total = segs.Sum(s => s.Value);

        if (total <= 0)
        {
            gfx.DrawEllipse(new XPen(Track, r * 0.42), box);
        }
        else
        {
            double start = -90;
            foreach (var s in segs)
            {
                if (s.Value <= 0)
                {
                    continue;
                }

                var sweep = 360.0 * s.Value / total;
                gfx.DrawPie(new XSolidBrush(s.Color), box, start, sweep);
                start += sweep;
            }
        }

        // Punch the centre to make it a ring.
        var r2 = r * 0.60;
        gfx.DrawEllipse(new XSolidBrush(White), cx - r2, cy - r2, 2 * r2, 2 * r2);
        gfx.DrawString(fill + "%", Bold(20), new XSolidBrush(fillColor), new XRect(cx - r2, cy - 16, 2 * r2, 20), XStringFormats.Center);
        gfx.DrawString("FILL", Bold(7.5), new XSolidBrush(Muted), new XRect(cx - r2, cy + 5, 2 * r2, 10), XStringFormats.Center);

        // Legend on the right of the donut.
        var lx = x + w * 0.62;
        var lw = x + w - 14 - lx;
        var ly = y + 48;
        var legend = new (string Label, int Value, XColor Color)[]
        {
            ("Present", m.Present, Ok),
            ("Absent", m.Absent, Danger),
            ("On vacation", m.OnVacation, Amber),
            ("Supply (OS)", m.Supply, Teal),
        };
        foreach (var item in legend)
        {
            gfx.DrawEllipse(new XSolidBrush(item.Color), lx, ly + 2, 8, 8);
            gfx.DrawString(item.Label, Regular(8.5), new XSolidBrush(Ink), new XRect(lx + 14, ly, lw - 40, 12), XStringFormats.CenterLeft);
            gfx.DrawString(item.Value.ToString(), Bold(8.5), new XSolidBrush(item.Color), new XRect(lx, ly, lw, 12), XStringFormats.CenterRight);
            ly += 22;
        }
    }

    // ── Division fill bars ────────────────────────────────────────────────────
    private static void DrawDivisionBars(XGraphics gfx, double x, double y, double w, double h, IReadOnlyList<DivisionRow> divisions)
    {
        var innerX = x + 14;
        var innerW = w - 28;
        var labelW = 96.0;
        var valueW = 96.0;
        var barX = innerX + labelW;
        var barW = innerW - labelW - valueW - 8;
        var top = y + 44;
        var rows = Math.Max(divisions.Count, 1);
        var slot = Math.Min(38.0, (h - 58) / rows);

        if (divisions.Count == 0)
        {
            gfx.DrawString("No division data.", Regular(9), new XSolidBrush(Muted), new XRect(innerX, top, innerW, 14), XStringFormats.TopLeft);
            return;
        }

        for (var i = 0; i < divisions.Count; i++)
        {
            var d = divisions[i];
            var rowY = top + i * slot;
            var pct = d.Required > 0 ? Math.Min(1.0, (double)d.Present / d.Required) : (d.Present > 0 ? 1.0 : 0);
            var ratio = d.Required > 0 ? (double)d.Present / d.Required : 1.0;
            var color = ratio >= 1.0 ? Ok : ratio >= 0.85 ? Amber : Danger;
            var barH = 13.0;
            var barY = rowY + 4;

            gfx.DrawString(d.Division, Bold(9), new XSolidBrush(Ink), new XRect(innerX, barY - 2, labelW - 6, barH + 4), XStringFormats.CenterLeft);
            gfx.DrawRoundedRectangle(new XSolidBrush(Track), barX, barY, barW, barH, 6, 6);
            gfx.DrawRoundedRectangle(new XSolidBrush(color), barX, barY, Math.Max(6, barW * pct), barH, 6, 6);
            var pctText = d.Required > 0 ? $"{d.Present}/{d.Required}  ·  {(int)Math.Round(ratio * 100)}%" : $"{d.Present}/0";
            gfx.DrawString(pctText, Bold(8.5), new XSolidBrush(color), new XRect(barX + barW + 8, barY - 2, valueW, barH + 4), XStringFormats.CenterRight);
        }
    }

    // ── Status pill ─────────────────────────────────────────────────────────
    private static void DrawStatusPill(XGraphics gfx, double x, double y, double w, double h, string status)
    {
        var s = (status ?? string.Empty).Trim();
        var lower = s.ToLowerInvariant();
        var color = lower.Contains("short") ? Danger
            : lower.Contains("excess") ? Teal
            : lower.Contains("optimal") ? Ok
            : Muted;
        var text = string.IsNullOrEmpty(s) ? "—" : s.ToUpperInvariant();
        var pw = Math.Min(w - 8, gfx.MeasureString(text, Bold(7.5)).Width + 16);
        var px = x + (w - pw) / 2;
        var py = y + (h - 14) / 2;
        gfx.DrawRoundedRectangle(new XSolidBrush(color), px, py, pw, 14, 7, 7);
        gfx.DrawString(text, Bold(7.5), new XSolidBrush(White), new XRect(px, py, pw, 14), XStringFormats.Center);
    }

    // ── Footers ───────────────────────────────────────────────────────────────
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

    /// <summary>Truncates text with an ellipsis so it fits within the given width for the font.</summary>
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

    private static string Signed(int value) => value > 0 ? $"+{value}" : value.ToString();
}
