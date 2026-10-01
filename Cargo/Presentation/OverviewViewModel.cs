namespace Cargo.Presentation;

/// <summary>
/// Overview: the masthead's figures, the day's timeline, the Needs-you queue and Next 6 h. The charts on the page
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
            Timeline = new TimelineViewModel(state);
            NextSix = new NextSixHoursViewModel(state, Timeline.Events);
            Activity = new ActivityViewModel(state);
            Refresh();
            state.Ticked += (_, _) => Refresh();
            state.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PortState.NeedsYouCount))
                {
                    OnPropertyChanged(nameof(NeedsYouText));
                    OnPropertyChanged(nameof(NeedsYouLink));
                }
            };
        });
    }

    public PortState State { get; }

    /// <summary>"Today at North Quay": the shared-axis lanes, the tide lane and the hourly table.</summary>
    [ObservableProperty]
    private TimelineViewModel? _timeline;

    public string Subtitle => $"{PortData.Today} · Westhaven · the harbour as it is right now.";

    /// <summary>The shift log, with the entries an open decision is about marked.</summary>
    [ObservableProperty]
    private ActivityViewModel? _activity;

    /// <summary>Next 6 h: the movements around NOW, ships to scale.</summary>
    [ObservableProperty]
    private NextSixHoursViewModel? _nextSix;

    /// <summary>Hours since midnight on the shift clock, for the charts' now markers.</summary>
    [ObservableProperty]
    private double _nowHours;

    /// <summary>The operator on shift, for the masthead's ID card.</summary>
    public DutyOperator Operator => PortData.Operator;

    public string OperatorSince => $"{Operator.Role} · in {Operator.ClockedInText}";

    public string OperatorUntil => $"until {Operator.ShiftEndText}";

    public string OperatorSummary => $"{Operator.Role} {Operator.Name}, {Operator.Badge}, on shift {Operator.ClockedInText} to {Operator.ShiftEndText}";

    /// <summary>The masthead's needs-you figure, as text for the figure's TextBlock.</summary>
    public string NeedsYouText => State.NeedsYouCount.ToString();

    public string NeedsYouLink => $"{State.NeedsYouCount} need you, show the queue";

    /// <summary>How much of the shift has passed, 0 to 1.</summary>
    [ObservableProperty]
    private double _shiftProgress;

    private void Refresh()
    {
        NowHours = State.NowHours;
        ShiftProgress = Math.Clamp((NowHours - Operator.ClockedIn) / (Operator.ShiftEnd - Operator.ClockedIn), 0, 1);
        Timeline?.Refresh();
        NextSix?.Refresh();
    }
}
