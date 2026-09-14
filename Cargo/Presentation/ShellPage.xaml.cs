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
        Callouts = new ObservableCollection<VesselCallout>();
        Layers = new ObservableCollection<LayerItem>();

        InitializeComponent();

        DataContext = state;
        Stage.State = state;

        BuildNav();
        BuildLayers();
        BuildCallouts();
        ShowSection(state.Section);
        RefreshTide();

        state.PropertyChanged += OnStateChanged;
        ScannerHost.Content = new ScannerOverlay(state);
        state.Ticked += (_, _) => RefreshTide();

        SizeChanged += (_, e) => ApplyBreakpoints(e.NewSize.Width);
        StageHost.SizeChanged += (_, _) => BuildCallouts();

        Loaded += (_, _) =>
        {
            WarmSections();
            ScrollToStartOffset();
        };
    }

    public PortState State { get; }

    public ObservableCollection<NavItem> NavItems { get; }

    public ObservableCollection<VesselCallout> Callouts { get; }

    public ObservableCollection<LayerItem> Layers { get; }

    public TideReadout Tide { get; } = new();

    // The overlay and the callouts each depend on one property, so they listen for that one
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
            case nameof(PortState.HarbourSelection):
                BuildCallouts();
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
        "vessels" => new VesselsView(State),
        "docking" => new DockingView(State),
        "containers" => new ContainersView(State),
        "yard" => new YardView(State),
        "security" => new SecurityView(State),
        "fleet" => new FleetView(State),
        "activity" => new ActivityView(State),
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
            ["overview"] = ("3 alongside · 2 inbound", null),
            ["fleet"] = ("6 underway · 2 running late", "OrangeColor"),
            ["vessels"] = ($"Nordic Star in {nordicIn} min", null),
            ["docking"] = ("Berth 06 frees 21:30", "AmberColor"),
            ["containers"] = ("4,812 on site · 1 hold", "OrangeColor"),
            ["yard"] = ($"{State.YardOccupancy}% occupied · live", null),
            ["security"] = ("1 hold · 0 incidents", "OrangeColor"),
            ["activity"] = ("2,146 moves today", null)
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
                Background = current ? Tokens.Brush("DeckWhiteColor", 0.14) : Tokens.Transparent,
                Foreground = current ? Tokens.Brush("SurfaceBrush") : Tokens.Brush("DeckWhiteColor", 0.6),
                Command = State.SetLayerCommand
            });
        }
    }

    private void BuildCallouts()
    {
        var width = StageHost.ActualWidth;
        var height = StageHost.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        // Anchored to the hulls they describe, as fractions of the stage. Nordic Star's card
        // is pinned to the bottom-right corner instead, clear of the approach channel.
        (string Id, double Fx, double Fy, string Tone, string L1, string L2)[] definitions =
        {
            ("kaida", 0.053, 0.505, "TealBrightBrush", "Berth 01 · Loading", "74% loaded · sails 23:30"),
            ("aurora", 0.354, 0.505, "TealPaleBrush", "Docked · Berth 04", "1,248 containers · 62% unloaded"),
            ("nordic", -1, -1, "AmberBrush", "Arriving in 42 min", "Assigned · Berth 07")
        };

        Callouts.Clear();
        foreach (var (id, fx, fy, tone, line1, line2) in definitions)
        {
            var vessel = PortData.Vessel(id);
            var expanded = State.HarbourSelection == id;
            var pinned = fx < 0;

            Callouts.Add(new VesselCallout
            {
                Id = id,
                Name = vessel.Name,
                Line1 = line1,
                Line2 = line2,
                Dot = Tokens.Brush(tone),
                Border = expanded ? Tokens.Brush("DeckWhiteColor", 0.35) : Tokens.Brush("DeckWhiteColor", 0.12),
                Expanded = expanded,
                MinWidth = expanded ? 240 : 168,
                Left = pinned ? Math.Round(width - (expanded ? 292 : 220)) : Math.Round(width * fx),
                Top = pinned ? Math.Round(height - (expanded ? 216 : 112)) : Math.Round(height * fy),
                Facts = new[]
                {
                    new Fact { Key = "Arrival", Value = vessel.Eta },
                    new Fact { Key = "Departure", Value = vessel.Etd },
                    new Fact { Key = "Length · draft", Value = $"{vessel.Length} m · {vessel.Draft} m" },
                    new Fact { Key = "Security", Value = vessel.Security }
                },
                Toggle = State.PickHullVesselCommand,
                Open = State.OpenVesselCommand
            });
        }
    }
}
