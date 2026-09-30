using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Animation;

namespace Cargo.Controls;

/// <summary>
/// Sets a <see cref="RangeBase"/>'s value by sliding to it (<c>DurationBarMs</c> on
/// <c>EaseSmooth</c>) instead of jumping: <c>controls:Slide.Value="{x:Bind ...}"</c> on a
/// ProgressBar. The first value, and every value under reduced motion, is set at once.
/// </summary>
public static class Slide
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.RegisterAttached(
        "Value", typeof(double), typeof(Slide), new PropertyMetadata(double.NaN, OnValueChanged));

    private static readonly DependencyProperty BoardProperty = DependencyProperty.RegisterAttached(
        "Board", typeof(Storyboard), typeof(Slide), new PropertyMetadata(null));

    public static double GetValue(DependencyObject d) => (double)d.GetValue(ValueProperty);

    public static void SetValue(DependencyObject d, double value) => d.SetValue(ValueProperty, value);

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RangeBase range || e.NewValue is not double to || double.IsNaN(to))
        {
            return;
        }

        // Start from wherever a running slide has the bar, so a quick second pick re-targets smoothly
        var from = range.Value;
        (range.GetValue(BoardProperty) as Storyboard)?.Stop();
        range.Value = from;

        if (double.IsNaN((double)e.OldValue) || Motion.Reduced || !range.IsLoaded)
        {
            range.Value = to;
            return;
        }

        var animation = new DoubleAnimationUsingKeyFrames { EnableDependentAnimation = true };
        animation.KeyFrames.Add(new SplineDoubleKeyFrame
        {
            KeyTime = Motion.Duration("DurationBarMs"),
            Value = to,
            KeySpline = (KeySpline)Application.Current.Resources["EaseSmooth"]
        });
        Storyboard.SetTarget(animation, range);
        Storyboard.SetTargetProperty(animation, nameof(RangeBase.Value));

        var board = new Storyboard { Children = { animation } };
        // Hand the end value back to the property so a stopped board never snaps it back
        board.Completed += (_, _) =>
        {
            board.Stop();
            range.Value = to;
        };
        range.SetValue(BoardProperty, board);
        board.Begin();
    }
}
