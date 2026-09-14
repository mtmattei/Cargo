using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Cargo.Controls;

/// <summary>
/// The harbour itself — the shared stage that sits under Overview, Vessels and Security.
/// Drawn in a fixed 1328 × 436 space and scaled to the available width, so berths,
/// hulls and markers keep their relationship at any window size.
/// </summary>
public sealed partial class PortStage : Panel
{
    private const double SceneWidth = 1328;
    private const double SceneHeight = 436;
    private const double CompactShift = 0.22;
    private const double CompactHeight = 150;

    /// <summary>Left edge of each berth marker, matching the quay furniture.</summary>
    private static readonly double[] ChipX = { 45, 249, 403, 573, 742, 908, 1075, 1213 };


    private readonly Canvas _scene = new() { Width = SceneWidth, Height = SceneHeight };
    private readonly Microsoft.UI.Xaml.Shapes.Rectangle[] _berthHighlights =
        new Microsoft.UI.Xaml.Shapes.Rectangle[8];
    private readonly Canvas _berthLabel = new() { IsHitTestVisible = false };
    private readonly ScaleTransform _scale = new();
    private readonly TranslateTransform _shift = new();

    private PortState? _state;

    public PortStage()
    {
        var group = new TransformGroup();
        group.Children.Add(_scale);
        group.Children.Add(_shift);
        _scene.RenderTransform = group;
        Children.Add(_scene);
    }

    public PortState? State
    {
        get => _state;
        set
        {
            if (_state is not null)
            {
                _state.StructureChanged -= OnStructureChanged;
                _state.HoverChanged -= OnHoverChanged;
            }

            _state = value;

            if (_state is not null)
            {
                _state.StructureChanged += OnStructureChanged;
                _state.HoverChanged += OnHoverChanged;
            }

            Rebuild();
        }
    }

    private void OnStructureChanged(object? sender, EventArgs e) => Rebuild();

    private void OnHoverChanged(object? sender, EventArgs e) => RefreshBerthHover();

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) || availableSize.Width <= 0
            ? SceneWidth
            : availableSize.Width;

        _scene.Measure(new Size(SceneWidth, SceneHeight));

        // Folded away, the harbour becomes a letterboxed strip across the quay.
        return new Size(width, _state?.StageFull ?? true ? width * SceneHeight / SceneWidth : CompactHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var scale = finalSize.Width / SceneWidth;
        _scale.ScaleX = scale;
        _scale.ScaleY = scale;

        var full = _state?.StageFull ?? true;
        _shift.Y = full ? 0 : -SceneHeight * scale * CompactShift;

        _scene.Arrange(new Rect(0, 0, SceneWidth, SceneHeight));
        Clip = new RectangleGeometry { Rect = new Rect(0, 0, finalSize.Width, finalSize.Height) };
        return finalSize;
    }

    // ── Scene ─────────────────────────────────────────────────────────────────

    private void Rebuild()
    {
        _scene.Children.Clear();
        Array.Clear(_berthHighlights);

        // Collapsed on the sections that do not show the harbour; nothing to draw.
        if (_state is null || !_state.ShowStage)
        {
            return;
        }

        DrawWater();
        DrawTerminal();
        DrawQuay();
        DrawBerths();
        DrawVessels();
        DrawBerthMarkers();
        DrawLayers();
        InvalidateMeasure();
    }

    /// <summary>The basin: flat colour, the design's water tile laid over it, then a depth shade.</summary>
    private void DrawWater()
    {
        const double top = 130;
        const double height = SceneHeight - top;

        _scene.Place(Draw.Rect(0, top, SceneWidth, height, Tokens.Brush("WaterBrush")));

        // WinUI dropped ImageBrush tiling, so the 280 x 80 tile is laid out by hand. Tiles are
        // placed at their natural size and the panel's clip trims the last row and column,
        // which keeps every tile square with its neighbours instead of squashing the edges.
        var tile = Sprites.SizeOf("water-tile");
        for (var y = top; y < SceneHeight; y += tile.Height)
        {
            for (var x = 0d; x < SceneWidth; x += tile.Width)
            {
                _scene.Place(Sprites.Fill("water-tile", x, y, tile.Width, tile.Height, 0.9));
            }
        }

        _scene.Place(Draw.Rect(0, top, SceneWidth, height, new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops =
            {
                new GradientStop { Offset = 0, Color = Windows.UI.Color.FromArgb(0, 0, 0, 0) },
                new GradientStop { Offset = 1, Color = Windows.UI.Color.FromArgb(64, 0, 0, 0) }
            }
        }));
    }

    /// <summary>The terminal behind the quay: one rendered strip, plus the four crane bases.</summary>
    private void DrawTerminal()
    {
        _scene.Place(Sprites.Fill("terminal-strip", 0, 0, SceneWidth, 128));
    }

    /// <summary>The quay edge, then the crane bases that stand on it.</summary>
    private void DrawQuay()
    {
        _scene.Place(Draw.Rect(0, 126, SceneWidth, 8, Tokens.Brush("QuayBrush")));
        _scene.Place(Draw.Rect(0, 133, SceneWidth, 2, Tokens.Brush("QuayEdgeBrush")));

        _scene.Place(Sprites.Fill("crane-leg-0", 104, 128, 48, 50));
        _scene.Place(Sprites.Fill("crane-leg-1", 520, 128, 46, 50));
        _scene.Place(Sprites.Fill("crane-leg-2", 622, 128, 46, 50));
        _scene.Place(Sprites.Fill("crane-leg-3", 856, 128, 46, 50));
    }

    private static readonly Dictionary<string, string> BerthTone = new()
    {
        ["occupied"] = "TealBrightColor",
        ["available"] = "SeaGreenColor",
        ["reserved"] = "AmberColor"
    };

    private void DrawBerths()
    {
        var state = _state!;

        for (var i = 0; i < PortData.HarbourBerths.Count; i++)
        {
            var (_, berthState, _) = PortData.HarbourBerths[i];
            var x = ChipX[i];

            var highlight = Draw.Rect(x - 12, 140, 150, 66, Tokens.Transparent, 8, Tokens.Transparent, 1.5);
            if (berthState is "reserved" or "available")
            {
                highlight.StrokeDashArray = Draw.Dash(4, 3.5);
            }

            highlight.IsHitTestVisible = false;
            _berthHighlights[i] = _scene.Place(highlight);

            var hit = Draw.Rect(x - 20, 136, 166, 80, Tokens.Transparent);
            var index = i;
            hit.PointerEntered += (_, _) => state.HoveredBerth = index;
            hit.PointerExited += (_, _) => state.HoveredBerth = null;
            _scene.Place(hit);
        }

        _berthLabel.Children.Clear();
        _scene.Place(_berthLabel);
        RefreshBerthHover();
    }

    /// <summary>
    /// Hover is a repaint, not a rebuild: eight brushes and one label, rather than tearing
    /// the whole harbour down and building it again on every pointer crossing.
    /// </summary>
    private void RefreshBerthHover()
    {
        if (_state is null || _berthHighlights[0] is null)
        {
            return;
        }

        var hoveredIndex = _state.HoveredBerth;

        for (var i = 0; i < _berthHighlights.Length; i++)
        {
            var highlight = _berthHighlights[i];
            if (highlight is null)
            {
                continue;
            }

            var (_, berthState, _) = PortData.HarbourBerths[i];
            var hovered = hoveredIndex == i;
            var tone = Tokens.Color(BerthTone[berthState]);
            var reserved = berthState == "reserved";

            highlight.Fill = Tokens.Of(tone, hovered ? 0.16 : 0);
            highlight.Stroke = reserved && !hovered
                ? Tokens.Brush("DeckWhiteColor", 0.7)
                : Tokens.Of(tone, hovered ? 0.95 : 0);
        }

        _berthLabel.Children.Clear();
        if (hoveredIndex is not { } index)
        {
            return;
        }

        var (_, state, label) = PortData.HarbourBerths[index];
        var width = label.Length * 6.6 + 16;
        _berthLabel.Place(Draw.Rect(ChipX[index] + 36, 138, width, 18,
            Tokens.Brush("InkDeepColor", 0.85), 5));
        _berthLabel.Place(Draw.Text(label, ChipX[index] + 44, 140, 11.5,
            Tokens.Brush(BerthTone[state].Replace("Color", "Brush")), "BodyMediumFont"));
    }

    /// <summary>
    /// The hulls, as the design's plan-view renders. Rects are the design's own, so a berth
    /// and the ship alongside it keep their exact relationship at any window size.
    /// </summary>
    private void DrawVessels()
    {
        var state = _state!;
        var selected = state.Section == "vessels" ? state.SelectedVesselId : state.HarbourSelection;

        AddPlanVessel("kaida", "ship-plan", 70, 156, 196, 52, selected);

        // MSC Aurora is working cargo: the discharged length of her deck is shaded back.
        AddPlanVessel("aurora", "msc-plan", 468, 152, 240, 60, selected);
        var discharged = 208 * PortData.Vessel("aurora").DeckGapPercent / 100;
        if (discharged > 0)
        {
            var gap = Draw.Rect(500, 154, discharged, 56, Tokens.Brush("InkDeepColor", 0.5));
            gap.IsHitTestVisible = false;
            _scene.Place(gap);
        }

        AddPlanVessel("baltic", "ship-plan", 830, 158, 172, 48, selected);

        // Nordic Star is inbound in the approach channel, still on her dashed route to Berth 07.
        var route = Draw.Shape("M1086 196 C 1088 230 1092 250 1100 262", null,
            Tokens.Brush("DeckWhiteColor", 0.75), 1.5);
        route.StrokeDashArray = Draw.Dash(6, 7);
        route.IsHitTestVisible = false;
        _scene.Place(route);

        // Her sprite is a wider crop that carries its own wake, so the hit ring is inset.
        AddPlanVessel("nordic", "nordic-approach", 1020, 182, 170, 170, selected,
            ringInset: new Thickness(26, 14, 26, 16), ringRadius: 40);

        AddPlanVessel("levant", "levant-anchor", 275, 345, 180, 60, selected,
            ringInset: new Thickness(15, 7, 15, 7), ringRadius: 23);
        _scene.Place(Draw.Text("AT ANCHOR", 292, 408, 10, Tokens.Brush("DeckWhiteColor", 0.55), "MonoMediumFont"));
    }

    private void AddPlanVessel(string id, string sprite, double x, double y,
        double width, double height, string? selected,
        Thickness? ringInset = null, double ringRadius = 0)
    {
        var vessel = PortData.Vessel(id);
        var inset = ringInset ?? new Thickness(-4);
        var host = new Grid { Width = width, Height = height };

        var radius = ringRadius > 0 ? ringRadius : (height + 8) / 2;
        host.Children.Add(new Microsoft.UI.Xaml.Shapes.Rectangle
        {
            RadiusX = radius,
            RadiusY = radius,
            Stroke = selected == id ? Tokens.Brush("DeckWhiteBrush") : Tokens.Transparent,
            StrokeThickness = 2.5,
            Margin = inset
        });

        host.Children.Add(new Image
        {
            Source = Sprites.Source(sprite),
            Stretch = Stretch.Fill,
            Width = width,
            Height = height
        });

        var button = new Button
        {
            Style = (Style)Application.Current.Resources["BareButton"],
            Content = host,
            Command = _state!.PickHullVesselCommand,
            CommandParameter = id,
            Width = width,
            Height = height
        };
        ToolTipService.SetToolTip(button, $"{vessel.Name} · {vessel.StatusLine}");

        _scene.Place(button.At(x, y));
    }

    private void DrawBerthMarkers()
    {
        for (var i = 0; i < PortData.HarbourBerths.Count; i++)
        {
            var (number, berthState, _) = PortData.HarbourBerths[i];
            var tone = Tokens.Color(BerthTone[berthState]);

            var marker = new Grid { Width = 30, Height = 24, IsHitTestVisible = false };
            marker.Children.Add(new Microsoft.UI.Xaml.Shapes.Rectangle
            {
                Fill = Tokens.Brush("InkDeepColor", 0.72),
                Stroke = Tokens.Of(tone),
                StrokeThickness = 1.2,
                RadiusX = 6,
                RadiusY = 6
            });
            marker.Children.Add(new TextBlock
            {
                Text = number,
                FontFamily = (FontFamily)Application.Current.Resources["MonoMediumFont"],
                FontSize = 11,
                Foreground = Tokens.Of(tone),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });

            _scene.Place(marker.At(ChipX[i], 134));
        }
    }

    // ── Overlay layers ────────────────────────────────────────────────────────

    private void DrawLayers()
    {
        var state = _state!;
        var layer = state.HarbourLayer;

        if (layer == "yard")
        {
            DrawYardLayer();
        }

        if (layer == "security" || state.Section == "security")
        {
            DrawSecurityLayer();
        }

        if (layer == "traffic")
        {
            DrawTrafficLayer();
        }
    }

    private void DrawYardLayer()
    {
        var veil = Draw.Rect(0, 130, SceneWidth, SceneHeight - 130, Tokens.Brush("InkDeepColor", 0.55));
        veil.IsHitTestVisible = false;
        _scene.Place(veil);

        (string Label, double X, string Token, double Width)[] zones =
        {
            ("Block A", 60, "CargoStandardColor", 64),
            ("Block B", 300, "CargoStandardColor", 64),
            ("Reefer", 540, "CargoReeferColor", 58),
            ("Hazmat", 800, "CargoHazardColor", 62),
            ("Inspection", 1050, "CargoOversizeColor", 78)
        };

        foreach (var (label, x, token, width) in zones)
        {
            var color = Tokens.Color(token);
            _scene.Place(Draw.Rect(x, 10, 136, 74, Tokens.Of(color, 0.28), 6, Tokens.Of(color, 0.9), 1.5));
            _scene.Place(Draw.Rect(x, 90, width, 20, Tokens.Brush("InkDeepColor", 0.78), 5));
            _scene.Place(Draw.Text(label, x + 8, 92, 11.5, Tokens.Brush("SurfaceBrush"), "BodyStrongFont"));
        }
    }

    private void DrawSecurityLayer()
    {
        var fence = Draw.Rect(0, 118, SceneWidth, 92, Tokens.Brush("TealColor", 0.14),
            0, Tokens.Brush("TealBrightColor", 0.7), 1.5);
        fence.StrokeDashArray = Draw.Dash(6, 5);
        fence.IsHitTestVisible = false;
        _scene.Place(fence);

        _scene.Place(Draw.Rect(0, 0, 220, 22, Tokens.Brush("AmberColor", 0.3), 0, Tokens.Brush("AmberBrush"), 1.5));
        var lane = Draw.Rect(1150, 36, 144, 80, Tokens.Brush("AmberColor", 0.22), 6, Tokens.Brush("AmberBrush"), 1.5);
        lane.StrokeDashArray = Draw.Dash(6, 5);
        _scene.Place(lane);

        foreach (var x in new[] { 232d, 719, 1117 })
        {
            _scene.Place(Draw.Shape($"M{x} 22 l-26 42 h52z", Tokens.Brush("TealBrightColor", 0.18)));
            _scene.Place(Draw.Dot(x, 22, 3, Tokens.Brush("TealBrightBrush")));
        }

        _scene.Place(Draw.Dot(620, 172, 18, Tokens.Brush("AmberColor", 0.25)));
        _scene.Place(Draw.Dot(620, 172, 6, Tokens.Brush("AmberBrush"), Tokens.Brush("SurfaceBrush"), 2));

        Label(8, 216, 112, "Vessel access", "SurfaceBrush");
        Label(8, 26, 92, "Truck gate", "SurfaceBrush");
        Label(1150, 120, 128, "Inspection lane", "SurfaceBrush");
        Label(636, 182, 200, "CMAU 918204 4 · Hold", "OnDarkWarnBrush");
    }

    private void DrawTrafficLayer()
    {
        var outbound = Draw.Shape("M1012 180 C 1106 210 1194 276 1328 332", null, Tokens.Brush("SeaGreenBrush"), 1.8);
        outbound.StrokeDashArray = Draw.Dash(6, 7);
        _scene.Place(outbound);

        var waiting = Draw.Shape("M442 380 C 575 368 708 290 841 234", null, Tokens.Brush("TextFaintBrush"), 1.5)
            ;
        waiting.StrokeDashArray = Draw.Dash(3, 6);
        waiting.Opacity = 0.7;
        _scene.Place(waiting);

        Label(1106, 240, 180, "Baltic Crown · departs 21:30", "OnDarkPositiveBrush");
        Label(929, 313, 132, "Nordic Star · 42 min", "OnDarkWarnBrush");
        Label(509, 336, 224, "Levant Express · holds for Berth 06", "TextOnDarkBrush");
    }

    private void Label(double x, double y, double width, string text, string toneBrush)
    {
        _scene.Place(Draw.Rect(x, y, width, 20, Tokens.Brush("InkDeepColor", 0.85), 5));
        _scene.Place(Draw.Text(text, x + 8, y + 2.5, 11.5, Tokens.Brush(toneBrush), "BodyStrongFont"));
    }
}
