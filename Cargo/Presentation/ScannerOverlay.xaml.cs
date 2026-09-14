using Cargo.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Cargo.Presentation;

public sealed class ManifestRow
{
    public required string Name { get; init; }
    public required Brush Tone { get; init; }
    public required string Flag { get; init; }
    public required Brush FlagTone { get; init; }
}

public sealed class ScanViewOption
{
    public required string Label { get; init; }
    public required Brush Background { get; init; }
}

/// <summary>
/// The full-window container inspection. It lives in the shell rather than inside the
/// Containers page so it covers the whole app the way the design's fixed overlay does.
/// </summary>
public sealed partial class ScannerOverlay : UserControl
{
    private static readonly (string Label, double Rx, double Ry)[] ViewAngles =
    {
        ("Iso", -18, -32), ("Front", 0, 0), ("Side", 0, -90), ("Top", -88, 0)
    };

    public ScannerOverlay(PortState state)
    {
        State = state;
        ScanFacts = new ObservableCollection<Fact>();
        Manifest = new ObservableCollection<ManifestRow>();
        Density = new ObservableCollection<CompositionSlice>();
        Views = new ObservableCollection<ScanViewOption>();

        InitializeComponent();
        Scanner3D.State = state;

        Refresh();
        this.RebuildWhenVisible(state, Refresh);
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(PortState.Xray) or nameof(PortState.ScanRotationX)
                or nameof(PortState.ScanRotationY) or nameof(PortState.SelectedContainerId))
            {
                Refresh();
            }
        };
    }

    public PortState State { get; }

    public ObservableCollection<Fact> ScanFacts { get; }
    public ObservableCollection<ManifestRow> Manifest { get; }
    public ObservableCollection<CompositionSlice> Density { get; }
    public ObservableCollection<ScanViewOption> Views { get; }

    public string ContainerId { get; private set; } = string.Empty;
    public string ContainerMeta { get; private set; } = string.Empty;
    public string ScanMode { get; private set; } = string.Empty;
    public string Verdict { get; private set; } = string.Empty;
    public string VerdictNote { get; private set; } = string.Empty;
    public Brush VerdictTone { get; private set; } = Tokens.Transparent;
    public string ManifestHeading { get; private set; } = string.Empty;
    public string ScanAction { get; private set; } = string.Empty;
    public Brush XrayBackground { get; private set; } = Tokens.Transparent;
    public Brush XrayBorder { get; private set; } = Tokens.Transparent;
    public Brush XrayForeground { get; private set; } = Tokens.Transparent;

    private void Refresh()
    {
        // The overlay stays in the tree behind a Visibility flag, so skip the work when shut.
        if (!State.ScannerOpen)
        {
            return;
        }

        var container = State.SelectedContainer;

        ContainerId = $"{container.Id[..4]} {container.Id[4..10]} {container.Id[10..]}";
        ContainerMeta = $"{container.Size} · {container.Carrier} · {container.Route}";

        ScanMode = State.Xray ? "X-RAY · 160 kV · SCANNING" : "OPTICAL · 3D INSPECTION";
        XrayBackground = State.Xray ? Tokens.Brush("TealBrightColor", 0.2) : Tokens.Transparent;
        XrayBorder = State.Xray ? Tokens.Brush("TealBrightBrush") : Tokens.Brush("HairlineOnDarkBrush");
        XrayForeground = State.Xray ? Tokens.Brush("TealPaleBrush") : Tokens.Brush("TextOnDarkBrush");

        Verdict = container.Warn ? "Anomaly detected" : container.Stage == 3 ? "Under review" : "Clear";
        VerdictTone = Tokens.Brush(container.SecurityToken);
        VerdictNote = container.Warn
            ? "Dense unlisted mass at bay 3 · measured weight +1.8 t over manifest. Physical inspection required before release."
            : container.Stage == 3
                ? "Documentary review in progress · imaging consistent with manifest."
                : "Imaging consistent with manifest · seal intact · weight within tolerance.";
        ScanAction = container.Warn ? "Open inspection →" : "Log scan →";

        var manifest = PortData.Manifests.TryGetValue(container.Id, out var listed)
            ? listed
            : PortData.DefaultManifest;

        ManifestHeading = $"MANIFEST · {manifest.Count} ITEMS";

        Manifest.Clear();
        foreach (var item in manifest)
        {
            Manifest.Add(new ManifestRow
            {
                Name = item.Name,
                Tone = Tokens.Brush(item.TintToken),
                Flag = item.Flagged ? "FLAG" : "ok",
                FlagTone = Tokens.Brush(item.Flagged ? "ScanAnomalyColor" : "SeaGreenColor")
            });
        }

        BuildDensity(manifest);

        ScanFacts.Clear();
        foreach (var (key, value, tone) in new (string, string, string?)[]
                 {
                     ("Seal", container.Seal, null),
                     ("Gross weight", container.Weight, container.Warn ? "ScanOrganicColor" : null),
                     ("Customs", container.Customs,
                         container.Customs is "Cleared" or "Pre-cleared" ? "ScanMixedColor" : "ScanOrganicColor"),
                     ("Inspection", container.Inspection, container.Warn ? "ScanOrganicColor" : null),
                     ("Vessel", container.Vessel, null),
                     ("Yard slot", container.YardSlot, null)
                 })
        {
            ScanFacts.Add(new Fact { Key = key, Value = value, Tone = tone is null ? null : Tokens.Brush(tone) });
        }

        Views.Clear();
        foreach (var (label, rx, ry) in ViewAngles)
        {
            var current = Math.Abs(State.ScanRotationX - rx) < 0.5 && Math.Abs(State.ScanRotationY - ry) < 0.5;
            Views.Add(new ScanViewOption
            {
                Label = label,
                Background = current ? Tokens.Brush("DeckWhiteColor", 0.16) : Tokens.Transparent
            });
        }

        Bindings.Update();
    }

    private void BuildDensity(IReadOnlyList<CargoItem> manifest)
    {
        (string Label, string Token)[] groups =
        {
            ("Organic", "ScanOrganicColor"),
            ("Metallic", "ScanMetallicColor"),
            ("Mixed / low density", "ScanMixedColor"),
            ("Anomaly · dense", "ScanAnomalyColor")
        };

        Density.Clear();
        DensityBar.Items.Clear();

        var bar = new Grid { ColumnSpacing = 2 };

        foreach (var (label, token) in groups)
        {
            var count = manifest.Count(item => item.TintToken == token);
            if (count == 0)
            {
                continue;
            }

            var percent = (int)Math.Round(count * 100d / manifest.Count);
            var brush = Tokens.Brush(token);

            Density.Add(new CompositionSlice
            {
                Label = label,
                Percent = percent,
                Fill = brush,
                Count = $"{percent}%"
            });

            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(percent, GridUnitType.Star) });
            var block = new Rectangle { Fill = brush };
            Grid.SetColumn(block, bar.ColumnDefinitions.Count - 1);
            bar.Children.Add(block);
        }

        DensityBar.Items.Add(new Border { CornerRadius = new CornerRadius(5), Height = 10, Child = bar });
    }

    // ── Controls ──────────────────────────────────────────────────────────────

    private void OnClose(object sender, RoutedEventArgs e) => State.ScannerOpen = false;

    private void OnToggleXray(object sender, RoutedEventArgs e) => State.Xray = !State.Xray;

    private void OnOpenInspection(object sender, RoutedEventArgs e)
    {
        State.ScannerOpen = false;
        State.OpenInspectionCommand.Execute(null);
    }

    private void OnViewClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string label })
        {
            return;
        }

        var (_, rx, ry) = ViewAngles.First(v => v.Label == label);
        State.ScanRotationX = rx;
        State.ScanRotationY = ry;
    }

    private void OnZoomIn(object sender, RoutedEventArgs e) => State.ScanZoom = Math.Min(1.8, State.ScanZoom + 0.15);

    private void OnZoomOut(object sender, RoutedEventArgs e) => State.ScanZoom = Math.Max(0.6, State.ScanZoom - 0.15);

    private void OnResetView(object sender, RoutedEventArgs e)
    {
        State.ScanZoom = 1;
        State.ScanRotationX = -18;
        State.ScanRotationY = -32;
    }
}
