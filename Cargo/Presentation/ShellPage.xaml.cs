using System.ComponentModel;
using Microsoft.UI.Dispatching;
using Cargo.Controls;
using Microsoft.UI.Xaml.Media;

namespace Cargo.Presentation;

public sealed partial class ShellPage : Page
{
    private const double WideBreakpoint = 1280;
    private const double NarrowBreakpoint = 1040;

    private readonly Dictionary<string, UIElement> _sections = new();

    private bool _wide = true;
    private bool _roomy = true;

    public ShellPage(PortState state)
    {
        State = state;
        NavItems = new ObservableCollection<NavItem>();
        Layers = new ObservableCollection<LayerItem>();

        InitializeComponent();

        DataContext = state;
        Harbour.State = state;

        BuildNav();
        BuildLayers();
        ShowSection(state.Section);
        RefreshTide();

        state.PropertyChanged += OnStateChanged;
        ScannerHost.Content = new ScannerOverlay(state);
        state.Ticked += (_, _) => RefreshTide();

        SizeChanged += (_, e) => ApplyBreakpoints(e.NewSize.Width);

        Loaded += (_, _) =>
        {
            WarmSections();
            ScrollToStartOffset();
        };
    }

    public PortState State { get; }

    public ObservableCollection<NavItem> NavItems { get; }

    public ObservableCollection<LayerItem> Layers { get; }

    public TideReadout Tide { get; } = new();

    // The nav and the layer switcher each depend on one property, so they listen for that one
    // rather than for every structural change in the app.
    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PortState.Section):
                BuildNav();
                ShowSection(State.Section);
                break;
            case nameof(PortState.HarbourLayer):
                BuildLayers();
                break;
        }
    }

    // ── Sections ──────────────────────────────────────────────────────────────

    private void ShowSection(string section)
    {
        if (!_sections.TryGetValue(section, out var view))
        {
            view = Create(section);
            _sections[section] = view;
        }

        SectionHost.Content = view;
    }

    /// <summary>
    /// Lets a verification run photograph a panel that sits below the fold, which a
    /// background process cannot scroll to with synthesized input.
    /// </summary>
    [System.Diagnostics.Conditional("DEBUG")]
    private void ScrollToStartOffset()
    {
        if (!double.TryParse(Environment.GetEnvironmentVariable("CARGO_SCROLL"), out var offset) || offset <= 0)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low,
            () => SectionScroll.ChangeView(null, offset, null, true));
    }

    /// <summary>
    /// Builds the sections that have not been visited yet, one per idle turn. A section costs
    /// the better part of a second to construct the first time, and paying that on the click
    /// is what made moving to a new section feel like a stall.
    /// </summary>
    private void WarmSections()
    {
        var pending = PortData.Sections
            .Select(s => s.Id)
            .Where(id => !_sections.ContainsKey(id))
            .ToList();

        var index = 0;

        void Next()
        {
            if (index >= pending.Count)
            {
                return;
            }

            var id = pending[index++];
            if (!_sections.ContainsKey(id))
            {
                _sections[id] = Create(id);
            }

            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, Next);
        }

        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, Next);
    }

    private UIElement Create(string section) => section switch
    {
        "berths" => new BerthsView(State),
        "cargo" => new CargoView(State),
        "security" => new SecurityView(State),
        "fleet" => new FleetView(State),
        _ => new OverviewView(State)
    };

    // ── Header ────────────────────────────────────────────────────────────────

    private void ApplyBreakpoints(double width)
    {
        var wide = width >= WideBreakpoint;
        var roomy = width >= NarrowBreakpoint;

        if (wide == _wide && roomy == _roomy)
        {
            return;
        }

        _wide = wide;
        _roomy = roomy;

        WeatherBadge.Visibility = wide ? Visibility.Visible : Visibility.Collapsed;
        TideBadge.Visibility = wide ? Visibility.Visible : Visibility.Collapsed;
        IspsBadge.Visibility = roomy ? Visibility.Visible : Visibility.Collapsed;

        BuildNav();
    }

    /// <summary>
    /// Builds the eight header items on first use, then only refreshes what navigation
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
                    Label = section.Label,
                    Command = State.GoCommand
                });
            }
        }

        foreach (var item in NavItems)
        {
            var current = State.Section == item.Id;
            var (sub, badge) = meta[item.Id];

            // Below the wide breakpoint only the section you are on keeps its name.
            var showLabel = _wide || (current && _roomy);

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

    // ── Harbour overlay ───────────────────────────────────────────────────────

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
