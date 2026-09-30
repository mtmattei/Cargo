namespace Cargo.Presentation;

/// <summary>Model for the Fleet route. It carries the shared store until the page's own state moves in.</summary>
public sealed class FleetViewModel(PortState state)
{
    public PortState State { get; } = state;
}
