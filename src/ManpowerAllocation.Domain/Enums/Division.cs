namespace ManpowerAllocation.Domain.Enums;

/// <summary>
/// The three top-level organisational divisions the factory manpower is grouped into.
/// A department belongs to exactly one division; the prototype enforced this exclusivity
/// with the precedence BRG &gt; FunctionalSupport &gt; EGL, which is preserved server-side.
/// </summary>
public enum Division
{
    /// <summary>Emirates Glass production floor (the prototype's "PRODUCTION" / "prod" division).</summary>
    Egl = 1,

    /// <summary>Functional / support functions such as Quality Control, Warehouse and HSE
    /// (the prototype's "OPERATIONS" / "ops" division).</summary>
    FunctionalSupport = 2,

    /// <summary>Bent &amp; Reinforced Glass division (the prototype's "BRG" division).</summary>
    Brg = 3
}
