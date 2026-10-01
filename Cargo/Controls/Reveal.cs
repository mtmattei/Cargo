namespace Cargo.Controls;

/// <summary>
/// Shows and hides an element by easing it in and out (<c>DurationRevealMs</c> on <c>EaseSmooth</c>)
/// instead of switching: <c>controls:Reveal.IsOn="{x:Bind IsExpanded, Mode=OneWay}"</c>.
/// <list type="bullet">
/// <item>Opacity fades 0 to <see cref="OnOpacityProperty"/>; <see cref="RiseProperty"/> also lifts a TranslateTransform by that many pixels.</item>
/// <item><see cref="GrowProperty"/> grows the height from 0 to the content's own and back, collapsing at 0.</item>
/// <item><see cref="FadeProperty"/> False keeps the opacity (a rise alone); <see cref="HidesProperty"/> collapses it once faded out.</item>
/// </list>
/// The values are stepped per frame (CompositionTarget.Rendering, by elapsed time) as local values
/// and the last step writes the end state, so nothing is left held by an animation: replacing a
/// running Storyboard left rows stuck open on Uno 6.7 (Activity, 2026-10-01). Rendering stops once
/// a frame changes nothing on screen, so a one-shot guard timer lands the end state if frames stop
/// first; stepping on a timer alone beat against the display and stuttered.
/// Under reduced motion, or before load, the end state applies at once.
/// </summary>
// xaml-lint: allow codebehind - an attached animation behavior; it sets Visibility only to finish a grow-out
public static class Reveal
{
    public static readonly DependencyProperty IsOnProperty = DependencyProperty.RegisterAttached(
        "IsOn", typeof(bool), typeof(Reveal), new PropertyMetadata(false, OnIsOnChanged));

    public static readonly DependencyProperty RiseProperty = DependencyProperty.RegisterAttached(
        "Rise", typeof(double), typeof(Reveal), new PropertyMetadata(0d));

    public static readonly DependencyProperty GrowProperty = DependencyProperty.RegisterAttached(
        "Grow", typeof(bool), typeof(Reveal), new PropertyMetadata(false));

    /// <summary>The opacity when on (1 by default); below 1 lets what is under a backdrop layer read through.</summary>
    public static readonly DependencyProperty OnOpacityProperty = DependencyProperty.RegisterAttached(
        "OnOpacity", typeof(double), typeof(Reveal), new PropertyMetadata(1d));

    public static readonly DependencyProperty FadeProperty = DependencyProperty.RegisterAttached(
        "Fade", typeof(bool), typeof(Reveal), new PropertyMetadata(true));

    public static readonly DependencyProperty HidesProperty = DependencyProperty.RegisterAttached(
        "Hides", typeof(bool), typeof(Reveal), new PropertyMetadata(false));

    private sealed class Tween
    {
        public required FrameworkElement Element { get; init; }
        public required bool On { get; init; }
        public required DateTimeOffset Start { get; init; }
        public required TimeSpan Duration { get; init; }
        public (double From, double To)? Opacity { get; init; }
        public (TranslateTransform Shift, double From, double To)? Rise { get; init; }
        public (double From, double To)? Height { get; init; }
        public bool Hides { get; init; }
    }

    // One tween per element; a new one replaces it from wherever it has the element
    private static readonly Dictionary<FrameworkElement, Tween> Running = new();
    private static Microsoft.UI.Dispatching.DispatcherQueueTimer? _guard;

    public static bool GetIsOn(DependencyObject d) => (bool)d.GetValue(IsOnProperty);

    public static void SetIsOn(DependencyObject d, bool value) => d.SetValue(IsOnProperty, value);

    public static double GetRise(DependencyObject d) => (double)d.GetValue(RiseProperty);

    public static void SetRise(DependencyObject d, double value) => d.SetValue(RiseProperty, value);

    public static bool GetGrow(DependencyObject d) => (bool)d.GetValue(GrowProperty);

    public static void SetGrow(DependencyObject d, bool value) => d.SetValue(GrowProperty, value);

    public static double GetOnOpacity(DependencyObject d) => (double)d.GetValue(OnOpacityProperty);

    public static void SetOnOpacity(DependencyObject d, double value) => d.SetValue(OnOpacityProperty, value);

    public static bool GetFade(DependencyObject d) => (bool)d.GetValue(FadeProperty);

    public static void SetFade(DependencyObject d, bool value) => d.SetValue(FadeProperty, value);

    public static bool GetHides(DependencyObject d) => (bool)d.GetValue(HidesProperty);

    public static void SetHides(DependencyObject d, bool value) => d.SetValue(HidesProperty, value);

    private static void OnIsOnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element || e.NewValue is not bool on)
        {
            return;
        }

        var shift = element.RenderTransform as TranslateTransform;
        var visible = element.Visibility == Visibility.Visible;

        // From wherever the element is now: a running tween's last frame left these as local values
        var tween = new Tween
        {
            Element = element,
            On = on,
            Start = DateTimeOffset.Now,
            Duration = Motion.Duration("DurationRevealMs"),
            Opacity = GetFade(element) ? (visible ? element.Opacity : 0, on ? GetOnOpacity(element) : 0) : null,
            Rise = shift is null ? null : (shift, shift.Y, on ? -GetRise(element) : 0),
            Height = GetGrow(element) ? (visible ? element.ActualHeight : 0, on ? Natural(element) : 0) : null,
            Hides = GetGrow(element) || GetHides(element)
        };

        if (!element.IsLoaded || Motion.Reduced)
        {
            Running.Remove(element);
            Apply(tween, 1);
            return;
        }

        if (tween.Hides)
        {
            element.Visibility = Visibility.Visible;
        }

        Apply(tween, 0);
        if (Running.Count == 0)
        {
            CompositionTarget.Rendering += OnFrame;
        }

        Running[element] = tween;
        if (_guard is null)
        {
            _guard = element.DispatcherQueue.CreateTimer();
            _guard.IsRepeating = false;
            _guard.Tick += (_, _) => Step();
        }

        // Restarted by each tween, so it fires once the latest has had its time
        _guard.Interval = tween.Duration + TimeSpan.FromMilliseconds(40);
        _guard.Stop();
        _guard.Start();
    }

    private static void OnFrame(object? sender, object e) => Step();

    private static void Step()
    {
        var now = DateTimeOffset.Now;
        foreach (var tween in Running.Values.ToArray())
        {
            var k = (now - tween.Start).TotalMilliseconds / tween.Duration.TotalMilliseconds;
            Apply(tween, Math.Min(1, k));
            if (k >= 1)
            {
                Running.Remove(tween.Element);
            }
        }

        if (Running.Count == 0)
        {
            CompositionTarget.Rendering -= OnFrame;
            _guard?.Stop();
        }
    }

    /// <summary>One step at progress <paramref name="k"/>; at 1 it writes the end state.</summary>
    private static void Apply(Tween tween, double k)
    {
        var eased = Motion.Curve(k);
        var element = tween.Element;
        if (tween.Opacity is { } opacity)
        {
            element.Opacity = opacity.From + (opacity.To - opacity.From) * eased;
        }

        if (tween.Rise is { } rise)
        {
            rise.Shift.Y = rise.From + (rise.To - rise.From) * eased;
        }

        if (tween.Height is { } height)
        {
            element.Height = k >= 1 ? double.NaN : height.From + (height.To - height.From) * eased;
        }

        if (k >= 1 && tween.Hides)
        {
            element.Visibility = tween.On ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>The content's own height at the width it will have: the grow-in target.</summary>
    private static double Natural(FrameworkElement element)
    {
        var width = element.ActualWidth > 0 ? element.ActualWidth : (element.Parent as FrameworkElement)?.ActualWidth ?? double.PositiveInfinity;
        var height = element.Height;
        element.Height = double.NaN;
        element.Measure(new Windows.Foundation.Size(width, double.PositiveInfinity));
        element.Height = height;
        return element.DesiredSize.Height;
    }
}
