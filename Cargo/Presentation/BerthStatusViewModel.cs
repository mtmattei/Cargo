using System.Windows.Input;
using Microsoft.UI.Xaml.Media;

namespace Cargo.Presentation;

/// <summary>
/// One line of the Berths status list: a berth, or a vessel waiting at anchor. Rows are built
/// once and then updated in place, so a confirm recolours row 07 without rebuilding the list or
/// losing the scroll position.
/// </summary>
public sealed partial class BerthStatusRow : ObservableObject
{
    /// <summary>A berth number ("07"), or for an anchorage row the vessel id.</summary>
    public required string Slot { get; init; }

    public required ICommand Select { get; init; }

    /// <summary>The vessel on or bound for this berth; null for a free or closed berth.</summary>
    [ObservableProperty] private string? _vesselId;
    [ObservableProperty] private string _plate = string.Empty;
    [ObservableProperty] private string _stateLabel = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _meta = string.Empty;
    [ObservableProperty] private string _when = string.Empty;
    [ObservableProperty] private string _accessibleName = string.Empty;
    [ObservableProperty] private Brush? _rowBackground;
    [ObservableProperty] private Brush? _outline;
    [ObservableProperty] private Brush? _plateBackground;
    [ObservableProperty] private Brush? _plateForeground;
    [ObservableProperty] private Brush? _plateBorder;
    [ObservableProperty] private Brush? _stateForeground;
    [ObservableProperty] private Brush? _nameForeground;
    [ObservableProperty] private Brush? _whenForeground;
}

/// <summary>
/// Berth status: the eight berths of North Quay and the anchorage, each with what it holds
/// now or next. Created by <see cref="BerthsViewModel"/> on the UI thread.
/// </summary>
public sealed partial class BerthStatusViewModel : ObservableObject
{
    private readonly PortState _state;

    public BerthStatusViewModel(PortState state)
    {
        _state = state;
        Refresh();
    }

    public ObservableCollection<BerthStatusRow> Rows { get; } = new();

    [ObservableProperty]
    private string _count = string.Empty;

    [RelayCommand]
    private void Pick(string slot)
    {
        var row = Rows.FirstOrDefault(r => r.Slot == slot);
        if (row?.VesselId is { } vessel)
        {
            _state.PickVessel(vessel);
        }
        else
        {
            _state.SelectedBerth = slot;
        }
    }

    /// <summary>Called by the page while it is on screen, on structure changes and each tick.</summary>
    public void Refresh()
    {
        var anchored = PortData.Vessels.Where(v => _state.BerthOf(v.Id) is null).ToList();
        var slots = PortData.Berths.Select(b => b.Number).Concat(anchored.Select(v => v.Id)).ToList();

        // The berths never change; only the anchorage rows come and go as vessels get a berth
        if (!Rows.Select(r => r.Slot).SequenceEqual(slots))
        {
            Rows.Clear();
            foreach (var slot in slots)
            {
                Rows.Add(new BerthStatusRow { Slot = slot, Select = PickCommand });
            }
        }

        foreach (var row in Rows)
        {
            if (PortData.Berths.FirstOrDefault(b => b.Number == row.Slot) is { } berth)
            {
                FillBerth(row, berth);
            }
            else
            {
                FillAnchorage(row, PortData.Vessel(row.Slot));
            }
        }

        Count = anchored.Count == 0
            ? $"{PortData.Berths.Count} berths"
            : $"{PortData.Berths.Count} berths · {anchored.Count} at anchor";
    }

    private void FillBerth(BerthStatusRow row, BerthDef berth)
    {
        var now = _state.NowHours;
        var vessel = _state.OccupantAt(berth.Number, now) ?? _state.IncomingAt(berth.Number, now);
        row.VesselId = vessel?.Id;
        row.Plate = berth.Number;

        if (vessel is not null)
        {
            var needs = _state.PendingDecision == vessel.Id;
            row.StateLabel = BerthFacts.State(_state, vessel).ToUpperInvariant();
            row.Name = vessel.Name;
            row.Meta = needs
                ? $"Pilot aboard · under-keel {BerthFacts.UnderKeel(vessel, berth):0.0} m"
                : BerthFacts.Note(vessel);
            row.When = BerthFacts.When(_state, vessel);
            row.AccessibleName = $"Berth {berth.Number}, {vessel.Name}, {BerthFacts.State(_state, vessel)}, {row.When}"
                + (needs ? ", needs confirming" : string.Empty);
            Paint(row, needs ? Kind.Needs : Kind.Vessel, IsCurrent(vessel.Id, berth.Number));
            return;
        }

        row.Name = berth.State == "restricted" ? "Closed" : "Free berth";
        row.StateLabel = berth.State == "restricted" ? "RESTRICTED" : "AVAILABLE";
        row.Meta = berth.State == "restricted"
            ? $"{BerthFacts.Restriction(berth)} · {berth.Depth:0.0} m"
            : $"Depth {berth.Depth:0.0} m";
        row.When = string.Empty;
        row.AccessibleName = $"Berth {berth.Number}, {row.Name.ToLowerInvariant()}, {row.Meta}";
        Paint(row, berth.State == "restricted" ? Kind.Restricted : Kind.Free, IsCurrent(null, berth.Number));
    }

    private void FillAnchorage(BerthStatusRow row, Vessel vessel)
    {
        row.VesselId = vessel.Id;
        row.Plate = "A";
        row.StateLabel = "AT ANCHOR";
        row.Name = vessel.Name;
        row.Meta = BerthFacts.FitsLine(_state, vessel);
        row.When = BerthFacts.Clock(vessel.Eta);
        row.AccessibleName = $"Anchorage, {vessel.Name}, at anchor, arrival window {row.When}, {row.Meta}";
        Paint(row, Kind.Vessel, IsCurrent(vessel.Id, null));
    }

    private bool IsCurrent(string? vesselId, string? berth) =>
        _state.SelectedBerth is { } picked
            ? picked == berth && vesselId is null
            : vesselId is not null && vesselId == _state.SelectedVesselId;

    private enum Kind { Vessel, Needs, Free, Restricted }

    private static void Paint(BerthStatusRow row, Kind kind, bool current)
    {
        var ink = Tokens.Brush("InkInvariantBrush");
        var muted = Tokens.Brush("TextMutedInvariantBrush");
        var transparent = Tokens.Brush("InkInvariantBrush", 0);

        row.Outline = current ? ink : transparent;
        row.RowBackground = kind == Kind.Needs ? Tokens.Brush("AmberInvariantBrush", .16) : transparent;
        row.PlateBackground = kind == Kind.Needs ? Tokens.Brush("AmberInvariantBrush") : Tokens.Brush("SurfaceSunkInvariantBrush");
        row.PlateForeground = kind == Kind.Restricted ? Tokens.Brush("RestrictedInvariantBrush") : ink;
        row.PlateBorder = kind switch
        {
            Kind.Needs => transparent,
            Kind.Restricted => Tokens.Brush("RestrictedInvariantBrush"),
            _ => Tokens.Brush("HairlineStrongInvariantBrush")
        };
        row.StateForeground = kind switch
        {
            Kind.Needs => Tokens.Brush("AmberInkInvariantBrush"),
            Kind.Restricted => Tokens.Brush("RestrictedInvariantBrush"),
            _ => muted
        };
        row.NameForeground = kind == Kind.Free ? muted : ink;
        row.WhenForeground = kind == Kind.Needs ? Tokens.Brush("AmberInkInvariantBrush") : ink;
    }
}

/// <summary>The wording the status list, the vessel detail and the needs-you bar share.</summary>
internal static class BerthFacts
{
    /// <summary>"Today 21:40" → 21.67; "Thu 02:30" → 26.5 (the day after the demo's Wednesday).</summary>
    public static double Hours(string stamp)
    {
        var parts = stamp.Split(' ');
        var hm = parts[^1].Split(':');
        var hours = int.Parse(hm[0]) + int.Parse(hm[1]) / 60d;
        var dayOffset = parts.Length < 2 ? 0 : parts[0] switch
        {
            "Tue" => -24,
            "Thu" => 24,
            "Fri" => 48,
            _ => 0
        };
        return hours + dayOffset;
    }

    /// <summary>"Today 21:40" → "21:40"; other days keep their name.</summary>
    public static string Clock(string stamp) => stamp.Replace("Today ", string.Empty);

    /// <summary>Whole minutes to go, rounded up so the figure changes on the minute: "42 min", then "Due".</summary>
    public static string MinutesTo(PortState state, double hour)
    {
        var minutes = (int)Math.Ceiling((hour - state.NowHours) * 60 - 1e-9);
        return minutes > 0 ? $"{minutes} min" : "Due";
    }

    public static string State(PortState state, Vessel vessel) => vessel.Status switch
    {
        "Arriving" when state.PendingDecision == vessel.Id => "Arriving · confirm berth",
        "Arriving" when state.IsConfirmed(vessel.Id) => "Arriving · berth confirmed",
        "Docked" when vessel.UnloadPercent is > 0 and < 100 => "Discharging",
        "Docked" => "Loading",
        _ => vessel.Status
    };

    /// <summary>The time a row leads with: the arrival countdown, else when the vessel sails.</summary>
    public static string When(PortState state, Vessel vessel) => vessel.Status switch
    {
        "Arriving" => MinutesTo(state, Hours(vessel.Eta)),
        "At anchor" => Clock(vessel.Eta),
        _ => Clock(vessel.Etd)
    };

    /// <summary>The first sentence of the ops note, which is what a row has room for.</summary>
    // A sentence ends at ". " before a capital, so "est. completion" stays whole
    public static string Note(Vessel vessel) =>
        System.Text.RegularExpressions.Regex.Split(vessel.OpsNote, @"(?<=\.)\s+(?=[A-Z])")[0].TrimEnd('.');

    public static double UnderKeel(Vessel vessel, BerthDef berth) => berth.Depth - vessel.Draft;

    public static string Restriction(BerthDef berth) =>
        PortData.BerthHistory.TryGetValue(berth.Number, out var segments)
            ? segments.FirstOrDefault(s => s.Kind == "restricted")?.Label ?? "Closed"
            : "Closed";

    /// <summary>"Unassigned · fits berths 01, 04, 06": the berths whose depth clears the draft.</summary>
    public static string FitsLine(PortState state, Vessel vessel)
    {
        var fits = PortData.Berths.Where(b => b.State != "restricted" && b.Depth >= vessel.Draft).Select(b => b.Number).ToList();
        return fits.Count == 0 ? "Unassigned · no berth deep enough" : $"Unassigned · fits berths {string.Join(", ", fits)}";
    }
}
