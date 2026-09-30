using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Cargo.Controls;

/// <summary>
/// The faint 48 px survey grid the design lays under every page, plus a teal wash in the
/// top-left corner. Drawn as lines rather than a tiled bitmap so it stays crisp at any DPI,
/// and as one Path so a window resize rebuilds a geometry, not a hundred elements.
/// </summary>
public sealed partial class GridPaper : UserControl
{
    private const double Pitch = 48;

    private readonly Path _lines = new() { StrokeThickness = 1 };

    public GridPaper()
    {
        IsHitTestVisible = false;
        _lines.Stroke = Tokens.Brush("InkColor", 0.045);

        var glow = new Rectangle
        {
            Fill = new RadialGradientBrush
            {
                Center = new Windows.Foundation.Point(0.2, 0),
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

        Content = new Grid { Children = { glow, _lines } };
        SizeChanged += (_, e) => Rebuild(e.NewSize.Width, e.NewSize.Height);
    }

    private void Rebuild(double width, double height)
    {
        var grid = new GeometryGroup();
        for (var x = Pitch; x < width; x += Pitch)
        {
            grid.Children.Add(new LineGeometry { StartPoint = new(x, 0), EndPoint = new(x, height) });
        }

        for (var y = Pitch; y < height; y += Pitch)
        {
            grid.Children.Add(new LineGeometry { StartPoint = new(0, y), EndPoint = new(width, y) });
        }

        _lines.Data = grid;
    }
}
