using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Cargo.Controls;

/// <summary>A donut of weighted slices (<see cref="Draw.Donut"/>), redrawn when the slices change.</summary>
public sealed partial class DonutChart : ContentControl
{
    public static readonly DependencyProperty SlicesProperty = DependencyProperty.Register(
        nameof(Slices), typeof(IReadOnlyList<(double Weight, Brush Fill)>), typeof(DonutChart),
        new PropertyMetadata(null, (d, _) => ((DonutChart)d).Redraw()));

    public IReadOnlyList<(double Weight, Brush Fill)>? Slices
    {
        get => (IReadOnlyList<(double Weight, Brush Fill)>?)GetValue(SlicesProperty);
        set => SetValue(SlicesProperty, value);
    }

    public double Radius { get; set; } = 36;

    public double Thickness { get; set; } = 16;

    private void Redraw() => Content = Slices is { Count: > 0 } slices ? Draw.Donut(slices, Radius, Thickness) : null;
}

/// <summary>A rounded bar split into proportional blocks, one per slice.</summary>
// xaml-lint: allow builtin - a stacked bar of several cargo classes; ProgressBar holds one value
public sealed partial class CompositionBar : ContentControl
{
    public static readonly DependencyProperty SlicesProperty = DependencyProperty.Register(
        nameof(Slices), typeof(IReadOnlyList<(double Weight, Brush Fill)>), typeof(CompositionBar),
        new PropertyMetadata(null, (d, _) => ((CompositionBar)d).Redraw()));

    public CompositionBar()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
    }

    public IReadOnlyList<(double Weight, Brush Fill)>? Slices
    {
        get => (IReadOnlyList<(double Weight, Brush Fill)>?)GetValue(SlicesProperty);
        set => SetValue(SlicesProperty, value);
    }

    private void Redraw()
    {
        var panel = new Grid { ColumnSpacing = 2 };
        foreach (var (weight, fill) in Slices ?? [])
        {
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(weight, GridUnitType.Star) });
            var block = new Rectangle { Fill = fill };
            Grid.SetColumn(block, panel.ColumnDefinitions.Count - 1);
            panel.Children.Add(block);
        }

        Content = new Border { CornerRadius = new CornerRadius(7), Height = 14, Child = panel };
    }
}

/// <summary>
/// Thirty progress ticks, built once. Progress moves a tick every few minutes, so a
/// one-second update only restyles when the lit count actually changes.
/// </summary>
// xaml-lint: allow builtin - 30 segmented ticks, every fifth taller; lightweight ProgressBar keys cannot draw that
public sealed partial class TickBar : StackPanel
{
    private const int Count = 30;

    public static readonly DependencyProperty PercentProperty = DependencyProperty.Register(
        nameof(Percent), typeof(double), typeof(TickBar),
        new PropertyMetadata(0d, (d, _) => ((TickBar)d).Restyle()));

    private int _lit = -1;

    public TickBar()
    {
        Orientation = Orientation.Horizontal;
        VerticalAlignment = VerticalAlignment.Bottom;
        for (var i = 0; i < Count; i++)
        {
            Children.Add(new Rectangle
            {
                RadiusX = 1,
                RadiusY = 1,
                Width = 4,
                Margin = new Thickness(0, 0, 2, 0),
                VerticalAlignment = VerticalAlignment.Bottom
            });
        }
    }

    public double Percent
    {
        get => (double)GetValue(PercentProperty);
        set => SetValue(PercentProperty, value);
    }

    /// <summary>Token for the lit ticks, e.g. "SeaGreenBrush".</summary>
    public string LitToken { get; set; } = "TealBrush";

    private void Restyle()
    {
        var lit = Enumerable.Range(0, Count).Count(i => i / (double)Count * 100 < Percent);
        if (lit == _lit)
        {
            return;
        }

        _lit = lit;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(this, $"{Percent:0} percent");
        for (var i = 0; i < Count; i++)
        {
            var on = i < lit;
            var tick = (Rectangle)Children[i];
            tick.Height = on ? i % 5 == 0 ? 14 : 10 : 6;
            tick.Fill = on ? Tokens.Brush(LitToken) : Tokens.Brush("InkColor", 0.12);
        }
    }
}
