namespace Cargo.Controls;

/// <summary>
/// The live quay: a gantry crane working the berth, with the trolley tracking out over
/// the ship and a box on the hook. Driven by a 20 fps timer rather than a Forever
/// storyboard — a permanently repeating storyboard pins the Skia compositor at display
/// refresh and costs about a third of a core even when it is off screen.
/// </summary>
public sealed partial class CargoOpsScene : SceneHost
{
    private const double LoopSeconds = 6;

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly TranslateTransform _trolley = new();
    private readonly TranslateTransform _hook = new();
    private readonly Canvas _hookGroup = new();

    private DateTimeOffset _origin = DateTimeOffset.Now;
    private bool _loading;

    public static readonly DependencyProperty VesselProperty = DependencyProperty.Register(
        nameof(Vessel), typeof(Vessel), typeof(CargoOpsScene),
        new PropertyMetadata(null, (d, e) =>
        {
            // Only a different vessel restarts the crane loop; the same one keeps its place
            if (e.NewValue is Vessel vessel && (e.OldValue as Vessel)?.Id != vessel.Id)
            {
                ((CargoOpsScene)d).Show(vessel);
            }
        }));

    public CargoOpsScene() : base(420, 175)
    {
        _timer.Tick += (_, _) => Advance();
        this.RunWhileShown(_timer);
    }

    public Vessel? Vessel
    {
        get => (Vessel?)GetValue(VesselProperty);
        set => SetValue(VesselProperty, value);
    }

    private void Show(Vessel vessel)
    {
        Scene.Children.Clear();
        _origin = DateTimeOffset.Now;
        _loading = vessel.LoadPercent is > 0 and < 100;

        Scene.Place(Draw.Rect(0, 0, 420, 175, new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(0, 1),
            GradientStops =
            {
                new GradientStop { Offset = 0, Color = Tokens.Color("SurfaceColor") },
                new GradientStop { Offset = 1, Color = Tokens.Color("SeaLightColor") }
            }
        }));

        // Water and quay
        Scene.Place(Draw.Rect(0, 150, 420, 25, Tokens.Brush("SeaFlatInvariantBrush")));
        Scene.Place(Draw.Rect(0, 140, 140, 12, Tokens.Brush("QuayInvariantBrush")));
        Scene.Place(Draw.Rect(0, 138, 140, 3, Tokens.Brush("QuayEdgeInvariantBrush")));

        // The stack already landed, then the boxes that fill in as discharge proceeds
        Scene.Place(Sprites.Fit("cont-stack", 4, 98, 58, 44, anchorY: 1));

        var ashore = (int)Math.Round(6 * vessel.UnloadPercent / 100d);
        for (var i = 0; i < 6; i++)
        {
            Scene.Place(Sprites.Fill(Sprites.ContainerSide(vessel.Mix.ClassAt(i * 5)),
                72 + i % 3 * 38, 122 - i / 3 * 16, 34, 16, i < ashore ? 1 : 0.18));
        }

        // The ship alongside
        Scene.Place(Sprites.Fit(Sprites.Side(vessel.Id), 140, 40, 280, 112, anchorY: 1));

        var trolley = new Canvas { RenderTransform = _trolley };
        trolley.Place(Draw.Rect(58, 8, 24, 7, Tokens.Brush("SteelInvariantBrush"), 1.5));
        trolley.Place(Draw.Rule(70, 15, 70, 22, Tokens.Brush("SteelInvariantBrush"), 1.5));
        Scene.Place(trolley);

        _hookGroup.Children.Clear();
        _hookGroup.RenderTransform = _hook;
        _hookGroup.Place(Draw.Rect(-4, -2, 42, 4, Tokens.Brush("CargoOversizeInvariantBrush"), 1));
        _hookGroup.Place(Sprites.Fill(
            Sprites.ContainerSide(vessel.Mix.Hazard > 5 ? CargoClass.Hazard : CargoClass.Standard),
            0, 2, 34, 16));
        Scene.Place(_hookGroup);

        // Crane last, so the hook passes behind its legs exactly as the design has it.
        Scene.Place(Sprites.Fit("crane-side", 36, -4, 150, 148, anchorY: 1, opacity: 0.96));

        Scene.Place(Draw.Text($"Quay · {Math.Round(vessel.Containers * vessel.UnloadPercent / 100d):N0} discharged",
            8, 158, 10.5, Tokens.Brush("TextMutedInvariantBrush")));
        Scene.Place(Draw.Text($"Aboard · {vessel.Containers - Math.Round(vessel.Containers * vessel.UnloadPercent / 100d):N0} TEU",
            212, 158, 10.5, Tokens.Brush("TextMutedInvariantBrush"), alignment: TextAlignment.Right, width: 200));
    }

    private void Advance()
    {
        var t = (DateTimeOffset.Now - _origin).TotalSeconds % LoopSeconds / LoopSeconds;

        // Trolley: parked outboard, tracks in over the quay, parks again
        _trolley.X = t < 0.28 ? 198 : t < 0.62 ? 198 * (1 - (t - 0.28) / 0.34) : 0;

        // Hook: lift out of the hold, traverse, land on the apron
        var (x, y, opacity) = Hook(t);
        _hook.X = x;
        _hook.Y = y;
        _hookGroup.Opacity = opacity;
    }

    private (double X, double Y, double Opacity) Hook(double t)
    {
        // Loading runs the same path backwards, half a cycle out of phase
        if (_loading)
        {
            var (bx, by, bo) = Discharge(1 - t);
            return (bx, by, bo);
        }

        return Discharge(t);
    }

    private static (double X, double Y, double Opacity) Discharge(double t) => t switch
    {
        < 0.06 => (262, 74, t / 0.06),
        < 0.28 => (262, 74 - 52 * ((t - 0.06) / 0.22), 1),
        < 0.62 => (262 - 198 * ((t - 0.28) / 0.34), 22, 1),
        < 0.92 => (64, 22 + 96 * ((t - 0.62) / 0.30), 1),
        _ => (64, 118, 1 - (t - 0.92) / 0.08)
    };
}
