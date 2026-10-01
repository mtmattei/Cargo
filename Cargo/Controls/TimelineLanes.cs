using Cargo.Presentation;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace Cargo.Controls;

/// <summary>
/// The timeline's lanes on one time axis (EXTRACTION §2 "Today at North Quay"): vessels, moves and
/// cargo, the 3-hourly grid, the axis, the future shade and the NOW line. The tide lane is a
/// Liveline chart laid over its band. Everything is in the design's viewBox units: 580 tall, the
/// summary column 0-168 (XAML text over it), the plot from 168 to 12 short of the right edge.
/// </summary>
// xaml-lint: allow builtin - a bespoke canvas by decision (spec: four lanes share one axis, one NOW line and a future shade)
public sealed partial class TimelineLanes : SKCanvasElement
{
    public const double DesignHeight = 580;
    public const float PlotLeft = 168, PlotRightInset = 12;
    private const float PlotTop = 28, PlotBottom = 540;
    private const float VesselsTop = 28, TideTop = 164, MovesTop = 300, CargoTop = 428;
    private const float Baseline = 92, ArrivalDot = 84, DepartureDot = 100;

    private readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
    private readonly SKPaint _halo = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4, StrokeJoin = SKStrokeJoin.Round };
    private readonly SKFont _label = new(SKTypeface.Default, 12);
    private readonly SKFont _mono = new(SKTypeface.Default, 12);
    private readonly SKFont _monoStrong = new(SKTypeface.Default, 13);
    private readonly SKPath _path = new();
    private readonly SKPath _area = new();

    // Palette, resolved once from Themes/Tokens.xaml (the canvas has no XAML to bind)
    private readonly SKColor _ink = Sk("InkColor"), _muted = Sk("TextMutedColor"), _surface = Sk("SurfaceColor"),
        _navy = Sk("NavyColor"), _water = Sk("WaterColor"), _accent = Sk("AccentColor"), _ground = Sk("PaperColor");

    public TimelineLanes()
    {
        Height = DesignHeight;
        LoadFonts();
    }

    public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(
        nameof(Frame), typeof(TimelineFrame), typeof(TimelineLanes),
        new PropertyMetadata(null, (d, _) => ((TimelineLanes)d).Invalidate()));

    /// <summary>The tick's snapshot: NOW, the events, the moves and cargo series. Immutable, read on the render thread.</summary>
    public TimelineFrame? Frame
    {
        get => (TimelineFrame?)GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
    }

    private async void LoadFonts()
    {
        if (await CanvasFonts.Load(CanvasFonts.BodyMedium) is { } body)
        {
            _label.Typeface = body;
        }

        if (await CanvasFonts.Load(CanvasFonts.Mono) is { } mono)
        {
            _mono.Typeface = mono;
        }

        if (await CanvasFonts.Load(CanvasFonts.MonoMedium) is { } monoMedium)
        {
            _monoStrong.Typeface = monoMedium;
        }

        Invalidate();
    }

    private static SKColor Sk(string key)
    {
        var c = Tokens.Color(key);
        return new SKColor(c.R, c.G, c.B, c.A);
    }

    private static SKColor Alpha(SKColor c, double a) => c.WithAlpha((byte)Math.Round(255 * a));

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        if (Frame is not { } frame)
        {
            return;
        }

        var width = (float)area.Width;
        var right = width - PlotRightInset;
        var perHour = (right - PlotLeft) / (float)(TimelineViewModel.End - TimelineViewModel.Start);
        float X(double hour) => PlotLeft + (float)(hour - TimelineViewModel.Start) * perHour;
        var nowX = X(frame.Now);

        // Ahead of now: a 3% ink shade over every lane
        _fill.Color = Alpha(_ink, .03);
        canvas.DrawRect(new SKRect(nowX, PlotTop, right, PlotBottom), _fill);

        // Gridlines every 3 h; Thursday 00:00 stronger
        for (var h = (int)TimelineViewModel.Start; h <= (int)TimelineViewModel.End; h += 3)
        {
            _stroke.StrokeWidth = 1;
            _stroke.Color = Alpha(_ink, h == 24 ? .18 : .08);
            canvas.DrawLine(X(h), PlotTop, X(h), PlotBottom, _stroke);
        }

        // Lane dividers: the y = 300 rule between the water and the quay is the strong one
        foreach (var (y, a) in new[] { (TideTop, .08), (MovesTop, .18), (CargoTop, .08) })
        {
            _stroke.Color = Alpha(_ink, a);
            canvas.DrawLine(0, y, width, y, _stroke);
        }

        DrawVessels(canvas, frame, X, right);
        DrawMoves(canvas, frame, X, nowX);
        DrawCargo(canvas, frame, X, nowX);
        DrawAxis(canvas, X, nowX);
        DrawNow(canvas, frame, nowX);
    }

    private void DrawVessels(SKCanvas canvas, TimelineFrame frame, Func<double, float> x, float right)
    {
        _stroke.StrokeWidth = 1;
        _stroke.Color = Alpha(_ink, .18);
        canvas.DrawLine(PlotLeft, Baseline, right, Baseline, _stroke);

        // Labels stack into tiers away from the baseline so neighbours never collide
        var arrivalTiers = new List<float>();
        var departureTiers = new List<float>();
        foreach (var e in frame.Events)
        {
            var happened = e.Hour <= frame.Now;
            var cx = x(e.Hour);
            var cy = e.Arrival ? ArrivalDot : DepartureDot;
            var time = PortState.Format(e.Hour);
            var nameWidth = _label.MeasureText(e.Name);
            var labelWidth = nameWidth + 6 + _mono.MeasureText(time);
            var left = Math.Clamp(cx - labelWidth / 2, PlotLeft, right - labelWidth);

            var tiers = e.Arrival ? arrivalTiers : departureTiers;
            var tier = tiers.FindIndex(end => left > end + 10);
            if (tier < 0)
            {
                tier = tiers.Count;
                tiers.Add(0);
            }

            tiers[tier] = left + labelWidth;
            var labelY = e.Arrival ? cy - 16 - tier * 16 : cy + 24 + tier * 16;

            // Leader from the dot to the label
            _stroke.StrokeWidth = 1;
            _stroke.Color = Alpha(_ink, .18);
            canvas.DrawLine(cx, cy, cx, e.Arrival ? labelY + 4 : labelY - 12, _stroke);

            // Happened: filled navy with a surface ring; planned: hollow navy ring
            _fill.Color = happened ? _navy : _surface;
            canvas.DrawCircle(cx, cy, 5, _fill);
            _stroke.StrokeWidth = happened ? 2 : 1.6f;
            _stroke.Color = happened ? _surface : _navy;
            canvas.DrawCircle(cx, cy, happened ? 5 : 4.2f, _stroke);

            Text(canvas, e.Name, left, labelY, _label, happened ? _ink : _muted);
            Text(canvas, time, left + nameWidth + 6, labelY, _mono, _muted);
        }
    }

    private void DrawMoves(SKCanvas canvas, TimelineFrame frame, Func<double, float> x, float nowX)
    {
        static float Y(double v) => 412 - .46f * (float)v;

        // The 100 / h reference
        _stroke.StrokeWidth = 1;
        _stroke.Color = Alpha(_ink, .08);
        canvas.DrawLine(PlotLeft, Y(100), nowX, Y(100), _stroke);
        Text(canvas, "100 / h", PlotLeft + 4, Y(100) - 5, _mono, _muted);

        Smooth(frame.Moves.Select(m => new SKPoint(x(m.Hour), Y(m.Value))).ToArray());
        _area.Reset();
        _area.AddPath(_path);
        _area.LineTo(nowX, Y(0));
        _area.LineTo(x(frame.Moves[0].Hour), Y(0));
        _area.Close();
        using (var shader = SKShader.CreateLinearGradient(new SKPoint(0, MovesTop), new SKPoint(0, Y(0)),
                   new[] { Alpha(_accent, .2), Alpha(_accent, 0) }, null, SKShaderTileMode.Clamp))
        {
            _fill.Shader = shader;
            canvas.DrawPath(_area, _fill);
            _fill.Shader = null;
        }

        _stroke.StrokeWidth = 2;
        _stroke.Color = _accent;
        canvas.DrawPath(_path, _stroke);

        // The peak, hollow, with its value
        var (peakHour, peakValue) = frame.MovesPeak;
        _fill.Color = _surface;
        canvas.DrawCircle(x(peakHour), Y(peakValue), 3.5f, _fill);
        _stroke.StrokeWidth = 1.6f;
        canvas.DrawCircle(x(peakHour), Y(peakValue), 3.5f, _stroke);
        Text(canvas, $"{peakValue:0}", x(peakHour) - 8, Y(peakValue) - 8, _mono, _muted);

        // Now: the live rate
        var now = frame.Moves[^1];
        _fill.Color = _accent;
        canvas.DrawCircle(nowX, Y(now.Value), 5, _fill);
        _stroke.StrokeWidth = 2;
        _stroke.Color = _surface;
        canvas.DrawCircle(nowX, Y(now.Value), 5, _stroke);
        Text(canvas, $"{now.Value:0} / h", nowX + 10, Y(now.Value) + 4, _monoStrong, _ink);
    }

    private void DrawCargo(SKCanvas canvas, TimelineFrame frame, Func<double, float> x, float nowX)
    {
        // Fitted to the lane: the busiest hour (170 out) sits 6 px under the lane's top
        static float Y(double v) => 534 - .52f * (float)v;

        var inPoints = frame.Cargo.Select(c => new SKPoint(x(c.Hour), Y(c.In))).ToArray();
        var outPoints = frame.Cargo.Select(c => new SKPoint(x(c.Hour), Y(c.Out))).ToArray();

        // The band between in and out, in water at 16%
        Smooth(outPoints);
        _area.Reset();
        _area.AddPath(_path);
        Smooth(inPoints.Reverse().ToArray());
        _area.AddPath(_path, SKPathAddMode.Extend);
        _area.Close();
        _fill.Color = Alpha(_water, .16);
        canvas.DrawPath(_area, _fill);

        _stroke.StrokeWidth = 2;
        Smooth(inPoints);
        _stroke.Color = _navy;
        canvas.DrawPath(_path, _stroke);
        Smooth(outPoints);
        _stroke.Color = _water;
        canvas.DrawPath(_path, _stroke);

        var now = frame.Cargo[^1];
        var outAbove = now.Out >= now.In;
        Text(canvas, $"out {now.Out:0}", nowX + 8, Y(now.Out) + (outAbove ? -2 : 12), _mono, _muted);
        Text(canvas, $"in {now.In:0}", nowX + 8, Y(now.In) + (outAbove ? 12 : -2), _mono, _muted);
    }

    private void DrawAxis(SKCanvas canvas, Func<double, float> x, float nowX)
    {
        for (var h = (int)TimelineViewModel.Start; h <= (int)TimelineViewModel.End; h++)
        {
            var major = h % 3 == 0;
            _stroke.StrokeWidth = 1;
            _stroke.Color = Alpha(_ink, .18);
            canvas.DrawLine(x(h), PlotBottom, x(h), PlotBottom + (major ? 8 : 5), _stroke);
            if (!major)
            {
                continue;
            }

            var label = h == 24 ? "Thu 00:00" : PortState.Format(h);
            var w = _mono.MeasureText(label);
            var lx = h == (int)TimelineViewModel.Start ? x(h)
                : h == (int)TimelineViewModel.End ? x(h) - w
                : x(h) - w / 2;

            // The label under NOW gives way to the NOW pill's line
            if (Math.Abs(x(h) - nowX) < 28 && h != (int)TimelineViewModel.Start && h != (int)TimelineViewModel.End)
            {
                continue;
            }

            Text(canvas, label, lx, 568, h == 24 ? _monoStrong : _mono, h == 24 ? _ink : _muted);
        }
    }

    private void DrawNow(SKCanvas canvas, TimelineFrame frame, float nowX)
    {
        _stroke.StrokeWidth = 1.25f;
        _stroke.Color = _ink;
        canvas.DrawLine(nowX, 20, nowX, 548, _stroke);

        var text = $"NOW {PortState.Format(frame.Now)}";
        var pill = SKRect.Create(nowX - 40, 0, 80, 22);
        _fill.Color = _ink;
        canvas.DrawRoundRect(pill, 11, 11, _fill);
        _fill.Color = _ground;
        var w = _monoStrong.MeasureText(text);
        canvas.DrawText(text, nowX - w / 2, 15.5f, SKTextAlign.Left, _monoStrong, _fill);
    }

    /// <summary>Text on a 4 px surface halo, so it reads over gridlines and areas.</summary>
    private void Text(SKCanvas canvas, string text, float x, float y, SKFont font, SKColor color)
    {
        _halo.Color = _surface;
        canvas.DrawText(text, x, y, SKTextAlign.Left, font, _halo);
        _fill.Color = color;
        canvas.DrawText(text, x, y, SKTextAlign.Left, font, _fill);
    }

    /// <summary>A smooth line through the points into <see cref="_path"/> (cubic, control points 30% out).</summary>
    private void Smooth(IReadOnlyList<SKPoint> points)
    {
        _path.Reset();
        if (points.Count == 0)
        {
            return;
        }

        _path.MoveTo(points[0]);
        for (var i = 1; i < points.Count; i++)
        {
            var (a, b) = (points[i - 1], points[i]);
            var dx = (b.X - a.X) * .3f;
            _path.CubicTo(a.X + dx, a.Y, b.X - dx, b.Y, b.X, b.Y);
        }
    }
}
