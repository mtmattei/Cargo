namespace Cargo.Controls.Harbour;

/// <summary>A world-space point or direction. World units are 3.9 m (a 366 m hull is 94 units).</summary>
public readonly record struct V3(double X, double Y, double Z)
{
    public static V3 operator +(V3 a, V3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static V3 operator -(V3 a, V3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static V3 operator *(V3 a, double k) => new(a.X * k, a.Y * k, a.Z * k);

    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

    public V3 Normalized()
    {
        var l = Length;
        return l < 1e-9 ? this : new V3(X / l, Y / l, Z / l);
    }

    public static V3 Cross(V3 a, V3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    public static double Dot(V3 a, V3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
}

/// <summary>A 2D point on the ground plane.</summary>
public readonly record struct P2(double X, double Y);

/// <summary>Polygon helpers shared by the world builder and the renderer.</summary>
public static class Poly
{
    public static double Area(IReadOnlyList<P2> p)
    {
        var a = 0d;
        for (var i = 0; i < p.Count; i++)
        {
            var q = p[i];
            var r = p[(i + 1) % p.Count];
            a += q.X * r.Y - r.X * q.Y;
        }

        return a / 2;
    }

    /// <summary>A rectangle of <paramref name="lx"/> × <paramref name="ly"/> centred on (cx, cy), rotated by <paramref name="ang"/>. Counter-clockwise.</summary>
    public static P2[] Rect(double cx, double cy, double lx, double ly, double ang = 0)
    {
        var c = Math.Cos(ang);
        var s = Math.Sin(ang);
        var hx = lx / 2;
        var hy = ly / 2;
        P2 Corner(double u, double v) => new(cx + u * c - v * s, cy + u * s + v * c);
        return new[] { Corner(-hx, -hy), Corner(hx, -hy), Corner(hx, hy), Corner(-hx, hy) };
    }

    /// <summary>Andrew's monotone chain; returns the hull counter-clockwise.</summary>
    public static P2[] ConvexHull(IEnumerable<P2> points)
    {
        var p = points.OrderBy(q => q.X).ThenBy(q => q.Y).ToArray();
        if (p.Length < 3)
        {
            return p;
        }

        static double Cross(P2 o, P2 a, P2 b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);

        var lower = new List<P2>();
        foreach (var q in p)
        {
            while (lower.Count >= 2 && Cross(lower[^2], lower[^1], q) <= 0)
            {
                lower.RemoveAt(lower.Count - 1);
            }

            lower.Add(q);
        }

        var upper = new List<P2>();
        for (var i = p.Length - 1; i >= 0; i--)
        {
            var q = p[i];
            while (upper.Count >= 2 && Cross(upper[^2], upper[^1], q) <= 0)
            {
                upper.RemoveAt(upper.Count - 1);
            }

            upper.Add(q);
        }

        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);
        lower.AddRange(upper);
        return lower.ToArray();
    }

    /// <summary>Clips a polygon to the land side (y ≤ 0) or the water side (y ≥ 0) of the quay line.</summary>
    public static P2[] ClipToQuayLine(IReadOnlyList<P2> poly, bool keepLand)
    {
        var output = new List<P2>();
        for (var i = 0; i < poly.Count; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Count];
            var inA = keepLand ? a.Y <= 0 : a.Y >= 0;
            var inB = keepLand ? b.Y <= 0 : b.Y >= 0;
            if (inA)
            {
                output.Add(a);
            }

            if (inA != inB)
            {
                var t = a.Y / (a.Y - b.Y);
                output.Add(new P2(a.X + (b.X - a.X) * t, 0));
            }
        }

        return output.ToArray();
    }

    public static bool Contains(IReadOnlyList<(float X, float Y)> poly, float px, float py)
    {
        var inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var a = poly[i];
            var b = poly[j];
            if ((a.Y > py) != (b.Y > py) && px < (b.X - a.X) * (py - a.Y) / (b.Y - a.Y) + a.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}

/// <summary>Deterministic PRNG (mulberry32) so the scene lays out identically on every run.</summary>
public sealed class SeededRandom
{
    private uint _state;

    public SeededRandom(int seed) => _state = unchecked((uint)seed);

    public double Next()
    {
        _state = unchecked(_state + 0x6D2B79F5);
        var t = unchecked((_state ^ (_state >> 15)) * (1 | _state));
        t = unchecked((t + (t ^ (t >> 7)) * (61 | t)) ^ t);
        return (t ^ (t >> 14)) / 4294967296.0;
    }
}
