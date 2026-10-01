using System.Windows.Input;
using Cargo.Controls;
using Microsoft.UI.Xaml.Media;

namespace Cargo.Presentation;

/// <summary>
/// Rows are built once and only the countdown ticks, so the list is never momentarily
/// empty while the clock refreshes.
/// </summary>
public sealed partial class NextUpItem : ObservableObject
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Detail { get; init; }
    public required double Hour { get; init; }
    public required Brush Tone { get; init; }
    public required ImageSource Silhouette { get; init; }
    public required ICommand Open { get; init; }

    [ObservableProperty]
    private string _countdown = string.Empty;
}

/// <summary>
/// Overview: the live figures and the next arrivals and departures. The charts on the page
/// follow <see cref="NowHours"/>; everything else is the day's plan.
/// </summary>
public sealed partial class OverviewViewModel : ObservableObject
{
    public OverviewViewModel(PortState state, IDispatcher dispatcher)
    {
        State = state;

        // The router builds models off the UI thread; the rows carry brushes and images,
        // which must be created on it.
        dispatcher.TryEnqueue(() =>
        {
            BuildNextUp();
            Refresh();
            state.Ticked += (_, _) => Refresh();
        });
    }

    public PortState State { get; }

    public string Subtitle => $"{PortData.Today} · Westhaven · the harbour as it is right now.";

    public ObservableCollection<NextUpItem> NextUp { get; } = new();

    /// <summary>Hours since midnight on the shift clock, for the charts' now markers.</summary>
    [ObservableProperty]
    private double _nowHours;

    [ObservableProperty]
    private string _movesNow = string.Empty;

    private void Refresh()
    {
        NowHours = State.NowHours;
        MovesNow = PortState.MovesAt(NowHours).ToString();

        foreach (var row in NextUp)
        {
            row.Countdown = State.Countdown(row.Hour);
        }
    }

    private void BuildNextUp()
    {
        (string Id, string Name, string Detail, double Hour, string Tone)[] rows =
        {
            ("nordic", "Nordic Star", "Arrival · Berth 07 · 21:40", 21 + 40 / 60d, "AccentInvariantBrush"),
            ("baltic", "Baltic Crown", "Departure · Berth 06 · 21:30", 21.5, "SeaGreenInvariantBrush"),
            ("levant", "Levant Express", "Arrival · Unassigned · Thu 02:30", 26.5, "TextFaintInvariantBrush")
        };

        foreach (var (id, name, detail, hour, tone) in rows)
        {
            NextUp.Add(new NextUpItem
            {
                Id = id,
                Name = name,
                Detail = detail,
                Hour = hour,
                Tone = Tokens.Brush(tone),
                Silhouette = Sprites.Source(Sprites.Side(id)),
                Open = State.OpenVesselCommand
            });
        }
    }
}
