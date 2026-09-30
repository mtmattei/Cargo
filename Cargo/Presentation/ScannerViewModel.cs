using Microsoft.UI.Xaml.Media;

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
    public required System.Windows.Input.ICommand Select { get; init; }
}

/// <summary>
/// The full-window container inspection: verdict, manifest, density and the 3D view's
/// presets. The overlay stays in the shell behind <see cref="PortState.ScannerOpen"/>, so
/// this model does its work only while the scanner is open. Created by
/// <see cref="MainViewModel"/> on the UI thread.
/// </summary>
public sealed partial class ScannerViewModel : ObservableObject
{
    private static readonly (string Label, double Rx, double Ry)[] ViewAngles =
    {
        ("Iso", -18, -32), ("Front", 0, 0), ("Side", 0, -90), ("Top", -88, 0)
    };

    private int _currentView = -2;

    public ScannerViewModel(PortState state)
    {
        State = state;
        Refresh();
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(PortState.Xray) or nameof(PortState.SelectedContainerId)
                or nameof(PortState.ScannerOpen))
            {
                Refresh();
            }
            else if (e.PropertyName is nameof(PortState.ScanRotationX) or nameof(PortState.ScanRotationY))
            {
                // A drag changes the angles on every pointer move; only the preset highlight depends on them
                RefreshViews();
            }
        };
    }

    public PortState State { get; }

    public ObservableCollection<Fact> ScanFacts { get; } = new();
    public ObservableCollection<ManifestRow> Manifest { get; } = new();
    public ObservableCollection<CompositionSlice> Density { get; } = new();
    public ObservableCollection<ScanViewOption> Views { get; } = new();

    [ObservableProperty] private string _containerId = string.Empty;
    [ObservableProperty] private string _containerMeta = string.Empty;
    [ObservableProperty] private string _scanMode = string.Empty;
    [ObservableProperty] private string _verdict = string.Empty;
    [ObservableProperty] private string _verdictNote = string.Empty;
    [ObservableProperty] private Brush? _verdictTone;
    [ObservableProperty] private string _manifestHeading = string.Empty;
    [ObservableProperty] private string _scanAction = string.Empty;
    [ObservableProperty] private Brush? _xrayBackground;
    [ObservableProperty] private Brush? _xrayBorder;
    [ObservableProperty] private Brush? _xrayForeground;
    [ObservableProperty] private IReadOnlyList<(double Weight, Brush Fill)> _densitySlices = [];

    [RelayCommand]
    private void Close() => State.ScannerOpen = false;

    [RelayCommand]
    private void ToggleXray() => State.Xray = !State.Xray;

    [RelayCommand]
    private void OpenInspection()
    {
        State.ScannerOpen = false;
        State.OpenInspectionCommand.Execute(null);
    }

    [RelayCommand]
    private void ShowView(string? label)
    {
        if (ViewAngles.FirstOrDefault(v => v.Label == label) is not { Label: not null } view)
        {
            return;
        }

        State.ScanRotationX = view.Rx;
        State.ScanRotationY = view.Ry;
    }

    [RelayCommand]
    private void ZoomIn() => State.ScanZoom = Math.Min(1.8, State.ScanZoom + 0.15);

    [RelayCommand]
    private void ZoomOut() => State.ScanZoom = Math.Max(0.6, State.ScanZoom - 0.15);

    [RelayCommand]
    private void ResetView()
    {
        State.ScanZoom = 1;
        State.ScanRotationX = -18;
        State.ScanRotationY = -32;
    }

    public void Refresh()
    {
        // The overlay stays in the tree behind a Visibility flag, so skip the work while shut
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

        _currentView = -2;
        RefreshViews();
    }

    /// <summary>Rebuilds the view presets only when the highlighted preset actually changes.</summary>
    private void RefreshViews()
    {
        if (!State.ScannerOpen)
        {
            return;
        }

        var current = Array.FindIndex(ViewAngles, v =>
            Math.Abs(State.ScanRotationX - v.Rx) < 0.5 && Math.Abs(State.ScanRotationY - v.Ry) < 0.5);
        if (current == _currentView)
        {
            return;
        }

        _currentView = current;
        Views.Clear();
        for (var i = 0; i < ViewAngles.Length; i++)
        {
            Views.Add(new ScanViewOption
            {
                Label = ViewAngles[i].Label,
                Background = i == current ? Tokens.Brush("DeckWhiteColor", 0.16) : Tokens.Transparent,
                Select = ShowViewCommand
            });
        }
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
        var slices = new List<(double, Brush)>();

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
                Fill = brush,
                Count = $"{percent}%"
            });
            slices.Add((percent, brush));
        }

        DensitySlices = slices;
    }
}
