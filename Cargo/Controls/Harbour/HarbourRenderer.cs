using SkiaSharp;

namespace Cargo.Controls.Harbour;

/// <summary>What the scene needs to know about app state for one frame.</summary>
public sealed record HarbourFrameState(
    string? SelectedVessel,
    string? HoveredVessel,
    int? HoveredBerth,
    string Layer,
    bool Animate);

/// <summary>
/// Draws the harbour with SkiaSharp. The still scene (ground, chart, yard, ships, shadows) is
/// recorded into an <see cref="SKPicture"/> whenever the camera, layer or size changes; each frame
/// replays it, adds the tilt-shift blur, then draws the live layer (crane trolleys, trucks, booms)
/// and the overlays on top. Paints, paths and filters are created once and reused.
/// </summary>
public sealed class HarbourRenderer : IDisposable
{
    private readonly HarbourWorld _world;
    private readonly HarbourPalette _pal;
    private readonly HarbourCamera _camera;

    private readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeJoin = SKStrokeJoin.Round, StrokeCap = SKStrokeCap.Round };
    private readonly SKPaint _text = new() { IsAntialias = true };
    private readonly SKPaint _layerBlur = new();
    private readonly SKPaint _mask = new() { BlendMode = SKBlendMode.DstIn };
    private readonly SKPath _path = new();
    private readonly SKFont _chartFont = new() { Size = 10, Subpixel = true };
    private readonly SKFont _paintFont = new() { Size = 7, Subpixel = true, Embolden = true };
    private readonly Dictionary<int, SKMaskFilter> _maskFilters = new();
    private readonly Dictionary<int, SKImageFilter> _blurFilters = new();
    private readonly SKPathEffect _dashRoute = SKPathEffect.CreateDash(new[] { 6f, 5f }, 0);
    private readonly SKPathEffect _dashFairway = SKPathEffect.CreateDash(new[] { 10f, 6f }, 0);
    private readonly SKPathEffect _dashTile = SKPathEffect.CreateDash(new[] { 5f, 4f }, 0);

    private SKPicture? _still;
    private string _stillKey = string.Empty;

    // The still scene with its tilt-shift, baked once the camera settles. Replaying the picture
    // twice and blurring the full frame cost well over a core at 30 fps; a settled frame is now
    // one image draw plus the live layer.
    private SKImage? _baked;
    private string _bakedKey = string.Empty;
    private string _lastKey = string.Empty;
    private SKShader? _maskShader;
    private SKShader? _hazeShader;
    private readonly SKShader?[] _fadeShaders = new SKShader?[4];

    public HarbourRenderer(HarbourWorld world, HarbourPalette palette, HarbourCamera camera)
    {
        _world = world;
        _pal = palette;
        _camera = camera;
    }

    /// <summary>Anchor (mast top) and tag foot for each vessel's leader line, in view coordinates.</summary>
    public IReadOnlyDictionary<string, (SKPoint Anchor, SKPoint Tag)> Leaders { get; set; } =
        new Dictionary<string, (SKPoint, SKPoint)>();

    public void SetTypeface(SKTypeface typeface)
    {
        _chartFont.Typeface = typeface;
        _paintFont.Typeface = typeface;
        _paintFont.Embolden = false;
        Invalidate();
    }

    /// <summary>Forces the still scene to be recorded again on the next frame.</summary>
    public void Invalidate()
    {
        _stillKey = string.Empty;
        _bakedKey = string.Empty;
    }

    public void Render(SKCanvas canvas, float width, float height, double seconds, HarbourFrameState state)
    {
        if (width < 2 || height < 2)
        {
            return;
        }

        _camera.Update(width, height);

        var key = $"{_camera.Pose}|{width}|{height}|{_camera.TopInset}|{state.Layer}";
        if (key != _stillKey || _still is null)
        {
            _stillKey = key;
            RecordStill(width, height, state);
        }

        var bounds = new SKRect(0, 0, width, height);
        canvas.Clear(_pal["HarbourStageColor"]);

        // While the camera moves, the key changes every frame: draw directly and never pay for a bake.
        // The first frame it holds still, bake, and every frame after that is a single image draw.
        var settled = key == _lastKey;
        _lastKey = key;
        if (settled && _bakedKey != key)
        {
            Bake(canvas, width, height, key);
        }

        if (_baked is not null && _bakedKey == key)
        {
            canvas.DrawImage(_baked, bounds);
        }
        else
        {
            canvas.DrawPicture(_still);
            TiltShift(canvas, width, height);
        }

        DrawLive(canvas, seconds, state);
        DrawAtmosphere(canvas, width, height);
        DrawLeaders(canvas, state);
    }

    /// <summary>
    /// Renders the sharp still plus tilt-shift into an image at device resolution, on the same
    /// GPU context as the window when there is one, so drawing it back is a texture blit.
    /// </summary>
    private void Bake(SKCanvas target, float width, float height, string key)
    {
        var scale = target.TotalMatrix;
        var sx = Math.Max(1f, Math.Abs(scale.ScaleX));
        var sy = Math.Max(1f, Math.Abs(scale.ScaleY));
        var info = new SKImageInfo((int)Math.Ceiling(width * sx), (int)Math.Ceiling(height * sy),
            SKColorType.Rgba8888, SKAlphaType.Premul);

        using var surface = target.Context is { } gpu
            ? SKSurface.Create(gpu, true, info) ?? SKSurface.Create(info)
            : SKSurface.Create(info);
        if (surface is null)
        {
            return;
        }

        var canvas = surface.Canvas;
        canvas.Scale(sx, sy);
        canvas.Clear(_pal["HarbourStageColor"]);
        canvas.DrawPicture(_still);
        TiltShift(canvas, width, height);

        _baked?.Dispose();
        _baked = surface.Snapshot();
        _bakedKey = key;
    }

    // ── Still scene ────────────────────────────────────────────────────────────

    private void RecordStill(float width, float height, HarbourFrameState state)
    {
        _still?.Dispose();
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(new SKRect(0, 0, width, height));

        DrawGround(canvas, state);
        var list = _world.Objects.ToList();
        SortAndDraw(canvas, list);
        GroundText(canvas, "WESTHAVEN", 420, -150, HarbourWorld.QuayZ + 4.52, _pal.WithAlpha("HarbourSignColor", .75), 6.5f);

        _still = recorder.EndRecording();
        RebuildScreenShaders(width, height);
    }

    private void RebuildScreenShaders(float width, float height)
    {
        // Tilt-shift mask: opaque at the top and bottom of the frame, clear through the focus band.
        var focus = Math.Clamp(_camera.Project(_camera.TargetX, _camera.TargetY, 0).Y / height, .3f, .75f);
        var a = Math.Max(0, focus - .2f);
        var c = Math.Min(1, focus + .16f);
        _maskShader?.Dispose();
        _maskShader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, height),
            new[] { SKColors.Black, SKColors.Transparent, SKColors.Transparent, SKColors.Black },
            new[] { 0f, a, c, 1f }, SKShaderTileMode.Clamp);

        // Haze toward the horizon, strongest at low pitch.
        var stage = _pal["HarbourStageColor"];
        var hazeAlpha = (byte)Math.Round(255 * .7 * Math.Max(0, Math.Cos(_camera.Pitch) - .1));
        _hazeShader?.Dispose();
        _hazeShader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, height * .62f),
            new[] { stage.WithAlpha(hazeAlpha), stage.WithAlpha(0) }, null, SKShaderTileMode.Clamp);

        var top = (float)Math.Clamp(_camera.TopInset, 0, height * .5);
        // The world has no edge: it dissolves into the stage at the frame.
        var fades = new (SKPoint From, SKPoint To)[]
        {
            (new(0, top), new(0, top + (height - top) * .14f)),
            (new(0, height), new(0, height * .92f)),
            (new(0, 0), new(width * .05f, 0)),
            (new(width, 0), new(width * .95f, 0))
        };
        for (var i = 0; i < 4; i++)
        {
            _fadeShaders[i]?.Dispose();
            _fadeShaders[i] = SKShader.CreateLinearGradient(fades[i].From, fades[i].To,
                new[] { stage, stage.WithAlpha(0) }, null, SKShaderTileMode.Clamp);
        }
    }

    private void DrawGround(SKCanvas canvas, HarbourFrameState state)
    {
        var cam = _camera;

        // Water, darker by the quay and paling toward open water.
        var near = cam.Project(cam.TargetX, 0, 0);
        var far = cam.Project(cam.TargetX, HarbourWorld.SeaY1, 0);
        using (var water = SKShader.CreateLinearGradient(new SKPoint(near.X, near.Y), new SKPoint(far.X, far.Y),
                   new[] { _pal["HarbourSeaNearColor"], _pal["HarbourSeaFarColor"] }, null, SKShaderTileMode.Clamp))
        {
            TracePolygon(new[] { new V3(HarbourWorld.X0, 0, 0), new V3(HarbourWorld.X1, 0, 0), new V3(HarbourWorld.X1, HarbourWorld.SeaY1, 0), new V3(HarbourWorld.X0, HarbourWorld.SeaY1, 0) });
            _fill.Shader = water;
            canvas.DrawPath(_path, _fill);
            _fill.Shader = null;
        }

        SunGlint(canvas);
        DrawChart(canvas);

        // Berth tiles: status colour is reserved for the berths that need attention.
        foreach (var berth in _world.Berths)
        {
            if (berth.State == "restricted")
            {
                BerthTile(canvas, berth, _pal.WithAlpha("AlertColor", .16), _pal["AlertColor"], dashed: true);
            }
            else if (berth.State == "reserved")
            {
                BerthTile(canvas, berth, _pal.WithAlpha("AmberColor", .22), _pal["AmberDeepColor"], dashed: false);
            }
        }

        DrawRoutes(canvas, state.Layer);

        foreach (var vessel in _world.Vessels.Where(v => PortData.Vessel(v.Id).Status == "Arriving"))
        {
            Wake(canvas, vessel, 70);
        }

        foreach (var tug in _world.Tugs)
        {
            Wake(canvas, tug, 18);
        }

        Shadows(canvas, _world.SeaShadows, 0, 3, .22);
        Shadows(canvas, _world.SeaContact, 0, 4, .12);
        Shadows(canvas, _world.SeaContact, 0, .8, .22);

        DrawFace(canvas, _world.QuaySide);
        DrawFace(canvas, _world.Land);
        foreach (var (poly, color) in _world.Decals)
        {
            TraceGround(poly, HarbourWorld.QuayZ);
            _fill.Color = color;
            canvas.DrawPath(_path, _fill);
        }

        if (state.Layer == "yard")
        {
            YardLayer(canvas);
        }

        if (state.Layer == "security")
        {
            SecurityLayer(canvas);
        }

        // Berth numbers painted on the quay: in the world rather than floating over it.
        foreach (var berth in _world.Berths)
        {
            var color = berth.State switch
            {
                "reserved" => _pal["AmberDeepColor"],
                "restricted" => _pal["AlertColor"],
                _ => _pal.WithAlpha("HarbourSignColor", .6)
            };
            GroundText(canvas, berth.Number, berth.X - 34, -17, HarbourWorld.QuayZ + .02, color, 9);
        }

        Shadows(canvas, _world.LandShadows, HarbourWorld.QuayZ, 2.5, .19);
        Shadows(canvas, _world.LandContact, HarbourWorld.QuayZ, 3, .11);
        Shadows(canvas, _world.LandContact, HarbourWorld.QuayZ, .6, .18);
    }

    private void SunGlint(SKCanvas canvas)
    {
        var light = HarbourPalette.Light;
        var len = Math.Sqrt(light.X * light.X + light.Y * light.Y);
        var sx = light.X / len;
        var sy = light.Y / len;
        var facing = _camera.Facing(sx, sy);
        if (facing <= 0)
        {
            return;
        }

        var at = _camera.Project(_camera.TargetX + sx * 240, _camera.TargetY + sy * 240, 0);
        var radius = (float)(480 * _camera.Scale);
        var glint = _pal["HarbourGlintColor"];
        using var shader = SKShader.CreateRadialGradient(new SKPoint(at.X, at.Y), Math.Max(1, radius),
            new[] { glint.WithAlpha((byte)(255 * .35 * facing)), glint.WithAlpha(0) }, null, SKShaderTileMode.Clamp);

        canvas.Save();
        TracePolygon(new[] { new V3(HarbourWorld.X0, 0, 0), new V3(HarbourWorld.X1, 0, 0), new V3(HarbourWorld.X1, HarbourWorld.SeaY1, 0), new V3(HarbourWorld.X0, HarbourWorld.SeaY1, 0) });
        canvas.ClipPath(_path, antialias: true);
        _fill.Shader = shader;
        canvas.DrawRect(new SKRect(0, 0, (float)_camera.ViewWidth, (float)_camera.ViewHeight), _fill);
        _fill.Shader = null;
        canvas.Restore();
    }

    private void DrawChart(SKCanvas canvas)
    {
        var ink = "HarbourDepthInkColor";

        // Berth pocket dredged to 16 m along the quay.
        TraceGround(new[] { new P2(-420, 0), new P2(420, 0), new P2(420, 22), new P2(-420, 22) }, .01);
        _fill.Color = _pal.WithAlpha(ink, .07);
        canvas.DrawPath(_path, _fill);

        // Fairway limits.
        _stroke.Color = _pal.WithAlpha(ink, .42);
        _stroke.StrokeWidth = 1;
        _stroke.PathEffect = _dashFairway;
        foreach (var y in new[] { 95d, 186 })
        {
            TraceLine(new[] { new P2(HarbourWorld.X0, y), new P2(HarbourWorld.X1, y) }, .02);
            canvas.DrawPath(_path, _stroke);
        }

        _stroke.PathEffect = null;

        // Isobaths.
        _stroke.Color = _pal.WithAlpha(ink, .3);
        foreach (var (_, points) in _world.Isobaths)
        {
            TraceLine(points, .02);
            canvas.DrawPath(_path, _stroke);
        }

        // Soundings and contour values, set in screen space like a chart.
        var labels = _world.Isobaths.Select(i => i.Points[(int)(i.Points.Length * .62)]).ToArray();
        _text.Color = _pal.WithAlpha(ink, .62);
        if (_camera.Scale > 1.1)
        {
            foreach (var (x, y, depth) in _world.Soundings)
            {
                if (labels.Any(p => Math.Sqrt((p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y)) < 45))
                {
                    continue;
                }

                var q = _camera.Project(x, y, 0);
                if (q.X > 0 && q.X < _camera.ViewWidth && q.Y > 0 && q.Y < _camera.ViewHeight)
                {
                    canvas.DrawText(depth.ToString("0.0"), q.X, q.Y + 3.5f, SKTextAlign.Center, _chartFont, _text);
                }
            }
        }

        for (var i = 0; i < labels.Length; i++)
        {
            var q = _camera.Project(labels[i].X, labels[i].Y, 0);
            _fill.Color = _pal["HarbourSeaNearColor"];
            canvas.DrawRect(q.X - 9, q.Y - 6, 18, 12, _fill);
            canvas.DrawText(_world.Isobaths[i].Level.ToString(), q.X, q.Y + 3.5f, SKTextAlign.Center, _chartFont, _text);
        }

        var fq = _camera.Project(-700, 140, 0);
        _text.Color = _pal.WithAlpha(ink, .45);
        canvas.DrawText("FAIRWAY  17.0 m", fq.X, fq.Y, SKTextAlign.Center, _chartFont, _text);
    }

    private void BerthTile(SKCanvas canvas, BerthSpot berth, SKColor fill, SKColor stroke, bool dashed)
    {
        TraceGround(Poly.Rect(berth.X, 7.3, 92, 12.5), .03);
        _fill.Color = fill;
        canvas.DrawPath(_path, _fill);
        _stroke.Color = stroke;
        _stroke.StrokeWidth = 1.4f;
        _stroke.PathEffect = dashed ? _dashTile : null;
        canvas.DrawPath(_path, _stroke);
        _stroke.PathEffect = null;
    }

    /// <summary>The planned track of the inbound vessel, and on the Traffic layer the other movements too.</summary>
    private void DrawRoutes(SKCanvas canvas, string layer)
    {
        foreach (var vessel in _world.Vessels)
        {
            var data = PortData.Vessel(vessel.Id);
            if (data.Status != "Arriving")
            {
                continue;
            }

            var berth = _world.Berths.First(b => b.Number == data.HomeBerth);
            var bow = new P2(vessel.X + Math.Cos(vessel.Heading) * (vessel.Length / 2 + 2), vessel.Y + Math.Sin(vessel.Heading) * (vessel.Length / 2 + 2));
            Route(canvas, bow, new P2(berth.X + 72, 50), new P2(berth.X, 7.3), _pal["AmberDeepColor"], 1.8f);
        }

        if (layer != "traffic")
        {
            return;
        }

        var baltic = _world.Vessels.FirstOrDefault(v => v.Id == "baltic");
        if (baltic is not null)
        {
            Route(canvas, new P2(baltic.X + 40, baltic.Y), new P2(baltic.X + 160, 90), new P2(baltic.X + 360, 300), _pal["SeaGreenColor"], 1.8f);
        }

        var levant = _world.Vessels.FirstOrDefault(v => v.Id == "levant");
        var berth06 = _world.Berths.FirstOrDefault(b => b.Number == "06");
        if (levant is not null && berth06 is not null)
        {
            Route(canvas, new P2(levant.X + 40, levant.Y - 6), new P2(berth06.X - 150, 140), new P2(berth06.X, 20), _pal.WithAlpha("HarbourDepthInkColor", .45), 1.4f);
        }
    }

    private void Route(SKCanvas canvas, P2 from, P2 control, P2 to, SKColor color, float width)
    {
        _path.Reset();
        for (var i = 0; i <= 28; i++)
        {
            var t = i / 28.0;
            var u = 1 - t;
            var q = _camera.Project(u * u * from.X + 2 * u * t * control.X + t * t * to.X,
                u * u * from.Y + 2 * u * t * control.Y + t * t * to.Y, .05);
            if (i == 0)
            {
                _path.MoveTo(q.X, q.Y);
            }
            else
            {
                _path.LineTo(q.X, q.Y);
            }
        }

        _stroke.Color = color;
        _stroke.StrokeWidth = width;
        _stroke.PathEffect = _dashRoute;
        canvas.DrawPath(_path, _stroke);
        _stroke.PathEffect = null;
    }

    /// <summary>Kelvin wake: two arms at 19.5° either side of the track, plus turbulent water astern.</summary>
    private void Wake(SKCanvas canvas, VesselShape v, double length)
    {
        var c = Math.Cos(v.Heading);
        var s = Math.Sin(v.Heading);
        var stern = new P2(v.X - c * v.Length / 2, v.Y - s * v.Length / 2);
        var white = _pal["HarbourWakeColor"];

        void Arm(double angle, float width, double alpha, P2 from)
        {
            var dx = -Math.Cos(angle) * c + Math.Sin(angle) * s;
            var dy = -Math.Cos(angle) * s - Math.Sin(angle) * c;
            var p0 = _camera.Project(from.X, from.Y, .05);
            var p1 = _camera.Project(from.X + dx * length, from.Y + dy * length, .05);
            using var shader = SKShader.CreateLinearGradient(new SKPoint(p0.X, p0.Y), new SKPoint(p1.X, p1.Y),
                new[] { white.WithAlpha((byte)(255 * alpha * .7)), white.WithAlpha(0) }, null, SKShaderTileMode.Clamp);
            _stroke.Shader = shader;
            _stroke.StrokeWidth = width;
            canvas.DrawLine(p0.X, p0.Y, p1.X, p1.Y, _stroke);
            _stroke.Shader = null;
        }

        P2 Side(double sign) => new(stern.X - s * sign * v.Beam * .45, stern.Y + c * sign * v.Beam * .45);
        var arm = 19.5 * Math.PI / 180;
        Arm(arm, 1.2f, .9, Side(1));
        Arm(-arm, 1.2f, .9, Side(-1));
        Arm(0, (float)Math.Max(2, v.Beam * _camera.Scale * .35), .7, stern);
    }

    private void YardLayer(SKCanvas canvas)
    {
        foreach (var (key, x, y, w, h) in _world.YardBlocks)
        {
            TraceGround(Poly.Rect(x, y, w + 4, h + 3), HarbourWorld.QuayZ + .01);
            _fill.Color = _pal.WithAlpha(key, .3);
            canvas.DrawPath(_path, _fill);
            _stroke.Color = _pal.WithAlpha(key, .9);
            _stroke.StrokeWidth = 1.2f;
            canvas.DrawPath(_path, _stroke);
        }

        GroundText(canvas, "BLOCK A", -470, -38, HarbourWorld.QuayZ + .02, _pal["CargoStandardColor"], 4);
        GroundText(canvas, "BLOCK B", -470, -58, HarbourWorld.QuayZ + .02, _pal["CargoStandardColor"], 4);
        GroundText(canvas, "REEFER", -470, -78, HarbourWorld.QuayZ + .02, _pal["CargoReeferColor"], 4);
        GroundText(canvas, "HAZMAT", 470, -78, HarbourWorld.QuayZ + .02, _pal["CargoHazardColor"], 4);
        GroundText(canvas, "INSPECTION", 470, -98, HarbourWorld.QuayZ + .02, _pal["CargoOversizeColor"], 4);
    }

    private void SecurityLayer(SKCanvas canvas)
    {
        // Restricted perimeter around the terminal and the hold at the inspection lane.
        TraceGround(new[] { new P2(-490, -1), new P2(490, -1), new P2(490, -136), new P2(-490, -136) }, HarbourWorld.QuayZ + .02);
        _fill.Color = _pal.WithAlpha("TealColor", .06);
        canvas.DrawPath(_path, _fill);
        _stroke.Color = _pal.WithAlpha("TealColor", .8);
        _stroke.StrokeWidth = 1.4f;
        _stroke.PathEffect = _dashTile;
        canvas.DrawPath(_path, _stroke);
        _stroke.PathEffect = null;

        TraceGround(Poly.Rect(0, -150, 40, 9), HarbourWorld.QuayZ + .03);
        _fill.Color = _pal.WithAlpha("AmberColor", .22);
        canvas.DrawPath(_path, _fill);
        GroundText(canvas, "TRUCK GATE", 0, -140, HarbourWorld.QuayZ + .03, _pal["AmberDeepColor"], 4);

        var hold = _camera.Project(375, -98, HarbourWorld.QuayZ + 3.5);
        _fill.Color = _pal.WithAlpha("AmberColor", .28);
        canvas.DrawCircle(hold.X, hold.Y, (float)Math.Max(6, 9 * _camera.Scale), _fill);
        _fill.Color = _pal["AmberDeepColor"];
        canvas.DrawCircle(hold.X, hold.Y, (float)Math.Max(2.5, 3 * _camera.Scale), _fill);
        GroundText(canvas, "HOLD · CMAU 918204 4", 375, -108, HarbourWorld.QuayZ + .03, _pal["AmberDeepColor"], 3.2f);
    }

    // ── Objects ────────────────────────────────────────────────────────────────

    private void SortAndDraw(SKCanvas canvas, List<SceneObject> list)
    {
        foreach (var o in list)
        {
            o.Key = o.Overhead ? NearestDepth(o) : _camera.Depth(o.Anchor);
        }

        list.Sort((a, b) => a.Key.CompareTo(b.Key));
        foreach (var o in list)
        {
            DrawObject(canvas, o);
        }
    }

    private double NearestDepth(SceneObject o)
    {
        var nearest = double.MinValue;
        foreach (var stack in o.Stacks)
        {
            foreach (var face in stack.Faces)
            {
                foreach (var p in face.Points)
                {
                    var d = _camera.Depth(p);
                    if (d > nearest)
                    {
                        nearest = d;
                    }
                }
            }
        }

        return nearest;
    }

    private void DrawObject(SKCanvas canvas, SceneObject o)
    {
        if (o.Tree is { } tree)
        {
            var c = _camera.Project(tree.X, tree.Y, tree.Z);
            var r = (float)Math.Max(1, tree.R * _camera.Scale);
            _fill.Color = _pal.Shade(_pal["HarbourTreeColor"], .82);
            canvas.DrawCircle(c.X, c.Y, r, _fill);
            _fill.Color = _pal["HarbourTreeLightColor"];
            canvas.DrawCircle(c.X - r * .2f, c.Y - r * .25f, r * .7f, _fill);
            return;
        }

        foreach (var face in o.Base)
        {
            DrawFace(canvas, face);
        }

        if (o.Stacks.Count > 1)
        {
            foreach (var stack in o.Stacks)
            {
                stack.Key = _camera.Depth(stack.Center);
            }

            o.Stacks.Sort((a, b) => a.Layer != b.Layer ? a.Layer.CompareTo(b.Layer) : a.Key.CompareTo(b.Key));
        }

        foreach (var stack in o.Stacks)
        {
            foreach (var face in stack.Faces)
            {
                DrawFace(canvas, face);
            }
        }
    }

    private void DrawFace(SKCanvas canvas, Face face)
    {
        if (!_camera.Faces(face.Normal, face.Points[0]))
        {
            return;
        }

        TracePolygon(face.Points);
        _fill.Color = face.Fill;
        canvas.DrawPath(_path, _fill);

        if (face.Top && (!face.Thin || _camera.Scale > 3.2))
        {
            _stroke.Color = face.Edge;
            _stroke.StrokeWidth = .7f;
            canvas.DrawPath(_path, _stroke);
        }
        else if (!face.Thin)
        {
            // Seals the anti-alias seam between adjacent faces.
            _stroke.Color = face.Fill;
            _stroke.StrokeWidth = .8f;
            canvas.DrawPath(_path, _stroke);
        }
    }

    // ── Live layer ─────────────────────────────────────────────────────────────

    private void DrawLive(SKCanvas canvas, double seconds, HarbourFrameState state)
    {
        var live = new List<SceneObject>(_world.Cranes.Count + _world.Trucks.Count + _world.Uppers.Count);
        var steel = _pal["HarbourSteelColor"];
        var house = _pal["HarbourHouseColor"];
        const double qz = HarbourWorld.QuayZ;

        foreach (var crane in _world.Cranes)
        {
            var s = crane.Working ? (state.Animate ? (seconds / 11 + crane.Phase) % 1 : .3 + crane.Phase * .2) : .02;
            var lane = (HarbourWorld.RailWater + HarbourWorld.RailLand) / 2;
            var ty = lane + (crane.ShipY - lane) * (.5 - .5 * Math.Cos(2 * Math.PI * s));
            var lower = crane.Working ? Math.Pow(Math.Max(0, Math.Cos(4 * Math.PI * s)), 2) : 0;
            var top = qz + 10.5;
            var end = s < .25 || s > .75 ? qz + 1.4 : HarbourWorld.DeckZ + 4.2;
            var zb = top - (top - end) * lower;
            var carrying = crane.Working && (crane.Discharging ? s >= .5 : s < .5);

            var faces = new List<Face>();
            if (carrying)
            {
                faces.AddRange(_world.Extrude(Poly.Rect(crane.X, ty, HarbourWorld.BoxL, HarbourWorld.BoxW), zb - HarbourWorld.BoxH, zb, crane.Box));
            }

            faces.AddRange(_world.Extrude(Poly.Rect(crane.X, ty, HarbourWorld.BoxL + .1, HarbourWorld.BoxW + .1), zb, zb + .22, steel));
            faces.AddRange(_world.Extrude(Poly.Rect(crane.X, ty, .1, .1), zb + .22, qz + 12.2, steel));
            faces.AddRange(_world.Extrude(Poly.Rect(crane.X, ty, 1.6, 1.8), qz + 12.2, qz + 12.85, house));
            var o = new SceneObject { Anchor = new V3(crane.X, ty, zb), Overhead = true };
            o.Stacks.Add(new Stack { Center = new V3(crane.X, ty, zb), Faces = faces });
            live.Add(o);
        }

        foreach (var truck in _world.Trucks)
        {
            var y = HarbourWorld.TruckLanes[truck.Lane];
            var dir = HarbourWorld.TruckDirections[truck.Lane];
            var t = state.Animate ? seconds : 0;
            var x = ((truck.X0 + dir * truck.Speed * t + 480) % 960 + 960) % 960 - 480;
            var faces = new List<Face>();
            faces.AddRange(_world.Extrude(Poly.Rect(x, y, 3.3, .66), qz + .25, qz + .45, _pal["HarbourCapColor"]));
            if (truck.Load is { } load)
            {
                faces.AddRange(_world.Extrude(Poly.Rect(x, y, HarbourWorld.BoxL, HarbourWorld.BoxW), qz + .45, qz + .45 + HarbourWorld.BoxH, load));
            }

            faces.AddRange(_world.Extrude(Poly.Rect(x + dir * 2.2, y, 1.1, .72), qz, qz + .95, truck.Cab));
            var o = new SceneObject { Anchor = new V3(x, y, qz) };
            o.Stacks.Add(new Stack { Center = new V3(x, y, qz), Faces = faces });
            live.Add(o);
        }

        live.AddRange(_world.Uppers);
        SortAndDraw(canvas, live);

        if (state.HoveredBerth is { } hovered && hovered >= 0 && hovered < _world.Berths.Count)
        {
            var berth = _world.Berths[hovered];
            BerthTile(canvas, berth, _pal.WithAlpha("InkColor", .05), _pal.WithAlpha("InkColor", .55), dashed: true);
        }

        if (state.HoveredVessel is { } hover && hover != state.SelectedVessel)
        {
            Outline(canvas, hover, _pal.WithAlpha("InkColor", .45), 1);
        }

        if (state.SelectedVessel is { } selected)
        {
            Outline(canvas, selected, _pal["InkColor"], 1.6f);
        }
    }

    private void Outline(SKCanvas canvas, string vesselId, SKColor color, float width)
    {
        var hull = VesselOutline(vesselId);
        if (hull.Length < 3)
        {
            return;
        }

        _path.Reset();
        _path.MoveTo(hull[0].X, hull[0].Y);
        for (var i = 1; i < hull.Length; i++)
        {
            _path.LineTo(hull[i].X, hull[i].Y);
        }

        _path.Close();
        _stroke.Color = color;
        _stroke.StrokeWidth = width;
        canvas.DrawPath(_path, _stroke);
    }

    /// <summary>Screen-space silhouette of a vessel: hull at the waterline and deck, plus the mast.</summary>
    public (float X, float Y)[] VesselOutline(string vesselId)
    {
        var vessel = _world.Vessels.FirstOrDefault(v => v.Id == vesselId);
        if (vessel is null)
        {
            return Array.Empty<(float, float)>();
        }

        var points = new List<P2>();
        foreach (var p in vessel.Ring)
        {
            var a = _camera.Project(p.X, p.Y, 0);
            var b = _camera.Project(p.X, p.Y, HarbourWorld.DeckZ + 3);
            points.Add(new P2(a.X, a.Y));
            points.Add(new P2(b.X, b.Y));
        }

        var m = _camera.Project(vessel.Mast);
        points.Add(new P2(m.X, m.Y));
        return Poly.ConvexHull(points).Select(p => ((float)p.X, (float)p.Y)).ToArray();
    }

    // ── Screen effects ─────────────────────────────────────────────────────────

    /// <summary>
    /// Tilt-shift: the optical signature of a model shot with a macro lens. A blurred copy of the
    /// still scene is masked to the top and bottom of the frame and laid over the sharp one.
    /// </summary>
    private void TiltShift(SKCanvas canvas, float width, float height)
    {
        if (_still is null || _maskShader is null)
        {
            return;
        }

        var sigma = (int)Math.Round((2 + 1.2 * Math.Cos(_camera.Pitch)) * 4);
        if (!_blurFilters.TryGetValue(sigma, out var blur))
        {
            blur = SKImageFilter.CreateBlur(sigma / 4f, sigma / 4f);
            _blurFilters[sigma] = blur;
        }

        var bounds = new SKRect(0, 0, width, height);
        canvas.SaveLayer(bounds, null);
        _layerBlur.ImageFilter = blur;
        canvas.SaveLayer(bounds, _layerBlur);
        canvas.DrawPicture(_still);
        canvas.Restore();
        _mask.Shader = _maskShader;
        canvas.DrawRect(bounds, _mask);
        canvas.Restore();
    }

    private void DrawAtmosphere(SKCanvas canvas, float width, float height)
    {
        if (_hazeShader is not null)
        {
            _fill.Shader = _hazeShader;
            canvas.DrawRect(0, 0, width, height * .62f, _fill);
        }

        var rects = new[]
        {
            new SKRect(0, 0, width, (float)_camera.TopInset + (height - (float)_camera.TopInset) * .14f),
            new SKRect(0, height * .92f, width, height),
            new SKRect(0, 0, width * .05f, height),
            new SKRect(width * .95f, 0, width, height)
        };
        for (var i = 0; i < 4; i++)
        {
            if (_fadeShaders[i] is { } shader)
            {
                _fill.Shader = shader;
                canvas.DrawRect(rects[i], _fill);
            }
        }

        _fill.Shader = null;
    }

    private void DrawLeaders(SKCanvas canvas, HarbourFrameState state)
    {
        _stroke.StrokeWidth = 1;
        foreach (var (id, (anchor, tag)) in Leaders)
        {
            var needs = PortData.Vessel(id).Status == "Arriving";
            var color = needs ? _pal["AmberDeepColor"] : _pal.WithAlpha("InkColor", .5);
            _stroke.Color = color;
            canvas.DrawLine(anchor, tag, _stroke);
            _fill.Color = color;
            canvas.DrawCircle(anchor, 2, _fill);
        }
    }

    // ── Paths ──────────────────────────────────────────────────────────────────

    private void TracePolygon(IReadOnlyList<V3> points)
    {
        _path.Reset();
        for (var i = 0; i < points.Count; i++)
        {
            var q = _camera.Project(points[i]);
            if (i == 0)
            {
                _path.MoveTo(q.X, q.Y);
            }
            else
            {
                _path.LineTo(q.X, q.Y);
            }
        }

        _path.Close();
    }

    private void TraceGround(IReadOnlyList<P2> poly, double z)
    {
        _path.Reset();
        for (var i = 0; i < poly.Count; i++)
        {
            var q = _camera.Project(poly[i].X, poly[i].Y, z);
            if (i == 0)
            {
                _path.MoveTo(q.X, q.Y);
            }
            else
            {
                _path.LineTo(q.X, q.Y);
            }
        }

        _path.Close();
    }

    private void TraceLine(IReadOnlyList<P2> points, double z)
    {
        _path.Reset();
        for (var i = 0; i < points.Count; i++)
        {
            var q = _camera.Project(points[i].X, points[i].Y, z);
            if (i == 0)
            {
                _path.MoveTo(q.X, q.Y);
            }
            else
            {
                _path.LineTo(q.X, q.Y);
            }
        }
    }

    private void Shadows(SKCanvas canvas, List<P2[]> polygons, double z, double blurWorld, double alpha)
    {
        if (polygons.Count == 0)
        {
            return;
        }

        _path.Reset();
        foreach (var poly in polygons)
        {
            for (var i = 0; i < poly.Length; i++)
            {
                var q = _camera.Project(poly[i].X, poly[i].Y, z);
                if (i == 0)
                {
                    _path.MoveTo(q.X, q.Y);
                }
                else
                {
                    _path.LineTo(q.X, q.Y);
                }
            }

            _path.Close();
        }

        var sigmaKey = (int)Math.Round(Math.Max(.5, blurWorld * _camera.Scale / 2) * 4);
        if (!_maskFilters.TryGetValue(sigmaKey, out var filter))
        {
            filter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, sigmaKey / 4f);
            _maskFilters[sigmaKey] = filter;
        }

        _fill.MaskFilter = filter;
        _fill.Color = _pal.WithAlpha("HarbourShadowColor", alpha);
        canvas.DrawPath(_path, _fill);
        _fill.MaskFilter = null;
    }

    /// <summary>Text lying in a horizontal plane; the plane's projection is near-affine at text scale.</summary>
    private void GroundText(SKCanvas canvas, string text, double x, double y, double z, SKColor color, float size)
    {
        var o = _camera.Project(x, y, z);
        var ex = _camera.Project(x + 1, y, z);
        var ey = _camera.Project(x, y + 1, z);
        var matrix = new SKMatrix(ex.X - o.X, ey.X - o.X, o.X, ex.Y - o.Y, ey.Y - o.Y, o.Y, 0, 0, 1);

        canvas.Save();
        canvas.Concat(in matrix);
        _paintFont.Size = size;
        _text.Color = color;
        canvas.DrawText(text, 0, size * .36f, SKTextAlign.Center, _paintFont, _text);
        canvas.Restore();
    }

    public void Dispose()
    {
        _still?.Dispose();
        _baked?.Dispose();
        _maskShader?.Dispose();
        _hazeShader?.Dispose();
        foreach (var shader in _fadeShaders)
        {
            shader?.Dispose();
        }

        foreach (var filter in _maskFilters.Values)
        {
            filter.Dispose();
        }

        foreach (var filter in _blurFilters.Values)
        {
            filter.Dispose();
        }

        _fill.Dispose();
        _stroke.Dispose();
        _text.Dispose();
        _layerBlur.Dispose();
        _mask.Dispose();
        _path.Dispose();
        _chartFont.Dispose();
        _paintFont.Dispose();
        _dashRoute.Dispose();
        _dashFairway.Dispose();
        _dashTile.Dispose();
    }
}
