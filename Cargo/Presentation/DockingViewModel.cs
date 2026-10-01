using Cargo.Controls;
using Microsoft.UI.Xaml.Media;

namespace Cargo.Presentation;

public sealed class PlanRow
{
    public required string Berth { get; init; }
    public required IReadOnlyList<ProportionalPanel.Segment> Segments { get; init; }
    public required double NowFraction { get; init; }
}

/// <summary>
/// The berth plan: the timeline, the scrubber over the next 36 hours, conflicts, and the
/// selected vessel's panel. <see cref="Refresh"/> follows structural changes; <see cref="Tick"/>
/// follows the clock. Created by <see cref="BerthsViewModel"/> on the UI thread.
/// </summary>
public sealed partial class DockingViewModel : ObservableObject
{
    public const double PlanHours = 36;

    private const string DefaultHint =
        "Drag an inbound vessel (dashed outline) onto a berth · scrub the timeline to preview the harbour later today.";

    private int _liveMinute = -1;

    public DockingViewModel(PortState state)
    {
        State = state;
        Legend = new[]
        {
            new LegendItem { Label = "Available", Swatch = Tokens.Brush("AccentColor", 0.35) },
            new LegendItem { Label = "Reserved", Swatch = Tokens.Brush("AccentInvariantBrush") },
            new LegendItem { Label = "Occupied", Swatch = Tokens.Brush("AccentInvariantBrush") },
            new LegendItem { Label = "Restricted", Swatch = Tokens.Brush("RestrictedInvariantBrush") },
            new LegendItem { Label = "Conflict", Swatch = Tokens.Brush("AlertInvariantBrush") }
        };

        Refresh();
    }

    public PortState State { get; }

    public IReadOnlyList<LegendItem> Legend { get; }

    public ObservableCollection<PlanRow> PlanRows { get; } = new();

    public ObservableCollection<Fact> SelectedFacts { get; } = new();

    [ObservableProperty] private string _planLabel = string.Empty;
    [ObservableProperty] private string _conflictText = string.Empty;
    [ObservableProperty] private Brush? _conflictTone;
    [ObservableProperty] private string _hint = DefaultHint;
    [ObservableProperty] private string _selectedName = string.Empty;
    [ObservableProperty] private string _selectedStatus = string.Empty;
    [ObservableProperty] private string _selectedNote = string.Empty;
    [ObservableProperty] private ImageSource? _selectedProfile;
    [ObservableProperty] private bool _canRelease;
    [ObservableProperty] private Brush? _nowBackground;
    [ObservableProperty] private Brush? _nowForeground;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PanelWidth))]
    private bool _hasSelection;

    /// <summary>The selected vessel's panel takes a column only while something is selected.</summary>
    public GridLength PanelWidth => HasSelection ? new GridLength(320) : new GridLength(0);

    /// <summary>
    /// The scrubber position in hours. A value equal to the time already shown is ignored, so
    /// the slider following the live clock never pins the plan to a fixed hour.
    /// </summary>
    public double Scrub
    {
        get => Math.Clamp(State.EffectivePlanHour, 0, PlanHours);
        set
        {
            if (Math.Abs(value - Scrub) > 0.01)
            {
                State.PlanHour = value;
            }
        }
    }

    [RelayCommand]
    private void Now() => State.PlanHour = null;

    [RelayCommand]
    private void Optimize() => Hint = State.Optimize();

    [RelayCommand]
    private void ClearSelection() => State.DockSelection = null;

    [RelayCommand]
    private void Release()
    {
        if (State.DockSelection is { } id)
        {
            State.Unassign(id);
        }
    }

    [RelayCommand]
    private void OpenProfile()
    {
        if (State.DockSelection is { } id)
        {
            State.OpenVesselCommand.Execute(id);
        }
    }

    /// <summary>Only the live view moves with the clock, and the plan shows minutes, so this
    /// refreshes once a minute rather than on every one-second tick.</summary>
    public void Tick()
    {
        var minute = (int)(State.NowHours * 60);
        if (State.PlanHour is null && minute != _liveMinute)
        {
            _liveMinute = minute;
            Refresh();
        }
    }

    public void Refresh()
    {
        var planHour = State.EffectivePlanHour;
        PlanLabel = (planHour >= 24 ? "Thu " : string.Empty) + PortState.Format(planHour % 24);
        OnPropertyChanged(nameof(Scrub));

        NowBackground = State.PlanHour is null ? Tokens.Brush("InkInvariantBrush") : Tokens.Brush("SurfaceInvariantBrush");
        NowForeground = State.PlanHour is null ? Tokens.Brush("PaperInvariantBrush") : Tokens.Brush("InkInvariantBrush");

        var conflicts = State.Conflicts();
        ConflictText = conflicts.Count == 0
            ? "No conflicts in the next 36 h"
            : $"{conflicts.Count} berth conflict{(conflicts.Count > 1 ? "s" : string.Empty)}: " +
              string.Join(" · ", conflicts.Select(c => $"{c.A} × {c.B} on {c.Berth}"));
        ConflictTone = conflicts.Count == 0 ? Tokens.Brush("SeaGreenInvariantBrush") : Tokens.Brush("AlertInvariantBrush");

        BuildTimeline(conflicts);
        BuildSelection();

        if (string.IsNullOrEmpty(Hint))
        {
            Hint = DefaultHint;
        }
    }

    private void BuildTimeline(IReadOnlyList<(string Berth, string A, string B)> conflicts)
    {
        var now = State.NowHours;
        var nowFraction = Math.Clamp(State.EffectivePlanHour / PlanHours, 0, 1);

        PlanRows.Clear();
        foreach (var berth in PortData.Berths)
        {
            var segments = new List<ProportionalPanel.Segment>();

            // What was here earlier today, and any planned closure
            if (PortData.BerthHistory.TryGetValue(berth.Number, out var history))
            {
                foreach (var segment in history.Where(s => s.Kind is "prev" or "restricted"))
                {
                    segments.Add(new ProportionalPanel.Segment(
                        segment.Label,
                        segment.Start / PlanHours,
                        (Math.Min(segment.End, PlanHours) - segment.Start) / PlanHours,
                        Tokens.Brush(segment.Kind == "prev" ? "TextFaintColor" : "RestrictedColor"),
                        segment.Kind == "prev" ? 0.45 : 1));
                }
            }

            // Live assignments, which move as vessels are dragged around
            foreach (var vessel in PortData.Vessels.Where(v => State.BerthOf(v.Id) == berth.Number))
            {
                var window = PortData.Schedule[vessel.Id];
                var start = Math.Max(0, window.Start);
                var end = Math.Min(PlanHours, window.End);
                if (end <= start)
                {
                    continue;
                }

                var conflicted = conflicts.Any(c => c.Berth == berth.Number && (c.A == vessel.Name || c.B == vessel.Name));
                segments.Add(new ProportionalPanel.Segment(
                    vessel.Name,
                    start / PlanHours,
                    (end - start) / PlanHours,
                    Tokens.Brush(conflicted ? "AlertColor" : window.Start > now ? "AccentColor" : "NavyColor"),
                    conflicted ? 0.9 : window.Start > now ? 0.75 : 1));
            }

            PlanRows.Add(new PlanRow
            {
                Berth = berth.Number,
                Segments = segments,
                NowFraction = nowFraction
            });
        }
    }

    private void BuildSelection()
    {
        if (State.DockSelection is not { } id)
        {
            HasSelection = false;
            return;
        }

        var vessel = PortData.Vessel(id);
        var berth = State.BerthOf(id);

        HasSelection = true;
        SelectedName = vessel.Name;
        SelectedStatus = $"{vessel.Operator} · {vessel.StatusLine}";
        SelectedNote = berth is null
            ? "Not yet berthed. Drag onto a green berth, or Optimize."
            : State.IsAssigned(id)
                ? $"Assigned to Berth {berth} · mooring gang notified."
                : vessel.OpsNote;
        SelectedProfile = Sprites.Source(Sprites.Side(vessel.Id));
        CanRelease = State.IsAssigned(id);

        SelectedFacts.Clear();
        foreach (var (key, value) in new[]
                 {
                     ("ETA", vessel.Eta),
                     ("Berth", berth is null ? "Unassigned" : $"Berth {berth}"),
                     ("Length", $"{vessel.Length} m"),
                     ("Draft", $"{vessel.Draft} m"),
                     ("Cargo", $"{vessel.Containers:N0} TEU"),
                     ("Security status", vessel.Security)
                 })
        {
            SelectedFacts.Add(new Fact { Key = key, Value = value });
        }
    }
}
