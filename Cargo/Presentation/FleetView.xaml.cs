using System.Windows.Input;
using Cargo.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.System;

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
    public required Brush Tone { get; init; }
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

public sealed partial class FleetView : Page
{
    private bool _attached;

    public FleetView()
    {
        Fleet = new ObservableCollection<FleetRow>();
        Thread = new ObservableCollection<ChatBubble>();
        QuickMessages = new ObservableCollection<QuickMessage>();

        Legend = new[]
        {
            new LegendItem { Label = "On time", Swatch = Tokens.Brush("SeaGreenBrush") },
            new LegendItem { Label = "Running late", Swatch = Tokens.Brush("OrangeBrush") },
            new LegendItem { Label = "Lock", Swatch = Tokens.Brush("InkBrush") },
            new LegendItem { Label = "Warning", Swatch = Tokens.Brush("AlertBrush") }
        };

        Lockings = PortData.Lockings.Select(l =>
        {
            var (tone, ink) = l.State switch
            {
                "Done" => ("TextFaintBrush", "TextMutedBrush"),
                "Queued" or "At risk" => ("OrangeBrush", "OrangeInkBrush"),
                "After maintenance" => ("AlertBrush", "AlertBrush"),
                _ => ("SeaGreenBrush", "SeaGreenInkBrush")
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

        Gauges = PortData.Gauges.Select((g, i) => new GaugeRow
        {
            Name = g.Name,
            Chainage = $"km {g.Km:0}",
            Level = $"{g.Level:0.0} m {g.Trend}",
            Spark = Spark(g, i),
            Tone = Tokens.Brush(g.Tone),
            Ink = Tokens.Brush(InkFor(g.Tone))
        }).ToList();

        Notices = PortData.Notices.Select(n => new NoticeRow
        {
            Title = n.Title,
            Detail = n.Detail,
            Tone = Tokens.Brush(n.Tone)
        }).ToList();

        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
    }

    /// <summary>The router builds the page, then hands it its model; the live work starts there.</summary>
    private void Attach()
    {
        if (_attached || DataContext is not FleetViewModel vm)
        {
            return;
        }

        _attached = true;
        State = vm.State;
        var state = vm.State;

        RiverHost.Content = new RiverMap(state);

        Refresh();
        this.RebuildWhenVisible(state, Refresh);
        Bindings.Update();
    }

    public PortState State { get; private set; } = null!;

    public IReadOnlyList<LegendItem> Legend { get; }
    public IReadOnlyList<LockingRow> Lockings { get; }
    public IReadOnlyList<GaugeRow> Gauges { get; }
    public IReadOnlyList<NoticeRow> Notices { get; }

    public ObservableCollection<FleetRow> Fleet { get; }
    public ObservableCollection<ChatBubble> Thread { get; }
    public ObservableCollection<QuickMessage> QuickMessages { get; }

    public string BookedLabel => $"Booked passages · now {PortState.Format(PortData.NowHours)}";
    public string NoticeCount => $"{PortData.Notices.Count} active";
    public string FleetCount => $"{PortData.RiverFleet.Count} vessels";

    // Masthead figures
    public string UnderwayCount => PortData.RiverFleet.Count.ToString();
    public string LateCount => PortData.RiverFleet.Count(v => v.SlipMinutes > 0).ToString();
    public string LockCount => PortData.Lockings.Count.ToString();
    public string NoticeTotal => PortData.Notices.Count.ToString();

    public Brush SlipInk { get; private set; } = Tokens.Transparent;

    /// <summary>Status colours are fills; the text beside them takes the darker ink variant.</summary>
    private static string InkFor(string tone) => tone switch
    {
        var t when t.StartsWith("Orange") => "OrangeInkBrush",
        var t when t.StartsWith("Amber") => "AmberInkBrush",
        var t when t.StartsWith("SeaGreen") || t.StartsWith("Teal") => "SeaGreenInkBrush",
        var t when t.StartsWith("Alert") => "AlertBrush",
        _ => "InkBrush"
    };

    public string CrewHeading { get; private set; } = string.Empty;
    public string CrewSubtitle { get; private set; } = string.Empty;
    public string SlipLabel { get; private set; } = string.Empty;
    public Brush SlipTone { get; private set; } = Tokens.Transparent;

    /// <summary>A 12-point trace that drifts with the gauge's trend arrow.</summary>
    private static Geometry Spark(GaugeDef gauge, int index)
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

        return Geo.Polyline(values
            .Select((v, j) => new Point(j * 90 / 11d, 20 - (v - min) / span * 16))
            .ToList());
    }

    private void Refresh()
    {
        var selected = PortData.RiverFleet.FirstOrDefault(v => v.Id == State.FleetSelection)
                       ?? PortData.RiverFleet[0];

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
                Tone = Tokens.Brush(late ? "OrangeBrush" : "SeaGreenBrush"),
                SlipInk = Tokens.Brush(late ? "OrangeInkBrush" : "SeaGreenInkBrush"),
                Background = current ? Tokens.Brush("SurfaceSunkAltBrush") : Tokens.Transparent,
                Select = new RelayCommand<string>(id => State.FleetSelection = id ?? selected.Id)
            });
        }

        var isLate = selected.SlipMinutes > 0;
        CrewHeading = $"{selected.Name} · crew";
        CrewSubtitle = $"{selected.Master} · VHF 12 · " +
                       (selected.Direction > 0 ? "upbound · " : "downbound · ") + selected.Next;
        SlipLabel = isLate ? $"+{selected.SlipMinutes} min" : "On time";
        SlipTone = Tokens.Brush(isLate ? "OrangeBrush" : "SeaGreenBrush");
        SlipInk = Tokens.Brush(isLate ? "OrangeInkBrush" : "SeaGreenInkBrush");

        Thread.Clear();
        foreach (var line in State.ThreadFor(selected.Id))
        {
            var fromCrew = line.Who == "crew";
            Thread.Add(new ChatBubble
            {
                Text = line.Text,
                Meta = $"{(fromCrew ? selected.Master.Split(' ').Last() : "Dispatch")} · {PortState.Format(line.Hour)}",
                Align = fromCrew ? HorizontalAlignment.Left : HorizontalAlignment.Right,
                Background = fromCrew ? Tokens.Brush("SurfaceSunkAltBrush") : Tokens.Brush("InkBrush"),
                Foreground = fromCrew ? Tokens.Brush("InkBrush") : Tokens.Brush("PaperBrush")
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
            QuickMessages.Add(new QuickMessage
            {
                Label = label,
                Text = text,
                Send = new RelayCommand<string>(message => State.Send(selected.Id, message ?? string.Empty))
            });
        }

        Bindings.Update();
    }

    private void OnSendClick(object sender, RoutedEventArgs e) => SendDraft();

    private void OnChatKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            SendDraft();
            e.Handled = true;
        }
    }

    private void SendDraft()
    {
        var selected = PortData.RiverFleet.FirstOrDefault(v => v.Id == State.FleetSelection)
                       ?? PortData.RiverFleet[0];
        State.Send(selected.Id, ChatBox.Text);
        ChatBox.Text = string.Empty;
    }
}
