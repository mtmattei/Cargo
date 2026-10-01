using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Cargo.Presentation;

public sealed class RecordRow
{
    public required string Label { get; init; }
    public required string Count { get; init; }
    public required Brush Tone { get; init; }
}

public sealed class PersonCard
{
    public required int Index { get; init; }
    public required string Name { get; init; }
    public required string Role { get; init; }
    public required string Credential { get; init; }
    public required string Initials { get; init; }
    public required Brush Avatar { get; init; }
    public required Brush Border { get; init; }
    public required ICommand Select { get; init; }
}

public sealed class VehicleCard
{
    public required Color Tint { get; init; }
    public required IReadOnlyList<Fact> Facts { get; init; }
}

/// <summary>One in-page tab; <see cref="Route"/> is its nested route under the page.</summary>
public sealed class PageTab
{
    public required string Label { get; init; }
    public required string Route { get; init; }
    public required Brush Background { get; init; }
    public required Brush Foreground { get; init; }
}

/// <summary>
/// Security: zones, access control and inspection, each a nested route in the page's
/// Visibility region. The tab buttons navigate declaratively; the route they land on is written
/// back to <see cref="PortState.SecurityTab"/>, and a tab set from elsewhere in the store (the
/// "Open inspection" deep link) navigates the region to match.
/// </summary>
public sealed partial class SecurityViewModel : ObservableObject
{
    public static readonly (string Id, string Label)[] TabDefinitions =
    {
        ("zones", "Zones"), ("access", "Access control"), ("inspection", "Inspection")
    };

    private readonly INavigator _navigator;
    private readonly ILogger<SecurityViewModel> _logger;

    // The tab the router last reported, so a change that came from the router is not navigated again
    private string? _routedTab;

    public SecurityViewModel(PortState state, INavigator navigator, IRouteNotifier routeNotifier,
        IDispatcher dispatcher, ILogger<SecurityViewModel> logger)
    {
        State = state;
        _navigator = navigator;
        _logger = logger;

        // The router builds models off the UI thread; the rows carry brushes, which must be
        // created on it.
        dispatcher.TryEnqueue(() =>
        {
            BuildStatic();
            BuildTabs();
            BuildPeople();

            state.PropertyChanged += (_, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(PortState.SecurityTab):
                        BuildTabs();
                        _ = ShowTabAsync(state.SecurityTab);
                        break;
                    case nameof(PortState.SelectedPerson):
                        BuildPeople();
                        break;
                }
            };
            routeNotifier.RouteChanged += (_, e) => dispatcher.TryEnqueue(() => OnRouteChanged(e));
        });
    }

    public PortState State { get; }

    public ObservableCollection<PageTab> Tabs { get; } = new();
    public ObservableCollection<PersonCard> People { get; } = new();

    [ObservableProperty]
    private IReadOnlyList<CheckRow> _clearedChecks = [];

    [ObservableProperty]
    private IReadOnlyList<CheckRow> _heldChecks = [];

    [ObservableProperty]
    private IReadOnlyList<RecordRow> _records = [];

    [ObservableProperty]
    private IReadOnlyList<CheckRow> _inspectionLog = [];

    [ObservableProperty]
    private IReadOnlyList<VehicleCard> _vehicles = [];

    [ObservableProperty]
    private string _zoneHeading = string.Empty;

    [ObservableProperty]
    private string _zoneCount = string.Empty;

    /// <summary>The zones the selected person's badge opens, for the access map.</summary>
    [ObservableProperty]
    private IReadOnlyList<int> _allowedZones = [];

    [RelayCommand]
    private void SelectPerson(int index) => State.SelectedPerson = index;

    private void OnRouteChanged(RouteChangedEventArgs e)
    {
        if (MainViewModel.RouteSegments(e) is not ["security", var tab, ..] || TabDefinitions.All(t => t.Id != tab))
        {
            return;
        }

        _routedTab = tab;
        State.SecurityTab = tab;
    }

    private async Task ShowTabAsync(string tab)
    {
        if (tab == _routedTab)
        {
            return;
        }

        try
        {
            // This model shares Main's navigator (it is created with MainViewModel), so the tab is
            // addressed through its section
            await _navigator.NavigateRouteAsync(this, $"./security/{tab}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation to security tab {Tab} failed", tab);
        }
    }

    private void BuildTabs()
    {
        Tabs.Clear();
        foreach (var (id, label) in TabDefinitions)
        {
            var current = State.SecurityTab == id;
            Tabs.Add(new PageTab
            {
                Label = label,
                Route = $"./{id}",
                Background = current ? Tokens.Brush("SurfaceInvariantBrush") : Tokens.Transparent,
                Foreground = current ? Tokens.Brush("InkInvariantBrush") : Tokens.Brush("TextMutedInvariantBrush")
            });
        }
    }

    private void BuildPeople()
    {
        People.Clear();
        for (var i = 0; i < PortData.People.Count; i++)
        {
            var person = PortData.People[i];
            People.Add(new PersonCard
            {
                Index = i,
                Name = person.Name,
                Role = person.Role,
                Credential = person.Credential,
                Initials = string.Concat(person.Name.Split(' ').Select(part => part[0])),
                Avatar = Tokens.Brush(person.AvatarToken),
                Border = State.SelectedPerson == i ? Tokens.Brush("InkInvariantBrush") : Tokens.Brush("HairlineInvariantBrush"),
                Select = SelectPersonCommand
            });
        }

        var selected = PortData.People[State.SelectedPerson];
        ZoneHeading = $"{selected.Name} · authorized zones";
        ZoneCount = $"{selected.Zones.Count} of {PortData.AccessZones.Count}";
        AllowedZones = selected.Zones;
    }

    private void BuildStatic()
    {
        ClearedChecks = new[]
        {
            "Seal verified", "Manifest matched", "Customs cleared", "X-ray inspection complete"
        }.Select(label => new CheckRow
        {
            Mark = "✓",
            Label = label,
            Time = string.Empty,
            Tone = Tokens.Brush("AccentInvariantBrush")
        }).ToList();

        HeldChecks = new[]
        {
            ("✓", "Seal verified", "AccentInvariantBrush"),
            ("✓", "Manifest matched", "AccentInvariantBrush"),
            ("⚠", "Weight variance +1.8 t · manual inspection required", "OrangeInvariantBrush")
        }.Select(entry => new CheckRow
        {
            Mark = entry.Item1,
            Label = entry.Item2,
            Time = string.Empty,
            Tone = Tokens.Brush(entry.Item3)
        }).ToList();

        Records = PortData.SecurityRecords
            .Select(r => new RecordRow { Label = r.Label, Count = r.Count, Tone = Tokens.Brush(r.Tone) })
            .ToList();

        InspectionLog = PortData.InspectionLog
            .Select(l => new CheckRow { Mark = l.Mark, Label = l.Text, Time = l.Time, Tone = Tokens.Brush(l.Tone) })
            .ToList();

        Vehicles = PortData.Vehicles.Select(v => new VehicleCard
        {
            Tint = Tokens.Color(v.TintToken),
            Facts = new[]
            {
                new Fact { Key = "Truck", Value = v.Id },
                new Fact { Key = "Driver", Value = v.Driver },
                new Fact { Key = "Container", Value = v.Container },
                new Fact { Key = "Gate", Value = v.Gate },
                new Fact { Key = "Authorization", Value = v.Authorization, Tone = Tokens.Brush(v.AuthToken) },
                new Fact { Key = "Entry", Value = v.Entry }
            }
        }).ToList();
    }
}
