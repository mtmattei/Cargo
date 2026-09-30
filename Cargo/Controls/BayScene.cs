using Microsoft.UI.Xaml.Media;

namespace Cargo.Controls;

/// <summary>Cross-section through one bay, looking forward from the bridge.</summary>
public sealed partial class BayScene : SceneHost
{
    public static readonly DependencyProperty BayProperty = DependencyProperty.Register(
        nameof(Bay), typeof(BayView), typeof(BayScene),
        new PropertyMetadata(null, (d, e) =>
        {
            if (e.NewValue is BayView bay)
            {
                ((BayScene)d).Show(bay);
            }
        }));

    public BayScene() : base(360, 190)
    {
    }

    public BayView? Bay
    {
        get => (BayView?)GetValue(BayProperty);
        set => SetValue(BayProperty, value);
    }

    private void Show(BayView bay)
    {
        Scene.Children.Clear();

        Scene.Place(Draw.Rect(0, 0, 360, 190, new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(0, 1),
            GradientStops =
            {
                new GradientStop { Offset = 0, Color = Tokens.Color("SurfaceColor") },
                new GradientStop { Offset = 1, Color = Tokens.Color("SeaLightColor") }
            }
        }));

        // Hull section: flared topsides over a rounded bilge
        Scene.Place(Draw.Shape(
            "M62 20 L62 132 C62 156 84 172 116 176 L246 176 C278 172 300 156 300 132 L300 20 " +
            "L292 20 L292 130 C292 150 274 164 246 168 L116 168 C88 164 70 150 70 130 L70 20 Z",
            Tokens.Brush("HullInvariantBrush")));
        Scene.Place(Draw.Rect(62, 128, 238, 5, Tokens.Brush("BootToppingInvariantBrush")));

        // Tier labels down the port side
        for (var row = 0; row < BayView.Rows; row++)
        {
            Scene.Place(Draw.Text((BayView.Rows - row).ToString("D2"), 10, 100 - row * 22, 10.5,
                Tokens.Brush("TextMutedInvariantBrush"), "MonoMediumFont", TextAlignment.Right, 48));
        }

        foreach (var (column, row, cargo, ashore) in bay.Cells)
        {
            var x = 70 + column * 38;
            var y = 104 - row * 22;

            if (ashore)
            {
                var slot = Draw.Rect(x, y, 36, 21, Tokens.Brush("PaperWarmInvariantBrush"), 1.5,
                    Tokens.Brush("TextFaintInvariantBrush"), 1);
                slot.StrokeDashArray = Draw.Dash(3, 2);
                Scene.Place(slot);
                continue;
            }

            var color = Tokens.Color(cargo.Token());
            Scene.Place(Draw.Rect(x, y, 36, 21, Tokens.Of(color), 1.5, Tokens.Brush("InkColor", 0.25), 1));
            Scene.Place(Draw.Rect(x, y, 36, 4, Tokens.Of(Tokens.Shade(color, 30))));
        }

        // Bay badge on the hatch cover
        Scene.Place(Draw.Rect(166, 138, 28, 18, Tokens.Brush("TealInvariantBrush"), 6));
        Scene.Place(Draw.Text(bay.Number, 166, 140, 11, Tokens.Brush("SurfaceInvariantBrush"),
            "MonoMediumFont", TextAlignment.Center, 28));

        Scene.Place(Draw.Rect(0, 176, 360, 14, Tokens.Brush("SeaFlatInvariantBrush", 0.85)));
    }
}
