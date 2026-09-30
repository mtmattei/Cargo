using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Cargo.Domain;

/// <summary>
/// Typed access to the palette declared in Themes/Tokens.xaml, so view models can hand
/// brushes to the view without any colour literal ever appearing in code.
/// </summary>
public static class Tokens
{
    private static readonly Dictionary<string, Color> ColorCache = new();
    private static readonly Dictionary<(string, int), SolidColorBrush> BrushCache = new();

    public static Color Color(string key)
    {
        if (ColorCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var resources = Application.Current.Resources;
        Color value = default;

        if (resources.TryGetValue(key, out var raw))
        {
            value = raw switch
            {
                Color c => c,
                // A brush token carries its transparency in Opacity (the hairlines are ink at
                // 4.5-14%); fold it into the alpha, or every code-set hairline renders solid ink.
                SolidColorBrush b => Windows.UI.Color.FromArgb((byte)Math.Round(b.Color.A * b.Opacity), b.Color.R, b.Color.G, b.Color.B),
                _ => default
            };
        }
        else if (resources.TryGetValue(key + "Color", out var rawColor) && rawColor is Color c2)
        {
            value = c2;
        }

        ColorCache[key] = value;
        return value;
    }

    /// <summary>Brush for a palette key, optionally at reduced opacity. Instances are cached and shared.</summary>
    public static SolidColorBrush Brush(string key, double opacity = 1d)
    {
        var bucket = (int)Math.Round(opacity * 1000);
        if (BrushCache.TryGetValue((key, bucket), out var cached))
        {
            return cached;
        }

        var brush = new SolidColorBrush(Color(key)) { Opacity = bucket / 1000d };
        BrushCache[(key, bucket)] = brush;
        return brush;
    }

    /// <summary>
    /// A token at partial alpha with the alpha in the colour itself (brush Opacity 1). Use it for
    /// anything a <see cref="Microsoft.UI.Xaml.BrushTransition"/> animates: on Uno Skia the transition
    /// interpolates Color only, so an Opacity-based brush fades to and from solid colour.
    /// </summary>
    public static SolidColorBrush Tint(string key, double alpha)
    {
        var c = Color(key);
        return Of(Windows.UI.Color.FromArgb((byte)Math.Round(c.A * alpha), c.R, c.G, c.B));
    }

    private static readonly Dictionary<(uint, int), SolidColorBrush> AdHoc = new();

    /// <summary>Brush for a colour that was resolved from a token but then blended or shaded.</summary>
    public static SolidColorBrush Of(Color color, double opacity = 1d)
    {
        var bucket = (int)Math.Round(opacity * 1000);
        var packed = ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
        if (AdHoc.TryGetValue((packed, bucket), out var cached))
        {
            return cached;
        }

        var brush = new SolidColorBrush(color) { Opacity = bucket / 1000d };
        AdHoc[(packed, bucket)] = brush;
        return brush;
    }

    public static Color Shade(Color source, int delta)
    {
        static byte Clamp(int v) => (byte)Math.Clamp(v, 0, 255);
        return Windows.UI.Color.FromArgb(source.A, Clamp(source.R + delta), Clamp(source.G + delta), Clamp(source.B + delta));
    }

    public static readonly SolidColorBrush Transparent = new(Windows.UI.Color.FromArgb(0, 0, 0, 0));
}
