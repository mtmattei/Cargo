namespace Cargo.Controls;

/// <summary>Terminal security zones, cameras, gates and personnel access points.</summary>
public sealed partial class SecurityZoneMap : SceneHost
{
    public SecurityZoneMap() : base(1100, 484)
    {
        Build();
    }

    private void Build()
    {
        Scene.Place(Draw.Rect(0, 0, 1100, 484, Tokens.Brush("LandBrush")));
        Scene.Place(Draw.Rect(0, 0, 1100, 90, Tokens.Brush("ShallowsBrush")));
        Scene.Place(Draw.Rect(0, 88, 1100, 6, Tokens.Brush("RoadBrush")));

        // Two ships alongside
        Scene.Place(Sprites.Fill(Sprites.Plan("aurora"), 160, 26, 220, 40));
        Scene.Place(Sprites.Fill(Sprites.Plan("kaida"), 640, 28, 190, 34));

        // Roads
        Scene.Place(Draw.Rect(0, 400, 1100, 26, Tokens.Brush("RoadBrush")));
        Scene.Place(Draw.Rule(0, 413, 1100, 413, Tokens.Brush("PaperWarmBrush"), 1.5, dash: Draw.Dash(9, 8)));
        foreach (var x in new[] { 60d, 540, 1020 })
        {
            Scene.Place(Draw.Rect(x, 94, 20, 310, Tokens.Brush("RoadBrush")));
        }

        foreach (var zone in PortData.SecurityZones)
        {
            var tint = Tokens.Color(zone.TintToken);
            var box = Draw.Rect(zone.X, zone.Y, zone.W, zone.H, Tokens.Of(tint, zone.FillOpacity), 10,
                Tokens.Of(tint, 0.6), 1.5);
            if (zone.Dashed)
            {
                box.StrokeDashArray = Draw.Dash(6, 4);
            }

            Scene.Place(box);
            Scene.Place(Draw.Text(zone.Label, zone.X + 12, zone.Y + 8, 15, Tokens.Brush("InkBrush"), "BodyStrongFont"));
            if (!string.IsNullOrEmpty(zone.Sub))
            {
                Scene.Place(Draw.Text(zone.Sub, zone.X + 12, zone.Y + 28, 13, Tokens.Brush("TextMutedBrush")));
            }
        }

        // Camera cones
        foreach (var camera in PortData.Cameras)
        {
            var group = new Canvas();
            group.Place(Draw.Shape("M0 0 L-38 -70 A80 80 0 0 1 38 -70 Z", Tokens.Brush("TealColor", 0.13)));
            group.Place(Draw.Dot(0, 0, 4, Tokens.Brush("TealBrush")));
            Scene.Place(group.At(camera.X, camera.Y));
        }

        foreach (var (label, x, y) in PortData.Gates)
        {
            var main = label == "Main";
            Scene.Place(Draw.Rect(x, y, 44, 18, Tokens.Brush("SurfaceBrush"), 4,
                Tokens.Brush(main ? "TealBrush" : "AmberDeepBrush"), 1.5));
            Scene.Place(Draw.Text(label, x, y + 22, 11.5, Tokens.Brush(main ? "TealBrush" : "AmberDeepBrush"),
                "BodyStrongFont", TextAlignment.Center, 44));
        }

        foreach (var point in PortData.AccessPoints)
        {
            Scene.Place(Draw.Dot(point.X, point.Y, 5, Tokens.Brush("SurfaceBrush"), Tokens.Brush("TealBrush"), 2));
        }

        // The active hold
        Scene.Place(Draw.Dot(700, 320, 22, Tokens.Brush("OrangeColor", 0.18)));
        Scene.Place(Draw.Dot(700, 320, 7, Tokens.Brush("OrangeBrush"), Tokens.Brush("SurfaceBrush"), 2));
    }
}

/// <summary>Today's security events, laned above and below the spine.</summary>
public sealed partial class SecurityTimeline : SceneHost
{
    public SecurityTimeline() : base(1000, 160)
    {
        Overflow = 20;
        Build();
    }

    private void Build()
    {
        Scene.Place(Draw.Rule(0, 60, 1000, 60, Tokens.Brush("InkColor", 0.12)));
        Scene.Place(Draw.Rule(0, 138, 1000, 138, Tokens.Brush("InkColor", 0.06)));
        Scene.Place(Draw.Rule(832, 0, 832, 136, Tokens.Brush("InkBrush"), 1, dash: Draw.Dash(3, 3)));

        var lanes = new Dictionary<bool, List<(double X, int Lane)>>
        {
            [true] = new(),
            [false] = new()
        };

        for (var i = 0; i < PortData.SecurityEvents.Count; i++)
        {
            var item = PortData.SecurityEvents[i];
            var x = (item.Hour - 6) / 18 * 1000;
            var up = i % 2 == 0;
            var y = up ? 44 : 76;
            var tone = Tokens.Brush(item.Tone);
            var radius = item.Tone is "OrangeColor" or "RestrictedColor" ? 6 : 4.5;

            Scene.Place(Draw.Rule(x, 60, x, y, tone, 1));
            Scene.Place(Draw.Dot(x, y, radius, tone, Tokens.Brush("SurfaceBrush"), 2));

            var lane = 0;
            while (lanes[up].Any(p => p.Lane == lane && Math.Abs(p.X - x) < 130))
            {
                lane++;
            }

            lanes[up].Add((x, lane));

            var labelY = up ? 24 - lane * 16 : 86 + lane * 16;
            var alignment = x < 60 ? TextAlignment.Left : x > 940 ? TextAlignment.Right : TextAlignment.Center;
            Scene.Place(Draw.Label(item.Label, x, labelY, 15, Tokens.Brush("InkBrush"),
                "BodyMediumFont", alignment, 180));
        }

        foreach (var (label, x, alignment) in new[]
                 {
                     ("06:00", 0d, TextAlignment.Left),
                     ("12:00", 333d, TextAlignment.Center),
                     ("18:00", 666d, TextAlignment.Center),
                     ("24:00", 1000d, TextAlignment.Right)
                 })
        {
            Scene.Place(Draw.Label(label, x, 144, 14, Tokens.Brush("TextFaintBrush"), "BodyFont", alignment, 120));
        }
    }
}

/// <summary>Which zones a badge opens.</summary>
public sealed partial class AccessZoneMap : SceneHost
{
    public static readonly DependencyProperty AllowedProperty = DependencyProperty.Register(
        nameof(Allowed), typeof(IReadOnlyList<int>), typeof(AccessZoneMap),
        new PropertyMetadata(null, (d, e) => ((AccessZoneMap)d).Show(e.NewValue as IReadOnlyList<int> ?? [])));

    public AccessZoneMap() : base(520, 220)
    {
    }

    /// <summary>Indexes into <see cref="PortData.AccessZones"/> that the selected badge opens.</summary>
    public IReadOnlyList<int>? Allowed
    {
        get => (IReadOnlyList<int>?)GetValue(AllowedProperty);
        set => SetValue(AllowedProperty, value);
    }

    private void Show(IReadOnlyList<int> allowed)
    {
        Scene.Children.Clear();
        Scene.Place(Draw.Rect(0, 0, 520, 220, Tokens.Brush("LandBrush"), 10));
        Scene.Place(Draw.Rect(0, 0, 520, 40, Tokens.Brush("ShallowsBrush")));

        for (var i = 0; i < PortData.AccessZones.Count; i++)
        {
            var zone = PortData.AccessZones[i];
            var ok = allowed.Contains(i);
            var tint = ok ? Tokens.Color(zone.TintToken) : Tokens.Color("TextMutedColor");

            var box = Draw.Rect(zone.X, zone.Y, zone.W, zone.H, Tokens.Of(tint, ok ? 0.28 : 0.04), 6,
                Tokens.Of(tint, ok ? 0.9 : 0.3), 1);
            if (!ok)
            {
                box.StrokeDashArray = Draw.Dash(4, 3);
            }

            Scene.Place(box);
            Scene.Place(Draw.Text(zone.Label, zone.X + 10, zone.Y + zone.H / 2 - 8, 10.5,
                Tokens.Brush(ok ? "InkBrush" : "TextFaintBrush"), ok ? "BodyStrongFont" : "BodyFont"));
        }
    }
}

/// <summary>The inspection sheet for the held container.</summary>
public sealed partial class InspectionDiagram : SceneHost
{
    public InspectionDiagram() : base(900, 520)
    {
        Build();
    }

    private void Build()
    {
        // Connectors from each checkpoint to the box
        var hair = Tokens.Brush("InkColor", 0.18);
        foreach (var (x, y1, y2) in new[]
                 {
                     (150d, 96d, 160d), (450d, 84d, 150d), (750d, 96d, 160d),
                     (150d, 424d, 360d), (450d, 436d, 370d), (750d, 424d, 360d)
                 })
        {
            Scene.Place(Draw.Rule(x, y1, x, y2, hair, 1.2, dash: Draw.Dash(4, 4)));
        }

        Scene.Place(Sprites.Fit("cont-red", 150, 125, 640, 300));

        // Markings sit on the long side face of the box, where a real one carries them
        Scene.Place(Draw.Rect(340, 266, 152, 22, Tokens.Brush("SurfaceBrush", 0.92), 4));
        Scene.Place(Draw.Text("CMAU 918204 4", 349, 269, 13, Tokens.Brush("InkBrush"), "MonoMediumFont"));
        Scene.Place(Draw.Text("MAX GROSS 32,500 KG · TARE 3,940 KG", 349, 293, 10.5,
            Tokens.Brush("DeckWhiteColor", 0.85)));

        // Seal callout
        Scene.Place(Draw.Dot(212, 318, 5, Tokens.Brush("CargoOversizeBrush"), Tokens.Brush("SurfaceBrush"), 2));
        Scene.Place(Draw.Rule(207, 318, 150, 340, Tokens.Brush("AmberDeepBrush"), 1.2));
        Scene.Place(Draw.Text("Seal CGM-77201-A", 0, 332, 10.5, Tokens.Brush("TextMutedBrush"),
            "MonoFont", TextAlignment.Right, 146));

        // The variance itself
        Scene.Place(Draw.Dot(600, 306, 30, Tokens.Brush("OrangeColor", 0.16)));
        Scene.Place(Draw.Dot(600, 306, 12, Tokens.Brush("OrangeBrush"), Tokens.Brush("SurfaceBrush"), 2.5));
        Scene.Place(Draw.Label("!", 600, 297, 13, Tokens.Brush("SurfaceBrush"), "BodyStrongFont", TextAlignment.Center, 40));

        foreach (var step in PortData.InspectionSteps)
        {
            var ok = step.Kind == "ok";
            var warn = step.Kind == "warn";
            var badge = ok ? "TealColor" : warn ? "OrangeColor" : "SurfaceSunkAltColor";
            var border = warn ? "OrangeColor" : ok ? "TealColor" : "InkColor";

            Scene.Place(Draw.Rect(step.X - 110, step.Y - 32, 220, 64, Tokens.Brush("SurfaceBrush"), 12,
                Tokens.Brush(border, warn ? 1 : ok ? 0.4 : 0.1), 1.5));
            Scene.Place(Draw.Dot(step.X - 82, step.Y, 14, Tokens.Brush(badge)));
            Scene.Place(Draw.Text(ok ? "✓" : warn ? "!" : "○", step.X - 102, step.Y - 9, 14,
                Tokens.Brush(step.Kind == "pending" ? "TextFaintBrush" : "SurfaceBrush"),
                "BodyStrongFont", TextAlignment.Center, 40));
            Scene.Place(Draw.Text(step.Label, step.X - 58, step.Y - 16, 13.5, Tokens.Brush("InkBrush"), "BodyStrongFont"));
            Scene.Place(Draw.Text(step.Detail, step.X - 58, step.Y + 3, 11.5,
                Tokens.Brush(warn ? "OrangeInkBrush" : "TextMutedBrush")));
        }
    }
}
