using SkiaSharp;

namespace Cargo.Controls.Harbour;

/// <summary>
/// Every colour in the scene, resolved once from Themes/Tokens.xaml. No literals live here:
/// the scene follows the app's rule that colours resolve from the token file.
/// </summary>
public sealed class HarbourPalette
{
    private readonly Dictionary<string, SKColor> _cache = new();
    private readonly SKColor _skyTint;

    /// <summary>The sun direction (toward the light), fixed to the world.</summary>
    public static readonly V3 Light = new V3(-0.55, 0.4, 0.72).Normalized();

    public HarbourPalette() => _skyTint = this["HarbourSkyTintColor"];

    public SKColor this[string key]
    {
        get
        {
            if (!_cache.TryGetValue(key, out var color))
            {
                var c = Tokens.Color(key);
                color = new SKColor(c.R, c.G, c.B, c.A);
                _cache[key] = color;
            }

            return color;
        }
    }

    public SKColor WithAlpha(string key, double alpha) => this[key].WithAlpha((byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255));

    /// <summary>Soft studio light: low contrast between faces, and shaded faces drift toward the sky tint.</summary>
    public static double LightOf(V3 normal) => 0.66 + 0.4 * Math.Max(0, V3.Dot(normal, Light));

    public SKColor Shade(SKColor c, double light)
    {
        var t = Math.Max(0, 1 - light) * 0.4;
        var k = Math.Min(light, 1.04);
        byte Channel(byte v, byte tint) => (byte)Math.Clamp(Math.Round((v * (1 - t) + tint * t) * k), 0, 255);
        return new SKColor(Channel(c.Red, _skyTint.Red), Channel(c.Green, _skyTint.Green), Channel(c.Blue, _skyTint.Blue));
    }

    /// <summary>The thin bright line a model's top edges catch under soft light.</summary>
    public static SKColor EdgeLight(SKColor c)
    {
        const double k = 0.42;
        byte Channel(byte v) => (byte)Math.Round(v + (255 - v) * k);
        return new SKColor(Channel(c.Red), Channel(c.Green), Channel(c.Blue));
    }

    /// <summary>Hull and funnel livery by operator.</summary>
    public (SKColor Hull, SKColor Funnel) Livery(string @operator) => @operator switch
    {
        "MSC" => (this["HullMscColor"], this["FunnelMscColor"]),
        "ONE" => (this["HullOneColor"], this["FunnelOneColor"]),
        "Maersk" => (this["HullMaerskColor"], this["FunnelMaerskColor"]),
        "Hapag-Lloyd" => (this["HullHapagColor"], this["FunnelHapagColor"]),
        _ => (this["HullCmaColor"], this["FunnelCmaColor"])
    };

    private static readonly (string Key, double Weight)[] Liveries =
    {
        ("LiveryBlueColor", .17), ("LiveryRustColor", .13), ("LiveryOrangeColor", .08), ("LiveryMagentaColor", .06),
        ("LiveryWhiteColor", .14), ("LiveryGreyColor", .12), ("LiveryBeigeColor", .10), ("LiveryNavyColor", .12),
        ("LiveryOxideColor", .08)
    };

    public SKColor PickLivery(SeededRandom random)
    {
        var x = random.Next();
        foreach (var (key, weight) in Liveries)
        {
            if (x <= weight)
            {
                return this[key];
            }

            x -= weight;
        }

        return this[Liveries[^1].Key];
    }
}
