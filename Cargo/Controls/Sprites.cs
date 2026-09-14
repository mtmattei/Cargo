using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;

namespace Cargo.Controls;

/// <summary>
/// The design's rendered artwork. Every scene in the app is drawn in the design's own
/// coordinate space, so sprites are placed with the same rects the design uses.
/// </summary>
/// <remarks>
/// <see cref="BitmapImage"/> instances are cached and shared: the decoded bitmap is the
/// expensive part, and scenes are rebuilt often enough that decoding per rebuild is what
/// makes a redraw feel slow. Sharing one source across many <see cref="Image"/> elements
/// is supported and is what keeps the harbour rebuild cheap.
/// </remarks>
internal static class Sprites
{
    /// <summary>Intrinsic pixel size of each sprite, so "meet" fits can be computed exactly.</summary>
    private static readonly Dictionary<string, Size> Intrinsic = new()
    {
        ["buoy-green"] = new(58, 110),
        ["buoy-red"] = new(70, 110),
        ["cont-blue"] = new(113, 87),
        ["cont-blue-side"] = new(79, 70),
        ["cont-flat"] = new(110, 69),
        ["cont-open"] = new(73, 59),
        ["cont-orange"] = new(130, 88),
        ["cont-orange-side"] = new(87, 70),
        ["cont-red"] = new(112, 89),
        ["cont-red-side"] = new(82, 70),
        ["cont-reefer"] = new(107, 88),
        ["cont-stack"] = new(136, 110),
        ["cont-tank"] = new(109, 81),
        ["cont-white-side"] = new(121, 73),
        ["cont-yellow"] = new(130, 88),
        ["cont-yellow-side"] = new(80, 69),
        ["crane-leg-0"] = new(48, 50),
        ["crane-leg-1"] = new(46, 50),
        ["crane-leg-2"] = new(46, 50),
        ["crane-leg-3"] = new(46, 50),
        ["crane-rtg-1"] = new(149, 186),
        ["crane-side"] = new(380, 280),
        ["levant-anchor"] = new(180, 60),
        ["msc-bow"] = new(178, 240),
        ["msc-plan"] = new(844, 134),
        ["msc-side"] = new(845, 245),
        ["nordic-approach"] = new(170, 170),
        ["ship-bow"] = new(178, 268),
        ["ship-plan"] = new(934, 142),
        ["ship-side"] = new(1018, 274),
        ["terminal-strip"] = new(1328, 128),
        ["tug-plan-b"] = new(240, 99),
        ["water-tile"] = new(280, 80)
    };

    private static readonly Dictionary<string, BitmapImage> Cache = new();

    public static BitmapImage Source(string name)
    {
        if (!Cache.TryGetValue(name, out var bitmap))
        {
            bitmap = new BitmapImage(new Uri($"ms-appx:///Assets/Sprites/{name}.png"));
            Cache[name] = bitmap;
        }

        return bitmap;
    }

    public static Size SizeOf(string name) =>
        Intrinsic.TryGetValue(name, out var size) ? size : new Size(1, 1);

    /// <summary>An sprite stretched to fill the rect exactly (SVG preserveAspectRatio="none").</summary>
    public static Image Fill(string name, double x, double y, double w, double h, double opacity = 1)
        => Element(name, x, y, w, h, opacity);

    /// <summary>
    /// A sprite fitted inside the rect with its aspect kept (SVG "meet"). <paramref name="anchorY"/>
    /// is 0 for top, 0.5 for middle (xMidYMid) and 1 for bottom (xMidYMax).
    /// </summary>
    public static Image Fit(string name, double x, double y, double w, double h,
        double anchorY = 0.5, double opacity = 1)
    {
        var size = SizeOf(name);
        var scale = Math.Min(w / size.Width, h / size.Height);
        var fw = size.Width * scale;
        var fh = size.Height * scale;

        return Element(name, x + (w - fw) / 2, y + (h - fh) * anchorY, fw, fh, opacity);
    }

    // ── Which artwork belongs to what ─────────────────────────────────────────

    /// <summary>
    /// Where a hull sprite's painted deck sits inside its own pixels, so an interactive bay
    /// grid can be laid over the containers the artwork actually shows. Measured off the
    /// sprites once; everything downstream scales from these.
    /// </summary>
    internal readonly record struct Hull(
        string Name, double DeckLeft, double DeckRight, double DeckTop, double DeckBottom,
        double Waterline, double Bow, double Stern)
    {
        public Size Size => SizeOf(Name);
    }

    private static readonly Hull ShipHull = new("ship-side", 135, 872, 78, 172, 228, 10, 1010);
    private static readonly Hull MscHull = new("msc-side", 95, 690, 76, 160, 205, 8, 820);

    /// <summary>MSC Aurora has her own render; the rest of the fleet shares the generic hull.</summary>
    public static bool IsMsc(string vesselId) => vesselId == "aurora";

    internal static Hull HullOf(string vesselId) => IsMsc(vesselId) ? MscHull : ShipHull;

    public static string Side(string vesselId) => HullOf(vesselId).Name;

    public static string Plan(string vesselId) => IsMsc(vesselId) ? "msc-plan" : "ship-plan";

    public static string Bow(string vesselId) => IsMsc(vesselId) ? "msc-bow" : "ship-bow";

    /// <summary>The isometric render for a container class, for the inspection panels.</summary>
    public static string Container(CargoClass cargo) => cargo switch
    {
        CargoClass.Reefer => "cont-reefer",
        CargoClass.Hazard => "cont-red",
        CargoClass.Oversize => "cont-flat",
        CargoClass.Empty => "cont-open",
        _ => "cont-blue"
    };

    /// <summary>The side-on render for a container class, for quay piles and crane hoists.</summary>
    public static string ContainerSide(CargoClass cargo) => cargo switch
    {
        CargoClass.Reefer => "cont-white-side",
        CargoClass.Hazard => "cont-red-side",
        CargoClass.Oversize => "cont-orange-side",
        CargoClass.Empty => "cont-yellow-side",
        _ => "cont-blue-side"
    };

    /// <summary>
    /// The container renders, chosen by the tint token the yard data already carries so the
    /// box on screen is the one the record describes.
    /// </summary>
    public static string ContainerByTone(string tintToken) => tintToken switch
    {
        "CargoReeferColor" => "cont-reefer",
        "CargoHazardColor" => "cont-red",
        "CargoOversizeColor" => "cont-flat",
        "CargoEmptyColor" => "cont-open",
        "ContainerRustColor" => "cont-orange",
        "ContainerOliveColor" => "cont-tank",
        "ContainerMagentaColor" => "cont-yellow",
        _ => "cont-blue"
    };

    public static string ContainerSideByTone(string tintToken) => tintToken switch
    {
        "CargoReeferColor" => "cont-white-side",
        "CargoHazardColor" => "cont-red-side",
        "CargoOversizeColor" => "cont-orange-side",
        "CargoEmptyColor" => "cont-yellow-side",
        "ContainerRustColor" => "cont-orange-side",
        "ContainerMagentaColor" => "cont-yellow-side",
        _ => "cont-blue-side"
    };

    private static Image Element(string name, double x, double y, double w, double h, double opacity)
    {
        var image = new Image
        {
            Source = Source(name),
            Width = Math.Max(0, w),
            Height = Math.Max(0, h),
            Stretch = Stretch.Fill,
            Opacity = opacity,
            IsHitTestVisible = false
        };

        return image.At(x, y);
    }
}
