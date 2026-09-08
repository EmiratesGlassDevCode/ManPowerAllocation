using ClosedXML.Excel;
using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Dashboard;
using ManpowerAllocation.Application.Exports;
using ManpowerAllocation.Application.Reconciliation;
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
        // Present / Absent / Vacation are reported PER SHIFT, not pooled across day and night: an
        // employee works one shift only, so summing both shifts' statuses would count the shift that
        // isn't running as "absent" and overstate absenteeism. Each department therefore gets one row
        // per shift, each measured against that shift's own requirement.
        var row = WriteBrandedHeader(ws, "Staffing Report",
            "Division", "Department", "Shift", "Required",
            "Present", "Outsource", "Total Present", "Absent", "Vacation", "Status");

        foreach (var d in departments)
        {
            var members = employeesByDepartment.TryGetValue(d.Id, out var list) ? list : new List<Employee>();

            foreach (var (shiftFilter, shiftLabel) in new[] { (ShiftFilter.Day, "DAY"), (ShiftFilter.Night, "NIGHT") })
            {
                var stats = StaffingCalculator.ComputeDepartmentStats(d, members, shiftFilter);

                ws.Cell(row, 1).Value = DivisionLabel(d.Division);
                ws.Cell(row, 2).Value = d.Name;
                ws.Cell(row, 3).Value = shiftLabel;
                ws.Cell(row, 4).Value = stats.Required;
                ws.Cell(row, 5).Value = stats.Present;
                ws.Cell(row, 6).Value = stats.SupplyPresent;
                ws.Cell(row, 7).Value = stats.TotalPresent;
                ws.Cell(row, 8).Value = stats.Absent;
                ws.Cell(row, 9).Value = stats.OnVacation;
                ws.Cell(row, 10).Value = stats.Status.ToString();
                row++;
            }
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

        // The full list of valid department names (including empty departments), used to build the
        // in-cell dropdown so an offline editor can only pick an existing department, never type a new one.
        var departmentNames = await _dbContext.Departments
            .AsNoTracking()
            .Select(d => d.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(cancellationToken);

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Attendance");
        // "Ref" (the employee id) is the stable key the non-destructive edit-apply upload uses to
        // match an edited row back to the exact employee. Leave it intact when editing offline.
        var firstDataRow = WriteBrandedHeader(ws, "Attendance",
            "Ref", "Name", "ID", "Division", "Department", "Shift", "Available", "Outsource", "Notes");
        var row = firstDataRow;

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

        // Lock the controlled-vocabulary columns to a dropdown so an edited sheet can never introduce
        // a new Division / Department / Shift value (which the import would otherwise reject or skip).
        // Department resolution keys on (Division, Name), so Division is guarded alongside Department.
        var lastDataRow = row - 1;
        if (lastDataRow >= firstDataRow)
        {
            SetDropdown(ws.Range(firstDataRow, 4, lastDataRow, 4), "\"EGL,Functional Support,BRG\"",
                "Division", "Pick a division from the list.");

            if (departmentNames.Count > 0)
            {
                // Department names go on a hidden sheet: the inline-list form is capped at 255
                // characters and cannot hold a long or comma-bearing list, so a range reference is used.
                var lists = wb.AddWorksheet("Lists");
                for (var i = 0; i < departmentNames.Count; i++)
                {
                    lists.Cell(i + 1, 1).Value = departmentNames[i];
                }
                lists.Hide();

                var deptRange = lists.Range(1, 1, departmentNames.Count, 1);
                SetDropdown(ws.Range(firstDataRow, 5, lastDataRow, 5), deptRange,
                    "Department", "Pick a department from the list.");
            }

            SetDropdown(ws.Range(firstDataRow, 6, lastDataRow, 6), "\"DAY,NIGHT\"",
                "Shift", "Pick DAY or NIGHT.");
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

        var absentees = await BuildAbsenteeRowsAsync(snapshot, cancellationToken);

        var deptStatuses = snapshot.Departments
            .OrderBy(d => d.Division)
            .ThenBy(d => d.DepartmentName)
            .Select(d => new PdfDailyReport.DeptStatusRow(DivisionLabel(d.Division), d.DepartmentName, d.IsActive))
            .ToList();

        var model = new PdfDailyReport.Model(
            snapshot.OperationalDate, snapshot.Shift.ToString(), snapshot.CapturedAtUtc,
            snapshot.Required, snapshot.OnRoll, snapshot.Present, snapshot.Absent, snapshot.OnVacation,
            snapshot.SupplyPresent, snapshot.TotalPresent, snapshot.Variance, snapshot.ShortageDepartmentCount,
            divisions, deptRows, absentees, deptStatuses);

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

        // Absentees & reasons section below the department table.
        var absentees = await BuildAbsenteeRowsAsync(snapshot, cancellationToken);
        row += 2;
        var absTitle = ws.Cell(row, 1);
        absTitle.Value = "Absentees & Reasons";
        absTitle.Style.Font.Bold = true;
        absTitle.Style.Font.FontColor = XLColor.FromHtml("#12446B");
        ws.Range(row, 1, row, 6).Merge();
        row++;

        var absHeaders = new[] { "Employee", "Badge", "Division", "Department", "Status", "Reason", "Category", "Detail" };
        for (var i = 0; i < absHeaders.Length; i++)
        {
            var cell = ws.Cell(row, i + 1);
            cell.Value = absHeaders[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#12446B");
        }
        row++;

        if (absentees.Count == 0)
        {
            ws.Cell(row, 1).Value = "No absentees recorded for this shift.";
            ws.Range(row, 1, row, 8).Merge();
            row++;
        }
        else
        {
            foreach (var a in absentees)
            {
                ws.Cell(row, 1).Value = a.Name;
                ws.Cell(row, 2).Value = a.Badge ?? string.Empty;
                ws.Cell(row, 3).Value = a.Division;
                ws.Cell(row, 4).Value = a.Department;
                ws.Cell(row, 5).Value = a.StatusLabel;
                ws.Cell(row, 6).Value = a.ReasonKind;
                ws.Cell(row, 7).Value = a.ReasonCategory;
                ws.Cell(row, 8).Value = a.Detail;
                row++;
            }
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
            .Include(s => s.Employees)
            .FirstOrDefaultAsync(s => s.Id == snapshotId, cancellationToken);

        return snapshot ?? throw new Application.Common.NotFoundException("Daily report", snapshotId);
    }

    /// <summary>
    /// Builds the absentees section for a captured snapshot: every employee who was Absent or On
    /// vacation at capture time, with the reason recorded for the snapshot's operational date (if any).
    /// </summary>
    private async Task<List<PdfDailyReport.AbsenteeRow>> BuildAbsenteeRowsAsync(AllocationSnapshot snapshot, CancellationToken cancellationToken)
    {
        var absentEmployees = snapshot.Employees
            .Where(e => e.Status is AttendanceStatus.Absent or AttendanceStatus.OnVacation)
            .ToList();

        if (absentEmployees.Count == 0)
        {
            return new List<PdfDailyReport.AbsenteeRow>();
        }

        var opDate = DateOnly.FromDateTime(snapshot.OperationalDate);
        var employeeIds = absentEmployees.Select(e => e.EmployeeId).Distinct().ToList();

        // Reason records active on the snapshot's operational date, most-recent-per-employee.
        var reasons = await _dbContext.EmployeeAbsences
            .AsNoTracking()
            .Where(a => employeeIds.Contains(a.EmployeeId)
                && a.FromDate <= opDate && (a.ToDate == null || opDate <= a.ToDate))
            .Select(a => new
            {
                a.EmployeeId,
                a.Kind,
                CategoryName = a.Category!.Name,
                a.FromDate,
                a.ToDate,
                a.Comment,
                a.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var reasonByEmployee = reasons
            .GroupBy(r => r.EmployeeId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.CreatedAtUtc).First());

        return absentEmployees
            .OrderBy(e => e.Division)
            .ThenBy(e => e.DepartmentName)
            .ThenBy(e => e.Name)
            .Select(e =>
            {
                var statusLabel = e.Status == AttendanceStatus.OnVacation ? "On vacation" : "Absent";
                if (reasonByEmployee.TryGetValue(e.EmployeeId, out var r))
                {
                    var kind = Application.Absences.AbsenceKindText.Label(r.Kind);
                    string detail;
                    if (Application.Absences.AbsenceKindText.HasDateRange(r.Kind))
                    {
                        var to = r.ToDate?.ToString("dd MMM") ?? "—";
                        detail = $"{r.FromDate:dd MMM} – {to}";
                        if (!string.IsNullOrWhiteSpace(r.Comment)) { detail += $" · {r.Comment}"; }
                    }
                    else
                    {
                        detail = string.IsNullOrWhiteSpace(r.Comment) ? "—" : r.Comment!;
                    }

                    return new PdfDailyReport.AbsenteeRow(
                        e.Name, e.BadgeNumber, DivisionLabel(e.Division), e.DepartmentName, statusLabel, kind, r.CategoryName, detail);
                }

                // No recorded reason: for a vacation this is self-explanatory; otherwise it's unexplained.
                var fallbackKind = e.Status == AttendanceStatus.OnVacation ? "—" : "Not recorded";
                return new PdfDailyReport.AbsenteeRow(
                    e.Name, e.BadgeNumber, DivisionLabel(e.Division), e.DepartmentName, statusLabel, fallbackKind, "—", "—");
            })
            .ToList();
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

    /// <inheritdoc />
    public Task<ExportFile> BuildReconciliationExcelAsync(ReconciliationReport report, CancellationToken cancellationToken = default)
    {
        using var wb = new XLWorkbook();

        // Sheet 1 — verification summary: what the view returned vs what the roster holds.
        var summary = wb.AddWorksheet("Verification");
        var srow = WriteBrandedHeader(summary, "Biometric Reconciliation — Verification", "Metric", "Value");
        // XLCellValue has implicit conversions from string and int, so callers pass either directly.
        void Metric(string label, XLCellValue value)
        {
            summary.Cell(srow, 1).Value = label;
            summary.Cell(srow, 2).Value = value;
            srow++;
        }

        Metric("Attendance source configured", report.SourceConfigured ? "Yes" : "No");
        Metric("Distinct biometric IDs pulled", report.BiometricIdCount);
        Metric("Total punches behind those IDs", report.BiometricPunchCount);
        Metric("Checked in — current shift", report.CurrentShiftCheckedIn);
        Metric("Checked in — previous shift", report.PreviousShiftCheckedIn);
        Metric("Most recent punch", report.MostRecentPunch?.ToString("yyyy-MM-dd HH:mm") ?? "—");
        Metric("Matched to a roster badge", report.MatchedIdCount);
        Metric("Unmatched biometric IDs", report.UnmatchedIdCount);
        Metric("Employees on roster", report.RosterEmployeeCount);
        Metric("— of which outsource/supply", report.RosterSupplyCount);
        Metric("Distinct roster badges", report.RosterBadgeCount);
        Metric("Roster marked Present", report.RosterPresent);
        Metric("Roster marked Absent", report.RosterAbsent);
        Metric("Roster marked On Vacation", report.RosterOnVacation);
        Metric("Employees with no badge (unmatchable)", report.EmployeesWithoutBadgeCount);
        Metric("Status counts balance to roster", report.StatusCountsBalance ? "Yes" : "No");
        summary.Columns().AdjustToContents(1, 60);

        // Sheet 2 — unmatched biometric identifiers (punches with no employee).
        var unmatched = wb.AddWorksheet("Unmatched IDs");
        var urow = WriteBrandedHeader(unmatched, "Unmatched Biometric IDs",
            "Biometric ID", "Shift Label", "Last Seen", "Punches");
        foreach (var u in report.UnmatchedBiometricIds)
        {
            unmatched.Cell(urow, 1).Value = u.BiometricId;
            unmatched.Cell(urow, 2).Value = u.ShiftLabel ?? string.Empty;
            unmatched.Cell(urow, 3).Value = u.LastSeen?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty;
            unmatched.Cell(urow, 4).Value = u.PunchCount;
            urow++;
        }
        unmatched.Columns().AdjustToContents(1, 60);

        // Sheet 3 — employees with no badge (can never be matched).
        var noBadge = wb.AddWorksheet("No Badge");
        var nrow = WriteBrandedHeader(noBadge, "Employees Without a Badge",
            "Ref", "Name", "Division", "Department", "Shift");
        foreach (var e in report.EmployeesWithoutBadge)
        {
            noBadge.Cell(nrow, 1).Value = e.EmployeeId;
            noBadge.Cell(nrow, 2).Value = e.Name;
            noBadge.Cell(nrow, 3).Value = DivisionLabel(e.Division);
            noBadge.Cell(nrow, 4).Value = e.DepartmentName;
            noBadge.Cell(nrow, 5).Value = e.Shift.ToString().ToUpperInvariant();
            nrow++;
        }
        noBadge.Columns().AdjustToContents(1, 60);

        return Task.FromResult(ToFile(wb, "biometric_reconciliation"));
    }

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
    /// <summary>
    /// Applies a "Stop"-style list data-validation (in-cell dropdown) to a range from a short inline
    /// list such as <c>"DAY,NIGHT"</c>. A rejected entry cannot be typed into the cell.
    /// </summary>
    private static void SetDropdown(IXLRange range, string inlineList, string title, string message)
    {
        var dv = range.CreateDataValidation();
        dv.List(inlineList, inCellDropdown: true);
        ConfigureDropdown(dv, title, message);
    }

    /// <summary>
    /// Applies a "Stop"-style list data-validation (in-cell dropdown) to a range, sourcing the allowed
    /// values from another range (used for the department list, which can exceed the inline 255-char cap).
    /// </summary>
    private static void SetDropdown(IXLRange range, IXLRange source, string title, string message)
    {
        var dv = range.CreateDataValidation();
        dv.List(source, inCellDropdown: true);
        ConfigureDropdown(dv, title, message);
    }

    /// <summary>Shared validation settings: blanks allowed, hard stop on an off-list value.</summary>
    private static void ConfigureDropdown(IXLDataValidation dv, string title, string message)
    {
        dv.IgnoreBlanks = true;
        dv.ErrorStyle = XLErrorStyle.Stop;
        dv.ErrorTitle = title;
        dv.ErrorMessage = message;
    }

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
