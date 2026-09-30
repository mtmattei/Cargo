using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace Cargo.Controls;

/// <summary>One row of the berth timeline: occupancy blocks placed by fraction of the window.</summary>
public sealed partial class ProportionalPanel : Panel
{
    public sealed record Segment(string Label, double Start, double Width, Brush Fill, double Opacity);

    public static readonly DependencyProperty SegmentsProperty = DependencyProperty.Register(
        nameof(Segments), typeof(IReadOnlyList<Segment>), typeof(ProportionalPanel),
        new PropertyMetadata(null, (d, _) => ((ProportionalPanel)d).Rebuild()));

    public IReadOnlyList<Segment>? Segments
    {
        get => (IReadOnlyList<Segment>?)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public static readonly DependencyProperty NowFractionProperty = DependencyProperty.Register(
        nameof(NowFraction), typeof(double), typeof(ProportionalPanel),
        new PropertyMetadata(0d, (d, _) => ((ProportionalPanel)d).InvalidateArrange()));

    public double NowFraction
    {
        get => (double)GetValue(NowFractionProperty);
        set => SetValue(NowFractionProperty, value);
    }

    private void Rebuild()
    {
        Children.Clear();
        if (Segments is null)
        {
            return;
        }

        foreach (var segment in Segments)
        {
            Children.Add(new Border
            {
                CornerRadius = new CornerRadius(7),
                Background = segment.Fill,
                Opacity = segment.Opacity,
                Padding = new Thickness(9, 0, 9, 0),
                Child = new TextBlock
                {
                    Text = segment.Label,
                    FontFamily = (FontFamily)Application.Current.Resources["BodyMediumFont"],
                    FontSize = 11,
                    Foreground = Tokens.Brush("SurfaceInvariantBrush"),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.Clip
                }
            });
        }

        Children.Add(new Rectangle { Width = 1.5, Fill = Tokens.Brush("InkInvariantBrush") });
        InvalidateArrange();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(availableSize);
        }

        return new Size(
            double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 26 : availableSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var segments = Segments ?? Array.Empty<Segment>();

        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            Children[i].Arrange(new Rect(
                finalSize.Width * segment.Start,
                0,
                Math.Max(0, finalSize.Width * segment.Width),
                finalSize.Height));
        }

        if (Children.Count > segments.Count)
        {
            Children[^1].Arrange(new Rect(finalSize.Width * NowFraction, -4, 1.5, finalSize.Height + 8));
        }

        return finalSize;
    }
}
