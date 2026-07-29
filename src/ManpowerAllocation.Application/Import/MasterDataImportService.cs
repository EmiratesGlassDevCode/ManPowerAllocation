using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Common;
using ManpowerAllocation.Domain;
using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManpowerAllocation.Application.Import;

/// <summary>
/// Default implementation of <see cref="IMasterDataImportService"/>. Parsing is delegated to
/// <see cref="IExcelImportParser"/>; this service applies the business rules and persistence.
/// A bulk seeding import is recorded in the audit trail as a single, clearly-described event
/// (with per-division counts) rather than thousands of per-row entries.
/// </summary>
public sealed class MasterDataImportService : IMasterDataImportService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IExcelImportParser _parser;
    private readonly IAuditWriter _auditWriter;
    private readonly ICurrentUser _currentUser;

    /// <summary>Initialises the service.</summary>
    /// <param name="dbContext">The application persistence context.</param>
    /// <param name="parser">Parser that reads rows from the uploaded workbook.</param>
    /// <param name="auditWriter">Writer used to record the import in the audit trail.</param>
    /// <param name="currentUser">The current principal, used for a defence-in-depth Admin check.</param>
    public MasterDataImportService(IApplicationDbContext dbContext, IExcelImportParser parser, IAuditWriter auditWriter, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _parser = parser;
        _auditWriter = auditWriter;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<ImportResult> ImportAttendanceAsync(Stream workbook, bool replaceExisting, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        RequireAdmin();

        var rows = await _parser.ParseAttendanceAsync(workbook, cancellationToken);

        var warnings = new List<string>();
        var departmentsCreated = 0;
        var employeesImported = 0;
        var employeesRemoved = 0;

        await _dbContext.ExecuteInTransactionAsync(async ct =>
        {
            // Reset accumulators so a transient-failure retry (the execution strategy re-invokes
            // this delegate) does not double-count.
            departmentsCreated = 0;
            employeesImported = 0;
            employeesRemoved = 0;
            warnings.Clear();

            foreach (var divisionGroup in rows.GroupBy(r => r.Division))
            {
                var division = divisionGroup.Key;

                var divisionHasEmployees = await _dbContext.Employees
                    .AnyAsync(e => e.Division == division, ct);

                if (divisionHasEmployees && !replaceExisting)
                {
                    warnings.Add($"{division} already contains employees and was left unchanged. Enable replace to re-seed it.");
                    continue;
                }

                if (divisionHasEmployees)
                {
                    var existing = await _dbContext.Employees.Where(e => e.Division == division).ToListAsync(ct);
                    employeesRemoved += existing.Count;
                    _dbContext.Employees.RemoveRange(existing);
                    await _dbContext.SaveChangesAsync(ct);
                }

                // Ensure every referenced department exists before inserting employees.
                var departmentIdByName = await EnsureDepartmentsAsync(division, divisionGroup, ct, () => departmentsCreated++);

                foreach (var row in divisionGroup)
                {
                    var name = DepartmentName.Normalize(row.DepartmentName);
                    if (!departmentIdByName.TryGetValue(name, out var departmentId))
                    {
                        warnings.Add($"Skipped '{row.Name}' — department '{name}' could not be resolved.");
                        continue;
                    }

                    _dbContext.Employees.Add(new Employee
                    {
                        Name = row.Name.Trim(),
                        BadgeNumber = string.IsNullOrWhiteSpace(row.BadgeNumber) ? null : row.BadgeNumber.Trim(),
                        Division = division,
                        DepartmentId = departmentId,
                        Shift = row.Shift,
                        Status = row.Status,
                        IsSupply = row.IsSupply,
                        Notes = string.IsNullOrWhiteSpace(row.Notes) ? null : row.Notes.Trim()
                    });
                    employeesImported++;
                }
            }

            await _dbContext.SaveChangesAsync(ct);

            var summary = new
            {
                Kind = "AttendanceImport",
                DepartmentsCreated = departmentsCreated,
                EmployeesImported = employeesImported,
                EmployeesRemoved = employeesRemoved,
                Replaced = replaceExisting
            };
            _auditWriter.Add(AuditAction.Create, "MasterDataImport", null, null, summary);
            await _dbContext.SaveChangesAsync(ct);
        }, cancellationToken);

        return new ImportResult
        {
            DepartmentsCreated = departmentsCreated,
            EmployeesImported = employeesImported,
            EmployeesRemoved = employeesRemoved,
            Warnings = warnings
        };
    }

    /// <inheritdoc />
    public async Task<ImportResult> ImportRequirementsAsync(Stream workbook, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        RequireAdmin();

        var rows = await _parser.ParseRequirementsAsync(workbook, cancellationToken);

        // Collapse duplicate rows for the same department (common in hand-maintained sheets)
        // to a single last-wins entry. Without this, two rows for the same (Division, Name)
        // would both miss the DB lookup and both insert, violating the unique index and
        // aborting the entire import.
        var deduped = rows
            .Where(r => DepartmentName.Normalize(r.DepartmentName).Length > 0)
            .GroupBy(r => (r.Division, Name: DepartmentName.Normalize(r.DepartmentName)))
            .Select(g => g.Last())
            .ToList();

        var warnings = new List<string>();
        var created = 0;
        var updated = 0;

        await _dbContext.ExecuteInTransactionAsync(async ct =>
        {
            // Reset accumulators so a transient-failure retry (the execution strategy re-invokes
            // this delegate) does not double-count.
            created = 0;
            updated = 0;
            warnings.Clear();

            foreach (var row in deduped)
            {
                var name = DepartmentName.Normalize(row.DepartmentName);

                var department = await _dbContext.Departments
                    .FirstOrDefaultAsync(d => d.Division == row.Division && d.Name == name, ct);

                if (department is null)
                {
                    _dbContext.Departments.Add(new Department
                    {
                        Division = row.Division,
                        Name = name,
                        RequiredDay = row.RequiredDay,
                        RequiredNight = row.RequiredNight,
                        Sequence = 9999m,
                        IsActive = true
                    });
                    created++;
                }
                else
                {
                    department.RequiredDay = row.RequiredDay;
                    department.RequiredNight = row.RequiredNight;
                    updated++;
                }
            }

            await _dbContext.SaveChangesAsync(ct);

            var summary = new { Kind = "RequirementsImport", DepartmentsCreated = created, DepartmentsUpdated = updated };
            _auditWriter.Add(AuditAction.Update, "MasterDataImport", null, null, summary);
            await _dbContext.SaveChangesAsync(ct);
        }, cancellationToken);

        return new ImportResult
        {
            DepartmentsCreated = created,
            DepartmentsUpdated = updated,
            Warnings = warnings
        };
    }

    /// <summary>
    /// Defence-in-depth authorisation: master-data import is destructive and Admin-only. The
    /// primary enforcement is the endpoint/page authorization policy; this second check ensures
    /// the rule holds even if the service is ever reached another way.
    /// </summary>
    private void RequireAdmin()
    {
        if (!_currentUser.HasAtLeast(UserRole.Admin))
        {
            throw new ForbiddenException("Master-data import requires the Admin role.");
        }
    }

    /// <summary>
    /// Ensures a department row exists for every distinct department name in the group, creating
    /// any that are missing, and returns a map of normalised name to department id.
    /// </summary>
    private async Task<Dictionary<string, int>> EnsureDepartmentsAsync(
        Division division,
        IEnumerable<Models.ImportedEmployeeRow> rows,
        CancellationToken cancellationToken,
        Action onCreated)
    {
        var existing = await _dbContext.Departments
            .Where(d => d.Division == division)
            .ToDictionaryAsync(d => d.Name, d => d.Id, cancellationToken);

        var distinctNames = rows
            .Select(r => DepartmentName.Normalize(r.DepartmentName))
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.Ordinal);

        var toCreate = new List<Department>();
        foreach (var name in distinctNames)
        {
            if (!existing.ContainsKey(name))
            {
                toCreate.Add(new Department
                {
                    Division = division,
                    Name = name,
                    RequiredDay = 0,
                    RequiredNight = 0,
                    Sequence = 9999m,
                    IsActive = true
                });
            }
        }

        if (toCreate.Count > 0)
        {
            _dbContext.Departments.AddRange(toCreate);
            await _dbContext.SaveChangesAsync(cancellationToken);
            foreach (var department in toCreate)
            {
                existing[department.Name] = department.Id;
                onCreated();
            }
        }

        return existing;
    }
}
