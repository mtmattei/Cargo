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
    public ActivityView()
    {
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
    }

    public string Eyebrow => $"ACTIVITY · {PortData.Today.ToUpperInvariant()}";

    public IReadOnlyList<FeedEntry> Feed { get; }
}
