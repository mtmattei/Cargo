using Cargo.Controls;

namespace Cargo.Presentation;

/// <summary>Cargo: the container explorer above the yard it lives in.</summary>
public sealed partial class CargoView : UserControl
{
    public CargoView(PortState state)
    {
        State = state;
        InitializeComponent();

        ContainersHost.Content = new ContainersView(state);
        YardHost.Content = new YardView(state);

        this.RebuildOnYardMove(state, Bindings.Update);
    }

    public PortState State { get; }

    public string Occupancy => $"{State.YardOccupancy}%";

    public string Moves => State.YardMoves.ToString("N0");
}
