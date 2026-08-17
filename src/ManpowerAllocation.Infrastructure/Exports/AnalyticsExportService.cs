using ClosedXML.Excel;
using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Analytics;
using ManpowerAllocation.Application.Exports;
using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Infrastructure.Exports;

/// <summary>
/// Builds Excel (ClosedXML) and PDF (PdfSharp) files for the analytics reports. Each report is first
/// shaped into a common <see cref="ReportModel"/> (title, subtitle, KPI tiles, titled tables), then
/// rendered to the requested format so the two outputs stay in step.
/// </summary>
public sealed class AnalyticsExportService : IAnalyticsExportService
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IAnalyticsService _analytics;
    private readonly IClock _clock;

    /// <summary>Initialises the service.</summary>
    public AnalyticsExportService(IAnalyticsService analytics, IClock clock)
    {
        _analytics = analytics;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<ExportFile> ExportAsync(
        AnalyticsReport report, AnalyticsExportFormat format, DateTime from, DateTime to,
        Division? division, int? employeeId, CancellationToken cancellationToken = default)
    {
        var model = report switch
        {
            AnalyticsReport.Overall => await BuildOverallAsync(from, to, cancellationToken),
            AnalyticsReport.Departments => await BuildDepartmentsAsync(from, to, division, cancellationToken),
            AnalyticsReport.Absentees => await BuildAbsenteesAsync(from, to, division, cancellationToken),
            AnalyticsReport.Employee => await BuildEmployeeAsync(employeeId, from, to, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(report))
        };

        var prefix = $"{report.ToString().ToLowerInvariant()}_analytics_{from:yyyyMMdd}_{to:yyyyMMdd}";
        return format == AnalyticsExportFormat.Excel ? ToExcel(model, prefix) : ToPdf(model, prefix);
    }

    // ── Report builders ─────────────────────────────────────────────────────────────────

    private async Task<ReportModel> BuildOverallAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var points = await _analytics.GetOverallTrendAsync(from, to, ct);
        var rows = points
            .Select(p => new[]
            {
                p.Date.ToString("dd MMM yyyy"), p.Shift, p.Required.ToString(), p.TotalPresent.ToString(),
                p.Absent.ToString(), p.OnVacation.ToString(), p.Supply.ToString(), Signed(p.Variance), p.FillPct + "%"
            })
            .ToList();

        var kpis = new List<(string, string)>
        {
            ("Captures", points.Count.ToString()),
            ("Avg fill %", points.Count > 0 ? (int)Math.Round(points.Average(p => p.FillPct)) + "%" : "—"),
            ("Short captures", points.Count(p => p.Variance < 0).ToString())
        };

        return new ReportModel(
            "Overall Staffing Trend", RangeText(from, to), kpis,
            new List<ReportSection>
            {
                new("Captured points",
                    new[] { "Date", "Shift", "Required", "Present", "Absent", "Vacation", "Supply", "Variance", "Fill %" },
                    rows)
            });
    }

    private async Task<ReportModel> BuildDepartmentsAsync(DateTime from, DateTime to, Division? division, CancellationToken ct)
    {
        var data = await _analytics.GetDepartmentTrendAsync(from, to, division, ct);
        var rows = data
            .Select(d => new[]
            {
                d.Division, d.Department, d.Captures.ToString(), d.AvgRequired.ToString("0.0"),
                d.AvgPresent.ToString("0.0"), d.AvgFillPct + "%", d.DaysShort.ToString(), d.DaysExcess.ToString(), Signed(d.WorstVariance)
            })
            .ToList();

        var kpis = new List<(string, string)>
        {
            ("Departments", data.Count.ToString()),
            ("With short days", data.Count(d => d.DaysShort > 0).ToString())
        };

        return new ReportModel(
            "Department Trend", RangeText(from, to) + DivisionText(division), kpis,
            new List<ReportSection>
            {
                new("Per department",
                    new[] { "Division", "Department", "Captures", "Avg req", "Avg present", "Avg fill %", "Days short", "Days excess", "Worst var" },
                    rows)
            });
    }

    private async Task<ReportModel> BuildAbsenteesAsync(DateTime from, DateTime to, Division? division, CancellationToken ct)
    {
        var a = await _analytics.GetAbsenteeAnalyticsAsync(from, to, division, ct);

        var kpis = new List<(string, string)>
        {
            ("Records", a.TotalRecords.ToString()),
            ("Employees", a.TotalEmployees.ToString()),
            ("Informed", a.InformedRecords.ToString()),
            ("Not informed", a.NotInformedRecords.ToString())
        };

        return new ReportModel(
            "Absentee Analytics", RangeText(from, to) + DivisionText(division), kpis,
            new List<ReportSection>
            {
                new("By reason",
                    new[] { "Kind", "Category", "Records", "Employees" },
                    a.ByCategory.Select(c => new[] { c.Kind, c.Category, c.Records.ToString(), c.Employees.ToString() }).ToList()),
                new("By department",
                    new[] { "Division", "Department", "Records", "Employees" },
                    a.ByDepartment.Select(c => new[] { c.Division, c.Department, c.Records.ToString(), c.Employees.ToString() }).ToList()),
                new("Absence trend",
                    new[] { "Date", "Absent", "On vacation" },
                    a.Trend.Select(t => new[] { t.Date.ToString("dd MMM yyyy"), t.Absent.ToString(), t.OnVacation.ToString() }).ToList()),
                new("Absenteeism rate",
                    new[] { "Division", "Department", "Avg absent", "Avg on roll", "Rate %" },
                    a.ByDepartmentRate.Select(r => new[] { r.Division, r.Department, r.AvgAbsent.ToString("0.0"), r.AvgOnRoll.ToString("0.0"), r.AbsenceRatePct + "%" }).ToList()),
                new("Informed vs not informed",
                    new[] { "Division", "Department", "Informed", "Not informed", "Informed %" },
                    a.Compliance.Select(c => new[] { c.Division, c.Department, c.Informed.ToString(), c.NotInformed.ToString(), c.InformedPct + "%" }).ToList()),
                new("Top absentees",
                    new[] { "Employee", "Badge", "Department", "Absent days" },
                    a.TopAbsentees.Select(t => new[] { t.Name, t.Badge ?? "—", t.Department, t.AbsentDays.ToString() }).ToList())
            });
    }

    private async Task<ReportModel> BuildEmployeeAsync(int? employeeId, DateTime from, DateTime to, CancellationToken ct)
    {
        if (employeeId is not { } id)
        {
            return new ReportModel("Employee History", RangeText(from, to), new List<(string, string)>(),
                new List<ReportSection> { new("Employee", new[] { "Note" }, new List<string[]> { new[] { "No employee selected." } }) });
        }

        var history = await _analytics.GetEmployeeHistoryAsync(id, from, to, ct);
        if (history is null)
        {
            return new ReportModel("Employee History", RangeText(from, to), new List<(string, string)>(),
                new List<ReportSection> { new("Employee", new[] { "Note" }, new List<string[]> { new[] { "Employee not found." } }) });
        }

        var who = string.IsNullOrWhiteSpace(history.Badge) ? history.Name : $"{history.Name} · {history.Badge}";
        var kpis = new List<(string, string)>
        {
            ("Present days", history.PresentDays.ToString()),
            ("Absent days", history.AbsentDays.ToString()),
            ("Vacation days", history.VacationDays.ToString())
        };

        return new ReportModel(
            $"Employee History — {who}", RangeText(from, to), kpis,
            new List<ReportSection>
            {
                new("Recorded absence reasons",
                    new[] { "Kind", "Category", "From", "To", "Comment", "Recorded by" },
                    history.Absences.Select(r => new[]
                    {
                        r.Kind, r.Category, r.FromDate.ToString("dd MMM yyyy"),
                        r.ToDate?.ToString("dd MMM yyyy") ?? "—", r.Comment ?? "—", r.SetBy ?? "—"
                    }).ToList()),
                new("Captured attendance",
                    new[] { "Date", "Shift", "Department", "Status" },
                    history.Days.Select(d => new[] { d.Date.ToString("dd MMM yyyy"), d.Shift, d.Department, d.Status }).ToList())
            });
    }

    // ── Renderers ─────────────────────────────────────────────────────────────────────

    private ExportFile ToExcel(ReportModel model, string prefix)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Report");

        var titleCell = ws.Cell(1, 1);
        titleCell.Value = "EMIRATES GLASS";
        titleCell.Style.Font.Bold = true;
        titleCell.Style.Font.FontSize = 16;
        titleCell.Style.Font.FontColor = XLColor.FromHtml("#B8935A");
        ws.Range(1, 1, 1, 6).Merge();

        var subtitle = ws.Cell(2, 1);
        subtitle.Value = $"{model.Title} — {model.Subtitle} · generated {_clock.UtcNow:yyyy-MM-dd HH:mm} UTC";
        subtitle.Style.Font.Bold = true;
        subtitle.Style.Font.FontColor = XLColor.FromHtml("#12446B");
        ws.Range(2, 1, 2, 6).Merge();

        var row = 4;
        if (model.Kpis.Count > 0)
        {
            for (var i = 0; i < model.Kpis.Count; i++)
            {
                var lbl = ws.Cell(row, i + 1);
                lbl.Value = model.Kpis[i].Label;
                lbl.Style.Font.Bold = true;
                lbl.Style.Font.FontColor = XLColor.White;
                lbl.Style.Fill.BackgroundColor = XLColor.FromHtml("#12446B");
                ws.Cell(row + 1, i + 1).Value = model.Kpis[i].Value;
            }
            row += 3;
        }

        foreach (var section in model.Sections)
        {
            var head = ws.Cell(row, 1);
            head.Value = section.Title;
            head.Style.Font.Bold = true;
            head.Style.Font.FontColor = XLColor.FromHtml("#12446B");
            ws.Range(row, 1, row, Math.Max(1, section.Headers.Length)).Merge();
            row++;

            for (var c = 0; c < section.Headers.Length; c++)
            {
                var cell = ws.Cell(row, c + 1);
                cell.Value = section.Headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#12446B");
            }
            row++;

            foreach (var dataRow in section.Rows)
            {
                for (var c = 0; c < dataRow.Length; c++)
                {
                    ws.Cell(row, c + 1).Value = dataRow[c];
                }
                row++;
            }
            row += 2;
        }

        ws.Columns().AdjustToContents(1, 60);

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        var stamp = _clock.UtcNow.ToString("yyyyMMdd");
        return new ExportFile($"{prefix}_{stamp}.xlsx", XlsxContentType, stream.ToArray());
    }

    private ExportFile ToPdf(ReportModel model, string prefix)
    {
        var kpis = model.Kpis.Select(k => new PdfAnalyticsReport.Kpi(k.Label, k.Value)).ToList();
        var sections = model.Sections.Select(s => new PdfAnalyticsReport.Section(s.Title, s.Headers, s.Rows)).ToList();
        var pdf = PdfAnalyticsReport.Render(LoadLogo(), model.Title, model.Subtitle, kpis, sections);
        var stamp = _clock.UtcNow.ToString("yyyyMMdd");
        return new ExportFile($"{prefix}_{stamp}.pdf", "application/pdf", pdf);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────

    private static byte[] LoadLogo() => EmbeddedPdfFonts.ReadEmbedded("emirates-glass-logo.png");

    private static string RangeText(DateTime from, DateTime to)
    {
        var a = from.Date <= to.Date ? from.Date : to.Date;
        var b = from.Date <= to.Date ? to.Date : from.Date;
        return $"{a:dd MMM yyyy} – {b:dd MMM yyyy}";
    }

    private static string DivisionText(Division? division) => division is { } d
        ? " · " + d switch { Division.Egl => "EGL", Division.FunctionalSupport => "Functional Support", Division.Brg => "BRG", _ => d.ToString() }
        : " · All divisions";

    private static string Signed(int value) => value > 0 ? $"+{value}" : value.ToString();

    /// <summary>Common shape both renderers consume.</summary>
    private sealed record ReportModel(string Title, string Subtitle, IReadOnlyList<(string Label, string Value)> Kpis, IReadOnlyList<ReportSection> Sections);

    /// <summary>A titled table within a report.</summary>
    private sealed record ReportSection(string Title, string[] Headers, IReadOnlyList<string[]> Rows);
}
