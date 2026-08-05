using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Web.Common;

/// <summary>
/// A per-department outsource (supply) tally for the "Outsource by department" summary panel.
/// </summary>
/// <param name="Division">The division the department belongs to.</param>
/// <param name="Department">The department name.</param>
/// <param name="Present">Outsource workers currently present in the department (for the selected shift).</param>
/// <param name="Total">Outsource workers assigned to the department (for the selected shift).</param>
public sealed record OutsourceRow(Division Division, string Department, int Present, int Total);
