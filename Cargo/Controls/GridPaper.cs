using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Cargo.Controls;

/// <summary>
/// The faint 48 px survey grid the design lays under every page, plus a teal wash in the
/// top-left corner. Drawn as lines rather than a tiled bitmap so it stays crisp at any DPI.
/// </summary>
public sealed partial class GridPaper : UserControl
{
    private const double Pitch = 48;

    private readonly Canvas _canvas = new();

    public GridPaper()
    {
        Content = _canvas;
        IsHitTestVisible = false;
        SizeChanged += (_, e) => Rebuild(e.NewSize.Width, e.NewSize.Height);
    }

    private void Rebuild(double width, double height)
    {
        _canvas.Children.Clear();
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var glow = new Microsoft.UI.Xaml.Shapes.Rectangle
        {
            Width = width,
            Height = height,
            Fill = new RadialGradientBrush
            {
                Center = new Windows.Foundation.Point(0.2, 0),
                GradientOrigin = new Windows.Foundation.Point(0.2, 0),
                RadiusX = 0.7,
                RadiusY = 0.9,
                GradientStops =
                {
                    new GradientStop { Offset = 0, Color = Tokens.Color("TealColor") },
                    new GradientStop { Offset = 1, Color = Windows.UI.Color.FromArgb(0, 0, 0, 0) }
                }
            },
            Opacity = 0.10
        };
        _canvas.Children.Add(glow);

        var line = Tokens.Brush("InkColor", 0.045);
        for (var x = Pitch; x < width; x += Pitch)
        {
            _canvas.Children.Add(Draw.Rule(x, 0, x, height, line));
        }

        for (var y = Pitch; y < height; y += Pitch)
        {
            _canvas.Children.Add(Draw.Rule(0, y, width, y, line));
        }
    }
}
