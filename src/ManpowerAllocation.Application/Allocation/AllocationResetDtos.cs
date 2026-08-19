namespace ManpowerAllocation.Application.Allocation;

/// <summary>Status of the shift-loan auto-reset, for the admin screen.</summary>
/// <param name="Enabled">Whether the automatic shift-reset is currently on.</param>
/// <param name="LastMasterUploadUtc">When a master sheet was last uploaded (the enable gate); null if never.</param>
/// <param name="LastResetAtUtc">When the reset last ran (automatic or manual); null if never.</param>
/// <param name="LoanedNow">How many employees are currently away from their home department (on loan).</param>
/// <param name="UnsetHomeCount">How many employees have no home department set (must be zero to enable).</param>
/// <param name="CanEnable">True when the enable gate is satisfied (a master was uploaded and every home is set).</param>
public sealed record AllocationResetStatusDto(
    bool Enabled,
    DateTime? LastMasterUploadUtc,
    DateTime? LastResetAtUtc,
    int LoanedNow,
    int UnsetHomeCount,
    bool CanEnable);
