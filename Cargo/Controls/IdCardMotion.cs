using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;

namespace Cargo.Controls;

/// <summary>
/// The duty operator's ID card: it drops into place once per launch (700 ms after 250 ms, from
/// opacity 0, 10 px up and tilted 5 degrees to its resting 1.2) and straightens and lifts 2 px
/// under the pointer (300 ms). Both on EaseSmooth; under reduced motion it simply rests. The
/// element's RenderTransform must be a CompositeTransform carrying the resting rotation.
/// </summary>
// xaml-lint: allow codebehind - an attached behavior: Loaded and pointer-over have no Command surface
public static class IdCardMotion
{
    private const double RestAngle = -1.2, EntryAngle = -5, EntryRise = -10, HoverLift = -2;
    private static bool _entered;

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(IdCardMotion), new PropertyMetadata(false, OnEnabledChanged));

    // The hover's running board, stopped before the next pose starts
    private static readonly DependencyProperty BoardProperty = DependencyProperty.RegisterAttached(
        "Board", typeof(Storyboard), typeof(IdCardMotion), new PropertyMetadata(null));

    public static bool GetIsEnabled(DependencyObject d) => (bool)d.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject d, bool value) => d.SetValue(IsEnabledProperty, value);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement card || e.NewValue is not true)
        {
            return;
        }

        card.Loaded += (_, _) => Enter(card);
        card.PointerEntered += (_, args) =>
        {
            if (args.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
            {
                Pose(card, angle: 0, y: HoverLift);
            }
        };
        card.PointerExited += (_, _) => Pose(card, angle: RestAngle, y: 0);
    }

    private static void Enter(FrameworkElement card)
    {
        if (_entered || card.RenderTransform is not CompositeTransform transform)
        {
            return;
        }

        _entered = true;
        if (Motion.Reduced)
        {
            return;
        }

        var board = new Storyboard();
        var delay = TimeSpan.FromMilliseconds(250);
        var duration = TimeSpan.FromMilliseconds(700);
        Add(board, card, "Opacity", 0, 1, delay, duration);
        Add(board, transform, "TranslateY", EntryRise, 0, delay, duration);
        Add(board, transform, "Rotation", EntryAngle, RestAngle, delay, duration);
        board.Begin();
    }

    private static void Pose(FrameworkElement card, double angle, double y)
    {
        if (card.RenderTransform is not CompositeTransform transform)
        {
            return;
        }

        if (Motion.Reduced)
        {
            transform.Rotation = angle;
            transform.TranslateY = y;
            return;
        }

        // Read where a running pose has the card before stopping it, so a quick in-and-out re-targets smoothly
        var (fromAngle, fromY) = (transform.Rotation, transform.TranslateY);
        (card.GetValue(BoardProperty) as Storyboard)?.Stop();
        transform.Rotation = angle;
        transform.TranslateY = y;
        var board = new Storyboard();
        var duration = TimeSpan.FromMilliseconds(300);
        Add(board, transform, "Rotation", fromAngle, angle, TimeSpan.Zero, duration);
        Add(board, transform, "TranslateY", fromY, y, TimeSpan.Zero, duration);
        card.SetValue(BoardProperty, board);
        board.Begin();
    }

    /// <summary>
    /// One eased track: the start value (if given) holds from 0 through <paramref name="delay"/>, then
    /// eases to <paramref name="to"/>. FillBehavior Stop over the local resting value, so a finished
    /// board never holds a value against a later one (gotcha G60); a null start eases from wherever it is.
    /// </summary>
    private static void Add(Storyboard board, DependencyObject target, string property, double? from, double to, TimeSpan delay, TimeSpan duration)
    {
        var animation = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
        if (from is { } start)
        {
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame { KeyTime = TimeSpan.Zero, Value = start });
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame { KeyTime = delay, Value = start });
        }

        animation.KeyFrames.Add(new SplineDoubleKeyFrame
        {
            KeyTime = delay + duration,
            Value = to,
            KeySpline = (KeySpline)Application.Current.Resources["EaseSmooth"]
        });
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        board.Children.Add(animation);
    }
}
