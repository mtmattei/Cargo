using System.Windows.Input;
using Cargo.Controls;
using Microsoft.UI.Xaml.Media;

namespace Cargo.Presentation;

public sealed class FilterChip
{
    public required string Label { get; init; }
    public required Brush Background { get; init; }
    public required Brush Foreground { get; init; }
    public required Brush Border { get; init; }
    public required ICommand Apply { get; init; }
}

public sealed class ContainerCard
{
    public required string Id { get; init; }
    public required string IdPretty { get; init; }
    public required string Meta { get; init; }
    public required string Route { get; init; }
    public required string State { get; init; }
    public required Brush StateTone { get; init; }
    public required Brush Border { get; init; }
    public required ImageSource Render { get; init; }
    public required ICommand Select { get; init; }
}

public sealed class JourneyStep
{
    public required string Label { get; init; }
    public required Geometry Icon { get; init; }
    public required Brush Background { get; init; }
    public required Brush Border { get; init; }
    public required Brush Foreground { get; init; }
    public required Brush LabelTone { get; init; }
    public required Brush Connector { get; init; }
}

public sealed partial class ContainersView : UserControl
{
    public ContainersView(PortState state)
    {
        State = state;
        Filters = new ObservableCollection<FilterChip>();
        Cards = new ObservableCollection<ContainerCard>();
        Journey = new ObservableCollection<JourneyStep>();
        Facts = new ObservableCollection<Fact>();

        InitializeComponent();

        Refresh();
        this.RebuildWhenVisible(state, Refresh);
    }

    public PortState State { get; }

    public ObservableCollection<FilterChip> Filters { get; }
    public ObservableCollection<ContainerCard> Cards { get; }
    public ObservableCollection<JourneyStep> Journey { get; }
    public ObservableCollection<Fact> Facts { get; }
    public string SelectedId { get; private set; } = string.Empty;
    public string SelectedMeta { get; private set; } = string.Empty;

    // ── Refresh ───────────────────────────────────────────────────────────────

    private void Refresh()
    {
        var container = State.SelectedContainer;

        BuildFilters();
        BuildCards();
        BuildDetail(container);

        SelectedIso.Source = Sprites.Source(Sprites.ContainerByTone(container.TintToken));

        Bindings.Update();
    }

    private void BuildFilters()
    {
        Filters.Clear();
        foreach (var filter in PortData.ContainerFilters)
        {
            var current = State.ContainerFilter == filter;
            Filters.Add(new FilterChip
            {
                Label = filter,
                Background = current ? Tokens.Brush("InkBrush") : Tokens.Brush("SurfaceBrush"),
                Foreground = current ? Tokens.Brush("PaperBrush") : Tokens.Brush("InkBrush"),
                Border = current ? Tokens.Brush("InkBrush") : Tokens.Brush("HairlineStrongBrush"),
                Apply = new RelayCommand<string>(value => State.ContainerFilter = value ?? "All")
            });
        }
    }

    private void BuildCards()
    {
        var stages = PortData.FilterStages.TryGetValue(State.ContainerFilter, out var allowed) ? allowed : null;

        Cards.Clear();
        foreach (var container in PortData.Containers.Where(c => stages is null || stages.Contains(c.Stage)))
        {
            Cards.Add(new ContainerCard
            {
                Id = container.Id,
                IdPretty = Pretty(container.Id),
                Meta = $"{container.Size} · {container.Carrier.ToUpperInvariant()}",
                Route = container.Route,
                State = container.State,
                StateTone = Tokens.Brush(container.Warn ? "OrangeColor" : "TealColor"),
                Border = State.SelectedContainerId == container.Id
                    ? Tokens.Brush("InkBrush")
                    : Tokens.Brush("HairlineBrush"),
                Render = Sprites.Source(Sprites.ContainerByTone(container.TintToken)),
                Select = new RelayCommand<string>(id =>
                {
                    if (id is null)
                    {
                        return;
                    }

                    State.SelectedContainerId = id;
                    State.ScannerOpen = true;
                    State.Xray = false;
                    State.ScanRotationX = -18;
                    State.ScanRotationY = -32;
                })
            });
        }
    }

    private static string Pretty(string id) => $"{id[..4]} {id[4..10]} {id[10..]}";

    private void BuildDetail(ContainerDef container)
    {
        SelectedId = Pretty(container.Id);
        SelectedMeta = $"{container.Size} · {container.Carrier} · {container.Route}";

        Journey.Clear();
        for (var i = 0; i < PortData.JourneyStages.Count; i++)
        {
            var current = i == container.Stage;
            var done = i < container.Stage;
            var tone = current && container.Warn
                ? Tokens.Brush("OrangeBrush")
                : current ? Tokens.Brush("InkBrush")
                : done ? Tokens.Brush("TealBrush")
                : Tokens.Brush("InkColor", 0.18);

            Journey.Add(new JourneyStep
            {
                Label = PortData.JourneyStages[i],
                Icon = Geo.Path(PortData.JourneyIcons[i]),
                Background = current ? tone : done ? Tokens.Brush("TealColor", 0.1) : Tokens.Brush("SurfaceBrush"),
                Border = tone,
                Foreground = current ? Tokens.Brush("SurfaceBrush") : done ? Tokens.Brush("TealBrush") : Tokens.Brush("TextFaintBrush"),
                LabelTone = current ? Tokens.Brush("InkBrush") : done ? Tokens.Brush("TealBrush") : Tokens.Brush("TextFaintBrush"),
                Connector = i == PortData.JourneyStages.Count - 1
                    ? Tokens.Transparent
                    : done ? Tokens.Brush("TealBrush") : Tokens.Brush("InkColor", 0.12)
            });
        }

        Facts.Clear();
        foreach (var (key, value, tone) in new (string, string, string?)[]
                 {
                     ("Weight", container.Weight, null),
                     ("Dimensions", container.Dimensions, null),
                     ("Carrier", container.Carrier, null),
                     ("Vessel", container.Vessel, null),
                     ("Origin", container.Origin, null),
                     ("Destination", container.Destination, null),
                     ("Yard location", container.YardSlot, null),
                     ("Customs", container.Customs,
                         container.Customs is "Cleared" or "Pre-cleared" ? "TealColor" : "OrangeColor"),
                     ("Inspection", container.Inspection, container.Warn ? "OrangeColor" : null),
                     ("Seal", container.Seal, null),
                     ("Security", container.Security, container.SecurityToken)
                 })
        {
            Facts.Add(new Fact { Key = key, Value = value, Tone = tone is null ? null : Tokens.Brush(tone) });
        }
    }


    private void OnOpenScanner(object sender, RoutedEventArgs e) => State.ScannerOpen = true;
}
