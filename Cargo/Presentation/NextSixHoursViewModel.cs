using Microsoft.UI.Xaml.Media;

namespace Cargo.Presentation;

/// <summary>One movement in Next 6 h. Built once per membership change; the countdown ticks in place.</summary>
public sealed partial class NextSixRow : ObservableObject
{
    public required string Id { get; init; }
    public required double Hour { get; init; }
    public required string Time { get; init; }
    public required string Name { get; init; }
    public required string Sub { get; init; }
    public required bool Arrival { get; init; }
    public required double Length { get; init; }

    /// <summary>Opens the vessel; hovering or focusing the row lights it on the map and the chart.</summary>
    public required System.Windows.Input.ICommand Open { get; init; }
    public required System.Windows.Input.ICommand Hover { get; init; }

    /// <summary>The linked-hover tint (accent at 7%) while this vessel is lit anywhere.</summary>
    [ObservableProperty] private Brush? _ground;

    [ObservableProperty] private bool _happened;
    [ObservableProperty] private string _countdown = string.Empty;
    [ObservableProperty] private Brush? _countdownInk;
    [ObservableProperty] private Brush? _dotFill;
    [ObservableProperty] private Brush? _dotRing;
    [ObservableProperty] private double _dotRingWidth;
    [ObservableProperty] private bool _departing;
}

/// <summary>
/// Next 6 h: the movements from 15 minutes ago to six hours ahead, each ship drawn to scale. The
/// window reaches back 15 minutes so a ship that has just left stays above the NOW row.
/// </summary>
public sealed partial class NextSixHoursViewModel : ObservableObject
{
    private const double Back = .25, Ahead = 6;

    private readonly PortState _state;
    private readonly IReadOnlyList<TimelineEvent> _events;
    private string _membership = string.Empty;

    public NextSixHoursViewModel(PortState state, IReadOnlyList<TimelineEvent> events)
    {
        _state = state;
        _events = events;
        state.HoverChanged += (_, _) => PaintHover();
        Refresh();
    }

    private void PaintHover()
    {
        foreach (var row in Past.Concat(Upcoming))
        {
            row.Ground = Tokens.Tint("AccentInvariantBrush", row.Id == _state.HoveredVessel ? .07 : 0);
        }
    }

    /// <summary>Movements that have happened inside the window, above the NOW row.</summary>
    public ObservableCollection<NextSixRow> Past { get; } = new();

    /// <summary>Movements to come, below the NOW row.</summary>
    public ObservableCollection<NextSixRow> Upcoming { get; } = new();

    [ObservableProperty] private string _sub = string.Empty;
    [ObservableProperty] private Visibility _emptyVisibility;

    public void Refresh()
    {
        var now = _state.NowHours;
        var inWindow = _events.Where(e => e.Hour >= now - Back && e.Hour <= now + Ahead).ToArray();
        var membership = string.Join("|", inWindow.Select(e => $"{e.Id}{e.Arrival}{e.Hour <= now}"));
        if (membership != _membership)
        {
            _membership = membership;
            Past.Clear();
            Upcoming.Clear();
            foreach (var e in inWindow)
            {
                (e.Hour <= now ? Past : Upcoming).Add(Row(e));
            }

            PaintHover();
        }

        foreach (var row in Past.Concat(Upcoming))
        {
            Tick(row, now);
        }

        var count = inWindow.Length;
        Sub = $"{PortState.Format(now)} → {PortState.Format(now + Ahead)} · {(count == 1 ? "1 movement" : $"{count} movements")} · ships to scale";
        EmptyVisibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private NextSixRow Row(TimelineEvent e)
    {
        var vessel = PortData.Vessels.FirstOrDefault(v => v.Id == e.Id);
        var berth = vessel?.HomeBerth;
        var sub = e.Arrival
            ? $"Arrives {(e.Hour >= 24 ? "Thu" : berth is null ? "today" : $"Berth {berth}")}"
              + (berth is null ? " · no berth yet" : string.Empty)
              + (vessel?.StatusLine.Contains("pilot", StringComparison.OrdinalIgnoreCase) == true ? " · pilot aboard" : string.Empty)
            : $"Departs {(berth is null ? "the quay" : $"Berth {berth}")}";

        return new NextSixRow
        {
            Id = e.Id,
            Hour = e.Hour,
            Time = PortState.Format(e.Hour),
            Name = e.Name,
            Sub = sub,
            Arrival = e.Arrival,
            Length = vessel?.Length ?? 250,
            Open = _state.OpenVesselCommand,
            Hover = _state.HoverCommand
        };
    }

    private static void Tick(NextSixRow row, double now)
    {
        var minutes = (int)Math.Ceiling((row.Hour - now) * 60);
        row.Happened = row.Hour <= now;
        row.Departing = row.Happened && !row.Arrival;
        row.Countdown = row.Happened
            ? (row.Arrival ? "arrived" : "departing")
            : minutes < 60 ? $"in {minutes} min" : $"in {minutes / 60} h {minutes % 60:D2}";
        row.CountdownInk = Tokens.Brush(minutes < 60 ? "InkInvariantBrush" : "TextMutedInvariantBrush");
        row.DotFill = Tokens.Brush(row.Happened ? "NavyInvariantBrush" : "SurfaceInvariantBrush");
        row.DotRing = Tokens.Brush(row.Happened ? "SurfaceInvariantBrush" : "NavyInvariantBrush");
        row.DotRingWidth = row.Happened ? 2 : 1.6;
    }
}
