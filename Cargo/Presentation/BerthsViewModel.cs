using Microsoft.UI.Xaml.Media;

namespace Cargo.Presentation;

/// <summary>
/// Berths: the berth plan above the vessel you are working. The masthead counts live here;
/// the plan and the vessel panel each have their own model.
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
            Plan = new DockingViewModel(state);
            Vessel = new VesselsViewModel(state);
            RefreshMasthead();
            state.StructureChanged += (_, _) => RefreshMasthead();
        });
    }

    public PortState State { get; }

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

    private void RefreshMasthead()
    {
        var count = State.Conflicts().Count;
        Conflicts = count.ToString();
        ConflictInk = count > 0 ? Tokens.Brush("AlertInvariantBrush") : Tokens.Brush("InkInvariantBrush");
    }
}
