using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Cargo.Controls;

/// <summary>
/// The vessel profile with its interactive bay grid: the design's hull render, plus a
/// clickable bay column per stow position, the discharge boundary, and the length and
/// draft dimensions. Drawn in the design's 900 × 290 space.
/// </summary>
/// <remarks>
/// The bay grid is derived from where the deck actually sits inside the hull sprite
/// (<see cref="Sprites.Hull"/>), so the columns land on the containers the artwork shows
/// rather than on a rectangle guessed alongside it.
/// </remarks>
public sealed partial class VesselProfile : SceneHost
{
    private const double SceneW = 900;
    private const double SceneH = 290;
    private const double SeaTop = 226;

    /// <summary>The box the design fits the hull into.</summary>
    private static readonly Rect HullBox = new(20, 16, 864, 232);

    private readonly PortState _state;

    /// <summary>The lit bay. It moves under the pointer, so it is repositioned, never rebuilt.</summary>
    private readonly Microsoft.UI.Xaml.Shapes.Rectangle _bayHighlight = new()
    {
        RadiusX = 2,
        RadiusY = 2,
        StrokeThickness = 1.5,
        IsHitTestVisible = false
    };

    private double _bayWidth;
    private double _deckLeft;
    private double _deckTop;
    private int _selectedBay;

    public VesselProfile(PortState state) : base(SceneW, SceneH)
    {
        _state = state;
        Build();
        this.RebuildWhenVisible(state, Build);
        this.RepaintWhenVisible(state, RefreshBayHover);
    }

    private void Build()
    {
        Scene.Children.Clear();

        var vessel = _state.SelectedVessel;
        var hull = Sprites.HullOf(vessel.Id);
        var size = hull.Size;

        Scene.Place(Draw.Rect(0, SeaTop, SceneW, SceneH - SeaTop, new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops =
            {
                new GradientStop { Offset = 0, Color = Tokens.Color("SeaLightColor") },
                new GradientStop { Offset = 1, Color = Tokens.Color("SeaMidColor") }
            }
        }));

        // Fitted the way the design fits it: aspect kept, sitting on the waterline.
        var scale = Math.Min(HullBox.Width / size.Width, HullBox.Height / size.Height);
        var hullX = HullBox.X + (HullBox.Width - size.Width * scale) / 2;
        var hullY = HullBox.Y + (HullBox.Height - size.Height * scale);
        Scene.Place(Sprites.Fill(hull.Name, hullX, hullY, size.Width * scale, size.Height * scale));

        double ToX(double v) => hullX + v * scale;
        double ToY(double v) => hullY + v * scale;

        var deckLeft = ToX(hull.DeckLeft);
        var deckRight = ToX(hull.DeckRight);
        var deckTop = ToY(hull.DeckTop);
        var deckBottom = ToY(hull.DeckBottom);

        DrawBays(vessel, deckLeft, deckRight, deckTop, deckBottom);
        DrawDimensions(vessel, ToX(hull.Bow), ToX(hull.Stern), ToY(hull.Waterline));
    }

    private void DrawBays(Vessel vessel, double deckLeft, double deckRight, double deckTop, double deckBottom)
    {
        var bays = vessel.Bays;
        var bayWidth = (deckRight - deckLeft) / Math.Max(1, bays);
        var deckHeight = deckBottom - deckTop;

        var boundary = Math.Min(bays - 1, (int)Math.Floor(bays * vessel.UnloadPercent / 100d));
        var selected = _state.SelectedBay is { } bay && bay < bays ? bay : boundary;

        _bayWidth = bayWidth;
        _deckLeft = deckLeft;
        _deckTop = deckTop;
        _selectedBay = selected;

        var pillTop = deckBottom + 14;

        for (var i = 0; i < bays; i++)
        {
            var x = deckLeft + i * bayWidth;
            var index = i;

            // Bays already ashore: washed back and outlined, matching the legend swatch.
            if (i < boundary && vessel.UnloadPercent is > 0 and < 100)
            {
                var ashore = Draw.Rect(x, deckTop, bayWidth, deckHeight,
                    Tokens.Brush("PaperColor", 0.45), 1, Tokens.Brush("TextFaintColor", 0.55), 1);
                ashore.StrokeDashArray = Draw.Dash(3, 2.5);
                ashore.IsHitTestVisible = false;
                Scene.Place(ashore);
            }

            var hit = Draw.Rect(x, deckTop - 6, bayWidth, deckHeight + 34, Tokens.Transparent);
            hit.PointerEntered += (_, _) => _state.HoveredBay = index;
            hit.PointerExited += (_, _) => _state.HoveredBay = null;
            hit.PointerPressed += (_, _) => _state.SelectedBay = index;
            Scene.Place(hit);

            var pill = Draw.Rect(x + bayWidth / 2 - 14, pillTop, 28, 18,
                i == selected ? Tokens.Brush("AccentInvariantBrush") : Tokens.Brush("InkColor", 0.55), 6);
            pill.PointerPressed += (_, _) => _state.SelectedBay = index;
            Scene.Place(pill);

            Scene.Place(Draw.Text((i + 1).ToString("D2"), x + bayWidth / 2 - 14, pillTop + 2, 11,
                Tokens.Brush("SurfaceInvariantBrush"), "MonoMediumFont", TextAlignment.Center, 28));
        }

        _bayHighlight.Width = bayWidth;
        _bayHighlight.Height = deckHeight + 4;
        _bayHighlight.Fill = Tokens.Brush("DeckWhiteColor", 0.3);
        _bayHighlight.Stroke = Tokens.Brush("AccentBrightInvariantBrush");
        Scene.Place(_bayHighlight);
        RefreshBayHover();

        if (vessel.UnloadPercent is > 0 and < 100)
        {
            var lineX = deckLeft + Math.Round(bays * vessel.UnloadPercent / 100d) * bayWidth;
            Scene.Place(Draw.Rule(lineX, deckTop - 16, lineX, deckBottom + 2,
                Tokens.Brush("SeaGreenInvariantBrush"), 1.5, dash: Draw.Dash(4, 4)));
            Scene.Place(Draw.Text("Discharged", lineX - 70, deckTop - 48, 12,
                Tokens.Brush("SeaGreenInvariantBrush"), "BodyFont", TextAlignment.Center, 140));
            Scene.Place(Draw.Text($"{vessel.UnloadPercent}%", lineX - 70, deckTop - 36, 18,
                Tokens.Brush("SeaGreenInvariantBrush"), "BodyStrongFont", TextAlignment.Center, 140));
        }
    }

    private void RefreshBayHover()
    {
        if (_bayWidth <= 0)
        {
            return;
        }

        var bay = _state.HoveredBay ?? _selectedBay;
        _bayHighlight.At(_deckLeft + bay * _bayWidth, _deckTop - 2);
    }

    private void DrawDimensions(Vessel vessel, double bowX, double sternX, double waterline)
    {
        const double y = 262;
        var faint = Tokens.Brush("TextFaintInvariantBrush");
        var mid = (bowX + sternX) / 2;

        Scene.Place(Draw.Rule(bowX, y, sternX, y, faint, 1, dash: Draw.Dash(3, 3)));
        Scene.Place(Draw.Rule(bowX, y - 6, bowX, y + 6, faint));
        Scene.Place(Draw.Rule(sternX, y - 6, sternX, y + 6, faint));

        Scene.Place(Draw.Rect(mid - 29, y - 9, 58, 18, Tokens.Brush("PaperInvariantBrush")));
        Scene.Place(Draw.Text($"{vessel.Length} m", mid - 29, y - 8, 11, Tokens.Brush("InkInvariantBrush"),
            "MonoMediumFont", TextAlignment.Center, 58));

        var draftX = sternX - 40;
        Scene.Place(Draw.Rule(draftX, waterline, draftX, 286, faint, 1, dash: Draw.Dash(3, 3)));
        Scene.Place(Draw.Label($"↕ {vessel.Draft} m draft", draftX - 8, 272, 11, Tokens.Brush("InkInvariantBrush"),
            "MonoMediumFont", TextAlignment.Right, 160));
    }
}
