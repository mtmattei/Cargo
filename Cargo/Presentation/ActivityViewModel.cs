using System.Collections.Specialized;

namespace Cargo.Presentation;

public sealed class FeedEntry
{
    public required string Time { get; init; }
    public required string Title { get; init; }
    public required string Detail { get; init; }
    public required Geometry Icon { get; init; }
    public required Brush Tone { get; init; }
    public required Visibility ChipVisibility { get; init; }
}

/// <summary>
/// The shift log, newest first in two columns. An entry about an open decision (its subject named
/// in the entry) carries a NEEDS ACTION chip and the queue's alert icon; resolving the decision
/// drops both. Created on the UI thread by <see cref="OverviewViewModel"/>.
/// </summary>
public sealed partial class ActivityViewModel : ObservableObject
{
    private readonly PortState _state;

    public ActivityViewModel(PortState state)
    {
        _state = state;
        state.Decisions.CollectionChanged += OnQueueChanged;
        Build();
    }

    [ObservableProperty] private IReadOnlyList<FeedEntry> _left = Array.Empty<FeedEntry>();
    [ObservableProperty] private IReadOnlyList<FeedEntry> _right = Array.Empty<FeedEntry>();
    [ObservableProperty] private string _sub = string.Empty;

    public Visibility EmptyVisibility => PortData.Activity.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void OnQueueChanged(object? sender, NotifyCollectionChangedEventArgs e) => Build();

    private void Build()
    {
        var open = _state.Decisions.Where(d => d.Subject is not null).ToArray();
        var feed = PortData.Activity.Select(entry =>
        {
            var about = open.FirstOrDefault(d => entry.Title.Contains(d.Subject!) || entry.Detail.Contains(d.Subject!));
            return new FeedEntry
            {
                Time = entry.Time,
                Title = entry.Title,
                Detail = entry.Detail,
                Icon = (about is null ? null : QueueIcon(about.Kind)) ?? Geo.Path(PortData.ActivityIcons[entry.Kind].Icon),
                Tone = Tokens.Brush(about is null ? "TextMutedInvariantBrush" : "AlertInvariantBrush"),
                ChipVisibility = about is null ? Visibility.Collapsed : Visibility.Visible
            };
        }).ToList();

        // Newest first, down the left column and then the right
        var half = (feed.Count + 1) / 2;
        Left = feed.Take(half).ToArray();
        Right = feed.Skip(half).ToArray();
        var needs = feed.Count(f => f.ChipVisibility == Visibility.Visible);
        Sub = $"{feed.Count} today · {needs} need action · newest first";
    }

    /// <summary>
    /// The Needs-you queue's icon for the kind, read from its style in Themes/Icons.xaml. Uno keeps
    /// a Path.Data setter's value as the path string until the style is applied, so both forms count.
    /// </summary>
    private static Geometry? QueueIcon(DecisionKind kind) =>
        (Application.Current.Resources[NeedsYouPanel.IconKey(kind)] as Style)?.Setters
            .OfType<Setter>().FirstOrDefault(s => s.Property == Microsoft.UI.Xaml.Shapes.Path.DataProperty)?.Value switch
        {
            Geometry geometry => geometry,
            string data => Geo.Path(data),
            _ => null
        };
}
