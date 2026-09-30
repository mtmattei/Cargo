using System.Windows.Input;
using Cargo.Controls;
using Microsoft.UI.Xaml.Media;

namespace Cargo.Presentation;

/// <summary>
/// Rows are built once and only the countdown ticks, so the list is never momentarily
/// empty while the clock refreshes.
/// </summary>
public sealed partial class NextUpItem : ObservableObject
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Detail { get; init; }
    public required double Hour { get; init; }
    public required Brush Tone { get; init; }
    public required ImageSource Silhouette { get; init; }
    public required ICommand Open { get; init; }

    [ObservableProperty]
    private string _countdown = string.Empty;
}

public sealed partial class OverviewView : UserControl
{
    public OverviewView(PortState state)
    {
        State = state;
        NextUp = new ObservableCollection<NextUpItem>();
        InitializeComponent();

        // The shift log (what happened) sits under the live picture (what is happening)
        ActivityHost.Content = new ActivityView(state);

        BuildMovements();
        BuildFlow();
        BuildTide();
        RefreshLive();

        this.TickWhenVisible(state, RefreshLive);
    }

    public PortState State { get; }

    public string Subtitle => $"{PortData.Today} · Westhaven · the harbour as it is right now.";

    public ObservableCollection<NextUpItem> NextUp { get; }

    public string MovesNow { get; private set; } = string.Empty;

    // ── Live ──────────────────────────────────────────────────────────────────

    private void RefreshLive()
    {
        var hours = State.NowHours;
        var hour = (int)Math.Floor(hours);
        var fraction = hours - hour;
        var moves = (int)Math.Round(PortData.Volume[Math.Clamp(hour, 0, 24)] * fraction + Math.Sin(hours * 97) * 3);
        MovesNow = Math.Max(0, moves).ToString();

        BuildVolume(hour, moves);
        BuildNextUp();
        UpdateNowMarker();

        Bindings.Update();
    }

    private void BuildNextUp()
    {
        if (NextUp.Count == 0)
        {
            (string Id, string Name, string Detail, double Hour, string Tone)[] rows =
            {
                ("nordic", "Nordic Star", "Arrival · Berth 07 · 21:40", 21 + 40 / 60d, "AmberBrush"),
                ("baltic", "Baltic Crown", "Departure · Berth 06 · 21:30", 21.5, "SeaGreenBrush"),
                ("levant", "Levant Express", "Arrival · Unassigned · Thu 02:30", 26.5, "TextFaintBrush")
            };

            foreach (var (id, name, detail, hour, tone) in rows)
            {
                NextUp.Add(new NextUpItem
                {
                    Id = id,
                    Name = name,
                    Detail = detail,
                    Hour = hour,
                    Tone = Tokens.Brush(tone),
                    Silhouette = Sprites.Source(Sprites.Side(id)),
                    Open = State.OpenVesselCommand
                });
            }
        }

        foreach (var row in NextUp)
        {
            row.Countdown = State.Countdown(row.Hour);
        }
    }

    // ── Vessel movements ──────────────────────────────────────────────────────

    private Microsoft.UI.Xaml.Shapes.Rectangle? _future;
    private Microsoft.UI.Xaml.Shapes.Line? _nowLine;
    private Microsoft.UI.Xaml.Shapes.Ellipse? _nowDot;

    private void BuildMovements()
    {
        var host = new SceneHost(1200, 218);
        var scene = host.Scene;

        var arrivals = Geo.Mirror(PortData.Arrivals, 1200, 100, 62, -1);
        var departures = Geo.Mirror(PortData.Departures, 1200, 100, 46, 1);

        scene.Place(new Microsoft.UI.Xaml.Shapes.Path
        {
            Data = arrivals.Area,
            Fill = Tokens.Brush("TealColor", 0.14)
        });
        scene.Place(new Microsoft.UI.Xaml.Shapes.Path
        {
            Data = departures.Area,
            Fill = Tokens.Brush("SeaGreenColor", 0.14)
        });
        scene.Place(new Microsoft.UI.Xaml.Shapes.Path
        {
            Data = arrivals.Stroke,
            Stroke = Tokens.Brush("TealBrush"),
            StrokeThickness = 1.8
        });
        scene.Place(new Microsoft.UI.Xaml.Shapes.Path
        {
            Data = departures.Stroke,
            Stroke = Tokens.Brush("SeaGreenBrush"),
            StrokeThickness = 1.8
        });

        scene.Place(Draw.Rule(0, 100, 1200, 100, Tokens.Brush("InkColor", 0.18)));

        _future = Draw.Rect(0, 22, 0, 156, Tokens.Brush("PaperWarmColor", 0.55));
        scene.Place(_future);

        _nowLine = Draw.Rule(0, 22, 0, 178, Tokens.Brush("InkBrush"), 1.2, dash: Draw.Dash(3, 3));
        scene.Place(_nowLine);

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

            scene.Place(Draw.Rule(x, y, x, arrival ? 22 : 170, tone, 1));
            scene.Place(Draw.Dot(x, y, 4.5, tone, Tokens.Brush("SurfaceBrush"), 2));

            var lane = 0;
            while (lanes[arrival].Any(p => p.Lane == lane && Math.Abs(p.X - x) < 190))
            {
                lane++;
            }

            lanes[arrival].Add((x, lane));

            var labelY = arrival ? 4 - lane * 17 : 176 + lane * 17;
            var alignment = hour < 1.5 ? TextAlignment.Left : hour > 22.5 ? TextAlignment.Right : TextAlignment.Center;
            scene.Place(Draw.Label(name, x, labelY, 15, Tokens.Brush("InkBrush"), "BodyMediumFont", alignment, 220));
        }

        _nowDot = Draw.Dot(0, 100, 5, Tokens.Brush("AlertBrush"), Tokens.Brush("SurfaceBrush"), 2);
        scene.Place(_nowDot);

        scene.Place(Draw.Rule(0, 198, 1200, 198, Tokens.Brush("InkColor", 0.06)));
        foreach (var (label, x, alignment) in new[]
                 {
                     ("00:00", 0d, TextAlignment.Left),
                     ("06:00", 300d, TextAlignment.Center),
                     ("12:00", 600d, TextAlignment.Center),
                     ("18:00", 900d, TextAlignment.Center),
                     ("24:00", 1200d, TextAlignment.Right)
                 })
        {
            scene.Place(Draw.Label(label, x, 202, 14, Tokens.Brush("TextFaintBrush"), "BodyFont", alignment, 120));
        }

        MovementsHost.Content = host;
    }

    private void UpdateNowMarker()
    {
        var x = Math.Min(1200, State.NowHours * 50);
        if (_future is not null)
        {
            _future.Width = Math.Max(0, 1200 - x);
            Canvas.SetLeft(_future, x);
        }

        if (_nowLine is not null)
        {
            _nowLine.X1 = x;
            _nowLine.X2 = x;
        }

        if (_nowDot is not null)
        {
            Canvas.SetLeft(_nowDot, x - 5);
        }
    }

    // ── Container volume ──────────────────────────────────────────────────────

    private readonly SceneHost _volume = new(300, 120);
    private readonly Canvas _volumeSettled = new();
    private readonly Canvas _volumeLive = new();
    private bool _volumeReady;
    private int _volumeHour = -1;

    /// <summary>
    /// Throughput, six settled hours plus the one in progress. Only the live column changes
    /// between ticks, so the settled six and the axis are drawn once per hour and the live
    /// column gets its own layer to redraw — rebuilding all of it every second was the bulk
    /// of what the clock cost.
    /// </summary>
    private void BuildVolume(int hour, int movesNow)
    {
        if (!_volumeReady)
        {
            VolumeHost.Content = _volume;
            _volume.Scene.Place(_volumeSettled);
            _volume.Scene.Place(_volumeLive);
            _volumeReady = true;
        }

        if (hour != _volumeHour)
        {
            _volumeHour = hour;
            _volumeSettled.Children.Clear();

            for (var i = 0; i < 6; i++)
            {
                var index = Math.Clamp(hour - 6 + i, 0, PortData.Volume.Count - 1);
                DrawVolumeColumn(_volumeSettled, 6 + i * 42, PortData.Volume[index], live: false);
            }

            _volumeSettled.Place(Draw.Rule(0, 104, 300, 104, Tokens.Brush("InkColor", 0.14)));
            _volumeSettled.Place(Draw.Text("14:00", 0, 108, 11, Tokens.Brush("TextFaintBrush")));
            _volumeSettled.Place(Draw.Label(PortState.Format(hour - 3), 150, 108, 11, Tokens.Brush("TextFaintBrush"),
                alignment: TextAlignment.Center, width: 80));
            _volumeSettled.Place(Draw.Label(PortState.Format(hour), 300, 108, 11, Tokens.Brush("TextFaintBrush"),
                alignment: TextAlignment.Right, width: 80));
        }

        _volumeLive.Children.Clear();
        DrawVolumeColumn(_volumeLive, 6 + 6 * 42, movesNow, live: true);
    }

    private static void DrawVolumeColumn(Canvas layer, double x, double value, bool live)
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

    // ── Cargo flow ────────────────────────────────────────────────────────────

    private void BuildFlow()
    {
        var host = new SceneHost(300, 150);
        var scene = host.Scene;

        var entering = Geo.MakeSeries(PortData.CargoIn, 300, 132, 14);
        var leaving = Geo.MakeSeries(PortData.CargoOut, 300, 132, 14);

        scene.Place(new Microsoft.UI.Xaml.Shapes.Path { Data = leaving.Area, Fill = Tokens.Brush("AmberColor", 0.22) });
        scene.Place(new Microsoft.UI.Xaml.Shapes.Path { Data = entering.Area, Fill = Tokens.Brush("TealColor", 0.18) });
        scene.Place(new Microsoft.UI.Xaml.Shapes.Path { Data = leaving.Stroke, Stroke = Tokens.Brush("AmberBrush"), StrokeThickness = 1.8 });
        scene.Place(new Microsoft.UI.Xaml.Shapes.Path { Data = entering.Stroke, Stroke = Tokens.Brush("TealBrush"), StrokeThickness = 1.8 });

        scene.Place(Draw.Rule(0, 132, 300, 132, Tokens.Brush("InkColor", 0.1)));
        scene.Place(Draw.Text("00:00", 0, 136, 12, Tokens.Brush("TextFaintBrush")));
        scene.Place(Draw.Label("12:00", 150, 136, 12, Tokens.Brush("TextFaintBrush"), alignment: TextAlignment.Center, width: 80));
        scene.Place(Draw.Label("24:00", 300, 136, 12, Tokens.Brush("TextFaintBrush"), alignment: TextAlignment.Right, width: 80));

        FlowHost.Content = host;
    }

    // ── Tide ──────────────────────────────────────────────────────────────────

    private void BuildTide()
    {
        var host = new SceneHost(600, 70, stretchHeight: true);
        var scene = host.Scene;

        var tide = Geo.MakeSeries(PortData.Tide, 600, 62, 8);
        scene.Place(new Microsoft.UI.Xaml.Shapes.Path { Data = tide.Area, Fill = Tokens.Brush("TealColor", 0.12) });
        scene.Place(new Microsoft.UI.Xaml.Shapes.Path { Data = tide.Stroke, Stroke = Tokens.Brush("TealBrush"), StrokeThickness = 1.8 });

        var x = Math.Clamp((PortData.NowHours - 18) / 12 * 600, 0, 600);
        var y = tide.At((PortData.NowHours - 18) * 2);
        scene.Place(Draw.Rule(x, 4, x, 62, Tokens.Brush("InkBrush"), 1, dash: Draw.Dash(3, 3)));
        scene.Place(Draw.Dot(x, y, 4, Tokens.Brush("TealBrush"), Tokens.Brush("SurfaceBrush"), 2));

        TideHost.Content = host;
    }
}
