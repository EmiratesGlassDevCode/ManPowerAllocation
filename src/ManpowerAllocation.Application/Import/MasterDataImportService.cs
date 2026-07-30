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

    /// <inheritdoc />
    public async Task<ImportResult> ApplyAttendanceEditsAsync(Stream workbook, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        RequireAdmin();

        var rows = await _parser.ParseAttendanceEditsAsync(workbook, cancellationToken);

        var warnings = new List<string>();
        var updated = 0;

        await _dbContext.ExecuteInTransactionAsync(async ct =>
        {
            // Reset accumulators so a transient-failure retry does not double-count.
            updated = 0;
            warnings.Clear();

            // Resolve departments in-memory: (division, canonical name) -> department.
            var departments = await _dbContext.Departments.ToListAsync(ct);
            var departmentByKey = departments.ToDictionary(d => (d.Division, d.Name), d => d);

            foreach (var row in rows)
            {
                // Match the edited row back to an existing employee: Ref (the id column the export
                // now writes) is authoritative; a unique badge number is the fallback for older files.
                Employee? employee = null;

                if (row.Ref is int id)
                {
                    employee = await _dbContext.Employees.FirstOrDefaultAsync(e => e.Id == id, ct);
                    if (employee is null)
                    {
                        warnings.Add($"'{row.Name}' — Ref {id} matches no current employee; skipped.");
                        continue;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(row.BadgeNumber))
                {
                    var badge = row.BadgeNumber!.Trim();
                    var matches = await _dbContext.Employees.Where(e => e.BadgeNumber == badge).ToListAsync(ct);
                    if (matches.Count == 1)
                    {
                        employee = matches[0];
                    }
                    else if (matches.Count > 1)
                    {
                        warnings.Add($"'{row.Name}' — badge {badge} matches {matches.Count} employees; skipped (re-download for a Ref column).");
                        continue;
                    }
                }

                if (employee is null)
                {
                    warnings.Add($"'{row.Name}' — no matching employee found; skipped (add new staff via Add Employee).");
                    continue;
                }

                // Resolve the target department by name, preferring the employee's current division
                // then the row's Division column. A rename/move to an unknown department leaves the
                // department unchanged and warns, rather than guessing or creating one silently.
                Department? department = null;
                if (row.DepartmentName.Length > 0)
                {
                    if (departmentByKey.TryGetValue((employee.Division, row.DepartmentName), out var byCurrent))
                    {
                        department = byCurrent;
                    }
                    else if (departmentByKey.TryGetValue((row.Division, row.DepartmentName), out var byRow))
                    {
                        department = byRow;
                    }
                    else
                    {
                        warnings.Add($"'{row.Name}' — department '{row.DepartmentName}' not found; department left unchanged.");
                    }
                }

                if (department is not null)
                {
                    employee.DepartmentId = department.Id;
                    employee.Division = department.Division;
                }

                employee.Name = row.Name.Trim();
                // Only overwrite the badge when the edited row supplies one, so a blank cell never
                // wipes an existing badge (and thus its biometric link).
                if (!string.IsNullOrWhiteSpace(row.BadgeNumber))
                {
                    employee.BadgeNumber = row.BadgeNumber!.Trim();
                }
                employee.Shift = row.Shift;
                employee.Status = row.Status;
                employee.IsSupply = row.IsSupply;
                employee.Notes = row.Notes;
                updated++;
            }

            await _dbContext.SaveChangesAsync(ct);

            var summary = new { Kind = "AttendanceEditApply", EmployeesUpdated = updated, RowsRead = rows.Count, Skipped = warnings.Count };
            _auditWriter.Add(AuditAction.Update, "MasterDataImport", null, null, summary);
            await _dbContext.SaveChangesAsync(ct);
        }, cancellationToken);

        return new ImportResult
        {
            EmployeesUpdated = updated,
            Warnings = warnings
        };
    }

    /// <inheritdoc />
    public async Task<ResetResult> ResetOperationalDataAsync(CancellationToken cancellationToken = default)
    {
        RequireAdmin();

        var employeesRemoved = 0;
        var departmentsRemoved = 0;
        var snapshotsRemoved = 0;

        await _dbContext.ExecuteInTransactionAsync(async ct =>
        {
            // Reset accumulators so a transient-failure retry does not double-count.
            employeesRemoved = 0;
            departmentsRemoved = 0;
            snapshotsRemoved = 0;

            // Employees first (they reference departments through a restricted FK), then the now
            // unreferenced departments, then the snapshots (their per-department and per-employee
            // detail rows are removed by the database cascade). Roles, audit, shift settings and the
            // break-glass account are deliberately untouched. Load-and-RemoveRange keeps this in the
            // application layer's core-EF surface (set-based ExecuteDelete lives in the relational
            // assembly, which this project does not reference).
            var employees = await _dbContext.Employees.ToListAsync(ct);
            employeesRemoved = employees.Count;
            _dbContext.Employees.RemoveRange(employees);
            await _dbContext.SaveChangesAsync(ct);

            var departments = await _dbContext.Departments.ToListAsync(ct);
            departmentsRemoved = departments.Count;
            _dbContext.Departments.RemoveRange(departments);
            await _dbContext.SaveChangesAsync(ct);

            var snapshots = await _dbContext.AllocationSnapshots.ToListAsync(ct);
            snapshotsRemoved = snapshots.Count;
            _dbContext.AllocationSnapshots.RemoveRange(snapshots);
            await _dbContext.SaveChangesAsync(ct);

            var summary = new
            {
                Kind = "OperationalDataReset",
                EmployeesRemoved = employeesRemoved,
                DepartmentsRemoved = departmentsRemoved,
                SnapshotsRemoved = snapshotsRemoved
            };
            _auditWriter.Add(AuditAction.Delete, "MasterDataImport", null, null, summary);
            await _dbContext.SaveChangesAsync(ct);
        }, cancellationToken);

        return new ResetResult(employeesRemoved, departmentsRemoved, snapshotsRemoved);
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
