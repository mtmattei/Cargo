namespace Cargo.Presentation;

/// <summary>
/// Berths: the decision waiting on you, the status of every berth, then the berth plan and the
/// vessel you are working. The masthead counts live here; each panel has its own model.
/// </summary>
public sealed partial class BerthsViewModel : ObservableObject
{
    public BerthsViewModel(PortState state, IDispatcher dispatcher)
    {
        State = state;

        // The router builds models off the UI thread; the sub-models build brushes and
        // images as they start, so they are created on it.
        dispatcher.TryEnqueue(() =>
        {
            NeedsYou = new NeedsYouViewModel(state);
            Status = new BerthStatusViewModel(state);
            Detail = new VesselDetailViewModel(state);
            Plan = new DockingViewModel(state);
            Vessel = new VesselsViewModel(state);
            RefreshMasthead();
            state.StructureChanged += (_, _) => RefreshMasthead();
        });
    }

    public PortState State { get; }

    [ObservableProperty]
    private NeedsYouViewModel? _needsYou;

    [ObservableProperty]
    private BerthStatusViewModel? _status;

    [ObservableProperty]
    private VesselDetailViewModel? _detail;

    [ObservableProperty]
    private DockingViewModel? _plan;

    [ObservableProperty]
    private VesselsViewModel? _vessel;

    public string Alongside => PortData.Berths.Count(b => b.State == "occupied").ToString();

    public string Arriving => PortData.Vessels.Count(v => v.Status == "Arriving").ToString();

    public string AtAnchor => PortData.Vessels.Count(v => v.Status == "At anchor").ToString();

    [ObservableProperty]
    private string _conflicts = "0";

    [ObservableProperty]
    private Brush? _conflictInk;

    /// <summary>The decision, status and detail panels; the page runs this only while it is shown.</summary>
    public void RefreshPanels()
    {
        NeedsYou?.Refresh();
        Status?.Refresh();
        Detail?.Refresh();
    }

    public void RepaintHover() => Status?.RepaintLinks();

    private void RefreshMasthead()
    {
        var count = State.Conflicts().Count;
        Conflicts = count.ToString();
        ConflictInk = count > 0 ? Tokens.Brush("AlertInvariantBrush") : Tokens.Brush("InkInvariantBrush");
    }
}
