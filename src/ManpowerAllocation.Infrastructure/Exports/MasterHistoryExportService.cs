using ClosedXML.Excel;
using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Common;
using ManpowerAllocation.Application.Exports;
using ManpowerAllocation.Application.MasterHistory;
using ManpowerAllocation.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManpowerAllocation.Infrastructure.Exports;

/// <summary>Builds Excel/PDF files for a captured master snapshot (employees + department targets).</summary>
public sealed class MasterHistoryExportService : IMasterHistoryExportService
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    /// <summary>Initialises the service.</summary>
    public MasterHistoryExportService(IApplicationDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<ExportFile> ExportAsync(long snapshotId, string? format, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.HasAtLeast(UserRole.Admin))
        {
            throw new ForbiddenException("Exporting the master history requires the Admin role.");
        }

        var snapshot = await _dbContext.MasterSnapshots
            .AsNoTracking()
            .Include(s => s.Employees)
            .Include(s => s.Departments)
            .FirstOrDefaultAsync(s => s.Id == snapshotId, cancellationToken)
            ?? throw new NotFoundException("Master snapshot", snapshotId);

        var stamp = snapshot.CapturedAtUtc.ToString("yyyyMMdd_HHmm");
        var employees = snapshot.Employees.OrderBy(e => e.Division).ThenBy(e => e.HomeDepartmentName).ThenBy(e => e.Name).ToList();
        var departments = snapshot.Departments.OrderBy(d => d.Division).ThenBy(d => d.DepartmentName).ToList();

        var fmt = format?.ToLowerInvariant();
        return fmt is "xlsx" or "excel"
            ? BuildExcel(snapshot, employees, departments, stamp)
            : BuildPdf(snapshot, employees, departments, stamp);
    }

    private static ExportFile BuildExcel(
        Domain.Entities.MasterSnapshot snapshot,
        IReadOnlyList<Domain.Entities.MasterSnapshotEmployee> employees,
        IReadOnlyList<Domain.Entities.MasterSnapshotDepartment> departments,
        string stamp)
    {
        using var wb = new XLWorkbook();

        var ws = wb.AddWorksheet("Employees");
        var title = ws.Cell(1, 1);
        title.Value = $"Master data — {snapshot.CapturedAtUtc:dd MMM yyyy HH:mm} UTC ({snapshot.Source})";
        title.Style.Font.Bold = true;
        title.Style.Font.FontColor = XLColor.FromHtml("#12446B");
        ws.Range(1, 1, 1, 6).Merge();

        var headers = new[] { "Employee", "Badge", "Division", "Home department", "Shift", "Outsource" };
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(3, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#12446B");
        }

        var row = 4;
        foreach (var e in employees)
        {
            ws.Cell(row, 1).Value = e.Name;
            ws.Cell(row, 2).Value = e.BadgeNumber ?? string.Empty;
            ws.Cell(row, 3).Value = DivisionLabel(e.Division);
            ws.Cell(row, 4).Value = e.HomeDepartmentName;
            ws.Cell(row, 5).Value = e.Shift.ToString();
            ws.Cell(row, 6).Value = e.IsSupply ? "YES" : string.Empty;
            row++;
        }
        ws.Columns().AdjustToContents(1, 60);

        var ds = wb.AddWorksheet("Departments");
        var dHeaders = new[] { "Division", "Department", "Required day", "Required night", "Pool" };
        for (var i = 0; i < dHeaders.Length; i++)
        {
            var cell = ds.Cell(1, i + 1);
            cell.Value = dHeaders[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#12446B");
        }
        var dr = 2;
        foreach (var d in departments)
        {
            ds.Cell(dr, 1).Value = DivisionLabel(d.Division);
            ds.Cell(dr, 2).Value = d.DepartmentName;
            ds.Cell(dr, 3).Value = d.RequiredDay;
            ds.Cell(dr, 4).Value = d.RequiredNight;
            ds.Cell(dr, 5).Value = d.IsPool ? "YES" : string.Empty;
            dr++;
        }
        ds.Columns().AdjustToContents(1, 60);

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return new ExportFile($"master_data_{stamp}.xlsx", XlsxContentType, stream.ToArray());
    }

    private static ExportFile BuildPdf(
        Domain.Entities.MasterSnapshot snapshot,
        IReadOnlyList<Domain.Entities.MasterSnapshotEmployee> employees,
        IReadOnlyList<Domain.Entities.MasterSnapshotDepartment> departments,
        string stamp)
    {
        var kpis = new List<PdfAnalyticsReport.Kpi>
        {
            new("Employees", snapshot.EmployeeCount.ToString()),
            new("Departments", snapshot.DepartmentCount.ToString())
        };

        var sections = new List<PdfAnalyticsReport.Section>
        {
            new("Employees (home allocation)",
                new[] { "Employee", "Badge", "Division", "Home department", "Shift", "OS" },
                employees.Select(e => new[]
                {
                    e.Name, e.BadgeNumber ?? "—", DivisionLabel(e.Division), e.HomeDepartmentName, e.Shift.ToString(), e.IsSupply ? "YES" : "—"
                }).ToList()),
            new("Department targets",
                new[] { "Division", "Department", "Req day", "Req night", "Pool" },
                departments.Select(d => new[]
                {
                    DivisionLabel(d.Division), d.DepartmentName, d.RequiredDay.ToString(), d.RequiredNight.ToString(), d.IsPool ? "YES" : "—"
                }).ToList())
        };

        var pdf = PdfAnalyticsReport.Render(
            EmbeddedPdfFonts.ReadEmbedded("emirates-glass-logo.png"),
            "Master Data Snapshot",
            $"{snapshot.CapturedAtUtc:dd MMM yyyy HH:mm} UTC · {snapshot.Source}",
            kpis, sections);

        return new ExportFile($"master_data_{stamp}.pdf", "application/pdf", pdf);
    }

    private static string DivisionLabel(Division division) => division switch
    {
        Division.Egl => "EGL",
        Division.FunctionalSupport => "Functional Support",
        Division.Brg => "BRG",
        _ => division.ToString()
    };
}
