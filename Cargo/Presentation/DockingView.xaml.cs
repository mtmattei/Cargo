using Cargo.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace Cargo.Presentation;

public sealed class PlanRow
{
    public required string Berth { get; init; }
    public required IReadOnlyList<ProportionalPanel.Segment> Segments { get; init; }
    public required double NowFraction { get; init; }
}

public sealed partial class DockingView : UserControl
{
    private const double PlanHours = 36;

    private readonly BerthMap _map;
    private bool _syncingSlider;
    private int _liveMinute = -1;

    public DockingView(PortState state)
    {
        State = state;
        PlanRows = new ObservableCollection<PlanRow>();
        SelectedFacts = new ObservableCollection<Fact>();

        Legend = new[]
        {
            new LegendItem { Label = "Available", Swatch = Tokens.Brush("TealColor", 0.35) },
            new LegendItem { Label = "Reserved", Swatch = Tokens.Brush("AmberDeepBrush") },
            new LegendItem { Label = "Occupied", Swatch = Tokens.Brush("TealBrush") },
            new LegendItem { Label = "Restricted", Swatch = Tokens.Brush("RestrictedBrush") },
            new LegendItem { Label = "Conflict", Swatch = Tokens.Brush("AlertBrush") }
        };

        InitializeComponent();

        _map = new BerthMap(state);
        _map.HintChanged += (_, message) =>
        {
            Hint = message;
            Bindings.Update();
        };
        MapHost.Content = _map;

        Refresh();
        this.RebuildWhenVisible(state, Refresh);
        this.TickWhenVisible(state, () =>
        {
            // Only the live view moves with the clock, and the plan shows minutes, so a
            // refresh on every one-second tick redrew an identical plan 59 times in 60.
            var minute = (int)(State.NowHours * 60);
            if (State.PlanHour is null && minute != _liveMinute)
            {
                _liveMinute = minute;
                Refresh();
            }
        });
    }

    public PortState State { get; }

    public IReadOnlyList<LegendItem> Legend { get; }

    public ObservableCollection<PlanRow> PlanRows { get; }

    public ObservableCollection<Fact> SelectedFacts { get; }

    public string PlanLabel { get; private set; } = string.Empty;
    public string ConflictText { get; private set; } = string.Empty;
    public Brush ConflictTone { get; private set; } = Tokens.Transparent;
    public string Hint { get; private set; } = string.Empty;
    public string SelectedName { get; private set; } = string.Empty;
    public string SelectedStatus { get; private set; } = string.Empty;
    public string SelectedNote { get; private set; } = string.Empty;

    // ── Refresh ───────────────────────────────────────────────────────────────

    private void Refresh()
    {
        var planHour = State.EffectivePlanHour;
        PlanLabel = (planHour >= 24 ? "Thu " : string.Empty) + PortState.Format(planHour % 24);

        _syncingSlider = true;
        Scrubber.Value = Math.Clamp(planHour, 0, PlanHours);
        _syncingSlider = false;

        NowButton.Background = State.PlanHour is null ? Tokens.Brush("InkBrush") : Tokens.Brush("SurfaceBrush");
        NowButton.Foreground = State.PlanHour is null ? Tokens.Brush("PaperBrush") : Tokens.Brush("InkBrush");

        var conflicts = State.Conflicts();
        ConflictText = conflicts.Count == 0
            ? "No conflicts in the next 36 h"
            : $"{conflicts.Count} berth conflict{(conflicts.Count > 1 ? "s" : string.Empty)}: " +
              string.Join(" · ", conflicts.Select(c => $"{c.A} × {c.B} on {c.Berth}"));
        ConflictTone = conflicts.Count == 0 ? Tokens.Brush("SeaGreenBrush") : Tokens.Brush("AlertBrush");

        BuildTimeline(conflicts);
        BuildSelection();

        if (string.IsNullOrEmpty(Hint))
        {
            Hint = "Drag an inbound vessel (dashed outline) onto a berth · scrub the timeline to preview the harbour later today.";
        }

        Bindings.Update();
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
                    Tokens.Brush(conflicted ? "AlertColor" : window.Start > now ? "AmberDeepColor" : "TealColor"),
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
            VesselPanel.Visibility = Visibility.Collapsed;
            PanelColumn.Width = new GridLength(0);
            return;
        }

        var vessel = PortData.Vessel(id);
        var berth = State.BerthOf(id);

        VesselPanel.Visibility = Visibility.Visible;
        PanelColumn.Width = new GridLength(320);

        SelectedName = vessel.Name;
        SelectedStatus = $"{vessel.Operator} · {vessel.StatusLine}";
        SelectedNote = berth is null
            ? "Not yet berthed. Drag onto a green berth, or Optimize."
            : State.IsAssigned(id)
                ? $"Assigned to Berth {berth} · mooring gang notified."
                : vessel.OpsNote;

        SelectedProfile.Source = Sprites.Source(Sprites.Side(vessel.Id));

        ReleaseButton.Visibility = State.IsAssigned(id) ? Visibility.Visible : Visibility.Collapsed;

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

    // ── Interaction ───────────────────────────────────────────────────────────

    private void OnScrubbed(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_syncingSlider)
        {
            return;
        }

        State.PlanHour = e.NewValue;
    }

    private void OnNowClick(object sender, RoutedEventArgs e) => State.PlanHour = null;

    private void OnOptimizeClick(object sender, RoutedEventArgs e)
    {
        Hint = State.Optimize();
        Bindings.Update();
    }

    private void OnClearSelection(object sender, RoutedEventArgs e) => State.DockSelection = null;

    private void OnReleaseClick(object sender, RoutedEventArgs e)
    {
        if (State.DockSelection is { } id)
        {
            State.Unassign(id);
        }
    }

    private void OnOpenProfileClick(object sender, RoutedEventArgs e)
    {
        if (State.DockSelection is { } id)
        {
            State.OpenVesselCommand.Execute(id);
        }
    }
}
