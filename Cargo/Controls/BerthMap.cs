using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Cargo.Controls;

/// <summary>
/// The berth planner map. Inbound vessels carry a dashed ring and can be dragged onto a
/// berth; berths light green while a drop would fit and red where the draft or the window
/// does not work. Drawn in a 1200 × 460 space and scaled to the panel.
/// </summary>
public sealed partial class BerthMap : SceneHost
{
    private const double BerthSpacing = 144;
    private const double BerthLeft = 44;

    private readonly PortState _state;

    // Kept from the last build so a drag repaints them instead of rebuilding the scene.
    private readonly Microsoft.UI.Xaml.Shapes.Rectangle?[] _boxes = new Microsoft.UI.Xaml.Shapes.Rectangle?[8];
    private readonly TextBlock?[] _statusLabels = new TextBlock?[8];
    private readonly (string Status, bool Conflict, bool Selected)[] _berthInfo = new (string, bool, bool)[8];
    private readonly Dictionary<string, (FrameworkElement Hull, FrameworkElement Name)> _hulls = new();
    private int _paintedHover = -2;
    private string? _lastHint;

    private string? _dragId;
    private Point _dragPoint;
    private Point _dragOffset;
    private Point _dragStart;
    private bool _dragMoved;

    public BerthMap(PortState state) : base(1200, 460)
    {
        _state = state;

        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += (_, _) => CompleteDrop();
        PointerExited += (_, _) => CompleteDrop();

        Build();
        this.RebuildWhenVisible(state, Build);
    }

    /// <summary>Raised whenever the hint strip under the map should change.</summary>
    public event EventHandler<string>? HintChanged;

    private static double BerthX(int index) => BerthLeft + index * BerthSpacing;

    private int BerthUnder(Point point)
    {
        for (var i = 0; i < PortData.Berths.Count; i++)
        {
            var x = BerthX(i);
            if (point.X >= x - 8 && point.X <= x + 140 && point.Y >= 110 && point.Y <= 270)
            {
                return i;
            }
        }

        return -1;
    }

    private Point ToScene(PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(this).Position;
        var scale = ActualWidth <= 0 ? 1 : ActualWidth / SceneWidth;
        return new Point(position.X / scale, position.Y / scale);
    }

    // ── Drawing ───────────────────────────────────────────────────────────────

    private void Build()
    {
        Scene.Children.Clear();
        _hulls.Clear();

        var planHour = _state.EffectivePlanHour;
        var conflicts = _state.Conflicts();

        DrawGround();
        DrawBerths(planHour, conflicts);
        DrawVessels(planHour);
        _paintedHover = -2;
        PaintBerths();
    }

    private void DrawGround()
    {
        Scene.Place(Draw.Rect(0, 0, 1200, 460, Tokens.Brush("LandInvariantBrush")));
        Scene.Place(Draw.Rect(0, 140, 1200, 320, Tokens.Brush("ShallowsInvariantBrush")));

        // Terminal apron behind the quay
        Scene.Place(Draw.Rect(0, 0, 1200, 128, Tokens.Brush("LandInvariantBrush")));
        for (var g = 0; g < 12; g++)
        {
            for (var i = 0; i < 5; i++)
            {
                for (var j = 0; j < 3; j++)
                {
                    if (Geo.H(g * 13 + i * 5 + j) < 0.2)
                    {
                        continue;
                    }

                    Scene.Place(Draw.Rect(28 + g * 98 + i * 15, 20 + j * 13, 13, 10,
                        Tokens.Brush("CargoStandardColor", 0.35), 1));
                }
            }
        }

        Scene.Place(Draw.Rect(0, 128, 1200, 8, Tokens.Brush("QuayInvariantBrush")));
        Scene.Place(Draw.Rect(0, 135, 1200, 2, Tokens.Brush("QuayEdgeInvariantBrush")));

        // Swell in the basin
        var swell = Tokens.Brush("DeckWhiteColor", 0.35);
        foreach (var (x, y) in new[] { (100d, 330d), (420d, 400d), (1000d, 300d) })
        {
            Scene.Place(Draw.Shape($"M{x} {y} c20 -6 40 -6 60 0 c20 6 40 6 60 0 c20 -6 40 -6 60 0",
                null, swell, 1.5));
        }

        // Anchorage
        Scene.Place(Draw.Text("ANCHORAGE", 700, 346, 12, Tokens.Brush("TextMutedInvariantBrush"), "BodyMediumFont"));
        Scene.Place(Draw.Rule(700, 440, 1170, 440, Tokens.Brush("TextFaintInvariantBrush"), 1, dash: Draw.Dash(3, 5)));

        // Nordic Star's approach
        var approach = Draw.Shape("M700 392 C 760 330 860 260 930 222", null, Tokens.Brush("AccentInvariantBrush"), 1.5);
        approach.StrokeDashArray = Draw.Dash(6, 8);
        Scene.Place(approach);
    }

    private void DrawBerths(double planHour, IReadOnlyList<(string Berth, string A, string B)> conflicts)
    {
        for (var i = 0; i < PortData.Berths.Count; i++)
        {
            var berth = PortData.Berths[i];
            var x = BerthX(i);
            var occupied = _state.OccupantAt(berth.Number, planHour) is not null;
            var incoming = _state.IncomingAt(berth.Number, planHour) is not null;
            var conflict = conflicts.Any(c => c.Berth == berth.Number);

            var status = berth.State == "restricted" ? "restricted"
                : occupied ? "occupied"
                : incoming ? "reserved"
                : "available";

            var selected = _state.DockSelection is { } id && _state.BerthOf(id) == berth.Number;
            _berthInfo[i] = (status, conflict, selected);

            _boxes[i] = Scene.Place(Draw.Rect(x, 146, 132, 70, Tokens.Transparent, 8, Tokens.Transparent, 1.2));
            Scene.Place(Draw.Text(berth.Number, x, 228, 16, Tokens.Brush("InkInvariantBrush"),
                "MonoMediumFont", TextAlignment.Center, 132));
            _statusLabels[i] = Scene.Place(Draw.Text(conflict ? "Conflict" : char.ToUpperInvariant(status[0]) + status[1..],
                x, 248, 14, Tokens.Transparent, "BodyMediumFont", TextAlignment.Center, 132));
            Scene.Place(Draw.Text($"{berth.Depth:0.0} m", x, 218, 10.5, Tokens.Brush("TextFaintInvariantBrush"),
                "BodyFont", TextAlignment.Center, 132));
        }
    }

    /// <summary>
    /// Berth colours depend on the drag (fits / cannot take it) and the berth under the pointer.
    /// This only restyles the eight existing boxes, and only when the berth under the pointer changes.
    /// </summary>
    private void PaintBerths()
    {
        var dragVessel = _dragId is null ? null : PortData.Vessel(_dragId);
        var hover = _dragId is null ? -1 : BerthUnder(_dragPoint);
        if (hover == _paintedHover && _paintedHover != -2)
        {
            return;
        }

        _paintedHover = hover;
        for (var i = 0; i < PortData.Berths.Count; i++)
        {
            if (_boxes[i] is not { } box || _statusLabels[i] is not { } label)
            {
                continue;
            }

            var (status, conflict, selected) = _berthInfo[i];
            var isHover = hover == i;
            var fits = dragVessel is not null && _state.Fits(dragVessel, i);

            var toneToken = conflict ? "AlertColor" : StatusToken(status);
            if (dragVessel is not null)
            {
                toneToken = fits ? "AccentColor" : "RestrictedColor";
            }

            var tone = Tokens.Color(toneToken);
            var fillOpacity = isHover && fits ? 0.45
                : selected ? 0.35
                : dragVessel is not null && fits ? 0.22
                : status == "available" ? 0.14
                : 0.18;

            box.Fill = Tokens.Of(tone, fillOpacity);
            box.Stroke = Tokens.Of(isHover && dragVessel is not null ? Tokens.Color("InkColor") : tone);
            box.StrokeThickness = isHover || selected || conflict ? 2.5 : 1.2;
            box.StrokeDashArray = status == "reserved" || (dragVessel is not null && fits) ? Draw.Dash(6, 5) : null;
            label.Foreground = Tokens.Of(tone);
        }
    }

    private static string StatusToken(string status) => status switch
    {
        "occupied" => "AccentColor",
        "reserved" => "AccentColor",
        "restricted" => "RestrictedColor",
        _ => "AccentColor"
    };

    private void DrawVessels(double planHour)
    {
        foreach (var vessel in PortData.Vessels)
        {
            var berth = _state.BerthOf(vessel.Id);
            var index = berth is null ? -1 : PortData.Berths.ToList().FindIndex(b => b.Number == berth);
            var window = PortData.Schedule[vessel.Id];
            var present = window.Start <= planHour && window.End > planHour;
            var upcoming = window.Start > planHour;
            var dragging = _dragId == vessel.Id;

            double x, y, opacity = 1;

            if (dragging)
            {
                x = _dragPoint.X - _dragOffset.X;
                y = _dragPoint.Y - _dragOffset.Y;
            }
            else if (index >= 0 && present)
            {
                x = BerthX(index) - 14;
                y = 160;
            }
            else if (index >= 0 && upcoming && vessel.Id == "nordic" && !_state.IsAssigned(vessel.Id))
            {
                x = 700;
                y = 372;
                opacity = 0.9;
            }
            else if (index >= 0 && upcoming)
            {
                x = BerthX(index) - 14;
                y = 160;
                opacity = 0.45;
            }
            else if (!present && !upcoming)
            {
                continue;
            }
            else
            {
                x = 980;
                y = 372;
            }

            var movable = vessel.Status != "Docked" && vessel.Status != "Departing";
            var selected = _state.DockSelection == vessel.Id;

            var host = new Grid { Width = 160, Height = 30 };
            host.Children.Add(new Microsoft.UI.Xaml.Shapes.Rectangle
            {
                RadiusX = 20,
                RadiusY = 20,
                Margin = new Thickness(-6),
                Stroke = selected || dragging
                    ? Tokens.Brush("InkInvariantBrush")
                    : movable ? Tokens.Brush("InkColor", 0.35) : Tokens.Transparent,
                StrokeThickness = selected || dragging ? 2.5 : movable ? 1.2 : 0,
                StrokeDashArray = movable && !selected && !dragging ? Draw.Dash(5, 4) : null
            });
            host.Children.Add(new Image
            {
                Source = Sprites.Source(Sprites.Plan(vessel.Id)),
                Stretch = Stretch.Fill,
                Width = 160,
                Height = 30
            });

            host.Opacity = opacity;
            host.PointerPressed += (_, e) => BeginDrag(vessel, movable, e, Canvas.GetLeft(host), Canvas.GetTop(host));
            Scene.Place(host.At(x, y));

            var name = Scene.Place(Draw.Label(vessel.Name, x + 80, y + 36, 15, Tokens.Brush("InkInvariantBrush"),
                "BodyStrongFont", TextAlignment.Center, 220));
            _hulls[vessel.Id] = (host, name);
        }
    }

    // ── Dragging ──────────────────────────────────────────────────────────────

    private void BeginDrag(Vessel vessel, bool movable, PointerRoutedEventArgs e, double x, double y)
    {
        // A movable hull selects on release instead: selecting on press opens the side panel,
        // which narrows the map under the pointer and sends the drop to the wrong berth.
        if (!movable)
        {
            Select(vessel.Id);
            return;
        }

        var point = ToScene(e);
        _dragId = vessel.Id;
        _dragPoint = point;
        _dragStart = point;
        _dragOffset = new Point(point.X - x, point.Y - y);
        _dragMoved = false;
        CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void Select(string id)
    {
        if (_dragMoved)
        {
            return;
        }

        _state.DockSelection = _state.DockSelection == id ? null : id;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragId is null)
        {
            return;
        }

        _dragPoint = ToScene(e);
        if (Math.Abs(_dragPoint.X - _dragStart.X) + Math.Abs(_dragPoint.Y - _dragStart.Y) > 4)
        {
            _dragMoved = true;
        }

        var target = BerthUnder(_dragPoint);
        var vessel = PortData.Vessel(_dragId);
        var hint = target >= 0
            ? _state.Fits(vessel, target)
                ? $"Release to assign Berth {PortData.Berths[target].Number}"
                : $"Berth {PortData.Berths[target].Number} can't take {vessel.Name}"
            : "Drop on a green berth";
        if (hint != _lastHint)
        {
            _lastHint = hint;
            HintChanged?.Invoke(this, hint);
        }

        // Move the dragged hull and its name; everything else stays put.
        if (_hulls.TryGetValue(_dragId, out var hull))
        {
            var x = _dragPoint.X - _dragOffset.X;
            var y = _dragPoint.Y - _dragOffset.Y;
            hull.Hull.At(x, y);
            hull.Name.At(x + 80 - 110, y + 36); // the name is centred in a 220-wide box under the hull
        }

        PaintBerths();
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_dragId is not null)
        {
            _dragPoint = ToScene(e);
        }

        CompleteDrop();
        ReleasePointerCaptures();
    }

    private void CompleteDrop()
    {
        if (_dragId is null)
        {
            return;
        }

        var id = _dragId;
        var target = BerthUnder(_dragPoint);
        var assigned = target >= 0 && _state.Fits(PortData.Vessel(id), target);
        var clicked = !_dragMoved;
        _dragId = null;
        _lastHint = null;
        _dragMoved = false;

        if (clicked)
        {
            Select(id);
            return;
        }

        if (target >= 0)
        {
            var message = _state.Assign(id, target);
            if (message is not null)
            {
                HintChanged?.Invoke(this, message);
            }
        }

        // A successful assignment is a structural change, which rebuilds the map once;
        // otherwise the hull goes back to where it came from.
        if (!assigned)
        {
            Build();
        }
    }
}
