namespace ManpowerAllocation.Application.Import;

/// <summary>The outcome of a master-data import, returned to the administrator.</summary>
public sealed record ImportResult
{
    /// <summary>Number of departments created during the import.</summary>
    public int DepartmentsCreated { get; init; }

    /// <summary>Number of departments whose requirements were updated during the import.</summary>
    public int DepartmentsUpdated { get; init; }

    /// <summary>Number of employees imported.</summary>
    public int EmployeesImported { get; init; }

    /// <summary>Number of existing employees updated in place by a non-destructive edit-apply.</summary>
    public int EmployeesUpdated { get; init; }

    /// <summary>Number of existing employees removed because the import replaced a division's data.</summary>
    public int EmployeesRemoved { get; init; }

    /// <summary>Non-fatal messages describing rows that were skipped or divisions that were left untouched.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
