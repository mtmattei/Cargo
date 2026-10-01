namespace Cargo.Presentation;

public sealed partial class CargoFlowPanels : UserControl
{
    public CargoFlowPanels()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bindings.Update();
    }

    public PortState? ViewModel => DataContext as PortState;

    /// <summary>The moves so far this hour.</summary>
    public static string Moves(double now) => PortState.MovesAt(now).ToString();
}
