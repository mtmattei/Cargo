using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Cargo.Controls;

/// <summary>Base for the fixed-design-space vector sprites the design has no render for.</summary>
public abstract partial class SpriteControl : UserControl
{
    protected Canvas Surface { get; }

    protected SpriteControl(double width, double height)
    {
        Surface = Draw.Stage(width, height);
        Content = new Viewbox { Stretch = Stretch.Uniform, Child = Surface };
        IsHitTestVisible = false;
        Loaded += (_, _) => Rebuild();
    }

    protected static readonly PropertyChangedCallback Redraw =
        (d, _) => ((SpriteControl)d).Rebuild();

    protected abstract void Compose();

    protected void Rebuild()
    {
        Surface.Children.Clear();
        Compose();
    }
}
/// <summary>Yard tractor with a chassis, seen from the side.</summary>
public sealed partial class YardTruck : SpriteControl
{
    public YardTruck() : base(120, 48)
    {
    }

    public static readonly DependencyProperty TintProperty = DependencyProperty.Register(
        nameof(Tint), typeof(Color), typeof(YardTruck),
        new PropertyMetadata(Microsoft.UI.Colors.SlateGray, Redraw));

    public Color Tint
    {
        get => (Color)GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    protected override void Compose()
    {
        Surface.Place(Draw.Rect(34, 10, 80, 22, Tokens.Of(Tint), 2));
        Surface.Place(Draw.Rect(34, 10, 80, 4, Tokens.Of(Tokens.Shade(Tint, 30))));
        Surface.Place(Draw.Rect(6, 12, 26, 20, Tokens.Brush("BridgeBrush"), 3));
        Surface.Place(Draw.Rect(9, 15, 12, 9, Tokens.Brush("ShallowsBrush"), 1));
        Surface.Place(Draw.Rect(4, 32, 112, 4, Tokens.Brush("SteelBrush"), 1));
        Surface.Place(Draw.Dot(16, 39, 5, Tokens.Brush("InkBrush")));
        Surface.Place(Draw.Dot(86, 39, 5, Tokens.Brush("InkBrush")));
        Surface.Place(Draw.Dot(101, 39, 5, Tokens.Brush("InkBrush")));
    }
}
