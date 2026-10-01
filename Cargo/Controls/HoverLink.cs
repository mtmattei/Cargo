using Microsoft.UI.Input;

namespace Cargo.Controls;

/// <summary>
/// Links an element to the thing it stands for elsewhere on screen: a mouse over it, or keyboard
/// focus on it, runs <c>Command</c> with <c>Key</c>; leaving runs it with <c>"-Key"</c>. Rows,
/// harbour tags and the needs-you bar use it to light each other through
/// <see cref="PortState.HoverCommand"/>. Touch does not hover, so it is ignored.
/// </summary>
// xaml-lint: allow codebehind - an attached behavior: pointer-over and keyboard focus have no Command surface
public static class HoverLink
{
    public static readonly DependencyProperty CommandProperty = DependencyProperty.RegisterAttached(
        "Command", typeof(ICommand), typeof(HoverLink), new PropertyMetadata(null, OnCommandChanged));

    public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached(
        "Key", typeof(string), typeof(HoverLink), new PropertyMetadata(null));

    public static ICommand? GetCommand(DependencyObject d) => (ICommand?)d.GetValue(CommandProperty);

    public static void SetCommand(DependencyObject d, ICommand? value) => d.SetValue(CommandProperty, value);

    public static string? GetKey(DependencyObject d) => (string?)d.GetValue(KeyProperty);

    public static void SetKey(DependencyObject d, string? value) => d.SetValue(KeyProperty, value);

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element || e.OldValue is not null)
        {
            return;
        }

        element.PointerEntered += (_, args) =>
        {
            if (args.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
            {
                Run(element, enter: true);
            }
        };
        element.PointerExited += (_, _) => Run(element, enter: false);
        element.PointerCanceled += (_, _) => Run(element, enter: false);
        element.GotFocus += (_, _) =>
        {
            if (element is Control { FocusState: FocusState.Keyboard })
            {
                Run(element, enter: true);
            }
        };
        element.LostFocus += (_, _) => Run(element, enter: false);
    }

    private static void Run(UIElement element, bool enter)
    {
        if (GetCommand(element) is { } command && GetKey(element) is { Length: > 0 } key)
        {
            command.Execute(enter ? key : "-" + key);
        }
    }
}
