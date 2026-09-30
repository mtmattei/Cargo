using System.Windows.Input;
using Cargo.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

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

public sealed partial class VesselsView : UserControl
{
    private readonly VesselProfile _profile;
    private readonly CargoOpsScene _ops = new();
    private readonly BayScene _bay = new();
    private string? _opsVesselId;

    public VesselsView(PortState state)
    {
        State = state;
        Vessels = new ObservableCollection<VesselRow>();
        MiniCards = new ObservableCollection<MiniCard>();
        Composition = new ObservableCollection<CompositionSlice>();
        BayComposition = new ObservableCollection<CompositionSlice>();
        Facts = new ObservableCollection<Fact>();
        Checks = new ObservableCollection<CheckRow>();

        InitializeComponent();

        _profile = new VesselProfile(state);
        ProfileHost.Content = _profile;
        OpsHost.Content = _ops;
        BayHost.Content = _bay;

        CargoLegend = Enum.GetValues<CargoClass>()
            .Select(c => new LegendItem { Label = c.Label(), Swatch = Tokens.Brush(c.Token()) })
            .Append(new LegendItem { Label = "Already ashore", Swatch = Tokens.Brush("TextFaintBrush"), Outline = true })
            .ToList();

        Refresh();
        RefreshLive();

        this.RebuildWhenVisible(state, Refresh);
        this.TickWhenVisible(state, RefreshLive);
    }

    public PortState State { get; }

    public string Eyebrow => $"VESSELS · {PortData.Vessels.Count} IN PORT TODAY";

    public ObservableCollection<VesselRow> Vessels { get; }
    public ObservableCollection<MiniCard> MiniCards { get; }
    public ObservableCollection<CompositionSlice> Composition { get; }
    public ObservableCollection<CompositionSlice> BayComposition { get; }
    public ObservableCollection<Fact> Facts { get; }
    public ObservableCollection<CheckRow> Checks { get; }
    public IReadOnlyList<LegendItem> CargoLegend { get; }

    public string VesselName { get; private set; } = string.Empty;
    public string ProfileHint { get; private set; } = string.Empty;
    public string BayLabel { get; private set; } = string.Empty;
    public string Voyage { get; private set; } = string.Empty;
    public string Security { get; private set; } = string.Empty;
    public Brush SecurityBackground { get; private set; } = Tokens.Transparent;
    public Brush SecurityForeground { get; private set; } = Tokens.Transparent;
    public string TeuLabel { get; private set; } = string.Empty;
    public string OpsNote { get; private set; } = string.Empty;
    public string MovesLabel { get; private set; } = string.Empty;
    public string UnloadLive { get; private set; } = string.Empty;
    public string LoadLive { get; private set; } = string.Empty;

    public string BayHeading { get; private set; } = string.Empty;
    public string BayTotal { get; private set; } = string.Empty;
    public string BayRemaining { get; private set; } = string.Empty;
    public string BayOpsHeading { get; private set; } = string.Empty;
    public string BayCompositionHeading { get; private set; } = string.Empty;
    public string BayUnloaded { get; private set; } = string.Empty;
    public string BayLeft { get; private set; } = string.Empty;
    public string BayNextMove { get; private set; } = string.Empty;
    public string BayNextEta { get; private set; } = string.Empty;
    public string BayContainerId { get; private set; } = string.Empty;
    public string BayContainerType { get; private set; } = string.Empty;
    public string BayContainerWeight { get; private set; } = string.Empty;
    public string BayContainerStatus { get; private set; } = string.Empty;

    // ── Structure ─────────────────────────────────────────────────────────────

    private void Refresh()
    {
        var vessel = State.SelectedVessel;

        VesselName = vessel.Name;
        ProfileHint = $"{vessel.Length} m · {vessel.Bays} bays · Click a bay";
        Voyage = vessel.Voyage;
        Security = vessel.Security;

        var cleared = vessel.Security == "Cleared";
        SecurityBackground = cleared ? Tokens.Brush("TealColor", 0.12) : Tokens.Brush("AmberDeepColor", 0.16);
        SecurityForeground = cleared ? Tokens.Brush("TealBrush") : Tokens.Brush("OrangeInkBrush");

        TeuLabel = vessel.Containers.ToString("N0");
        OpsNote = vessel.OpsNote;

        BuildVesselList();
        BuildFacts(vessel);
        BuildChecks(vessel);
        BuildComposition(vessel);
        BuildBay(vessel);

        // A bay click or a hover is a structure change too; restarting the crane loop for the
        // same vessel made the cranes jump back to their first move.
        if (_opsVesselId != vessel.Id)
        {
            _opsVesselId = vessel.Id;
            _ops.Show(vessel);
        }

        Bindings.Update();
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
                Border = selected ? Tokens.Brush("TealBrush") : Tokens.Brush("HairlineBrush"),
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
        CompositionBar.Items.Clear();

        var slices = new List<(double, Brush)>();
        var panel = new Grid { ColumnSpacing = 2 };

        foreach (var (cargo, percent) in vessel.Mix.Parts())
        {
            var brush = Tokens.Brush(cargo.Token());
            Composition.Add(new CompositionSlice
            {
                Label = cargo.Label(),
                Percent = percent,
                Fill = brush,
                Count = $"{percent}%"
            });
            slices.Add((percent, brush));

            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(percent, GridUnitType.Star) });
            var block = new Rectangle { Fill = brush };
            Grid.SetColumn(block, panel.ColumnDefinitions.Count - 1);
            panel.Children.Add(block);
        }

        CompositionBar.Items.Add(new Border
        {
            CornerRadius = new CornerRadius(7),
            Height = 14,
            Child = panel
        });

        CompositionDonut.Content = Draw.Donut(slices, 38, 14);
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
        BayContainer.Source = Sprites.Source(Sprites.Container(view.ContainerClass));

        BayComposition.Clear();
        var slices = new List<(double, Brush)>();
        foreach (var (cargo, count, percent) in view.Composition)
        {
            var brush = Tokens.Brush(cargo.Token());
            BayComposition.Add(new CompositionSlice
            {
                Label = cargo.Label(),
                Percent = percent,
                Fill = brush,
                Count = $"{count} ({percent}%)"
            });
            slices.Add((count, brush));
        }

        BayDonut.Content = Draw.Donut(slices, 36, 16);
        _bay.Show(view);
    }

    // ── Live ──────────────────────────────────────────────────────────────────

    private void RefreshLive()
    {
        var vessel = State.SelectedVessel;
        var elapsed = State.ElapsedSeconds;

        var unload = vessel.UnloadPercent is > 0 and < 100
            ? Math.Min(100, vessel.UnloadPercent + elapsed * 0.012)
            : vessel.UnloadPercent;
        var load = vessel.LoadPercent is > 0 and < 100
            ? Math.Min(100, vessel.LoadPercent + elapsed * 0.006)
            : vessel.LoadPercent;

        UnloadLive = $"{unload:0.0}%";
        LoadLive = $"{load:0.0}%";

        var moves = vessel.UnloadPercent is > 0 and < 100
            ? vessel.LoadPercent > 0 ? 58 : 41
            : vessel.LoadPercent is > 0 and < 100 ? 36 : 0;
        MovesLabel = $"Live · {moves} moves/h";

        FillTicks(UnloadTicks, unload, "SeaGreenBrush");
        FillTicks(LoadTicks, load, "TealBrush");

        var ashore = Math.Round(vessel.Containers * vessel.UnloadPercent / 100d);
        (string Key, string Value, string Detail, string Tone, string Icon)[] cards =
        {
            ("Length overall", $"{vessel.Length} m", $"Draft {vessel.Draft} m", "InkBrush",
                "M3 12h18M3 12l4-4M3 12l4 4M21 12l-4-4M21 12l-4 4"),
            ("Discharged", UnloadLive, $"{ashore:N0} discharged", "SeaGreenBrush",
                "M12 3v12M12 15l-4-4M12 15l4-4M4 21h16"),
            ("Loaded", LoadLive, $"{moves} moves/h", "TealBrush",
                "M12 21V9M12 9l-4 4M12 9l4 4M4 3h16"),
            ("Departure", vessel.Etd, State.BerthOf(vessel.Id) is { } b ? $"Berth {b}" : "Unassigned", "InkBrush",
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

        Bindings.Update();
    }

    /// <summary>
    /// Thirty ticks, built once. Progress moves a tick every few minutes, so the one-second
    /// tick only restyles when the lit count actually changes.
    /// </summary>
    private static void FillTicks(ItemsControl host, double percent, string litToken)
    {
        var litCount = Enumerable.Range(0, 30).Count(i => i / 30d * 100 < percent);
        if (host.Items.Count == 30 && host.Tag is int drawn && drawn == litCount)
        {
            return;
        }

        host.ItemsPanel ??= HorizontalTicks();
        if (host.Items.Count != 30)
        {
            host.Items.Clear();
            for (var i = 0; i < 30; i++)
            {
                host.Items.Add(new Rectangle
                {
                    RadiusX = 1,
                    RadiusY = 1,
                    Width = 4,
                    Margin = new Thickness(0, 0, 2, 0),
                    VerticalAlignment = VerticalAlignment.Bottom
                });
            }
        }

        for (var i = 0; i < 30; i++)
        {
            var lit = i < litCount;
            var tick = (Rectangle)host.Items[i];
            tick.Height = lit ? i % 5 == 0 ? 14 : 10 : 6;
            tick.Fill = lit ? Tokens.Brush(litToken) : Tokens.Brush("InkColor", 0.12);
        }

        host.Tag = litCount;
    }

    private static ItemsPanelTemplate HorizontalTicks() =>
        (ItemsPanelTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(
            "<ItemsPanelTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">" +
            "<StackPanel Orientation=\"Horizontal\" VerticalAlignment=\"Bottom\" /></ItemsPanelTemplate>");
}
