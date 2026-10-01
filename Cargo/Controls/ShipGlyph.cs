using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace Cargo.Controls;

/// <summary>
/// A ship in profile, to scale (EXTRACTION §2 "Ship glyph", 0.2825 px per metre, 26 tall): the
/// navy hull with its bow to the left, deck stacks up to the bridge, the bridge and funnel, the
/// waterline, and a wake off the stern while it departs. Static: it redraws only when its
/// properties change.
/// </summary>
// xaml-lint: allow builtin - a drawn glyph, not a control the platform ships
public sealed partial class ShipGlyph : SKCanvasElement
{
    public const double PixelsPerMetre = .2825;
    private const float WakeRoom = 18;

    private static readonly string[] BoxTokens =
        { "AccentColor", "WaterColor", "GlyphBoxPaleColor", "GlyphBoxLightColor", "GlyphBoxSlateColor", "GlyphBoxMistColor", "GlyphBoxStoneColor" };

    private readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private readonly SKPath _hull = new();

    public ShipGlyph()
    {
        Height = 26;
        IsHitTestVisible = false;
    }

    public static readonly DependencyProperty LengthProperty = DependencyProperty.Register(
        nameof(Length), typeof(double), typeof(ShipGlyph), new PropertyMetadata(250d, (d, _) => ((ShipGlyph)d).Resize()));

    /// <summary>Length overall in metres.</summary>
    public double Length
    {
        get => (double)GetValue(LengthProperty);
        set => SetValue(LengthProperty, value);
    }

    public static readonly DependencyProperty DepartingProperty = DependencyProperty.Register(
        nameof(Departing), typeof(bool), typeof(ShipGlyph), new PropertyMetadata(false, (d, _) => ((ShipGlyph)d).Invalidate()));

    /// <summary>Draws the wake off the stern.</summary>
    public bool Departing
    {
        get => (bool)GetValue(DepartingProperty);
        set => SetValue(DepartingProperty, value);
    }

    public static readonly DependencyProperty SeedProperty = DependencyProperty.Register(
        nameof(Seed), typeof(string), typeof(ShipGlyph), new PropertyMetadata(string.Empty, (d, _) => ((ShipGlyph)d).Invalidate()));

    /// <summary>Picks the deck stacks and their colours, so each ship keeps its own load between redraws.</summary>
    public string Seed
    {
        get => (string)GetValue(SeedProperty);
        set => SetValue(SeedProperty, value);
    }

    private void Resize()
    {
        Width = Length * PixelsPerMetre + WakeRoom;
        Invalidate();
    }

    private static SKColor Sk(string key, double alpha = 1)
    {
        var c = Tokens.Color(key);
        return new SKColor(c.R, c.G, c.B, (byte)Math.Round(c.A * alpha));
    }

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        var w = (float)(Length * PixelsPerMetre);

        // Waterline
        _stroke.StrokeWidth = 1.25f;
        _stroke.Color = Sk("WaterColor", .7);
        canvas.DrawLine(-3, 22.5f, w + 2, 22.5f, _stroke);

        // Deck stacks, 2 or 3 high, from just aft of the bow to the bridge
        var bridgeX = w * .8f;
        var seed = Seed.Aggregate(17, (h, ch) => h * 31 + ch);
        float[] rows = { 11.75f, 8.25f, 4.75f };
        for (var n = 0; 7 + 5.5f * n + 4.5f < bridgeX - 1; n++)
        {
            var stack = 2 + (Math.Abs(seed + n * 7) % 2);
            for (var r = 0; r < stack; r++)
            {
                _fill.Color = Sk(BoxTokens[Math.Abs(seed + n * 3 + r * 5) % BoxTokens.Length]);
                canvas.DrawRect(7 + 5.5f * n, rows[r], 4.5f, 3, _fill);
            }
        }

        // Hull, bow to the left
        _hull.Reset();
        _hull.MoveTo(0, 14.4f);
        _hull.LineTo(w - 1, 15);
        _hull.LineTo(w - 2.2f, 22);
        _hull.LineTo(6, 22);
        _hull.CubicTo(3, 22, 1.2f, 18.5f, 0, 14.4f);
        _hull.Close();
        _fill.Color = Sk("NavyColor");
        canvas.DrawPath(_hull, _fill);

        // Bridge with its window line, then the funnel
        var bridge = new SKRoundRect(SKRect.Create(bridgeX, 1, 7, 14), .6f);
        _fill.Color = Sk("GlyphBridgeColor");
        canvas.DrawRoundRect(bridge, _fill);
        _stroke.StrokeWidth = .75f;
        _stroke.Color = Sk("InkColor", .35);
        canvas.DrawRoundRect(bridge, _stroke);
        _stroke.StrokeWidth = .6f;
        _stroke.Color = Sk("InkColor");
        canvas.DrawLine(bridgeX + 1, 3.4f, bridgeX + 6, 3.4f, _stroke);
        _fill.Color = Sk("NavyColor");
        canvas.DrawRect(bridgeX + 8, 6, 3, 9, _fill);

        if (Departing)
        {
            _stroke.StrokeWidth = 1;
            _stroke.Color = Sk("WaterColor");
            canvas.DrawLine(w + 2, 21, w + 16, 21.8f, _stroke);
            canvas.DrawLine(w + 3, 18.6f, w + 12, 19.4f, _stroke);
        }
    }
}
