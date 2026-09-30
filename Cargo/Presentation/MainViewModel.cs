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
    private const double WideBreakpoint = 1280;
    private const double NarrowBreakpoint = 1040;

    private readonly INavigator _navigator;
    private readonly ILogger<MainViewModel> _logger;

    // The section the router last reported, so a Section change that came from the router
    // is not navigated a second time.
    private string? _routedSection;

    public MainViewModel(PortState state, INavigator navigator, IRouteNotifier routeNotifier, IDispatcher dispatcher, ILogger<MainViewModel> logger)
    {
        State = state;
        _navigator = navigator;
        _logger = logger;


        // The router builds models off the UI thread, and the header items carry brushes and
        // geometries, which must be created on it.
        dispatcher.TryEnqueue(() =>
        {
            BuildNav();
            BuildLayers();
            RefreshTide();

            state.PropertyChanged += OnStateChanged;
            state.Ticked += (_, _) => RefreshTide();
            routeNotifier.RouteChanged += (_, e) => dispatcher.TryEnqueue(() => OnRouteChanged(e));
        });
    }

    public PortState State { get; }

    public ObservableCollection<NavItem> NavItems { get; } = new();

    public ObservableCollection<LayerItem> Layers { get; } = new();

    public TideReadout Tide { get; } = new();

    /// <summary>Weather and tide leave the header below the wide breakpoint.</summary>
    [ObservableProperty]
    private bool _wide = true;

    /// <summary>The ISPS badge leaves below the narrow breakpoint.</summary>
    [ObservableProperty]
    private bool _roomy = true;

    public void ApplyWidth(double width)
    {
        var wide = width >= WideBreakpoint;
        var roomy = width >= NarrowBreakpoint;
        if (wide == Wide && roomy == Roomy)
        {
            return;
        }

        Wide = wide;
        Roomy = roomy;
        BuildNav();
    }

    // The nav and the layer switcher each depend on one property, so they listen for that one
    // rather than for every structural change in the app.
    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PortState.Section):
                BuildNav();
                _ = ShowSectionAsync(State.Section);
                break;
            case nameof(PortState.HarbourLayer):
                BuildLayers();
                break;
        }
    }

    /// <summary>
    /// For a section route the event's Route is the app-level path ("Main/berths") and the
    /// navigator's route base is the section id; between moves it also reports a bare "Main".
    /// </summary>
    private void OnRouteChanged(RouteChangedEventArgs e)
    {
        if (e.Navigator?.Route?.Base is not { Length: > 0 } section
            || PortData.Sections.All(s => s.Id != section))
        {
            return;
        }

        _routedSection = section;
        State.Section = section;
    }

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
            await _navigator.NavigateRouteAsync(this, $"./{section}");
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
        var meta = new Dictionary<string, (string Sub, string? Badge)>
        {
            ["overview"] = ("3 alongside · 2 inbound · 2,146 moves today", null),
            ["berths"] = ($"Nordic Star in {nordicIn} min · Berth 06 frees 21:30", "AmberColor"),
            ["cargo"] = ($"4,812 on site · {State.YardOccupancy}% yard · 1 hold", "OrangeColor"),
            ["fleet"] = ("6 underway · 2 running late", "OrangeColor"),
            ["security"] = ("ISPS level 1 · 0 incidents", null)
        };

        if (NavItems.Count == 0)
        {
            for (var i = 0; i < PortData.Sections.Count; i++)
            {
                var section = PortData.Sections[i];
                NavItems.Add(new NavItem
                {
                    Id = section.Id,
                    Index = (i + 1).ToString("D2"),
                    Label = section.Label
                });
            }
        }

        foreach (var item in NavItems)
        {
            var current = State.Section == item.Id;
            var (sub, badge) = meta[item.Id];

            // Below the wide breakpoint only the section you are on keeps its name.
            var showLabel = Wide || (current && Roomy);

            item.Tooltip = $"{item.Label} · {sub}";
            item.ChipBackground = current ? Tokens.Brush("TealBrightBrush") : Tokens.Brush("DeckWhiteColor", 0.08);
            item.ChipForeground = current ? Tokens.Brush("InkDeepBrush") : Tokens.Brush("TextOnDarkMutedBrush");
            item.LabelForeground = current ? Tokens.Brush("SurfaceBrush") : Tokens.Brush("TextOnDarkBrush");
            item.BadgeBrush = badge is null ? Tokens.Transparent : Tokens.Brush(badge);
            item.BadgeOpacity = badge is null ? 0 : 1;
            item.UnderlineOpacity = current ? 1 : 0;
            item.LabelVisibility = showLabel ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void RefreshTide()
    {
        Tide.Spark ??= Geo.MakeSeries(PortData.Tide, 160, 36, 4).Stroke;

        var level = PortData.TideAt(State.NowHours);
        Tide.Label = $"+{level:0.0} m";
        Tide.Tooltip = $"Tide · +{level:0.0} m rising";
    }

    private void BuildLayers()
    {
        (string Id, string Label, string Icon)[] definitions =
        {
            ("port", "Map", "M3 6l6-2 6 2 6-2v14l-6 2-6-2-6 2zM9 4v14M15 6v14"),
            ("yard", "Yard", "M3 14h6v6H3zM9 14h6v6H9zM15 14h6v6h-6zM6 8h6v6H6zM12 8h6v6h-6z"),
            ("security", "Security", "M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6z"),
            ("traffic", "Traffic", "M4 12h12M12 6l6 6-6 6")
        };

        Layers.Clear();
        foreach (var (id, label, icon) in definitions)
        {
            var current = State.HarbourLayer == id;
            Layers.Add(new LayerItem
            {
                Id = id,
                Label = label,
                Icon = Geo.Path(icon),
                Background = current ? Tokens.Brush("InkBrush") : Tokens.Transparent,
                Foreground = current ? Tokens.Brush("PaperBrush") : Tokens.Brush("TextMutedBrush"),
                Command = State.SetLayerCommand
            });
        }
    }
}
