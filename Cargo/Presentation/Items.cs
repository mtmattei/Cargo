using Microsoft.UI.Xaml.Media;

namespace Cargo.Presentation;

// Flat, already-resolved shapes for the lists in the UI. Most are rebuilt wholesale when
// the state changes, so they carry brushes rather than raising notifications.

/// <summary>
/// One section in the header. Unlike the other rows here this one is built once and then
/// updated in place: rebuilding the collection on every navigation recreated every
/// button, which is what made moving between sections flicker.
/// </summary>
public sealed partial class NavItem : ObservableObject
{
    public required string Id { get; init; }
    public required string Label { get; init; }

    /// <summary>The section's route inside Main's region ("./berths").</summary>
    public string Route => $"./{Id}";

    [ObservableProperty]
    private string _tooltip = string.Empty;

    /// <summary>The tab's spoken name: the label, plus ", needs you" when the dot shows.</summary>
    [ObservableProperty]
    private string _automationName = string.Empty;

    /// <summary>Muted, or ink on the section you are on (the tab's hover inks it too).</summary>
    [ObservableProperty]
    private Brush? _labelForeground;

    [ObservableProperty]
    private Microsoft.UI.Xaml.Visibility _needsVisibility;

    [ObservableProperty]
    private double _underlineOpacity;

    /// <summary>The semibold label, on the section you are on.</summary>
    [ObservableProperty]
    private Microsoft.UI.Xaml.Visibility _currentVisibility;

    /// <summary>The medium-weight label, on the other sections.</summary>
    [ObservableProperty]
    private Microsoft.UI.Xaml.Visibility _otherVisibility;
}

/// <summary>One segment of the harbour layer switcher; built once, recoloured in place.</summary>
public sealed partial class LayerItem : ObservableObject
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required Geometry Icon { get; init; }
    public required ICommand Command { get; init; }

    [ObservableProperty]
    private Brush? _foreground;

    [ObservableProperty]
    private Brush? _iconForeground;
}

public sealed class Fact
{
    public required string Key { get; init; }
    public required string Value { get; init; }
    public Brush? Tone { get; init; }
}

public sealed class LegendItem
{
    public required string Label { get; init; }
    public required Brush Swatch { get; init; }
}

public sealed class CompositionSlice
{
    public required string Label { get; init; }
    public required Brush Fill { get; init; }
    public string Count { get; init; } = string.Empty;
}
