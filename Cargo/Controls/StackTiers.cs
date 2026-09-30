namespace Cargo.Controls;

/// <summary>The selected yard slot drawn as an isometric pile, one box per tier.</summary>
public sealed partial class StackTiers : SceneHost
{
    public static readonly DependencyProperty BoxesProperty = DependencyProperty.Register(
        nameof(Boxes), typeof(IReadOnlyList<(string Id, string Tag, string Token)>), typeof(StackTiers),
        new PropertyMetadata(null, (d, e) =>
            ((StackTiers)d).Show(e.NewValue as IReadOnlyList<(string Id, string Tag, string Token)> ?? [])));

    public StackTiers() : base(300, 200)
    {
    }

    /// <summary>The boxes in the pile, bottom tier first.</summary>
    public IReadOnlyList<(string Id, string Tag, string Token)>? Boxes
    {
        get => (IReadOnlyList<(string Id, string Tag, string Token)>?)GetValue(BoxesProperty);
        set => SetValue(BoxesProperty, value);
    }

    private void Show(IReadOnlyList<(string Id, string Tag, string Token)> tiers)
    {
        Scene.Children.Clear();
        Scene.Place(Draw.Rule(60, 191, 300, 191, Tokens.Brush("InkColor", 0.15)));

        for (var i = 0; i < tiers.Count; i++)
        {
            var (id, tag, token) = tiers[i];
            var color = Tokens.Color(token);
            var top = 160 - i * 30;

            Scene.Place(Draw.Rect(90, top, 170, 30, Tokens.Of(color)));
            Scene.Place(Draw.Shape($"M90 {top} l22 -12 h170 l-22 12 z", Tokens.Of(Tokens.Shade(color, 30))));
            Scene.Place(Draw.Shape($"M260 {top} l22 -12 v30 l-22 12 z", Tokens.Of(Tokens.Shade(color, -40))));

            var ribs = Tokens.Brush("InkColor", 0.14);
            for (var r = 0; r < 8; r++)
            {
                Scene.Place(Draw.Rule(110 + r * 20, top, 110 + r * 20, top + 30, ribs));
            }

            Scene.Place(Draw.Text(id, 98, top + 8, 10.5, Tokens.Brush("DeckWhiteColor", 0.92), "MonoMediumFont"));
            Scene.Place(Draw.Text(tag, 135, top + 9, 9.5, Tokens.Brush("DeckWhiteColor", 0.8),
                alignment: TextAlignment.Right, width: 120));
        }
    }
}
