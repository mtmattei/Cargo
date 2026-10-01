using Windows.Foundation;

namespace Cargo.Controls;

/// <summary>
/// Hosts a fixed design-space <see cref="Canvas"/> and scales it to the width it is given —
/// the WinUI equivalent of an SVG viewBox. Every chart and map in the app draws into one.
/// </summary>
public partial class SceneHost : Panel
{
    private readonly ScaleTransform _scale = new();

    public SceneHost(double sceneWidth, double sceneHeight, bool stretchHeight = false)
    {
        SceneWidth = sceneWidth;
        SceneHeight = sceneHeight;
        StretchHeight = stretchHeight;

        Scene = new Canvas { Width = sceneWidth, Height = sceneHeight, RenderTransform = _scale };
        Children.Add(Scene);
    }

    public Canvas Scene { get; }

    public double SceneWidth { get; }

    public double SceneHeight { get; }

    /// <summary>When set, the vertical scale follows the arranged height instead of the width.</summary>
    public bool StretchHeight { get; }

    /// <summary>Extra room below the scene for labels that overflow the design box.</summary>
    public double Overflow { get; set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) || availableSize.Width <= 0
            ? SceneWidth
            : availableSize.Width;

        Scene.Measure(new Size(SceneWidth, SceneHeight));

        if (StretchHeight)
        {
            var height = double.IsInfinity(availableSize.Height) ? SceneHeight : availableSize.Height;
            return new Size(width, height);
        }

        return new Size(width, width * (SceneHeight + Overflow) / SceneWidth);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _scale.ScaleX = finalSize.Width / SceneWidth;
        _scale.ScaleY = StretchHeight ? finalSize.Height / SceneHeight : _scale.ScaleX;
        Scene.Arrange(new Rect(0, 0, SceneWidth, SceneHeight));
        return finalSize;
    }
}
