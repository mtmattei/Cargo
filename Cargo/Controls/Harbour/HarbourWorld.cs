using SkiaSharp;

namespace Cargo.Controls.Harbour;

public sealed class Face
{
    public required V3[] Points { get; init; }
    public required V3 Normal { get; init; }
    public SKColor Fill { get; set; }
    public SKColor Edge { get; set; }
    public bool Top { get; init; }

    /// <summary>Small faces (containers, cars) skip the anti-alias seam stroke.</summary>
    public bool Thin { get; init; }
}

public sealed class Stack
{
    public required V3 Center { get; init; }
    public required List<Face> Faces { get; init; }
    public int Layer { get; init; }
    public double Key { get; set; }
}

public sealed class SceneObject
{
    public required V3 Anchor { get; set; }

    /// <summary>Sorted by its nearest point rather than its anchor: booms and gantry beams overhang everything below.</summary>
    public bool Overhead { get; init; }

    public List<Face> Base { get; } = new();
    public List<Stack> Stacks { get; } = new();
    public (double X, double Y, double Z, double R)? Tree { get; init; }
    public double Key { get; set; }
}

public sealed class VesselShape
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required SceneObject Object { get; init; }
    public required P2[] Ring { get; init; }
    public required V3 Mast { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Heading { get; init; }
    public required double Length { get; init; }
    public required double Beam { get; init; }
}

public sealed record CraneRig(double X, string? VesselId, double Phase, bool Working, bool Discharging, double ShipY, SKColor Box);

public sealed record BerthSpot(int Index, string Number, double X, string State, double Depth);

public sealed record TruckRun(int Lane, double X0, double Speed, SKColor? Load, SKColor Cab);

/// <summary>
/// The whole harbour at true scale, built once from PortData: 1 unit = 3.9 m, 40 ft boxes
/// 3.13 × 0.63 × 0.66, STS cranes on a 30 m rail gauge with the boom at ~52 m, a 5.5 m quay.
/// </summary>
public sealed class HarbourWorld
{
    public const double MetresPerUnit = 3.9;
    public const double X0 = -900, X1 = 900, Y0 = -260, SeaY1 = 620, QuayZ = 1.4;
    public const double BoxL = 3.13, BoxW = 0.63, BoxH = 0.66, Bay = 3.3, Row = 0.7;
    public const double DeckZ = 3.6;
    public const double RailWater = -2, RailLand = -9.7;

    private static readonly double ShadowX = -HarbourPalette.Light.X / HarbourPalette.Light.Z;
    private static readonly double ShadowY = -HarbourPalette.Light.Y / HarbourPalette.Light.Z;

    private readonly HarbourPalette _pal;

    public List<SceneObject> Objects { get; } = new();
    public List<SceneObject> Uppers { get; } = new();
    public List<CraneRig> Cranes { get; } = new();
    public List<VesselShape> Vessels { get; } = new();
    public List<VesselShape> Tugs { get; } = new();
    public List<BerthSpot> Berths { get; } = new();
    public List<TruckRun> Trucks { get; } = new();
    public List<(P2[] Poly, SKColor Color)> Decals { get; } = new();
    public List<P2[]> SeaShadows { get; } = new();
    public List<P2[]> LandShadows { get; } = new();
    public List<P2[]> SeaContact { get; } = new();
    public List<P2[]> LandContact { get; } = new();
    public List<(int Level, P2[] Points)> Isobaths { get; } = new();
    public List<(double X, double Y, double Depth)> Soundings { get; } = new();
    public List<(string Key, double X, double Y, double W, double H)> YardBlocks { get; } = new();

    public Face QuaySide { get; }
    public Face Land { get; }

    public static readonly double[] TruckLanes = { (RailWater + RailLand) / 2 + 0.6, -14, -19 };
    public static readonly int[] TruckDirections = { 1, -1, 1 };

    public HarbourWorld(HarbourPalette palette)
    {
        _pal = palette;

        QuaySide = MakeFace(new[] { new V3(X0, 0, 0), new V3(X1, 0, 0), new V3(X1, 0, QuayZ), new V3(X0, 0, QuayZ) },
            new V3(0, 1, 0), _pal["HarbourQuaySideColor"]);
        Land = MakeFace(new[] { new V3(X0, Y0, QuayZ), new V3(X1, Y0, QuayZ), new V3(X1, 0, QuayZ), new V3(X0, 0, QuayZ) },
            new V3(0, 0, 1), _pal["HarbourLandColor"]);

        BuildBerthsAndVessels();
        BuildYard();
        BuildLandside();
        BuildGroundMarkings();
        BuildChart();
        BuildTrucks();
    }

    // ── Faces ──────────────────────────────────────────────────────────────────

    private Face MakeFace(V3[] points, V3 normal, SKColor color, bool thin = false)
    {
        var fill = _pal.Shade(color, HarbourPalette.LightOf(normal));
        return new Face
        {
            Points = points,
            Normal = normal,
            Fill = fill,
            Edge = HarbourPalette.EdgeLight(color),
            Top = normal.Z > 0.99,
            Thin = thin
        };
    }

    /// <summary>A vertical prism: the polygon swept from <paramref name="z0"/> to <paramref name="z1"/>.</summary>
    public List<Face> Extrude(IReadOnlyList<P2> poly, double z0, double z1, SKColor color,
        bool noTop = false, bool noSides = false, SKColor? top = null, bool thin = false)
    {
        var p = Poly.Area(poly) < 0 ? poly.Reverse().ToArray() : poly.ToArray();
        var faces = new List<Face>();

        if (!noSides)
        {
            for (var i = 0; i < p.Length; i++)
            {
                var a = p[i];
                var b = p[(i + 1) % p.Length];
                var n = new V3(b.Y - a.Y, -(b.X - a.X), 0).Normalized();
                faces.Add(MakeFace(new[] { new V3(a.X, a.Y, z0), new V3(b.X, b.Y, z0), new V3(b.X, b.Y, z1), new V3(a.X, a.Y, z1) },
                    n, color, thin));
            }
        }

        if (!noTop)
        {
            faces.Add(MakeFace(p.Select(q => new V3(q.X, q.Y, z1)).ToArray(), new V3(0, 0, 1), top ?? color, thin));
        }

        return faces;
    }

    /// <summary>A square-section member between two points: crane stays and leg bracing.</summary>
    private List<Face> Beam(V3 p0, V3 p1, double width, SKColor color)
    {
        var d = (p1 - p0).Normalized();
        var a = V3.Cross(d, new V3(0, 0, 1));
        a = a.Length < 1e-3 ? new V3(1, 0, 0) : a.Normalized();
        var b = V3.Cross(a, d).Normalized();
        var h = width / 2;
        V3 Off(V3 p, double sa, double sb) => p + a * (h * sa) + b * (h * sb);

        var faces = new List<Face>();
        foreach (var (sa, sb, n) in new[] { (1d, 0d, a), (-1d, 0d, a * -1), (0d, 1d, b), (0d, -1d, b * -1) })
        {
            var ta = sa != 0 ? (sa, sa) : (-1d, 1d);
            var tb = sb != 0 ? (sb, sb) : (-1d, 1d);
            faces.Add(MakeFace(new[] { Off(p0, ta.Item1, tb.Item1), Off(p0, ta.Item2, tb.Item2), Off(p1, ta.Item2, tb.Item2), Off(p1, ta.Item1, tb.Item1) },
                n, color));
        }

        return faces;
    }

    private SceneObject Add(V3 anchor, bool overhead = false, List<SceneObject>? list = null)
    {
        var o = new SceneObject { Anchor = anchor, Overhead = overhead };
        (list ?? Objects).Add(o);
        return o;
    }

    private static void AddStack(SceneObject o, V3 center, List<Face> faces, int layer = 0) =>
        o.Stacks.Add(new Stack { Center = center, Faces = faces, Layer = layer });

    // ── Shadows ────────────────────────────────────────────────────────────────

    private void Cast(IReadOnlyList<P2> poly, double zb, double zt, bool sea)
    {
        var pz = sea ? 0 : QuayZ;
        var a = poly.Select(p => new P2(p.X + ShadowX * (zb - pz), p.Y + ShadowY * (zb - pz)));
        var b = poly.Select(p => new P2(p.X + ShadowX * (zt - pz), p.Y + ShadowY * (zt - pz)));
        var hull = Poly.ClipToQuayLine(Poly.ConvexHull(a.Concat(b)), keepLand: !sea);
        if (hull.Length > 2)
        {
            (sea ? SeaShadows : LandShadows).Add(hull);
        }
    }

    private void Contact(IReadOnlyList<P2> poly, bool sea, double grow = 0.5)
    {
        var cx = poly.Average(p => p.X);
        var cy = poly.Average(p => p.Y);
        var grown = poly.Select(p =>
        {
            var dx = p.X - cx;
            var dy = p.Y - cy;
            var l = Math.Max(1e-6, Math.Sqrt(dx * dx + dy * dy));
            return new P2(p.X + dx / l * grow, p.Y + dy / l * grow);
        }).ToArray();
        (sea ? SeaContact : LandContact).Add(grown);
    }

    // ── Container stacks ───────────────────────────────────────────────────────

    /// <summary>
    /// A grid of stacks. Side faces hidden by an equal-or-taller neighbour are never built, so a
    /// ship of several hundred stacks costs roughly its tops plus its visible terraces.
    /// </summary>
    private void StackGrid(SceneObject o, Func<double, double, P2> toWorld, double angle,
        double[] us, double[] vs, double z0, int[,] heights, SKColor[,][] colours)
    {
        var ni = us.Length;
        var nj = vs.Length;
        for (var i = 0; i < ni; i++)
        {
            for (var j = 0; j < nj; j++)
            {
                var n = heights[i, j];
                if (n == 0)
                {
                    continue;
                }

                var c = toWorld(us[i], vs[j]);
                var poly = Poly.Rect(c.X, c.Y, BoxL, BoxW, angle);
                var faces = new List<Face>();
                var neighbours = new[]
                {
                    j > 0 ? heights[i, j - 1] : 0,
                    i < ni - 1 ? heights[i + 1, j] : 0,
                    j < nj - 1 ? heights[i, j + 1] : 0,
                    i > 0 ? heights[i - 1, j] : 0
                };

                for (var s = 0; s < 4; s++)
                {
                    var a = poly[s];
                    var b = poly[(s + 1) % 4];
                    var normal = new V3(b.Y - a.Y, -(b.X - a.X), 0).Normalized();
                    for (var k = neighbours[s]; k < n; k++)
                    {
                        var za = z0 + k * BoxH;
                        var zt = za + BoxH * 0.95;
                        faces.Add(MakeFace(new[] { new V3(a.X, a.Y, za), new V3(b.X, b.Y, za), new V3(b.X, b.Y, zt), new V3(a.X, a.Y, zt) },
                            normal, colours[i, j][k], thin: true));
                    }
                }

                var topZ = z0 + (n - 1) * BoxH + BoxH * 0.95;
                faces.Add(MakeFace(poly.Select(p => new V3(p.X, p.Y, topZ)).ToArray(), new V3(0, 0, 1), colours[i, j][n - 1], thin: true));
                AddStack(o, new V3(c.X, c.Y, z0), faces);
            }
        }
    }

    /// <summary>Boxes of one line tend to travel together, so a tier often repeats the one below.</summary>
    private SKColor[] ColumnColours(SeededRandom random, int n)
    {
        var colours = new SKColor[n];
        for (var k = 0; k < n; k++)
        {
            colours[k] = k > 0 && random.Next() < 0.45 ? colours[k - 1] : _pal.PickLivery(random);
        }

        return colours;
    }

    // ── Vessels ────────────────────────────────────────────────────────────────

    private void BuildBerthsAndVessels()
    {
        for (var i = 0; i < PortData.Berths.Count; i++)
        {
            var b = PortData.Berths[i];
            Berths.Add(new BerthSpot(i, b.Number, -350 + i * 100, b.State, b.Depth));
        }

        var seed = 3;
        foreach (var vessel in PortData.Vessels)
        {
            var length = vessel.Length / MetresPerUnit;
            var beam = vessel.Length / 7.1 / MetresPerUnit;
            var berth = Berths.FirstOrDefault(b => PortData.Berths[b.Index].Occupant == vessel.Id);

            double x, y, heading;
            if (berth is not null && berth.State == "occupied")
            {
                x = berth.X;
                y = 1.2 + beam / 2;
                heading = berth.Index % 2 == 0 ? 0 : Math.PI;
            }
            else if (berth is not null)
            {
                // Inbound: out in the fairway, bow on the berth it is heading for.
                x = berth.X + 88;
                y = 150;
                heading = Math.Atan2(7 - y, berth.X - x);
            }
            else
            {
                x = -300;
                y = 262;
                heading = -8 * Math.PI / 180;
            }

            var aboard = vessel.UnloadPercent is > 0 and < 100 ? (100 - vessel.UnloadPercent) / 100.0
                : vessel.LoadPercent > 0 ? vessel.LoadPercent / 100.0
                : 0.9;

            Vessels.Add(BuildShip(vessel, x, y, heading, length, beam, aboard, seed += 4));
        }

        // Quay cranes on the working ships: MSC Aurora discharging on four, Kaida Maru loading on three.
        foreach (var shape in Vessels)
        {
            var v = PortData.Vessel(shape.Id);
            var working = v.Status == "Docked";
            if (!working)
            {
                continue;
            }

            var discharging = v.UnloadPercent is > 0 and < 100;
            var count = discharging ? 4 : 3;
            for (var k = 0; k < count; k++)
            {
                BuildCrane(shape.X + (k - (count - 1) / 2.0) * 18, shape.Id, k * 0.27, true, discharging, shape.Y);
            }
        }

        foreach (var x in new[] { 108d, 430, -230 })
        {
            BuildCrane(x, null, 0, false, false, 6);
        }

        // Tugs on the inbound vessel, one ahead and one on the quarter.
        var inbound = Vessels.FirstOrDefault(s => PortData.Vessel(s.Id).Status == "Arriving");
        if (inbound is not null)
        {
            var c = Math.Cos(inbound.Heading);
            var s = Math.Sin(inbound.Heading);
            Tugs.Add(BuildTug(inbound.X + c * 50, inbound.Y + s * 50, inbound.Heading));
            Tugs.Add(BuildTug(inbound.X - s * 10 - c * 14, inbound.Y + c * 10 - s * 14, inbound.Heading));
        }

        foreach (var bx in new[] { -620d, -300, 20, 560 })
        {
            BuildBuoy(bx, 96, _pal["HarbourBuoyPortColor"]);
            BuildBuoy(bx, 185, _pal["HarbourBuoyStarboardColor"]);
        }
    }

    private VesselShape BuildShip(Vessel vessel, double x, double y, double heading, double length, double beam, double aboard, int seed)
    {
        var (hullColor, funnelColor) = _pal.Livery(vessel.Operator);
        var deck = _pal["HarbourDeckColor"];
        var house = _pal["HarbourHouseColor"];
        var glass = _pal["HarbourGlassColor"];
        var steel = _pal["HarbourSteelColor"];
        var random = new SeededRandom(seed);
        var c = Math.Cos(heading);
        var s = Math.Sin(heading);
        P2 ToWorld(double u, double v) => new(x + u * c - v * s, y + u * s + v * c);

        var lh = length / 2;
        var bh = beam / 2;
        const double bowLength = 18;

        // Transom stern, parallel body, parabolic bow.
        var outline = new List<P2> { new(-lh, -bh * .86), new(-lh + 1.2, -bh), new(lh - bowLength, -bh) };
        for (var t = 1; t <= 7; t++)
        {
            var a = t / 7.0;
            outline.Add(new P2(lh - bowLength + bowLength * a, -bh * (1 - a * a)));
        }

        for (var t = 6; t >= 1; t--)
        {
            var a = t / 7.0;
            outline.Add(new P2(lh - bowLength + bowLength * a, bh * (1 - a * a)));
        }

        outline.Add(new P2(lh - bowLength, bh));
        outline.Add(new P2(-lh + 1.2, bh));
        outline.Add(new P2(-lh, bh * .86));
        var ring = outline.Select(p => ToWorld(p.X, p.Y)).ToArray();

        var o = Add(new V3(x, y, 3));
        o.Base.AddRange(Extrude(ring, 0, 0.45, _pal["HarbourBootColor"], noTop: true));
        o.Base.AddRange(Extrude(ring, 0.45, DeckZ, hullColor, top: deck));

        // Forecastle: a raised bow deck.
        var fu = lh - 10;
        var fa = (fu - (lh - bowLength)) / bowLength;
        var fv = bh * (1 - fa * fa);
        var forecastle = new List<P2> { new(fu, -fv) };
        for (var t = 1; t <= 7; t++)
        {
            var a = t / 7.0;
            var u = lh - bowLength + bowLength * a;
            if (u > fu)
            {
                forecastle.Add(new P2(u, -bh * (1 - a * a)));
            }
        }

        for (var t = 6; t >= 1; t--)
        {
            var a = t / 7.0;
            var u = lh - bowLength + bowLength * a;
            if (u > fu)
            {
                forecastle.Add(new P2(u, bh * (1 - a * a)));
            }
        }

        forecastle.Add(new P2(fu, fv));
        var fc = ToWorld(fu + 4, 0);
        AddStack(o, new V3(fc.X, fc.Y, DeckZ), Extrude(forecastle.Select(p => ToWorld(p.X, p.Y)).ToArray(), DeckZ, DeckZ + 0.9, hullColor, top: deck));

        // Big ships carry the bridge forward, smaller ones aft.
        var bridgeForward = vessel.Length >= 330;
        var uBridge = bridgeForward ? lh - 0.33 * length : -lh + 4.5;
        var aftEnd = bridgeForward ? -lh + 10.5 : -lh + 7;

        var us = new List<double>();
        for (var u = aftEnd + Bay / 2; u + Bay / 2 < fu - 0.6; u += Bay)
        {
            if (Math.Abs(u - uBridge) > 3.4)
            {
                us.Add(u);
            }
        }

        var nj = (int)Math.Floor((beam - 0.9) / Row);
        var vs = Enumerable.Range(0, nj).Select(j => (j - (nj - 1) / 2.0) * Row).ToArray();
        var tiers = length > 88 ? 7 : 6;
        var heights = new int[us.Count, nj];
        var colours = new SKColor[us.Count, nj][];
        var total = 0;
        for (var i = 0; i < us.Count; i++)
        {
            var emptyBay = aboard < 0.5 && random.Next() > aboard * 1.5;
            var bayTiers = (int)Math.Round(tiers * (0.5 + 0.5 * aboard) + (random.Next() - 0.5) * 2);
            for (var j = 0; j < nj; j++)
            {
                var edge = Math.Min(j, nj - 1 - j);
                var n = emptyBay ? 0 : bayTiers - (edge == 0 ? 2 : edge == 1 ? 1 : 0) + (random.Next() < 0.12 ? -1 : 0);
                n = Math.Clamp(n, 0, tiers);
                heights[i, j] = n;
                colours[i, j] = ColumnColours(random, n);
                total += n;
            }
        }

        StackGrid(o, ToWorld, heading, us.ToArray(), vs, DeckZ, heights, colours);
        var averageTiers = total / (double)Math.Max(1, us.Count * nj);

        // Bridge house with a glazing band, full-beam wings and a mast.
        var bc = ToWorld(uBridge, 0);
        var bridge = new List<Face>();
        bridge.AddRange(Extrude(Poly.Rect(bc.X, bc.Y, 3, beam * .78, heading), DeckZ, 9.4, house));
        bridge.AddRange(Extrude(Poly.Rect(bc.X, bc.Y, 3.05, beam * .79, heading), 9.4, 10, glass));
        var wing = ToWorld(uBridge + .4, 0);
        bridge.AddRange(Extrude(Poly.Rect(wing.X, wing.Y, 2, beam + .3, heading), 10, 10.5, house));
        var mast = ToWorld(uBridge - .3, 0);
        bridge.AddRange(Extrude(Poly.Rect(mast.X, mast.Y, .25, .25, heading), 10.5, 13, steel));
        AddStack(o, new V3(bc.X, bc.Y, DeckZ), bridge);

        // Engine casing and funnel aft.
        var funnelAt = ToWorld(bridgeForward ? -lh + 6 : -lh + 1.6, 0);
        var funnel = new List<Face>();
        if (bridgeForward)
        {
            funnel.AddRange(Extrude(Poly.Rect(funnelAt.X, funnelAt.Y, 4, beam * .45, heading), DeckZ, 8.2, house));
        }

        funnel.AddRange(Extrude(Poly.Rect(funnelAt.X, funnelAt.Y, 1.6, 2.4, heading), bridgeForward ? 8.2 : DeckZ, 9.8, funnelColor));
        funnel.AddRange(Extrude(Poly.Rect(funnelAt.X, funnelAt.Y, 1.6, 2.4, heading), 9.8, 10.3, _pal["HarbourCapColor"]));
        AddStack(o, new V3(funnelAt.X, funnelAt.Y, DeckZ), funnel);

        Cast(ring, 0, DeckZ, sea: true);
        if (us.Count > 0)
        {
            var mid = ToWorld((us[0] + us[^1]) / 2, 0);
            Cast(Poly.Rect(mid.X, mid.Y, us[^1] - us[0] + BoxL, beam - 1, heading), 0, DeckZ + averageTiers * BoxH, sea: true);
        }

        Cast(Poly.Rect(bc.X, bc.Y, 3, beam * .78, heading), 0, 10.5, sea: true);
        Contact(ring, sea: true);

        return new VesselShape
        {
            Id = vessel.Id,
            Name = vessel.Name,
            Object = o,
            Ring = ring,
            Mast = new V3(bc.X, bc.Y, 13),
            X = x,
            Y = y,
            Heading = heading,
            Length = length,
            Beam = beam
        };
    }

    private VesselShape BuildTug(double x, double y, double heading)
    {
        var c = Math.Cos(heading);
        var s = Math.Sin(heading);
        P2 ToWorld(double u, double v) => new(x + u * c - v * s, y + u * s + v * c);
        var ring = new[] { new P2(-4, -1.3), new P2(2.2, -1.5), new P2(4, 0), new P2(2.2, 1.5), new P2(-4, 1.3) }
            .Select(p => ToWorld(p.X, p.Y)).ToArray();
        var house = _pal["HarbourHouseColor"];

        var o = Add(new V3(x, y, 1));
        o.Base.AddRange(Extrude(ring, 0, 1.3, _pal["HarbourTugColor"], top: _pal["HarbourDeckColor"]));
        var faces = new List<Face>();
        var h = ToWorld(-.5, 0);
        faces.AddRange(Extrude(Poly.Rect(h.X, h.Y, 2.6, 1.9, heading), 1.3, 2.6, house));
        var w = ToWorld(-.2, 0);
        faces.AddRange(Extrude(Poly.Rect(w.X, w.Y, 1.4, 1.7, heading), 2.6, 3.1, _pal["HarbourGlassColor"]));
        faces.AddRange(Extrude(Poly.Rect(w.X, w.Y, 1.4, 1.7, heading), 3.1, 3.4, house));
        var m = ToWorld(-.4, 0);
        faces.AddRange(Extrude(Poly.Rect(m.X, m.Y, .15, .15, heading), 3.4, 4.6, _pal["HarbourSteelColor"]));
        AddStack(o, new V3(x, y, 1.3), faces);
        Cast(ring, 0, 1.3, sea: true);
        Contact(ring, sea: true, grow: .3);

        return new VesselShape { Id = "tug", Name = "Tug", Object = o, Ring = ring, Mast = new V3(x, y, 4.6), X = x, Y = y, Heading = heading, Length = 8, Beam = 3 };
    }

    private void BuildBuoy(double x, double y, SKColor color)
    {
        var pts = Enumerable.Range(0, 8).Select(i => new P2(x + Math.Cos(i * Math.PI / 4) * .6, y + Math.Sin(i * Math.PI / 4) * .6)).ToArray();
        var o = Add(new V3(x, y, 1));
        o.Base.AddRange(Extrude(pts, 0, 1.3, color));
        AddStack(o, new V3(x, y, 1.3), Extrude(Poly.Rect(x, y, .25, .25), 1.3, 2.3, _pal["HarbourSteelColor"]));
        Cast(pts, 0, 1.5, sea: true);
    }

    // ── Quay cranes ────────────────────────────────────────────────────────────

    private void BuildCrane(double x, string? vesselId, double phase, bool working, bool discharging, double shipY)
    {
        var crane = _pal["HarbourCraneColor"];
        var mid = (RailWater + RailLand) / 2;

        var low = Add(new V3(x, mid, QuayZ));
        foreach (var (dx, y) in new[] { (-2.3, RailWater), (2.3, RailWater), (-2.3, RailLand), (2.3, RailLand) })
        {
            var p = Poly.Rect(x + dx, y, .7, .7);
            AddStack(low, new V3(x + dx, y, QuayZ), Extrude(p, QuayZ, QuayZ + 12, crane));
            Cast(p, QuayZ, QuayZ + 12, sea: false);
        }

        foreach (var y in new[] { RailWater, RailLand })
        {
            AddStack(low, new V3(x, y, QuayZ), Extrude(Poly.Rect(x, y, 6.2, .8), QuayZ, QuayZ + .8, _pal["HarbourCraneDarkColor"]));
        }

        foreach (var dx in new[] { -2.3, 2.3 })
        {
            AddStack(low, new V3(x + dx, mid, QuayZ + 6), Beam(new V3(x + dx, RailWater, QuayZ + 2.2), new V3(x + dx, RailLand, QuayZ + 9.5), .3, crane));
        }

        // The overhead structure is drawn live, above the trolley that runs beneath it.
        var up = Add(new V3(x, 0, QuayZ + 14), overhead: true, list: Uppers);
        foreach (var dx in new[] { -2.3, 2.3 })
        {
            AddStack(up, new V3(x + dx, mid, QuayZ + 12), Extrude(Poly.Rect(x + dx, mid, .8, 8.6), QuayZ + 12, QuayZ + 12.9, crane), 0);
        }

        var boom = Poly.Rect(x, 1.25, 1.3, 34.5);
        AddStack(up, new V3(x, 1.25, QuayZ + 12.9), Extrude(boom, QuayZ + 12.9, QuayZ + 13.9, crane), 1);
        AddStack(up, new V3(x, -13.5, QuayZ + 13.9), Extrude(Poly.Rect(x, -13.5, 3.2, 4), QuayZ + 13.9, QuayZ + 15.9, _pal["HarbourHouseColor"]), 2);
        foreach (var dx in new[] { -.8, .8 })
        {
            AddStack(up, new V3(x + dx, -6.5, QuayZ + 13.9), Extrude(Poly.Rect(x + dx, -6.5, .4, .4), QuayZ + 13.9, QuayZ + 20.5, crane), 2);
        }

        foreach (var dx in new[] { -.6, .6 })
        {
            AddStack(up, new V3(x + dx, 6, QuayZ + 17), Beam(new V3(x + dx, -6.5, QuayZ + 20.3), new V3(x + dx * .6, 18.2, QuayZ + 13.9), .22, crane), 3);
            AddStack(up, new V3(x + dx, -11, QuayZ + 18), Beam(new V3(x + dx, -6.5, QuayZ + 20.3), new V3(x + dx, -15.3, QuayZ + 15.9), .28, crane), 3);
        }

        Cast(boom, QuayZ + 12.9, QuayZ + 13.9, sea: false);
        Cast(boom, QuayZ + 12.9, QuayZ + 13.9, sea: true);
        Cast(Poly.Rect(x, -13.5, 3.2, 4), QuayZ + 13.9, QuayZ + 15.9, sea: false);

        var box = _pal.PickLivery(new SeededRandom((int)Math.Abs(x) + 11));
        Cranes.Add(new CraneRig(x, vesselId, phase, working, discharging, shipY, box));
    }

    // ── Yard ───────────────────────────────────────────────────────────────────

    private void BuildYard()
    {
        var random = new SeededRandom(42);
        var rtg = _pal["HarbourRtgColor"];
        double[] rows = { -38, -58, -78, -98 };
        double[] columns = { -375, -225, -75, 75, 225, 375 };

        for (var r = 0; r < rows.Length; r++)
        {
            for (var cIndex = 0; cIndex < columns.Length; cIndex++)
            {
                var yc = rows[r];
                var xc = columns[cIndex];
                const int bays = 20;
                var us = Enumerable.Range(0, bays).Select(i => (i - (bays - 1) / 2.0) * Bay).ToArray();
                var vs = Enumerable.Range(0, 6).Select(j => (j - 2.5) * Row).ToArray();
                var fill = .35 + random.Next() * .55;
                var heights = new int[bays, 6];
                var colours = new SKColor[bays, 6][];
                for (var i = 0; i < bays; i++)
                {
                    var gap = random.Next() < .08;
                    for (var j = 0; j < 6; j++)
                    {
                        var n = gap ? 0 : Math.Clamp((int)Math.Round(fill * 5 + (random.Next() - .5) * 2.2), 0, 5);
                        heights[i, j] = n;
                        colours[i, j] = ColumnColours(random, n);
                    }
                }

                var o = Add(new V3(xc, yc, QuayZ));
                StackGrid(o, (u, v) => new P2(xc + u, yc + v), 0, us, vs, QuayZ, heights, colours);
                for (var i = 0; i < bays; i++)
                {
                    var tallest = Enumerable.Range(0, 6).Max(j => heights[i, j]);
                    if (tallest > 0)
                    {
                        var p = Poly.Rect(xc + us[i], yc, BoxL, 6 * Row);
                        Cast(p, QuayZ, QuayZ + tallest * BoxH, sea: false);
                        Contact(p, sea: false, grow: .25);
                    }
                }

                // Zone per block, for the Yard layer: two standard rows, then reefer / hazmat, inspection at the end.
                var key = r switch
                {
                    0 => "CargoStandardColor",
                    1 => "CargoStandardColor",
                    2 => cIndex < 3 ? "CargoReeferColor" : "CargoHazardColor",
                    _ => cIndex >= 4 ? "CargoOversizeColor" : "CargoStandardColor"
                };
                YardBlocks.Add((key, xc, yc, 70, 6));
            }
        }

        // Rubber-tyred gantries straddling a block.
        foreach (var (xc, yc, bay) in new[] { (-375d, -38d, 12), (-75, -58, 5), (225, -38, 15), (75, -78, 9), (375, -98, 6) })
        {
            var x = xc + (bay - 9.5) * Bay;
            foreach (var dx in new[] { -1.6, 1.6 })
            {
                foreach (var y in new[] { yc - 3.2, yc + 3.2 })
                {
                    var p = Poly.Rect(x + dx, y, .45, .45);
                    var leg = Add(new V3(x + dx, y, QuayZ));
                    AddStack(leg, new V3(x + dx, y, QuayZ), Extrude(p, QuayZ, QuayZ + 5.6, rtg));
                    Cast(p, QuayZ, QuayZ + 5.6, sea: false);
                }
            }

            var up = Add(new V3(x, yc, QuayZ + 6), overhead: true);
            foreach (var dx in new[] { -1.6, 1.6 })
            {
                var p = Poly.Rect(x + dx, yc, .5, 7);
                AddStack(up, new V3(x + dx, yc, QuayZ + 5.6), Extrude(p, QuayZ + 5.6, QuayZ + 6.1, rtg), 0);
                Cast(p, QuayZ + 5.6, QuayZ + 6.1, sea: false);
            }

            foreach (var y in new[] { yc - 3.2, yc + 3.2 })
            {
                AddStack(up, new V3(x, y, QuayZ + 6.1), Extrude(Poly.Rect(x, y, 3.8, .5), QuayZ + 6.1, QuayZ + 6.5, rtg), 1);
            }

            AddStack(up, new V3(x + 1.6, yc - 1.4, QuayZ + 4.4), Extrude(Poly.Rect(x + 1.6, yc - 1.4, .8, 1), QuayZ + 4.4, QuayZ + 5.4, _pal["HarbourHouseColor"]), 2);
        }
    }

    // ── Landside ───────────────────────────────────────────────────────────────

    private void BuildLandside()
    {
        foreach (var x in new[] { -450d, -300, -150, 0, 150, 300, 450 })
        {
            Mast(x, -48);
            Mast(x + 75, -88);
        }

        Building(-440, -150, 34, 14, 4);
        Shed(420, -150, 64, 22, 4.5, _pal["HarbourShedColor"], _pal["HarbourShedRoofColor"]);
        Shed(250, -152, 36, 16, 3.5, _pal["HarbourBuildingColor"], _pal["HarbourRoofColor"]);
        Building(-300, -165, 18, 10, 2);

        // Gate canopy over the lanes.
        var gate = Add(new V3(0, -150, QuayZ + 2), overhead: true);
        foreach (var x in new[] { -16d, -5, 5, 16 })
        {
            AddStack(gate, new V3(x, -150, QuayZ), Extrude(Poly.Rect(x, -150, .4, .4), QuayZ, QuayZ + 2.6, _pal["HarbourSteelColor"]), 0);
        }

        AddStack(gate, new V3(0, -150, QuayZ + 2.6), Extrude(Poly.Rect(0, -150, 38, 7), QuayZ + 2.6, QuayZ + 3, _pal["HarbourBuildingColor"]), 1);
        Cast(Poly.Rect(0, -150, 38, 7), QuayZ + 2.6, QuayZ + 3, sea: false);

        // Street trees along the back road and the gate approach, ~8 m crowns.
        var random = new SeededRandom(55);
        for (var x = -560d; x <= 560; x += 9 + random.Next() * 5)
        {
            if (Math.Abs(x) < 36)
            {
                continue;
            }

            foreach (var y in new[] { -112d, -130 })
            {
                if (random.Next() < .2)
                {
                    continue;
                }

                var r = 1 + random.Next() * .45;
                var z = QuayZ + 1.2 + r;
                Objects.Add(new SceneObject { Anchor = new V3(x, y, z), Tree = (x, y, z, r) });
                Contact(Poly.Rect(x, y, r * 1.6, r * 1.6), sea: false, grow: .6);
            }
        }

        Parking(-470, -172, 4, 40);
        Parking(300, -176, 3, 36);
    }

    private void Mast(double x, double y)
    {
        var o = Add(new V3(x, y, QuayZ + 4), overhead: true);
        AddStack(o, new V3(x, y, QuayZ), Extrude(Poly.Rect(x, y, .25, .25), QuayZ, QuayZ + 9, _pal["HarbourSteelColor"]), 0);
        AddStack(o, new V3(x, y, QuayZ + 9), Extrude(Poly.Rect(x, y, 1.4, .45), QuayZ + 9, QuayZ + 9.35, _pal["HarbourRoofColor"]), 1);
        Cast(Poly.Rect(x, y, .25, .25), QuayZ, QuayZ + 9, sea: false);
    }

    private void Building(double cx, double cy, double lx, double ly, int floors)
    {
        var wall = _pal["HarbourBuildingColor"];
        var glass = _pal["HarbourGlassColor"];
        var p = Poly.Rect(cx, cy, lx, ly);
        var o = Add(new V3(cx, cy, QuayZ));
        var faces = new List<Face>();
        var z = QuayZ;
        for (var i = 0; i < floors; i++)
        {
            faces.AddRange(Extrude(p, z, z + .7, wall, noTop: true));
            faces.AddRange(Extrude(Poly.Rect(cx, cy, lx + .06, ly + .06), z + .7, z + 1.15, glass, noTop: true));
            z += 1.15;
        }

        faces.AddRange(Extrude(p, z, z + .5, wall, top: _pal["HarbourRoofColor"]));
        faces.AddRange(Extrude(Poly.Rect(cx - lx * .25, cy, lx * .2, ly * .4), z + .5, z + 1.2, _pal["HarbourVentColor"]));
        AddStack(o, new V3(cx, cy, QuayZ), faces);
        Cast(p, QuayZ, z + .5, sea: false);
        Contact(p, sea: false, grow: .4);
    }

    private void Shed(double cx, double cy, double lx, double ly, double h, SKColor wall, SKColor roof)
    {
        var p = Poly.Rect(cx, cy, lx, ly);
        var o = Add(new V3(cx, cy, QuayZ));
        AddStack(o, new V3(cx, cy, QuayZ), Extrude(p, QuayZ, QuayZ + h, wall, top: roof));
        Cast(p, QuayZ, QuayZ + h, sea: false);
        Contact(p, sea: false, grow: .4);
    }

    private void Parking(double x0, double y0, int rows, int cols)
    {
        var random = new SeededRandom(77 + rows);
        var o = Add(new V3(x0 + cols * .65, y0 - rows * 1.6, QuayZ));
        Decals.Add((Poly.Rect(x0 + cols * .65, y0 - rows * 1.6 + .8, cols * 1.3 + 2, rows * 3.2 + 1), _pal["HarbourRoadColor"]));
        for (var j = 0; j < rows; j++)
        {
            for (var i = 0; i < cols; i++)
            {
                if (random.Next() > .68)
                {
                    continue;
                }

                var cx = x0 + i * 1.3 + .65;
                var cy = y0 - j * 3.2;
                AddStack(o, new V3(cx, cy, QuayZ), Extrude(Poly.Rect(cx, cy, .48, 1.15), QuayZ, QuayZ + .38, _pal.PickLivery(random), thin: true));
            }
        }
    }

    // ── Ground ─────────────────────────────────────────────────────────────────

    private void BuildGroundMarkings()
    {
        var road = _pal["HarbourRoadColor"];
        var mark = _pal["HarbourMarkColor"];
        var rail = _pal["HarbourRailColor"];

        // Decals are drawn in order, so the apron and yard pad go first.
        Decals.Insert(0, (new[] { new P2(X0, -26), new P2(X1, -26), new P2(X1, 0), new P2(X0, 0) }, _pal["HarbourApronColor"]));
        Decals.Insert(1, (new[] { new P2(-480, -108), new P2(480, -108), new P2(480, -30), new P2(-480, -30) }, _pal["HarbourPadColor"]));
        Decals.Add((new[] { new P2(X0, -126), new P2(X1, -126), new P2(X1, -116), new P2(X0, -116) }, road));
        Decals.Add((Poly.Rect(0, -150, 60, 34), road));
        foreach (var y in new[] { RailWater, RailLand })
        {
            Decals.Add((new[] { new P2(X0, y - .12), new P2(X1, y - .12), new P2(X1, y + .12), new P2(X0, y + .12) }, rail));
        }

        for (var x = X0 + 6; x < X1 - 6; x += 9)
        {
            Decals.Add((Poly.Rect(x, -14.5, 4.5, .18), mark));
            Decals.Add((Poly.Rect(x, -121, 4.5, .18), mark));
        }

        for (var i = 0; i <= 8; i++)
        {
            Decals.Add((Poly.Rect(-400 + i * 100, -.9, .35, 1.2), mark));
        }

        Decals.Add((new[] { new P2(X0, -.45), new P2(X1, -.45), new P2(X1, -.25), new P2(X0, -.25) }, _pal["HarbourSafetyColor"]));
    }

    public static double DepthAt(double x, double y)
    {
        var d = 10 + 7 * Math.Exp(-Math.Pow((y - 140) / 70, 2));
        if (y < 22)
        {
            d = Math.Max(d, 16);
        }

        d -= Math.Max(0, (y - 330) / 32);
        d -= Math.Max(0, (Math.Abs(x) - 560) / 40);
        d += Math.Sin(x / 90 + y / 70) * .5;
        return Math.Max(2, d);
    }

    private void BuildChart()
    {
        foreach (var level in new[] { 15, 12, 10, 5 })
        {
            var line = new List<P2>();
            for (var x = X0; x <= X1; x += 12)
            {
                var previous = DepthAt(x, 200);
                double? found = null;
                for (var y = 204d; y < SeaY1; y += 4)
                {
                    var d = DepthAt(x, y);
                    if ((previous - level) * (d - level) <= 0)
                    {
                        found = y;
                        break;
                    }

                    previous = d;
                }

                if (found is { } fy)
                {
                    line.Add(new P2(x, fy));
                }
                else if (line.Count > 1)
                {
                    Isobaths.Add((level, line.ToArray()));
                    line.Clear();
                }
                else
                {
                    line.Clear();
                }
            }

            if (line.Count > 1)
            {
                Isobaths.Add((level, line.ToArray()));
            }
        }

        var random = new SeededRandom(21);
        var keepClear = Vessels.Select(v => (v.X, v.Y, R: v.Length * .8)).ToArray();
        for (var gx = X0 + 70; gx < X1 - 40; gx += 95)
        {
            for (var gy = 60d; gy < SeaY1 - 30; gy += 70)
            {
                var x = gx + (random.Next() - .5) * 40;
                var y = gy + (random.Next() - .5) * 30;
                if (keepClear.Any(k => Math.Sqrt((x - k.X) * (x - k.X) + (y - k.Y) * (y - k.Y)) < k.R))
                {
                    continue;
                }

                Soundings.Add((x, y, DepthAt(x, y)));
            }
        }
    }

    private void BuildTrucks()
    {
        var random = new SeededRandom(8);
        var cabs = new[] { _pal["HarbourRtgColor"], _pal["HarbourHouseColor"] };
        for (var i = 0; i < 12; i++)
        {
            SKColor? load = random.Next() < .6 ? _pal.PickLivery(random) : null;
            Trucks.Add(new TruckRun(i % 3, -480 + random.Next() * 960, 5 + random.Next() * 4, load, cabs[random.Next() < .5 ? 0 : 1]));
        }
    }
}
