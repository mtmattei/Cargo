using Microsoft.UI.Xaml.Markup;
using Windows.Foundation;

namespace Cargo.Domain;

/// <summary>
/// Geometry helpers. The source design draws every chart and every piece of harbour
/// furniture as SVG; these rebuild the same curves as WinUI <see cref="Geometry"/>.
/// </summary>
public static class Geo
{
    private static readonly Dictionary<string, Geometry> PathCache = new();

    /// <summary>Parses an SVG path string (circular arcs only — Uno rejects elliptical ones).</summary>
    public static Geometry Path(string data)
    {
        if (PathCache.TryGetValue(data, out var cached))
        {
            return cached;
        }

        var xaml = "<Path xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" Data=\"" + data + "\" />";
        var geometry = ((Microsoft.UI.Xaml.Shapes.Path)XamlReader.Load(xaml)).Data!;
        PathCache[data] = geometry;
        return geometry;
    }

    /// <summary>Deterministic pseudo-noise — the same generator the design uses, so layouts match.</summary>
    public static double H(int i) => ((i * 7919 + 13) % 97) / 97d;

    /// <summary>Horizontal-tangent cubic through the points (the design's `smooth`).</summary>
    public static PathFigure SmoothFigure(IReadOnlyList<Point> points)
    {
        var figure = new PathFigure { StartPoint = points[0], IsFilled = true, IsClosed = false };
        for (var i = 1; i < points.Count; i++)
        {
            var a = points[i - 1];
            var b = points[i];
            var cx = (a.X + b.X) / 2;
            figure.Segments.Add(new BezierSegment
            {
                Point1 = new Point(cx, a.Y),
                Point2 = new Point(cx, b.Y),
                Point3 = b
            });
        }

        return figure;
    }

    public static Geometry Smooth(IReadOnlyList<Point> points)
    {
        var geometry = new PathGeometry();
        geometry.Figures.Add(SmoothFigure(points));
        return geometry;
    }

    /// <summary>Same curve closed down to <paramref name="baseline"/> so it can be filled.</summary>
    public static Geometry SmoothArea(IReadOnlyList<Point> points, double baseline)
    {
        var figure = SmoothFigure(points);
        figure.Segments.Add(new LineSegment { Point = new Point(points[^1].X, baseline) });
        figure.Segments.Add(new LineSegment { Point = new Point(points[0].X, baseline) });
        figure.IsClosed = true;
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    public static Geometry Polyline(IReadOnlyList<Point> points, bool close = false)
    {
        var figure = new PathFigure { StartPoint = points[0], IsClosed = close, IsFilled = close };
        for (var i = 1; i < points.Count; i++)
        {
            figure.Segments.Add(new LineSegment { Point = points[i] });
        }

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    /// <summary>A series scaled into a box, matching the design's `series(vals, W, H, pad)`.</summary>
    public sealed class Series
    {
        public required IReadOnlyList<Point> Points { get; init; }
        public required Geometry Stroke { get; init; }
        public required Geometry Area { get; init; }

        public double At(double index)
        {
            var i = (int)Math.Floor(index);
            var f = index - i;
            var a = Points[Math.Clamp(i, 0, Points.Count - 1)].Y;
            var b = Points[Math.Clamp(i + 1, 0, Points.Count - 1)].Y;
            return a + (b - a) * f;
        }
    }

    public static Series MakeSeries(IReadOnlyList<double> values, double width, double height, double pad)
    {
        var max = values.Max();
        var points = values
            .Select((v, i) => new Point(i * width / (values.Count - 1), height - pad - v / max * (height - pad * 2)))
            .ToList();

        return new Series
        {
            Points = points,
            Stroke = Smooth(points),
            Area = SmoothArea(points, height)
        };
    }
}
