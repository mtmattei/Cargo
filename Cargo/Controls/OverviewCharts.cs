using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Cargo.Controls;

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
    private (int Blocks, bool Partial) _liveDrawn = (-1, false);

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
            _settled.Place(Draw.Text("14:00", 0, 108, 11, Tokens.Brush("TextFaintInvariantBrush")));
            _settled.Place(Draw.Label(PortState.Format(hour - 3), 150, 108, 11, Tokens.Brush("TextFaintInvariantBrush"),
                alignment: TextAlignment.Center, width: 80));
            _settled.Place(Draw.Label(PortState.Format(hour), 300, 108, 11, Tokens.Brush("TextFaintInvariantBrush"),
                alignment: TextAlignment.Right, width: 80));
        }

        // The live column only changes when a block fills or starts, a few times an hour:
        // rebuilding its shapes every second was the bulk of the clock tick's cost
        var moves = PortState.MovesAt(Now);
        var live = ((int)Math.Ceiling(moves / 20d), moves % 20 != 0);
        if (live == _liveDrawn)
        {
            return;
        }

        _liveDrawn = live;
        _live.Children.Clear();
        DrawColumn(_live, 6 + 6 * 42, moves, live: true);
    }

    private static void DrawColumn(Canvas layer, double x, double value, bool live)
    {
        var blocks = (int)Math.Ceiling(value / 20d);

        for (var j = 0; j < blocks; j++)
        {
            var partial = live && j == blocks - 1 && value % 20 != 0;
            var y = 94 - j * 10.5;
            var token = live
                ? "AccentColor"
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
        Scene.Place(new Path { Data = entering.Area, Fill = Draw.Fade("AccentColor", 0.26) });
        Scene.Place(new Path { Data = leaving.Stroke, Stroke = Tokens.Brush("AmberInvariantBrush"), StrokeThickness = 1.8 });
        Scene.Place(new Path { Data = entering.Stroke, Stroke = Tokens.Brush("AccentInvariantBrush"), StrokeThickness = 1.8 });

        Scene.Place(Draw.Rule(0, 132, 300, 132, Tokens.Brush("InkColor", 0.1)));
        Scene.Place(Draw.Text("00:00", 0, 136, 12, Tokens.Brush("TextFaintInvariantBrush")));
        Scene.Place(Draw.Label("12:00", 150, 136, 12, Tokens.Brush("TextFaintInvariantBrush"), alignment: TextAlignment.Center, width: 80));
        Scene.Place(Draw.Label("24:00", 300, 136, 12, Tokens.Brush("TextFaintInvariantBrush"), alignment: TextAlignment.Right, width: 80));
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
        Scene.Place(Draw.Rule(x, 4, x, 62, Tokens.Brush("InkInvariantBrush"), 1, dash: Draw.Dash(3, 3)));
        Scene.Place(Draw.Dot(x, y, 4, Tokens.Brush("AccentInvariantBrush"), Tokens.Brush("SurfaceInvariantBrush"), 2));
    }
}
