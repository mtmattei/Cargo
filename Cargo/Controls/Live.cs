namespace Cargo.Controls;

/// <summary>
/// Section views are cached once visited, so without a guard every one of them would keep
/// rebuilding on each yard move and each clock tick while off screen. These subscriptions
/// only do the work when the element is actually in the tree, and catch up on the way back.
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

    private static void Subscribe(FrameworkElement element, Action work, Action<EventHandler> attach)
    {
        var pending = false;

        attach((_, _) =>
        {
            if (element.IsLoaded)
            {
                work();
            }
            else
            {
                pending = true;
            }
        });

        element.Loaded += (_, _) =>
        {
            if (!pending)
            {
                return;
            }

            pending = false;
            work();
        };
    }
}
