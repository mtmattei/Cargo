
namespace Cargo.Domain;

/// <summary>
/// Everything the harbour knows about itself right now: which section is on screen,
/// what is selected where, which vessels have been given a berth, and the live clock
/// that drives the countdowns and the crane counters.
/// </summary>
public sealed partial class PortState : ObservableObject
{
    private readonly DispatcherTimer _clock;
    private readonly DateTimeOffset _started = DateTimeOffset.Now;
    private readonly Dictionary<string, string> _assigned = new();
    private readonly Dictionary<string, List<ChatLine>> _sentMessages = new();

    private DateTimeOffset _lastYardShuffle;
    private readonly Dictionary<string, int> _yardDelta = new();
    private readonly Dictionary<string, DateTimeOffset> _yardRecent = new();

    public PortState()
    {
#if DEBUG
        // Lets a verification run open straight onto a section without synthesized input,
        // which Windows will not let a background process deliver reliably.
        if (Environment.GetEnvironmentVariable("CARGO_START_SECTION") is { Length: > 0 } requested
            && Canonical(requested) is var start
            && PortData.Sections.Any(s => s.Id == start))
        {
            _section = start;
        }

        if (Environment.GetEnvironmentVariable("CARGO_SECURITY_TAB") is { Length: > 0 } tab)
        {
            _securityTab = tab;
        }

        // Panels that only exist once something is selected, so a headless run can see them.
        _dockSelection = Environment.GetEnvironmentVariable("CARGO_DOCK_SELECT");
        _selectedStack = Environment.GetEnvironmentVariable("CARGO_STACK");
        _scannerOpen = Environment.GetEnvironmentVariable("CARGO_SCANNER") == "1";
        _xray = Environment.GetEnvironmentVariable("CARGO_XRAY") == "1";
        _harbourLayer = Environment.GetEnvironmentVariable("CARGO_LAYER") ?? _harbourLayer;

        // Hover states cannot be reached with synthesized input from a background process.
        if (int.TryParse(Environment.GetEnvironmentVariable("CARGO_HOVER_BERTH"), out var berth))
        {
            _hoveredBerth = berth;
        }

        if (Environment.GetEnvironmentVariable("CARGO_OPTIMIZE") == "1")
        {
            Optimize();
        }
#endif

        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => Tick();
        _clock.Start();
    }

    /// <summary>Raised once a second, after the clock advances.</summary>
    public event EventHandler? Ticked;

    /// <summary>Raised when something structural changed (selection, assignment, filter).</summary>
    public event EventHandler? StructureChanged;

    /// <summary>
    /// Raised when only the pointer moved over something. Hover changes fire far more often
    /// than anything else and never change what is on screen, only how one element is lit,
    /// so they are kept off <see cref="StructureChanged"/> and repaint in place instead.
    /// </summary>
    public event EventHandler? HoverChanged;

    /// <summary>
    /// Raised when the yard works a stack. It fires on its own every few seconds whether or
    /// not anyone is looking at the yard, so it stays off <see cref="StructureChanged"/>:
    /// one slot changing height is no reason to rebuild the harbour and every other screen.
    /// </summary>
    public event EventHandler? YardChanged;

    private void Tick()
    {
        ShuffleYard();
        OnPropertyChanged(nameof(Now));
        OnPropertyChanged(nameof(NowMinutes));
        OnPropertyChanged(nameof(NowSeconds));
        OnPropertyChanged(nameof(NowHours));
        Ticked?.Invoke(this, EventArgs.Empty);
    }

    public void NotifyStructureChanged() => StructureChanged?.Invoke(this, EventArgs.Empty);

    private void NotifyHoverChanged() => HoverChanged?.Invoke(this, EventArgs.Empty);

    private void NotifyYardChanged() => YardChanged?.Invoke(this, EventArgs.Empty);

    // ── Clock ─────────────────────────────────────────────────────────────────

    public double NowHours => PortData.NowHours + (DateTimeOffset.Now - _started).TotalHours;

    public double ElapsedSeconds => (DateTimeOffset.Now - _started).TotalSeconds;

    public string Now => FormatWithSeconds(NowHours);

    /// <summary>The header clock's rolling part, "20:58".</summary>
    public string NowMinutes => Now[..5];

    /// <summary>The header clock's plain part, ":34".</summary>
    public string NowSeconds => Now[5..];

    /// <summary>Container moves in the hour so far: the hour's planned volume scaled by how
    /// far into it we are, with a little crane-to-crane jitter.</summary>
    public static int MovesAt(double hours)
    {
        var hour = (int)Math.Floor(hours);
        var fraction = hours - hour;
        var moves = (int)Math.Round(PortData.Volume[Math.Clamp(hour, 0, 24)] * fraction + Math.Sin(hours * 97) * 3);
        return Math.Max(0, moves);
    }

    public static string Format(double hours)
    {
        var s = (int)Math.Round(hours * 3600) % 86400;
        if (s < 0)
        {
            s += 86400;
        }

        return $"{s / 3600:D2}:{s % 3600 / 60:D2}";
    }

    public static string FormatWithSeconds(double hours)
    {
        var s = (int)Math.Round(hours * 3600) % 86400;
        if (s < 0)
        {
            s += 86400;
        }

        return $"{s / 3600:D2}:{s % 3600 / 60:D2}:{s % 60:D2}";
    }

    /// <summary>"in 12:04" / "in 2 h 15 min" / "now".</summary>
    public string Countdown(double targetHour)
    {
        var delta = targetHour - NowHours;
        if (delta <= 0)
        {
            return "now";
        }

        var minutes = (int)Math.Floor(delta * 60);
        var seconds = (int)Math.Floor(delta * 3600) % 60;
        return minutes >= 60
            ? $"in {minutes / 60} h {minutes % 60} min"
            : $"in {minutes}:{seconds:D2}";
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    [ObservableProperty]
    private string _section = "overview";

    partial void OnSectionChanged(string value)
    {
        StageOpen = false;
        OnPropertyChanged(nameof(ShowStage));
        OnPropertyChanged(nameof(StageFull));
        OnPropertyChanged(nameof(StageCompact));
        OnPropertyChanged(nameof(ShowStageToggle));
        NotifyStructureChanged();
    }

    [RelayCommand]
    private void Go(string section) => Section = Canonical(section);

    /// <summary>
    /// Maps a retired section id onto the page that now holds it (Vessels and Docking live on
    /// Berths, Containers and Yard on Cargo, Activity on Overview), so a DEBUG start hook or an
    /// old link still lands in the right place.
    /// </summary>
    public static string Canonical(string section) => section switch
    {
        "vessels" or "docking" => "berths",
        "containers" or "yard" => "cargo",
        "activity" => "overview",
        _ => section
    };

    [RelayCommand]
    private void OpenVessel(string vesselId)
    {
        SelectedVesselId = vesselId;
        SelectedBay = null;
        Section = "berths";
    }

    [RelayCommand]
    private void OpenDocking()
    {
        DockSelection = SelectedVesselId;
        Section = "berths";
    }

    [RelayCommand]
    private void OpenInspection()
    {
        SecurityTab = "inspection";
        Section = "security";
    }

    [RelayCommand]
    private void OpenSecurity()
    {
        SecurityTab = "zones";
        Section = "security";
    }

    // ── Shared harbour stage ──────────────────────────────────────────────────

    public bool ShowStage => Section is "overview" or "berths" or "cargo" or "security";

    public bool StageFull => Section == "overview" || StageOpen;

    public bool StageCompact => ShowStage && !StageFull;

    [ObservableProperty]
    private bool _stageOpen;

    partial void OnStageOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(StageFull));
        OnPropertyChanged(nameof(StageCompact));
        OnPropertyChanged(nameof(StageToggleLabel));
        NotifyStructureChanged();
    }

    public string StageToggleLabel => StageOpen ? "Collapse harbour" : "Expand harbour";

    /// <summary>Overview always shows the harbour in full; the other two can fold it away.</summary>
    public bool ShowStageToggle => ShowStage && Section != "overview";

    [RelayCommand]
    private void ToggleStage() => StageOpen = !StageOpen;

    [ObservableProperty]
    private string _harbourLayer = "port";

    partial void OnHarbourLayerChanged(string value) => NotifyStructureChanged();

    [RelayCommand]
    private void SetLayer(string layer) => HarbourLayer = layer;

    [ObservableProperty]
    private string? _harbourSelection;

    partial void OnHarbourSelectionChanged(string? value) => NotifyStructureChanged();

    [ObservableProperty]
    private int? _hoveredBerth;

    partial void OnHoveredBerthChanged(int? value) => NotifyHoverChanged();

    /// <summary>The vessel a list row, tag or the needs-you bar is pointing at. Repaints, never rebuilds.</summary>
    [ObservableProperty]
    private string? _hoveredVessel;

    partial void OnHoveredVesselChanged(string? value) => NotifyHoverChanged();

    /// <summary>
    /// Linked hover from a list row, a harbour tag or the needs-you bar. The key is a vessel id or
    /// a berth number; "-key" ends that hover, and only if it is still the current one, since the
    /// next row's enter can arrive before the last row's leave.
    /// </summary>
    [RelayCommand]
    private void Hover(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        if (key[0] == '-')
        {
            if (HoverKey == key[1..])
            {
                HoveredVessel = null;
                HoveredBerth = null;
            }

            return;
        }

        var berth = PortData.Berths.Select(b => b.Number).ToList().IndexOf(key);
        HoveredVessel = berth < 0 ? key : null;
        HoveredBerth = berth < 0 ? null : berth;
    }

    private string? HoverKey => HoveredVessel ?? (HoveredBerth is { } i ? PortData.Berths[i].Number : null);

    /// <summary>Clicking a hull means different things depending on which screen is up.</summary>
    [RelayCommand]
    private void PickHullVessel(string vesselId)
    {
        switch (Section)
        {
            case "berths":
                SelectedVesselId = vesselId;
                SelectedBay = null;
                break;
            case "security":
                OpenVessel(vesselId);
                break;
            default:
                HarbourSelection = HarbourSelection == vesselId ? null : vesselId;
                break;
        }
    }

    // ── Vessels ───────────────────────────────────────────────────────────────

    [ObservableProperty]
    private string _selectedVesselId = "aurora";

    partial void OnSelectedVesselIdChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedVessel));
        NotifyStructureChanged();
    }

    public Vessel SelectedVessel => PortData.Vessel(SelectedVesselId);

    /// <summary>A berth picked in the Berths status list with no vessel on it; null when a vessel is picked.</summary>
    [ObservableProperty]
    private string? _selectedBerth;

    partial void OnSelectedBerthChanged(string? value) => NotifyStructureChanged();

    /// <summary>Picks a vessel on the Berths page (the harbour frames it), clearing any berth pick.</summary>
    public void PickVessel(string vesselId)
    {
        SelectedBerth = null;
        SelectedVesselId = vesselId;
    }

    [ObservableProperty]
    private int? _selectedBay;

    partial void OnSelectedBayChanged(int? value) => NotifyStructureChanged();

    [ObservableProperty]
    private int? _hoveredBay;

    partial void OnHoveredBayChanged(int? value) => NotifyHoverChanged();

    // ── Berth assignment ──────────────────────────────────────────────────────

    public string? BerthOf(string vesselId) =>
        _assigned.TryGetValue(vesselId, out var assigned)
            ? assigned
            : PortData.Berths.FirstOrDefault(b => b.Occupant == vesselId)?.Number;

    public bool IsAssigned(string vesselId) => _assigned.ContainsKey(vesselId);

    public Vessel? OccupantAt(string berth, double hour) => PortData.Vessels.FirstOrDefault(v =>
        BerthOf(v.Id) == berth && PortData.Schedule[v.Id].Start <= hour && PortData.Schedule[v.Id].End > hour);

    public Vessel? IncomingAt(string berth, double hour) => PortData.Vessels.FirstOrDefault(v =>
        BerthOf(v.Id) == berth && PortData.Schedule[v.Id].Start > hour);

    public IReadOnlyList<(string Berth, string A, string B)> Conflicts()
    {
        var found = new List<(string, string, string)>();
        foreach (var berth in PortData.Berths)
        {
            var here = PortData.Vessels.Where(v => BerthOf(v.Id) == berth.Number).ToList();
            for (var i = 0; i < here.Count; i++)
            {
                for (var j = i + 1; j < here.Count; j++)
                {
                    var a = PortData.Schedule[here[i].Id];
                    var b = PortData.Schedule[here[j].Id];
                    if (a.Start < b.End && b.Start < a.End)
                    {
                        found.Add((berth.Number, here[i].Name, here[j].Name));
                    }
                }
            }
        }

        return found;
    }

    public bool Fits(Vessel vessel, int berthIndex)
    {
        if (berthIndex < 0 || berthIndex >= PortData.Berths.Count)
        {
            return false;
        }

        var berth = PortData.Berths[berthIndex];
        if (berth.State == "restricted" || vessel.Draft > berth.Depth)
        {
            return false;
        }

        var window = PortData.Schedule[vessel.Id];
        return !PortData.Vessels
            .Where(o => o.Id != vessel.Id && BerthOf(o.Id) == berth.Number)
            .Any(o =>
            {
                var other = PortData.Schedule[o.Id];
                return window.Start < other.End && other.Start < window.End;
            });
    }

    /// <summary>Returns the message to show in the map hint strip.</summary>
    public string? Assign(string vesselId, int berthIndex)
    {
        var vessel = PortData.Vessel(vesselId);
        var berth = PortData.Berths[berthIndex];

        if (Fits(vessel, berthIndex))
        {
            _assigned[vesselId] = berth.Number;
            NotifyStructureChanged();
            return $"{vessel.Name} assigned to Berth {berth.Number} · pilots and mooring gang notified.";
        }

        return vessel.Draft > berth.Depth
            ? $"Berth {berth.Number} is too shallow for {vessel.Name} ({vessel.Draft} m draft · {berth.Depth} m depth)."
            : $"Berth {berth.Number} is taken during {vessel.Name}'s window.";
    }

    public void Unassign(string vesselId)
    {
        _assigned.Remove(vesselId);
        NotifyStructureChanged();
    }

    // ── Decisions ─────────────────────────────────────────────────────────────

    private readonly HashSet<string> _confirmed = new();

    /// <summary>
    /// The arrival waiting on the dispatcher: a vessel still arriving at a reserved berth whose
    /// berth nobody has confirmed. In the demo data that is Nordic Star to berth 07.
    /// </summary>
    public string? PendingDecision => PortData.Vessels
        .Where(v => v.Status == "Arriving" && !_confirmed.Contains(v.Id))
        .FirstOrDefault(v => BerthOf(v.Id) is { } berth && PortData.Berths.Any(b => b.Number == berth && b.State == "reserved"))
        ?.Id;

    public bool NeedsDecision => PendingDecision is not null;

    private static bool HasHold => PortData.Containers.Any(c => c.Security == "Hold");
    private static bool HasDriverCheck => PortData.Vehicles.Any(v => v.Authorization == "Awaiting driver ID");
    private static bool HasLateOrders => PortData.RiverFleet.Any(v => v.SlipMinutes > 0);

    /// <summary>
    /// Whether a section holds something waiting on the dispatcher, for the header's needs-you
    /// dots: Berths the unconfirmed arrival, Cargo the late orders, Security the inspection hold
    /// and the driver ID check.
    /// </summary>
    public bool SectionNeedsYou(string section) => section switch
    {
        "berths" => NeedsDecision,
        "cargo" => HasLateOrders,
        "security" => HasHold || HasDriverCheck,
        _ => false
    };

    /// <summary>What needs you, counted as the queue lists it: the berth, the hold, the driver check, the late orders.</summary>
    public int NeedsYouCount => (NeedsDecision ? 1 : 0) + (HasHold ? 1 : 0) + (HasDriverCheck ? 1 : 0) + (HasLateOrders ? 1 : 0);

    public bool IsConfirmed(string vesselId) => _confirmed.Contains(vesselId);

    /// <summary>
    /// Confirms the vessel's reserved berth as a real assignment, so the planner, the conflict
    /// check and the harbour all follow. Returns null on success, or why the berth cannot take it.
    /// </summary>
    public string? Confirm(string vesselId)
    {
        var berth = BerthOf(vesselId);
        var index = PortData.Berths.ToList().FindIndex(b => b.Number == berth);
        if (index < 0)
        {
            return $"{PortData.Vessel(vesselId).Name} has no berth to confirm.";
        }

        if (!Fits(PortData.Vessel(vesselId), index))
        {
            return Assign(vesselId, index);
        }

        Assign(vesselId, index);
        _confirmed.Add(vesselId);
        OnDecisionChanged();
        return null;
    }

    /// <summary>Reverses <see cref="Confirm"/>: the berth goes back to reserved and the decision is pending again.</summary>
    public void UndoConfirm(string vesselId)
    {
        if (_confirmed.Remove(vesselId))
        {
            Unassign(vesselId);
            OnDecisionChanged();
        }
    }

    private void OnDecisionChanged()
    {
        OnPropertyChanged(nameof(PendingDecision));
        OnPropertyChanged(nameof(NeedsDecision));
        OnPropertyChanged(nameof(NeedsYouCount));
    }

    /// <summary>Give every movable vessel the shallowest berth that still clears its draft.</summary>
    public string Optimize()
    {
        var conflicts = Conflicts();
        var movable = PortData.Vessels
            .Where(v => v.Status != "Docked" && v.Status != "Departing")
            .Where(v => BerthOf(v.Id) is null || conflicts.Any(c => c.A == v.Name || c.B == v.Name))
            .ToList();

        foreach (var vessel in movable)
        {
            var window = PortData.Schedule[vessel.Id];
            var candidates = Enumerable.Range(0, PortData.Berths.Count)
                .Where(i =>
                {
                    var berth = PortData.Berths[i];
                    if (berth.State == "restricted" || vessel.Draft > berth.Depth)
                    {
                        return false;
                    }

                    return !PortData.Vessels.Any(o =>
                    {
                        if (o.Id == vessel.Id || BerthOf(o.Id) != berth.Number)
                        {
                            return false;
                        }

                        var other = PortData.Schedule[o.Id];
                        return other.Start < window.End && window.Start < other.End;
                    });
                })
                .OrderBy(i => Math.Abs(PortData.Berths[i].Depth - vessel.Draft))
                .ToList();

            if (candidates.Count > 0)
            {
                _assigned[vessel.Id] = PortData.Berths[candidates[0]].Number;
            }
        }

        NotifyStructureChanged();
        return "Optimized: every inbound vessel has a berth that fits its draft, with no overlaps in the next 36 h.";
    }

    // ── Docking planner ───────────────────────────────────────────────────────

    [ObservableProperty]
    private string? _dockSelection;

    partial void OnDockSelectionChanged(string? value) => NotifyStructureChanged();

    /// <summary>Null means "follow the live clock".</summary>
    [ObservableProperty]
    private double? _planHour;

    partial void OnPlanHourChanged(double? value) => NotifyStructureChanged();

    public double EffectivePlanHour => PlanHour ?? NowHours;

    // ── Containers ────────────────────────────────────────────────────────────

    [ObservableProperty]
    private string _containerFilter = "All";

    partial void OnContainerFilterChanged(string value) => NotifyStructureChanged();

    [ObservableProperty]
    private string _selectedContainerId = "MSCU4821937";

    partial void OnSelectedContainerIdChanged(string value) => NotifyStructureChanged();

    public ContainerDef SelectedContainer =>
        PortData.Containers.First(c => c.Id == SelectedContainerId);

    [ObservableProperty]
    private bool _scannerOpen;

    [ObservableProperty]
    private bool _xray;

    partial void OnXrayChanged(bool value) => NotifyStructureChanged();

    [ObservableProperty]
    private double _scanRotationX = -18;

    [ObservableProperty]
    private double _scanRotationY = -32;

    [ObservableProperty]
    private double _scanZoom = 1;

    // ── Yard ──────────────────────────────────────────────────────────────────

    [ObservableProperty]
    private string? _selectedStack;

    partial void OnSelectedStackChanged(string? value) => NotifyStructureChanged();

    public int YardMoves { get; private set; } = 1284;

    public int YardOccupancy { get; set; } = 71;

    /// <summary>Keys the yard renderer registered on its last pass.</summary>
    public IReadOnlyList<(string Key, int Base)> YardKeys { get; set; } = Array.Empty<(string, int)>();

    public int YardHeight(string key, int baseHeight) =>
        Math.Clamp(baseHeight + (_yardDelta.TryGetValue(key, out var d) ? d : 0), 0, 5);

    public bool YardRecentlyMoved(string key) =>
        _yardRecent.TryGetValue(key, out var at) && (DateTimeOffset.Now - at).TotalSeconds < 60;

    private void ShuffleYard()
    {
        if (YardKeys.Count == 0 || (DateTimeOffset.Now - _lastYardShuffle).TotalMilliseconds < 3500)
        {
            return;
        }

        _lastYardShuffle = DateTimeOffset.Now;
        var pick = YardKeys[Random.Shared.Next(YardKeys.Count)];
        var current = YardHeight(pick.Key, pick.Base);
        var delta = current <= 0 ? 1 : current >= 5 ? -1 : Random.Shared.Next(2) == 0 ? -1 : 1;
        _yardDelta[pick.Key] = (_yardDelta.TryGetValue(pick.Key, out var d) ? d : 0) + delta;
        _yardRecent[pick.Key] = DateTimeOffset.Now;
        YardMoves++;
        NotifyYardChanged();
    }

    // ── Security ──────────────────────────────────────────────────────────────

    [ObservableProperty]
    private string _securityTab = "zones";

    partial void OnSecurityTabChanged(string value)
    {
        OnPropertyChanged(nameof(SecurityTitle));
        NotifyStructureChanged();
    }

    public string SecurityTitle => SecurityTab switch
    {
        "access" => "Access control",
        "inspection" => "Cargo inspection",
        _ => "Port security zones"
    };

    [ObservableProperty]
    private int _selectedPerson;

    partial void OnSelectedPersonChanged(int value) => NotifyStructureChanged();

    // ── Fleet ─────────────────────────────────────────────────────────────────

    [ObservableProperty]
    private string _fleetSelection = "al118";

    partial void OnFleetSelectionChanged(string value) => NotifyStructureChanged();

    [ObservableProperty]
    private string _chatDraft = string.Empty;

    public IReadOnlyList<ChatLine> ThreadFor(string vesselId)
    {
        var vessel = PortData.RiverFleet.First(v => v.Id == vesselId);
        return _sentMessages.TryGetValue(vesselId, out var extra)
            ? vessel.Thread.Concat(extra).ToList()
            : vessel.Thread;
    }

    public void Send(string vesselId, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (!_sentMessages.TryGetValue(vesselId, out var list))
        {
            list = new List<ChatLine>();
            _sentMessages[vesselId] = list;
        }

        list.Add(new ChatLine("dispatch", text.Trim(), NowHours));
        ChatDraft = string.Empty;
        NotifyStructureChanged();
    }
}
