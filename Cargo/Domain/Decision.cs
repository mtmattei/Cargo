namespace Cargo.Domain;

public enum DecisionKind
{
    InspectionHold,
    DriverCheck,
    LateOrders,
    BerthConfirm
}

/// <summary>
/// One thing waiting on the dispatcher, as the Needs-you queue lists it. The list is rebuilt when
/// what needs deciding changes; the timing part of the sub-line (<see cref="When"/>) is updated in
/// place on the clock, so a tick never rebuilds a row.
/// </summary>
public sealed partial class Decision : ObservableObject
{
    public required string Id { get; init; }
    public required DecisionKind Kind { get; init; }
    public required string Title { get; init; }

    /// <summary>The fixed part of the sub-line: "Inspection due 21:15".</summary>
    public required string Sub { get; init; }

    public required string ActionLabel { get; init; }

    /// <summary>The queue's one primary action; the rest are secondary buttons.</summary>
    public required bool IsPrimary { get; init; }

    public required ICommand Command { get; init; }
    public object? CommandParameter { get; init; }

    /// <summary>What the shift log calls it ("CMAU 918204 4", "TRK 8834"), so a log entry can show it needs action.</summary>
    public string? Subject { get; init; }

    /// <summary>The vessel the row is about, for hover linking; null for cargo and gate rows.</summary>
    public string? VesselId { get; init; }

    /// <summary>The live part of the sub-line: "in 17 min", "20 min late", "waiting 54 min".</summary>
    [ObservableProperty]
    private string _when = string.Empty;

    /// <summary>Whether <see cref="When"/> is overdue, which sets it in alert ink.</summary>
    [ObservableProperty]
    private bool _isLate;
}
