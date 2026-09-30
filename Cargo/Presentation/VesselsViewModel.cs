using System.Windows.Input;
using Cargo.Controls;
using Microsoft.UI.Xaml.Media;

namespace Cargo.Presentation;

public sealed class VesselRow
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Line1 { get; init; }
    public required string Line2 { get; init; }
    public required Brush Tone { get; init; }
    public required Brush Border { get; init; }
    public required ImageSource Silhouette { get; init; }
    public required ICommand Select { get; init; }
}

/// <summary>Rebuilt only when the vessel changes; the live figures tick in place.</summary>
public sealed partial class MiniCard : ObservableObject
{
    public required string Key { get; init; }
    public required Brush Tone { get; init; }
    public required Geometry Icon { get; init; }

    [ObservableProperty]
    private string _value = string.Empty;

    [ObservableProperty]
    private string _detail = string.Empty;
}

public sealed class CheckRow
{
    public required string Mark { get; init; }
    public required string Label { get; init; }
    public required string Time { get; init; }
    public required Brush Tone { get; init; }
}

/// <summary>
/// The vessel being worked, under the berth plan: its profile and bays, cargo operations,
/// composition and clearance. <see cref="Refresh"/> follows structural changes (a different
/// vessel or bay); <see cref="RefreshLive"/> follows the clock. Created by
/// <see cref="BerthsViewModel"/> on the UI thread.
/// </summary>
public sealed partial class VesselsViewModel : ObservableObject
{
    public VesselsViewModel(PortState state)
    {
        State = state;
        CargoLegend = Enum.GetValues<CargoClass>()
            .Select(c => new LegendItem { Label = c.Label(), Swatch = Tokens.Brush(c.Token()) })
            .Append(new LegendItem { Label = "Already ashore", Swatch = Tokens.Brush("TextFaintInvariantBrush") })
            .ToList();

        Refresh();
        RefreshLive();
    }

    public PortState State { get; }

    public string Eyebrow => $"VESSELS · {PortData.Vessels.Count} IN PORT TODAY";

    public ObservableCollection<VesselRow> Vessels { get; } = new();
    public ObservableCollection<MiniCard> MiniCards { get; } = new();
    public ObservableCollection<CompositionSlice> Composition { get; } = new();
    public ObservableCollection<CompositionSlice> BayComposition { get; } = new();
    public ObservableCollection<Fact> Facts { get; } = new();
    public ObservableCollection<CheckRow> Checks { get; } = new();
    public IReadOnlyList<LegendItem> CargoLegend { get; }

    /// <summary>The vessel whose cranes the operations scene animates.</summary>
    [ObservableProperty]
    private Vessel? _vessel;

    [ObservableProperty]
    private BayView? _bay;

    [ObservableProperty]
    private IReadOnlyList<(double Weight, Brush Fill)> _compositionSlices = [];

    [ObservableProperty]
    private IReadOnlyList<(double Weight, Brush Fill)> _baySlices = [];

    [ObservableProperty]
    private ImageSource? _bayContainerImage;

    [ObservableProperty] private string _vesselName = string.Empty;
    [ObservableProperty] private string _profileHint = string.Empty;
    [ObservableProperty] private string _bayLabel = string.Empty;
    [ObservableProperty] private string _voyage = string.Empty;
    [ObservableProperty] private string _security = string.Empty;
    [ObservableProperty] private Brush? _securityBackground;
    [ObservableProperty] private Brush? _securityForeground;
    [ObservableProperty] private string _teuLabel = string.Empty;
    [ObservableProperty] private string _opsNote = string.Empty;
    [ObservableProperty] private string _movesLabel = string.Empty;
    [ObservableProperty] private string _unloadLive = string.Empty;
    [ObservableProperty] private string _loadLive = string.Empty;
    [ObservableProperty] private double _unloadPercent;
    [ObservableProperty] private double _loadPercent;

    [ObservableProperty] private string _bayHeading = string.Empty;
    [ObservableProperty] private string _bayTotal = string.Empty;
    [ObservableProperty] private string _bayRemaining = string.Empty;
    [ObservableProperty] private string _bayOpsHeading = string.Empty;
    [ObservableProperty] private string _bayCompositionHeading = string.Empty;
    [ObservableProperty] private string _bayUnloaded = string.Empty;
    [ObservableProperty] private string _bayLeft = string.Empty;
    [ObservableProperty] private string _bayNextMove = string.Empty;
    [ObservableProperty] private string _bayNextEta = string.Empty;
    [ObservableProperty] private string _bayContainerId = string.Empty;
    [ObservableProperty] private string _bayContainerType = string.Empty;
    [ObservableProperty] private string _bayContainerWeight = string.Empty;
    [ObservableProperty] private string _bayContainerStatus = string.Empty;

    // ── Structure ─────────────────────────────────────────────────────────────

    public void Refresh()
    {
        var vessel = State.SelectedVessel;

        VesselName = vessel.Name;
        ProfileHint = $"{vessel.Length} m · {vessel.Bays} bays · Click a bay";
        Voyage = vessel.Voyage;
        Security = vessel.Security;

        var cleared = vessel.Security == "Cleared";
        SecurityBackground = cleared ? Tokens.Brush("TealColor", 0.12) : Tokens.Brush("AmberDeepColor", 0.16);
        SecurityForeground = cleared ? Tokens.Brush("TealInvariantBrush") : Tokens.Brush("OrangeInkInvariantBrush");

        TeuLabel = vessel.Containers.ToString("N0");
        OpsNote = vessel.OpsNote;

        BuildVesselList();
        BuildFacts(vessel);
        BuildChecks(vessel);
        BuildComposition(vessel);
        BuildBay(vessel);

        // A bay click or a hover is a structure change too; the scene restarts its crane loop
        // only when this is a different vessel.
        Vessel = vessel;
    }

    private void BuildVesselList()
    {
        Vessels.Clear();
        foreach (var vessel in PortData.Vessels)
        {
            var selected = vessel.Id == State.SelectedVesselId;
            var parts = vessel.StatusLine.Split(" · ");

            Vessels.Add(new VesselRow
            {
                Id = vessel.Id,
                Name = vessel.Name,
                Line1 = string.Join(" · ", parts.Take(2)),
                Line2 = SecondLine(vessel, parts),
                Tone = Tokens.Brush(vessel.AccentToken),
                Border = selected ? Tokens.Brush("TealInvariantBrush") : Tokens.Brush("HairlineInvariantBrush"),
                Silhouette = Sprites.Source(Sprites.Side(vessel.Id)),
                Select = State.PickHullVesselCommand
            });
        }
    }

    private static string SecondLine(Vessel vessel, string[] parts)
    {
        if (parts.Length < 3)
        {
            return vessel.Status == "At anchor" ? "Waiting for Berth 06" : string.Empty;
        }

        var tail = parts[2];
        var capitalised = char.ToUpperInvariant(tail[0]) + tail[1..];
        return tail switch
        {
            "discharging" => $"{capitalised} {vessel.UnloadPercent}%",
            "loading" => $"{capitalised} {vessel.LoadPercent}%",
            _ => capitalised
        };
    }

    private void BuildFacts(Vessel vessel)
    {
        Facts.Clear();
        var berth = State.BerthOf(vessel.Id);
        foreach (var (key, value) in new[]
                 {
                     ("Operator", vessel.Operator),
                     ("IMO", vessel.Imo),
                     ("Arrival", vessel.Eta),
                     ("Departure", vessel.Etd),
                     ("Berth", berth is null ? "Unassigned" : $"Berth {berth}"),
                     ("Containers", $"{vessel.Containers:N0} TEU"),
                     ("Length", $"{vessel.Length} m"),
                     ("Draft", $"{vessel.Draft} m")
                 })
        {
            Facts.Add(new Fact { Key = key, Value = value });
        }
    }

    private void BuildChecks(Vessel vessel)
    {
        Checks.Clear();
        foreach (var check in vessel.Checks)
        {
            Checks.Add(new CheckRow
            {
                Mark = check.Mark,
                Label = check.Label,
                Time = check.Time,
                Tone = Tokens.Brush(check.Tone)
            });
        }
    }

    private void BuildComposition(Vessel vessel)
    {
        Composition.Clear();
        var slices = new List<(double, Brush)>();

        foreach (var (cargo, percent) in vessel.Mix.Parts())
        {
            var brush = Tokens.Brush(cargo.Token());
            Composition.Add(new CompositionSlice
            {
                Label = cargo.Label(),
                Fill = brush,
                Count = $"{percent}%"
            });
            slices.Add((percent, brush));
        }

        CompositionSlices = slices;
    }

    private void BuildBay(Vessel vessel)
    {
        var boundary = Math.Min(vessel.Bays - 1, (int)Math.Floor(vessel.Bays * vessel.UnloadPercent / 100d));
        var selected = State.SelectedBay is { } bay && bay < vessel.Bays ? bay : boundary;
        var view = BayView.For(vessel, selected);

        BayLabel = BayView.Summary(vessel, State.HoveredBay ?? selected);
        BayHeading = $"Bay {view.Number}";
        BayTotal = $"· {BayView.Capacity} containers";
        BayRemaining = $"· {view.Remaining} remaining aboard";
        BayOpsHeading = $"Bay {view.Number} operations";
        BayCompositionHeading = $"Bay {view.Number} composition";
        BayUnloaded = view.Unloaded.ToString();
        BayLeft = view.Remaining.ToString();
        BayNextMove = view.NextMove;
        BayNextEta = $"Crane 03 · ETA {view.NextEta}";
        BayContainerId = view.ContainerId;
        BayContainerType = view.ContainerType;
        BayContainerWeight = view.Weight;
        BayContainerStatus = view.Status;
        BayContainerImage = Sprites.Source(Sprites.Container(view.ContainerClass));

        BayComposition.Clear();
        var slices = new List<(double, Brush)>();
        foreach (var (cargo, count, percent) in view.Composition)
        {
            var brush = Tokens.Brush(cargo.Token());
            BayComposition.Add(new CompositionSlice
            {
                Label = cargo.Label(),
                Fill = brush,
                Count = $"{count} ({percent}%)"
            });
            slices.Add((count, brush));
        }

        BaySlices = slices;
        Bay = view;
    }

    // ── Live ──────────────────────────────────────────────────────────────────

    public void RefreshLive()
    {
        var vessel = State.SelectedVessel;
        var elapsed = State.ElapsedSeconds;

        var unload = vessel.UnloadPercent is > 0 and < 100
            ? Math.Min(100, vessel.UnloadPercent + elapsed * 0.012)
            : vessel.UnloadPercent;
        var load = vessel.LoadPercent is > 0 and < 100
            ? Math.Min(100, vessel.LoadPercent + elapsed * 0.006)
            : vessel.LoadPercent;

        UnloadPercent = unload;
        LoadPercent = load;
        UnloadLive = $"{unload:0.0}%";
        LoadLive = $"{load:0.0}%";

        var moves = vessel.UnloadPercent is > 0 and < 100
            ? vessel.LoadPercent > 0 ? 58 : 41
            : vessel.LoadPercent is > 0 and < 100 ? 36 : 0;
        MovesLabel = $"Live · {moves} moves/h";

        var ashore = Math.Round(vessel.Containers * vessel.UnloadPercent / 100d);
        (string Key, string Value, string Detail, string Tone, string Icon)[] cards =
        {
            ("Length overall", $"{vessel.Length} m", $"Draft {vessel.Draft} m", "InkInvariantBrush",
                "M3 12h18M3 12l4-4M3 12l4 4M21 12l-4-4M21 12l-4 4"),
            ("Discharged", UnloadLive, $"{ashore:N0} discharged", "SeaGreenInvariantBrush",
                "M12 3v12M12 15l-4-4M12 15l4-4M4 21h16"),
            ("Loaded", LoadLive, $"{moves} moves/h", "TealInvariantBrush",
                "M12 21V9M12 9l-4 4M12 9l4 4M4 3h16"),
            ("Departure", vessel.Etd, State.BerthOf(vessel.Id) is { } b ? $"Berth {b}" : "Unassigned", "InkInvariantBrush",
                "M3 15l2 5h14l2-5zM5 15V9h14v6M9 9V5h6v4")
        };

        if (MiniCards.Count != cards.Length)
        {
            MiniCards.Clear();
            foreach (var card in cards)
            {
                MiniCards.Add(new MiniCard
                {
                    Key = card.Key,
                    Tone = Tokens.Brush(card.Tone),
                    Icon = Geo.Path(card.Icon)
                });
            }
        }

        for (var i = 0; i < cards.Length; i++)
        {
            MiniCards[i].Value = cards[i].Value;
            MiniCards[i].Detail = cards[i].Detail;
        }
    }
}
