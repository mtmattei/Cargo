using Cargo.Controls;
using Microsoft.UI.Xaml.Media;

namespace Cargo.Presentation;

public sealed class FeedEntry
{
    public required string Time { get; init; }
    public required string Title { get; init; }
    public required string Detail { get; init; }
    public required Geometry Icon { get; init; }
    public required Brush Tone { get; init; }
}

public sealed partial class ActivityView : UserControl
{
    public ActivityView(PortState state)
    {
        State = state;

        Feed = PortData.Activity.Select(entry =>
        {
            var (icon, tone) = PortData.ActivityIcons[entry.Kind];
            return new FeedEntry
            {
                Time = entry.Time,
                Title = entry.Title,
                Detail = entry.Detail,
                Icon = Geo.Path(icon),
                Tone = Tokens.Brush(tone)
            };
        }).ToList();

        InitializeComponent();
        BuildThroughput();
    }

    public PortState State { get; }

    public string Eyebrow => $"ACTIVITY · {PortData.Today.ToUpperInvariant()}";

    public IReadOnlyList<FeedEntry> Feed { get; }

    private void BuildThroughput()
    {
        var host = new SceneHost(1000, 120) { Overflow = 24 };
        var scene = host.Scene;

        var series = Geo.MakeSeries(PortData.Volume, 1000, 100, 14);

        scene.Place(new Microsoft.UI.Xaml.Shapes.Path { Data = series.Area, Fill = Tokens.Brush("TealColor", 0.1) });
        scene.Place(new Microsoft.UI.Xaml.Shapes.Path
        {
            Data = series.Stroke,
            Stroke = Tokens.Brush("TealBrush"),
            StrokeThickness = 2
        });

        scene.Place(Draw.Rule(0, 100, 1000, 100, Tokens.Brush("InkColor", 0.1)));
        scene.Place(Draw.Rule(874, 8, 874, 100, Tokens.Brush("InkBrush"), 1, dash: Draw.Dash(3, 3)));
        scene.Place(Draw.Label("184 now", 874, -14, 13, Tokens.Brush("InkBrush"), "BodyStrongFont",
            TextAlignment.Center, 160));

        foreach (var (label, x, alignment) in new[]
                 {
                     ("00:00", 0d, TextAlignment.Left),
                     ("06:00", 250d, TextAlignment.Center),
                     ("12:00", 500d, TextAlignment.Center),
                     ("18:00", 750d, TextAlignment.Center),
                     ("24:00", 1000d, TextAlignment.Right)
                 })
        {
            scene.Place(Draw.Label(label, x, 106, 14, Tokens.Brush("TextFaintBrush"), "BodyFont", alignment, 120));
        }

        ThroughputHost.Content = host;
    }
}
