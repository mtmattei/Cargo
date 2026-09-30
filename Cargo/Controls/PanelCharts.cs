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

    /// <summary>Height of the bar; the ends are fully rounded.</summary>
    public double BarHeight { get; set; } = 14;

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

        Content = new Border { CornerRadius = new CornerRadius(BarHeight / 2), Height = BarHeight, Child = panel };
    }
}

/// <summary>
/// A row of progress ticks, built on first use. Progress moves a tick every few minutes, so
/// a one-second update only restyles when the lit count actually changes. Every fifth lit
/// tick stands taller, so the count reads at a glance.
/// </summary>
// xaml-lint: allow builtin - segmented ticks, every fifth taller; lightweight ProgressBar keys cannot draw that
public sealed partial class TickBar : StackPanel
{
    public static readonly DependencyProperty PercentProperty = DependencyProperty.Register(
        nameof(Percent), typeof(double), typeof(TickBar),
        new PropertyMetadata(0d, (d, _) => ((TickBar)d).Restyle()));

    private int _lit = -1;

    public TickBar()
    {
        Orientation = Orientation.Horizontal;
        VerticalAlignment = VerticalAlignment.Bottom;
        Loaded += (_, _) => Restyle();
    }

    public double Percent
    {
        get => (double)GetValue(PercentProperty);
        set => SetValue(PercentProperty, value);
    }

    /// <summary>Token for the lit ticks, e.g. "SeaGreenInvariantBrush".</summary>
    public string LitToken { get; set; } = "TealInvariantBrush";

    public int Count { get; set; } = 30;

    public double TickWidth { get; set; } = 4;

    public double Gap { get; set; } = 2;

    public double TallHeight { get; set; } = 14;

    public double LitHeight { get; set; } = 10;

    public double OffHeight { get; set; } = 6;

    private void Restyle()
    {
        if (Children.Count != Count)
        {
            Children.Clear();
            _lit = -1;
            for (var i = 0; i < Count; i++)
            {
                Children.Add(new Rectangle
                {
                    RadiusX = 1,
                    RadiusY = 1,
                    Width = TickWidth,
                    Margin = new Thickness(0, 0, Gap, 0),
                    VerticalAlignment = VerticalAlignment.Bottom
                });
            }
        }

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
            tick.Height = on ? i % 5 == 0 ? TallHeight : LitHeight : OffHeight;
            tick.Fill = on ? Tokens.Brush(LitToken) : Tokens.Brush("InkColor", 0.12);
        }
    }
}
