using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Cargo.Controls;

/// <summary>
/// The 120 km waterway from Westhaven (km 0, right) up to Alder Basin. Vessels are placed
/// by chainage, so a boat that reports a lower speed visibly falls behind on the map.
/// </summary>
public sealed partial class RiverMap : SceneHost
{
    private readonly PortState _state;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DateTimeOffset _origin = DateTimeOffset.Now;
    private readonly List<(RiverVessel Vessel, Button Hull, RotateTransform Heading, FrameworkElement Tag, FrameworkElement Dot, FrameworkElement Name, double Width)> _boats = new();

    private double[]? _cumulative;
    private double _totalLength;

    public RiverMap(PortState state) : base(1440, 400)
    {
        _state = state;

        // Boats move on each tick; the river, gauges and locks are built once.
        _timer.Tick += (_, _) => PlaceFleet();
        Loaded += (_, _) => _timer.Start();
        Unloaded += (_, _) => _timer.Stop();

        Build();
        this.RebuildWhenVisible(state, Build);
    }

    /// <summary>Demo time-lapse: an hour of river passage every 90 seconds.</summary>
    private double ElapsedHours => (DateTimeOffset.Now - _origin).TotalHours * 40;

    // ── Chainage ──────────────────────────────────────────────────────────────

    private void MeasureRiver()
    {
        if (_cumulative is not null)
        {
            return;
        }

        _cumulative = new double[PortData.River.Count];
        for (var i = 1; i < PortData.River.Count; i++)
        {
            var a = PortData.River[i - 1];
            var b = PortData.River[i];
            _totalLength += Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
            _cumulative[i] = _totalLength;
        }
    }

    public (Point Position, double Angle, Point Normal) At(double km)
    {
        MeasureRiver();

        var distance = Math.Clamp(km / PortData.RiverKm, 0, 1) * _totalLength;
        var i = 1;
        while (i < _cumulative!.Length - 1 && _cumulative[i] < distance)
        {
            i++;
        }

        var a = PortData.River[i - 1];
        var b = PortData.River[i];
        var span = _cumulative[i] - _cumulative[i - 1];
        var f = span <= 0 ? 0 : (distance - _cumulative[i - 1]) / span;

        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length <= 0)
        {
            length = 1;
        }

        return (
            new Point(a.X + dx * f, a.Y + dy * f),
            Math.Atan2(dy, dx) * 180 / Math.PI,
            new Point(-dy / length, dx / length));
    }

    // ── Drawing ───────────────────────────────────────────────────────────────

    private void Build()
    {
        Scene.Children.Clear();
        MeasureRiver();

        Scene.Place(Draw.Rect(0, 0, 1440, 400, Tokens.Brush("LandBrush")));
        Scene.Place(Draw.Rect(0, 0, 1440, 60, Tokens.Brush("RiverBankBrush")));
        Scene.Place(Draw.Rect(0, 340, 1440, 60, Tokens.Brush("RiverBankBrush")));

        var channel = Geo.Polyline(PortData.River);
        Scene.Place(new Microsoft.UI.Xaml.Shapes.Path
        {
            Data = channel,
            Stroke = Tokens.Brush("RiverEdgeBrush"),
            StrokeThickness = 86,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        });
        Scene.Place(new Microsoft.UI.Xaml.Shapes.Path
        {
            Data = channel,
            Stroke = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0),
                GradientStops =
                {
                    new GradientStop { Offset = 0, Color = Tokens.Color("RiverLightColor") },
                    new GradientStop { Offset = 1, Color = Tokens.Color("RiverDeepColor") }
                }
            },
            StrokeThickness = 64,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        });
        Scene.Place(new Microsoft.UI.Xaml.Shapes.Path
        {
            Data = channel,
            Stroke = Tokens.Brush("DeckWhiteColor", 0.35),
            StrokeThickness = 1,
            StrokeDashArray = Draw.Dash(10, 14)
        });

        DrawChainage();
        DrawBuoys();
        DrawGauges();
        DrawLocks();
        DrawNotices();
        DrawFleet();

        Scene.Place(Draw.Text("ALDER BASIN · KM 120", 40, 26, 11, Tokens.Brush("TextMutedBrush")));
        Scene.Place(Draw.Label("WESTHAVEN · KM 0", 1400, 26, 11, Tokens.Brush("TextMutedBrush"),
            alignment: TextAlignment.Right, width: 200));
    }

    private void DrawChainage()
    {
        for (var i = 0; i <= 12; i++)
        {
            var km = i * 10;
            var (position, _, normal) = At(km);
            var x = position.X + normal.X * 58;
            var y = position.Y + normal.Y * 58;

            Scene.Place(Draw.Rule(x, y, position.X + normal.X * 66, position.Y + normal.Y * 66,
                Tokens.Brush("TextMutedBrush")));
            Scene.Place(Draw.Label($"km {km}", position.X + normal.X * 78, position.Y + normal.Y * 78 - 7, 10.5,
                Tokens.Brush("TextMutedBrush"), "MonoFont", TextAlignment.Center, 80));
        }
    }

    private void DrawBuoys()
    {
        for (var i = 0; i < 14; i++)
        {
            var (position, _, normal) = At(4 + i * 8.5);
            var side = i % 2 == 0 ? -1 : 1;
            var x = position.X + normal.X * 26 * side;
            var y = position.Y + normal.Y * 26 * side;
            // Red to starboard coming upriver, green to port, as the buoyage runs.
            Scene.Place(Sprites.Fit(side > 0 ? "buoy-red" : "buoy-green", x - 7, y - 24, 14, 24));
        }
    }

    private void DrawGauges()
    {
        foreach (var gauge in PortData.Gauges)
        {
            var (position, _, normal) = At(gauge.Km);
            var x = position.X - normal.X * 70 - 37;
            var y = position.Y - normal.Y * 70 - 15;
            var tone = Tokens.Brush(gauge.Tone);

            Scene.Place(Draw.Rule(position.X - normal.X * 38, position.Y - normal.Y * 38,
                position.X - normal.X * 70 + 15, position.Y - normal.Y * 70 + 15, tone, 1, dash: Draw.Dash(2, 3)));
            Scene.Place(Draw.Rect(x, y, 74, 30, Tokens.Brush("SurfaceBrush", 0.92), 7, tone, 1));
            Scene.Place(Draw.Text(gauge.Name, x + 8, y + 3, 9.5, Tokens.Brush("TextMutedBrush")));
            Scene.Place(Draw.Text($"{gauge.Level:0.0} m {gauge.Trend}", x + 8, y + 15, 11.5, tone, "MonoMediumFont"));
        }
    }

    private void DrawLocks()
    {
        foreach (var lockDef in PortData.Locks)
        {
            var (position, angle, _) = At(lockDef.Km);

            var gate = new Canvas
            {
                RenderTransform = new RotateTransform { Angle = angle + 90 }
            };
            gate.Place(Draw.Rect(-8, -46, 16, 92, Tokens.Brush("InkBrush"), 3));
            gate.Place(Draw.Rect(-3, -30, 6, 60, Tokens.Brush(lockDef.Tone)));
            Scene.Place(gate.At(position.X, position.Y));

            Scene.Place(Draw.Rect(position.X - 46, position.Y - 78, 92, 22, Tokens.Brush("SurfaceBrush", 0.94), 6));
            Scene.Place(Draw.Label(lockDef.Name, position.X, position.Y - 76, 11, Tokens.Brush("InkBrush"),
                "BodyStrongFont", TextAlignment.Center, 200));
            Scene.Place(Draw.Label(lockDef.Status, position.X, position.Y + 60, 10.5,
                Tokens.Brush(lockDef.Tone), "BodyFont", TextAlignment.Center, 200));
        }
    }

    private void DrawNotices()
    {
        foreach (var notice in PortData.Notices)
        {
            var (position, _, normal) = At(notice.Km);
            var x = position.X + normal.X * 58;
            var y = position.Y + normal.Y * 58;

            Scene.Place(Draw.Dot(x, y, 22, Tokens.Brush(notice.Tone, 0.14)));
            Scene.Place(Draw.Dot(x, y, 9, Tokens.Brush("SurfaceBrush"), Tokens.Brush(notice.Tone), 2));
            Scene.Place(Draw.Label("!", x, y - 9, 11, Tokens.Brush(notice.Tone), "BodyStrongFont",
                TextAlignment.Center, 40));
        }
    }

    private void DrawFleet()
    {
        _boats.Clear();
        foreach (var vessel in PortData.RiverFleet)
        {
            var selected = _state.FleetSelection == vessel.Id;
            var late = vessel.SlipMinutes > 0;
            var heading = new RotateTransform();

            var host = new Grid
            {
                Width = 96,
                Height = 28,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = heading
            };

            host.Children.Add(new Microsoft.UI.Xaml.Shapes.Rectangle
            {
                RadiusX = 14,
                RadiusY = 14,
                Stroke = selected
                    ? Tokens.Brush("InkBrush")
                    : late ? Tokens.Brush("OrangeBrush") : Tokens.Brush("DeckWhiteColor", 0.7),
                StrokeThickness = 2
            });

            host.Children.Add(new Image
            {
                Source = Sprites.Source(vessel.Id.StartsWith("tg", StringComparison.Ordinal)
                    ? "tug-plan-b"
                    : "ship-plan"),
                Stretch = Stretch.Fill,
                Width = 80,
                Height = 16
            });

            var button = new Button
            {
                Style = (Style)Application.Current.Resources["BareButton"],
                Content = host,
                Width = 96,
                Height = 28,
                Command = new RelayCommand(() => _state.FleetSelection = vessel.Id)
            };
            AutomationProperties.SetName(button, vessel.Name);
            Scene.Place(button);

            var width = vessel.Name.Length * 6.2 + 22;
            var tag = Scene.Place(Draw.Rect(0, 0, width, 20, Tokens.Brush("InkDeepColor", 0.86), 6));
            var dot = Scene.Place(Draw.Dot(0, 0, 3.5, Tokens.Brush(late ? "OrangeBrush" : "SeaGreenBrush")));
            var name = Scene.Place(Draw.Label(vessel.Name, 0, 0, 11.5,
                Tokens.Brush("SurfaceBrush"), "BodyStrongFont", TextAlignment.Center, 200));

            _boats.Add((vessel, button, heading, tag, dot, name, width));
        }

        PlaceFleet();
    }

    /// <summary>Moves each boat to its current chainage: positions and heading only, no new elements.</summary>
    private void PlaceFleet()
    {
        foreach (var (vessel, hull, heading, tag, dot, name, width) in _boats)
        {
            var km = Math.Clamp(vessel.Km + vessel.Direction * vessel.Speed * 1.852 * ElapsedHours, 0.5, 119.5);
            var (position, angle, _) = At(km);

            heading.Angle = angle + (vessel.Direction > 0 ? 180 : 0);
            hull.At(position.X - 48, position.Y - 14);
            ToolTipService.SetToolTip(hull, $"{vessel.Name} · {vessel.Order} · km {km:0.0}");
            tag.At(position.X - width / 2, position.Y - 46);
            DotAt(dot, position.X - width / 2 + 9, position.Y - 36, 3.5);
            name.At(position.X + 5 - 100, position.Y - 44);
        }
    }

    private static void DotAt(FrameworkElement dot, double cx, double cy, double r) => dot.At(cx - r, cy - r);
}
