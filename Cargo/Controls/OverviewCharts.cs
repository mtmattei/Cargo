using Microsoft.UI.Xaml.Shapes;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Cargo.Controls;

/// <summary>
/// The day's arrivals above the line and departures below, with the time now marked.
/// Built once; <see cref="Now"/> only moves the marker and the shaded future.
/// </summary>
public sealed partial class MovementsChart : SceneHost
{
    public static readonly DependencyProperty NowProperty = DependencyProperty.Register(
        nameof(Now), typeof(double), typeof(MovementsChart),
        new PropertyMetadata(0d, (d, _) => ((MovementsChart)d).UpdateNowMarker()));

    private readonly Rectangle _future;
    private readonly Line _nowLine;
    private readonly Ellipse _nowDot;

    public MovementsChart() : base(1200, 218)
    {
        var arrivals = Geo.Mirror(PortData.Arrivals, 1200, 100, 62, -1);
        var departures = Geo.Mirror(PortData.Departures, 1200, 100, 46, 1);

        Scene.Place(new Path { Data = arrivals.Area, Fill = Draw.Fade("TealColor", 0.24) });
        Scene.Place(new Path { Data = departures.Area, Fill = Draw.Fade("SeaGreenColor", 0.24, up: true) });
        Scene.Place(new Path { Data = arrivals.Stroke, Stroke = Tokens.Brush("TealBrush"), StrokeThickness = 1.8 });
        Scene.Place(new Path { Data = departures.Stroke, Stroke = Tokens.Brush("SeaGreenBrush"), StrokeThickness = 1.8 });

        Scene.Place(Draw.Rule(0, 100, 1200, 100, Tokens.Brush("InkColor", 0.18)));

        _future = Draw.Rect(0, 22, 0, 156, Tokens.Brush("PaperWarmColor", 0.55));
        Scene.Place(_future);

        _nowLine = Draw.Rule(0, 22, 0, 178, Tokens.Brush("InkBrush"), 1.2, dash: Draw.Dash(3, 3));
        Scene.Place(_nowLine);

        // Scheduled arrivals and departures, laned so neighbours do not collide
        var lanes = new Dictionary<bool, List<(double X, int Lane)>>
        {
            [true] = new(),
            [false] = new()
        };

        foreach (var (name, hour, arrival) in PortData.Movements)
        {
            var x = hour * 50;
            var y = arrival ? arrivals.At(hour) : departures.At(hour);
            var future = hour > PortData.NowHours;
            var tone = future
                ? Tokens.Brush("AmberBrush")
                : arrival ? Tokens.Brush("TealBrush") : Tokens.Brush("SeaGreenBrush");

            Scene.Place(Draw.Rule(x, y, x, arrival ? 22 : 170, tone, 1));
            Scene.Place(Draw.Dot(x, y, 4.5, tone, Tokens.Brush("SurfaceBrush"), 2));

            var lane = 0;
            while (lanes[arrival].Any(p => p.Lane == lane && Math.Abs(p.X - x) < 190))
            {
                lane++;
            }

            lanes[arrival].Add((x, lane));

            var labelY = arrival ? 4 - lane * 17 : 176 + lane * 17;
            var alignment = hour < 1.5 ? TextAlignment.Left : hour > 22.5 ? TextAlignment.Right : TextAlignment.Center;
            Scene.Place(Draw.Label(name, x, labelY, 15, Tokens.Brush("InkBrush"), "BodyMediumFont", alignment, 220));
        }

        _nowDot = Draw.Dot(0, 100, 5, Tokens.Brush("AlertBrush"), Tokens.Brush("SurfaceBrush"), 2);
        Scene.Place(_nowDot);

        Scene.Place(Draw.Rule(0, 198, 1200, 198, Tokens.Brush("InkColor", 0.06)));
        foreach (var (label, x, alignment) in new[]
                 {
                     ("00:00", 0d, TextAlignment.Left),
                     ("06:00", 300d, TextAlignment.Center),
                     ("12:00", 600d, TextAlignment.Center),
                     ("18:00", 900d, TextAlignment.Center),
                     ("24:00", 1200d, TextAlignment.Right)
                 })
        {
            Scene.Place(Draw.Label(label, x, 202, 14, Tokens.Brush("TextFaintBrush"), "BodyFont", alignment, 120));
        }
    }

    /// <summary>Hours since midnight on the shift clock.</summary>
    public double Now
    {
        get => (double)GetValue(NowProperty);
        set => SetValue(NowProperty, value);
    }

    private void UpdateNowMarker()
    {
        var x = Math.Min(1200, Now * 50);
        _future.Width = Math.Max(0, 1200 - x);
        Canvas.SetLeft(_future, x);
        _nowLine.X1 = x;
        _nowLine.X2 = x;
        Canvas.SetLeft(_nowDot, x - 5);
    }
}

/// <summary>
/// Throughput, six settled hours plus the one in progress. Only the live column changes
/// between ticks, so the settled six and the axis are drawn once per hour and the live
/// column gets its own layer to redraw; rebuilding all of it every second was the bulk
/// of what the clock cost.
/// </summary>
public sealed partial class VolumeChart : SceneHost
{
    public static readonly DependencyProperty NowProperty = DependencyProperty.Register(
        nameof(Now), typeof(double), typeof(VolumeChart),
        new PropertyMetadata(0d, (d, _) => ((VolumeChart)d).Redraw()));

    private readonly Canvas _settled = new();
    private readonly Canvas _live = new();
    private int _hour = -1;

    public VolumeChart() : base(300, 120)
    {
        Scene.Place(_settled);
        Scene.Place(_live);
    }

    /// <summary>Hours since midnight on the shift clock.</summary>
    public double Now
    {
        get => (double)GetValue(NowProperty);
        set => SetValue(NowProperty, value);
    }

    private void Redraw()
    {
        var hour = (int)Math.Floor(Now);
        if (hour != _hour)
        {
            _hour = hour;
            _settled.Children.Clear();

            for (var i = 0; i < 6; i++)
            {
                var index = Math.Clamp(hour - 6 + i, 0, PortData.Volume.Count - 1);
                DrawColumn(_settled, 6 + i * 42, PortData.Volume[index], live: false);
            }

            _settled.Place(Draw.Rule(0, 104, 300, 104, Tokens.Brush("InkColor", 0.14)));
            _settled.Place(Draw.Text("14:00", 0, 108, 11, Tokens.Brush("TextFaintBrush")));
            _settled.Place(Draw.Label(PortState.Format(hour - 3), 150, 108, 11, Tokens.Brush("TextFaintBrush"),
                alignment: TextAlignment.Center, width: 80));
            _settled.Place(Draw.Label(PortState.Format(hour), 300, 108, 11, Tokens.Brush("TextFaintBrush"),
                alignment: TextAlignment.Right, width: 80));
        }

        _live.Children.Clear();
        DrawColumn(_live, 6 + 6 * 42, PortState.MovesAt(Now), live: true);
    }

    private static void DrawColumn(Canvas layer, double x, double value, bool live)
    {
        var blocks = (int)Math.Ceiling(value / 20d);

        for (var j = 0; j < blocks; j++)
        {
            var partial = live && j == blocks - 1 && value % 20 != 0;
            var y = 94 - j * 10.5;
            var token = live
                ? "TealColor"
                : j % 4 == 1 ? "CargoHazardColor" : j % 4 == 3 ? "CargoOversizeColor" : "CargoStandardColor";
            var color = Tokens.Color(token);
            var opacity = partial ? 0.45 : live ? 1 : 0.8;

            layer.Place(Draw.Rect(x, y, 20, 9, Tokens.Of(color, opacity), 1));
            layer.Place(Draw.Rect(x, y, 20, 2, Tokens.Brush("DeckWhiteColor", 0.35 * opacity)));
            for (var rib = 1; rib <= 3; rib++)
            {
                layer.Place(Draw.Rule(x + rib * 5, y, x + rib * 5, y + 9, Tokens.Brush("InkColor", 0.18 * opacity), 0.8));
            }
        }
    }
}

/// <summary>Containers entering and leaving the terminal over the day.</summary>
public sealed partial class FlowChart : SceneHost
{
    public FlowChart() : base(300, 150)
    {
        var entering = Geo.MakeSeries(PortData.CargoIn, 300, 132, 14);
        var leaving = Geo.MakeSeries(PortData.CargoOut, 300, 132, 14);

        Scene.Place(new Path { Data = leaving.Area, Fill = Draw.Fade("AmberColor", 0.30) });
        Scene.Place(new Path { Data = entering.Area, Fill = Draw.Fade("TealColor", 0.26) });
        Scene.Place(new Path { Data = leaving.Stroke, Stroke = Tokens.Brush("AmberBrush"), StrokeThickness = 1.8 });
        Scene.Place(new Path { Data = entering.Stroke, Stroke = Tokens.Brush("TealBrush"), StrokeThickness = 1.8 });

        Scene.Place(Draw.Rule(0, 132, 300, 132, Tokens.Brush("InkColor", 0.1)));
        Scene.Place(Draw.Text("00:00", 0, 136, 12, Tokens.Brush("TextFaintBrush")));
        Scene.Place(Draw.Label("12:00", 150, 136, 12, Tokens.Brush("TextFaintBrush"), alignment: TextAlignment.Center, width: 80));
        Scene.Place(Draw.Label("24:00", 300, 136, 12, Tokens.Brush("TextFaintBrush"), alignment: TextAlignment.Right, width: 80));
    }
}

/// <summary>The outer-harbour tide from 18:00, with the reading now.</summary>
public sealed partial class TideChart : SceneHost
{
    public TideChart() : base(600, 70, stretchHeight: true)
    {
        var tide = Geo.MakeSeries(PortData.Tide, 600, 62, 8);
        Scene.Place(new Path { Data = tide.Area, Fill = Draw.Fade("RiverDeepColor", 0.32) });
        Scene.Place(new Path { Data = tide.Stroke, Stroke = Tokens.Brush("RiverDeepColor"), StrokeThickness = 1.8 });

        var x = Math.Clamp((PortData.NowHours - 18) / 12 * 600, 0, 600);
        var y = tide.At((PortData.NowHours - 18) * 2);
        Scene.Place(Draw.Rule(x, 4, x, 62, Tokens.Brush("InkBrush"), 1, dash: Draw.Dash(3, 3)));
        Scene.Place(Draw.Dot(x, y, 4, Tokens.Brush("TealBrush"), Tokens.Brush("SurfaceBrush"), 2));
    }
}

/// <summary>Moves per hour across the day, with the current hour marked.</summary>
public sealed partial class ThroughputChart : SceneHost
{
    public ThroughputChart() : base(1000, 120)
    {
        Overflow = 24;
        var series = Geo.MakeSeries(PortData.Volume, 1000, 100, 14);

        Scene.Place(new Path { Data = series.Area, Fill = Draw.Fade("TealColor", 0.22) });
        Scene.Place(new Path { Data = series.Stroke, Stroke = Tokens.Brush("TealBrush"), StrokeThickness = 2 });

        Scene.Place(Draw.Rule(0, 100, 1000, 100, Tokens.Brush("InkColor", 0.1)));
        Scene.Place(Draw.Rule(874, 8, 874, 100, Tokens.Brush("InkBrush"), 1, dash: Draw.Dash(3, 3)));
        Scene.Place(Draw.Label("184 now", 874, -14, 13, Tokens.Brush("InkBrush"), "BodyStrongFont",
            TextAlignment.Center, 160));

        foreach (var (label, x, alignment) in new[]
                 {
                     ("00:00", 0d, TextAlignment.Left),
                     ("06:00", 250d, TextAlignment.Center),
                     ("12:00", 500d, TextAlignment.Center),
                     ("18:00", 750d, TextAlignment.Center),
                     ("24:00", 1000d, TextAlignment.Right)
                 })
        {
            Scene.Place(Draw.Label(label, x, 106, 14, Tokens.Brush("TextFaintBrush"), "BodyFont", alignment, 120));
        }
    }
}
