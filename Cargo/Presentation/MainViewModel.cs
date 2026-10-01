using System.ComponentModel;

namespace Cargo.Presentation;

/// <summary>
/// The dispatcher's frame: the section header, the harbour layer switcher, the tide readout,
/// and the bridge between the router and <see cref="PortState.Section"/>. The header navigates
/// declaratively (uen:Navigation.Request); the route it lands on is written back to Section,
/// which frames the harbour. Moves that start inside the store (opening a vessel, an
/// inspection, a harbour tag) set Section, and this model navigates to match.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly INavigator _navigator;
    private readonly ILogger<MainViewModel> _logger;

    // The section the router last reported, so a Section change that came from the router
    // is not navigated a second time.
    private string? _routedSection;

    public MainViewModel(PortState state, INavigator navigator, IRouteNotifier routeNotifier, IDispatcher dispatcher,
        ILogger<MainViewModel> logger, OverviewViewModel overview, BerthsViewModel berths, CargoViewModel cargo,
        FleetViewModel fleet, SecurityViewModel security)
    {
        State = state;
        Overview = overview;
        Berths = berths;
        Cargo = cargo;
        Fleet = fleet;
        Security = security;
        _navigator = navigator;
        _logger = logger;


        // The router builds models off the UI thread, and the header items carry brushes and
        // geometries, which must be created on it.
        dispatcher.TryEnqueue(() =>
        {
            Scanner = new ScannerViewModel(state);
            BuildNav();
            BuildLayers();

            state.PropertyChanged += OnStateChanged;
            routeNotifier.RouteChanged += (_, e) => dispatcher.TryEnqueue(() => OnRouteChanged(e));
        });
    }

    public PortState State { get; }

    public OverviewViewModel Overview { get; }
    public BerthsViewModel Berths { get; }
    public CargoViewModel Cargo { get; }
    public FleetViewModel Fleet { get; }
    public SecurityViewModel Security { get; }

    /// <summary>The Overview greeting and figures sit above the harbour, on Overview only.</summary>
    public bool IsOverview => State.Section == "overview";

    public ObservableCollection<NavItem> NavItems { get; } = new();

    public ObservableCollection<LayerItem> Layers { get; } = new();

    /// <summary>The operator on shift, behind the header's avatar.</summary>
    public DutyOperator Operator => PortData.Operator;

    public string AccountName => $"Account and settings: {Operator.ShortName}";

    public string OperatorLine => $"{Operator.Role} · {Operator.Badge} · {Operator.ClockedInText} to {Operator.ShiftEndText}";

    /// <summary>The account menu's reduced-motion toggle, saved between launches (see <see cref="Motion"/>).</summary>
    public bool ReduceMotion
    {
        get => Motion.Requested;
        set
        {
            if (Motion.Requested != value)
            {
                Motion.Requested = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>The container inspection that covers the whole window while it is open.</summary>
    [ObservableProperty]
    private ScannerViewModel? _scanner;

    // The nav and the layer switcher each depend on one or two properties, so they listen for
    // those rather than for every structural change in the app.
    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PortState.Section):
                OnPropertyChanged(nameof(IsOverview));
                BuildNav();
                _ = ShowSectionAsync(State.Section);
                break;
            case nameof(PortState.NeedsDecision):
                // Confirming or undoing the berth adds or clears the Berths dot
                BuildNav();
                break;
            case nameof(PortState.HarbourLayer):
                BuildLayers();
                break;
        }
    }

    /// <summary>
    /// The segments of the app-level route under Main: "Main/security/zones" gives
    /// ["security", "zones"]. The navigator's own route names only the deepest level, and a
    /// bare "Main" is reported between moves, so the full path is the reliable source.
    /// </summary>
    public static string[] RouteSegments(RouteChangedEventArgs e) =>
        (e.Route?.Path ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);

    private void OnRouteChanged(RouteChangedEventArgs e)
    {
        if (RouteSegments(e) is not [var section, ..] || PortData.Sections.All(s => s.Id != section))
        {
            return;
        }

        _routedSection = section;
        State.Section = section;
    }

    /// <summary>
    /// The route for the store's section. Security carries its tab, so a deep link such as
    /// "Open inspection" lands on the right pane even on the page's first visit, when the
    /// region would otherwise start on its default tab.
    /// </summary>
    public static string RouteFor(PortState state) =>
        state.Section == "security" ? $"security/{state.SecurityTab}" : state.Section;

    private async Task ShowSectionAsync(string section)
    {
        if (section == _routedSection)
        {
            return;
        }

        try
        {
            // Section ids are the route names, registered under Main in App.RegisterRoutes.
            // "./" targets the section region inside Main; without it the route replaces Main itself.
            await _navigator.NavigateRouteAsync(this, $"./{RouteFor(State)}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation to section {Section} failed", section);
        }
    }

    /// <summary>
    /// Builds the header items on first use, then only refreshes what navigation
    /// actually changes. Replacing the collection recreated every button and its subtree,
    /// which read as a flicker across the header on each move between sections.
    /// </summary>
    private void BuildNav()
    {
        // Sub-labels double as the tooltip: what each section is worth looking at for.
        var nordicIn = (int)Math.Floor(Math.Max(0, 21 + 40 / 60d - State.NowHours) * 60);
        var subs = new Dictionary<string, string>
        {
            ["overview"] = "3 alongside · 2 inbound · 2,146 moves today",
            ["berths"] = $"Nordic Star in {nordicIn} min · Berth 06 frees 21:30",
            ["cargo"] = $"4,812 on site · {State.YardOccupancy}% yard · 1 hold",
            ["fleet"] = "6 underway · 2 running late",
            ["security"] = "ISPS level 1 · 0 incidents"
        };

        if (NavItems.Count == 0)
        {
            foreach (var section in PortData.Sections)
            {
                NavItems.Add(new NavItem { Id = section.Id, Label = section.Label });
            }
        }

        foreach (var item in NavItems)
        {
            var current = State.Section == item.Id;
            var needs = State.SectionNeedsYou(item.Id);

            item.Tooltip = $"{item.Label} · {subs[item.Id]}";
            item.AutomationName = needs ? $"{item.Label}, needs you" : item.Label;
            item.LabelForeground = current ? Tokens.Brush("InkInvariantBrush") : Tokens.Brush("TextMutedInvariantBrush");
            item.NeedsVisibility = needs ? Visibility.Visible : Visibility.Collapsed;
            item.UnderlineOpacity = current ? 1 : 0;
            item.CurrentVisibility = current ? Visibility.Visible : Visibility.Collapsed;
            item.OtherVisibility = current ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    /// <summary>The current layer's place in <see cref="Layers"/>, for the switcher's sliding indicator.</summary>
    [ObservableProperty]
    private int _selectedLayerIndex = -1;

    /// <summary>The design's 24-unit stroke icons for the four layers (EXTRACTION §2 Harbour).</summary>
    private static readonly (string Id, string Label, string Icon)[] LayerDefinitions =
    {
        ("port", "Map", "M9 4 3.5 6v14L9 18l6 2 5.5-2V4L15 6zM9 4v14M15 6v14"),
        ("yard", "Yard", "M4.5 7h15a1 1 0 0 1 1 1v9a1 1 0 0 1-1 1h-15a1 1 0 0 1-1-1V8a1 1 0 0 1 1-1zM3.5 12.5h17M9.2 7v11M14.8 7v11"),
        ("security", "Security", "M12 3.2 19 6v5.2c0 4.4-2.9 7.5-7 8.9-4.1-1.4-7-4.5-7-8.9V6z"),
        ("traffic", "Traffic", "M4 12h15M14 7l5 5-5 5")
    };

    private void BuildLayers()
    {
        if (Layers.Count == 0)
        {
            foreach (var (id, label, icon) in LayerDefinitions)
            {
                Layers.Add(new LayerItem { Id = id, Label = label, Icon = Geo.Path(icon), Command = State.SetLayerCommand });
            }
        }

        for (var i = 0; i < Layers.Count; i++)
        {
            var current = State.HarbourLayer == Layers[i].Id;
            Layers[i].Foreground = current ? Tokens.Brush("PaperInvariantBrush") : Tokens.Brush("InkInvariantBrush");
            Layers[i].IconForeground = current ? Tokens.Brush("PaperInvariantBrush") : Tokens.Brush("TextMutedInvariantBrush");
            if (current)
            {
                SelectedLayerIndex = i;
            }
        }
    }
}
