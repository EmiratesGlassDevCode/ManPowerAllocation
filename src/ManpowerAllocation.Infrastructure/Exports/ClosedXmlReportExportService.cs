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
        WriteHeader(ws, "Category", "Department", "Day Shift Required", "Night Shift Required", "Total", "Sequence");

        var row = 2;
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

        ws.Columns().AdjustToContents();
        return ToFile(wb, "master_requirements");
    }

    /// <inheritdoc />
    public async Task<ExportFile> BuildStaffingReportAsync(CancellationToken cancellationToken = default)
    {
        var departments = await LoadDepartmentsAsync(cancellationToken);
        var employeesByDepartment = await LoadEmployeesByDepartmentAsync(cancellationToken);

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Report");
        WriteHeader(ws, "Division", "Department", "Day Req", "Night Req", "Total Req",
            "Present", "Outsource", "Total Present", "Absent", "Vacation", "Status");

        var row = 2;
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

        ws.Columns().AdjustToContents();
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
        WriteHeader(ws, "Name", "ID", "Division", "Department", "Shift", "Available", "Outsource", "Notes");

        var row = 2;
        foreach (var e in employees)
        {
            ws.Cell(row, 1).Value = e.Name;
            ws.Cell(row, 2).Value = e.BadgeNumber ?? string.Empty;
            ws.Cell(row, 3).Value = DivisionLabel(e.Division);
            ws.Cell(row, 4).Value = e.Department?.Name ?? string.Empty;
            ws.Cell(row, 5).Value = e.Shift.ToString().ToUpperInvariant();
            ws.Cell(row, 6).Value = AvailabilityLabel(e.Status);
            ws.Cell(row, 7).Value = e.IsSupply ? "YES" : string.Empty;
            ws.Cell(row, 8).Value = e.Notes ?? string.Empty;
            row++;
        }

        ws.Columns().AdjustToContents();
        return ToFile(wb, "attendance");
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

    private static void WriteHeader(IXLWorksheet ws, params string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
        }
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
