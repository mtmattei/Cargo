using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Cargo.Presentation;

public sealed class LockingRow
{
    public required string Time { get; init; }
    public required string Lock { get; init; }
    public required string Vessel { get; init; }
    public required string State { get; init; }
    public required Brush Tone { get; init; }
    public required Brush Ink { get; init; }
}

public sealed class GaugeRow
{
    public required string Name { get; init; }
    public required string Chainage { get; init; }
    public required string Level { get; init; }
    public required Geometry Spark { get; init; }
    public required Geometry SparkArea { get; init; }
    public required Brush Ink { get; init; }
}

public sealed class NoticeRow
{
    public required string Title { get; init; }
    public required string Detail { get; init; }
    public required Brush Tone { get; init; }
}

public sealed class FleetRow
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Detail { get; init; }
    public required string Eta { get; init; }
    public required string Slip { get; init; }
    public required Brush Tone { get; init; }
    public required Brush SlipInk { get; init; }
    public required Brush Background { get; init; }
    public required ICommand Select { get; init; }
}

public sealed class ChatBubble
{
    public required string Text { get; init; }
    public required string Meta { get; init; }
    public required HorizontalAlignment Align { get; init; }
    public required Brush Background { get; init; }
    public required Brush Foreground { get; init; }
}

public sealed class QuickMessage
{
    public required string Label { get; init; }
    public required string Text { get; init; }
    public required ICommand Send { get; init; }
}

/// <summary>
/// Waterways: the river fleet, lockings, gauges, notices and the crew chat for the selected
/// barge. Everything on the page follows <see cref="PortState.FleetSelection"/> and the messages
/// this page sends, so it refreshes on those two and ignores the rest of the port.
/// </summary>
public sealed partial class FleetViewModel : ObservableObject
{
    public FleetViewModel(PortState state, IDispatcher dispatcher)
    {
        State = state;

        // The router builds models off the UI thread; the rows carry brushes and geometries,
        // which must be created on it.
        dispatcher.TryEnqueue(() =>
        {
            BuildStatic();
            Refresh();
            state.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PortState.FleetSelection))
                {
                    Refresh();
                }
            };
        });
    }

    public PortState State { get; }

    [ObservableProperty]
    private IReadOnlyList<LegendItem> _legend = [];

    [ObservableProperty]
    private IReadOnlyList<LockingRow> _lockings = [];

    [ObservableProperty]
    private IReadOnlyList<GaugeRow> _gauges = [];

    [ObservableProperty]
    private IReadOnlyList<NoticeRow> _notices = [];

    public ObservableCollection<FleetRow> Fleet { get; } = new();
    public ObservableCollection<ChatBubble> Thread { get; } = new();
    public ObservableCollection<QuickMessage> QuickMessages { get; } = new();

    public string BookedLabel => $"Booked passages · now {PortState.Format(PortData.NowHours)}";
    public string NoticeCount => $"{PortData.Notices.Count} active";
    public string FleetCount => $"{PortData.RiverFleet.Count} vessels";

    // Masthead figures
    public string UnderwayCount => PortData.RiverFleet.Count.ToString();
    public string LateCount => PortData.RiverFleet.Count(v => v.SlipMinutes > 0).ToString();
    public string LockCount => PortData.Lockings.Count.ToString();
    public string NoticeTotal => PortData.Notices.Count.ToString();

    [ObservableProperty]
    private string _crewHeading = string.Empty;

    [ObservableProperty]
    private string _crewSubtitle = string.Empty;

    [ObservableProperty]
    private string _slipLabel = string.Empty;

    [ObservableProperty]
    private Brush? _slipTone;

    [ObservableProperty]
    private Brush? _slipInk;

    private RiverVessel Selected =>
        PortData.RiverFleet.FirstOrDefault(v => v.Id == State.FleetSelection) ?? PortData.RiverFleet[0];

    [RelayCommand]
    private void Select(string id) => State.FleetSelection = id;

    [RelayCommand]
    private void SendDraft() => SendToSelected(State.ChatDraft);

    [RelayCommand]
    private void SendQuick(string text) => SendToSelected(text);

    private void SendToSelected(string text)
    {
        State.Send(Selected.Id, text);
        Refresh();
    }

    /// <summary>Status colours are fills; the text beside them takes the darker ink variant.</summary>
    private static string InkFor(string tone) => tone switch
    {
        var t when t.StartsWith("Orange") => "OrangeInkInvariantBrush",
        var t when t.StartsWith("Amber") => "AmberInkInvariantBrush",
        var t when t.StartsWith("SeaGreen") => "SeaGreenInkInvariantBrush",
        var t when t.StartsWith("Accent") => "AccentDarkInvariantBrush",
        var t when t.StartsWith("Alert") => "AlertInkInvariantBrush",
        _ => "InkInvariantBrush"
    };

    /// <summary>
    /// A 12-point trace that drifts with the gauge's trend arrow, as the line and the area under
    /// it, both in the gauge's real 80 x 22 frame so they register without stretching.
    /// </summary>
    private static (Geometry Line, Geometry Area) Spark(GaugeDef gauge, int index)
    {
        var drift = gauge.Trend == "↓" ? -0.02 : gauge.Trend == "↑" ? 0.02 : 0;
        var values = Enumerable.Range(0, 12)
            .Select(j => gauge.Level + Math.Sin(j * 0.9 + index) * 0.12 + j * drift)
            .ToList();

        var min = values.Min();
        var max = values.Max();
        var span = max - min;
        if (span <= 0)
        {
            span = 1;
        }

        const double width = 80, height = 22, pad = 2;
        var points = values
            .Select((v, j) => new Point(j * width / 11d, height - pad - (v - min) / span * (height - pad * 2 - 2)))
            .ToList();
        var area = points.Prepend(new Point(0, height)).Append(new Point(width, height)).ToList();
        return (Geo.Polyline(points), Geo.Polyline(area, close: true));
    }

    private void BuildStatic()
    {
        Legend = new[]
        {
            new LegendItem { Label = "On time", Swatch = Tokens.Brush("SeaGreenInvariantBrush") },
            new LegendItem { Label = "Running late", Swatch = Tokens.Brush("OrangeInvariantBrush") },
            new LegendItem { Label = "Lock", Swatch = Tokens.Brush("InkInvariantBrush") },
            new LegendItem { Label = "Warning", Swatch = Tokens.Brush("AlertInvariantBrush") }
        };

        Lockings = PortData.Lockings.Select(l =>
        {
            var (tone, ink) = l.State switch
            {
                "Done" => ("TextFaintInvariantBrush", "TextMutedInvariantBrush"),
                "Queued" or "At risk" => ("OrangeInvariantBrush", "OrangeInkInvariantBrush"),
                "After maintenance" => ("AlertInvariantBrush", "AlertInvariantBrush"),
                _ => ("SeaGreenInvariantBrush", "SeaGreenInkInvariantBrush")
            };

            return new LockingRow
            {
                Time = l.Time,
                Lock = l.Lock,
                Vessel = l.Vessel,
                State = l.State,
                Tone = Tokens.Brush(tone),
                Ink = Tokens.Brush(ink)
            };
        }).ToList();

        // Every trace is water, in the river's colour; the gauge's status stays on its reading
        Gauges = PortData.Gauges.Select((g, i) =>
        {
            var (line, area) = Spark(g, i);
            return new GaugeRow
            {
                Name = g.Name,
                Chainage = $"km {g.Km:0}",
                Level = $"{g.Level:0.0} m {g.Trend}",
                Spark = line,
                SparkArea = area,
                Ink = Tokens.Brush(InkFor(g.Tone))
            };
        }).ToList();

        Notices = PortData.Notices.Select(n => new NoticeRow
        {
            Title = n.Title,
            Detail = n.Detail,
            Tone = Tokens.Brush(n.Tone)
        }).ToList();
    }

    private void Refresh()
    {
        var selected = Selected;

        Fleet.Clear();
        foreach (var vessel in PortData.RiverFleet)
        {
            var late = vessel.SlipMinutes > 0;
            var current = vessel.Id == selected.Id;

            Fleet.Add(new FleetRow
            {
                Id = vessel.Id,
                Name = vessel.Name,
                Detail = $"Order {vessel.Order} · km {vessel.Km:0.0} · {vessel.Speed} kn",
                Eta = $"ETA {PortState.Format(vessel.Eta + vessel.SlipMinutes / 60d)}",
                Slip = late ? $"+{vessel.SlipMinutes} min" : "On time",
                Tone = Tokens.Brush(late ? "OrangeInvariantBrush" : "SeaGreenInvariantBrush"),
                SlipInk = Tokens.Brush(late ? "OrangeInkInvariantBrush" : "SeaGreenInkInvariantBrush"),
                Background = current ? Tokens.Brush("SurfaceSunkAltInvariantBrush") : Tokens.Transparent,
                Select = SelectCommand
            });
        }

        var isLate = selected.SlipMinutes > 0;
        CrewHeading = $"{selected.Name} · crew";
        CrewSubtitle = $"{selected.Master} · VHF 12 · " +
                       (selected.Direction > 0 ? "upbound · " : "downbound · ") + selected.Next;
        SlipLabel = isLate ? $"+{selected.SlipMinutes} min" : "On time";
        SlipTone = Tokens.Brush(isLate ? "OrangeInvariantBrush" : "SeaGreenInvariantBrush");
        SlipInk = Tokens.Brush(isLate ? "OrangeInkInvariantBrush" : "SeaGreenInkInvariantBrush");

        Thread.Clear();
        foreach (var line in State.ThreadFor(selected.Id))
        {
            var fromCrew = line.Who == "crew";
            Thread.Add(new ChatBubble
            {
                Text = line.Text,
                Meta = $"{(fromCrew ? selected.Master.Split(' ').Last() : "Dispatch")} · {PortState.Format(line.Hour)}",
                Align = fromCrew ? HorizontalAlignment.Left : HorizontalAlignment.Right,
                Background = fromCrew ? Tokens.Brush("SurfaceSunkAltInvariantBrush") : Tokens.Brush("InkInvariantBrush"),
                Foreground = fromCrew ? Tokens.Brush("InkInvariantBrush") : Tokens.Brush("PaperInvariantBrush")
            });
        }

        QuickMessages.Clear();
        foreach (var (label, text) in new[]
                 {
                     ("Confirm new ETA",
                         $"Please confirm revised ETA {PortState.Format(selected.Eta + selected.SlipMinutes / 60d)} " +
                         "to the consignee. Any further slip, call dispatch on VHF 12."),
                     ("Hold at next lock", $"Hold at {selected.Next} until further notice — traffic ahead. Acknowledge."),
                     ("Report water level", $"Report observed water level and any squat at your position, km {selected.Km:0.0}.")
                 })
        {
            QuickMessages.Add(new QuickMessage { Label = label, Text = text, Send = SendQuickCommand });
        }
    }
}
