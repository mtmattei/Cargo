namespace Cargo.Presentation;

/// <summary>
/// Cargo: the container explorer above the yard it lives in. The masthead figures follow
/// the yard's own moves; the explorer and the yard each have their own model.
/// </summary>
public sealed partial class CargoViewModel : ObservableObject
{
    public CargoViewModel(PortState state, IDispatcher dispatcher)
    {
        State = state;

        // The router builds models off the UI thread; the sub-models build brushes and
        // images as they start, so they are created on it.
        dispatcher.TryEnqueue(() =>
        {
            Containers = new ContainersViewModel(state);
            Yard = new YardViewModel(state);
            RefreshMasthead();
            state.YardChanged += (_, _) => RefreshMasthead();
        });
    }

    public PortState State { get; }

    [ObservableProperty]
    private ContainersViewModel? _containers;

    [ObservableProperty]
    private YardViewModel? _yard;

    [ObservableProperty]
    private string _occupancy = string.Empty;

    [ObservableProperty]
    private string _moves = string.Empty;

    private void RefreshMasthead()
    {
        Occupancy = $"{State.YardOccupancy}%";
        Moves = State.YardMoves.ToString("N0");
    }
}
