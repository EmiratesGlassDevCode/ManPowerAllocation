using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Common;
using ManpowerAllocation.Domain.Entities;
using ManpowerAllocation.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManpowerAllocation.Application.Absences;

/// <summary>Default <see cref="IAbsenceCategoryService"/>. Admin-only mutations, all audited.</summary>
public sealed class AbsenceCategoryService : IAbsenceCategoryService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAuditWriter _auditWriter;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    /// <summary>Initialises the service.</summary>
    public AbsenceCategoryService(IApplicationDbContext dbContext, IAuditWriter auditWriter, IClock clock, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _auditWriter = auditWriter;
        _clock = clock;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AbsenceCategoryDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.AbsenceReasonCategories.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .OrderBy(c => c.Kind)
            .ThenBy(c => c.Sequence)
            .ThenBy(c => c.Name)
            .Select(c => new AbsenceCategoryDto(c.Id, c.Kind, c.Name, c.Sequence, c.IsActive))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AbsenceCategoryDto> CreateAsync(CreateAbsenceCategoryRequest request, CancellationToken cancellationToken = default)
    {
        Require(UserRole.Admin);

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            throw new BusinessRuleException("A category name is required.");
        }

        var duplicate = await _dbContext.AbsenceReasonCategories
            .AnyAsync(c => c.Kind == request.Kind && c.Name == name, cancellationToken);
        if (duplicate)
        {
            throw new BusinessRuleException($"A '{request.Kind}' category named '{name}' already exists.");
        }

        var nextSequence = await _dbContext.AbsenceReasonCategories
            .Where(c => c.Kind == request.Kind)
            .Select(c => (int?)c.Sequence)
            .MaxAsync(cancellationToken) ?? 0;

        var category = new AbsenceReasonCategory
        {
            Kind = request.Kind,
            Name = name,
            Sequence = nextSequence + 1,
            IsActive = true,
            CreatedAtUtc = _clock.UtcNow
        };

        _dbContext.AbsenceReasonCategories.Add(category);
        _auditWriter.Add(AuditAction.Create, nameof(AbsenceReasonCategory), null, null,
            new { category.Kind, category.Name, category.Sequence });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AbsenceCategoryDto(category.Id, category.Kind, category.Name, category.Sequence, category.IsActive);
    }

    /// <inheritdoc />
    public async Task<AbsenceCategoryDto> UpdateAsync(int id, UpdateAbsenceCategoryRequest request, CancellationToken cancellationToken = default)
    {
        Require(UserRole.Admin);

        var category = await _dbContext.AbsenceReasonCategories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(AbsenceReasonCategory), id);

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            throw new BusinessRuleException("A category name is required.");
        }

        var duplicate = await _dbContext.AbsenceReasonCategories
            .AnyAsync(c => c.Id != id && c.Kind == category.Kind && c.Name == name, cancellationToken);
        if (duplicate)
        {
            throw new BusinessRuleException($"A '{category.Kind}' category named '{name}' already exists.");
        }

        var before = new { category.Name, category.Sequence, category.IsActive };

        category.Name = name;
        category.Sequence = request.Sequence;
        category.IsActive = request.IsActive;

        _auditWriter.Add(AuditAction.Update, nameof(AbsenceReasonCategory), category.Id.ToString(), before,
            new { category.Name, category.Sequence, category.IsActive });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AbsenceCategoryDto(category.Id, category.Kind, category.Name, category.Sequence, category.IsActive);
    }

    /// <summary>Throws when the current principal lacks the required role.</summary>
    private void Require(UserRole minimumRole)
    {
        if (!_currentUser.HasAtLeast(minimumRole))
        {
            throw new ForbiddenException();
        }
    }
}
