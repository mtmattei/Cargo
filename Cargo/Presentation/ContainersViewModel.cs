using Cargo.Controls;

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

/// <summary>
/// The container explorer: filters, the container cards and the selected box's journey and
/// facts. Created by <see cref="CargoViewModel"/> on the UI thread.
/// </summary>
public sealed partial class ContainersViewModel : ObservableObject
{
    public ContainersViewModel(PortState state)
    {
        State = state;
        Refresh();
    }

    public PortState State { get; }

    public ObservableCollection<FilterChip> Filters { get; } = new();
    public ObservableCollection<ContainerCard> Cards { get; } = new();
    public ObservableCollection<JourneyStep> Journey { get; } = new();
    public ObservableCollection<Fact> Facts { get; } = new();

    [ObservableProperty] private string _selectedId = string.Empty;
    [ObservableProperty] private string _selectedMeta = string.Empty;
    [ObservableProperty] private ImageSource? _selectedIso;

    [RelayCommand]
    private void Filter(string? value) => State.ContainerFilter = value ?? "All";

    /// <summary>Selects a container and opens it in the scanner at the default view.</summary>
    [RelayCommand]
    private void Inspect(string? id)
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
    }

    [RelayCommand]
    private void OpenScanner() => State.ScannerOpen = true;

    public void Refresh()
    {
        var container = State.SelectedContainer;

        BuildFilters();
        BuildCards();
        BuildDetail(container);

        SelectedIso = Sprites.Source(Sprites.ContainerByTone(container.TintToken));
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
                Background = current ? Tokens.Brush("InkInvariantBrush") : Tokens.Brush("SurfaceInvariantBrush"),
                Foreground = current ? Tokens.Brush("PaperInvariantBrush") : Tokens.Brush("InkInvariantBrush"),
                Border = current ? Tokens.Brush("InkInvariantBrush") : Tokens.Brush("HairlineStrongInvariantBrush"),
                Apply = FilterCommand
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
                IdPretty = container.DisplayId,
                Meta = $"{container.Size} · {container.Carrier.ToUpperInvariant()}",
                Route = container.Route,
                State = container.State,
                StateTone = Tokens.Brush(container.Warn ? "OrangeColor" : "AccentColor"),
                Border = State.SelectedContainerId == container.Id
                    ? Tokens.Brush("InkInvariantBrush")
                    : Tokens.Brush("HairlineInvariantBrush"),
                Render = Sprites.Source(Sprites.ContainerByTone(container.TintToken)),
                Select = InspectCommand
            });
        }
    }

    private void BuildDetail(ContainerDef container)
    {
        SelectedId = container.DisplayId;
        SelectedMeta = $"{container.Size} · {container.Carrier} · {container.Route}";

        Journey.Clear();
        for (var i = 0; i < PortData.JourneyStages.Count; i++)
        {
            var current = i == container.Stage;
            var done = i < container.Stage;
            var tone = current && container.Warn
                ? Tokens.Brush("OrangeInvariantBrush")
                : current ? Tokens.Brush("InkInvariantBrush")
                : done ? Tokens.Brush("AccentInvariantBrush")
                : Tokens.Brush("InkColor", 0.18);

            Journey.Add(new JourneyStep
            {
                Label = PortData.JourneyStages[i],
                Icon = Geo.Path(PortData.JourneyIcons[i]),
                Background = current ? tone : done ? Tokens.Brush("AccentColor", 0.1) : Tokens.Brush("SurfaceInvariantBrush"),
                Border = tone,
                Foreground = current ? Tokens.Brush("SurfaceInvariantBrush") : done ? Tokens.Brush("AccentInvariantBrush") : Tokens.Brush("TextFaintInvariantBrush"),
                LabelTone = current ? Tokens.Brush("InkInvariantBrush") : done ? Tokens.Brush("AccentInvariantBrush") : Tokens.Brush("TextFaintInvariantBrush"),
                Connector = i == PortData.JourneyStages.Count - 1
                    ? Tokens.Transparent
                    : done ? Tokens.Brush("AccentInvariantBrush") : Tokens.Brush("InkColor", 0.12)
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
                         container.Customs is "Cleared" or "Pre-cleared" ? "AccentColor" : "OrangeColor"),
                     ("Inspection", container.Inspection, container.Warn ? "OrangeColor" : null),
                     ("Seal", container.Seal, null),
                     ("Security", container.Security, container.SecurityToken)
                 })
        {
            Facts.Add(new Fact { Key = key, Value = value, Tone = tone is null ? null : Tokens.Brush(tone) });
        }
    }
}
