using System.Windows.Input;
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
    public required string Index { get; init; }
    public required string Label { get; init; }
    public required ICommand Command { get; init; }

    [ObservableProperty]
    private string _tooltip = string.Empty;

    [ObservableProperty]
    private Brush? _chipBackground;

    [ObservableProperty]
    private Brush? _chipForeground;

    [ObservableProperty]
    private Brush? _labelForeground;

    [ObservableProperty]
    private Brush? _badgeBrush;

    [ObservableProperty]
    private double _badgeOpacity;

    [ObservableProperty]
    private double _underlineOpacity;

    [ObservableProperty]
    private Microsoft.UI.Xaml.Visibility _labelVisibility;
}

/// <summary>
/// The tide badge in the header. It is the only thing on the shell that changes on the
/// clock, so it notifies for itself rather than having the tick re-evaluate every binding
/// on the page.
/// </summary>
public sealed partial class TideReadout : ObservableObject
{
    [ObservableProperty]
    private Geometry? _spark;

    [ObservableProperty]
    private string _label = string.Empty;

    [ObservableProperty]
    private string _tooltip = string.Empty;
}

public sealed class LayerItem
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required Geometry Icon { get; init; }
    public required Brush Background { get; init; }
    public required Brush Foreground { get; init; }
    public required ICommand Command { get; init; }
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
    public bool Outline { get; init; }
    public bool Round { get; init; }
}

public sealed class CompositionSlice
{
    public required string Label { get; init; }
    public required Brush Fill { get; init; }
    public string Count { get; init; } = string.Empty;
}
