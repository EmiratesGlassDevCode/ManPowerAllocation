using ClosedXML.Excel;
using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Dashboard;
using ManpowerAllocation.Application.Exports;
using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManpowerAllocation.Infrastructure.Exports;

/// <summary>Builds the downloadable Excel reports with ClosedXML from the governed database.</summary>
public sealed class ClosedXmlReportExportService : IReportExportService
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IApplicationDbContext _dbContext;
    private readonly IClock _clock;

    /// <summary>Initialises the service.</summary>
    /// <param name="dbContext">The application persistence context.</param>
    /// <param name="clock">Clock used to date-stamp file names.</param>
    public ClosedXmlReportExportService(IApplicationDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<ExportFile> BuildRequirementsTemplateAsync(CancellationToken cancellationToken = default)
    {
        var departments = await LoadDepartmentsAsync(cancellationToken);

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Master Requirements");
        var row = WriteBrandedHeader(ws, "Master Requirements Template",
            "Category", "Department", "Day Shift Required", "Night Shift Required", "Total", "Sequence");

        foreach (var d in departments)
        {
            ws.Cell(row, 1).Value = CategoryLabel(d.Division);
            ws.Cell(row, 2).Value = d.Name;
            ws.Cell(row, 3).Value = d.RequiredDay;
            ws.Cell(row, 4).Value = d.RequiredNight;
            ws.Cell(row, 5).Value = d.RequiredDay + d.RequiredNight;
            ws.Cell(row, 6).Value = d.Sequence;
            row++;
        }

        ws.Columns().AdjustToContents(1, 60); // bound the scan: sizing from the first rows keeps large exports fast
        return ToFile(wb, "master_requirements");
    }

    /// <inheritdoc />
    public async Task<ExportFile> BuildStaffingReportAsync(CancellationToken cancellationToken = default)
    {
        var departments = await LoadDepartmentsAsync(cancellationToken);
        var employeesByDepartment = await LoadEmployeesByDepartmentAsync(cancellationToken);

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Report");
        var row = WriteBrandedHeader(ws, "Staffing Report",
            "Division", "Department", "Day Req", "Night Req", "Total Req",
            "Present", "Outsource", "Total Present", "Absent", "Vacation", "Status");

        foreach (var d in departments)
        {
            var members = employeesByDepartment.TryGetValue(d.Id, out var list) ? list : new List<Employee>();
            var stats = StaffingCalculator.ComputeDepartmentStats(d, members, ShiftFilter.All);

            ws.Cell(row, 1).Value = DivisionLabel(d.Division);
            ws.Cell(row, 2).Value = d.Name;
            ws.Cell(row, 3).Value = d.RequiredDay;
            ws.Cell(row, 4).Value = d.RequiredNight;
            ws.Cell(row, 5).Value = stats.Required;
            ws.Cell(row, 6).Value = stats.Present;
            ws.Cell(row, 7).Value = stats.SupplyPresent;
            ws.Cell(row, 8).Value = stats.TotalPresent;
            ws.Cell(row, 9).Value = stats.Absent;
            ws.Cell(row, 10).Value = stats.OnVacation;
            ws.Cell(row, 11).Value = stats.Status.ToString();
            row++;
        }

        ws.Columns().AdjustToContents(1, 60); // bound the scan: sizing from the first rows keeps large exports fast
        return ToFile(wb, "staffing_report");
    }

    /// <inheritdoc />
    public async Task<ExportFile> BuildAttendanceAsync(CancellationToken cancellationToken = default)
    {
        var employees = await _dbContext.Employees
            .AsNoTracking()
            .Include(e => e.Department)
            .OrderBy(e => e.Division)
            .ThenBy(e => e.Department!.Name)
            .ThenBy(e => e.Name)
            .ToListAsync(cancellationToken);

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Attendance");
        // "Ref" (the employee id) is the stable key the non-destructive edit-apply upload uses to
        // match an edited row back to the exact employee. Leave it intact when editing offline.
        var row = WriteBrandedHeader(ws, "Attendance",
            "Ref", "Name", "ID", "Division", "Department", "Shift", "Available", "Outsource", "Notes");

        foreach (var e in employees)
        {
            ws.Cell(row, 1).Value = e.Id;
            ws.Cell(row, 2).Value = e.Name;
            ws.Cell(row, 3).Value = e.BadgeNumber ?? string.Empty;
            ws.Cell(row, 4).Value = DivisionLabel(e.Division);
            ws.Cell(row, 5).Value = e.Department?.Name ?? string.Empty;
            ws.Cell(row, 6).Value = e.Shift.ToString().ToUpperInvariant();
            ws.Cell(row, 7).Value = AvailabilityLabel(e.Status);
            ws.Cell(row, 8).Value = e.IsSupply ? "YES" : string.Empty;
            ws.Cell(row, 9).Value = e.Notes ?? string.Empty;
            row++;
        }

        ws.Columns().AdjustToContents(1, 60); // bound the scan: sizing from the first rows keeps large exports fast
        return ToFile(wb, "attendance");
    }

    /// <inheritdoc />
    public async Task<ExportFile> BuildSnapshotHistoryExcelAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var rows = await LoadSnapshotFactRowsAsync(fromDate, toDate, cancellationToken);

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("History");
        var row = WriteBrandedHeader(ws, "Daily Report History",
            "Date", "Shift", "Division", "Department", "Required", "On Roll", "Present",
            "Outsource", "Total Present", "Absent", "Vacation", "Variance", "Status");

        foreach (var r in rows)
        {
            ws.Cell(row, 1).Value = r.Date;
            ws.Cell(row, 1).Style.DateFormat.Format = "yyyy-mm-dd";
            ws.Cell(row, 2).Value = r.Shift;
            ws.Cell(row, 3).Value = r.Division;
            ws.Cell(row, 4).Value = r.Department;
            ws.Cell(row, 5).Value = r.Required;
            ws.Cell(row, 6).Value = r.OnRoll;
            ws.Cell(row, 7).Value = r.Present;
            ws.Cell(row, 8).Value = r.SupplyPresent;
            ws.Cell(row, 9).Value = r.TotalPresent;
            ws.Cell(row, 10).Value = r.Absent;
            ws.Cell(row, 11).Value = r.OnVacation;
            ws.Cell(row, 12).Value = r.Variance;
            ws.Cell(row, 13).Value = r.Status;
            row++;
        }

        ws.Columns().AdjustToContents(1, 60); // bound the scan: sizing from the first rows keeps large exports fast
        return ToFile(wb, "report_history");
    }

    /// <inheritdoc />
    public async Task<ExportFile> BuildSnapshotHistoryCsvAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var rows = await LoadSnapshotFactRowsAsync(fromDate, toDate, cancellationToken);

        var sb = new System.Text.StringBuilder();
        sb.Append("Date,Shift,Division,Department,Required,OnRoll,Present,Outsource,TotalPresent,Absent,Vacation,Variance,Status\n");
        foreach (var r in rows)
        {
            sb.Append(r.Date.ToString("yyyy-MM-dd")).Append(',')
              .Append(Csv(r.Shift)).Append(',')
              .Append(Csv(r.Division)).Append(',')
              .Append(Csv(r.Department)).Append(',')
              .Append(r.Required).Append(',')
              .Append(r.OnRoll).Append(',')
              .Append(r.Present).Append(',')
              .Append(r.SupplyPresent).Append(',')
              .Append(r.TotalPresent).Append(',')
              .Append(r.Absent).Append(',')
              .Append(r.OnVacation).Append(',')
              .Append(r.Variance).Append(',')
              .Append(Csv(r.Status)).Append('\n');
        }

        var stamp = _clock.UtcNow.ToString("yyyyMMdd");
        // Prepend a UTF-8 BOM so Excel opens non-ASCII names in the correct encoding.
        var encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var preamble = encoding.GetPreamble();
        var body = encoding.GetBytes(sb.ToString());
        var bytes = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);
        return new ExportFile($"report_history_{stamp}.csv", "text/csv", bytes);
    }

    /// <inheritdoc />
    public async Task<ExportFile> BuildSnapshotHistoryPdfAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var from = fromDate.Date;
        var to = toDate.Date;

        var raw = await _dbContext.AllocationSnapshots
            .AsNoTracking()
            .Where(s => s.OperationalDate >= from && s.OperationalDate <= to)
            .OrderBy(s => s.OperationalDate)
            .ThenBy(s => s.Shift)
            .Select(s => new { s.OperationalDate, s.Shift, s.TotalPresent, s.Required, s.Variance, s.ShortageDepartmentCount })
            .ToListAsync(cancellationToken);

        var rows = raw
            .Select(s => new PdfHistoryReport.Row(
                s.OperationalDate, s.Shift.ToString(), s.TotalPresent, s.Required, s.Variance, s.ShortageDepartmentCount))
            .ToList();

        var pdf = PdfHistoryReport.Render(LoadLogo(), from, to, rows);
        var stamp = _clock.UtcNow.ToString("yyyyMMdd");
        return new ExportFile($"report_history_{stamp}.pdf", "application/pdf", pdf);
    }

    /// <inheritdoc />
    public async Task<ExportFile> BuildDailyReportPdfAsync(long snapshotId, CancellationToken cancellationToken = default)
    {
        var snapshot = await LoadSnapshotWithDepartmentsAsync(snapshotId, cancellationToken);

        var divisions = snapshot.Departments
            .GroupBy(d => d.Division)
            .OrderBy(g => g.Key)
            .Select(g => new PdfDailyReport.DivisionRow(DivisionLabel(g.Key), g.Sum(d => d.Required), g.Sum(d => d.TotalPresent)))
            .ToList();

        var deptRows = snapshot.Departments
            .OrderBy(d => d.Division)
            .ThenByDescending(d => d.Required)
            .ThenBy(d => d.DepartmentName)
            .Select(d => new PdfDailyReport.DeptRow(
                DivisionLabel(d.Division), d.DepartmentName, d.Required, d.TotalPresent,
                d.Absent, d.OnVacation, d.SupplyPresent, d.Variance, d.Status))
            .ToList();

        var model = new PdfDailyReport.Model(
            snapshot.OperationalDate, snapshot.Shift.ToString(), snapshot.CapturedAtUtc,
            snapshot.Required, snapshot.OnRoll, snapshot.Present, snapshot.Absent, snapshot.OnVacation,
            snapshot.SupplyPresent, snapshot.TotalPresent, snapshot.Variance, snapshot.ShortageDepartmentCount,
            divisions, deptRows);

        var pdf = PdfDailyReport.Render(LoadLogo(), model);
        return new ExportFile($"daily_report_{snapshot.OperationalDate:yyyyMMdd}_{snapshot.Shift}.pdf", "application/pdf", pdf);
    }

    /// <inheritdoc />
    public async Task<ExportFile> BuildDailyReportExcelAsync(long snapshotId, CancellationToken cancellationToken = default)
    {
        var snapshot = await LoadSnapshotWithDepartmentsAsync(snapshotId, cancellationToken);

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Daily Report");
        var title = $"Daily Report — {snapshot.OperationalDate:dd MMM yyyy} · {snapshot.Shift} Shift";

        // Summary block above the department table.
        var titleCell = ws.Cell(1, 1);
        titleCell.Value = "EMIRATES GLASS";
        titleCell.Style.Font.Bold = true;
        titleCell.Style.Font.FontSize = 16;
        titleCell.Style.Font.FontColor = XLColor.FromHtml("#B8935A");
        ws.Range(1, 1, 1, 11).Merge();

        var subtitle = ws.Cell(2, 1);
        subtitle.Value = $"Manpower Allocation · {title} — generated {_clock.UtcNow:yyyy-MM-dd HH:mm} UTC";
        subtitle.Style.Font.Bold = true;
        subtitle.Style.Font.FontColor = XLColor.FromHtml("#12446B");
        ws.Range(2, 1, 2, 11).Merge();

        var fill = snapshot.Required > 0 ? (int)Math.Round(100.0 * snapshot.TotalPresent / snapshot.Required) : 0;
        var summary = new (string Label, int Value)[]
        {
            ("Required", snapshot.Required),
            ("Total Present", snapshot.TotalPresent),
            ("Absent", snapshot.Absent),
            ("On Vacation", snapshot.OnVacation),
            ("Supply (OS)", snapshot.SupplyPresent),
            ("Variance", snapshot.Variance),
            ("Fill %", fill),
            ("Short departments", snapshot.ShortageDepartmentCount),
        };
        for (var i = 0; i < summary.Length; i++)
        {
            var col = i + 1;
            var lbl = ws.Cell(4, col);
            lbl.Value = summary[i].Label;
            lbl.Style.Font.Bold = true;
            lbl.Style.Font.FontColor = XLColor.White;
            lbl.Style.Fill.BackgroundColor = XLColor.FromHtml("#12446B");
            ws.Cell(5, col).Value = summary[i].Value;
        }

        // Department table.
        const int headerRow = 7;
        var headers = new[] { "Division", "Department", "Required", "On Roll", "Present", "Outsource", "Total Present", "Absent", "Vacation", "Variance", "Status" };
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(headerRow, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#12446B");
        }

        var row = headerRow + 1;
        foreach (var d in snapshot.Departments.OrderBy(d => d.Division).ThenByDescending(d => d.Required).ThenBy(d => d.DepartmentName))
        {
            ws.Cell(row, 1).Value = DivisionLabel(d.Division);
            ws.Cell(row, 2).Value = d.DepartmentName;
            ws.Cell(row, 3).Value = d.Required;
            ws.Cell(row, 4).Value = d.OnRoll;
            ws.Cell(row, 5).Value = d.Present;
            ws.Cell(row, 6).Value = d.SupplyPresent;
            ws.Cell(row, 7).Value = d.TotalPresent;
            ws.Cell(row, 8).Value = d.Absent;
            ws.Cell(row, 9).Value = d.OnVacation;
            ws.Cell(row, 10).Value = d.Variance;
            ws.Cell(row, 11).Value = d.Status;
            row++;
        }

        ws.Columns().AdjustToContents(1, 60);
        return ToFile(wb, $"daily_report_{snapshot.OperationalDate:yyyyMMdd}_{snapshot.Shift}");
    }

    /// <summary>Loads a single captured snapshot with its department rows, or throws if absent.</summary>
    private async Task<AllocationSnapshot> LoadSnapshotWithDepartmentsAsync(long snapshotId, CancellationToken cancellationToken)
    {
        var snapshot = await _dbContext.AllocationSnapshots
            .AsNoTracking()
            .Include(s => s.Departments)
            .FirstOrDefaultAsync(s => s.Id == snapshotId, cancellationToken);

        return snapshot ?? throw new Application.Common.NotFoundException("Daily report", snapshotId);
    }

    /// <summary>Reads the embedded Emirates Glass logo bytes (empty if the resource is missing).</summary>
    private static byte[] LoadLogo() => EmbeddedPdfFonts.ReadEmbedded("emirates-glass-logo.png");

    /// <summary>Loads the flattened department fact rows for the archived reports in a date range.</summary>
    private async Task<List<SnapshotFactRow>> LoadSnapshotFactRowsAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken)
    {
        var from = fromDate.Date;
        var to = toDate.Date;

        var snapshots = await _dbContext.AllocationSnapshots
            .AsNoTracking()
            .Include(s => s.Departments)
            .Where(s => s.OperationalDate >= from && s.OperationalDate <= to)
            .OrderBy(s => s.OperationalDate)
            .ThenBy(s => s.Shift)
            .ToListAsync(cancellationToken);

        var rows = new List<SnapshotFactRow>();
        foreach (var s in snapshots)
        {
            foreach (var d in s.Departments.OrderBy(d => d.Division).ThenBy(d => d.DepartmentName))
            {
                rows.Add(new SnapshotFactRow(
                    s.OperationalDate, s.Shift.ToString().ToUpperInvariant(), DivisionLabel(d.Division), d.DepartmentName,
                    d.Required, d.OnRoll, d.Present, d.SupplyPresent, d.TotalPresent, d.Absent, d.OnVacation, d.Variance, d.Status));
            }
        }

        return rows;
    }

    /// <summary>
    /// Escapes a value for CSV. Quotes when it contains a comma, quote or newline, and neutralises
    /// spreadsheet formula injection: a value beginning with = + - or @ is prefixed with a single
    /// quote so Excel treats it as text rather than executing it as a formula.
    /// </summary>
    private static string Csv(string value)
    {
        value ??= string.Empty;

        if (value.Length > 0 && (value[0] is '=' or '+' or '-' or '@'))
        {
            value = "'" + value;
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }

    private readonly record struct SnapshotFactRow(
        DateTime Date, string Shift, string Division, string Department,
        int Required, int OnRoll, int Present, int SupplyPresent, int TotalPresent,
        int Absent, int OnVacation, int Variance, string Status);

    private async Task<List<Department>> LoadDepartmentsAsync(CancellationToken cancellationToken) =>
        await _dbContext.Departments
            .AsNoTracking()
            .OrderBy(d => d.Division)
            .ThenBy(d => d.Sequence)
            .ThenBy(d => d.Name)
            .ToListAsync(cancellationToken);

    private async Task<Dictionary<int, List<Employee>>> LoadEmployeesByDepartmentAsync(CancellationToken cancellationToken)
    {
        var employees = await _dbContext.Employees.AsNoTracking().ToListAsync(cancellationToken);
        return employees.GroupBy(e => e.DepartmentId).ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    /// Writes an Emirates Glass branded band (company name + report title + generation stamp)
    /// followed by the bold, navy column-header row, and returns the first data row index.
    /// </summary>
    private int WriteBrandedHeader(IXLWorksheet ws, string reportTitle, params string[] headers)
    {
        var span = Math.Max(headers.Length, 1);

        var titleCell = ws.Cell(1, 1);
        titleCell.Value = "EMIRATES GLASS";
        titleCell.Style.Font.Bold = true;
        titleCell.Style.Font.FontSize = 16;
        titleCell.Style.Font.FontColor = XLColor.FromHtml("#B8935A");
        ws.Range(1, 1, 1, span).Merge();

        var subtitle = ws.Cell(2, 1);
        subtitle.Value = $"Manpower Allocation · {reportTitle} — generated {_clock.UtcNow:yyyy-MM-dd HH:mm} UTC";
        subtitle.Style.Font.Bold = true;
        subtitle.Style.Font.FontColor = XLColor.FromHtml("#12446B");
        ws.Range(2, 1, 2, span).Merge();

        const int headerRow = 4;
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(headerRow, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#12446B");
        }

        return headerRow + 1;
    }

    private ExportFile ToFile(XLWorkbook wb, string prefix)
    {
        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        var stamp = _clock.UtcNow.ToString("yyyyMMdd");
        return new ExportFile($"{prefix}_{stamp}.xlsx", XlsxContentType, stream.ToArray());
    }

    private static string CategoryLabel(Division division) => division switch
    {
        Division.Egl => "PRODUCTION",
        Division.FunctionalSupport => "FUNCTIONAL SUPPORT",
        Division.Brg => "BRG",
        _ => division.ToString().ToUpperInvariant()
    };

    private static string DivisionLabel(Division division) => division switch
    {
        Division.Egl => "EGL",
        Division.FunctionalSupport => "Functional Support",
        Division.Brg => "BRG",
        _ => division.ToString()
    };

    private static string AvailabilityLabel(AttendanceStatus status) => status switch
    {
        AttendanceStatus.Present => "YES",
        AttendanceStatus.Absent => "NO",
        AttendanceStatus.OnVacation => "VAC",
        _ => status.ToString()
    };
}
