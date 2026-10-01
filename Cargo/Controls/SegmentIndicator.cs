using Microsoft.UI.Xaml.Media.Animation;

namespace Cargo.Controls;

/// <summary>
/// The selection mark of a segmented control: one pill that slides and resizes to the selected
/// segment. Place it behind the segments in the same Grid, point <see cref="Host"/> at the
/// ItemsControl or Panel that holds them, and bind <see cref="SelectedIndex"/>; -1 fades it out
/// in place. Uno Toolkit's segmented TabBar style ships only with the Cupertino theme, so the
/// layer switcher and the Chart/Table toggle share this instead.
/// </summary>
// xaml-lint: allow builtin - utu:TabBar SegmentedStyle is absent from the Simple and Material themes (Toolkit 9.1.3, checked 2026-09-30); WCT 8 Segmented would be a new package (open question in HANDOFF)
public sealed partial class SegmentIndicator : Border
{
    private readonly TranslateTransform _shift = new();
    private Storyboard? _board;
    private FrameworkElement? _watched;

    public SegmentIndicator()
    {
        HorizontalAlignment = HorizontalAlignment.Left;
        IsHitTestVisible = false;
        Opacity = 0;
        Width = 0;
        RenderTransform = _shift;
    }

    public static readonly DependencyProperty HostProperty = DependencyProperty.Register(
        nameof(Host), typeof(FrameworkElement), typeof(SegmentIndicator),
        new PropertyMetadata(null, (d, e) => ((SegmentIndicator)d).OnHostChanged(e.OldValue as FrameworkElement, e.NewValue as FrameworkElement)));

    /// <summary>The ItemsControl or Panel whose children are the segments.</summary>
    public FrameworkElement? Host
    {
        get => (FrameworkElement?)GetValue(HostProperty);
        set => SetValue(HostProperty, value);
    }

    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
        nameof(SelectedIndex), typeof(int), typeof(SegmentIndicator),
        new PropertyMetadata(-1, (d, _) => ((SegmentIndicator)d).Move(animate: true)));

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    private void OnHostChanged(FrameworkElement? old, FrameworkElement? host)
    {
        if (old is not null)
        {
            old.SizeChanged -= OnHostSizeChanged;
        }

        if (host is not null)
        {
            host.SizeChanged += OnHostSizeChanged;
        }

        Move(animate: false);
    }

    private void OnHostSizeChanged(object sender, SizeChangedEventArgs e) => Move(animate: false);

    private FrameworkElement? Segment(int index) => index < 0 ? null : Host switch
    {
        ItemsControl items => items.ContainerFromIndex(index) as FrameworkElement,
        Panel panel when index < panel.Children.Count => panel.Children[index] as FrameworkElement,
        _ => null
    };

    /// <summary>
    /// Position and width together, on EaseSmooth. The keyframes carry no From, so a click
    /// mid-slide re-targets from wherever the pill is. No selection fades it out in place.
    /// </summary>
    private void Move(bool animate)
    {
        var segment = Segment(SelectedIndex);

        // A segment that has not been laid out yet reports width 0: follow it until it has one
        if (!ReferenceEquals(segment, _watched))
        {
            if (_watched is not null)
            {
                _watched.SizeChanged -= OnHostSizeChanged;
            }

            _watched = segment;
            if (_watched is not null)
            {
                _watched.SizeChanged += OnHostSizeChanged;
            }
        }

        var parent = Parent as UIElement;
        var target = segment is { ActualWidth: > 0 } && parent is not null
            ? (X: segment.TransformToVisual(parent).TransformPoint(default).X, Width: segment.ActualWidth, Opacity: 1d)
            : (X: _shift.X, Width: Width, Opacity: 0d);

        // Hand off mid-slide: read where the running board has the pill, stop it, pin that as the
        // start. A stopped board otherwise snaps back to the old local values.
        var (x, width, opacity) = (_shift.X, Width, Opacity);
        _board?.Stop();
        _board = null;
        var instant = !animate || Motion.Reduced || double.IsNaN(width) || width <= 0;
        _shift.X = instant ? target.X : x;
        Width = instant ? target.Width : width;
        Opacity = instant ? target.Opacity : opacity;
        if (instant)
        {
            return;
        }

        var board = new Storyboard();
        var slide = Motion.Duration(segment is null ? "DurationFastMs" : "DurationSlideMs");
        Add(board, _shift, "X", target.X, slide, dependent: false);
        Add(board, this, "Width", target.Width, slide, dependent: true);
        Add(board, this, "Opacity", target.Opacity, Motion.Duration("DurationFastMs"), dependent: false);
        board.Begin();
        _board = board;

        static void Add(Storyboard board, DependencyObject target, string property, double to, TimeSpan duration, bool dependent)
        {
            var animation = new DoubleAnimationUsingKeyFrames { EnableDependentAnimation = dependent };
            animation.KeyFrames.Add(new SplineDoubleKeyFrame
            {
                KeyTime = duration,
                Value = to,
                KeySpline = (KeySpline)Application.Current.Resources["EaseSmooth"]
            });
            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, property);
            board.Children.Add(animation);
        }
    }
}
