using System.Windows.Input;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using Windows.System;

namespace Cargo.Controls.Harbour;

/// <summary>One vessel's data tag, bound by <c>HarbourTagTemplate</c>.</summary>
public sealed partial class HarbourTag : ObservableObject
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Line { get; init; }
    public required string AccessibleName { get; init; }
    public required Brush Foreground { get; init; }
    public required Brush SubForeground { get; init; }
    public required IReadOnlyList<Fact> Facts { get; init; }
    public required ICommand Toggle { get; init; }
    public required ICommand Open { get; init; }
    public required ICommand Hover { get; init; }

    [ObservableProperty]
    private Brush _background = Tokens.Transparent;

    [ObservableProperty]
    private Brush _edge = Tokens.Transparent;

    [ObservableProperty]
    private Thickness _edgeThickness;

    [ObservableProperty]
    private Visibility _expanded = Visibility.Collapsed;
}

/// <summary>
/// The harbour stage: the 3D scene, the data tags that ride its vessels, and the camera HUD.
/// </summary>
// xaml-lint: allow codebehind - Skia glue: tag positions follow the camera every frame; presets drive the scene camera
public sealed partial class HarbourView : UserControl
{
    private const double Lead = 30;

    private readonly HarbourScene? _scene;
    private readonly List<(HarbourTag Tag, ContentControl Host)> _tags = new();
    private PortState? _state;
    private string? _selection;
    private bool _introPlayed;

    public HarbourView()
    {
        InitializeComponent();

        if (SKCanvasElement.IsSupportedOnCurrentPlatform())
        {
            _scene = new HarbourScene();
            _scene.Camera.TopInset = TopInset;
            _scene.PoseChanged += (_, _) => PlaceOverlays();
            _scene.Interacted += (_, _) => MarkView(null);
            SceneHost.Child = _scene;
            _scene.AttachInput(SceneHost);
            ApplyStartView();
        }

        SizeChanged += (_, _) => PlaceOverlays();
        KeyDown += OnKeyDown;
        Loaded += (_, _) =>
        {
            PlayIntro();
            DispatcherQueue.TryEnqueue(PlaceOverlays);
            // The stage height depends on the window's height, which the Auto row never re-measures for
            if (XamlRoot is { } root)
            {
                root.Changed += OnXamlRootChanged;
            }
        };
        Unloaded += (_, _) => { if (XamlRoot is { } root) { root.Changed -= OnXamlRootChanged; } };
    }

    private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => InvalidateMeasure();

    public PortState? State
    {
        get => _state;
        set
        {
            _state = value;
            if (_scene is not null)
            {
                _scene.State = value;
            }

            if (value is null)
            {
                return;
            }

            _selection = EffectiveSelection;
            SyncScene();
            BuildTags();
            this.RebuildWhenVisible(value, OnStructureChanged);
            // Hover outlines draw in the live layer, so a hover never re-bakes the still frame
            this.RepaintWhenVisible(value, () =>
            {
                _scene?.Invalidate();
                PaintTags();
            });
            value.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(PortState.Section) or nameof(PortState.StageOpen) or nameof(PortState.SelectedVesselId))
                {
                    SyncScene();
                    RefreshTags();
                    Frame(animate: true);
                }
            };
        }
    }

    public static readonly DependencyProperty CompactProperty = DependencyProperty.Register(
        nameof(Compact), typeof(bool), typeof(HarbourView),
        new PropertyMetadata(false, (d, _) => ((HarbourView)d).OnModeChanged()));

    /// <summary>The same harbour as a strip above a section: no HUD, no facts cards, framed by the section.</summary>
    public bool Compact
    {
        get => (bool)GetValue(CompactProperty);
        set => SetValue(CompactProperty, value);
    }

    public static readonly DependencyProperty TopInsetProperty = DependencyProperty.Register(
        nameof(TopInset), typeof(double), typeof(HarbourView),
        new PropertyMetadata(0d, (d, e) => ((HarbourView)d).OnTopInsetChanged((double)e.NewValue)));

    /// <summary>
    /// Height of an overlay laid over the top of the stage (the Overview greeting). The stage grows
    /// by it and the scene is framed below it, so the overlay sits on the stage without covering
    /// the harbour, its tags or its HUD.
    /// </summary>
    public double TopInset
    {
        get => (double)GetValue(TopInsetProperty);
        set => SetValue(TopInsetProperty, value);
    }

    public static readonly DependencyProperty LegendMarginProperty = DependencyProperty.Register(
        nameof(LegendMargin), typeof(Thickness), typeof(HarbourView), new PropertyMetadata(new Thickness(24, 16, 24, 16)));

    public Thickness LegendMargin
    {
        get => (Thickness)GetValue(LegendMarginProperty);
        private set => SetValue(LegendMarginProperty, value);
    }

    private void OnTopInsetChanged(double inset)
    {
        LegendMargin = new Thickness(24, 16 + inset, 24, 16);
        if (_scene is not null)
        {
            _scene.Camera.TopInset = inset;
            _scene.RefreshStill();
        }

        InvalidateMeasure();
        PlaceOverlays();
    }

    public static readonly DependencyProperty HudVisibilityProperty = DependencyProperty.Register(
        nameof(HudVisibility), typeof(Visibility), typeof(HarbourView), new PropertyMetadata(Visibility.Visible));

    public Visibility HudVisibility
    {
        get => (Visibility)GetValue(HudVisibilityProperty);
        private set => SetValue(HudVisibilityProperty, value);
    }

    /// <summary>
    /// The stage keeps a landscape proportion, bounded so it never swallows the page: at most 58% of
    /// the window's height (the charts below keep ~40% to scroll in), never less than 300 px of harbour
    /// under the greeting. The strip is fixed.
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) || availableSize.Width <= 0 ? 1328 : availableSize.Width;
        var byWidth = Math.Clamp(width * .42, 380, 640) + TopInset;
        var byWindow = XamlRoot is { } root ? Math.Max(TopInset + 300, root.Size.Height * .58) : byWidth;
        var size = new Size(width, Compact ? 210 : Math.Min(byWidth, byWindow));
        base.MeasureOverride(size);
        return size;
    }

    // ── State ──────────────────────────────────────────────────────────────────

    /// <summary>Berths has its own selection (the vessel being worked); elsewhere the harbour's selection applies.</summary>
    private string? EffectiveSelection =>
        _state is null ? null : _state.Section == "berths" ? _state.SelectedVesselId : _state.HarbourSelection;

    /// <summary>Security shows the perimeter and Cargo the yard zones; on Overview the layer switcher decides.</summary>
    private string EffectiveLayer => _state?.Section switch
    {
        null => "port",
        "security" => "security",
        "cargo" => "yard",
        _ => _state.HarbourLayer
    };

    private void SyncScene()
    {
        if (_scene is null)
        {
            return;
        }

        _scene.SelectedVessel = EffectiveSelection;
        _scene.Layer = EffectiveLayer;
    }

    private void OnModeChanged()
    {
        HudVisibility = Compact ? Visibility.Collapsed : Visibility.Visible;
        InvalidateMeasure();
        RefreshTags();
        Frame(animate: true);
    }

    private void OnStructureChanged()
    {
        if (_state is null)
        {
            return;
        }

        SyncScene();
        var selection = EffectiveSelection;
        if (selection != _selection)
        {
            _selection = selection;
            var vessel = _scene?.World.Vessels.FirstOrDefault(v => v.Id == selection);
            if (vessel is not null && !Compact)
            {
                _scene!.FlyTo(vessel.X, vessel.Y);
            }
        }

        RefreshTags();
        _scene?.RefreshStill();
    }

    /// <summary>
    /// Moving between sections moves the camera over the same harbour: the wide shot on
    /// Overview, the selected ship on Berths, the yard on Cargo, the terminal perimeter on Security.
    /// </summary>
    private void Frame(bool animate)
    {
        if (_scene is null || _state is null)
        {
            return;
        }

        static double Rad(double d) => d * Math.PI / 180;
        var ms = animate ? 420 : 0;

        if (!Compact)
        {
            if (_state.Section == "overview")
            {
                _scene.GoTo(HarbourCameraPose.Overview, ms);
                MarkView("overview");
            }

            return;
        }

        var vessel = _scene.World.Vessels.FirstOrDefault(v => v.Id == EffectiveSelection);
        var pose = _state.Section == "security"
            ? new HarbourCameraPose(Rad(-8), Rad(36), 2.4, 40, -40)
            : _state.Section == "cargo"
                ? new HarbourCameraPose(Rad(-6), Rad(42), 3.0, 0, -66)
            : vessel is not null
                ? new HarbourCameraPose(Rad(-14), Rad(26), 4.6, vessel.X, vessel.Y + 2)
                : HarbourCameraPose.Overview with { Pitch = Rad(26) };
        _scene.GoTo(pose, ms);
    }

    // ── Tags ───────────────────────────────────────────────────────────────────

    private void BuildTags()
    {
        foreach (var (_, host) in _tags)
        {
            TagLayer.Children.Remove(host);
        }

        _tags.Clear();
        if (_state is null || _scene is null)
        {
            return;
        }

        var template = (DataTemplate)Resources["HarbourTagTemplate"];
        foreach (var shape in _scene.World.Vessels)
        {
            var vessel = PortData.Vessel(shape.Id);
            var needs = vessel.Status == "Arriving";
            var tag = new HarbourTag
            {
                Id = vessel.Id,
                Title = vessel.Name.ToUpperInvariant(),
                Line = TagLine(vessel),
                AccessibleName = $"{vessel.Name}, {vessel.StatusLine}",
                Foreground = Tokens.Brush("InkInvariantBrush"),
                SubForeground = needs ? Tokens.Brush("InkInvariantBrush") : Tokens.Brush("TextMutedInvariantBrush"),
                Facts = new[]
                {
                    new Fact { Key = "Arrival", Value = vessel.Eta },
                    new Fact { Key = "Departure", Value = vessel.Etd },
                    new Fact { Key = "Length · draft", Value = $"{vessel.Length} m · {vessel.Draft} m" },
                    new Fact { Key = "Security", Value = vessel.Security }
                },
                Toggle = _state.PickHullVesselCommand,
                Open = _state.OpenVesselCommand,
                Hover = _state.HoverCommand
            };

            var host = new ContentControl { Content = tag, ContentTemplate = template, IsTabStop = false, RenderTransform = new TranslateTransform() };
            TagLayer.Children.Add(host);
            _tags.Add((tag, host));
        }

        RefreshTags();
    }

    private static string TagLine(Vessel v)
    {
        static string Clock(string s) => s.Replace("Today ", string.Empty).ToUpperInvariant();
        return v.Status switch
        {
            // The berth leads: a selected vessel's tag sits over its painted berth number
            "Docked" when v.UnloadPercent is > 0 and < 100 => $"B{v.HomeBerth} · DISCH {v.UnloadPercent}% · ETD {Clock(v.Etd)}",
            "Docked" => $"B{v.HomeBerth} · LOADING {v.LoadPercent}% · ETD {Clock(v.Etd)}",
            "Departing" => $"B{v.HomeBerth} · DEPARTING · {Clock(v.Etd)}",
            "Arriving" => $"ETA {Clock(v.Eta)} · B{v.HomeBerth}",
            _ => "ANCHORED · UNASSIGNED"
        };
    }

    private void RefreshTags()
    {
        PaintTags();
        DispatcherQueue.TryEnqueue(PlaceOverlays);
        UpdateSummary();
    }

    /// <summary>Tag chrome only: selected takes a 2 px ink edge, a linked hover a 1 px one.</summary>
    private void PaintTags()
    {
        foreach (var (tag, _) in _tags)
        {
            var needs = PortData.Vessel(tag.Id).Status == "Arriving";
            var selected = EffectiveSelection == tag.Id;
            var lit = _state?.HoveredVessel == tag.Id;
            tag.Background = needs ? Tokens.Brush("AmberInvariantBrush") : Tokens.Brush("SurfaceInvariantBrush");
            // The hairline comes straight from the resource: Tokens.Brush() drops brush opacity (audit C1).
            tag.Edge = selected || lit ? Tokens.Brush("InkInvariantBrush")
                : needs ? Tokens.Brush("AmberDeepInvariantBrush")
                : (Brush)Application.Current.Resources["HairlineStrongInvariantBrush"];
            tag.EdgeThickness = new Thickness(selected ? 2 : 1);
            tag.Expanded = selected && !Compact ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // ── Screen readers ─────────────────────────────────────────────────────────

    private string _viewName = "Overview";

    /// <summary>
    /// The canvas itself is opaque to assistive tech, so the harbour describes itself in one line:
    /// the view, the layer, what is alongside and what is selected. It is the view's description
    /// on focus, and a polite live region announces it when a preset, layer or selection changes.
    /// </summary>
    private void UpdateSummary()
    {
        if (_state is null)
        {
            return;
        }

        var vessels = _tags.Select(t => PortData.Vessel(t.Tag.Id)).ToList();
        var alongside = vessels.Count(v => v.Status is "Docked" or "Departing");
        var arriving = vessels.Count(v => v.Status == "Arriving");
        var selected = vessels.FirstOrDefault(v => v.Id == EffectiveSelection);
        var layer = EffectiveLayer switch { "yard" => "Yard", "security" => "Security", "traffic" => "Traffic", _ => "Map" };
        var text = $"{_viewName} view, {layer} layer. {alongside} vessels alongside, {arriving} arriving."
            + (selected is null ? string.Empty : $" Selected: {selected.Name}, {selected.StatusLine}.");
        if (text == Summary.Text)
        {
            return;
        }

        Summary.Text = text;
        AutomationProperties.SetFullDescription(this, text);
        FrameworkElementAutomationPeer.FromElement(Summary)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    /// <summary>
    /// Tags sit on a short leader above each mast and stay inside the frame; when a vessel is near
    /// an edge the leader angles over to reach its tag.
    /// </summary>
    // xaml-lint: allow responsive - ActualWidth/Height clamp tags inside the frame; this is projection, not a breakpoint
    private void PlaceOverlays()
    {
        if (_scene is null || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        Clip = new RectangleGeometry { Rect = new Rect(0, 0, ActualWidth, ActualHeight) };
        _scene.SyncCamera();
        var camera = _scene.Camera;
        var leaders = new Dictionary<string, (SKPoint, SKPoint)>();
        var placed = new List<Rect>();

        // Left to right, so a later tag gives way to one already placed
        foreach (var (tag, host) in _tags.OrderBy(t => camera.Project(_scene.World.Vessels.First(v => v.Id == t.Tag.Id).Mast).X))
        {
            var vessel = _scene.World.Vessels.First(v => v.Id == tag.Id);
            var (x, y) = camera.Project(vessel.Mast);

            // A vessel out of frame loses its tag rather than pinning it to an edge it is not near.
            // xaml-lint: allow responsive - per-frame projection culling, not a breakpoint
            var inFrame = x > 0 && x < ActualWidth && y > (Compact ? 16 : 40 + TopInset) && y < ActualHeight - (Compact ? 8 : 56);
            // xaml-lint: allow codebehind - per-frame projection culling; there is no XAML surface for the camera
            host.Visibility = inFrame ? Visibility.Visible : Visibility.Collapsed;
            if (!inFrame)
            {
                continue;
            }

            // xaml-lint: allow responsive - the tag's own measured size, used to place it on its leader
            var width = host.ActualWidth > 0 ? host.ActualWidth : 160;
            // xaml-lint: allow responsive - the tag's own measured size, used to place it on its leader
            var height = host.ActualHeight > 0 ? host.ActualHeight : 36;
            // The strip keeps its top-right corner clear for the Expand harbour button.
            var right = Compact ? 150 : 10;
            var left = Math.Clamp(x - width / 2, 10, Math.Max(10, ActualWidth - width - right));
            var minTop = Compact ? 6 : 52;
            var top = Math.Max(minTop, y - Lead - height);
            (left, top) = AvoidOverlap(placed, left, top, width, height, minTop, ActualWidth - right);
            placed.Add(new Rect(left, top, width, height));

            var transform = (TranslateTransform)host.RenderTransform;
            transform.X = Math.Round(left);
            transform.Y = Math.Round(top);
            leaders[tag.Id] = (new SKPoint(x, y), new SKPoint((float)(left + width / 2), (float)(top + height)));
        }

        _scene.Leaders = leaders;

        // North needle and a scale bar in round metres.
        var a = camera.Project(camera.TargetX, camera.TargetY, 0);
        var b = camera.Project(camera.TargetX, camera.TargetY - 100, 0);
        Needle.Angle = Math.Atan2(b.Y - a.Y, b.X - a.X) * 180 / Math.PI + 90;

        var pxPerMetre = camera.Scale / HarbourWorld.MetresPerUnit;
        var metres = new[] { 25, 50, 100, 200, 250, 500, 1000 }.LastOrDefault(m => m * pxPerMetre <= 130, 25);
        ScaleBar.Width = Math.Max(8, Math.Round(metres * pxPerMetre));
        ScaleLabel.Text = $"{metres} m";
    }

    /// <summary>
    /// Vessels alongside neighbouring berths project close together, most of all in the compact
    /// strip, so a tag that lands on one already placed moves up above it (its leader grows). When
    /// there is no room above, it moves beside it instead.
    /// </summary>
    private static (double Left, double Top) AvoidOverlap(List<Rect> placed, double left, double top,
        double width, double height, double minTop, double maxRight)
    {
        const double Gap = 6;
        for (var guard = 0; guard < placed.Count + 1; guard++)
        {
            var hit = placed.FirstOrDefault(r => left < r.Right + Gap && left + width + Gap > r.Left
                && top < r.Bottom + Gap && top + height + Gap > r.Top, Rect.Empty);
            if (hit.IsEmpty)
            {
                break;
            }

            if (hit.Top - Gap - height >= minTop)
            {
                top = hit.Top - Gap - height;
            }
            else
            {
                left = Math.Min(hit.Right + Gap, Math.Max(10, maxRight - width));
                if (left + width > maxRight)
                {
                    break;
                }
            }
        }

        return (left, top);
    }

    // ── Camera controls ────────────────────────────────────────────────────────

    [RelayCommand]
    private void View(string name)
    {
        if (_scene is null)
        {
            return;
        }

        var pose = _scene.Camera.Pose;
        _scene.GoTo(name switch
        {
            "sea" => HarbourCameraPose.FromSea(pose),
            "land" => HarbourCameraPose.FromLand(pose),
            "plan" => HarbourCameraPose.Plan(pose),
            _ => HarbourCameraPose.Overview
        });
        MarkView(name);
    }

    [RelayCommand]
    private void Zoom(string direction)
    {
        _scene?.Nudge(zoomFactor: direction == "in" ? 1.3 : 1 / 1.3);
        MarkView(null);
    }

    private void MarkView(string? name)
    {
        _viewName = name switch
        {
            "overview" => "Overview",
            "sea" => "From sea",
            "land" => "From land",
            "plan" => "Plan",
            _ => "Custom"
        };
        UpdateSummary();

        var current = (Style)Application.Current.Resources["HarbourSegmentCurrent"];
        var normal = (Style)Application.Current.Resources["HarbourSegment"];
        OverviewButton.Style = name == "overview" ? current : normal;
        SeaButton.Style = name == "sea" ? current : normal;
        LandButton.Style = name == "land" ? current : normal;
        PlanButton.Style = name == "plan" ? current : normal;
    }

    // xaml-lint: allow codebehind - keyboard orbit for the Skia canvas; the camera has no XAML surface
    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_scene is null)
        {
            return;
        }

        switch (e.Key)
        {
            case VirtualKey.Left: _scene.Nudge(yawDegrees: 15); break;
            case VirtualKey.Right: _scene.Nudge(yawDegrees: -15); break;
            case VirtualKey.Up: _scene.Nudge(pitchDegrees: 6); break;
            case VirtualKey.Down: _scene.Nudge(pitchDegrees: -6); break;
            case VirtualKey.Add or (VirtualKey)187: _scene.Nudge(zoomFactor: 1.25); break;
            case VirtualKey.Subtract or (VirtualKey)189: _scene.Nudge(zoomFactor: 1 / 1.25); break;
            case VirtualKey.Number0 or VirtualKey.NumberPad0: View("overview"); e.Handled = true; return;
            default: return;
        }

        MarkView(null);
        e.Handled = true;
    }

    /// <summary>A slow swing into the overview shows the harbour is a space you can move through.</summary>
    private void PlayIntro()
    {
        if (_introPlayed || _scene is null || Compact)
        {
            return;
        }

        _introPlayed = true;
        if (!_scene.Animate || Environment.GetEnvironmentVariable("CARGO_HARBOUR_VIEW") is { Length: > 0 })
        {
            return;
        }

        var target = _scene.Camera.Pose;
        _scene.Camera.Apply(target with { Yaw = target.Yaw - 40 * Math.PI / 180 });
        _scene.GoTo(target, 700);
    }

    /// <summary>Lets a verification run photograph a preset without synthesised input.</summary>
    [System.Diagnostics.Conditional("DEBUG")]
    private void ApplyStartView()
    {
        var view = Environment.GetEnvironmentVariable("CARGO_HARBOUR_VIEW");
        if (string.IsNullOrEmpty(view) || _scene is null)
        {
            return;
        }

        var pose = HarbourCameraPose.Overview;
        _scene.GoTo(view switch
        {
            "sea" => HarbourCameraPose.FromSea(pose),
            "land" => HarbourCameraPose.FromLand(pose),
            "plan" => HarbourCameraPose.Plan(pose),
            _ => pose
        }, 0);
        MarkView(view);
    }
}
