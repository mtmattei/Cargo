using System.ComponentModel;
using Microsoft.UI.Xaml.Media;

namespace Cargo.Presentation;

/// <summary>
/// The Needs-you queue over <see cref="PortState.Decisions"/>. The row visuals that depend on a
/// decision (icon, button, late ink) are picked by the static functions below through x:Bind, so
/// the decision itself carries no brushes.
/// </summary>
public sealed partial class NeedsYouPanel : UserControl
{
    private PortState? _state;

    public NeedsYouPanel()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
    }

    public PortState? ViewModel => DataContext as PortState;

    private void Attach()
    {
        if (_state is not null)
        {
            _state.PropertyChanged -= OnStateChanged;
        }

        _state = ViewModel;
        if (_state is not null)
        {
            _state.PropertyChanged += OnStateChanged;
        }

        Bindings.Update();
    }

    // xaml-lint: allow codebehind - "Needs you" in the masthead scrolls this section into view and
    // focuses its heading; bringing an element into view has no XAML surface
    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PortState.NeedsYouReveal))
        {
            Section.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = !Motion.Reduced, VerticalOffset = 24 });
            Heading.Focus(FocusState.Programmatic);
        }
    }

    public static Brush Ground(int count) => count > 0 ? Tokens.Brush("AlertInvariantBrush", .05) : Tokens.Transparent;

    public static Visibility Shown(int count) => count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility Empty(int count) => count > 0 ? Visibility.Collapsed : Visibility.Visible;

    public static string Count(int count) => count.ToString();

    public static string Spoken(int count) => count switch
    {
        0 => "Needs you, nothing open",
        1 => "Needs you, 1 item",
        _ => $"Needs you, {count} items"
    };

    public static Style Icon(DecisionKind kind) => (Style)Application.Current.Resources[kind switch
    {
        DecisionKind.InspectionHold => "LockIcon",
        DecisionKind.DriverCheck => "IdCardIcon",
        DecisionKind.LateOrders => "ClockIcon",
        _ => "AnchorIcon"
    }];

    public static Style Action(bool primary) =>
        (Style)Application.Current.Resources[primary ? "QueuePrimaryButton" : "QueueSecondaryButton"];

    /// <summary>The " · " before the live part, only when there is one.</summary>
    public static string Separator(string when) => string.IsNullOrEmpty(when) ? string.Empty : " · ";

    public static Brush WhenInk(bool late) => Tokens.Brush(late ? "AlertInkInvariantBrush" : "TextMutedInvariantBrush");

    public static FontFamily WhenFace(bool late) =>
        (FontFamily)Application.Current.Resources[late ? "BodyStrongFont" : "BodyFont"];
}
