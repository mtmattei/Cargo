using Cargo.Controls;

namespace Cargo.Presentation;

/// <summary>Cargo: the container explorer above the yard it lives in.</summary>
public sealed partial class CargoView : Page
{
    private bool _attached;

    public CargoView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
    }

    /// <summary>The router builds the page, then hands it its model; the live work starts there.</summary>
    private void Attach()
    {
        if (_attached || DataContext is not CargoViewModel vm)
        {
            return;
        }

        _attached = true;
        State = vm.State;
        var state = vm.State;

        ContainersHost.Content = new ContainersView(state);
        YardHost.Content = new YardView(state);

        this.RebuildOnYardMove(state, Bindings.Update);
        Bindings.Update();
    }

    public PortState State { get; private set; } = null!;

    public string Occupancy => $"{State.YardOccupancy}%";

    public string Moves => State.YardMoves.ToString("N0");
}
