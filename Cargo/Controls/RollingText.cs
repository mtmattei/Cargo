using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;

namespace Cargo.Controls;

/// <summary>
/// Text whose changed characters roll: the old glyph slides up and out, the new one slides in
/// from below (<c>DurationRollMs</c> on <c>EaseSmooth</c>, <c>RollTravelEm</c> of the font size).
/// Each character sits in its own clipped cell holding two text blocks that trade places, so an
/// unchanged character never moves. Meant for tabular (mono) text such as clocks and countdowns.
/// With reduced motion, or when the length changes, the text is swapped in place.
/// </summary>
// xaml-lint: allow builtin - no Toolkit or WCT control rolls digits
public sealed partial class RollingText : ContentControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(RollingText),
        new PropertyMetadata(string.Empty, (d, e) => ((RollingText)d).Apply((string?)e.OldValue, (string?)e.NewValue)));

    private readonly StackPanel _row = new() { Orientation = Orientation.Horizontal };
    private readonly List<Cell> _cells = [];

    public RollingText()
    {
        Content = _row;
        IsTabStop = false;
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private void Apply(string? old, string? text)
    {
        text ??= string.Empty;
        AutomationProperties.SetName(this, text);

        if (_cells.Count != text.Length)
        {
            _row.Children.Clear();
            _cells.Clear();
            foreach (var c in text)
            {
                var cell = new Cell(this, c.ToString());
                _cells.Add(cell);
                _row.Children.Add(cell.Host);
            }

            return;
        }

        var roll = !Motion.Reduced && IsLoaded;
        for (var i = 0; i < text.Length; i++)
        {
            var glyph = text[i].ToString();
            if (_cells[i].Glyph != glyph)
            {
                _cells[i].Show(glyph, roll, FontSize);
            }
        }
    }

    private sealed class Cell
    {
        private TextBlock _front;
        private TextBlock _back;

        public Cell(RollingText owner, string glyph)
        {
            _front = Block(owner, glyph);
            _back = Block(owner, string.Empty);
            Host = new Grid { Children = { _back, _front } };
            Host.SizeChanged += (_, e) =>
                Host.Clip = new RectangleGeometry { Rect = new(0, 0, e.NewSize.Width, e.NewSize.Height) };
        }

        public Grid Host { get; }

        public string Glyph => _front.Text;

        public void Show(string glyph, bool roll, double fontSize)
        {
            if (!roll)
            {
                _front.Text = glyph;
                return;
            }

            (_front, _back) = (_back, _front);
            _front.Text = glyph;

            var travel = (float)(fontSize * Motion.Number("RollTravelEm", .6));
            Slide(_back, 0, -travel, 1, 0);
            Slide(_front, travel, 0, 0, 1);
        }

        private static TextBlock Block(RollingText owner, string text)
        {
            var block = new TextBlock { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
            // Code-built blocks do not pick up the control's text properties, so bind them
            foreach (var (target, path) in new[]
            {
                (TextBlock.ForegroundProperty, nameof(Foreground)),
                (TextBlock.FontFamilyProperty, nameof(FontFamily)),
                (TextBlock.FontSizeProperty, nameof(FontSize)),
                (TextBlock.FontWeightProperty, nameof(FontWeight)),
            })
            {
                block.SetBinding(target, new Binding { Source = owner, Path = new PropertyPath(path) });
            }

            AutomationProperties.SetAccessibilityView(block, AccessibilityView.Raw);
            // Translation has no target property until this is set (xaml-design-polish, Uno Skia)
            ElementCompositionPreview.SetIsTranslationEnabled(block, true);
            return block;
        }

        private static void Slide(UIElement element, float fromY, float toY, float fromOpacity, float toOpacity)
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;
            var ease = Motion.Ease(compositor);
            var duration = Motion.Duration("DurationRollMs");

            var move = compositor.CreateVector3KeyFrameAnimation();
            move.InsertKeyFrame(0, new Vector3(0, fromY, 0));
            move.InsertKeyFrame(1, new Vector3(0, toY, 0), ease);
            move.Duration = duration;

            var fade = compositor.CreateScalarKeyFrameAnimation();
            fade.InsertKeyFrame(0, fromOpacity);
            fade.InsertKeyFrame(1, toOpacity, ease);
            fade.Duration = duration;

            visual.StartAnimation("Translation", move);
            visual.StartAnimation(nameof(Visual.Opacity), fade);
        }
    }
}
