using System.IO;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using Windows.Storage;

namespace Cargo.Controls.Harbour;

/// <summary>
/// The harbour as a SkiaSharp scene: an orbit camera over a true-scale model of the terminal.
/// Owns input, the camera ease and the frame timer; drawing lives in <see cref="HarbourRenderer"/>.
/// </summary>
public sealed class HarbourScene : SKCanvasElement
{
    // Camera motion (drag, glide, tween) runs at 30 fps. At rest only the ambient cranes and
    // trucks move, slowly (an 11 s crane cycle, trucks crawling a 1 km quay), so the idle rate
    // is 15 fps: the same look at half the live-layer work.
    private static readonly TimeSpan MotionFrame = TimeSpan.FromMilliseconds(33);
    private static readonly TimeSpan IdleFrame = TimeSpan.FromMilliseconds(66);

#if DEBUG
    private static readonly bool DebugOrbit = Environment.GetEnvironmentVariable("CARGO_ORBIT") == "1";
#endif

    private readonly HarbourPalette _palette = new();
    private readonly HarbourCamera _camera = new();
    private readonly HarbourWorld _world;
    private readonly HarbourRenderer _renderer;
    private readonly DispatcherTimer _timer = new() { Interval = IdleFrame };
    private readonly DateTimeOffset _started = DateTimeOffset.Now;
    private readonly Dictionary<uint, Point> _pointers = new();

    private PortState? _state;
    private UIElement _surface;
    private (HarbourCameraPose From, HarbourCameraPose To, DateTimeOffset Start, double Ms)? _tween;
    private double _yawVelocity;
    private Point _pressedAt;
    private double _dragDistance;
    private double _pinchStart;
    private double _zoomStart;
    private string? _hoveredVessel;
    private string? _selectedVessel;
    private string _layer = "port";
    private bool _typefaceRequested;

    public HarbourScene()
    {
        _surface = this;
        _world = new HarbourWorld(_palette);
        _renderer = new HarbourRenderer(_world, _palette, _camera);
        _camera.Apply(HarbourCameraPose.Overview);

        _timer.Tick += (_, _) => Tick();
        Loaded += (_, _) => LoadTypeface();

        // The stage collapses on pages without a harbour; a hidden scene should not draw 30 frames a second
        this.TrackShown(shown =>
        {
            if (shown)
            {
                Motion.Changed += OnMotionChanged;
                _timer.Start();
            }
            else
            {
                Motion.Changed -= OnMotionChanged;
                _timer.Stop();
            }
        });
    }

    /// <summary>
    /// Takes pointer input from a surface laid exactly over the scene (a transparent-background
    /// Border, which is always hit-testable), so input does not depend on the canvas element's own hit-testing.
    /// </summary>
    public void AttachInput(UIElement surface)
    {
        _surface = surface;
        surface.PointerPressed += OnPointerPressed;
        surface.PointerMoved += OnPointerMoved;
        surface.PointerReleased += OnPointerReleased;
        surface.PointerCanceled += OnPointerReleased;
        surface.PointerExited += OnPointerExited;
        surface.PointerWheelChanged += OnPointerWheelChanged;
    }

    /// <summary>Raised on the UI thread whenever the camera moves, so overlays can follow.</summary>
    public event EventHandler? PoseChanged;

    /// <summary>Raised when the user takes hold of the camera, so a preset no longer describes the view.</summary>
    public event EventHandler? Interacted;

    public PortState? State
    {
        get => _state;
        set
        {
            _state = value;
            Invalidate();
        }
    }

    /// <summary>The vessel drawn with the selection outline; each section decides whose selection that is.</summary>
    public string? SelectedVessel
    {
        get => _selectedVessel;
        set
        {
            _selectedVessel = value;
            Invalidate();
        }
    }

    /// <summary>Which overlay the still scene carries: port, yard, security or traffic.</summary>
    public string Layer
    {
        get => _layer;
        set
        {
            _layer = value;
            Invalidate();
        }
    }

    // Motion turned off mid-cycle: one redraw puts the cranes and trucks at their resting pose
    private void OnMotionChanged(object? sender, EventArgs e) => RefreshStill();

    public HarbourCamera Camera => _camera;

    public HarbourWorld World => _world;

    public bool Animate => !Motion.Reduced;

    /// <summary>Leader lines from each mast to its tag, computed by the view that owns the tags.</summary>
    public IReadOnlyDictionary<string, (SKPoint Anchor, SKPoint Tag)> Leaders
    {
        set
        {
            _renderer.Leaders = value;
            Invalidate();
        }
    }

    /// <summary>Brings the camera to the view's current size, for projections made off the render path.</summary>
    public void SyncCamera() => _camera.Update(ActualWidth, ActualHeight);

    public void RefreshStill()
    {
        _renderer.Invalidate();
        Invalidate();
    }

    // ── Camera ─────────────────────────────────────────────────────────────────

    // Camera moves are short and ease out hard (quartic): they read as responsive rather than as a
    // sequence to wait through. Section framing 420 ms, presets 450, fly-to 480, nudges 180.
    public void GoTo(HarbourCameraPose pose, double ms = 450)
    {
        _yawVelocity = 0;
        if (!Animate || ms <= 0)
        {
            _tween = null;
            _camera.Apply(pose);
            Moved();
            return;
        }

        _tween = (_camera.Pose, pose, DateTimeOffset.Now, ms);
    }

    public void FlyTo(double x, double y) =>
        GoTo(_camera.Pose with { TargetX = x, TargetY = y, Zoom = Math.Max(_camera.Zoom, 3.6) }, 480);

    public void Nudge(double yawDegrees = 0, double pitchDegrees = 0, double zoomFactor = 1)
    {
        var pose = _camera.Pose;
        GoTo(pose with
        {
            Yaw = pose.Yaw + yawDegrees * Math.PI / 180,
            Pitch = Math.Clamp(pose.Pitch + pitchDegrees * Math.PI / 180, HarbourCamera.MinPitch, HarbourCamera.MaxPitch),
            Zoom = Math.Clamp(pose.Zoom * zoomFactor, HarbourCamera.MinZoom, HarbourCamera.MaxZoom)
        }, 180);
    }

    private void Moved()
    {
        _timer.Interval = MotionFrame;
        Invalidate();
        PoseChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Frame loop ─────────────────────────────────────────────────────────────

    private void Tick()
    {
        var moved = false;

        if (_tween is { } tween)
        {
            var k = Math.Min(1, (DateTimeOffset.Now - tween.Start).TotalMilliseconds / tween.Ms);
            var eased = 1 - Math.Pow(1 - k, 4);
            _camera.Apply(tween.From.Lerp(tween.To, eased));
            if (k >= 1)
            {
                _tween = null;
            }

            moved = true;
        }

#if DEBUG
        // Measurement hook: a steady orbit runs the drag-frame path (camera moving, direct draw, no bake),
        // which synthesized input cannot reach from a background process.
        if (DebugOrbit)
        {
            _yawVelocity = .01;
        }
#endif

        if (_pointers.Count == 0 && Math.Abs(_yawVelocity) > 1e-4)
        {
            _camera.Yaw += _yawVelocity;
            _yawVelocity *= .92;
            moved = true;
        }

        if (moved)
        {
            Moved();
        }
        else if (Animate)
        {
            if (_pointers.Count == 0)
            {
                _timer.Interval = IdleFrame;
            }

            // Cranes and trucks: one frame per tick, never a held render loop.
            Invalidate();
        }
    }

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        var frame = new HarbourFrameState(_selectedVessel, _hoveredVessel, _state?.HoveredBerth, _layer, Animate);
        _renderer.Render(canvas, (float)area.Width, (float)area.Height, (DateTimeOffset.Now - _started).TotalSeconds, frame);
    }

    // ── Input ──────────────────────────────────────────────────────────────────

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_surface).Position;
        _pointers[e.Pointer.PointerId] = point;
        Interacted?.Invoke(this, EventArgs.Empty);
        _surface.CapturePointer(e.Pointer);

        if (_pointers.Count == 1)
        {
            _pressedAt = point;
            _dragDistance = 0;
            _yawVelocity = 0;
            _tween = null;
        }
        else if (_pointers.Count == 2)
        {
            var p = _pointers.Values.ToArray();
            _pinchStart = Distance(p[0], p[1]);
            _zoomStart = _camera.Zoom;
        }

        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_surface).Position;

        if (!_pointers.TryGetValue(e.Pointer.PointerId, out var previous))
        {
            Hover(point);
            return;
        }

        _pointers[e.Pointer.PointerId] = point;

        if (_pointers.Count == 2)
        {
            var p = _pointers.Values.ToArray();
            _camera.Zoom = Math.Clamp(_zoomStart * Distance(p[0], p[1]) / Math.Max(1, _pinchStart), HarbourCamera.MinZoom, HarbourCamera.MaxZoom);
            Moved();
            return;
        }

        var dx = point.X - previous.X;
        var dy = point.Y - previous.Y;
        _dragDistance += Math.Abs(dx) + Math.Abs(dy);
        _camera.Yaw -= dx * .0065;
        _yawVelocity = Animate ? -dx * .0065 : 0;
        _camera.Pitch = Math.Clamp(_camera.Pitch + dy * .005, HarbourCamera.MinPitch, HarbourCamera.MaxPitch);
        Moved();
        e.Handled = true;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_pointers.Remove(e.Pointer.PointerId))
        {
            return;
        }

        _surface.ReleasePointerCapture(e.Pointer);

        if (_pointers.Count == 0 && _dragDistance <= 4)
        {
            _yawVelocity = 0;
            SyncCamera();
            var hit = HitVessel(_pressedAt);
            if (hit is not null && _state is not null)
            {
                _state.PickHullVesselCommand.Execute(hit);
            }
        }

        e.Handled = true;
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (_pointers.Count > 0)
        {
            return;
        }

        SetHover(null, null);
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(_surface).Properties.MouseWheelDelta;
        Interacted?.Invoke(this, EventArgs.Empty);
        _tween = null;
        _camera.Zoom = Math.Clamp(_camera.Zoom * Math.Exp(delta * .0015), HarbourCamera.MinZoom, HarbourCamera.MaxZoom);
        Moved();
        e.Handled = true;
    }

    private void Hover(Point point)
    {
        SyncCamera();
        var vessel = HitVessel(point);
        var berth = vessel is null ? HitBerth(point) : null;
        SetHover(vessel, berth);
        ProtectedCursor = InputSystemCursor.Create(vessel is not null ? InputSystemCursorShape.Hand : InputSystemCursorShape.SizeAll);
    }

    private void SetHover(string? vessel, int? berth)
    {
        var changed = vessel != _hoveredVessel;
        _hoveredVessel = vessel;
        if (_state is not null && _state.HoveredBerth != berth)
        {
            _state.HoveredBerth = berth;
            changed = true;
        }

        if (changed)
        {
            Invalidate();
        }
    }

    private string? HitVessel(Point point)
    {
        string? best = null;
        var bestDepth = double.MinValue;
        foreach (var vessel in _world.Vessels)
        {
            var outline = _renderer.VesselOutline(vessel.Id);
            if (Poly.Contains(outline, (float)point.X, (float)point.Y))
            {
                var depth = _camera.Depth(vessel.Object.Anchor);
                if (depth > bestDepth)
                {
                    bestDepth = depth;
                    best = vessel.Id;
                }
            }
        }

        return best;
    }

    private int? HitBerth(Point point)
    {
        foreach (var berth in _world.Berths)
        {
            var tile = Poly.Rect(berth.X, 7.3, 92, 12.5).Select(p => _camera.Project(p.X, p.Y, 0)).ToArray();
            if (Poly.Contains(tile, (float)point.X, (float)point.Y))
            {
                return berth.Index;
            }
        }

        return null;
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    // ── Typeface ───────────────────────────────────────────────────────────────

    /// <summary>The chart and painted berth numbers use the app's IBM Plex Mono; the default face stands in until it loads.</summary>
    private async void LoadTypeface()
    {
        if (_typefaceRequested)
        {
            return;
        }

        _typefaceRequested = true;
        try
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Fonts/IBMPlexMono-Medium.ttf"));
            using var stream = await file.OpenStreamForReadAsync();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            var typeface = SKTypeface.FromData(SKData.CreateCopy(memory.ToArray()));
            if (typeface is not null)
            {
                _renderer.SetTypeface(typeface);
                Invalidate();
            }
        }
        catch (Exception)
        {
            // The default typeface is an acceptable fallback for chart text.
        }
    }
}
