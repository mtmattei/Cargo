using Microsoft.UI.Xaml.Media;

namespace Cargo.Controls;

/// <summary>
/// Section pages stay alive once visited: the router's Visibility region collapses a page when
/// you move on rather than unloading it. Without a guard every one of them would keep
/// rebuilding on each yard move and each clock tick while off screen. These subscriptions only
/// do the work while the element is actually shown, and catch up on the way back.
/// </summary>
internal static class Live
{
    public static void RebuildWhenVisible(this FrameworkElement element, PortState state, Action rebuild) =>
        Subscribe(element, rebuild, handler => state.StructureChanged += handler);

    public static void TickWhenVisible(this FrameworkElement element, PortState state, Action refresh) =>
        Subscribe(element, refresh, handler => state.Ticked += handler);

    /// <summary>For the yard's own live moves, which only the yard map cares about.</summary>
    public static void RebuildOnYardMove(this FrameworkElement element, PortState state, Action rebuild) =>
        Subscribe(element, rebuild, handler => state.YardChanged += handler);

    /// <summary>For hover, which repaints what is already there rather than rebuilding it.</summary>
    public static void RepaintWhenVisible(this FrameworkElement element, PortState state, Action repaint) =>
        Subscribe(element, repaint, handler => state.HoverChanged += handler);

    /// <summary>
    /// Reports when the element comes on screen and when it leaves: loaded, with no collapsed
    /// ancestor. Animation timers start and stop on this, so a hidden page costs nothing.
    /// </summary>
    public static void TrackShown(this FrameworkElement element, Action<bool> changed)
    {
        var callbacks = new List<(UIElement Node, long Token)>();
        var shown = false;

        void Update()
        {
            var now = element.IsLoaded && callbacks.TrueForAll(c => c.Node.Visibility == Visibility.Visible);
            if (now != shown)
            {
                shown = now;
                changed(now);
            }
        }

        void Release()
        {
            foreach (var (node, token) in callbacks)
            {
                node.UnregisterPropertyChangedCallback(UIElement.VisibilityProperty, token);
            }

            callbacks.Clear();
        }

        // Watch every ancestor's Visibility: the region collapses the page, not this element
        void Hook()
        {
            Release();
            for (var node = (DependencyObject?)element; node is not null; node = VisualTreeHelper.GetParent(node))
            {
                if (node is UIElement ui)
                {
                    callbacks.Add((ui, ui.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, (_, _) => Update())));
                }
            }

            Update();
        }

        element.Loaded += (_, _) => Hook();
        element.Unloaded += (_, _) =>
        {
            Release();
            if (shown)
            {
                shown = false;
                changed(false);
            }
        };

        // A page that is attached after it loaded has already missed its Loaded event
        if (element.IsLoaded)
        {
            Hook();
        }
    }

    /// <summary>
    /// Runs a decorative animation timer while the element is shown and reduced motion is off.
    /// The scene holds its last frame when motion is turned off. It only listens for the setting
    /// while shown, so the static event never keeps an off-screen scene alive.
    /// </summary>
    public static void RunWhileShown(this FrameworkElement element, DispatcherTimer timer)
    {
        void Apply()
        {
            if (Motion.Reduced) { timer.Stop(); } else { timer.Start(); }
        }

        void OnMotionChanged(object? sender, EventArgs e) => Apply();

        element.TrackShown(shown =>
        {
            if (shown)
            {
                Motion.Changed += OnMotionChanged;
                Apply();
            }
            else
            {
                Motion.Changed -= OnMotionChanged;
                timer.Stop();
            }
        });
    }

    private static void Subscribe(FrameworkElement element, Action work, Action<EventHandler> attach)
    {
        var shown = false;
        var pending = false;

        attach((_, _) =>
        {
            if (shown)
            {
                work();
            }
            else
            {
                pending = true;
            }
        });

        element.TrackShown(now =>
        {
            shown = now;
            if (now && pending)
            {
                pending = false;
                work();
            }
        });
    }
}
