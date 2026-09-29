using System.Windows.Input;
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
        };
    }

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

            _selection = value.HarbourSelection;
            BuildTags();
            this.RebuildWhenVisible(value, OnStructureChanged);
            this.RepaintWhenVisible(value, () => _scene?.RefreshStill());
        }
    }

    /// <summary>The stage keeps a landscape proportion, bounded so it never swallows the page.</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) || availableSize.Width <= 0 ? 1328 : availableSize.Width;
        var size = new Size(width, Math.Clamp(width * .42, 380, 640));
        base.MeasureOverride(size);
        return size;
    }

    // ── State ──────────────────────────────────────────────────────────────────

    private void OnStructureChanged()
    {
        if (_state is null)
        {
            return;
        }

        if (_state.HarbourSelection != _selection)
        {
            _selection = _state.HarbourSelection;
            var vessel = _scene?.World.Vessels.FirstOrDefault(v => v.Id == _selection);
            if (vessel is not null)
            {
                _scene!.FlyTo(vessel.X, vessel.Y);
            }
        }

        RefreshTags();
        _scene?.RefreshStill();
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
                Foreground = Tokens.Brush("InkBrush"),
                SubForeground = needs ? Tokens.Brush("InkBrush") : Tokens.Brush("TextMutedBrush"),
                Facts = new[]
                {
                    new Fact { Key = "Arrival", Value = vessel.Eta },
                    new Fact { Key = "Departure", Value = vessel.Etd },
                    new Fact { Key = "Length · draft", Value = $"{vessel.Length} m · {vessel.Draft} m" },
                    new Fact { Key = "Security", Value = vessel.Security }
                },
                Toggle = _state.PickHullVesselCommand,
                Open = _state.OpenVesselCommand
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
            "Docked" when v.UnloadPercent is > 0 and < 100 => $"DISCH {v.UnloadPercent}% · ETD {Clock(v.Etd)}",
            "Docked" => $"LOADING {v.LoadPercent}% · ETD {Clock(v.Etd)}",
            "Departing" => $"DEPARTING · {Clock(v.Etd)}",
            "Arriving" => $"ETA {Clock(v.Eta)} · B{v.HomeBerth}",
            _ => "ANCHORED · UNASSIGNED"
        };
    }

    private void RefreshTags()
    {
        foreach (var (tag, _) in _tags)
        {
            var needs = PortData.Vessel(tag.Id).Status == "Arriving";
            var selected = _state?.HarbourSelection == tag.Id;
            tag.Background = needs ? Tokens.Brush("AmberBrush") : Tokens.Brush("SurfaceBrush");
            // The hairline comes straight from the resource: Tokens.Brush() drops brush opacity (audit C1).
            tag.Edge = selected ? Tokens.Brush("InkBrush")
                : needs ? Tokens.Brush("AmberDeepBrush")
                : (Brush)Application.Current.Resources["HairlineStrongBrush"];
            tag.EdgeThickness = new Thickness(selected ? 2 : 1);
            tag.Expanded = selected ? Visibility.Visible : Visibility.Collapsed;
        }

        DispatcherQueue.TryEnqueue(PlaceOverlays);
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

        foreach (var (tag, host) in _tags)
        {
            var vessel = _scene.World.Vessels.First(v => v.Id == tag.Id);
            var (x, y) = camera.Project(vessel.Mast);

            // A vessel out of frame loses its tag rather than pinning it to an edge it is not near.
            // xaml-lint: allow responsive - per-frame projection culling, not a breakpoint
            var inFrame = x > 0 && x < ActualWidth && y > 40 && y < ActualHeight - 56;
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
            var left = Math.Clamp(x - width / 2, 10, Math.Max(10, ActualWidth - width - 10));
            var top = Math.Max(52, y - Lead - height);

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
        if (_introPlayed || _scene is null)
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
        _scene.GoTo(target, 1300);
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
