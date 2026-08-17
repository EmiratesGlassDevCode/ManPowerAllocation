using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Common;
using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManpowerAllocation.Application.Absences;

/// <summary>
/// Default <see cref="IAbsenceService"/>. An Informed absence is "active" only while today falls
/// within its From/To window, so once the period ends the reason simply stops matching and the
/// employee drops off the current list — the record itself is retained for history and audit.
/// </summary>
public sealed class AbsenceService : IAbsenceService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAuditWriter _auditWriter;
    private readonly IFactoryClock _factoryClock;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    /// <summary>Initialises the service.</summary>
    public AbsenceService(
        IApplicationDbContext dbContext,
        IAuditWriter auditWriter,
        IFactoryClock factoryClock,
        IClock clock,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _auditWriter = auditWriter;
        _factoryClock = factoryClock;
        _clock = clock;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AbsenceListItemDto>> GetAbsenteesAsync(Division? division, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(_factoryClock.LocalNow);

        // Active reasons (either kind): today within [FromDate, ToDate]; a null ToDate is open-ended.
        var active = await _dbContext.EmployeeAbsences
            .AsNoTracking()
            .Where(a => a.FromDate <= today && (a.ToDate == null || today <= a.ToDate))
            .Select(a => new ActiveReason(
                a.Id, a.EmployeeId, a.CategoryId, a.Category!.Name, a.Kind,
                a.FromDate, a.ToDate, a.Comment, a.CreatedByName, a.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        // Most recent active reason per employee (there is normally only one).
        var reasonByEmployee = active
            .GroupBy(a => a.EmployeeId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.CreatedAtUtc).First());

        var reasonEmployeeIds = reasonByEmployee.Keys.ToList();

        // Show everyone currently marked Absent or On vacation (i.e. not present), plus anyone on an
        // active (e.g. Informed) reason even if the sync has not yet flipped their status.
        var employeesQuery = _dbContext.Employees
            .AsNoTracking()
            .Where(e => e.Status == AttendanceStatus.Absent
                || e.Status == AttendanceStatus.OnVacation
                || reasonEmployeeIds.Contains(e.Id));

        if (division is { } d)
        {
            employeesQuery = employeesQuery.Where(e => e.Division == d);
        }

        var employees = await employeesQuery
            .Select(e => new
            {
                e.Id,
                e.Name,
                e.BadgeNumber,
                e.DepartmentId,
                DepartmentName = e.Department!.Name,
                e.Division,
                e.Shift,
                e.Status,
                e.IsSupply
            })
            .ToListAsync(cancellationToken);

        return employees
            .OrderBy(e => e.Division)
            .ThenBy(e => e.DepartmentName)
            .ThenBy(e => e.Name)
            .Select(e => new AbsenceListItemDto(
                e.Id, e.Name, e.BadgeNumber, e.DepartmentId, e.DepartmentName, e.Division, e.Shift, e.Status, e.IsSupply,
                reasonByEmployee.TryGetValue(e.Id, out var r)
                    ? new AbsenceReasonView(r.RecordId, r.CategoryId, r.CategoryName, r.Kind, r.FromDate, r.ToDate, r.Comment, r.SetByName, r.CreatedAtUtc)
                    : null))
            .ToList();
    }

    /// <inheritdoc />
    public async Task SetReasonAsync(SetAbsenceReasonRequest request, CancellationToken cancellationToken = default)
    {
        var employee = await _dbContext.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), request.EmployeeId);

        await RequireDepartmentEditAsync(employee.DepartmentId, cancellationToken);

        var category = await _dbContext.AbsenceReasonCategories.FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken)
            ?? throw new NotFoundException(nameof(AbsenceReasonCategory), request.CategoryId);

        if (!category.IsActive)
        {
            throw new BusinessRuleException("The chosen reason category is no longer available.");
        }

        var today = DateOnly.FromDateTime(_factoryClock.LocalNow);
        DateOnly fromDate;
        DateOnly? toDate;

        if (category.Kind == AbsenceKind.Informed)
        {
            fromDate = request.FromDate ?? throw new BusinessRuleException("An Informed absence requires a From date.");
            toDate = request.ToDate ?? throw new BusinessRuleException("An Informed absence requires a To date.");
            if (toDate < fromDate)
            {
                throw new BusinessRuleException("The To date must be on or after the From date.");
            }
        }
        else
        {
            // Not-Informed: comment only, applies from today with no end until cleared or the person returns.
            fromDate = today;
            toDate = null;
        }

        var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();

        // Replace the employee's current or still-upcoming reason, if any; otherwise add a new one.
        var existing = await _dbContext.EmployeeAbsences
            .Where(a => a.EmployeeId == employee.Id && (a.ToDate == null || a.ToDate >= today))
            .OrderByDescending(a => a.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var now = _clock.UtcNow;

        if (existing is not null)
        {
            var before = new { existing.CategoryId, existing.Kind, existing.FromDate, existing.ToDate, existing.Comment };

            existing.CategoryId = category.Id;
            existing.Kind = category.Kind;
            existing.FromDate = fromDate;
            existing.ToDate = toDate;
            existing.Comment = comment;
            existing.CreatedByObjectId = _currentUser.UserId;
            existing.CreatedByName = _currentUser.DisplayName;
            existing.CreatedAtUtc = now;

            _auditWriter.Add(AuditAction.Update, nameof(EmployeeAbsence), existing.Id.ToString(), before,
                new { existing.CategoryId, existing.Kind, existing.FromDate, existing.ToDate, existing.Comment });
        }
        else
        {
            var record = new EmployeeAbsence
            {
                EmployeeId = employee.Id,
                CategoryId = category.Id,
                Kind = category.Kind,
                FromDate = fromDate,
                ToDate = toDate,
                Comment = comment,
                CreatedByObjectId = _currentUser.UserId,
                CreatedByName = _currentUser.DisplayName,
                CreatedAtUtc = now
            };

            _dbContext.EmployeeAbsences.Add(record);
            _auditWriter.Add(AuditAction.Create, nameof(EmployeeAbsence), null,
                new { EmployeeId = employee.Id }, new { record.CategoryId, record.Kind, record.FromDate, record.ToDate, record.Comment });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task ClearReasonAsync(int employeeId, CancellationToken cancellationToken = default)
    {
        var employee = await _dbContext.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), employeeId);

        await RequireDepartmentEditAsync(employee.DepartmentId, cancellationToken);

        var today = DateOnly.FromDateTime(_factoryClock.LocalNow);
        var records = await _dbContext.EmployeeAbsences
            .Where(a => a.EmployeeId == employeeId && (a.ToDate == null || a.ToDate >= today))
            .ToListAsync(cancellationToken);

        if (records.Count == 0)
        {
            return;
        }

        foreach (var record in records)
        {
            _auditWriter.Add(AuditAction.Delete, nameof(EmployeeAbsence), record.Id.ToString(),
                new { record.EmployeeId, record.CategoryId, record.Kind, record.FromDate, record.ToDate, record.Comment }, null);
        }

        _dbContext.EmployeeAbsences.RemoveRange(records);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Authorises editing an absence for a department: a User/Admin may edit any department; a
    /// department head may edit only the departments assigned to them. Enforced server-side.
    /// </summary>
    private async Task RequireDepartmentEditAsync(int departmentId, CancellationToken cancellationToken)
    {
        if (_currentUser.HasAtLeast(UserRole.User)
            || await DepartmentScope.IsHeadOfAsync(_dbContext, _currentUser, departmentId, cancellationToken))
        {
            return;
        }

        throw new ForbiddenException();
    }

    /// <summary>Flat projection of an active absence reason used to build the list.</summary>
    private sealed record ActiveReason(
        int RecordId, int EmployeeId, int CategoryId, string CategoryName, AbsenceKind Kind,
        DateOnly FromDate, DateOnly? ToDate, string? Comment, string? SetByName, DateTime CreatedAtUtc);
}
