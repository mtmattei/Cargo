using Windows.Foundation;

namespace Cargo.Controls;

/// <summary>Two equal columns, filling left to right — the fact grids in the callouts and panels.</summary>
public partial class TwoColumnPanel : Panel
{
    public double ColumnSpacing { get; set; } = 16;

    public double RowSpacing { get; set; } = 8;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 320 : availableSize.Width;
        var column = Math.Max(0, (width - ColumnSpacing) / 2);
        var childSize = new Size(column, double.PositiveInfinity);

        var height = 0d;
        var rowHeight = 0d;

        for (var i = 0; i < Children.Count; i++)
        {
            Children[i].Measure(childSize);
            rowHeight = Math.Max(rowHeight, Children[i].DesiredSize.Height);

            if (i % 2 == 1 || i == Children.Count - 1)
            {
                height += rowHeight + (i == Children.Count - 1 ? 0 : RowSpacing);
                rowHeight = 0;
            }
        }

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var column = Math.Max(0, (finalSize.Width - ColumnSpacing) / 2);
        var y = 0d;
        var rowHeight = 0d;

        for (var i = 0; i < Children.Count; i++)
        {
            var x = i % 2 == 0 ? 0 : column + ColumnSpacing;
            Children[i].Arrange(new Rect(x, y, column, Children[i].DesiredSize.Height));
            rowHeight = Math.Max(rowHeight, Children[i].DesiredSize.Height);

            if (i % 2 == 1)
            {
                y += rowHeight + RowSpacing;
                rowHeight = 0;
            }
        }

        return finalSize;
    }
}
