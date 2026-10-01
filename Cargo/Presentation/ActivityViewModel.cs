using System.Collections.Specialized;

namespace Cargo.Presentation;

/// <summary>
/// One shift-log row. Clicking it opens its detail (the full line, how long ago, and the open
/// decision it is about, with that decision's action); the other rows in its column blur back.
/// </summary>
public sealed partial class FeedEntry : ObservableObject
{
    public required string Time { get; init; }
    public required string Title { get; init; }
    public required string Detail { get; init; }
    public required Geometry Icon { get; init; }
    public required Brush Tone { get; init; }
    public required Visibility ChipVisibility { get; init; }

    /// <summary>The open decision the entry is about, whose action the detail offers; null when none is.</summary>
    public Decision? About { get; init; }

    public Visibility AboutVisibility => About is null ? Visibility.Collapsed : Visibility.Visible;

    public required ICommand Toggle { get; init; }

    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>Another row in the same column is open.</summary>
    [ObservableProperty]
    private bool _isDimmed;

    /// <summary>"Logged 20:41 · 17 min ago", set when the row opens so it is current.</summary>
    [ObservableProperty]
    private string _logged = string.Empty;
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
        var opened = Left.Concat(Right).FirstOrDefault(f => f.IsExpanded)?.Title;
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
                ChipVisibility = about is null ? Visibility.Collapsed : Visibility.Visible,
                About = about,
                Toggle = ToggleCommand
            };
        }).ToList();

        // Newest first, down the left column and then the right
        var half = (feed.Count + 1) / 2;
        Left = feed.Take(half).ToArray();
        Right = feed.Skip(half).ToArray();
        // A rebuild (a decision resolved) keeps the open row open
        if (feed.FirstOrDefault(f => f.Title == opened) is { } reopen)
        {
            Open(reopen);
        }

        var needs = feed.Count(f => f.ChipVisibility == Visibility.Visible);
        Sub = $"{feed.Count} today · {needs} need action · newest first";
    }

    /// <summary>Opens a row, or closes it when it is already open; one open row at a time.</summary>
    [RelayCommand]
    private void Toggle(FeedEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        if (entry.IsExpanded)
        {
            foreach (var row in Left.Concat(Right))
            {
                row.IsExpanded = row.IsDimmed = false;
            }

            return;
        }

        Open(entry);
    }

    private void Open(FeedEntry entry)
    {
        var column = Left.Contains(entry) ? Left : Right;
        foreach (var row in Left.Concat(Right))
        {
            row.IsExpanded = row == entry;
            row.IsDimmed = row != entry && column.Contains(row);
        }

        entry.Logged = $"Logged {entry.Time} · {Ago(entry.Time)}";
    }

    private string Ago(string time)
    {
        if (!TimeSpan.TryParse(time, out var at))
        {
            return string.Empty;
        }

        var minutes = (int)Math.Round((_state.NowHours - at.TotalHours) * 60);
        return minutes < 1 ? "just now" : minutes < 60 ? $"{minutes} min ago" : $"{minutes / 60} h {minutes % 60} min ago";
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
