using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Cargo.Controls;

/// <summary>Terse constructors for the shapes the harbour art is built from.</summary>
internal static class Draw
{
    public static Canvas Stage(double width, double height)
    {
        var canvas = new Canvas { Width = width, Height = height };
        canvas.Clip = new Microsoft.UI.Xaml.Media.RectangleGeometry
        {
            Rect = new Windows.Foundation.Rect(0, 0, width, height)
        };
        return canvas;
    }

    public static T At<T>(this T element, double x, double y) where T : UIElement
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        return element;
    }

    public static T Place<T>(this Canvas canvas, T element) where T : UIElement
    {
        canvas.Children.Add(element);
        return element;
    }

    public static Rectangle Rect(double x, double y, double w, double h, Brush? fill,
        double radius = 0, Brush? stroke = null, double strokeWidth = 0, double opacity = 1)
    {
        var rect = new Rectangle
        {
            Width = Math.Max(0, w),
            Height = Math.Max(0, h),
            Fill = fill,
            RadiusX = radius,
            RadiusY = radius,
            Stroke = stroke,
            StrokeThickness = strokeWidth,
            Opacity = opacity
        };
        return rect.At(x, y);
    }

    public static Ellipse Dot(double cx, double cy, double r, Brush? fill,
        Brush? stroke = null, double strokeWidth = 0, double opacity = 1)
    {
        var ellipse = new Ellipse
        {
            Width = r * 2,
            Height = r * 2,
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = strokeWidth,
            Opacity = opacity
        };
        return ellipse.At(cx - r, cy - r);
    }

    public static Microsoft.UI.Xaml.Shapes.Path Shape(string data, Brush? fill = null,
        Brush? stroke = null, double strokeWidth = 0, double opacity = 1)
        => new()
        {
            Data = Geo.Path(data),
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = strokeWidth,
            Opacity = opacity,
            StrokeLineJoin = PenLineJoin.Round
        };

    public static Line Rule(double x1, double y1, double x2, double y2, Brush? stroke,
        double strokeWidth = 1, double opacity = 1, DoubleCollection? dash = null)
    {
        var line = new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = stroke,
            StrokeThickness = strokeWidth,
            Opacity = opacity
        };
        if (dash is not null)
        {
            line.StrokeDashArray = dash;
        }

        return line;
    }

    public static DoubleCollection Dash(params double[] pattern)
    {
        var collection = new DoubleCollection();
        foreach (var value in pattern)
        {
            collection.Add(value);
        }

        return collection;
    }

    /// <summary>
    /// A ring chart. XAML dash arrays are multiples of the stroke thickness (SVG's are
    /// absolute), so the arc lengths are divided through before they are applied.
    /// </summary>
    public static Canvas Donut(IReadOnlyList<(double Weight, Brush Fill)> slices, double radius, double thickness)
    {
        var size = radius * 2 + thickness;
        var canvas = new Canvas { Width = size, Height = size };

        var circumference = 2 * Math.PI * radius;
        var total = slices.Sum(s => s.Weight);
        if (total <= 0)
        {
            total = 1;
        }

        var accumulated = 0d;
        foreach (var (weight, fill) in slices)
        {
            var length = circumference * weight / total;
            var visible = Math.Max(length - 1.5, 0);

            var ring = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Stroke = fill,
                StrokeThickness = thickness,
                StrokeDashArray = Dash(visible / thickness, (circumference - visible) / thickness),
                StrokeDashOffset = -accumulated / thickness,
                StrokeDashCap = PenLineCap.Flat,
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
                RenderTransform = new RotateTransform { Angle = -90 }
            };

            canvas.Place(ring.At(thickness / 2, thickness / 2));
            accumulated += length;
        }

        return canvas;
    }

    /// <summary>
    /// Text placed by its anchor point rather than its box, so an axis label at x = 0 sits
    /// inside the scene instead of hanging off the left edge.
    /// </summary>
    public static TextBlock Label(string text, double anchorX, double y, double size, Brush? fill,
        string fontKey = "BodyFont", TextAlignment alignment = TextAlignment.Left, double width = 180)
    {
        var left = alignment switch
        {
            TextAlignment.Center => anchorX - width / 2,
            TextAlignment.Right => anchorX - width,
            _ => anchorX
        };

        return Text(text, left, y, size, fill, fontKey, alignment, width);
    }

    public static TextBlock Text(string text, double x, double y, double size, Brush? fill,
        string fontKey = "BodyFont", TextAlignment alignment = TextAlignment.Left, double width = 0)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            Foreground = fill,
            FontFamily = (FontFamily)Application.Current.Resources[fontKey],
            TextAlignment = alignment,
            IsHitTestVisible = false
        };

        if (width > 0)
        {
            block.Width = width;
        }

        return block.At(x, y);
    }
}
