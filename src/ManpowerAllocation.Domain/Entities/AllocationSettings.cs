namespace ManpowerAllocation.Domain.Entities;

/// <summary>
/// The single, admin-configurable settings row for the shift-loan model. Controls whether the
/// automatic shift-reset (returning every loaned employee to their home department at each shift
/// changeover) is switched on, and records when a master sheet was last uploaded (the gate that
/// must be satisfied before the reset can be enabled).
/// </summary>
public sealed class AllocationSettings
{
    /// <summary>The single settings row always uses this fixed identifier.</summary>
    public const int SingletonId = 1;

    /// <summary>Surrogate primary key; always <see cref="SingletonId"/>.</summary>
    public int Id { get; set; } = SingletonId;

    /// <summary>
    /// Whether the automatic shift-reset runs at each day/night changeover. Off by default; an admin
    /// enables it only after a master sheet has been uploaded (see <see cref="LastMasterUploadUtc"/>).
    /// </summary>
    public bool AutoShiftResetEnabled { get; set; }

    /// <summary>UTC time a master sheet was last uploaded (roster/apply-edits). Null until the first upload.</summary>
    public DateTime? LastMasterUploadUtc { get; set; }

    /// <summary>
    /// Marker of the last shift boundary the auto-reset ran for (e.g. "20260819-N"), so a restart or a
    /// repeated poll within the same shift window does not reset twice.
    /// </summary>
    public string? LastResetMarker { get; set; }

    /// <summary>UTC time the auto-reset last ran (automatic or manual), for the admin screen.</summary>
    public DateTime? LastResetAtUtc { get; set; }

    /// <summary>When these settings were last changed (UTC).</summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>Entra object id of the administrator who last changed the settings.</summary>
    public string? UpdatedByObjectId { get; set; }
}
