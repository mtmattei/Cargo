using Cargo.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Cargo.Presentation;

public sealed class TierRow
{
    public required string Id { get; init; }
    public required string Detail { get; init; }
    public required Brush Tone { get; init; }
}

public sealed partial class YardView : UserControl
{
    private readonly YardMap _map;
    private readonly StackTiers _tiers = new();

    public YardView(PortState state)
    {
        State = state;
        Tiers = new ObservableCollection<TierRow>();

        Legend = new[]
        {
            new LegendItem { Label = "Standard", Swatch = Tokens.Brush("CargoStandardBrush") },
            new LegendItem { Label = "Refrigerated", Swatch = Tokens.Brush("CargoReeferBrush") },
            new LegendItem { Label = "Hazardous", Swatch = Tokens.Brush("CargoHazardBrush") },
            new LegendItem { Label = "Inspection", Swatch = Tokens.Brush("CargoOversizeBrush") },
            new LegendItem { Label = "Restricted", Swatch = Tokens.Brush("RestrictedBrush") },
            new LegendItem { Label = "Available slot", Swatch = Tokens.Brush("InkColor", 0.12) }
        };

        InitializeComponent();

        _map = new YardMap(state);
        MapHost.Content = _map;
        TiersHost.Content = _tiers;

        Refresh();
        this.RebuildWhenVisible(state, Refresh);
        this.RebuildOnYardMove(state, Refresh);
    }

    public PortState State { get; }

    public IReadOnlyList<LegendItem> Legend { get; }

    public ObservableCollection<TierRow> Tiers { get; }

    public string LiveLine { get; private set; } = string.Empty;
    public string OccupancyLine { get; private set; } = string.Empty;
    public string SlotTitle { get; private set; } = string.Empty;
    public string SlotSubtitle { get; private set; } = string.Empty;

    private void Refresh()
    {
        LiveLine = $"Live · {State.YardMoves:N0} moves today · 3 RTGs ·";
        OccupancyLine = $"{State.YardOccupancy}% occupied";

        BuildOccupancyTicks();
        BuildStack();

        Bindings.Update();
    }

    private void BuildOccupancyTicks()
    {
        OccupancyTicks.ItemsPanel ??= (ItemsPanelTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(
            "<ItemsPanelTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">" +
            "<StackPanel Orientation=\"Horizontal\" VerticalAlignment=\"Bottom\" /></ItemsPanelTemplate>");

        OccupancyTicks.Items.Clear();
        for (var i = 0; i < 20; i++)
        {
            var lit = i / 20d * 100 < State.YardOccupancy;
            OccupancyTicks.Items.Add(new Rectangle
            {
                Width = 3,
                Height = lit ? i % 5 == 0 ? 10 : 7 : 4.5,
                Margin = new Thickness(0, 0, 1.5, 0),
                RadiusX = 1,
                RadiusY = 1,
                VerticalAlignment = VerticalAlignment.Bottom,
                Fill = lit ? Tokens.Brush("TealBrush") : Tokens.Brush("InkColor", 0.12)
            });
        }
    }

    private void BuildStack()
    {
        if (State.SelectedStack is not { } key)
        {
            StackPanelCard.Visibility = Visibility.Collapsed;
            PanelColumn.Width = new GridLength(0);
            return;
        }

        var slot = YardMap.Slots().FirstOrDefault(s => s.Key == key);
        if (slot.Key is null)
        {
            return;
        }

        var height = State.YardHeight(key, slot.Base);

        StackPanelCard.Visibility = Visibility.Visible;
        PanelColumn.Width = new GridLength(300);

        SlotTitle = $"Slot {slot.Slot}";
        SlotSubtitle = $"{slot.Zone.Name} · {slot.Zone.Sub} · {height} of 5 tiers";

        // Each tier gets a plausible box: reefers stay reefers, hazmat keeps its IMDG class
        var boxes = new List<(string Id, string Tag, string Token)>();
        Tiers.Clear();

        for (var i = 0; i < height; i++)
        {
            var seed = (int)(slot.X * 3 + slot.Y) + i * 11;
            var tag = slot.Zone.TintToken switch
            {
                "CargoReeferColor" => "Reefer −18°C",
                "CargoHazardColor" => $"IMDG class {3 + (int)(Geo.H(seed) * 6)}",
                "CargoOversizeColor" => "Awaiting exam",
                "RestrictedColor" => "Bonded",
                _ => Geo.H(seed) > 0.7 ? "Empty" : "Laden"
            };

            var token = tag == "Empty" ? "CargoEmptyColor" : slot.Zone.TintToken;
            var id = $"{PortData.ContainerPrefixes[(int)(Geo.H(seed) * 8)]} " +
                     $"{(int)(Geo.H(seed + 5) * 900000 + 100000)} {(int)(Geo.H(seed + 9) * 10)}";

            boxes.Add((id, tag, token));
            Tiers.Add(new TierRow
            {
                Id = id,
                Detail = $"Tier {i + 1} · {tag}",
                Tone = Tokens.Brush(token)
            });
        }

        _tiers.Show(boxes);
    }

    private void OnCloseStack(object sender, RoutedEventArgs e) => State.SelectedStack = null;
}
